// Haptics for the phone-as-racket and phone-as-club. Every strike is built by GolfHitFeel.h (the crack,
// the body, the sting of an off-centre ball, the grade's reward) and played here on one CoreHaptics engine
// that is started before the first hit and restarted whenever iOS stops it. Moments the player sees on the
// TV (a bounce, the point) can be played late by the TV's delay so the hand feels them when the eyes do.
// The phone's own speaker can add a short knock: system sounds play on the device and ignore the AirPlay
// route, so it arrives at once while the TV's hit sound is still on its way.
//
// Called from GolfArcade.Game.Haptics via DllImport("__Internal") and from the app (the Settings feel test)
// through dlsym. Every entry point is safe on devices without haptics (it just does less, or nothing).
#import <UIKit/UIKit.h>
#import <CoreHaptics/CoreHaptics.h>
#import <AudioToolbox/AudioToolbox.h>
#include "GolfHitFeel.h"

static CHHapticEngine *engine;
static volatile BOOL engineRunning;
static id<CHHapticAdvancedPatternPlayer> tension;
/// The "swing felt" swish waits a moment before it plays, and a hit that lands in that moment cancels it,
/// so a swing that meets the ball is one clean strike and not a tick and a strike on top of each other.
static id<CHHapticPatternPlayer> swingCue;
static NSTimeInterval swingCueAt;
static const float SwingCueHold = .035f;
static float strength = 1;
static BOOL racketSound;
static SystemSoundID knocks[GolfFeel::SoundCount];
static BOOL knocksReady, knocksBuilding;
static UIImpactFeedbackGenerator *impactGen;
static UISelectionFeedbackGenerator *selectionGen;
static UINotificationFeedbackGenerator *notifyGen;

static float Clamp01(float v) { return v < 0 ? 0 : (v > 1 ? 1 : v); }

/// The engine, created once and kept running. iOS stops it when the app goes to the background, when the
/// audio session is interrupted (voice chat switching category, a call) or on a server reset; it is started
/// again here before the next pattern rather than letting that hit fall through to a cold UIKit tap.
static BOOL EnsureEngine(void)
{
    if (!engine)
    {
        if (![CHHapticEngine capabilitiesForHardware].supportsHaptics) return NO;
        NSError *error = nil;
        engine = [[CHHapticEngine alloc] initAndReturnError:&error];
        if (error || !engine) { engine = nil; return NO; }
        engine.playsHapticsOnly = YES;      // lowest start latency; the knock goes through System Sound Services instead
        engine.autoShutdownEnabled = NO;    // an idle shutdown would put a restart in front of the next hit
        __weak CHHapticEngine *weakEngine = engine;
        engine.stoppedHandler = ^(CHHapticEngineStoppedReason reason) { engineRunning = NO; };
        engine.resetHandler = ^{
            engineRunning = [weakEngine startAndReturnError:nil];
            // Players made before a reset are dead.
            dispatch_async(dispatch_get_main_queue(), ^{ tension = nil; swingCue = nil; });
        };
        [[NSNotificationCenter defaultCenter] addObserverForName:UIApplicationWillEnterForegroundNotification object:nil
            queue:NSOperationQueue.mainQueue usingBlock:^(NSNotification *note) { EnsureEngine(); }];
    }
    if (!engineRunning) engineRunning = [engine startAndReturnError:nil];
    return engineRunning;
}

static CHHapticEventParameter *Param(CHHapticEventParameterID id, float value)
{
    return [[CHHapticEventParameter alloc] initWithParameterID:id value:Clamp01(value)];
}

static CHHapticPattern *PatternFor(const GolfFeel::Layer &layer)
{
    NSMutableArray<CHHapticEvent *> *events = [NSMutableArray array];
    for (const GolfFeel::Event &e : layer.events)
    {
        NSArray *parameters = @[Param(CHHapticEventParameterIDHapticIntensity, e.intensity), Param(CHHapticEventParameterIDHapticSharpness, e.sharpness)];
        [events addObject:e.continuous
            ? [[CHHapticEvent alloc] initWithEventType:CHHapticEventTypeHapticContinuous parameters:parameters relativeTime:e.time duration:e.duration]
            : [[CHHapticEvent alloc] initWithEventType:CHHapticEventTypeHapticTransient parameters:parameters relativeTime:e.time]];
    }
    NSMutableArray<CHHapticParameterCurve *> *curves = [NSMutableArray array];
    if (!layer.curve.empty())
    {
        NSMutableArray<CHHapticParameterCurveControlPoint *> *points = [NSMutableArray array];
        for (const GolfFeel::Point &p : layer.curve)
            [points addObject:[[CHHapticParameterCurveControlPoint alloc] initWithRelativeTime:p.time value:Clamp01(p.value)]];
        [curves addObject:[[CHHapticParameterCurve alloc] initWithParameterID:CHHapticDynamicParameterIDHapticIntensityControl controlPoints:points relativeTime:0]];
    }
    NSError *error = nil;
    CHHapticPattern *pattern = [[CHHapticPattern alloc] initWithEvents:events parameterCurves:curves error:&error];
    return error ? nil : pattern;
}

