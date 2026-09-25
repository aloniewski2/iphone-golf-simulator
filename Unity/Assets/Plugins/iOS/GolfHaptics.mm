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

/// The racket meeting the ball. A sharp transient from the Taptic Engine is audible as a crisp
/// click from the phone itself, and unlike a sound it can never be routed to the (delayed)
/// TV. Cleaner contact hits harder and sharper; a super shot strikes twice.
void GolfHaptics_Strike(float quality, int twice)
{
    EnsureEngine();
    if (!engine) { GolfHaptics_Impact(0.5f + 0.5f * quality); return; }
    float q = Clamp01(quality);
    NSMutableArray<CHHapticEvent*> *events = [NSMutableArray array];
    for (int i = 0; i < (twice ? 2 : 1); i++) {
        float t = i * 0.07f;
        [events addObject:[[CHHapticEvent alloc] initWithEventType:CHHapticEventTypeHapticTransient parameters:@[
            [[CHHapticEventParameter alloc] initWithParameterID:CHHapticEventParameterIDHapticIntensity value:0.55f + 0.45f * q],
            [[CHHapticEventParameter alloc] initWithParameterID:CHHapticEventParameterIDHapticSharpness value:0.6f + 0.4f * q]]
            relativeTime:t]];
        // A short body behind the click, so it lands as a thock rather than a tick.
        [events addObject:[[CHHapticEvent alloc] initWithEventType:CHHapticEventTypeHapticContinuous parameters:@[
            [[CHHapticEventParameter alloc] initWithParameterID:CHHapticEventParameterIDHapticIntensity value:0.35f + 0.35f * q],
            [[CHHapticEventParameter alloc] initWithParameterID:CHHapticEventParameterIDHapticSharpness value:0.3f]]
            relativeTime:t + 0.005f duration:0.045f]];
    }
    NSError *error = nil;
    CHHapticPattern *pattern = [[CHHapticPattern alloc] initWithEvents:events parameters:@[] error:&error];
    id<CHHapticPatternPlayer> player = error ? nil : [engine createPlayerWithPattern:pattern error:&error];
    if (!player || ![player startAtTime:CHHapticTimeImmediate error:&error]) GolfHaptics_Impact(0.5f + 0.5f * q);
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

void GolfHaptics_TensionStop(void)
{
    if (!tension) return;
    [tension stopAtTime:0 error:nil];
    tension = nil;
}

}
