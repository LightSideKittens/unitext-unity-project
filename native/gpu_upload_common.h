// Shared ABI between native paths (C++/Objective-C++) and C# DllImport side.
// Changing any field layout requires synchronized updates in GpuUpload.cs
// (NativeUploadRequest / NativeBatchHeader) and full native rebuild.

#pragma once

#include <stdint.h>

// ABI version of the request/batch structs and the event contract below.
// ut_gpu_get_abi_version() reports it; C# refuses the native path unless the
// loaded binary returns exactly this value, so stale shipped binaries fail
// safe into the Apply() fallback instead of reading misaligned requests.
// Bump on ANY change to GpuUploadRequest/GpuUploadBatch layout or event ids.
#define GPU_UPLOAD_ABI_VERSION 2

// Plugin event ids. C# must pass these to IssuePluginEvent(AndData) — the
// D3D12/Vulkan ConfigureEvent registrations are keyed by event id.
#define GPU_UPLOAD_EVENT_BATCH 0
#define GPU_UPLOAD_EVENT_FLUSH 1

// Triple-buffered staging ring for all per-frame GPU-owned slots
// (D3D12 / Vulkan / Metal). 3 = canonical CPU-GPU overlap depth.
#define GPU_UPLOAD_STAGING_RING_SIZE 3

// Format codes — must match C# GpuUploadFormat enum.
// Used by OpenGL path to disambiguate formats sharing the same bytesPerPixel
// (e.g. R16 vs RG8 vs RHalf — all 2 bpp but different GL type mappings).
// Other backends (D3D11/D3D12/Metal/Vulkan) only consume bytesPerPixel.
#define GPU_UPLOAD_FMT_UNKNOWN   0
#define GPU_UPLOAD_FMT_R8        1
#define GPU_UPLOAD_FMT_R16       2
#define GPU_UPLOAD_FMT_RHALF     3
#define GPU_UPLOAD_FMT_RG8       4
#define GPU_UPLOAD_FMT_RGBA32    5
#define GPU_UPLOAD_FMT_RGBAHALF  6

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
    int format;     // GPU_UPLOAD_FMT_* — only OpenGL reads it today
    int reserved;   // keeps struct size at 56 bytes (8-byte alignment)
};

struct GpuUploadBatch
{
    int count;
    int padding;
};

#pragma pack(pop)
