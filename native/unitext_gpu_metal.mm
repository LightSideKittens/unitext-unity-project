// UniText GPU Upload — Metal implementation.
// Staging MTLBuffer ring + MTLBlitCommandEncoder on a dedicated MTLCommandQueue.
// Metal's automatic hazard tracking serializes our blit against Unity's draws
// since both use the same MTLDevice and Unity textures are in tracked mode.
// Compiled as Objective-C++ (.mm) with ARC (-fobjc-arc).

#if defined(__APPLE__)

#import <Metal/Metal.h>
#import <Foundation/Foundation.h>
#import <TargetConditionals.h>
#include <dispatch/dispatch.h>
#include <string.h>

#include "unity_plugin_api/IUnityInterface.h"
#include "unity_plugin_api/IUnityGraphics.h"
#include "unity_plugin_api/IUnityGraphicsMetal.h"

#pragma pack(push, 1)
struct GpuUploadRequest
{
    void* nativeTexPtr;
    void* pixelData;
    int width;
    int height;
    int sliceIndex;
    int mipLevel;
    int bytesPerPixel;
    int dstX;
    int dstY;
    int srcRowPitch;
};
#pragma pack(pop)

static IUnityGraphicsMetal* s_Metal = nullptr;
static id<MTLDevice> s_MetalDevice = nil;
static id<MTLCommandQueue> s_MetalQueue = nil;
static bool s_MetalUsesManagedStorage = false;

static const int METAL_RING = 3;
static id<MTLBuffer> s_MetalStaging[METAL_RING] = { nil, nil, nil };
static size_t s_MetalStagingCap[METAL_RING] = { 0, 0, 0 };
static int s_MetalFrame = 0;
static dispatch_semaphore_t s_MetalSemaphore = nil;

extern "C" void InitMetal(IUnityInterfaces* interfaces)
{
    s_Metal = interfaces->Get<IUnityGraphicsMetal>();
    if (!s_Metal) return;
    s_MetalDevice = s_Metal->MetalDevice();
    if (!s_MetalDevice) { s_Metal = nullptr; return; }

    s_MetalQueue = [s_MetalDevice newCommandQueue];
    if (s_MetalQueue == nil) { s_MetalDevice = nil; s_Metal = nullptr; return; }
    s_MetalQueue.label = @"UniText GPU Upload Queue";

#if TARGET_OS_OSX
    // Intel Mac uses managed storage; Apple Silicon has unified memory.
    // hasUnifiedMemory landed in macOS 10.15 — older macOS → assume managed.
    if (@available(macOS 10.15, *))
        s_MetalUsesManagedStorage = !s_MetalDevice.hasUnifiedMemory;
    else
        s_MetalUsesManagedStorage = true;
#else
    // iOS / tvOS / visionOS / Mac Catalyst: unified memory, shared storage only.
    s_MetalUsesManagedStorage = false;
#endif

    s_MetalSemaphore = dispatch_semaphore_create(METAL_RING);
}

extern "C" void ReleaseMetalResources()
{
    for (int i = 0; i < METAL_RING; i++)
    {
        s_MetalStaging[i] = nil;
        s_MetalStagingCap[i] = 0;
    }
    s_MetalQueue = nil;
    s_MetalDevice = nil;
    s_MetalSemaphore = nil;
    s_Metal = nullptr;
    s_MetalFrame = 0;
}

extern "C" bool IsMetalReady()
{
    return s_Metal != nullptr;
}

static bool EnsureMetalStagingBuffer(int slot, size_t requiredSize)
{
    if (s_MetalStaging[slot] != nil && s_MetalStagingCap[slot] >= requiredSize)
        return true;

    MTLResourceOptions options;
#if TARGET_OS_OSX
    options = s_MetalUsesManagedStorage
        ? MTLResourceStorageModeManaged
        : MTLResourceStorageModeShared;
#else
    options = MTLResourceStorageModeShared;
#endif

    id<MTLBuffer> buf = [s_MetalDevice newBufferWithLength:requiredSize options:options];
    if (buf == nil) return false;

    s_MetalStaging[slot] = buf;
    s_MetalStagingCap[slot] = requiredSize;
    return true;
}

extern "C" void UploadMetalBatch(const GpuUploadRequest* requests, int count)
{
    if (!s_Metal || count <= 0) return;

    size_t totalSize = 0;
    for (int i = 0; i < count; i++)
        totalSize += (size_t)requests[i].width * requests[i].height * requests[i].bytesPerPixel;
    if (totalSize == 0) return;

    dispatch_semaphore_wait(s_MetalSemaphore, DISPATCH_TIME_FOREVER);

    int slot = s_MetalFrame % METAL_RING;
    s_MetalFrame++;

    if (!EnsureMetalStagingBuffer(slot, totalSize))
    {
        dispatch_semaphore_signal(s_MetalSemaphore);
        return;
    }

    id<MTLBuffer> staging = s_MetalStaging[slot];
    uint8_t* mapped = (uint8_t*)[staging contents];

    size_t offset = 0;
    for (int i = 0; i < count; i++)
    {
        const GpuUploadRequest& r = requests[i];
        if (!r.pixelData)
        {
            offset += (size_t)r.width * r.height * r.bytesPerPixel;
            continue;
        }
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
    if (s_MetalUsesManagedStorage)
        [staging didModifyRange:NSMakeRange(0, totalSize)];
#endif

    id<MTLCommandBuffer> cmdBuf = [s_MetalQueue commandBuffer];
    if (cmdBuf == nil)
    {
        dispatch_semaphore_signal(s_MetalSemaphore);
        return;
    }
    cmdBuf.label = @"UniText GPU Upload";

    // Texture lifetime safety is the caller's responsibility: C# must
    // GpuUpload.WaitForIdle() before DestroyImmediate on any referenced
    // Texture2DArray, so nativeTexPtr is guaranteed live until the render
    // thread processed counter catches up with the issued counter.
    id<MTLBlitCommandEncoder> blit = [cmdBuf blitCommandEncoder];
    blit.label = @"UniText GPU Upload";

    offset = 0;
    for (int i = 0; i < count; i++)
    {
        const GpuUploadRequest& r = requests[i];
        size_t rowBytes = (size_t)r.width * r.bytesPerPixel;
        size_t imageBytes = rowBytes * r.height;

        id<MTLTexture> tex = (__bridge id<MTLTexture>)r.nativeTexPtr;
        if (tex != nil && r.pixelData != nullptr)
        {
            [blit copyFromBuffer:staging
                    sourceOffset:offset
               sourceBytesPerRow:rowBytes
             sourceBytesPerImage:imageBytes
                      sourceSize:MTLSizeMake(r.width, r.height, 1)
                       toTexture:tex
                destinationSlice:r.sliceIndex
                destinationLevel:r.mipLevel
               destinationOrigin:MTLOriginMake(r.dstX, r.dstY, 0)];
        }
        offset += imageBytes;
    }

    [blit endEncoding];

    dispatch_semaphore_t sem = s_MetalSemaphore;
    [cmdBuf addCompletedHandler:^(id<MTLCommandBuffer> _Nonnull) {
        dispatch_semaphore_signal(sem);
    }];

    [cmdBuf commit];
}

#endif
