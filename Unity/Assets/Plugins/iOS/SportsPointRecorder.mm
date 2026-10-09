#import "SportsPointRecorder.h"
#import <AVFoundation/AVFoundation.h>
#include <mutex>
#import <CoreVideo/CoreVideo.h>
#include <atomic>
#include <cmath>

extern "C" void SportsEmit(const char *);

// 60 unique rendered frames/second. Never duplicate frames or silently fall back to 30.
static const int ClipFPS=60, ClipWidth=1920, ClipHeight=1080;
static const double MaxFrameGap=1.5/ClipFPS, MinMeasuredFPS=59.5, MaxPointSeconds=120;

@interface SportsPointTake : NSObject {
@public
    std::atomic<int> pending;
    std::atomic<bool> failed;
    std::atomic<double> audioOrigin;
    std::atomic<int> audioPending;
}
@property(nonatomic,strong) AVAssetWriter *writer;
@property(nonatomic,strong) AVAssetWriterInput *video;
@property(nonatomic,strong) AVAssetWriterInput *audio;
@property(nonatomic) NSInteger audioFrames;
@property(nonatomic,strong) AVAssetWriterInputPixelBufferAdaptor *pixels;
@property(nonatomic,strong) NSURL *url;
@property(nonatomic,strong) dispatch_group_t work;
@property(nonatomic) BOOL probe;
@property(nonatomic) NSInteger frames;
@property(nonatomic) double firstTime, lastTime, measuredStart;
@property(nonatomic) NSInteger measuredFrames;
@property(nonatomic) NSUInteger generation;
@end
@implementation SportsPointTake
- (instancetype)init { if((self=[super init])) { pending=0; failed=false; audioOrigin=0; audioPending=0; _work=dispatch_group_create(); } return self; }
@end

@implementation SportsPointRecorder {
    NSString *_session;
    int _display;
    BOOL _enabled, _qualified, _paused, _finishing;
    NSUInteger _generation;
    SportsPointTake *_take;
    SportsPointTake *_audioTake;
    std::mutex _audioMutex;
    std::recursive_mutex _stateMutex;
    NSURL *_lastPoint;
    dispatch_queue_t _encode;
    id<MTLRenderPipelineState> _pipeline;
    CVMetalTextureCacheRef _textureCache;
    double _lastCapture, _endAt;
}
+ (instancetype)shared { static SportsPointRecorder *r; static dispatch_once_t once; dispatch_once(&once,^{r=[self new];}); return r; }
- (instancetype)init { if((self=[super init])) _encode=dispatch_queue_create("club.point-encoder",DISPATCH_QUEUE_SERIAL); return self; }
- (int)display {
    std::lock_guard<std::recursive_mutex> stateLock(_stateMutex); return _display; }
- (BOOL)needsFrames {
    std::lock_guard<std::recursive_mutex> stateLock(_stateMutex); return _enabled && !_paused && _take!=nil; }
