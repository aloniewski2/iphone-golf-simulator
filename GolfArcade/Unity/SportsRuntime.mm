#import "SportsRuntime.h"
#import <dlfcn.h>
#import <mach-o/dyld.h>
#include <string>
#include <vector>
#if __has_include(<UnityFramework/UnityFramework.h>) && !TARGET_OS_SIMULATOR
#import <UnityFramework/UnityFramework.h>
#define HAS_UNITY 1
#else
#define HAS_UNITY 0
#endif

@implementation SportsRuntime {
#if HAS_UNITY
    UnityFramework *_unity;
    // Unity may retain argv; keep its storage alive for the singleton's lifetime.
    std::vector<std::string> _argumentStorage;
    std::vector<char*> _arguments;
#endif
    void (*_push)(const char*);
    void (*_pushSample)(const SportsSample*);
    int (*_poll)(char*,int);
    double (*_clock)(void);
    __weak UIWindow *_destination;
}
+ (instancetype)shared { static SportsRuntime *instance; static dispatch_once_t token; dispatch_once(&token, ^{instance=[SportsRuntime new];}); return instance; }
- (BOOL)loadInWindow:(UIWindow*)window error:(NSError**)error {
#if HAS_UNITY
    if (!_unity) {
        // Debug builds put app code in GolfArcade.debug.dylib, where the
        // executable-only _mh_execute_header symbol cannot be linked directly.
        // Find the actual host executable, not this dylib or UnityFramework.
        const MachHeader *executeHeader = nullptr;
        for (uint32_t i = 0; i < _dyld_image_count(); ++i) {
            const struct mach_header *header = _dyld_get_image_header(i);
            if (header && header->filetype == MH_EXECUTE) {
                executeHeader = reinterpret_cast<const MachHeader*>(header);
                break;
            }
        }
        if (!executeHeader) {
            if(error) *error=[NSError errorWithDomain:@"SportsRuntime" code:3 userInfo:@{NSLocalizedDescriptionKey:@"Unable to locate the app executable for Unity startup."}];
            return NO;
        }
        _unity = [UnityFramework getInstance];
        [_unity setExecuteHeader:executeHeader];
        // Export Data is copied into the host bundle by the integration build spec.
        [_unity setDataBundleId:NSBundle.mainBundle.bundleIdentifier.UTF8String];
        for (NSString *argument in NSProcessInfo.processInfo.arguments)
            _argumentStorage.emplace_back(argument.UTF8String);
        if (_argumentStorage.empty())
            _argumentStorage.emplace_back(NSBundle.mainBundle.executablePath.UTF8String ?: "GolfArcade");
        for (std::string &argument : _argumentStorage)
            _arguments.push_back(argument.data());
        _arguments.push_back(nullptr);
        NSLog(@"[SportsRuntime] Starting Unity with %lu arguments",(unsigned long)_argumentStorage.size());
        [_unity runEmbeddedWithArgc:(int)_argumentStorage.size() argv:_arguments.data() appLaunchOpts:nil];
        NSLog(@"[SportsRuntime] Unity warmup completed");
        _push=(void(*)(const char*))dlsym(RTLD_DEFAULT,"SportsPushInput");
        _pushSample=(void(*)(const SportsSample*))dlsym(RTLD_DEFAULT,"SportsPushSample");
        _poll=(int(*)(char*,int))dlsym(RTLD_DEFAULT,"SportsPollEvent");
        _clock=(double(*)(void))dlsym(RTLD_DEFAULT,"SportsClock");
    }
    if (!_push || !_pushSample || !_poll || !_clock) {
        if(error) *error=[NSError errorWithDomain:@"SportsRuntime" code:2 userInfo:@{NSLocalizedDescriptionKey:@"Unity bridge is missing. Re-export Unity before building the host app."}];
        return NO;
    }
    UIWindowScene *phoneScene=nil;
    for(UIScene *scene in UIApplication.sharedApplication.connectedScenes)
        if([scene isKindOfClass:UIWindowScene.class] && [scene.session.role isEqualToString:UIWindowSceneSessionRoleApplication]) { phoneScene=(UIWindowScene*)scene; break; }
    [_unity.appController initUnityWithScene:phoneScene];
    [self attachToWindow:window];
    return YES;
#else
    if(error) *error=[NSError errorWithDomain:@"SportsRuntime" code:1 userInfo:@{NSLocalizedDescriptionKey:@"Unity requires the integrated physical-device build. Simulator can preview native menus only."}];
    return NO;
#endif
}
- (void)attachToWindow:(UIWindow*)window {
    _destination=window;
#if HAS_UNITY
    // Let Unity's supported iOS multi-display renderer own the external surface.
    // Do not transplant its main-display view onto a different UIScreen.
    if([window.windowScene.session.role isEqualToString:UIWindowSceneSessionRoleExternalDisplayNonInteractive]) {
        _unity.appController.window.hidden=YES;
        return;
    }
    UIViewController *controller=_unity.appController.rootViewController;
    if(!controller) return;
    UIWindow *previous=_unity.appController.window;
    if(previous != window) previous.hidden=YES;
    [controller.view removeFromSuperview];
    window.rootViewController=controller;
    _unity.appController.window=window;
    controller.view.frame=window.bounds;
    controller.view.autoresizingMask=UIViewAutoresizingFlexibleWidth|UIViewAutoresizingFlexibleHeight;
    window.hidden=NO;
#endif
}
- (void)send:(NSString*)json {
#if HAS_UNITY
    [_unity sendMessageToGOWithName:"NativeSportsSession" functionName:"Receive" message:json.UTF8String];
#endif
}
- (void)push:(NSString*)json { if(_push) _push(json.UTF8String); }
- (void)pushSample:(SportsSample)sample { if(_pushSample) _pushSample(&sample); }
- (NSString*)pollEvent { char buffer[8192]; if(!_poll || !_poll(buffer,sizeof(buffer))) return nil; return [NSString stringWithUTF8String:buffer]; }
- (double)clock { return _clock ? _clock() : NSProcessInfo.processInfo.systemUptime; }
- (void)pause:(BOOL)paused {
#if HAS_UNITY
    [_unity pause:paused];
#endif
}
- (void)setForeground:(BOOL)foreground {
#if HAS_UNITY
    if(!_unity) return;
    if(foreground) {
        [_unity.appController applicationWillEnterForeground:UIApplication.sharedApplication];
        [_unity.appController applicationDidBecomeActive:UIApplication.sharedApplication];
    } else {
        [_unity.appController applicationWillResignActive:UIApplication.sharedApplication];
        [_unity.appController applicationDidEnterBackground:UIApplication.sharedApplication];
    }
#endif
}
@end
