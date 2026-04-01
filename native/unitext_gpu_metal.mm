// UniText GPU Upload — Metal implementation
// Compiled as Objective-C++ (.mm) for Metal API access.

#if defined(__APPLE__)

#import <Metal/Metal.h>

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

extern "C" void UploadMetal(const GpuUploadRequest& req)
{
    id<MTLTexture> tex = (__bridge id<MTLTexture>)req.nativeTexPtr;
    if (!tex) return;

    MTLRegion region = MTLRegionMake2D(req.dstX, req.dstY, req.width, req.height);
    NSUInteger bytesPerRow = (NSUInteger)(req.srcRowPitch > 0 ? req.srcRowPitch : req.width * req.bytesPerPixel);
    [tex replaceRegion:region
           mipmapLevel:req.mipLevel
                 slice:req.sliceIndex
             withBytes:req.pixelData
           bytesPerRow:bytesPerRow
         bytesPerImage:0];
}

#endif
