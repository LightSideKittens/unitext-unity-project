// UniText GPU Upload — Metal implementation.
//
// Uploads are blit-encoded into *Unity's current command buffer* (the
// IUnityGraphicsMetalV2 in-frame pattern): EndCurrentCommandEncoder() closes
// Unity's in-flight encoder, then a blit encoder opens on
// CurrentCommandBuffer(). Unity commits that buffer at its own submission
// points, so ordering against later shader reads is guaranteed without a
// separate command buffer or manual hazard tracking.
//
// Texture lifetime:
//   Between main-thread IssuePluginEventAndData and render-thread callback
//   execution, the caller (C#) may DestroyImmediate a Texture2DArray. Metal's
//   implicit encoder retain would dereference the already-freed MTLTexture
//   → SIGSEGV. We close the window by having the caller synchronously call
//   ut_gpu_metal_retain_texture on the main thread (bumps MTLTexture refcount)
//   and matching it with CFRelease in a completion handler installed on the
//   command buffer that carries the blit. Every early-out below must release
//   the same set, or the caller-side retain leaks.
//   The retain must target a materialized MTLTexture — callers must have
//   called Apply() (or otherwise forced Metal materialization) before passing
//   the native pointer in.
//
// Compiled as Objective-C++ (.mm) with ARC (-fobjc-arc).

#if defined(__APPLE__)

#import <Metal/Metal.h>
#import <Foundation/Foundation.h>
#import <TargetConditionals.h>
#include <string.h>

#include "unity_plugin_api/IUnityInterface.h"
#include "unity_plugin_api/IUnityGraphics.h"
#include "unity_plugin_api/IUnityGraphicsMetal.h"
#include "gpu_upload_common.h"

#define UTMETAL_EXPORT extern "C" __attribute__((visibility("default")))

static IUnityGraphicsMetalV2* s_Metal = nullptr;
static bool s_UsesManagedStorage = false;

extern "C" void InitMetal(IUnityInterfaces* interfaces)
{
    s_Metal = interfaces->Get<IUnityGraphicsMetalV2>();
    if (!s_Metal) return;
    id<MTLDevice> device = s_Metal->MetalDevice();
    if (!device) { s_Metal = nullptr; return; }

#if TARGET_OS_OSX
    if (@available(macOS 10.15, *))
        s_UsesManagedStorage = !device.hasUnifiedMemory;
    else
        s_UsesManagedStorage = true;
#else
    s_UsesManagedStorage = false;
#endif
}

extern "C" void ReleaseMetalResources()
{
    s_Metal = nullptr;
    s_UsesManagedStorage = false;
}

extern "C" bool IsMetalReady()
{
    return s_Metal != nullptr;
}

UTMETAL_EXPORT void ut_gpu_metal_retain_texture(void* ptr)
{
    if (ptr) CFRetain(ptr);
}

// Flush boundary: ends Unity's encoder, then commits Unity's current command
// buffer through the V2 receptor (Unity runs its own bookkeeping on commit
// and starts a fresh buffer), so work encoded so far — including our blits —
// reaches the GPU mid-frame. Safe to call any number of times per frame.
extern "C" void FlushMetal()
{
    if (!s_Metal) return;
    s_Metal->EndCurrentCommandEncoder();
    s_Metal->CommitCurrentCommandBuffer();
}

extern "C" void UploadMetalBatch(const GpuUploadRequest* requests, int count)
{
    NSMutableSet* retainedTexPtrs = [NSMutableSet set];
    for (int i = 0; i < count; i++)
    {
        if (requests[i].nativeTexPtr)
            [retainedTexPtrs addObject:[NSValue valueWithPointer:requests[i].nativeTexPtr]];
    }
    // The caller retained every unique texture in the batch; any path that
    // does not install the completion handler must release here.
    void (^releaseRetained)(void) = ^{
        for (NSValue* v in retainedTexPtrs)
            CFRelease(v.pointerValue);
    };

    if (!s_Metal || count <= 0) { releaseRetained(); return; }

    id<MTLDevice> device = s_Metal->MetalDevice();
    if (device == nil) { releaseRetained(); return; }

    size_t totalSize = 0;
    for (int i = 0; i < count; i++)
    {
        if (!requests[i].nativeTexPtr || !requests[i].pixelData) continue;
        totalSize += (size_t)requests[i].width * requests[i].height * requests[i].bytesPerPixel;
    }
    if (totalSize == 0) { releaseRetained(); return; }

    MTLResourceOptions options;
#if TARGET_OS_OSX
    options = s_UsesManagedStorage
        ? MTLResourceStorageModeManaged
        : MTLResourceStorageModeShared;
#else
    options = MTLResourceStorageModeShared;
#endif

    id<MTLBuffer> staging = [device newBufferWithLength:totalSize options:options];
    if (staging == nil) { releaseRetained(); return; }
    staging.label = @"UniText GPU Upload Staging";

    uint8_t* mapped = (uint8_t*)[staging contents];
    size_t offset = 0;
    for (int i = 0; i < count; i++)
    {
        const GpuUploadRequest& r = requests[i];
        if (!r.nativeTexPtr || !r.pixelData) continue;

        size_t dstRow = (size_t)r.width * r.bytesPerPixel;
        size_t srcStride = r.srcRowPitch > 0 ? (size_t)r.srcRowPitch : dstRow;
        if (srcStride == dstRow)
        {
            memcpy(mapped + offset, r.pixelData, dstRow * r.height);
        }
        else
        {
            for (int y = 0; y < r.height; y++)
            {
                memcpy(mapped + offset + (size_t)y * dstRow,
                       (const uint8_t*)r.pixelData + (size_t)y * srcStride,
                       dstRow);
            }
        }
        offset += dstRow * r.height;
    }

#if TARGET_OS_OSX
    if (s_UsesManagedStorage)
        [staging didModifyRange:NSMakeRange(0, totalSize)];
#endif

    id<MTLCommandBuffer> cmdBuf = s_Metal->CurrentCommandBuffer();
    if (cmdBuf == nil) { releaseRetained(); return; }

    s_Metal->EndCurrentCommandEncoder();

    id<MTLBlitCommandEncoder> blit = [cmdBuf blitCommandEncoder];
    if (blit == nil) { releaseRetained(); return; }
    blit.label = @"UniText GPU Upload";

    offset = 0;
    for (int i = 0; i < count; i++)
    {
        const GpuUploadRequest& r = requests[i];
        if (!r.nativeTexPtr || !r.pixelData) continue;

        size_t rowBytes = (size_t)r.width * r.bytesPerPixel;
        size_t imageBytes = rowBytes * r.height;

        id<MTLTexture> tex = (__bridge id<MTLTexture>)r.nativeTexPtr;
        [blit copyFromBuffer:staging
                sourceOffset:offset
           sourceBytesPerRow:rowBytes
         sourceBytesPerImage:imageBytes
                  sourceSize:MTLSizeMake(r.width, r.height, 1)
                   toTexture:tex
            destinationSlice:r.sliceIndex
            destinationLevel:r.mipLevel
           destinationOrigin:MTLOriginMake(r.dstX, r.dstY, 0)];

        offset += imageBytes;
    }

    [blit endEncoding];

    // Installed before Unity commits this buffer (we run mid-frame on the
    // render thread). The blit encoder's own retain keeps `staging` alive
    // until execution completes.
    [cmdBuf addCompletedHandler:^(id<MTLCommandBuffer> _Nonnull) {
        for (NSValue* v in retainedTexPtrs)
            CFRelease(v.pointerValue);
    }];
}

#endif
