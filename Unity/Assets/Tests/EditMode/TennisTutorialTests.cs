using NUnit.Framework;
using GolfArcade.Tennis;

/// The first-time tennis lesson: every step, in order, and nobody gets stuck.
public class TennisTutorialTests
{
    [Test] public void TheLessonTeachesEachSkillInOrder()
    {
        var kinds = System.Array.ConvertAll(TutorialLesson.Steps, s => s.Kind);
        Assert.AreEqual(new[] { TutorialLesson.Kind.Move, TutorialLesson.Kind.Forehand, TutorialLesson.Kind.Backhand,
            TutorialLesson.Kind.Aim, TutorialLesson.Kind.Timing, TutorialLesson.Kind.Serve, TutorialLesson.Kind.Point }, kinds);
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

    [Test] public void MissesHintThenMoveOn()
    {
        var lesson = new TutorialLesson();
        lesson.Success(); lesson.Success();   // through Move
        Assert.AreEqual(TutorialLesson.Kind.Forehand, lesson.Current.Kind);
        for (int i = 0; i < TutorialLesson.HintAfter - 1; i++) lesson.Miss();
        Assert.IsFalse(lesson.HintDue);
        lesson.Miss(); Assert.IsTrue(lesson.HintDue, "a hint after a few misses");
        for (int i = TutorialLesson.HintAfter; i < TutorialLesson.MoveOnAfter - 1; i++) Assert.IsFalse(lesson.Miss());
        Assert.IsTrue(lesson.Miss(), "the lesson moves on rather than leaving the player stuck");
        Assert.AreEqual(TutorialLesson.Kind.Backhand, lesson.Current.Kind);
    }

    [Test] public void AimAsksForTheLeftRingThenTheRight()
    {
        var lesson = new TutorialLesson();
        while (lesson.Current.Kind != TutorialLesson.Kind.Aim) lesson.Success();
        Assert.IsTrue(lesson.AimLeft); lesson.Success(); Assert.IsFalse(lesson.AimLeft);
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
