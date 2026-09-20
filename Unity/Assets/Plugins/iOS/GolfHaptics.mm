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

void GolfHaptics_TensionStop(void)
{
    if (!tension) return;
    [tension stopAtTime:0 error:nil];
    tension = nil;
}

}
