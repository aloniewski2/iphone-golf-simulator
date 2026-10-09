using NUnit.Framework;
using GolfArcade.Game;

namespace GolfArcade.Tests
{
    public class PresentationDirectorTests
    {
        [Test] public void FirstAndRepeatedIntroBudgetsMatchTheBrief()
        {
            foreach (bool golf in new[] { false, true })
            foreach (var cut in new[] { PresentationCut.Full, PresentationCut.Short })
            {
                float duration = PresentationDirector.Budget(PresentationBeat.Venue, golf, cut)
                    + PresentationDirector.Budget(PresentationBeat.WalkOn, golf, cut)
                    + PresentationDirector.Budget(PresentationBeat.Start, golf, cut);
                Assert.AreEqual(golf ? (cut == PresentationCut.Full ? 6.5f : 2.9f) : (cut == PresentationCut.Full ? 7.2f : 3.5f), duration, .001f);
            }
        }
        [Test] public void SkippingConsumesTheBeatAndRestoresExactlyOnce()
        {
            var director = new PresentationDirector(); int restored = 0, ready = 0, seen = 0;
            director.Begin(PresentationBeat.WalkOn, PresentationCut.Full, 3, .3f, () => restored++, () => ready++, () => seen++);
            Assert.IsFalse(director.Skip()); director.Tick(.3f); Assert.IsTrue(director.Skip());
            director.Tick(10); director.Cancel(); Assert.IsFalse(director.Skip());
            Assert.AreEqual(1, restored); Assert.AreEqual(1, ready); Assert.AreEqual(0, seen);
        }
        [Test] public void PauseAndCancellationNeverMarkAnIntroSeenOrUnlockPlay()
        {
            var director = new PresentationDirector(); int restored = 0, ready = 0, seen = 0;
            director.Begin(PresentationBeat.Venue, PresentationCut.Full, 3, .3f, () => restored++, () => ready++, () => seen++);
            director.Tick(10, paused: true); Assert.AreEqual(0, director.Elapsed); Assert.IsTrue(director.Playing);
            director.Cancel(); Assert.AreEqual(1, restored); Assert.AreEqual(0, ready); Assert.AreEqual(0, seen);
        }
        [Test] public void SharedBeatsRejectIndividualSkipAndFinishAtTheirCap()
        {
            var director = new PresentationDirector(); int ready = 0;
            director.Begin(PresentationBeat.Venue, PresentationCut.Short, 1.2f, .3f, null, () => ready++, shared: true);
            director.Tick(.8f); Assert.IsFalse(director.Skip()); director.Tick(.4f);
            Assert.AreEqual(1, ready); Assert.IsFalse(director.Playing);
        }
        [Test] public void OffAndThermalFallbackRetainTheSafeStart()
        {
            Assert.AreEqual(PresentationCut.Short, PresentationDirector.Select(PresentationCut.Full, false, false, true));
            Assert.AreEqual(PresentationCut.Short, PresentationDirector.Select(PresentationCut.Full, false, true, false));
            Assert.AreEqual(PresentationCut.Short, PresentationDirector.Select(PresentationCut.Full, true, false, false));
            Assert.AreEqual(0, PresentationDirector.Budget(PresentationBeat.Venue, false, PresentationCut.Off));
            Assert.AreEqual(.8f, PresentationDirector.Budget(PresentationBeat.Start, false, PresentationCut.Off));
            Assert.AreEqual(4, PresentationDirector.GolfReaction(-3, true, PresentationCut.Full));
        }
    }
}
