#import "SportsSessionRecorder.h"
#import <AVFoundation/AVFoundation.h>
#import <CoreMedia/CoreMedia.h>
#import <CoreVideo/CoreVideo.h>
#import <UIKit/UIKit.h>
#include <atomic>
#include <cmath>
#include <deque>
#include <mutex>
#include <vector>
#include "SportsAudioMixer.h"

// Session policy (not point-clip policy): the TV picture is recorded for as long as the player
// wants, at a variable frame rate capped to `fps`, with real elapsed time as the video clock.
// Frame gaps and low fps never fail a session; an encoder that falls behind drops frames.
// Only a writer error ends it as "failed".

static const double kMinFreeToStart = 1024.0 * 1024.0 * 1024.0;   // 1 GB
static const double kMinFreeWhileRecording = 300.0 * 1024.0 * 1024.0;
static const double kMaxSessionSeconds = 90.0 * 60.0;
static const double kWatchdogInterval = 0.5, kDiskInterval = 5.0;
static const int kMaxPendingFrames = 4;
static const double kDisplayMissingSeconds = 2.0;
static const double kNoFrameBackstopSeconds = 15.0, kFirstFrameTimeoutSeconds = 10.0;
static const size_t kMaxQueuedAudio = 300;   // ~3 s of 10 ms ticks waiting for the writer

enum { kIdle = 0, kActive = 1, kFinishing = 2 };

struct SessionConfig {
    int width = 1920, height = 1080, fps = 60;
    double mbps = 12.0;
    bool gameAudio = true, voice = true, mic = true;
    int display = 1;
    float gainGame = 1.0f, gainVoice = 1.0f, gainMic = 1.0f;
    double fragmentSeconds = 10.0;   // extra, not in the spec: 0 disables movie fragments
    double maxSeconds = kMaxSessionSeconds;   // extra: tests may lower it
};

static NSString *ParseConfig(const char *json, SessionConfig *out) {
    SessionConfig c;
    if (json && json[0]) {
        NSData *data = [NSData dataWithBytes:json length:strlen(json)];
        id object = [NSJSONSerialization JSONObjectWithData:data options:0 error:nil];
        if (![object isKindOfClass:NSDictionary.class]) return @"The recording settings could not be read.";
        NSDictionary *d = object;
        auto num = ^NSNumber *(NSString *key) { id v = d[key]; return [v isKindOfClass:NSNumber.class] ? v : nil; };
        if (NSNumber *v = num(@"width")) c.width = v.intValue;
        if (NSNumber *v = num(@"height")) c.height = v.intValue;
        if (NSNumber *v = num(@"fps")) c.fps = v.intValue;
        if (NSNumber *v = num(@"bitrateMbps")) c.mbps = v.doubleValue;
        if (NSNumber *v = num(@"gameAudio")) c.gameAudio = v.boolValue;
        if (NSNumber *v = num(@"voice")) c.voice = v.boolValue;
        if (NSNumber *v = num(@"mic")) c.mic = v.boolValue;
        if (NSNumber *v = num(@"display")) c.display = v.intValue;
        if (NSNumber *v = num(@"gainGame")) c.gainGame = v.floatValue;
        if (NSNumber *v = num(@"gainVoice")) c.gainVoice = v.floatValue;
        if (NSNumber *v = num(@"gainMic")) c.gainMic = v.floatValue;
        if (NSNumber *v = num(@"fragmentSeconds")) c.fragmentSeconds = v.doubleValue;
        if (NSNumber *v = num(@"maxMinutes")) c.maxSeconds = std::min(kMaxSessionSeconds, std::max(0.05, v.doubleValue) * 60.0);
    }
    if (c.fps != 30 && c.fps != 60) return @"Recording runs at 30 or 60 frames per second.";
    if (c.width < 320 || c.height < 180 || c.width > 3840 || c.height > 2160 || (c.width & 1) || (c.height & 1)) return @"That recording size is not supported.";
    if (c.display < 0) return @"That display is not available.";
    c.mbps = std::min(60.0, std::max(1.0, c.mbps));
    auto gain = [](float g) { return std::isfinite(g) ? std::min(8.0f, std::max(0.0f, g)) : 1.0f; };
    c.gainGame = gain(c.gainGame); c.gainVoice = gain(c.gainVoice); c.gainMic = gain(c.gainMic);
    c.fragmentSeconds = std::isfinite(c.fragmentSeconds) ? std::min(60.0, std::max(0.0, c.fragmentSeconds)) : 10.0;
    *out = c;
    return nil;
}

static double FreeBytes(NSURL *directory) {
    NSNumber *free = nil;
    if ([directory getResourceValue:&free forKey:NSURLVolumeAvailableCapacityForImportantUsageKey error:nil] && free) return free.doubleValue;
    NSDictionary *attrs = [[NSFileManager defaultManager] attributesOfFileSystemForPath:directory.path error:nil];
    NSNumber *size = attrs[NSFileSystemFreeSize];
    return size ? size.doubleValue : 1e18;   // unknown: do not block
}

