#import <Foundation/Foundation.h>
#import <Metal/Metal.h>

/// Encodes only the Unity gameplay surface. No camera, microphone or phone UI capture.
@interface SportsPointRecorder : NSObject
+ (instancetype)shared;
- (void)configureSession:(NSString *)session display:(int)display;
- (void)setEnabled:(BOOL)enabled;
- (void)setPaused:(BOOL)paused;
- (void)beginPoint;
- (void)endPoint;
- (void)saveLastPoint;
- (void)captureAudio:(const float *)samples count:(int)count channels:(int)channels rate:(int)rate timestamp:(double)timestamp;
- (void)stop;
- (void)captureTexture:(id<MTLTexture>)texture queue:(id<MTLCommandQueue>)queue timestamp:(double)timestamp;
- (void)captureTexture:(id<MTLTexture>)texture commandBuffer:(id<MTLCommandBuffer>)command timestamp:(double)timestamp;
@property(nonatomic,readonly) int display;
@property(nonatomic,readonly) BOOL needsFrames;
@end
