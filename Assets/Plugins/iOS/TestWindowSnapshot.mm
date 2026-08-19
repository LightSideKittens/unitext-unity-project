// Captures the app's own UIKit window for test artifacts.
//
// The player's camera render reaches only what that camera draws; UIKit views UniText places over
// the Unity view — the native input field and its presenter surface — are absent from it. They do
// live in the app's window, so a window snapshot shows them exactly as UIKit rendered them,
// including the appearance the running OS gives system controls. The soft keyboard belongs to a
// separate system window and stays out of reach; only a device screen recording contains it.

#import <UIKit/UIKit.h>

extern "C" UIView* UnityGetGLView(void);

extern "C" const void* UniTextTestScreenshot_CaptureWindow(int* outLength)
{
    if (outLength) *outLength = 0;
    UIView* view = UnityGetGLView();
    UIWindow* window = view.window;
    if (!window || CGRectIsEmpty(window.bounds)) return NULL;

    UIGraphicsImageRendererFormat* format = [UIGraphicsImageRendererFormat preferredFormat];
    format.opaque = YES;
    UIGraphicsImageRenderer* renderer =
        [[UIGraphicsImageRenderer alloc] initWithBounds:window.bounds format:format];

    // afterScreenUpdates renders the live hierarchy rather than replaying layer contents, which is
    // what brings visual-effect materials into the image.
    UIImage* image = [renderer imageWithActions:^(UIGraphicsImageRendererContext* context) {
        (void)context;
        [window drawViewHierarchyInRect:window.bounds afterScreenUpdates:YES];
    }];

    NSData* png = UIImagePNGRepresentation(image);
    if (!png.length) return NULL;

    void* buffer = malloc(png.length);
    if (!buffer) return NULL;
    memcpy(buffer, png.bytes, png.length);
    if (outLength) *outLength = (int)png.length;
    return buffer;
}

extern "C" void UniTextTestScreenshot_FreeWindowCapture(const void* buffer)
{
    if (buffer) free((void*)buffer);
}
