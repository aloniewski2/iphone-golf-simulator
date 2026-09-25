#import "UnityAppController.h"
#import "Unity/DisplayManager.h"

@interface SportsExternalRendererController : UIViewController
@end
@implementation SportsExternalRendererController
- (UIInterfaceOrientationMask)supportedInterfaceOrientations { return UIInterfaceOrientationMaskLandscape; }
@end

// Unity creates its secondary UIWindow with a raw subview and no controller.
// Give that same render surface a scene-owned controller before the host hides
// its waiting window. Never move the phone's primary surface to the TV.
// AirPlay compresses and streams whatever mode the TV screen is in, and Unity picks the
// preferred (largest) one: 4K on a 4K Apple TV. Four times the pixels to render, encode, send
// and decode, for a picture the player sits across the room from. Stream at 1080p at most.
static void SportsCapExternalMode(UIScreen *screen) {
    UIScreenMode *best = nil;
    for (UIScreenMode *mode in screen.availableModes) {
        CGSize size = mode.size;
        if (size.width > 1920 || size.height > 1080) continue;
        if (!best || size.width * size.height > best.size.width * best.size.height) best = mode;
    }
    if (!best || [screen.currentMode isEqual:best]) return;
    NSLog(@"[SportsDisplay] external mode %@ -> %@", NSStringFromCGSize(screen.currentMode.size), NSStringFromCGSize(best.size));
    screen.currentMode = best;
}

extern "C" __attribute__((visibility("default"))) bool SportsPresentExternalDisplay() {
    for (UIScene *candidate in UIApplication.sharedApplication.connectedScenes) {
        if (![candidate isKindOfClass:UIWindowScene.class] ||
            ![candidate.session.role isEqualToString:UIWindowSceneSessionRoleExternalDisplayNonInteractive]) continue;
        UIWindowScene *scene = (UIWindowScene*)candidate;
        DisplayConnection *connection = [DisplayManager Instance][scene.screen];
        UIWindow *window = connection.window;
        UIView *view = connection.view;
        NSLog(@"[SportsDisplay] secondary window=%@ view=%@ scene=%@ hidden=%d root=%@",window,view,window.windowScene,window.hidden,window.rootViewController);
        if (!window || !view || connection == [DisplayManager Instance].mainDisplay) return false;
        SportsCapExternalMode(scene.screen);
        window.windowScene = scene;
        window.frame = scene.coordinateSpace.bounds;
        if (!window.rootViewController) {
            SportsExternalRendererController *controller = [SportsExternalRendererController new];
            [view removeFromSuperview];
            controller.view = view;
            window.rootViewController = controller;
        }
        view.frame = window.bounds;
        view.autoresizingMask = UIViewAutoresizingFlexibleWidth | UIViewAutoresizingFlexibleHeight;
        window.hidden = NO;
        [window layoutIfNeeded];
        NSLog(@"[SportsDisplay] secondary presentation attached root=%@ bounds=%@",window.rootViewController,NSStringFromCGRect(window.bounds));
        return window.windowScene == scene && !window.hidden;
    }
    return false;
}

// Unity's default picker accepts any foreground scene, including the TV scene.
// DisplayManager still treats the phone as display 0, so always initialize there.
@interface SportsUnityAppController : UnityAppController
@end
@implementation SportsUnityAppController
- (UIWindowScene*)pickStartupWindowScene:(NSSet<UIScene*>*)scenes {
    for (UIScene *scene in scenes) {
        if ([scene isKindOfClass:UIWindowScene.class] &&
            [scene.session.role isEqualToString:UIWindowSceneSessionRoleApplication]) {
            NSLog(@"[SportsDisplay] Unity primary renderer assigned to phone scene");
            return (UIWindowScene*)scene;
        }
    }
    return nil;
}
- (BOOL)shouldUseMetalDisplayLink { return NO; }
@end
IMPL_APP_CONTROLLER_SUBCLASS(SportsUnityAppController)
