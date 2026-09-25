// Haptics for the phone-as-club: a continuous CoreHaptics "tension" that the game ramps with the
// backswing, plus the UIKit feedback generators for impact, taps and results. Called from
// GolfArcade.Game.Haptics via DllImport("__Internal"); every entry point is safe on devices
// without haptics (it just does nothing).
#import <UIKit/UIKit.h>
#import <CoreHaptics/CoreHaptics.h>

static CHHapticEngine *engine;
static id<CHHapticAdvancedPatternPlayer> tension;
static UIImpactFeedbackGenerator *impactGen;
static UISelectionFeedbackGenerator *selectionGen;
static UINotificationFeedbackGenerator *notifyGen;

static void EnsureEngine(void)
{
    if (engine) return;
    if (![CHHapticEngine capabilitiesForHardware].supportsHaptics) return;
    NSError *error = nil;
    engine = [[CHHapticEngine alloc] initAndReturnError:&error];
    if (error || !engine) { engine = nil; return; }
    engine.playsHapticsOnly = YES;
    __weak CHHapticEngine *weakEngine = engine;
    engine.resetHandler = ^{ [weakEngine startAndReturnError:nil]; };
    engine.stoppedHandler = ^(CHHapticEngineStoppedReason reason) { tension = nil; };
    [engine startAndReturnError:nil];
}

static float Clamp01(float v) { return v < 0 ? 0 : (v > 1 ? 1 : v); }

extern "C" {

void GolfHaptics_Impact(float intensity)
{
    if (!impactGen) { impactGen = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleHeavy]; }
    [impactGen impactOccurredWithIntensity:Clamp01(intensity)];
    [impactGen prepare];
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
    EnsureEngine();
    if (!engine) return;
    if (tension) { [tension stopAtTime:0 error:nil]; tension = nil; }
    [engine startAndReturnError:nil];
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
    CHHapticDynamicParameter *i = [[CHHapticDynamicParameter alloc] initWithParameterID:CHHapticDynamicParameterIDHapticIntensityControl value:Clamp01(intensity) relativeTime:0];
    CHHapticDynamicParameter *s = [[CHHapticDynamicParameter alloc] initWithParameterID:CHHapticDynamicParameterIDHapticSharpnessControl value:Clamp01(sharpness) relativeTime:0];
    [tension sendParameters:@[i, s] atTime:0 error:nil];
}

static CHHapticEvent *Tap(float at, float intensity, float sharpness)
{
    return [[CHHapticEvent alloc] initWithEventType:CHHapticEventTypeHapticTransient
        parameters:@[[[CHHapticEventParameter alloc] initWithParameterID:CHHapticEventParameterIDHapticIntensity value:Clamp01(intensity)],
                     [[CHHapticEventParameter alloc] initWithParameterID:CHHapticEventParameterIDHapticSharpness value:Clamp01(sharpness)]]
        relativeTime:at];
}

static CHHapticEvent *Buzz(float at, float seconds, float intensity, float sharpness)
{
    return [[CHHapticEvent alloc] initWithEventType:CHHapticEventTypeHapticContinuous
        parameters:@[[[CHHapticEventParameter alloc] initWithParameterID:CHHapticEventParameterIDHapticIntensity value:Clamp01(intensity)],
                     [[CHHapticEventParameter alloc] initWithParameterID:CHHapticEventParameterIDHapticSharpness value:Clamp01(sharpness)]]
        relativeTime:at duration:seconds];
}

/// One-shot patterns the game names by number, scaled by `intensity`:
/// 0 the top of the backswing — one crisp click;
/// 1 a PERFECT strike — a hard, bright crack with a short ring after it;
/// 2 a GREAT or GOOD strike — a solid knock;
/// 3 a THIN strike — a dull, buzzy sting;
/// 4 the crowd erupting (a holed ball) — a rolling run of thumps.
void GolfHaptics_Pattern(int kind, float intensity)
{
    EnsureEngine();
    if (!engine) { GolfHaptics_Impact(intensity); return; }
    [engine startAndReturnError:nil];
    float k = Clamp01(intensity);
    NSArray<CHHapticEvent *> *events;
    switch (kind)
    {
        case 0: events = @[Tap(0, 0.55f + 0.45f * k, 1.0f)]; break;
        case 1: events = @[Tap(0, 1.0f, 0.95f), Buzz(0.01f, 0.07f, 0.45f * k + 0.2f, 1.0f), Tap(0.09f, 0.35f * k, 0.9f)]; break;
        case 2: events = @[Tap(0, 0.4f + 0.6f * k, 0.6f)]; break;
        case 3: events = @[Buzz(0, 0.16f, 0.35f + 0.5f * k, 0.08f), Tap(0, 0.3f + 0.3f * k, 0.15f)]; break;
        default:
        {
            NSMutableArray<CHHapticEvent *> *run = [NSMutableArray array];
            for (int i = 0; i < 9; i++) [run addObject:Tap(i * 0.085f, (0.9f - i * 0.07f) * (0.5f + 0.5f * k), 0.35f + 0.05f * (i % 3))];
            [run addObject:Buzz(0, 0.8f, 0.25f * k, 0.2f)];
            events = run;
            break;
        }
    }
    NSError *error = nil;
    CHHapticPattern *pattern = [[CHHapticPattern alloc] initWithEvents:events parameters:@[] error:&error];
    if (error || !pattern) { GolfHaptics_Impact(intensity); return; }
    id<CHHapticPatternPlayer> player = [engine createPlayerWithPattern:pattern error:&error];
    if (error || !player) { GolfHaptics_Impact(intensity); return; }
    [player startAtTime:CHHapticTimeImmediate error:nil];
}

void GolfHaptics_TensionStop(void)
{
    if (!tension) return;
    [tension stopAtTime:0 error:nil];
    tension = nil;
}

}
