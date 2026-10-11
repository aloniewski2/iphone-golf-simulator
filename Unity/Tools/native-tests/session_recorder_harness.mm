// macOS harness for the REAL SportsSessionRecorder.mm: synthetic Metal frames + synthetic audio
// are pushed at real-time pace, the produced MP4 is read back and checked (tracks, duration,
// tones, A/V alignment, lifecycle events). It does NOT cover the Unity render hook, the TV
// display, or anything iOS specific (see README.md).
#import <AVFoundation/AVFoundation.h>
#import <Metal/Metal.h>
#import <UIKit/UIKit.h>
#include <cmath>
#include <mach/mach.h>
#include <vector>
#import "SportsSessionRecorder.h"

NSString *const UIApplicationWillResignActiveNotification = @"UIApplicationWillResignActiveNotification";
@implementation UIApplication
+ (UIApplication *)sharedApplication { static UIApplication *a; static dispatch_once_t o; dispatch_once(&o, ^{ a = [UIApplication new]; }); return a; }
- (UIBackgroundTaskIdentifier)beginBackgroundTaskWithName:(NSString *)n expirationHandler:(void (^)(void))h { return 1; }
- (void)endBackgroundTask:(UIBackgroundTaskIdentifier)i {}
@end

static int gChecks = 0, gFailed = 0;
#define CHECK(cond, ...) do { gChecks++; if (!(cond)) { gFailed++; printf("    CHECK FAILED line %d: %s  ", __LINE__, #cond); printf("" __VA_ARGS__); printf("\n"); } } while (0)

static NSMutableArray<NSDictionary *> *gEvents;
static void OnEvent(const char *json) {
    NSDictionary *e = [NSJSONSerialization JSONObjectWithData:[NSData dataWithBytes:json length:strlen(json)] options:0 error:nil];
    @synchronized(gEvents) { [gEvents addObject:e]; }
}
static NSDictionary *WaitFor(NSString *state, double timeout) {
    NSDate *end = [NSDate dateWithTimeIntervalSinceNow:timeout];
    while ([end timeIntervalSinceNow] > 0) {
        @synchronized(gEvents) { for (NSDictionary *e in gEvents) if ([e[@"state"] isEqualToString:state]) return e; }
        [[NSRunLoop currentRunLoop] runUntilDate:[NSDate dateWithTimeIntervalSinceNow:0.02]];
    }
    return nil;
}
static NSDictionary *Last(NSString *state) {
    @synchronized(gEvents) { for (NSDictionary *e in gEvents.reverseObjectEnumerator) if ([e[@"state"] isEqualToString:state]) return e; }
    return nil;
}
static void Reset() { @synchronized(gEvents) { [gEvents removeAllObjects]; } }

static double Now() { return NSProcessInfo.processInfo.systemUptime; }
static void Sleep(double s) { [[NSRunLoop currentRunLoop] runUntilDate:[NSDate dateWithTimeIntervalSinceNow:s]]; }

struct Source { double freq, amp; int rate; double phase = 0; };
static std::vector<float> Tone(Source &s, int frames, int channels) {
    std::vector<float> v((size_t)frames * channels);
    for (int i = 0; i < frames; i++) { float x = (float)(s.amp * std::sin(s.phase)); s.phase += 2 * M_PI * s.freq / s.rate; for (int c = 0; c < channels; c++) v[(size_t)i * channels + c] = x; }
    return v;
}

// Goertzel magnitude of `freq` in mono float samples (RMS-normalized amplitude estimate).
static double ToneAmp(const std::vector<float> &x, int rate, double freq) {
    double w = 2 * M_PI * freq / rate, c = 2 * std::cos(w), s1 = 0, s2 = 0;
    for (float v : x) { double s0 = v + c * s1 - s2; s2 = s1; s1 = s0; }
    double re = s1 - s2 * std::cos(w), im = s2 * std::sin(w);
    return 2 * std::sqrt(re * re + im * im) / x.size();
}

