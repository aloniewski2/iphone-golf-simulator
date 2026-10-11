// Minimal macOS stand-in for the few UIKit symbols SportsSessionRecorder.mm uses, so the real
// recorder can be exercised on a Mac (session_recorder_harness.mm). Test-only; never shipped.
#import <Foundation/Foundation.h>
typedef NSUInteger UIBackgroundTaskIdentifier;
static const UIBackgroundTaskIdentifier UIBackgroundTaskInvalid = 0;
extern NSString *const UIApplicationWillResignActiveNotification;
@interface UIApplication : NSObject
+ (UIApplication *)sharedApplication;
- (UIBackgroundTaskIdentifier)beginBackgroundTaskWithName:(NSString *)name expirationHandler:(void (^)(void))handler;
- (void)endBackgroundTask:(UIBackgroundTaskIdentifier)identifier;
@end
