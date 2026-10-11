#import <Foundation/Foundation.h>
#import <Metal/Metal.h>

/// Full-session recorder for the TV picture: the Unity gameplay surface (never the phone UI)
/// plus game audio, remote party voice and the player's own microphone mixed into one AAC track.
/// Pixels arrive from the same Unity render hook as SportsPointRecorder; voice samples are
/// pushed by the host app (this class never opens the microphone). Events are delivered as JSON
/// on the main queue through the callback registered with -setEventCallback:.
@interface SportsSessionRecorder : NSObject
+ (instancetype)shared;

/// Returns 1 when recording started, 0 when refused (an event explains why).
- (int)startWithConfigJSON:(const char *)json;
/// Finalizes asynchronously. Always ends with a "finished" or "failed" event. No-op when idle.
- (void)stop;
/// 1 while recording. Cheap, any thread.
@property(nonatomic,readonly) BOOL active;
/// Render hook: the session wants a frame for the presenting display right now.
@property(nonatomic,readonly) BOOL needsFrames;
/// Index of the display being recorded (0 = main, otherwise the first non-main display).
@property(nonatomic,readonly) int display;

/// Render thread, with Unity's still-open command buffer (blit is encoded into it).
- (void)captureTexture:(id<MTLTexture>)texture commandBuffer:(id<MTLCommandBuffer>)command timestamp:(double)timestamp;
/// Unity audio thread. Interleaved game audio at whatever rate Unity runs. Never blocks.
- (void)captureGameAudio:(const float *)samples count:(int)count channels:(int)channels rate:(int)rate;
/// Any real-time thread. source 0 = remote party voice (already mixed), 1 = own microphone. Mono.
- (void)captureVoice:(const float *)mono frames:(int)frames rate:(int)rate source:(int)source;

/// Called on the main queue with one JSON object per event.
- (void)setEventCallback:(void (*)(const char *eventJSON))callback;
/// Reports a refusal (event "failed") without starting. Used by the Unity-aware layer.
- (void)emitFailure:(NSString *)message reason:(NSString *)reason;

/// Unity-aware hooks installed by SportsSessionCapture.mm. Both run on the main thread.
/// displayCheck: nil when the display can be recorded, otherwise a plain-words refusal.
@property(nonatomic,copy) NSString *(^displayCheck)(int display);
/// surfaceProbe: YES while the display's surface exists.
@property(nonatomic,copy) BOOL (^surfaceProbe)(int display);
@end
