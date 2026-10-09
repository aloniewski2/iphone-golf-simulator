using System;
using NUnit.Framework;
using GolfArcade.Multiplayer;
using GolfArcade.Tennis;

namespace GolfArcade.Tests {
    /// The host judges a swing as the player SAW it: network time + sensor age + the player's screen
    /// (TV/AirPlay) delay. These tests replay a perfectly timed return through the real host rules.
    /// They use only plain C#, so they also run without Unity (Tools/netsim).
    public class NetworkTimingTests {
        const double Sensor = .03;   // how long after the motion the phone hands it to the game
        // A served ball stays in play only ~190 ms after it passes the receiver, so the host must HEAR that the
        // swing started (screen delay + network + sensor after the ideal moment - the stroke's own lead) inside
        // that time: about 0.34 s of screen delay + network time. 0.30 leaves margin for the test's coarse steps.
        const double ServeReturnBudget = .30;
        static readonly string Perfect = TennisRules.GradeLabel(Timing.Perfect);

        public static NetworkInput Event(NetworkTennisMatch m, string action, long id, double time, double age) =>
            new() { action = action, eventID = id, time = time, age = age, point = m.State.point, contact = m.State.contact, power = .7f };

        public static void RunTo(NetworkTennisMatch m, double time) { while (m.State.time < time - 1e-9) m.Step(1.0 / 120); }

        // Seat 0 serves; the ball is then in flight toward the receiver, seat 1.
        public static NetworkTennisMatch ServeToRally() {
            var m = new NetworkTennisMatch(1, 3);
            RunTo(m, .25);
            m.Input(0, Event(m, "toss", 1, m.State.time, 0), m.State.time);
            RunTo(m, m.State.time + .75);
            m.Input(0, Event(m, "swing", 2, m.State.time, 0), m.State.time);
            return m;
        }

        // Host time at which the served ball (after its bounce) reaches the receiver's racket plane.
        public static double BallReachesReceiver() {
            var m = ServeToRally();
            for (int i = 0; i < 2400; i++) {
                m.Step(1.0 / 120);
                Assert.AreEqual("rally", m.State.phase, "The serve must stay in play for this experiment.");
                if (m.State.bounces >= 1 && m.State.ball.z >= 11.55f) return m.State.time;
            }
            Assert.Fail("The ball never reached the receiver.");
            return -1;
        }

        /// The player watches a picture `screen` seconds late and starts swinging on the cue they SEE, so the
        /// real swing starts `screen` after the ideal moment. `credit` = the phone adds its screen delay to the
        /// ages it sends (what SportsMultiplayer.Sample does). Returns the grade label, or "MISS" / "REJECTED".
        public static string Return(double screen, double network, bool credit, double confirmGap = .18, double claimed = -1) {
            double tBall = BallReachesReceiver();
            var m = ServeToRally();
            double claim = claimed >= 0 ? claimed : screen;
            double onset = tBall - TennisRules.SweetTime + screen;      // real time the swing starts
            double confirm = onset + confirmGap;                         // real time the stroke peaks and is confirmed

            // Phones stamp events when they hand them to the game: real time + the sensor delay.
            double beginStamp = onset + Sensor, swingStamp = confirm + Sensor;
            double beginArrives = beginStamp + network, swingArrives = swingStamp + network;

            RunTo(m, beginArrives);
            bool began = m.Input(1, Event(m, "beginSwing", 3, beginStamp, credit ? NetworkTuning.OnsetAge(claim) : 0), m.State.time);
            RunTo(m, swingArrives);
            long before = m.State.contact;
            bool confirmed = m.Input(1, Event(m, "swing", 4, swingStamp, credit ? NetworkTuning.SwingAge(Sensor, claim) : Sensor), m.State.time);
            for (int i = 0; i < 240 && m.State.contact == before && m.State.phase == "rally"; i++) m.Step(1.0 / 120);
            if (m.State.contact > before) return m.State.reason;
            return began || confirmed ? "MISS" : "REJECTED";
        }