struct Decoded { double videoSeconds = 0, audioSeconds = 0; int width = 0, height = 0; int videoFrames = 0; bool hasAudio = false; std::vector<float> audioL; std::vector<double> frameTimes, frameLuma; double fpsNominal = 0; };
static Decoded Decode(NSString *path) {
    Decoded d;
    AVURLAsset *asset = [AVURLAsset URLAssetWithURL:[NSURL fileURLWithPath:path] options:nil];
    AVAssetTrack *vt = [asset tracksWithMediaType:AVMediaTypeVideo].firstObject, *at = [asset tracksWithMediaType:AVMediaTypeAudio].firstObject;
    if (vt) { d.width = (int)vt.naturalSize.width; d.height = (int)vt.naturalSize.height; d.videoSeconds = CMTimeGetSeconds(vt.timeRange.duration); }
    if (at) { d.hasAudio = true; d.audioSeconds = CMTimeGetSeconds(at.timeRange.duration); }
    NSError *err = nil;
    if (vt) {
        AVAssetReader *r = [AVAssetReader assetReaderWithAsset:asset error:&err];
        AVAssetReaderTrackOutput *o = [AVAssetReaderTrackOutput assetReaderTrackOutputWithTrack:vt outputSettings:@{(id)kCVPixelBufferPixelFormatTypeKey: @(kCVPixelFormatType_32BGRA)}];
        [r addOutput:o]; [r startReading];
        while (CMSampleBufferRef sb = [o copyNextSampleBuffer]) {
            d.videoFrames++; d.frameTimes.push_back(CMTimeGetSeconds(CMSampleBufferGetPresentationTimeStamp(sb)));
            CVPixelBufferRef pb = CMSampleBufferGetImageBuffer(sb); CVPixelBufferLockBaseAddress(pb, kCVPixelBufferLock_ReadOnly);
            const uint8_t *base = (const uint8_t *)CVPixelBufferGetBaseAddress(pb); size_t bpr = CVPixelBufferGetBytesPerRow(pb);
            double sum = 0; int n = 0; // sample the centre region (letterbox bars are black)
            size_t w = CVPixelBufferGetWidth(pb), h = CVPixelBufferGetHeight(pb);
            for (size_t y = h / 2 - 20; y < h / 2 + 20; y += 4) for (size_t x = w / 2 - 20; x < w / 2 + 20; x += 4) { sum += base[y * bpr + x * 4 + 1]; n++; }
            d.frameLuma.push_back(sum / n);
            CVPixelBufferUnlockBaseAddress(pb, kCVPixelBufferLock_ReadOnly); CFRelease(sb);
        }
    }
    if (at) {
        AVAssetReader *r = [AVAssetReader assetReaderWithAsset:asset error:&err];
        AVAssetReaderTrackOutput *o = [AVAssetReaderTrackOutput assetReaderTrackOutputWithTrack:at outputSettings:@{AVFormatIDKey: @(kAudioFormatLinearPCM), AVLinearPCMBitDepthKey: @32, AVLinearPCMIsFloatKey: @YES, AVLinearPCMIsNonInterleaved: @NO, AVLinearPCMIsBigEndianKey: @NO, AVSampleRateKey: @48000, AVNumberOfChannelsKey: @2}];
        [r addOutput:o]; [r startReading];
        // Place samples by PTS so a gap would show up as silence.
        while (CMSampleBufferRef sb = [o copyNextSampleBuffer]) {
            CMBlockBufferRef bb = CMSampleBufferGetDataBuffer(sb); size_t len = 0; char *ptr = nullptr;
            CMBlockBufferGetDataPointer(bb, 0, nullptr, &len, &ptr);
            size_t at0 = (size_t)std::llround(CMTimeGetSeconds(CMSampleBufferGetPresentationTimeStamp(sb)) * 48000);
            size_t n = len / 8; if (d.audioL.size() < at0 + n) d.audioL.resize(at0 + n, 0.0f);
            const float *f = (const float *)ptr; for (size_t i = 0; i < n; i++) d.audioL[at0 + i] = f[i * 2];
            CFRelease(sb);
        }
    }
    return d;
}

