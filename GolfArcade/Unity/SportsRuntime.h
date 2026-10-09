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
/// Classic golf: start Unity (once) on the phone's own screen, without a TV. Its window stays
/// hidden until `showUnityOnPhone:YES`.
- (BOOL)loadOnPhone:(NSError**)error;
/// Puts Unity's own phone window in front (classic golf), or hides it again for the club.
- (void)showUnityOnPhone:(BOOL)visible;
- (void)attachToWindow:(UIWindow*)window;
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
@end
NS_ASSUME_NONNULL_END