static NSString *MessageForReason(NSString *reason) {
    if ([reason isEqualToString:@"background"]) return @"Recording stopped because the app left the screen. Your video was saved.";
    if ([reason isEqualToString:@"thermal"]) return @"Recording stopped because the phone is too hot. Your video was saved.";
    if ([reason isEqualToString:@"storage"]) return @"Recording stopped because the phone is almost out of storage. Your video was saved.";
    if ([reason isEqualToString:@"display"]) return @"Recording stopped because the TV picture went away. Your video was saved.";
    if ([reason isEqualToString:@"limit"]) return @"Recording stopped at the 90 minute limit. Your video was saved.";
    return @"Recording saved.";
}

#pragma mark - Take

@interface SportsSessionTake : NSObject {
@public
    std::atomic<int> pending;
    std::atomic<bool> failed, stopping;
    std::atomic<double> firstTime;     // uptime of the first video frame, 0 until it arrives
    std::atomic<uint64_t> frames, dropped;
    std::atomic<double> lastPts;       // presentation time of the last appended video frame
    double lastEncode;                 // render thread only
    sports::PumpClock clock;           // audio queue only
    sports::AudioPts apts;             // audio queue only
    std::vector<float> tick;           // audio queue only
    std::deque<CMSampleBufferRef> queued;   // audio queue only (retained)
    uint64_t audioDropped;             // audio queue only
    uint64_t droppedReported;          // control queue only
}
@property(nonatomic,strong) AVAssetWriter *writer;
@property(nonatomic,strong) AVAssetWriterInput *video;
@property(nonatomic,strong) AVAssetWriterInput *audio;
@property(nonatomic,strong) AVAssetWriterInputPixelBufferAdaptor *pixels;
@property(nonatomic,strong) NSURL *url;
@property(nonatomic,strong) dispatch_group_t work;
@property(nonatomic) SessionConfig config;
@property(nonatomic) BOOL hasAudio;
@property(nonatomic) CMAudioFormatDescriptionRef audioFormat;
@property(nonatomic) double startUptime;
@end
@implementation SportsSessionTake
- (instancetype)init {
    if ((self = [super init])) {
        pending = 0; failed = false; stopping = false; firstTime = 0; frames = 0; dropped = 0; lastPts = 0;
        lastEncode = 0; audioDropped = 0; droppedReported = 0; _work = dispatch_group_create();
    }
    return self;
}
- (void)dealloc {
    for (CMSampleBufferRef s : queued) CFRelease(s);
    if (_audioFormat) CFRelease(_audioFormat);
}
@end

#pragma mark - Recorder

@implementation SportsSessionRecorder {
    dispatch_queue_t _control, _encode, _audioQueue;
    std::mutex _takeMutex;
    SportsSessionTake *_take;
    std::atomic<int> _state;
    std::atomic<int> _display;
    std::atomic<void (*)(const char *)> _callback;
    std::atomic<double> _lastFrameSeen;
    std::atomic<bool> _surfaceOK;
    sports::AudioMixer _mixer;
    dispatch_source_t _watchdog, _audioTimer;
    // render thread only
    id<MTLRenderPipelineState> _pipeline;
    CVMetalTextureCacheRef _textureCache;
    __weak id<MTLDevice> _cacheDevice;
    // control queue only
    double _lastDiskCheck, _missingSince;
    BOOL _thermalWarned, _dropWarned;
    double _lastDropWarning;
}

+ (instancetype)shared { static SportsSessionRecorder *r; static dispatch_once_t once; dispatch_once(&once, ^{ r = [self new]; }); return r; }

- (instancetype)init {
    if ((self = [super init])) {
        _state = kIdle; _display = 1; _callback = nullptr; _lastFrameSeen = 0; _surfaceOK = true;
        _control = dispatch_queue_create("club.session-control", DISPATCH_QUEUE_SERIAL);
        _encode = dispatch_queue_create("club.session-encoder", DISPATCH_QUEUE_SERIAL);
        dispatch_queue_attr_t rt = dispatch_queue_attr_make_with_qos_class(DISPATCH_QUEUE_SERIAL, QOS_CLASS_USER_INITIATED, 0);
        _audioQueue = dispatch_queue_create("club.session-audio", rt);
        NSNotificationCenter *nc = NSNotificationCenter.defaultCenter;
        [nc addObserver:self selector:@selector(willResignActive:) name:UIApplicationWillResignActiveNotification object:nil];
        [nc addObserver:self selector:@selector(thermalChanged:) name:NSProcessInfoThermalStateDidChangeNotification object:nil];
    }
    return self;
}

- (BOOL)active { return _state.load() == kActive; }
- (BOOL)needsFrames { return _state.load() == kActive; }
- (int)display { return _display.load(); }
- (void)setEventCallback:(void (*)(const char *))callback { _callback.store(callback); }

#pragma mark Events

