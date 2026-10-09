using GolfArcade.Tennis;
using NUnit.Framework;

public sealed class ScreenTimingCalibrationTests {
    [Test] public void LongDisplayDelayDoesNotAssignSwingsToTheNextBounce() {
        var check = new TennisBeatCalibration(10, 0, .8f);
        for (int beat = 0; beat < TennisBeatCalibration.Beats; beat++)
            Assert.That(check.Swing(check.BeatTime(beat) + .9f), Is.EqualTo(beat));
        Assert.That(check.TryResult(out float measured), Is.True);
        Assert.That(measured, Is.EqualTo(.9f).Within(.001f));
        Assert.That(check.Finished(check.BeatTime(8) + .9f), Is.False, "Allow the final delayed bounce to be seen and swung at");
    }
    [Test] public void CompensationDoesNotTruncateLongAirPlayDelayTo350ms() {
        var lag = new TennisLagLearner { Prior = .72f };
        Assert.That(lag.Estimate, Is.EqualTo(.72f).Within(.001f));
    }
}
