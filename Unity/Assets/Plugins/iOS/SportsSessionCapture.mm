// C API of the full-session recorder (see SportsSessionRecorder.h). The Swift app finds these
// with dlsym(RTLD_DEFAULT, ...) after Unity loads (GolfArcade/Unity/SportsRuntime.mm); Unity C#
// reaches SportsCaptureActive through __Internal. The frames themselves arrive from the render
// delegate in SportsPointCapture.mm, which feeds both this recorder and the 60 fps point recorder.
#import "SportsSessionRecorder.h"
#import "UnityAppController.h"
#import "Unity/DisplayManager.h"
#include "Unity/UnityInterface.h"

extern "C" bool SportsCaptureMetalReady();

static DisplayConnection *SessionConnection(int display) {
    DisplayManager *manager = [DisplayManager Instance];
    if (!manager) return nil;
    if (display == 0) return manager.mainDisplay;
    __block DisplayConnection *connection = nil;
    [manager enumerateNonMainDisplaysWithBlock:^(DisplayConnection *candidate) { if (!connection) connection = candidate; }];
    return connection;
}

// Installed once, before the first Start. Both blocks run on the main thread.
static void InstallUnityHooks() {
    static dispatch_once_t once;
    dispatch_once(&once, ^{
        SportsSessionRecorder *recorder = SportsSessionRecorder.shared;
        recorder.displayCheck = ^NSString *(int display) {
            if (!SportsCaptureMetalReady()) return @"Recording is not available right now. Restart the game and try again.";
            DisplayConnection *connection = SessionConnection(display);
            if (!connection) return display == 0 ? @"The game screen is not ready." : @"Connect the TV before recording.";
            UnityDisplaySurfaceBase *base = connection.surface;
            if (!base) return @"The TV picture is not ready yet. Try again in a moment.";
            UnityDisplaySurfaceMTL *surface = (UnityDisplaySurfaceMTL *)base;
            // The pixel format and the final texture are validated on the first frame (they live on
            // the render thread); a drawable that cannot be sampled is refused here already.
            if (!surface->targetColorRB && surface->framebufferOnly) return @"This display cannot be recorded.";
            return nil;
        };
        recorder.surfaceProbe = ^BOOL(int display) {
            DisplayConnection *connection = SessionConnection(display);
            return connection && connection.surface != nullptr;
        };
    });
}

extern "C" {

/// 1 = started, 0 = refused (an event with the reason is emitted).
__attribute__((visibility("default"), used)) int SportsCaptureStart(const char *configJSON) {
    @autoreleasepool {
        InstallUnityHooks();
        return [SportsSessionRecorder.shared startWithConfigJSON:configJSON];
    }
}

/// Finalizes asynchronously; always ends with a "finished" or "failed" event. No-op when idle.
__attribute__((visibility("default"), used)) void SportsCaptureStop(void) {
    @autoreleasepool { [SportsSessionRecorder.shared stop]; }
}

/// 1 while recording. Cheap, any thread.
__attribute__((visibility("default"), used)) int SportsCaptureActive(void) {
    return SportsSessionRecorder.shared.active ? 1 : 0;
}

/// Real-time safe: copies and returns. source 0 = remote party voice (mono, already mixed), 1 = own microphone.
__attribute__((visibility("default"), used)) void SportsCaptureVoice(const float *mono, int frames, int sampleRate, int source) {
    [SportsSessionRecorder.shared captureVoice:mono frames:frames rate:sampleRate source:source];
}

/// Events are JSON objects delivered on the main queue.
__attribute__((visibility("default"), used)) void SportsCaptureSetCallback(void (*callback)(const char *eventJSON)) {
    [SportsSessionRecorder.shared setEventCallback:callback];
}

}
