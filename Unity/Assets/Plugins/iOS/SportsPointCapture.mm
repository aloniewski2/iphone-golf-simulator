#import "SportsPointRecorder.h"
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
static thread_local bool capturedPresent;
static void (*originalPresent)(id,SEL,id<MTLCommandBuffer>);
static void PointPresent(id connection,SEL selector,id<MTLCommandBuffer> buffer) {
    presentingDisplay=connection; presentingBuffer=buffer; capturedPresent=false;
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
@implementation SportsPointRenderDelegate
- (void)onFrameResolved {
    SportsPointRecorder *recorder=SportsPointRecorder.shared;
    if(!recorder.needsFrames || !pointMetal || !presentingDisplay || capturedPresent) return;
    __block DisplayConnection *connection=nil;
    if(recorder.display==0) connection=[DisplayManager Instance].mainDisplay;
    else [[DisplayManager Instance] enumerateNonMainDisplaysWithBlock:^(DisplayConnection *candidate) { if(!connection) connection=candidate; }];
    if(connection!=presentingDisplay || !connection.surface) return;
    capturedPresent=true;
    UnityDisplaySurfaceMTL *surface=(UnityDisplaySurfaceMTL *)connection.surface;
    // Read the resolved Unity color target, including the gameplay HUD, before presentation.
    // The TV target is distinct from UIKit's phone controller window.
    id<MTLTexture> texture=surface->targetColorRB ? pointMetal->TextureFromRenderBuffer(surface->targetColorRB) : surface->swapchain.drawableTexture;
    // Encode into Unity's still-open buffer so the capture follows scene/HUD
    // rendering, and completes before Unity presents/reuses the drawable.
    pointMetal->EndCurrentCommandEncoder();
    [recorder captureTexture:texture commandBuffer:presentingBuffer timestamp:NSProcessInfo.processInfo.systemUptime];
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
void SportsRecorderAudio(const float *samples,int count,int channels,int rate,double timestamp) { @autoreleasepool { [SportsPointRecorder.shared captureAudio:samples count:count channels:channels rate:rate timestamp:timestamp]; } }
void SportsRecorderStop() { [SportsPointRecorder.shared stop]; }
}
