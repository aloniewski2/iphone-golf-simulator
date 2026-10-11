#import <UIKit/UIKit.h>
NS_ASSUME_NONNULL_BEGIN
/// One motion sample, passed to Unity as plain memory. It used to travel as JSON text at
/// 100Hz, which made Unity allocate a string and an object for every sample -- steady
/// garbage that surfaces as periodic collection hitches. Layout must match
/// `NativeSportsSession.Sample` exactly (sequential, natural alignment).
typedef struct SportsSample {
    int32_t version;        // SportsSampleVersion
    int32_t session;        // token from the start message
    double time;
    float target, power, aim;
    int32_t swing, swingStart, swingAbort, flags;
    float handSide, lift, strokeFacing;
    float qx, qy, qz, qw, rx, ry, rz, gx, gy, gz;
    double onsetTime, confirmationTime, abortTime;
} SportsSample;
enum { SportsSampleVersion = 3, SportsSampleValid = 1, SportsSampleDegraded = 2 };
@interface SportsRuntime : NSObject
+ (instancetype)shared;
- (BOOL)loadInWindow:(UIWindow*)window error:(NSError**)error;
- (BOOL)loadInWindow:(UIWindow*)window controllerReplica:(BOOL)controllerReplica error:(NSError**)error;
- (void)attachToWindow:(UIWindow*)window;
/// Golf: puts Unity's own phone window (the controller sheet) in front of the app, or hides it again.
- (void)showUnityOnPhone:(BOOL)visible;
- (void)send:(NSString*)json;
- (void)push:(NSString*)json;
- (void)pushSample:(SportsSample)sample;
- (BOOL)pushNetwork:(NSString*)json;
- (nullable NSString*)pollNetwork;
- (BOOL)multiplayerAvailable;
- (nullable NSString*)pollEvent;
- (nullable NSString*)tennisResult;
- (void)clearTennisResult;
- (double)clock;
- (void)pause:(BOOL)paused;
- (void)setForeground:(BOOL)foreground;

// MARK: TV session recording (Unity-side recorder; this app only starts/stops it and feeds voice).
// All of these are safe no-ops (NO / nothing) when Unity is absent, e.g. on the simulator.
/// YES when Unity is loaded and exports all five SportsCapture* functions.
- (BOOL)captureAvailable;
/// `configJSON`: width, height, fps (30|60), bitrateMbps, gameAudio, voice, mic, display, gainGame, gainVoice, gainMic.
/// YES when recording started; NO when refused (an event with the reason is delivered to the handler).
- (BOOL)startCapture:(NSString*)configJSON;
/// Finalizes asynchronously; a "finished" or "failed" event always follows.
- (void)stopCapture;
- (BOOL)captureActive;
/// Mono float samples; callable from a real-time audio thread. source 0 = remote party voice, 1 = own microphone.
- (void)captureVoice:(const float*)samples frames:(int)frames rate:(int)rate source:(int)source;
/// Receives each capture event as JSON on the main queue. Pass nil to stop receiving.
/// May be set before Unity has loaded; it is registered as soon as the symbols resolve.
- (void)setCaptureHandler:(nullable void (^)(NSString *eventJSON))handler;
@end
NS_ASSUME_NONNULL_END