// Drives frames+audio at real time for `seconds`. Frame colour = luma (grey ramp), white flash + audio click at flashAt.
struct SyncMark { double at; double flashWall = 0, clickWall = 0; bool flashed = false, clicked = false; };
struct DriveOptions { double seconds = 6; double fps = 60; std::vector<SyncMark> markers; bool game = true, voice = true, mic = true; bool skipFrames = false; };
static void Drive(id<MTLDevice> device, id<MTLCommandQueue> queue, id<MTLTexture> tex, SportsSessionRecorder *rec, DriveOptions &o) {
    Source game{440, 0.3, 48000}, voice{1500, 0.2, 44100}, mic{700, 0.2, 16000};
    double start = Now(), nextFrame = start; double lastGame = start, lastVoice = start, lastMic = start;
    int frameNo = 0;
    std::vector<uint32_t> pix((size_t)tex.width * tex.height);
    while (Now() - start < o.seconds) {
        double now = Now();
        // audio producers: 21.3 ms game chunks, 20 ms voice, 85 ms mic bursts, like the real sources
        while (o.game && now - lastGame >= 1024.0 / 48000) { auto v = Tone(game, 1024, 2);
            for (SyncMark &m : o.markers) if (!m.clicked && now - start >= m.at) { // click: loud 3 kHz burst on top of the chunk
                m.clicked = true; m.clickWall = now - start;
                for (int i = 0; i < 1024; i++) { float c = 0.6f * std::sin(2 * M_PI * 3000 * i / 48000.0); v[i * 2] += c; v[i * 2 + 1] += c; } }
            [rec captureGameAudio:v.data() count:2048 channels:2 rate:48000]; lastGame += 1024.0 / 48000; }
        while (o.voice && now - lastVoice >= 0.02) { auto v = Tone(voice, 882, 1); [rec captureVoice:v.data() frames:882 rate:44100 source:0]; lastVoice += 0.02; }
        while (o.mic && now - lastMic >= 0.085) { auto v = Tone(mic, 1360, 1); [rec captureVoice:v.data() frames:1360 rate:16000 source:1]; lastMic += 0.085; }
        if (!o.skipFrames && now >= nextFrame) {
            bool flash = false;
            for (SyncMark &m : o.markers) if (!m.flashed && now - start >= m.at) { m.flashed = true; m.flashWall = now - start; flash = true; }
            uint32_t grey = flash ? 255 : (uint32_t)(40 + (frameNo % 60));
            uint32_t px = 0xFF000000u | grey << 16 | grey << 8 | grey;
            std::fill(pix.begin(), pix.end(), px);
            [tex replaceRegion:MTLRegionMake2D(0, 0, tex.width, tex.height) mipmapLevel:0 withBytes:pix.data() bytesPerRow:tex.width * 4];
            id<MTLCommandBuffer> cb = [queue commandBuffer];
            [rec captureTexture:tex commandBuffer:cb timestamp:now];
            [cb commit];
            frameNo++; nextFrame += 1.0 / o.fps; if (nextFrame < now - 0.1) nextFrame = now;
        }
        Sleep(0.002);
    }
}

