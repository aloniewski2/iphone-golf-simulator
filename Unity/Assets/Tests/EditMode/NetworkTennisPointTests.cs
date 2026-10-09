using System;
using NUnit.Framework;
using GolfArcade.Multiplayer;
using GolfArcade.Tennis;

namespace GolfArcade.Tests {
    /// Who wins a point when the ball is not returned, and how long the host waits for a late swing.
    /// Plain C# only, so these also run without Unity (Tools/netsim).
    public class NetworkTennisPointTests {
        static NetworkInput Ev(NetworkTennisMatch m, string action, long id, double time, double age = 0) =>
            NetworkTimingTests.Event(m, action, id, time, age);

        // Seat 1 returns the serve perfectly; the ball is then coming back to seat 0.
        static NetworkTennisMatch AfterPerfectReturn() {
            double tBall = NetworkTimingTests.BallReachesReceiver();
            var m = NetworkTimingTests.ServeToRally();
            NetworkTimingTests.RunTo(m, tBall - TennisRules.SweetTime);
            m.Input(1, Ev(m, "beginSwing", 3, m.State.time), m.State.time);
            NetworkTimingTests.RunTo(m, m.State.time + TennisRules.SweetTime);
            m.Input(1, Ev(m, "swing", 4, m.State.time), m.State.time);
            for (int i = 0; i < 240 && m.State.contact < 2 && m.State.phase == "rally"; i++) m.Step(1.0 / 120);
            Assert.AreEqual(2, m.State.contact, "The perfectly timed return must be struck.");
            Assert.AreEqual(0, m.State.receiver);
            return m;
        }

        static double StepUntilPointEnds(NetworkTennisMatch m, double limit = 4) {
            double elapsed = 0;
            while (m.State.phase == "rally" && elapsed < limit) { m.Step(1.0 / 120); elapsed += 1.0 / 120; }
            return elapsed;
        }

        [Test] public void AnUnreturnedServeThatLandedLegallyIsAnAceForTheServer() {
            var m = NetworkTimingTests.ServeToRally();
            StepUntilPointEnds(m);
            Assert.AreEqual("point", m.State.phase);
            Assert.AreEqual(0, m.State.winner, "Nobody returned a legal serve: the server wins the point.");
            Assert.AreEqual(1, m.State.score.PlayerPoints);
            Assert.AreEqual(0, m.State.score.OpponentPoints);
        }

        [Test] public void AnUnreturnedRallyBallThatLandedInCourtWinsThePointForTheHitter() {
            var m = AfterPerfectReturn();
            StepUntilPointEnds(m);
            Assert.AreEqual("point", m.State.phase);
            Assert.AreEqual(1, m.State.winner, "Seat 1 hit it, seat 0 never reached it: seat 1 wins.");
        }

        [Test] public void ABallThatNeverLandedIsTheHittersFault() {
            var m = new NetworkTennisMatch();
            m.State.phase = "rally"; m.State.receiver = 1; m.State.serveFlight = false; m.State.bounces = 0;
            m.State.ball = new NetworkVector(0, 3, 17.5f); m.State.velocity = new NetworkVector(0, 2, 12);
            StepUntilPointEnds(m, 1);
            Assert.AreEqual("point", m.State.phase);
            Assert.AreEqual(1, m.State.winner, "The ball flew out without landing: the player who should have received it wins.");
        }

        [Test] public void WithNoSwingStartedThePointIsNotDelayed() {
            double tBall = NetworkTimingTests.BallReachesReceiver();
            var m = NetworkTimingTests.ServeToRally();
            NetworkTimingTests.RunTo(m, tBall);
            double alive = StepUntilPointEnds(m);
            Assert.Less(alive, .25, "An unreturned ball with no swing under way ends the point as soon as it is dead.");
        }

        [Test] public void AStartedSwingHoldsThePointOpenForItsConfirmationButOnlyBriefly() {
            double tBall = NetworkTimingTests.BallReachesReceiver();
            var m = NetworkTimingTests.ServeToRally();
            NetworkTimingTests.RunTo(m, tBall + .05);
            m.Input(1, Ev(m, "beginSwing", 3, m.State.time, 0), m.State.time);   // heard just before the ball dies
            double heardAt = m.State.time;
            double alive = StepUntilPointEnds(m);
            Assert.AreEqual("point", m.State.phase);
            Assert.AreEqual(0, m.State.winner, "The swing never confirmed, so the server still wins.");
            Assert.LessOrEqual(m.State.time - heardAt, NetworkTuning.PendingSwingHold + .02, "The wait is bounded.");
            Assert.Greater(m.State.time - heardAt, .1, "...but the host did wait for the confirmation.");
        }

        [Test] public void AnAbortedSwingDoesNotHoldThePoint() {
            double tBall = NetworkTimingTests.BallReachesReceiver();
            var m = NetworkTimingTests.ServeToRally();
            NetworkTimingTests.RunTo(m, tBall);
            m.Input(1, Ev(m, "beginSwing", 3, m.State.time, 0), m.State.time);
            m.Input(1, Ev(m, "abortSwing", 4, m.State.time, 0), m.State.time);
            double alive = StepUntilPointEnds(m);
            Assert.Less(alive, .25);
            Assert.AreEqual(0, m.State.winner);
        }

        [Test] public void AConfirmedSwingThatMissedDoesNotHoldThePointEither() {
            double tBall = NetworkTimingTests.BallReachesReceiver();
            var m = NetworkTimingTests.ServeToRally();
            // Swings far too early: begins and confirms long before the ball arrives, so it cannot connect.
            NetworkTimingTests.RunTo(m, tBall - .9);
            m.Input(1, Ev(m, "beginSwing", 3, m.State.time, 0), m.State.time);
            NetworkTimingTests.RunTo(m, m.State.time + .15);
            m.Input(1, Ev(m, "swing", 4, m.State.time, 0), m.State.time);
            NetworkTimingTests.RunTo(m, tBall);
            double alive = StepUntilPointEnds(m);
            Assert.AreEqual("point", m.State.phase);
            Assert.AreEqual(0, m.State.winner);
            Assert.Less(alive, .3);
        }
    }
}
