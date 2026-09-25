using NUnit.Framework;
using GolfArcade.Tennis;

/// The first-time tennis lesson: every step, in order, and nobody gets stuck.
public class TennisTutorialTests
{
    [Test] public void CalibrationCountsEverySwingOnADelayedDisplay()
    {
        var calibration = new TennisBeatCalibration(0);
        for (int beat = 0; beat < TennisBeatCalibration.Beats; beat++)
        {
            float time = calibration.BeatTime(beat) + .48f;
            Assert.AreEqual(beat, calibration.Swing(time));
            Assert.AreEqual(-1, calibration.Swing(time + .02f), "one count per bounce");
        }
        Assert.AreEqual(TennisBeatCalibration.Beats - TennisBeatCalibration.WarmUp, calibration.Scored);
        Assert.IsTrue(calibration.TryResult(out _));
    }

    [Test] public void TheLessonTeachesEachSkillInOrder()
    {
        var kinds = System.Array.ConvertAll(TutorialLesson.Steps, s => s.Kind);
        Assert.AreEqual(new[] { TutorialLesson.Kind.Serve, TutorialLesson.Kind.Forehand, TutorialLesson.Kind.Backhand,
            TutorialLesson.Kind.Aim, TutorialLesson.Kind.Point }, kinds);
        foreach (var s in TutorialLesson.Steps) { Assert.IsNotEmpty(s.Say); Assert.IsNotEmpty(s.Hint); Assert.Greater(s.Goal, 0); }
    }

    [Test] public void SuccessesCompleteAStepAndTheLesson()
    {
        var lesson = new TutorialLesson();
        int successes = 0;
        while (!lesson.Done) { lesson.Success(); successes++; Assert.Less(successes, 100); }
        int goal = 0; foreach (var s in TutorialLesson.Steps) goal += s.Goal;
        Assert.AreEqual(goal, successes);
    }

    [Test] public void MissesCoachAndRetryInsteadOfSilentlyPassing()
    {
        var lesson = new TutorialLesson();
        for (int i = 0; i < 20; i++) Assert.IsFalse(lesson.Miss());
        Assert.IsTrue(lesson.HintDue);
        Assert.AreEqual(TutorialLesson.Kind.Serve, lesson.Current.Kind);
        Assert.AreEqual(0, lesson.Progress);
        lesson.Success();
        Assert.AreEqual(TutorialLesson.Kind.Forehand, lesson.Current.Kind);
        Assert.AreEqual(0, lesson.Misses);
    }

    [Test] public void AimAsksForTheLeftRingThenTheRight()
    {
        var lesson = new TutorialLesson();
        while (lesson.Current.Kind != TutorialLesson.Kind.Aim) lesson.Success();
        Assert.IsTrue(lesson.AimLeft); Assert.That(lesson.Instruction, Does.Contain("LEFT"));
        lesson.Success(); Assert.IsFalse(lesson.AimLeft); Assert.That(lesson.Instruction, Does.Contain("RIGHT"));
    }

    [Test] public void KitColoursParseFromTheLaunchMessage()
    {
        var kit = TennisLook.Kit.From("FF0000", "", null, "1E2A6E", 2);
        Assert.AreEqual(1f, kit.Shirt.r, 1e-3); Assert.AreEqual(1f, kit.Shirt.a);
        Assert.AreEqual(0f, kit.Shorts.a, "empty keeps the kit's own colour");
        Assert.IsTrue(kit.Any);
        Assert.IsFalse(TennisLook.Kit.From("", "", "", "", 2).Any, "nothing chosen, nothing recoloured");
        Assert.IsTrue(TennisLook.Kit.From("", "", "", "", 4).Any, "a different skin tone recolours");
    }
}
