// UniText GPU Upload — Metal implementation
// Compiled as Objective-C++ (.mm) for Metal API access.

#if defined(__APPLE__)

#import <Metal/Metal.h>

#pragma pack(push, 1)
struct GpuUploadRequest
{
    void* nativeTexPtr;
    void* pixelData;
    int width;          // mip-level width (already downscaled)
    int height;         // mip-level height
    int sliceIndex;
    int mipLevel;
    int bytesPerPixel;
};
#pragma pack(pop)

extern "C" void UploadMetal(const GpuUploadRequest& req)
{
    id<MTLTexture> tex = (__bridge id<MTLTexture>)req.nativeTexPtr;
    if (!tex) return;

    MTLRegion region = MTLRegionMake2D(0, 0, req.width, req.height);
    [tex replaceRegion:region
           mipmapLevel:req.mipLevel
                 slice:req.sliceIndex
             withBytes:req.pixelData
           bytesPerRow:(NSUInteger)(req.width * req.bytesPerPixel)
         bytesPerImage:0];
}

#endif
