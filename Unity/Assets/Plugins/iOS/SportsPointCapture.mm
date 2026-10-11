#import "SportsPointRecorder.h"
#import "SportsSessionRecorder.h"
#import <objc/runtime.h>
#import "UnityAppController.h"
#import "Unity/DisplayManager.h"
#include "Unity/IUnityInterface.h"
#include "Unity/IUnityGraphics.h"
#include "Unity/IUnityGraphicsMetal.h"
#include "Unity/UnityInterface.h"

static IUnityGraphicsMetalV2 *pointMetal;
// Unity's render-delegate callback has no display/command-buffer arguments.
// Scope it to DisplayConnection.presentWith: (a Unity class, not an Apple API).
static thread_local __unsafe_unretained DisplayConnection *presentingDisplay;
static thread_local __unsafe_unretained id<MTLCommandBuffer> presentingBuffer;
// One flag per recorder and per present: a frame is captured at most once by each recorder.
static thread_local bool capturedPoint;
static thread_local bool capturedSession;
static void (*originalPresent)(id,SEL,id<MTLCommandBuffer>);
static void PointPresent(id connection,SEL selector,id<MTLCommandBuffer> buffer) {
    presentingDisplay=connection; presentingBuffer=buffer; capturedPoint=false; capturedSession=false;
    originalPresent(connection,selector,buffer);
    presentingDisplay=nil; presentingBuffer=nil;
}
static void UNITY_INTERFACE_API PointPluginLoad(IUnityInterfaces *interfaces) { pointMetal=interfaces->Get<IUnityGraphicsMetalV2>(); }
static void UNITY_INTERFACE_API PointPluginUnload() { pointMetal=nullptr; }
extern "C" void SportsRegisterPointCapture() {
    UnityRegisterPlugin(PointPluginLoad,PointPluginUnload);
    static dispatch_once_t once; dispatch_once(&once,^{
        Method method=class_getInstanceMethod(DisplayConnection.class,@selector(presentWith:));
        if(method) originalPresent=(void(*)(id,SEL,id<MTLCommandBuffer>))method_setImplementation(method,(IMP)PointPresent);
    });
}

@interface SportsPointRenderDelegate : RenderPluginDelegate
@end
// Same display selection for both recorders: 0 = Unity's main display, otherwise the first non-main display.
static DisplayConnection *SportsConnectionForDisplay(int display) {
    if(display==0) return [DisplayManager Instance].mainDisplay;
    __block DisplayConnection *connection=nil;
    [[DisplayManager Instance] enumerateNonMainDisplaysWithBlock:^(DisplayConnection *candidate) { if(!connection) connection=candidate; }];
    return connection;
}
extern "C" bool SportsCaptureMetalReady() { return pointMetal!=nullptr; }
@implementation SportsPointRenderDelegate
- (void)onFrameResolved {
    SportsPointRecorder *point=SportsPointRecorder.shared;
    SportsSessionRecorder *session=SportsSessionRecorder.shared;
    BOOL wantPoint=!capturedPoint && point.needsFrames, wantSession=!capturedSession && session.needsFrames;
    if((!wantPoint && !wantSession) || !pointMetal || !presentingDisplay) return;
    BOOL pointHere=NO, sessionHere=NO;
    if(wantPoint) { DisplayConnection *connection=SportsConnectionForDisplay(point.display); pointHere=connection==presentingDisplay && connection.surface; }
    if(wantSession) { DisplayConnection *connection=SportsConnectionForDisplay(session.display); sessionHere=connection==presentingDisplay && connection.surface; }
    if(!pointHere && !sessionHere) return;
    UnityDisplaySurfaceMTL *surface=(UnityDisplaySurfaceMTL *)presentingDisplay.surface;
    // Read the resolved Unity color target, including the gameplay HUD, before presentation.
    // The TV target is distinct from UIKit's phone controller window.
    id<MTLTexture> texture=surface->targetColorRB ? pointMetal->TextureFromRenderBuffer(surface->targetColorRB) : surface->swapchain.drawableTexture;
    // Encode into Unity's still-open buffer so the capture follows scene/HUD
    // rendering, and completes before Unity presents/reuses the drawable.
    pointMetal->EndCurrentCommandEncoder();
    double now=NSProcessInfo.processInfo.systemUptime;
    if(pointHere) { capturedPoint=true; [point captureTexture:texture commandBuffer:presentingBuffer timestamp:now]; }
    if(sessionHere) { capturedSession=true; [session captureTexture:texture commandBuffer:presentingBuffer timestamp:now]; }
}
- (void)willResignActive:(NSNotification *)notification { [SportsPointRecorder.shared setPaused:YES]; }
@end
extern "C" id SportsMakePointRenderDelegate(id existing) {
    id recorder=[SportsPointRenderDelegate new];
    if(!existing) return recorder;
    RenderPluginArrayDelegate *combined=[RenderPluginArrayDelegate new]; combined.delegateArray=@[existing,recorder]; return combined;
}
extern "C" {
void SportsRecorderConfigure(const char *session,int display) { [SportsPointRecorder.shared configureSession:session?[NSString stringWithUTF8String:session]:@"" display:display]; }
void SportsRecorderEnable(bool enabled) { [SportsPointRecorder.shared setEnabled:enabled]; }
void SportsRecorderPause(bool paused) { [SportsPointRecorder.shared setPaused:paused]; }
void SportsRecorderBeginPoint() { [SportsPointRecorder.shared beginPoint]; }
void SportsRecorderEndPoint() { [SportsPointRecorder.shared endPoint]; }
void SportsRecorderSave() { [SportsPointRecorder.shared saveLastPoint]; }
void SportsRecorderAudio(const float *samples,int count,int channels,int rate,double timestamp) {
    @autoreleasepool {
        [SportsPointRecorder.shared captureAudio:samples count:count channels:channels rate:rate timestamp:timestamp];
        // The full-session recorder takes the same game mix; its clock does not depend on these callbacks.
        [SportsSessionRecorder.shared captureGameAudio:samples count:count channels:channels rate:rate];
    }
}
void SportsRecorderStop() { [SportsPointRecorder.shared stop]; }
}