/// Plays each layer on its own player, all from the same moment: now, or `delay` seconds from now on the
/// engine's clock. Returns the players (empty when nothing could play).
static NSArray<id<CHHapticPatternPlayer>> *Play(const GolfFeel::Score &score, float delay)
{
    NSMutableArray<id<CHHapticPatternPlayer>> *players = [NSMutableArray array];
    if (!EnsureEngine()) return players;
    NSTimeInterval at = delay > .0005f ? engine.currentTime + delay : CHHapticTimeImmediate;
    for (const GolfFeel::Layer &layer : score)
    {
        CHHapticPattern *pattern = PatternFor(layer);
        if (!pattern) continue;
        NSError *error = nil;
        id<CHHapticPatternPlayer> player = [engine createPlayerWithPattern:pattern error:&error];
        if (!player) continue;
        if (![player startAtTime:at error:&error])
        {
            // Stopped behind our back (the handler had not run yet): start it and try once more.
            engineRunning = NO;
            if (!EnsureEngine() || ![player startAtTime:at error:nil]) continue;
        }
        [players addObject:player];
    }
    return players;
}

static void Impact(float intensity)
{
    if (!impactGen) impactGen = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleHeavy];
    [impactGen impactOccurredWithIntensity:Clamp01(intensity)];
    [impactGen prepare];
}

static void CancelSwingCue(void)
{
    if (swingCue && engine && engine.currentTime < swingCueAt) [swingCue cancelAndReturnError:nil];
    swingCue = nil;
}

// ----- The phone's own knock -----

static NSData *Wav(const std::vector<float> &samples, int rate)
{
    uint32_t n = (uint32_t)samples.size(), bytes = n * 2;
    NSMutableData *data = [NSMutableData dataWithLength:44 + bytes];
    uint8_t *b = (uint8_t *)data.mutableBytes;
    auto u32 = [&](int at, uint32_t v) { memcpy(b + at, &v, 4); };
    auto u16 = [&](int at, uint16_t v) { memcpy(b + at, &v, 2); };
    memcpy(b, "RIFF", 4); u32(4, 36 + bytes); memcpy(b + 8, "WAVE", 4);
    memcpy(b + 12, "fmt ", 4); u32(16, 16); u16(20, 1); u16(22, 1); u32(24, rate); u32(28, rate * 2); u16(32, 2); u16(34, 16);
    memcpy(b + 36, "data", 4); u32(40, bytes);
    int16_t *pcm = (int16_t *)(b + 44);
    for (uint32_t i = 0; i < n; i++) pcm[i] = (int16_t)lrintf(fmaxf(-1, fminf(1, samples[i])) * 32767);
    return data;
}

/// Writes the knocks once (a few kB in the temporary directory) and registers them as system sounds.
static void PrepareKnocks(void)
{
    if (knocksReady || knocksBuilding) return;
    knocksBuilding = YES;
    dispatch_async(dispatch_get_global_queue(QOS_CLASS_UTILITY, 0), ^{
        NSString *dir = [NSTemporaryDirectory() stringByAppendingPathComponent:@"golf-feel"];
        [[NSFileManager defaultManager] createDirectoryAtPath:dir withIntermediateDirectories:YES attributes:nil error:nil];
        std::vector<SystemSoundID> made(GolfFeel::SoundCount, 0);
        for (int s = 0; s < GolfFeel::SoundCount; s++)
        {
            NSString *path = [dir stringByAppendingPathComponent:[NSString stringWithFormat:@"knock%d.wav", s]];
            if (![Wav(GolfFeel::RenderSound(s), 44100) writeToFile:path atomically:YES]) continue;
            AudioServicesCreateSystemSoundID((__bridge CFURLRef)[NSURL fileURLWithPath:path], &made[s]);
        }
        dispatch_async(dispatch_get_main_queue(), ^{
            for (int s = 0; s < GolfFeel::SoundCount; s++) knocks[s] = made[s];
            knocksReady = YES; knocksBuilding = NO;
        });
    });
}

static void Knock(int sound)
{
    if (!racketSound || sound < 0 || sound >= GolfFeel::SoundCount) return;
    if (!knocksReady) { PrepareKnocks(); return; }
    if (knocks[sound]) AudioServicesPlaySystemSoundWithCompletion(knocks[sound], nil);
}

static void HitNow(const GolfFeel::Hit &hit)
{
    CancelSwingCue();
    Knock(GolfFeel::SoundFor(hit));
    if (Play(GolfFeel::BuildHit(hit, strength), 0).count == 0)
        Impact(GolfFeel::Mix(.5f, 1, GolfFeel::Clean(hit)) * strength);
}

static void CueNow(int kind, float intensity, float delay)
{
    float wait = fmaxf(0, fminf(1, delay));
    if (kind == GolfFeel::Swing) { CancelSwingCue(); wait = fmaxf(wait, SwingCueHold); }
    NSArray<id<CHHapticPatternPlayer>> *players = Play(GolfFeel::BuildCue(kind, intensity, strength), wait);
    if (kind == GolfFeel::Swing && players.count) { swingCue = players.firstObject; swingCueAt = engine.currentTime + wait; }
    else if (players.count == 0 && kind != GolfFeel::Swing && kind != GolfFeel::Bounce && wait < .001f) Impact(.5f * intensity * strength);
}

