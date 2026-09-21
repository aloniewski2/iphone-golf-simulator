#import <UIKit/UIKit.h>
NS_ASSUME_NONNULL_BEGIN
@interface SportsRuntime : NSObject
+ (instancetype)shared;
- (BOOL)loadInWindow:(UIWindow*)window error:(NSError**)error;
- (void)attachToWindow:(UIWindow*)window;
- (void)send:(NSString*)json;
- (void)push:(NSString*)json;
- (nullable NSString*)pollEvent;
- (double)clock;
- (void)pause:(BOOL)paused;
- (void)setForeground:(BOOL)foreground;
@end
NS_ASSUME_NONNULL_END
