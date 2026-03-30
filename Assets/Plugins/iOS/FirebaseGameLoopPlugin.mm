// FirebaseGameLoopPlugin.mm
// Native iOS plugin to handle firebase-game-loop URL scheme
// Firebase Test Lab requires application:openURL:options: to be implemented

#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>
#import "UnityAppController.h"

static NSString* _launchURL = nil;
static BOOL _isGameLoopLaunch = NO;

// Subclass UnityAppController to handle URL schemes
@interface FirebaseGameLoopAppController : UnityAppController
@end

@implementation FirebaseGameLoopAppController

- (BOOL)application:(UIApplication *)app openURL:(NSURL *)url options:(NSDictionary<UIApplicationOpenURLOptionsKey,id> *)options
{
    NSLog(@"[FirebaseGameLoop] openURL called: %@", url.absoluteString);

    if ([url.scheme isEqualToString:@"firebase-game-loop"])
    {
        _launchURL = [url.absoluteString copy];
        _isGameLoopLaunch = YES;
        NSLog(@"[FirebaseGameLoop] Game loop URL detected and stored");
    }

    // Call super if it responds to this selector
    if ([super respondsToSelector:@selector(application:openURL:options:)])
    {
        return [super application:app openURL:url options:options];
    }

    return YES;
}

// Also handle the deprecated method for older iOS versions
- (BOOL)application:(UIApplication *)application openURL:(NSURL *)url sourceApplication:(NSString *)sourceApplication annotation:(id)annotation
{
    NSLog(@"[FirebaseGameLoop] openURL (deprecated) called: %@", url.absoluteString);

    if ([url.scheme isEqualToString:@"firebase-game-loop"])
    {
        _launchURL = [url.absoluteString copy];
        _isGameLoopLaunch = YES;
        NSLog(@"[FirebaseGameLoop] Game loop URL detected and stored (deprecated method)");
    }

    if ([super respondsToSelector:@selector(application:openURL:sourceApplication:annotation:)])
    {
        return [super application:application openURL:url sourceApplication:sourceApplication annotation:annotation];
    }

    return YES;
}

// Handle URL when app is launched from cold start
- (BOOL)application:(UIApplication *)application didFinishLaunchingWithOptions:(NSDictionary<UIApplicationLaunchOptionsKey,id> *)launchOptions
{
    NSURL* url = launchOptions[UIApplicationLaunchOptionsURLKey];
    if (url && [url.scheme isEqualToString:@"firebase-game-loop"])
    {
        _launchURL = [url.absoluteString copy];
        _isGameLoopLaunch = YES;
        NSLog(@"[FirebaseGameLoop] App launched with game loop URL: %@", _launchURL);
    }

    return [super application:application didFinishLaunchingWithOptions:launchOptions];
}

@end

IMPL_APP_CONTROLLER_SUBCLASS(FirebaseGameLoopAppController)

// C interface for Unity
extern "C"
{
    const char* FirebaseGameLoop_GetLaunchURL()
    {
        if (_launchURL == nil)
            return NULL;

        return strdup([_launchURL UTF8String]);
    }

    bool FirebaseGameLoop_IsGameLoopLaunch()
    {
        return _isGameLoopLaunch;
    }

    void FirebaseGameLoop_ClearLaunchURL()
    {
        _launchURL = nil;
    }
}