- (NSURL *)bufferDirectory {
    NSURL *url=[NSURL fileURLWithPath:[NSTemporaryDirectory() stringByAppendingPathComponent:@"MotionClubPointBuffer"] isDirectory:YES];
    [[NSFileManager defaultManager] createDirectoryAtURL:url withIntermediateDirectories:YES attributes:nil error:nil]; return url;
}
- (void)emit:(NSString *)state detail:(NSString *)detail url:(NSURL *)url {
    if(!_session.length) return;
    NSMutableDictionary *event=[@{@"version":@1,@"session":_session,@"type":@"recording",@"state":state,@"message":detail?:@"",@"hasClip":@(_lastPoint!=nil),@"enabled":@(_enabled)} mutableCopy];
    if(url) event[@"url"]=url.path;
    NSData *data=[NSJSONSerialization dataWithJSONObject:event options:0 error:nil];
    SportsEmit([[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding].UTF8String);
}
- (void)configureSession:(NSString *)session display:(int)display {
    std::lock_guard<std::recursive_mutex> stateLock(_stateMutex);
    [self stop]; _session=[session copy]; _display=display;
    [self emit:@"off" detail:@"Enable 60 fps point clips before playing." url:nil];
}
- (void)discardTake {
    SportsPointTake *take=_take; _take=nil; _endAt=0;
    { std::lock_guard<std::mutex> lock(_audioMutex); _audioTake=nil; }
    if(!take) return;
    take->failed=true;
    dispatch_group_notify(take.work,_encode,^{
        [take.writer cancelWriting]; [[NSFileManager defaultManager] removeItemAtURL:take.url error:nil];
    });
}
- (void)stop {
    std::lock_guard<std::recursive_mutex> stateLock(_stateMutex);
    _generation++; _enabled=NO; _qualified=NO; _paused=NO; _finishing=NO;
    [self discardTake];
    if(_lastPoint) [[NSFileManager defaultManager] removeItemAtURL:_lastPoint error:nil];
    _lastPoint=nil;
}
- (void)fail:(NSString *)reason {
    _generation++; _finishing=NO; _enabled=NO; _qualified=NO; [self discardTake];
    [self emit:@"unsupported" detail:reason url:nil];
}
- (BOOL)conditionsAllowRecording {
    if(NSProcessInfo.processInfo.lowPowerModeEnabled) { [self fail:@"Turn off Low Power Mode to check 60 fps recording."]; return NO; }
    if(NSProcessInfo.processInfo.thermalState>=NSProcessInfoThermalStateSerious) { [self fail:@"Recording disabled: the phone is too warm to maintain 60 fps."]; return NO; }
    return YES;
}
- (void)setEnabled:(BOOL)enabled {
    std::lock_guard<std::recursive_mutex> stateLock(_stateMutex);
    if(!enabled) { _generation++; _finishing=NO; [self discardTake]; _enabled=NO; _qualified=NO; [self emit:@"off" detail:@"Point buffering off." url:nil]; return; }
    if(_enabled) return;
    if(![self conditionsAllowRecording]) return;
    _enabled=YES; _qualified=NO;
    if(!_paused) [self startTake:YES];
    [self emit:@"checking" detail:_paused?@"Resume play to check 1080p · 60 fps.":@"Checking 1080p · 60 fps…" url:nil];
}
- (void)setPaused:(BOOL)paused {
    std::lock_guard<std::recursive_mutex> stateLock(_stateMutex);
    if(_paused==paused) return;
    _paused=paused;
    if(paused) { [self discardTake]; if(_enabled) [self emit:@"waiting" detail:@"Paused — this incomplete point will not be saved." url:nil]; }
    else if(_enabled) {
        if(!_qualified) [self startTake:YES];
        [self emit:_qualified?@"ready":@"checking" detail:_qualified?@"Ready — the next complete point will be buffered.":@"Checking 1080p · 60 fps…" url:nil];
    }
}
- (void)startTake:(BOOL)probe {
    if(_take || _finishing || !_enabled || _paused) return;
    if(![self conditionsAllowRecording]) return;
    NSURL *volume=[self bufferDirectory]; NSNumber *free=nil;
    [volume getResourceValue:&free forKey:NSURLVolumeAvailableCapacityForImportantUsageKey error:nil];
    if(free && free.longLongValue<500*1024*1024) { [self fail:@"Recording needs at least 500 MB of free space."]; return; }
    SportsPointTake *take=[SportsPointTake new]; take.probe=probe; take.generation=_generation;
    take.url=[volume URLByAppendingPathComponent:[NSUUID.UUID.UUIDString stringByAppendingString:@".mp4"]];
    NSError *error=nil; take.writer=[[AVAssetWriter alloc] initWithURL:take.url fileType:AVFileTypeMPEG4 error:&error];
    NSDictionary *settings=@{AVVideoCodecKey:AVVideoCodecTypeH264,AVVideoWidthKey:@(ClipWidth),AVVideoHeightKey:@(ClipHeight),
        AVVideoCompressionPropertiesKey:@{AVVideoAverageBitRateKey:@14000000,AVVideoExpectedSourceFrameRateKey:@(ClipFPS),AVVideoMaxKeyFrameIntervalKey:@60,AVVideoAllowFrameReorderingKey:@NO,AVVideoProfileLevelKey:AVVideoProfileLevelH264HighAutoLevel}};
    take.video=[AVAssetWriterInput assetWriterInputWithMediaType:AVMediaTypeVideo outputSettings:settings]; take.video.expectsMediaDataInRealTime=YES;
    take.pixels=[AVAssetWriterInputPixelBufferAdaptor assetWriterInputPixelBufferAdaptorWithAssetWriterInput:take.video sourcePixelBufferAttributes:
        @{(id)kCVPixelBufferPixelFormatTypeKey:@(kCVPixelFormatType_32BGRA),(id)kCVPixelBufferWidthKey:@(ClipWidth),(id)kCVPixelBufferHeightKey:@(ClipHeight),(id)kCVPixelBufferMetalCompatibilityKey:@YES,(id)kCVPixelBufferIOSurfacePropertiesKey:@{}}];
    if(!take.writer || ![take.writer canAddInput:take.video]) { [self fail:@"1080p60 video encoding is unavailable on this phone."]; return; }
    [take.writer addInput:take.video];
    take.audio=[AVAssetWriterInput assetWriterInputWithMediaType:AVMediaTypeAudio outputSettings:@{AVFormatIDKey:@(kAudioFormatMPEG4AAC),AVSampleRateKey:@48000,AVNumberOfChannelsKey:@2,AVEncoderBitRateKey:@192000}];
    take.audio.expectsMediaDataInRealTime=YES;
    if(![take.writer canAddInput:take.audio]) { [self fail:@"Game audio encoding is unavailable."]; return; }
    [take.writer addInput:take.audio]; take.writer.shouldOptimizeForNetworkUse=YES;
    if(![take.writer startWriting]) { [self fail:@"The phone could not start 1080p60 video recording."]; return; }
    [take.writer startSessionAtSourceTime:kCMTimeZero];
    _take=take; { std::lock_guard<std::mutex> lock(_audioMutex); _audioTake=take; } _lastCapture=0; _endAt=0;
}
- (void)beginPoint {
    std::lock_guard<std::recursive_mutex> stateLock(_stateMutex);
    if(!_enabled || !_qualified || _paused) return;
    // Never overwrite a point still being finalized, or begin halfway through one.
    if(_take) [self discardTake];
    [self startTake:NO];
    if(_take) [self emit:@"buffering" detail:@"Buffering this point · 1080p60" url:nil];
}
- (void)endPoint {
    std::lock_guard<std::recursive_mutex> stateLock(_stateMutex);
    if(_take && !_take.probe && !_endAt) _endAt=NSProcessInfo.processInfo.systemUptime+1.0;
}
- (void)finishTake {
    SportsPointTake *take=_take; _take=nil; _endAt=0;
    { std::lock_guard<std::mutex> lock(_audioMutex); _audioTake=nil; } _finishing=YES;
    dispatch_group_notify(take.work,_encode,^{
        if(take->failed || take.frames<2 || take.audioFrames==0 || (take.measuredFrames-1)/MAX(.001,take.lastTime-take.measuredStart)<MinMeasuredFPS) {
            [take.writer cancelWriting];
            dispatch_async(dispatch_get_main_queue(),^{ std::lock_guard<std::recursive_mutex> stateLock(self->_stateMutex); if(take.generation==self->_generation) { self->_finishing=NO; [self fail:@"Recording disabled: sustained 60 fps could not be maintained."]; } });
            [[NSFileManager defaultManager] removeItemAtURL:take.url error:nil]; return;
        }
        [take.video markAsFinished]; [take.audio markAsFinished];
        [take.writer endSessionAtSourceTime:CMTimeMake(take.frames,ClipFPS)];
        [take.writer finishWritingWithCompletionHandler:^{ dispatch_async(dispatch_get_main_queue(),^{
            std::lock_guard<std::recursive_mutex> stateLock(self->_stateMutex);
            if(take.generation!=self->_generation) { [[NSFileManager defaultManager] removeItemAtURL:take.url error:nil]; return; }
            self->_finishing=NO;
            if(take.writer.status!=AVAssetWriterStatusCompleted) { [[NSFileManager defaultManager] removeItemAtURL:take.url error:nil]; [self fail:@"The clip could not be finalized."]; return; }
            if(take.probe) {
                [[NSFileManager defaultManager] removeItemAtURL:take.url error:nil];
                if(!self->_enabled) return;
                self->_qualified=YES; [self emit:@"ready" detail:@"60 fps verified. Recording starts with the next point." url:nil];
            } else {
                if(self->_lastPoint) [[NSFileManager defaultManager] removeItemAtURL:self->_lastPoint error:nil];
                self->_lastPoint=take.url; [self emit:@"available" detail:@"Last point ready · 1080p60" url:nil];
            }
        }); }];
    });
}
- (BOOL)prepareMetal:(id<MTLDevice>)device {
    if(_pipeline && _textureCache) return YES;
    NSString *shader=@"#include <metal_stdlib>\nusing namespace metal; struct V { float4 p [[position]]; float2 uv; }; vertex V vtx(uint n [[vertex_id]]) { float2 p=float2((n<<1)&2,n&2); V o; o.p=float4(p*2-1,0,1); o.uv=float2(p.x,1-p.y); return o; } fragment float4 frag(V i [[stage_in]],texture2d<float> t [[texture(0)]],constant float &gamma [[buffer(0)]]) { constexpr sampler s(filter::linear,address::clamp_to_edge); float3 c=t.sample(s,i.uv).rgb; if(gamma>0.5) c=select(c*12.92,1.055*pow(max(c,0.0),float3(1.0/2.4))-0.055,c>0.0031308); return float4(c,1); }";
    NSError *error=nil; id<MTLLibrary> library=[device newLibraryWithSource:shader options:nil error:&error];
    if(!library) return NO;
    MTLRenderPipelineDescriptor *desc=[MTLRenderPipelineDescriptor new]; desc.vertexFunction=[library newFunctionWithName:@"vtx"]; desc.fragmentFunction=[library newFunctionWithName:@"frag"]; desc.colorAttachments[0].pixelFormat=MTLPixelFormatBGRA8Unorm;
    _pipeline=[device newRenderPipelineStateWithDescriptor:desc error:&error];
    return _pipeline && CVMetalTextureCacheCreate(kCFAllocatorDefault,nil,device,nil,&_textureCache)==kCVReturnSuccess;
}
- (void)captureTexture:(id<MTLTexture>)source queue:(id<MTLCommandQueue>)queue timestamp:(double)now {
    id<MTLCommandBuffer> command=[queue commandBuffer];
    [self captureTexture:source commandBuffer:command timestamp:now];
    [command commit];
}
- (void)captureTexture:(id<MTLTexture>)source commandBuffer:(id<MTLCommandBuffer>)command timestamp:(double)now {
    std::lock_guard<std::recursive_mutex> stateLock(_stateMutex);
    if(!self.needsFrames || !command) return;
    SportsPointTake *take=_take;
    if(take->failed) { [self fail:@"Recording disabled: the encoder could not keep up at 60 fps."]; return; }
    if(![self conditionsAllowRecording]) return;
    if(!source || source.framebufferOnly || source.width<640 || source.height<360) { [self fail:@"This display does not expose a recordable gameplay surface."]; return; }
    if(source.pixelFormat!=MTLPixelFormatBGRA8Unorm && source.pixelFormat!=MTLPixelFormatBGRA8Unorm_sRGB && source.pixelFormat!=MTLPixelFormatRGBA8Unorm && source.pixelFormat!=MTLPixelFormatRGBA8Unorm_sRGB) { [self fail:@"This display color format is not supported for clips."]; return; }
    if(!_pipeline || !_textureCache) { if(![self prepareMetal:source.device]) [self fail:@"GPU video capture is unavailable on this phone."]; return; }
    if(_lastCapture>0 && now-_lastCapture<.012) return; // 120Hz renderers: take only unique 60Hz frames.
    if(_lastCapture>0 && (!take.probe || now-take.firstTime>1) && now-_lastCapture>MaxFrameGap) { NSLog(@"[PointClips] rejected frame gap %.2f ms",(now-_lastCapture)*1000); [self fail:@"Recording disabled: gameplay dropped below 60 fps. Try again after the phone cools down."]; return; }
    _lastCapture=now;
    if(take.firstTime>0 && now-take.firstTime>MaxPointSeconds) { [self discardTake]; [self emit:@"ready" detail:@"That point exceeded the two-minute clip limit; waiting for the next point." url:nil]; return; }
    if((take.probe && take.firstTime>0 && now-take.firstTime>=4.1) || (_endAt>0 && now>=_endAt)) { [self finishTake]; return; }
    if(![self prepareMetal:source.device]) { [self fail:@"GPU video capture is unavailable on this phone."]; return; }
    if(take->pending.load()>=3) { [self fail:@"Recording disabled: GPU capture could not sustain 60 fps."]; return; }
    CVPixelBufferRef pixel=nil; CVMetalTextureRef mapped=nil;
    if(!take.pixels.pixelBufferPool || CVPixelBufferPoolCreatePixelBuffer(kCFAllocatorDefault,take.pixels.pixelBufferPool,&pixel)!=kCVReturnSuccess) { [self fail:@"Not enough memory to record at 60 fps."]; return; }
    if(CVMetalTextureCacheCreateTextureFromImage(kCFAllocatorDefault,_textureCache,pixel,nil,MTLPixelFormatBGRA8Unorm,ClipWidth,ClipHeight,0,&mapped)!=kCVReturnSuccess) { CVPixelBufferRelease(pixel); [self fail:@"GPU video texture allocation failed."]; return; }
    command.label=@"Motion Club 60fps point capture";
    MTLRenderPassDescriptor *pass=[MTLRenderPassDescriptor renderPassDescriptor]; pass.colorAttachments[0].texture=CVMetalTextureGetTexture(mapped); pass.colorAttachments[0].loadAction=MTLLoadActionClear; pass.colorAttachments[0].storeAction=MTLStoreActionStore; pass.colorAttachments[0].clearColor=MTLClearColorMake(0,0,0,1);
    id<MTLRenderCommandEncoder> encoder=[command renderCommandEncoderWithDescriptor:pass];
    float scale=fmin((float)ClipWidth/source.width,(float)ClipHeight/source.height); double w=source.width*scale,h=source.height*scale;
    [encoder setViewport:(MTLViewport){(ClipWidth-w)*.5,(ClipHeight-h)*.5,w,h,0,1}];
    float gamma=(source.pixelFormat==MTLPixelFormatBGRA8Unorm_sRGB || source.pixelFormat==MTLPixelFormatRGBA8Unorm_sRGB)?1:0;
    [encoder setRenderPipelineState:_pipeline]; [encoder setFragmentTexture:source atIndex:0]; [encoder setFragmentBytes:&gamma length:sizeof(gamma) atIndex:0]; [encoder drawPrimitives:MTLPrimitiveTypeTriangle vertexStart:0 vertexCount:3]; [encoder endEncoding];
    if(take.firstTime==0) { take.firstTime=now; take->audioOrigin=now; }
    take->pending++; dispatch_group_enter(take.work);
    [command addCompletedHandler:^(id<MTLCommandBuffer> completed) {
        dispatch_async(self->_encode,^{
            if(!take->failed) {
                if(completed.status!=MTLCommandBufferStatusCompleted || !take.video.readyForMoreMediaData || ![take.pixels appendPixelBuffer:pixel withPresentationTime:CMTimeMake(take.frames,ClipFPS)]) take->failed=true;
                else {
                    take.frames++; take.lastTime=now;
                    if(!take.probe || now-take.firstTime>=1) {
                        if(take.measuredFrames==0) take.measuredStart=now;
                        take.measuredFrames++;
                    }
                }
            }
            CFRelease(mapped); CVPixelBufferRelease(pixel); take->pending--; dispatch_group_leave(take.work);
        });
    }];
}
// Called on Unity's audio thread. The published take and work-group entry are atomic
// with respect to finalization; file/encoder work stays off the audio thread.
- (void)captureAudio:(const float *)samples count:(int)count channels:(int)channels rate:(int)rate timestamp:(double)now {
    if(!samples || channels<1 || channels>2 || rate<8000 || count<channels) return;
    SportsPointTake *take;
    { std::lock_guard<std::mutex> lock(_audioMutex);
      take=_audioTake;
      if(!take || take->failed || take->audioOrigin.load()==0) return;
      if(take->audioPending.load()>=6) { take->failed=true; return; }
      take->audioPending++; dispatch_group_enter(take.work);
    }
    int frames=count/channels;
    double relative=now-take->audioOrigin.load();
    int skip=relative<0 ? MIN(frames,(int)ceil(-relative*rate)) : 0;
    frames-=skip; relative+=double(skip)/rate;
    NSMutableData *pcm=[NSMutableData dataWithLength:frames*2*sizeof(float)];
    float *out=(float *)pcm.mutableBytes;
    for(int f=0;f<frames;f++) { out[f*2]=samples[(f+skip)*channels]; out[f*2+1]=samples[(f+skip)*channels+channels-1]; }
    dispatch_async(_encode,^{
        if(!take->failed && frames>0) {
            AudioStreamBasicDescription asbd={}; asbd.mSampleRate=rate; asbd.mFormatID=kAudioFormatLinearPCM;
            asbd.mFormatFlags=kAudioFormatFlagIsFloat|kAudioFormatFlagIsPacked; asbd.mBytesPerPacket=8;
            asbd.mFramesPerPacket=1; asbd.mBytesPerFrame=8; asbd.mChannelsPerFrame=2; asbd.mBitsPerChannel=32;
            CMAudioFormatDescriptionRef format=nullptr; CMBlockBufferRef block=nullptr; CMSampleBufferRef sample=nullptr;
            OSStatus error=CMAudioFormatDescriptionCreate(kCFAllocatorDefault,&asbd,0,nullptr,0,nullptr,nullptr,&format);
            if(!error) error=CMBlockBufferCreateWithMemoryBlock(kCFAllocatorDefault,nullptr,pcm.length,kCFAllocatorDefault,nullptr,0,pcm.length,0,&block);
            if(!error) error=CMBlockBufferReplaceDataBytes(pcm.bytes,block,0,pcm.length);
            CMSampleTimingInfo timing={CMTimeMake(1,rate),CMTimeMakeWithSeconds(MAX(0,relative),rate),kCMTimeInvalid};
            size_t bytes=8;
            if(!error) error=CMSampleBufferCreateReady(kCFAllocatorDefault,block,format,frames,1,&timing,1,&bytes,&sample);
            if(error || !take.audio.readyForMoreMediaData || ![take.audio appendSampleBuffer:sample]) take->failed=true;
            else take.audioFrames+=frames;
            if(sample) CFRelease(sample); if(block) CFRelease(block); if(format) CFRelease(format);
        }
        take->audioPending--; dispatch_group_leave(take.work);
    });
}
- (void)saveLastPoint {
    std::lock_guard<std::recursive_mutex> stateLock(_stateMutex);
    if(!_lastPoint) { [self emit:@"waiting" detail:@"Finish a full point with clips enabled first." url:nil]; return; }
    NSURL *folder=[[[NSFileManager defaultManager] URLsForDirectory:NSDocumentDirectory inDomains:NSUserDomainMask].firstObject URLByAppendingPathComponent:@"PointClips" isDirectory:YES];
    [[NSFileManager defaultManager] createDirectoryAtURL:folder withIntermediateDirectories:YES attributes:nil error:nil];
    NSArray *files=[[NSFileManager defaultManager] contentsOfDirectoryAtURL:folder includingPropertiesForKeys:nil options:NSDirectoryEnumerationSkipsHiddenFiles error:nil];
    if(files.count>=30) { [self emit:@"saveFailed" detail:@"Your Clips library is full. Delete a clip before saving another." url:nil]; return; }
    NSURL *url=[folder URLByAppendingPathComponent:[NSString stringWithFormat:@"MotionClub-%@.mp4",NSUUID.UUID.UUIDString]];
    NSError *error=nil;
    if(![[NSFileManager defaultManager] copyItemAtURL:_lastPoint toURL:url error:&error]) { [self emit:@"saveFailed" detail:@"Not enough storage to save this clip." url:nil]; return; }
    [self emit:@"saved" detail:@"Saved · 1080p60" url:url];
}
@end
