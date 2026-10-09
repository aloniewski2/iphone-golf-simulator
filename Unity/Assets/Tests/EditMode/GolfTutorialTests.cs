using NUnit.Framework;
using GolfArcade.Game;

public class GolfTutorialTests
{
    [Test] public void PracticeThenChangedAimThenHole() {
        var lesson = new GolfTutorialLesson();
        Assert.IsTrue(lesson.Shot(false));
        Assert.AreEqual(GolfTutorialLesson.Phase.Aim, lesson.Current);
        Assert.IsFalse(lesson.Shot(false)); Assert.AreEqual(1, lesson.Misses);
        Assert.IsTrue(lesson.Shot(true));
        Assert.AreEqual(GolfTutorialLesson.Phase.Hole, lesson.Current);
        lesson.Shot(false); Assert.IsTrue(lesson.Holed());
        Assert.AreEqual(GolfTutorialLesson.Phase.Done, lesson.Current); Assert.IsFalse(lesson.Capped);
    }
    [Test] public void SixthHoleStrokeCompletesExactlyOnce() {
        var lesson = new GolfTutorialLesson(); lesson.Shot(false); lesson.Shot(true);
        for (int stroke = 1; stroke < 6; stroke++) { Assert.IsFalse(lesson.Shot(false)); Assert.AreEqual(GolfTutorialLesson.Phase.Hole, lesson.Current); }
        Assert.IsTrue(lesson.Shot(false)); Assert.IsTrue(lesson.Capped);
        Assert.AreEqual(6, lesson.Strokes); Assert.AreEqual(GolfTutorialLesson.Phase.Done, lesson.Current);
        Assert.IsFalse(lesson.Shot(false)); Assert.AreEqual(6, lesson.Strokes);
    }
    [Test] public void SkipAdvancesOnlyOneExercise() {
        var lesson = new GolfTutorialLesson(); lesson.Skip(); Assert.AreEqual(GolfTutorialLesson.Phase.Aim, lesson.Current);
        lesson.Skip(); Assert.AreEqual(GolfTutorialLesson.Phase.Hole, lesson.Current);
    }
}
