#import "UnityAppController.h"
#import "Unity/DisplayManager.h"
#import "Unity/UnityInterface.h"

// Unity-internal method implemented in its exported DisplayManager.mm.
@interface DisplayManager (SportsSceneRegistration)
- (void)registerScreen:(UIScreen*)screen;
@end

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

// Scene connection and Unity's UIScreen notification cache can arrive in either order.
// Build the display cache from live scenes, keeping the phone at zero, then initialize
// the exact external screen. Merely calling Display.Activate(1) on a stale cache leaves
// connection.window and connection.view nil and never produces a frame.
extern "C" __attribute__((visibility("default"))) int SportsPrepareExternalDisplay() {
    DisplayManager *manager = [DisplayManager Instance];
    if (!manager) return -1;
    UIWindowScene *externalScene = nil;
    void *screens[8];
    screens[0] = (__bridge void*)manager.mainDisplay.screen;
    int count = 1;
    for (UIScene *candidate in UIApplication.sharedApplication.connectedScenes) {
        if (![candidate isKindOfClass:UIWindowScene.class] ||
            ![candidate.session.role isEqualToString:UIWindowSceneSessionRoleExternalDisplayNonInteractive]) continue;
        UIWindowScene *scene = (UIWindowScene*)candidate;
        if (scene.activationState == UISceneActivationStateUnattached || scene.screen == manager.mainDisplay.screen) continue;
        if (!externalScene) externalScene = scene;
        if (count < 8) { [manager registerScreen:scene.screen]; screens[count++] = (__bridge void*)scene.screen; }
    }
    if (!externalScene) return -1;
    SportsCapExternalMode(externalScene.screen);
    UnityUpdateDisplayListCache(screens, count);
    UnityActivateScreenForRendering((__bridge void*)externalScene.screen);
    DisplayConnection *connection = manager[externalScene.screen];
    UIWindow *window = connection.window;
    UIView *view = connection.view;
    if (!window || !view || connection == manager.mainDisplay) return -1;
    window.windowScene = externalScene;
    window.frame = externalScene.coordinateSpace.bounds;
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
    NSLog(@"[SportsDisplay] external ready index=1 screen=%@ window=%@ view=%@ scene=%@ bounds=%@", externalScene.screen, window, view, window.windowScene, NSStringFromCGRect(window.bounds));
    return window.windowScene == externalScene && !window.hidden ? 1 : -1;
}

// Unity's default picker accepts any foreground scene, including the TV scene.
// DisplayManager still treats the phone as display 0, so always initialize there.
@interface SportsUnityAppController : UnityAppController
@end
extern "C" void SportsRegisterPointCapture();
extern "C" id SportsMakePointRenderDelegate(id existing);
@implementation SportsUnityAppController
- (void)preStartUnity { [super preStartUnity]; SportsRegisterPointCapture(); }
- (void)shouldAttachRenderDelegate {
    [super shouldAttachRenderDelegate];
    self.renderDelegate=SportsMakePointRenderDelegate(self.renderDelegate);
}
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