- (void)emit:(NSString *)state message:(NSString *)message reason:(NSString *)reason extra:(NSDictionary *)extra {
    NSMutableDictionary *event = [@{@"type": @"capture", @"state": state, @"message": message ?: @""} mutableCopy];
    if (reason) event[@"reason"] = reason;
    if (extra) [event addEntriesFromDictionary:extra];
    NSData *data = [NSJSONSerialization dataWithJSONObject:event options:0 error:nil];
    NSString *json = data ? [[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding] : nil;
    if (!json) return;
    NSLog(@"[SessionCapture] %@", json);
    void (*callback)(const char *) = _callback.load();
    if (!callback) return;
    dispatch_async(dispatch_get_main_queue(), ^{ callback(json.UTF8String); });
}
- (void)emitFailure:(NSString *)message reason:(NSString *)reason { [self emit:@"failed" message:message reason:reason ?: @"error" extra:nil]; }

#pragma mark Start / stop

- (int)startWithConfigJSON:(const char *)json {
    SessionConfig config;
    NSString *problem = ParseConfig(json, &config);
    if (problem) { [self emitFailure:problem reason:@"error"]; return 0; }
    if (_state.load() != kIdle) {
        [self emit:@"warning" message:_state.load() == kActive ? @"A recording is already running." : @"The last recording is still being saved. Try again in a moment." reason:@"error" extra:nil];
        return 0;
    }
    // The Unity-aware display check runs on the main thread, before taking the control queue
    // (the control queue never waits for the main thread).
    NSString *(^check)(int) = self.displayCheck;
    if (check) {
        __block NSString *refusal = nil;
        if (NSThread.isMainThread) refusal = check(config.display);
        else dispatch_sync(dispatch_get_main_queue(), ^{ refusal = check(config.display); });
        if (refusal) { [self emitFailure:refusal reason:@"error"]; return 0; }
    }
    __block int result = 0;
    dispatch_sync(_control, ^{ result = [self startOnControl:config]; });
    return result;
}

- (NSURL *)recordingsDirectory {
    NSURL *url = [NSURL fileURLWithPath:[NSTemporaryDirectory() stringByAppendingPathComponent:@"MotionClubRecordings"] isDirectory:YES];
    [[NSFileManager defaultManager] createDirectoryAtURL:url withIntermediateDirectories:YES attributes:nil error:nil];
    return url;
}

- (void)deleteOldRecordingsIn:(NSURL *)directory {
    NSArray<NSURL *> *files = [[NSFileManager defaultManager] contentsOfDirectoryAtURL:directory includingPropertiesForKeys:@[NSURLContentModificationDateKey] options:NSDirectoryEnumerationSkipsHiddenFiles error:nil];
    NSDate *cutoff = [NSDate dateWithTimeIntervalSinceNow:-24 * 3600];
    for (NSURL *file in files) {
        NSDate *modified = nil;
        [file getResourceValue:&modified forKey:NSURLContentModificationDateKey error:nil];
        if (modified && [modified compare:cutoff] == NSOrderedAscending) [[NSFileManager defaultManager] removeItemAtURL:file error:nil];
    }
}

- (int)startOnControl:(SessionConfig)config {
    if (_state.load() != kIdle) {
        [self emit:@"warning" message:@"A recording is already running." reason:@"error" extra:nil];
        return 0;
    }
    NSURL *directory = [self recordingsDirectory];
    [self deleteOldRecordingsIn:directory];
    if (FreeBytes(directory) < kMinFreeToStart) { [self emitFailure:@"There is not enough free space to record. Free up at least 1 GB and try again." reason:@"storage"]; return 0; }

    NSDateFormatter *formatter = [NSDateFormatter new];
    formatter.locale = [NSLocale localeWithLocaleIdentifier:@"en_US_POSIX"];
    formatter.dateFormat = @"yyyyMMdd-HHmmss";
    NSString *stamp = [formatter stringFromDate:NSDate.date];
    NSURL *url = [directory URLByAppendingPathComponent:[NSString stringWithFormat:@"MotionClub-%@.mp4", stamp]];
    for (int n = 2; [[NSFileManager defaultManager] fileExistsAtPath:url.path]; n++)
        url = [directory URLByAppendingPathComponent:[NSString stringWithFormat:@"MotionClub-%@-%d.mp4", stamp, n]];

    SportsSessionTake *take = [SportsSessionTake new];
    take.config = config; take.url = url;
    take.hasAudio = config.gameAudio || config.voice || config.mic;
    NSError *error = nil;
    take.writer = [[AVAssetWriter alloc] initWithURL:url fileType:AVFileTypeMPEG4 error:&error];
    if (!take.writer) { [self emitFailure:@"The phone could not start video recording." reason:@"error"]; return 0; }
    NSDictionary *compression = @{
        AVVideoAverageBitRateKey: @((int)(config.mbps * 1e6)),
        AVVideoExpectedSourceFrameRateKey: @(config.fps),
        AVVideoMaxKeyFrameIntervalKey: @(config.fps * 2),
        AVVideoMaxKeyFrameIntervalDurationKey: @2.0,
        AVVideoAllowFrameReorderingKey: @NO,
        AVVideoProfileLevelKey: AVVideoProfileLevelH264HighAutoLevel};
    NSDictionary *settings = @{AVVideoCodecKey: AVVideoCodecTypeH264, AVVideoWidthKey: @(config.width), AVVideoHeightKey: @(config.height), AVVideoCompressionPropertiesKey: compression};
    take.video = [AVAssetWriterInput assetWriterInputWithMediaType:AVMediaTypeVideo outputSettings:settings];
    take.video.expectsMediaDataInRealTime = YES;
    take.pixels = [AVAssetWriterInputPixelBufferAdaptor assetWriterInputPixelBufferAdaptorWithAssetWriterInput:take.video sourcePixelBufferAttributes:
        @{(id)kCVPixelBufferPixelFormatTypeKey: @(kCVPixelFormatType_32BGRA), (id)kCVPixelBufferWidthKey: @(config.width), (id)kCVPixelBufferHeightKey: @(config.height),
          (id)kCVPixelBufferMetalCompatibilityKey: @YES, (id)kCVPixelBufferIOSurfacePropertiesKey: @{}}];
    if (![take.writer canAddInput:take.video]) { [self emitFailure:@"Video recording is not available on this phone." reason:@"error"]; return 0; }
    [take.writer addInput:take.video];
    if (take.hasAudio) {
        take.audio = [AVAssetWriterInput assetWriterInputWithMediaType:AVMediaTypeAudio outputSettings:
            @{AVFormatIDKey: @(kAudioFormatMPEG4AAC), AVSampleRateKey: @48000, AVNumberOfChannelsKey: @2, AVEncoderBitRateKey: @192000}];
        take.audio.expectsMediaDataInRealTime = YES;
        if (![take.writer canAddInput:take.audio]) { [self emitFailure:@"Audio recording is not available on this phone." reason:@"error"]; return 0; }
        [take.writer addInput:take.audio];
        AudioStreamBasicDescription asbd = {};
        asbd.mSampleRate = sports::kMixRate; asbd.mFormatID = kAudioFormatLinearPCM;
        asbd.mFormatFlags = kAudioFormatFlagIsFloat | kAudioFormatFlagIsPacked; asbd.mBytesPerPacket = 8;
        asbd.mFramesPerPacket = 1; asbd.mBytesPerFrame = 8; asbd.mChannelsPerFrame = 2; asbd.mBitsPerChannel = 32;
        CMAudioFormatDescriptionRef format = nullptr;
        if (CMAudioFormatDescriptionCreate(kCFAllocatorDefault, &asbd, 0, nullptr, 0, nullptr, nullptr, &format) != noErr) { [self emitFailure:@"Audio recording is not available on this phone." reason:@"error"]; return 0; }
        take.audioFormat = format;
    }
    take.writer.shouldOptimizeForNetworkUse = NO;
    // Crash recovery: with fragments the file is playable up to the last completed fragment
    // even if the app dies mid-recording. Whether Photos accepts a fragmented MP4 is a device check.
    if (config.fragmentSeconds > 0) take.writer.movieFragmentInterval = CMTimeMakeWithSeconds(config.fragmentSeconds, 600);
    if (![take.writer startWriting]) {
        NSLog(@"[SessionCapture] startWriting failed: %@", take.writer.error);
        [[NSFileManager defaultManager] removeItemAtURL:url error:nil];
        [self emitFailure:@"The phone could not start video recording." reason:@"error"];
        return 0;
    }
    [take.writer startSessionAtSourceTime:kCMTimeZero];

    bool enabled[sports::kSourceCount] = {config.gameAudio, config.voice, config.mic};
    float gain[sports::kSourceCount] = {config.gainGame, config.gainVoice, config.gainMic};
    sports::SourceRing::Config rings[sports::kSourceCount];
    rings[sports::kSourceGame].prefillMs = 50;  rings[sports::kSourceGame].capacityMs = 800;
    rings[sports::kSourceVoice].prefillMs = 100; rings[sports::kSourceVoice].capacityMs = 1000;
    rings[sports::kSourceMic].prefillMs = 100;   rings[sports::kSourceMic].capacityMs = 1000;
    _mixer.configure(enabled, gain, rings);

    double now = NSProcessInfo.processInfo.systemUptime;
    take.startUptime = now;
    take->clock.start(now);
    _lastFrameSeen.store(now); _surfaceOK.store(true);
    _lastDiskCheck = now; _missingSince = 0; _thermalWarned = NO; _dropWarned = NO; _lastDropWarning = 0;
    _display.store(config.display);
    { std::lock_guard<std::mutex> lock(_takeMutex); _take = take; }
    _state.store(kActive);

    if (take.hasAudio) {
        _audioTimer = dispatch_source_create(DISPATCH_SOURCE_TYPE_TIMER, 0, 0, _audioQueue);
        dispatch_source_set_timer(_audioTimer, dispatch_time(DISPATCH_TIME_NOW, 10 * NSEC_PER_MSEC), 10 * NSEC_PER_MSEC, 1 * NSEC_PER_MSEC);
        dispatch_source_set_event_handler(_audioTimer, ^{ [self pumpAudio:take final:NO]; });
        dispatch_resume(_audioTimer);
    }
    _watchdog = dispatch_source_create(DISPATCH_SOURCE_TYPE_TIMER, 0, 0, _control);
    dispatch_source_set_timer(_watchdog, dispatch_time(DISPATCH_TIME_NOW, (int64_t)(kWatchdogInterval * NSEC_PER_SEC)), (uint64_t)(kWatchdogInterval * NSEC_PER_SEC), 50 * NSEC_PER_MSEC);
    dispatch_source_set_event_handler(_watchdog, ^{ [self watchdogTick]; });
    dispatch_resume(_watchdog);

    [self emit:@"started" message:@"Recording the TV." reason:nil extra:nil];
    return 1;
}

- (void)stop { [self requestEnd:@"user" failed:NO message:nil]; }

- (void)willResignActive:(NSNotification *)note { [self requestEnd:@"background" failed:NO message:nil]; }
- (void)thermalChanged:(NSNotification *)note {
    if (_state.load() != kActive) return;
    dispatch_async(_control, ^{ [self watchdogTick]; });
}

/// Any thread. Idempotent; a no-op unless a recording is running.
- (void)requestEnd:(NSString *)reason failed:(BOOL)failed message:(NSString *)message {
    if (_state.load() != kActive) return;
    dispatch_async(_control, ^{ [self beginFinish:reason failed:failed message:message]; });
}

#pragma mark Watchdog (control queue)

- (void)watchdogTick {
    if (_state.load() != kActive) return;
    SportsSessionTake *take;
    { std::lock_guard<std::mutex> lock(_takeMutex); take = _take; }
    if (!take) return;
    double now = NSProcessInfo.processInfo.systemUptime;
    if (take->failed.load()) { [self beginFinish:@"error" failed:YES message:@"The recording hit a problem and stopped."]; return; }
    if (now - take.startUptime >= take.config.maxSeconds) { [self beginFinish:@"limit" failed:NO message:nil]; return; }
    NSProcessInfoThermalState thermal = NSProcessInfo.processInfo.thermalState;
    if (thermal >= NSProcessInfoThermalStateCritical) { [self beginFinish:@"thermal" failed:NO message:nil]; return; }
    if (thermal == NSProcessInfoThermalStateSerious && !_thermalWarned) {
        _thermalWarned = YES;
        [self emit:@"warning" message:@"The phone is getting hot. Recording may stop soon." reason:@"thermal" extra:nil];
    } else if (thermal < NSProcessInfoThermalStateSerious) _thermalWarned = NO;
    if (now - _lastDiskCheck >= kDiskInterval) {
        _lastDiskCheck = now;
        if (FreeBytes([self recordingsDirectory]) < kMinFreeWhileRecording) { [self beginFinish:@"storage" failed:NO message:nil]; return; }
    }
    // The TV surface going away.
    double sinceFrame = now - _lastFrameSeen.load();
    BOOL (^probe)(int) = self.surfaceProbe;
    if (probe) {
        int display = take.config.display;
        dispatch_async(dispatch_get_main_queue(), ^{ self->_surfaceOK.store(probe(display)); });
        if (!_surfaceOK.load()) {
            if (_missingSince == 0) _missingSince = now;
            else if (now - _missingSince > kDisplayMissingSeconds) { [self beginFinish:@"display" failed:NO message:nil]; return; }
        } else _missingSince = 0;
        double limit = take->frames.load() == 0 ? kFirstFrameTimeoutSeconds : kNoFrameBackstopSeconds;
        if (sinceFrame > limit) { [self beginFinish:@"display" failed:NO message:nil]; return; }
    } else if (sinceFrame > kDisplayMissingSeconds) { [self beginFinish:@"display" failed:NO message:nil]; return; }
    // Dropped frames: tell the host, rarely.
    uint64_t dropped = take->dropped.load();
    if (dropped > take->droppedReported && now - _lastDropWarning > 60.0) {
        _lastDropWarning = now; take->droppedReported = dropped;
        [self emit:@"warning" message:@"The phone is struggling to keep up, so some video frames were skipped." reason:@"error" extra:@{@"dropped": @(dropped)}];
    }
}

#pragma mark Finish (control queue)

- (void)beginFinish:(NSString *)reason failed:(BOOL)failed message:(NSString *)message {
    if (_state.load() != kActive) return;
    _state.store(kFinishing);
    if (_watchdog) { dispatch_source_cancel(_watchdog); _watchdog = nil; }
    if (_audioTimer) { dispatch_source_cancel(_audioTimer); _audioTimer = nil; }
    SportsSessionTake *take;
    { std::lock_guard<std::mutex> lock(_takeMutex); take = _take; _take = nil; take->stopping.store(true); }
    _mixer.disableAll();   // stop intake from the real-time threads

    __block UIBackgroundTaskIdentifier background = UIBackgroundTaskInvalid;
    background = [UIApplication.sharedApplication beginBackgroundTaskWithName:@"MotionClubFinishRecording" expirationHandler:^{
        if (background != UIBackgroundTaskInvalid) { [UIApplication.sharedApplication endBackgroundTask:background]; background = UIBackgroundTaskInvalid; }
    }];

    dispatch_group_t all = dispatch_group_create();
    dispatch_group_enter(all);
    dispatch_async(_audioQueue, ^{ [self pumpAudio:take final:YES]; dispatch_group_leave(all); });
    dispatch_group_enter(all);
    dispatch_group_notify(take.work, _encode, ^{ dispatch_group_leave(all); });   // every in-flight GPU frame appended
    dispatch_group_notify(all, _encode, ^{
        NSFileManager *files = [NSFileManager defaultManager];
        AVAssetWriter *writer = take.writer;
        uint64_t frames = take->frames.load();
        uint64_t audioDropped = take->audioDropped, underruns = 0;   // frames lost to ring overrun/contention, pulls that ran dry
        for (int i = 0; i < sports::kSourceCount; i++) {
            sports::SourceStats st = self->_mixer.source((sports::SourceId)i).stats();
            audioDropped += st.droppedOverrun + st.droppedContention; underruns += st.underruns;
        }
        auto done = ^{
            self->_state.store(kIdle);
            if (background != UIBackgroundTaskInvalid) { [UIApplication.sharedApplication endBackgroundTask:background]; background = UIBackgroundTaskInvalid; }
        };
        auto fail = ^(NSString *text, NSString *why) {
            if (writer.status == AVAssetWriterStatusWriting) [writer cancelWriting];
            [files removeItemAtURL:take.url error:nil];
            [self emit:@"failed" message:text reason:why extra:@{@"dropped": @(take->dropped.load())}];
            done();
        };
        if (failed) { fail(message ?: @"The recording could not be saved.", reason); return; }
        if (take->failed.load() || writer.status != AVAssetWriterStatusWriting) {
            NSLog(@"[SessionCapture] writer error: %@", writer.error);
            fail(@"The recording could not be saved because the phone's video encoder stopped.", @"error"); return;
        }
        if (frames < 1) { fail(@"No picture was recorded from the TV.", [reason isEqualToString:@"user"] ? @"error" : reason); return; }
        double videoEnd = take->lastPts.load() + 1.0 / take.config.fps;
        [take.video markAsFinished];
        if (take.audio) [take.audio markAsFinished];
        [writer endSessionAtSourceTime:CMTimeMakeWithSeconds(videoEnd, 90000)];
        [writer finishWritingWithCompletionHandler:^{
            if (writer.status != AVAssetWriterStatusCompleted) {
                NSLog(@"[SessionCapture] finish failed: %@", writer.error);
                fail(@"The recording could not be saved.", @"error"); return;
            }
            NSDictionary *attrs = [files attributesOfItemAtPath:take.url.path error:nil];
            [self emit:@"finished" message:MessageForReason(reason) reason:reason extra:@{
                @"url": take.url.path, @"seconds": @(std::round(videoEnd * 10) / 10), @"bytes": attrs[NSFileSize] ?: @0, @"dropped": @(take->dropped.load()),
                @"audioDropped": @(audioDropped), @"audioUnderruns": @(underruns)}];
            done();
        }];
    });
}

#pragma mark Video (Unity render thread)

- (BOOL)prepareMetal:(id<MTLDevice>)device {
    if (_pipeline && _textureCache && _cacheDevice == device) return YES;
    if (_textureCache) { CFRelease(_textureCache); _textureCache = nil; }
    _pipeline = nil;
    NSString *shader = @"#include <metal_stdlib>\nusing namespace metal; struct V { float4 p [[position]]; float2 uv; }; vertex V vtx(uint n [[vertex_id]]) { float2 p=float2((n<<1)&2,n&2); V o; o.p=float4(p*2-1,0,1); o.uv=float2(p.x,1-p.y); return o; } fragment float4 frag(V i [[stage_in]],texture2d<float> t [[texture(0)]],constant float &gamma [[buffer(0)]]) { constexpr sampler s(filter::linear,address::clamp_to_edge); float3 c=t.sample(s,i.uv).rgb; if(gamma>0.5) c=select(c*12.92,1.055*pow(max(c,0.0),float3(1.0/2.4))-0.055,c>0.0031308); return float4(c,1); }";
    NSError *error = nil;
    id<MTLLibrary> library = [device newLibraryWithSource:shader options:nil error:&error];
    if (!library) return NO;
    MTLRenderPipelineDescriptor *desc = [MTLRenderPipelineDescriptor new];
    desc.vertexFunction = [library newFunctionWithName:@"vtx"]; desc.fragmentFunction = [library newFunctionWithName:@"frag"];
    desc.colorAttachments[0].pixelFormat = MTLPixelFormatBGRA8Unorm;
    _pipeline = [device newRenderPipelineStateWithDescriptor:desc error:&error];
    if (!_pipeline) return NO;
    CVMetalTextureCacheRef cache = nil;
    if (CVMetalTextureCacheCreate(kCFAllocatorDefault, nil, device, nil, &cache) != kCVReturnSuccess) { _pipeline = nil; return NO; }
    _textureCache = cache; _cacheDevice = device;
    return YES;
}

- (void)captureTexture:(id<MTLTexture>)source commandBuffer:(id<MTLCommandBuffer>)command timestamp:(double)now {
    if (_state.load() != kActive || !command) return;
    SportsSessionTake *take = nil;
    {   // Cheap gate. Taking a work-group slot under the lock is what lets finalization wait for exactly the frames that were accepted.
        std::lock_guard<std::mutex> lock(_takeMutex);
        take = _take;
        if (!take || take->stopping.load() || take->failed.load()) return;
        _lastFrameSeen.store(now);
        const SessionConfig &config = take.config;
        if (take->lastEncode > 0 && now - take->lastEncode < 1.0 / config.fps - 0.002) return;
        if (take->pending.load() >= kMaxPendingFrames) { take->dropped++; return; }
        take->pending++; dispatch_group_enter(take.work);
    }
    auto leave = ^{ take->pending--; dispatch_group_leave(take.work); };
    const SessionConfig config = take.config;
    if (take->frames.load() % 60 == 59 && _textureCache) CVMetalTextureCacheFlush(_textureCache, 0);   // bounded cache over hour-long sessions
    if (!source || source.framebufferOnly || source.width < 320 || source.height < 180) {
        leave(); [self requestEnd:@"error" failed:YES message:@"This TV picture cannot be recorded."]; return;
    }
    MTLPixelFormat format = source.pixelFormat;
    if (format != MTLPixelFormatBGRA8Unorm && format != MTLPixelFormatBGRA8Unorm_sRGB && format != MTLPixelFormatRGBA8Unorm && format != MTLPixelFormatRGBA8Unorm_sRGB) {
        leave(); [self requestEnd:@"error" failed:YES message:@"This TV picture format cannot be recorded."]; return;
    }
    if (![self prepareMetal:source.device]) { leave(); [self requestEnd:@"error" failed:YES message:@"The phone could not start video capture."]; return; }
    CVPixelBufferRef pixel = nil; CVMetalTextureRef mapped = nil;
    CVPixelBufferPoolRef pool = take.pixels.pixelBufferPool;
    if (!pool || CVPixelBufferPoolCreatePixelBuffer(kCFAllocatorDefault, pool, &pixel) != kCVReturnSuccess) { take->dropped++; leave(); return; }
    if (CVMetalTextureCacheCreateTextureFromImage(kCFAllocatorDefault, _textureCache, pixel, nil, MTLPixelFormatBGRA8Unorm, config.width, config.height, 0, &mapped) != kCVReturnSuccess) {
        CVPixelBufferRelease(pixel); take->dropped++; leave(); return;
    }
    command.label = @"Motion Club session capture";
    MTLRenderPassDescriptor *pass = [MTLRenderPassDescriptor renderPassDescriptor];
    pass.colorAttachments[0].texture = CVMetalTextureGetTexture(mapped);
    pass.colorAttachments[0].loadAction = MTLLoadActionClear; pass.colorAttachments[0].storeAction = MTLStoreActionStore;
    pass.colorAttachments[0].clearColor = MTLClearColorMake(0, 0, 0, 1);
    id<MTLRenderCommandEncoder> encoder = [command renderCommandEncoderWithDescriptor:pass];
    float scale = fmin((float)config.width / source.width, (float)config.height / source.height);
    double w = source.width * scale, h = source.height * scale;
    [encoder setViewport:(MTLViewport){(config.width - w) * .5, (config.height - h) * .5, w, h, 0, 1}];
    float gamma = (format == MTLPixelFormatBGRA8Unorm_sRGB || format == MTLPixelFormatRGBA8Unorm_sRGB) ? 1 : 0;
    [encoder setRenderPipelineState:_pipeline]; [encoder setFragmentTexture:source atIndex:0];
    [encoder setFragmentBytes:&gamma length:sizeof(gamma) atIndex:0];
    [encoder drawPrimitives:MTLPrimitiveTypeTriangle vertexStart:0 vertexCount:3]; [encoder endEncoding];

    take->lastEncode = now;
    double first = take->firstTime.load();
    if (first == 0) { first = now; take->firstTime.store(now); }   // audio aligns itself to this instant
    double pts = now - first;
    [command addCompletedHandler:^(id<MTLCommandBuffer> completed) {
        dispatch_async(self->_encode, ^{
            if (!take->failed.load()) {
                if (completed.status != MTLCommandBufferStatusCompleted || !take.video.readyForMoreMediaData) take->dropped++;
                else if (![take.pixels appendPixelBuffer:pixel withPresentationTime:CMTimeMakeWithSeconds(pts, 90000)]) {
                    if (take.writer.status == AVAssetWriterStatusFailed) {
                        NSLog(@"[SessionCapture] video append failed: %@", take.writer.error);
                        take->failed.store(true);
                        [self requestEnd:@"error" failed:YES message:@"The recording stopped because the phone's video encoder had a problem."];
                    } else take->dropped++;
                } else { take->frames++; take->lastPts.store(pts); }
            }
            CFRelease(mapped); CVPixelBufferRelease(pixel);
            take->pending--; dispatch_group_leave(take.work);
        });
    }];
}

#pragma mark Audio

- (void)captureGameAudio:(const float *)samples count:(int)count channels:(int)channels rate:(int)rate {
    if (!samples || channels < 1 || count < channels) return;
    _mixer.push(sports::kSourceGame, samples, count / channels, channels, rate);
}

- (void)captureVoice:(const float *)mono frames:(int)frames rate:(int)rate source:(int)source {
    if (!mono || frames <= 0) return;
    _mixer.push(source == 1 ? sports::kSourceMic : sports::kSourceVoice, mono, frames, 1, rate);
}

static CMSampleBufferRef MakeAudioSample(CMAudioFormatDescriptionRef format, const float *interleaved, int frames, int64_t ptsFrames) {
    size_t length = (size_t)frames * 8;
    CMBlockBufferRef block = nullptr; CMSampleBufferRef sample = nullptr;
    OSStatus error = CMBlockBufferCreateWithMemoryBlock(kCFAllocatorDefault, nullptr, length, kCFAllocatorDefault, nullptr, 0, length, 0, &block);
    if (!error) error = CMBlockBufferReplaceDataBytes(interleaved, block, 0, length);
    CMSampleTimingInfo timing = {CMTimeMake(1, sports::kMixRate), CMTimeMake(ptsFrames, sports::kMixRate), kCMTimeInvalid};
    size_t bytes = 8;
    if (!error) error = CMSampleBufferCreateReady(kCFAllocatorDefault, block, format, frames, 1, &timing, 1, &bytes, &sample);
    if (block) CFRelease(block);
    return error ? nullptr : sample;
}

/// Audio queue. The audio timeline is driven by the wall clock (PumpClock), never by Unity's
/// audio callbacks, so pausing Unity's audio only produces silence.
- (void)pumpAudio:(SportsSessionTake *)take final:(BOOL)final {
    if (!take.hasAudio) return;
    double now = NSProcessInfo.processInfo.systemUptime, blockStart = 0;
    float block[sports::kBlockFrames * 2];
    int kept = 0;
    double firstKeptStart = 0;
    while (take->clock.next(now, &blockStart)) {
        _mixer.pull(block, sports::kBlockFrames);
        double first = take->firstTime.load();
        if (first <= 0 || blockStart + sports::kBlockSeconds <= first) continue;    // before the first picture
        if (!take->apts.started()) take->apts.start(sports::audioStartOffset(first, blockStart));
        if (kept == 0) firstKeptStart = blockStart;
        take->tick.insert(take->tick.end(), block, block + sports::kBlockFrames * 2);
        kept++;
    }
    if (kept > 0) {
        int frames = kept * sports::kBlockFrames;
        // After a long stall the pump clock skips ahead; keep the PTS on the wall clock (a gap, never a shift).
        int64_t expected = (int64_t)std::llround(sports::audioStartOffset(take->firstTime.load(), firstKeptStart) * sports::kMixRate);
        if (std::llabs(expected - take->apts.nextFrames()) > 3 * sports::kBlockFrames) take->apts.seek(expected);
        CMSampleBufferRef sample = MakeAudioSample(take.audioFormat, take->tick.data(), frames, take->apts.nextFrames());
        take->apts.advance(frames);   // advance even on failure: a lost block leaves a gap, it never shifts later audio
        take->tick.clear();
        if (sample) take->queued.push_back(sample); else take->audioDropped++;
    }
    while (take->queued.size() > kMaxQueuedAudio) { CFRelease(take->queued.front()); take->queued.pop_front(); take->audioDropped++; }
    while (!take->queued.empty() && !take->failed.load()) {
        if (!take.audio.readyForMoreMediaData) break;
        CMSampleBufferRef sample = take->queued.front();
        if (![take.audio appendSampleBuffer:sample]) {
            if (take.writer.status == AVAssetWriterStatusFailed) {
                NSLog(@"[SessionCapture] audio append failed: %@", take.writer.error);
                take->failed.store(true);
                [self requestEnd:@"error" failed:YES message:@"The recording stopped because the phone's audio encoder had a problem."];
            } else take->audioDropped++;
        }
        CFRelease(sample); take->queued.pop_front();
    }
    if (final) {
        for (CMSampleBufferRef sample : take->queued) CFRelease(sample);
        take->queued.clear();
    }
}
@end
