using GolfArcade.Game;
using GolfArcade.Tennis;
using NUnit.Framework;
using UnityEngine;

namespace GolfArcade.Tests
{
    /// What a tennis contact hands the phone's haptics: the hand must agree with the call on the TV, feel where the
    /// ball met the strings and how late it was, count the streak, and give a fault a dull strike.
    public class TennisHitFeelTests
    {
        static Haptics.HitFeel Rally(Timing grade = Timing.Great, Vector2 face = default, float late = 0, TennisActor.Stroke kind = TennisActor.Stroke.Drive,
            int streak = 0, bool supercharged = false, int rally = 3) =>
            TennisHitFeel.Rally(grade, face, late, kind, .7f, streak, supercharged, rally);

        [Test]
        public void TheGradeIsTheOneTheTvCalls()
        {
            foreach (Timing grade in System.Enum.GetValues(typeof(Timing)))
                Assert.AreEqual((int)grade, Rally(grade).Grade, grade.ToString());
        }

        [Test]
        public void TheSweetSpotIsCentreAndTheFrameIsNot()
        {
            Assert.AreEqual(1, Rally().Center, 1e-5f, "dead centre");
            Assert.AreEqual(0, Rally(face: new Vector2(TennisRules.StringHalfWidth, 0)).Center, 1e-5f, "at the edge of the strings");
            float half = Rally(face: new Vector2(TennisRules.StringHalfWidth * .5f, 0)).Center;
            Assert.That(half, Is.InRange(.45f, .55f), "half way out");
        }

        [Test]
        public void LatenessEitherWayIsMistimingUpToTheEdgeOfGood()
        {
            Assert.AreEqual(0, Rally(late: 0).Mistime, 1e-5f);
            Assert.AreEqual(Rally(late: .07f).Mistime, Rally(late: -.07f).Mistime, 1e-5f, "early and late alike");
            Assert.AreEqual(.5f, Rally(late: .07f).Mistime, 1e-3f);
            Assert.AreEqual(1, Rally(late: .3f).Mistime, 1e-5f, "past GOOD it is simply mistimed");
        }

        [Test]
        public void StreakPipsCountTowardTheSuperchargeAndStopOnIt()
        {
            Assert.AreEqual(1, Rally(streak: 1).Pips);
            Assert.AreEqual(TennisRules.SuperchargeStreak - 1, Rally(streak: 9).Pips, "never more pips than balls to go");
            var charged = Rally(Timing.Perfect, streak: 0, supercharged: true);
            Assert.IsTrue(charged.Supercharged);
            Assert.AreEqual(0, charged.Pips, "the supercharge itself is the reward");
        }

        [Test]
        public void StrokesCarryTheirOwnWeight()
        {
            Assert.AreEqual(Haptics.HitStyle.Heavy, Rally(kind: TennisActor.Stroke.Smash).Style);
            Assert.AreEqual(Haptics.HitStyle.Touch, Rally(kind: TennisActor.Stroke.Volley).Style);
            Assert.AreEqual(Haptics.HitStyle.Touch, Rally(kind: TennisActor.Stroke.Slice).Style);
            Assert.AreEqual(Haptics.HitStyle.Scramble, Rally(kind: TennisActor.Stroke.Dive).Style);
            Assert.AreEqual(Haptics.HitStyle.Drive, Rally(kind: TennisActor.Stroke.Topspin).Style);
        }

        [Test]
        public void LongRalliesRunHotter()
        {
            Assert.AreEqual(0, Rally(rally: 1).Heat, 1e-5f);
            Assert.That(Rally(rally: 8).Heat, Is.GreaterThan(Rally(rally: 4).Heat));
            Assert.AreEqual(1, Rally(rally: 30).Heat, 1e-5f);
        }

        [Test]
        public void AServeIsGradedLikeItsBannerAndAFaultIsDull()
        {
            var perfect = TennisHitFeel.Serve(new TennisRules.ServeJudgement { Struck = true, Legal = true, Perfect = true, Power = .95f, Accuracy = 1 });
            Assert.AreEqual((int)Timing.Perfect, perfect.Grade);
            Assert.AreEqual(Haptics.HitStyle.Serve, perfect.Style);
            var firm = TennisHitFeel.Serve(new TennisRules.ServeJudgement { Struck = true, Legal = true, Power = .8f, Accuracy = .8f });
            Assert.AreEqual((int)Timing.Excellent, firm.Grade);
            var fault = TennisHitFeel.Serve(new TennisRules.ServeJudgement { Struck = true, Legal = false, Power = .9f });
            Assert.AreEqual((int)Timing.Ok, fault.Grade);
            Assert.That(fault.Center, Is.LessThan(firm.Center));
        }

        [Test]
        public void AnOnlineHitFeelsCleanerForABetterGrade()
        {
            Assert.That(TennisHitFeel.Graded(Timing.Perfect, .7f).Center, Is.GreaterThan(TennisHitFeel.Graded(Timing.Good, .7f).Center));
            Assert.That(TennisHitFeel.Graded(Timing.Good, .7f).Mistime, Is.GreaterThan(TennisHitFeel.Graded(Timing.Perfect, .7f).Mistime));
        }
    }
}
