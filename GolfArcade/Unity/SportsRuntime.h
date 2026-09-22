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
} SportsSample;
enum { SportsSampleVersion = 2, SportsSampleValid = 1, SportsSampleDegraded = 2 };
@interface SportsRuntime : NSObject
+ (instancetype)shared;
- (BOOL)loadInWindow:(UIWindow*)window error:(NSError**)error;
- (void)attachToWindow:(UIWindow*)window;
- (void)send:(NSString*)json;
- (void)push:(NSString*)json;
- (void)pushSample:(SportsSample)sample;
- (nullable NSString*)pollEvent;
- (double)clock;
- (void)pause:(BOOL)paused;
- (void)setForeground:(BOOL)foreground;
@end
NS_ASSUME_NONNULL_END