int main() {
    @autoreleasepool {
        gEvents = [NSMutableArray new];
        id<MTLDevice> device = MTLCreateSystemDefaultDevice();
        if (!device) { printf("no Metal device; cannot run\n"); return 2; }
        id<MTLCommandQueue> queue = [device newCommandQueue];
        MTLTextureDescriptor *td = [MTLTextureDescriptor texture2DDescriptorWithPixelFormat:MTLPixelFormatBGRA8Unorm width:1280 height:720 mipmapped:NO];
        td.usage = MTLTextureUsageShaderRead; td.storageMode = MTLStorageModeShared;
        id<MTLTexture> tex = [device newTextureWithDescriptor:td];
        SportsSessionRecorder *rec = SportsSessionRecorder.shared;
        [rec setEventCallback:OnEvent];
        // Same wiring SportsSessionCapture.mm does in the app (here: the display is "available").
        rec.displayCheck = ^NSString *(int d) { return nil; };

        if (const char *soak = getenv("SOAK_SECONDS")) {   // optional: SOAK_SECONDS=120 ./run_session_harness.sh
            auto rssMB = [] { mach_task_basic_info i; mach_msg_type_number_t n = MACH_TASK_BASIC_INFO_COUNT; task_info(mach_task_self(), MACH_TASK_BASIC_INFO, (task_info_t)&i, &n); return i.resident_size / 1048576.0; };
            CHECK([rec startWithConfigJSON:"{\"width\":1280,\"height\":720,\"display\":0}"] == 1);
            DriveOptions so; so.seconds = 20; Drive(device, queue, tex, rec, so);   // warm up
            double r0 = rssMB(); double total = atof(soak);
            for (double t = 0; t < total; t += 20) { so = DriveOptions(); so.seconds = 20; Drive(device, queue, tex, rec, so); printf("    t=%.0f s  RSS %.1f MB\n", t + 20, rssMB()); }
            double r1 = rssMB();
            [rec stop]; NSDictionary *f = WaitFor(@"finished", 30);
            printf("    soak: RSS %.1f -> %.1f MB over %.0f s; finished %s bytes %lld dropped %lld\n", r0, r1, total, f ? "yes" : "NO", [f[@"bytes"] longLongValue], [f[@"dropped"] longLongValue]);
            CHECK(f != nil); CHECK(r1 - r0 < 40.0, "RSS grew %.1f MB", r1 - r0);
            if (f) [[NSFileManager defaultManager] removeItemAtPath:f[@"url"] error:nil];
            printf("\n%d checks, %d failed\n", gChecks, gFailed); return gFailed ? 1 : 0;
        }
        printf("-- idle behaviour\n");
        [rec stop]; Sleep(0.1);
        CHECK(!rec.active); CHECK(Last(@"finished") == nil && Last(@"failed") == nil);
        CHECK([rec startWithConfigJSON:"{\"fps\":45}"] == 0); CHECK(WaitFor(@"failed", 1) != nil); Reset();
        CHECK([rec startWithConfigJSON:"{\"width\":1921}"] == 0); CHECK(WaitFor(@"failed", 1) != nil); Reset();
        CHECK([rec startWithConfigJSON:"not json"] == 0); CHECK(WaitFor(@"failed", 1) != nil); Reset();

        printf("-- full session: 8 s, 3 audio sources, A/V flash+click at 3.0 s\n");
        CHECK([rec startWithConfigJSON:"{\"width\":1280,\"height\":720,\"fps\":60,\"bitrateMbps\":8,\"display\":0}"] == 1);
        CHECK(rec.active); CHECK(WaitFor(@"started", 1) != nil);
        CHECK([rec startWithConfigJSON:"{}"] == 0); CHECK(WaitFor(@"warning", 1) != nil);   // second start while active
        DriveOptions o; o.seconds = 8; o.markers = {{1.5}, {4.5}, {7.0}};
        Drive(device, queue, tex, rec, o);
        [rec stop];
        NSDictionary *fin = WaitFor(@"finished", 15);
        CHECK(fin != nil); CHECK([fin[@"reason"] isEqualToString:@"user"]); CHECK(!rec.active);
        if (fin) {
            NSString *path = fin[@"url"]; printf("    file: %s  %.1f s  %lld bytes  dropped %lld\n", path.UTF8String, [fin[@"seconds"] doubleValue], [fin[@"bytes"] longLongValue], [fin[@"dropped"] longLongValue]);
            CHECK([path containsString:@"MotionClubRecordings/MotionClub-"]);
            Decoded d = Decode(path);
            CHECK(d.width == 1280 && d.height == 720, "size %dx%d", d.width, d.height);
            CHECK(std::fabs(d.videoSeconds - 8.0) < 0.35, "video %.3f s", d.videoSeconds);
            CHECK(d.hasAudio); CHECK(std::fabs(d.audioSeconds - d.videoSeconds) < 0.25, "audio %.3f vs video %.3f", d.audioSeconds, d.videoSeconds);
            CHECK(d.videoFrames > 8 * 50, "frames %d", d.videoFrames);
            // monotonic increasing PTS
            bool mono = true; for (size_t i = 1; i < d.frameTimes.size(); i++) if (d.frameTimes[i] <= d.frameTimes[i - 1]) mono = false; CHECK(mono);
            // tones present in the mix (steady part 1 s .. 2.5 s)
            std::vector<float> seg(d.audioL.begin() + 48000, d.audioL.begin() + 48000 + 72000);
            double a440 = ToneAmp(seg, 48000, 440), a1500 = ToneAmp(seg, 48000, 1500), a700 = ToneAmp(seg, 48000, 700), aOff = ToneAmp(seg, 48000, 2200);
            { size_t on = 0; while (on < d.audioL.size() && std::fabs(d.audioL[on]) < 0.01f) on++; printf("    first audible audio sample at %.1f ms; first video frame pts %.1f ms\n", on / 48.0, d.frameTimes.empty() ? -1.0 : d.frameTimes[0] * 1000); }
            printf("    tone amplitudes: game(440) %.3f voice(1500) %.3f mic(700) %.3f off-tone(2200) %.4f\n", a440, a1500, a700, aOff);
            CHECK(a440 > 0.25 && a440 < 0.35); CHECK(a1500 > 0.16 && a1500 < 0.24); CHECK(a700 > 0.16 && a700 < 0.24); CHECK(aOff < 0.02);
            // A/V alignment at three markers (start, middle, end): the flash frame vs the click in the file.
            std::vector<double> flashes;   // distinct bright frames
            for (size_t i = 0; i < d.frameLuma.size(); i++) if (d.frameLuma[i] > 200) flashes.push_back(d.frameTimes[i]);
            CHECK(flashes.size() == 3, "flash frames %zu", flashes.size());
            for (size_t k = 0; k < o.markers.size() && k < flashes.size(); k++) {
                double from = flashes[k] - 0.2; double audioClick = -1;
                for (size_t s = (size_t)(from * 48000); s + 240 < d.audioL.size(); s += 120) { std::vector<float> w(d.audioL.begin() + s, d.audioL.begin() + s + 240); if (ToneAmp(w, 48000, 3000) > 0.2) { audioClick = (double)s / 48000; break; } }
                double injected = (o.markers[k].clickWall - o.markers[k].flashWall) * 1000;
                double lagMs = (audioClick - flashes[k]) * 1000 - injected;
                printf("    marker %zu: flash frame %.3f s (wall %.3f), click in audio %.3f s, injected click-flash %.1f ms -> pipeline lag %+.1f ms\n", k, flashes[k], o.markers[k].flashWall, audioClick, injected, lagMs);
                CHECK(audioClick > 0);
                CHECK(std::fabs(flashes[k] - o.markers[k].flashWall) < 0.08, "flash at %.3f vs wall %.3f", flashes[k], o.markers[k].flashWall);
                // prefill (50 ms) + one source chunk (21 ms) + AAC; must stay inside perceptual limits
                CHECK(lagMs > -30 && lagMs < 130, "lag %.1f ms", lagMs);
            }
            // no gap: the whole timeline is covered by audio data
            size_t quiet = 0; for (size_t i = 48000; i + 480 < d.audioL.size() - 48000; i += 480) { double e = 0; for (int k = 0; k < 480; k++) e += std::fabs(d.audioL[i + k]); if (e == 0) quiet++; }
            CHECK(quiet == 0, "silent 10ms blocks in the middle: %zu", quiet);
            if (getenv("KEEP_FIRST")) printf("    kept: %s\n", path.UTF8String); else [[NSFileManager defaultManager] removeItemAtPath:path error:nil];
        }

        printf("-- sources disabled by config: no voice/mic tones\n");
        Reset();
        CHECK([rec startWithConfigJSON:"{\"width\":640,\"height\":360,\"fps\":30,\"display\":0,\"voice\":false,\"mic\":false}"] == 1);
        o = DriveOptions(); o.seconds = 3; o.fps = 30;
        Drive(device, queue, tex, rec, o); [rec stop];
        fin = WaitFor(@"finished", 15); CHECK(fin != nil);
        if (fin) { Decoded d = Decode(fin[@"url"]);
            std::vector<float> seg(d.audioL.begin() + 24000, d.audioL.begin() + 24000 + 48000);
            double a440 = ToneAmp(seg, 48000, 440), a1500 = ToneAmp(seg, 48000, 1500), a700 = ToneAmp(seg, 48000, 700);
            printf("    440 %.3f  1500 %.4f  700 %.4f; frames %d for 3 s at 30 fps cap\n", a440, a1500, a700, d.videoFrames);
            CHECK(a440 > 0.25); CHECK(a1500 < 0.01); CHECK(a700 < 0.01);
            CHECK(d.videoFrames > 60 && d.videoFrames < 100, "frames %d", d.videoFrames);   // 60 fps source capped to 30
            CHECK(d.width == 640 && d.height == 360);
            [[NSFileManager defaultManager] removeItemAtPath:fin[@"url"] error:nil]; }

        printf("-- no audio sources at all: video-only file\n");
        Reset();
        CHECK([rec startWithConfigJSON:"{\"width\":640,\"height\":360,\"display\":0,\"gameAudio\":false,\"voice\":false,\"mic\":false}"] == 1);
        o = DriveOptions(); o.seconds = 2; Drive(device, queue, tex, rec, o); [rec stop];
        fin = WaitFor(@"finished", 15); CHECK(fin != nil);
        if (fin) { Decoded d = Decode(fin[@"url"]); CHECK(!d.hasAudio); CHECK(d.videoFrames > 60); [[NSFileManager defaultManager] removeItemAtPath:fin[@"url"] error:nil]; }

        printf("-- crash recovery: snapshot the file while it is still being written (movie fragments)\n");
        Reset();
        CHECK([rec startWithConfigJSON:"{\"width\":640,\"height\":360,\"display\":0,\"fragmentSeconds\":2}"] == 1);
        o = DriveOptions(); o.seconds = 6.5; Drive(device, queue, tex, rec, o);
        {   // the recording is still running here: copy what is on disk right now, as a crash would leave it
            NSString *dir = [NSTemporaryDirectory() stringByAppendingPathComponent:@"MotionClubRecordings"];
            NSString *newest = nil; NSDate *newestDate = nil;
            for (NSString *f in [[NSFileManager defaultManager] contentsOfDirectoryAtPath:dir error:nil]) {
                NSDate *m = [[[NSFileManager defaultManager] attributesOfItemAtPath:[dir stringByAppendingPathComponent:f] error:nil] fileModificationDate];
                if (!newestDate || [m compare:newestDate] == NSOrderedDescending) { newest = f; newestDate = m; } }
            NSString *snap = [NSTemporaryDirectory() stringByAppendingPathComponent:@"crash-snapshot.mp4"];
            [[NSFileManager defaultManager] removeItemAtPath:snap error:nil];
            [[NSFileManager defaultManager] copyItemAtPath:[dir stringByAppendingPathComponent:newest] toPath:snap error:nil];
            Decoded c = Decode(snap);
            printf("    snapshot of the in-progress file: %.2f s video, %d frames readable, audio %s\n", c.videoSeconds, c.videoFrames, c.hasAudio ? "yes" : "no");
            CHECK(c.videoSeconds > 3.0 && c.videoFrames > 100, "recoverable video %.2f s / %d frames", c.videoSeconds, c.videoFrames);
            [[NSFileManager defaultManager] removeItemAtPath:snap error:nil];
        }
        [rec stop]; fin = WaitFor(@"finished", 15); CHECK(fin != nil);
        if (fin && getenv("KEEP_FIRST")) printf("    kept fragmented: %s\n", [fin[@"url"] UTF8String]);
        else if (fin) [[NSFileManager defaultManager] removeItemAtPath:fin[@"url"] error:nil];

        printf("-- auto-finish: elapsed limit (maxMinutes 0.05 = 3 s)\n");
        Reset();
        CHECK([rec startWithConfigJSON:"{\"width\":640,\"height\":360,\"display\":0,\"maxMinutes\":0.05}"] == 1);
        o = DriveOptions(); o.seconds = 5; Drive(device, queue, tex, rec, o);
        fin = WaitFor(@"finished", 15);
        CHECK(fin != nil && [fin[@"reason"] isEqualToString:@"limit"]); CHECK(!rec.active);
        if (fin) { Decoded d = Decode(fin[@"url"]); CHECK(d.videoSeconds > 2.5 && d.videoSeconds < 4.0, "len %.2f", d.videoSeconds); [[NSFileManager defaultManager] removeItemAtPath:fin[@"url"] error:nil]; }
        [rec stop]; // idle stop after auto-finish is a no-op
        Sleep(0.3);

        printf("-- auto-finish: app resigns active\n");
        Reset();
        CHECK([rec startWithConfigJSON:"{\"width\":640,\"height\":360,\"display\":0}"] == 1);
        o = DriveOptions(); o.seconds = 2; Drive(device, queue, tex, rec, o);
        [[NSNotificationCenter defaultCenter] postNotificationName:UIApplicationWillResignActiveNotification object:nil];
        fin = WaitFor(@"finished", 15); CHECK(fin != nil && [fin[@"reason"] isEqualToString:@"background"]);
        if (fin) [[NSFileManager defaultManager] removeItemAtPath:fin[@"url"] error:nil];

        printf("-- auto-finish: no TV frames for 2 s -> nothing usable -> failed(display), file deleted\n");
        Reset();
        CHECK([rec startWithConfigJSON:"{\"width\":640,\"height\":360,\"display\":0}"] == 1);
        o = DriveOptions(); o.seconds = 3.5; o.skipFrames = true; Drive(device, queue, tex, rec, o);
        NSDictionary *failed = WaitFor(@"failed", 5);
        CHECK(failed != nil && [failed[@"reason"] isEqualToString:@"display"], "%s", failed.description.UTF8String); CHECK(!rec.active);

        printf("-- display refused\n");
        Reset();
        rec.displayCheck = ^NSString *(int d) { return @"Connect the TV before recording."; };
        CHECK([rec startWithConfigJSON:"{}"] == 0); failed = WaitFor(@"failed", 1); CHECK(failed != nil); CHECK(!rec.active);

        printf("-- unsupported surface (framebufferOnly) -> failed(error), no leftovers\n");
        Reset();
        rec.displayCheck = ^NSString *(int d) { return nil; };
        td.usage = MTLTextureUsageRenderTarget | MTLTextureUsageShaderRead; td.storageMode = MTLStorageModePrivate; // not framebufferOnly, but we fake via a wrong format below
        MTLTextureDescriptor *bad = [MTLTextureDescriptor texture2DDescriptorWithPixelFormat:MTLPixelFormatRGBA16Float width:1280 height:720 mipmapped:NO];
        bad.usage = MTLTextureUsageShaderRead; id<MTLTexture> badTex = [device newTextureWithDescriptor:bad];
        CHECK([rec startWithConfigJSON:"{\"width\":640,\"height\":360,\"display\":0}"] == 1);
        for (int i = 0; i < 5; i++) { id<MTLCommandBuffer> cb = [queue commandBuffer]; [rec captureTexture:badTex commandBuffer:cb timestamp:Now()]; [cb commit]; Sleep(0.02); }
        failed = WaitFor(@"failed", 5); CHECK(failed != nil && [failed[@"reason"] isEqualToString:@"error"]); CHECK(!rec.active);

        printf("\n%d checks, %d failed\n", gChecks, gFailed);
        return gFailed ? 1 : 0;
    }
}