        [Test] public void ScreenCreditIsClampedAndNeverNegative() {
            Assert.AreEqual(0, NetworkTuning.ScreenCredit(-1));
            Assert.AreEqual(0, NetworkTuning.ScreenCredit(double.NaN));
            Assert.AreEqual(.1, NetworkTuning.ScreenCredit(.1), 1e-9);
            Assert.AreEqual(NetworkTuning.MaxDisplayCredit, NetworkTuning.ScreenCredit(.9), 1e-9);
        }

        [Test] public void SwingAgeAddsScreenDelayToSensorAgeAndNeverExceedsWhatTheHostAccepts() {
            Assert.AreEqual(.23, NetworkTuning.SwingAge(.03, .2), 1e-9);
            Assert.AreEqual(.03, NetworkTuning.SwingAge(.03, 0), 1e-9, "No screen delay: same age as before the credit existed.");
            foreach (double sensor in new[] { -1, 0, .1, .25, 3, double.NaN })
                foreach (double screen in new[] { -1, 0, .1, .3, 2, double.NaN }) {
                    double age = NetworkTuning.SwingAge(sensor, screen);
                    Assert.That(age, Is.InRange(0, NetworkTuning.MaxInputAge));
                    Assert.True(Event(new NetworkTennisMatch(), "swing", 1, 0, age).Valid, $"sensor {sensor}, screen {screen}");
                }
            Assert.AreEqual(NetworkTuning.MaxDisplayCredit, NetworkTuning.OnsetAge(.9), 1e-9);
        }

        [Test] public void HostAcceptsAgesUpToTheLimitAndRejectsBeyondIt() {
            var m = new NetworkTennisMatch();
            Assert.True(Event(m, "swing", 1, 0, NetworkTuning.MaxInputAge).Valid);
            Assert.False(Event(m, "swing", 1, 0, NetworkTuning.MaxInputAge + .01).Valid);
        }

        [Test] public void ACreditedScreenDelayLetsAPerfectlyTimedSwingGradePerfect() {
            foreach (double screen in new[] { 0, .1, .2, .3 })
                foreach (double network in new[] { .02, .05, .1, .15 }) {
                    if (screen + network > ServeReturnBudget) continue;   // beyond what a serve return can absorb
                    Assert.AreEqual(Perfect, Return(screen, network, credit: true), $"screen {screen * 1000:F0} ms, network {network * 1000:F0} ms");
                }
        }

        [Test] public void BeyondTheBudgetANeverStartedSwingCannotBeSaved() {
            // 250 ms of screen + 150 ms of network: the swing start reaches the host after the ball is dead.
            Assert.AreNotEqual(Perfect, Return(.25, .15, credit: true));
            Assert.That(Return(.25, .15, credit: true), Is.EqualTo("MISS").Or.EqualTo("REJECTED"));
        }

        [Test] public void WithoutTheCreditASlowScreenCostsHitQuality() {
            Assert.AreEqual(Perfect, Return(0, .05, credit: false), "No screen delay: nothing to credit.");
            string slow = Return(.2, .05, credit: false);
            Assert.AreNotEqual(Perfect, slow, "A 200 ms screen delay makes a perfectly timed swing late.");
            Assert.AreEqual(Perfect, Return(.2, .05, credit: true), "Crediting the delay restores the grade.");
        }

        [Test] public void WhenTheSwingIsConfirmedDoesNotMoveWhereItStarted() {
            string baseline = Return(.1, .05, credit: true, confirmGap: .18);
            foreach (double gap in new[] { .08, .12, .25 })
                Assert.AreEqual(baseline, Return(.1, .05, credit: true, confirmGap: gap), $"confirmation {gap * 1000:F0} ms after the start");
        }

        [Test] public void ClaimingMoreScreenDelayThanTheCapGainsNothing() {
            // A phone that claims 600 ms is credited only MaxDisplayCredit, so it cannot get a free early swing.
            string honest = Return(.6, .05, credit: true, claimed: NetworkTuning.MaxDisplayCredit);
            Assert.AreEqual(honest, Return(.6, .05, credit: true, claimed: .6));
        }
    }
}