// The app reaches Configure and the feel test through dlsym, so they must stay exported and survive dead-stripping.
#define GOLF_HAPTICS_APP_API __attribute__((visibility("default"), used))

extern "C" {

/// Start the engine and make the knocks now, so the first hit of a match pays for neither.
void GolfHaptics_Warm(void)
{
    EnsureEngine();
    if (racketSound) PrepareKnocks();
}

/// `level` scales every haptic (Settings: hit strength); `sound` turns the phone-speaker knock on.
GOLF_HAPTICS_APP_API void GolfHaptics_Configure(float level, int sound)
{
    strength = fmaxf(.2f, fminf(1, level));
    racketSound = sound != 0;
    if (racketSound) PrepareKnocks();
}

/// A strike, felt by how it was struck. See GolfHitFeel.h for the fields.
void GolfHaptics_Hit(int style, int grade, int pips, int flags, float power, float center, float mistime, float heat)
{
    HitNow({ style, grade, pips, flags, power, center, mistime, heat });
}

/// A moment that is not a strike (GolfFeel::Cue), `delay` seconds from now (the TV's lag for TV moments).
void GolfHaptics_Cue(int kind, float intensity, float delay) { CueNow(kind, intensity, delay); }

GOLF_HAPTICS_APP_API int GolfHaptics_DemoCount(void) { return (int)GolfFeel::Demos().size(); }

/// Plays entry `index` of the feel test and returns its name (static storage), or NULL past the end.
GOLF_HAPTICS_APP_API const char *GolfHaptics_Demo(int index)
{
    const auto &demos = GolfFeel::Demos();
    if (index < 0 || index >= (int)demos.size()) return NULL;
    const GolfFeel::Demo &d = demos[index];
    if (d.cue) CueNow(d.kind, 1, 0); else HitNow(d.hit);
    return d.name;
}

void GolfHaptics_Selection(void)
{
    if (!selectionGen) { selectionGen = [[UISelectionFeedbackGenerator alloc] init]; }
    [selectionGen selectionChanged];
    [selectionGen prepare];
}

/// 0 success, 1 warning, 2 error.
void GolfHaptics_Notification(int type)
{
    if (!notifyGen) { notifyGen = [[UINotificationFeedbackGenerator alloc] init]; }
    UINotificationFeedbackType t = type == 0 ? UINotificationFeedbackTypeSuccess
                                 : type == 1 ? UINotificationFeedbackTypeWarning
                                             : UINotificationFeedbackTypeError;
    [notifyGen notificationOccurred:t];
    [notifyGen prepare];
}

/// Starts a silent continuous buzz (up to 30 s) whose intensity and sharpness the game drives.
void GolfHaptics_TensionStart(void)
{
    if (!EnsureEngine()) return;
    if (tension) { [tension stopAtTime:0 error:nil]; tension = nil; }
    CHHapticEventParameter *intensity = [[CHHapticEventParameter alloc] initWithParameterID:CHHapticEventParameterIDHapticIntensity value:1.0f];
    CHHapticEventParameter *sharpness = [[CHHapticEventParameter alloc] initWithParameterID:CHHapticEventParameterIDHapticSharpness value:0.5f];
    CHHapticEvent *event = [[CHHapticEvent alloc] initWithEventType:CHHapticEventTypeHapticContinuous
                                                         parameters:@[intensity, sharpness]
                                                       relativeTime:0
                                                           duration:30];
    NSError *error = nil;
    CHHapticPattern *pattern = [[CHHapticPattern alloc] initWithEvents:@[event] parameters:@[] error:&error];
    if (error) return;
    tension = [engine createAdvancedPlayerWithPattern:pattern error:&error];
    if (error || !tension) { tension = nil; return; }
    // Begin at nothing; the first TensionSet brings it up.
    CHHapticDynamicParameter *quiet = [[CHHapticDynamicParameter alloc] initWithParameterID:CHHapticDynamicParameterIDHapticIntensityControl value:0 relativeTime:0];
    [tension sendParameters:@[quiet] atTime:0 error:nil];
    [tension startAtTime:0 error:nil];
}

void GolfHaptics_TensionSet(float intensity, float sharpness)
{
    if (!tension) return;
    CHHapticDynamicParameter *i = [[CHHapticDynamicParameter alloc] initWithParameterID:CHHapticDynamicParameterIDHapticIntensityControl value:Clamp01(intensity) * strength relativeTime:0];
    CHHapticDynamicParameter *s = [[CHHapticDynamicParameter alloc] initWithParameterID:CHHapticDynamicParameterIDHapticSharpnessControl value:Clamp01(sharpness) relativeTime:0];
    [tension sendParameters:@[i, s] atTime:0 error:nil];
}

void GolfHaptics_TensionStop(void)
{
    if (!tension) return;
    [tension stopAtTime:0 error:nil];
    tension = nil;
}

}
