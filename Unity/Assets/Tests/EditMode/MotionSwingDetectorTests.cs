using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using GolfArcade.Shot;
using GolfArcade.Swing;
using NUnit.Framework;

namespace GolfArcade.Tests
{
    public class MotionSwingDetectorTests
    {
        /// Rotates a phone about one axis through (duration, target angle) legs at 100 Hz, with
        /// an optional wrist roll about the phone's long axis ramped in over the downswing.
        /// The phone hanging like a club at address: long axis (device Y) straight down in the Z-up frame.
        static readonly Quaternion ClubDown = Quaternion.CreateFromAxisAngle(Vector3.UnitX, (float)(-Math.PI / 2));
        /// The phone held flat, screen up, the way you would read it — not a club.
        static readonly Quaternion Flat = Quaternion.Identity;

        static List<SwingEvent> Drive(MotionSwingDetector detector, (double seconds, double angle)[] legs, double faceRollDegrees = 0, int rollFromLeg = int.MaxValue, Quaternion? held = null)
        {
            var events = new List<SwingEvent>();
            double time = 0, angle = 0, roll = 0;
            var axis = Vector3.UnitX;
            var rest = held ?? ClubDown;
            for (int legIndex = 0; legIndex < legs.Length; legIndex++)
            {
                var leg = legs[legIndex];
                int steps = Math.Max(1, (int)(leg.seconds * 100));
                double delta = (leg.angle - angle) / steps;
                double rollTarget = legIndex >= rollFromLeg ? faceRollDegrees * Math.PI / 180 : 0;
                for (int i = 0; i < steps; i++)
                {
                    time += 0.01;
                    angle += delta;
                    roll += (rollTarget - roll) * 0.2;
                    var swing = Quaternion.CreateFromAxisAngle(axis, (float)angle);
                    var shaft = Vector3.Transform(Vector3.UnitY, rest);
                    var twist = Quaternion.CreateFromAxisAngle(shaft, (float)roll);
                    var attitude = Quaternion.Normalize(twist * swing * rest);
                    var rate = new Vector3((float)(delta / 0.01), 0, 0);
                    var e = detector.Ingest(time, attitude, rate);
                    if (e != null) events.Add(e.Value);
                }
            }
            return events;
        }

        static List<SwingImpact> Impacts(List<SwingEvent> events) => events.Where(e => e.Kind == SwingEventKind.Impact).Select(e => e.Impact).ToList();

        static readonly (double, double)[] FullSwing =
        {
            (0.6, 0),      // hold still at address
            (0.8, 1.8),    // backswing
            (0.2, 1.8),    // pause at the top
            (0.25, -0.4),  // downswing through address: 8.8 rad/s
            (0.6, -0.4),   // hold the finish
        };

        [Test]
        public void FullSwingLoadsThenImpactsWithTheBackswingsPower()
        {
            var detector = new MotionSwingDetector { FullSpeed = 14 };
            var events = Drive(detector, FullSwing);
            var loads = events.Where(e => e.Kind == SwingEventKind.Load).Select(e => e.Load).ToList();
            Assert.Greater(loads.Count, 10);
            double load = 1.8 / detector.FullBackswing;
            Assert.AreEqual(load, loads.Max(), 0.05);
            var impacts = Impacts(events);
            Assert.AreEqual(1, impacts.Count);
            // 8.8 rad/s is a committed downswing (over half of 14): the shot is the backswing's
            Assert.AreEqual(load, impacts[0].Power, 0.05);
            Assert.AreEqual(load, impacts[0].Backswing, 0.05);
            Assert.IsFalse(events.Any(e => e.Kind == SwingEventKind.Cancel));
            Assert.AreEqual(SwingPhase.Address, detector.Phase, "the finish position becomes the new address");
        }

        /// The downswing only has to be committed: past half the club's full speed, swinging
        /// harder adds nothing; a lazy push below that scales the shot down.
        [Test]
        public void ACommittedDownswingIsWorthTheWholeBackswing()
        {
            double load = 1.8 / new MotionSwingDetector().FullBackswing;
            double lazy = Impacts(Drive(new MotionSwingDetector(), new[] { (0.6, 0.0), (0.8, 1.8), (0.2, 1.8), (0.5, -0.4), (0.6, -0.4) }))[0].Power;
            double firm = Impacts(Drive(new MotionSwingDetector(), new[] { (0.6, 0.0), (0.8, 1.8), (0.2, 1.8), (0.25, -0.4), (0.6, -0.4) }))[0].Power;
            double hard = Impacts(Drive(new MotionSwingDetector(), new[] { (0.6, 0.0), (0.8, 1.8), (0.2, 1.8), (0.1, -0.4), (0.6, -0.4) }))[0].Power;
            Assert.AreEqual(load, firm, 0.03, "8.8 rad/s is committed: the whole backswing");
            Assert.AreEqual(firm, hard, 1e-9, "22 rad/s adds nothing: the backswing is the shot");
            Assert.Less(lazy, firm * 0.8, "4.4 rad/s is a push, not a swing");
        }

        /// The feel the user asked for: a full swing is the whole club, half a swing is half.
        [Test]
        public void TheBackswingDecidesTheDistance()
        {
            foreach (var (back, share) in new[] { (2.6, 1.0), (2.5, 1.0), (1.3, 0.5), (0.65, 0.25) })   // (2.5: a few degrees short is still full)
            {
                var impact = Impacts(Drive(new MotionSwingDetector { FullSpeed = 12 }, new[] { (0.6, 0.0), (0.8, back), (0.2, back), (0.12, -0.4), (0.6, -0.4) }))[0];
                Assert.AreEqual(share, impact.Power, 0.03, $"a {share:P0} backswing is {share:P0} of the club");
                Assert.AreEqual(0, impact.CurveDegrees, 1e-6, "and swinging hard never sends it wild");
            }
        }

        [Test]
        public void SquareFaceFliesStraight()
        {
            var impact = Impacts(Drive(new MotionSwingDetector(), FullSwing))[0];
            Assert.AreEqual(0, impact.CurveDegrees, 1e-6);
            Assert.AreEqual(0, impact.StartLineDegrees, 1e-6);
            Assert.AreEqual(0, impact.FaceDegrees, 1.0);
        }

        [Test]
        public void RolledWristCurvesTheBall()
        {
            var open = Impacts(Drive(new MotionSwingDetector(), FullSwing, faceRollDegrees: 30, rollFromLeg: 3))[0];
            var closed = Impacts(Drive(new MotionSwingDetector(), FullSwing, faceRollDegrees: -30, rollFromLeg: 3))[0];
            Assert.Greater(Math.Abs(open.FaceDegrees), 15, "the roll reaches the face reading");
            Assert.AreNotEqual(0, open.CurveDegrees);
            Assert.AreEqual(-Math.Sign(open.CurveDegrees), Math.Sign(closed.CurveDegrees), "rolling the other way curves the other way");
            Assert.AreEqual(Math.Sign(open.CurveDegrees), Math.Sign(open.StartLineDegrees), "the start line follows the face");
            Assert.LessOrEqual(Math.Abs(open.CurveDegrees), 15);
        }

        /// A full, hard swing is the club's full distance: never more, never wild.
        [Test]
        public void HardestSwingIsTheFullClubAndStraight()
        {
            foreach (var club in new[] { GolfClub.Driver, GolfClub.Iron, GolfClub.Wedge })
            {
                var detector = new MotionSwingDetector();
                detector.Configure(club);
                // a full turn lashed down at 30 rad/s, several times any club's full speed
                var impact = Impacts(Drive(detector, new[] { (0.6, 0.0), (0.8, 2.6), (0.2, 2.6), (0.1, -0.4), (0.6, -0.4) }))[0];
                Assert.AreEqual(1, impact.Power, 1e-6, $"{club}: the hardest swing is the whole club");
                Assert.AreEqual(0, impact.CurveDegrees, 1e-6, $"{club}: with a square face it flies straight");
                Assert.AreEqual(club.ReferenceDistanceYards(), BallFlight.Simulate(club.Launch(impact.Power, impact.StartLineDegrees, impact.CurveDegrees)).Carry, 1.5,
                                $"{club}: and carries the club's full distance");
            }
        }

        /// Taken all the way back, the phone passes pointing straight up and comes round towards
        /// address again while still going back; that is not the downswing.
        [Test]
        public void BackswingPastVerticalWaitsForTheRealDownswing()
        {
            var detector = new MotionSwingDetector();
            // 230° back (past straight up), a pause, then down hard through address
            var events = Drive(detector, new[] { (0.6, 0.0), (0.9, 4.0), (0.25, 4.0), (0.2, -0.4), (0.6, -0.4) });
            var impacts = Impacts(events);
            Assert.AreEqual(1, impacts.Count);
            Assert.IsFalse(events.Any(e => e.Kind == SwingEventKind.Cancel));
            Assert.AreEqual(1, impacts[0].Backswing, 1e-9, "past vertical is a full backswing");
            Assert.Greater(impacts[0].PeakSpeed, 18, "the speed is the downswing's, not the backswing's");
            Assert.AreEqual(1, impacts[0].Power, 1e-6);
        }

        [Test]
        public void SlowWaggleCancelsWithoutImpact()
        {
            var detector = new MotionSwingDetector();
            var events = Drive(detector, new[] { (0.6, 0.0), (1.0, 0.7), (1.0, 0.05), (0.6, 0.05) });
            Assert.IsEmpty(Impacts(events));
            Assert.AreEqual(SwingEventKind.Cancel, events.Last().Kind);
            Assert.AreEqual(SwingPhase.Address, detector.Phase);
        }

        [Test]
        public void NothingHappensUntilThePhoneSettles()
        {
            var detector = new MotionSwingDetector();
            var events = Drive(detector, new[] { (0.5, 2.0), (0.5, 0.0), (0.5, 2.0) });
            Assert.IsEmpty(events);
            Assert.AreEqual(SwingPhase.Settling, detector.Phase);
        }

        [Test]
        public void NoPowerUnlessThePhoneHangsLikeAClub()
        {
            var detector = new MotionSwingDetector();
            var flat = Drive(detector, new[] { (0.6, 0.0), (0.8, 0.9), (0.2, 0.9), (0.15, -0.2), (0.6, -0.2) }, held: Flat);
            Assert.IsEmpty(flat, "a phone held flat never arms, so a swing from there draws nothing");
            Assert.AreEqual(SwingPhase.Settling, detector.Phase);
            Assert.IsFalse(detector.PointedDown);
            Assert.Greater(detector.LeanDegrees, 35, "the last sample, at the finish, is still nowhere near vertical");

            var leaning = Quaternion.CreateFromAxisAngle(Vector3.UnitX, (float)(-Math.PI / 2 + 25 * Math.PI / 180));
            var events = Drive(new MotionSwingDetector(), FullSwing, held: leaning);
            Assert.AreEqual(1, Impacts(events).Count, "a shaft leaning 25° from vertical is still a club at address");

            var tooFar = Quaternion.CreateFromAxisAngle(Vector3.UnitX, (float)(-Math.PI / 2 + 50 * Math.PI / 180));
            // (no pause at the top: without gravity the fallback cannot tell a phone hanging from one pointing up)
            Assert.IsEmpty(Drive(new MotionSwingDetector(), new[] { (0.6, 0.0), (0.8, 1.8), (0.25, -0.4), (0.6, -0.4) }, held: tooFar), "50° off vertical is not addressing the ball");
        }

        [Test]
        public void GravityInThePhoneFrameDecidesTheClubPosition()
        {
            // With gravity known, the attitude frame does not matter: held flat by the attitude,
            // but gravity says the phone hangs along its length → armed.
            var detector = new MotionSwingDetector();
            double t = 0;
            for (int i = 0; i < 60; i++) detector.Ingest(t += 0.01, Flat, Vector3.Zero, Vector3.UnitY);
            Assert.AreEqual(SwingPhase.Address, detector.Phase);
            Assert.AreEqual(0, detector.LeanDegrees, 0.1);

            var lying = new MotionSwingDetector();
            for (int i = 0; i < 60; i++) lying.Ingest(t += 0.01, ClubDown, Vector3.Zero, -Vector3.UnitZ);
            Assert.AreEqual(SwingPhase.Settling, lying.Phase, "screen-up on a table is not a club, whatever the attitude says");
            Assert.AreEqual(90, lying.LeanDegrees, 0.1);

            var leaning = new MotionSwingDetector();
            var g = Vector3.Normalize(new Vector3(0, (float)Math.Cos(30 * Math.PI / 180), (float)Math.Sin(30 * Math.PI / 180)));
            for (int i = 0; i < 60; i++) leaning.Ingest(t += 0.01, Flat, Vector3.Zero, g);
            Assert.AreEqual(SwingPhase.Address, leaning.Phase, "30° of shaft lean is a normal address");
        }

        [Test]
        public void ControllerPacketCarriesGravityAndStillReadsVersionOne()
        {
            var state = new GolfArcade.Net.ControllerState { Sequence = 7, Time = 1.5, Attitude = ClubDown, RotationRate = new Vector3(1, 2, 3), Gravity = Vector3.UnitY, Buttons = GolfArcade.Net.ControllerButtons.AimLeft, HasGyro = true };
            var bytes = GolfArcade.Net.ControllerProtocol.EncodeState(state);
            Assert.AreEqual(GolfArcade.Net.ControllerProtocol.StateLength, bytes.Length);
            Assert.IsTrue(GolfArcade.Net.ControllerProtocol.TryDecodeState(bytes, bytes.Length, out var back));
            Assert.AreEqual(Vector3.UnitY, back.Gravity);
            Assert.AreEqual(state.Buttons, back.Buttons);
            // an old controller: version 1, no gravity
            var v1 = new byte[GolfArcade.Net.ControllerProtocol.StateLengthV1];
            Array.Copy(bytes, v1, v1.Length); v1[4] = 1;
            Assert.IsTrue(GolfArcade.Net.ControllerProtocol.TryDecodeState(v1, v1.Length, out var old));
            Assert.AreEqual(Vector3.Zero, old.Gravity);
            Assert.AreEqual(state.RotationRate, old.RotationRate);
        }

        [Test]
        public void LowerBackswingMeansProportionallyLessPowerAtTheSameSpeed()
        {
            // The same 8.8 rad/s downswing from a third of a backswing and a full one.
            var shortBack = Impacts(Drive(new MotionSwingDetector(), new[] { (0.6, 0.0), (0.4, 0.9), (0.2, 0.9), (0.125, -0.2), (0.6, -0.2) }))[0];
            var longBack = Impacts(Drive(new MotionSwingDetector(), new[] { (0.6, 0.0), (0.8, 2.6), (0.2, 2.6), (0.33, -0.4), (0.6, -0.4) }))[0];
            Assert.AreEqual(shortBack.PeakSpeed, longBack.PeakSpeed, 0.3);
            Assert.AreEqual(1, longBack.Power, 0.02, "a full backswing swung through is the whole club");
            Assert.AreEqual(shortBack.Backswing, shortBack.Power, 0.02, "a third of a backswing is a third of the club");
            // and a full backswing let down lazily (4.3 rad/s, under half of 14) is short of it
            var lazy = Impacts(Drive(new MotionSwingDetector(), new[] { (0.6, 0.0), (0.8, 2.6), (0.2, 2.6), (0.7, -0.4), (0.6, -0.4) }))[0];
            Assert.Less(lazy.Power, 0.75);
        }

        [Test]
        public void ArmsWheneverThePhoneHangsLikeAClubEvenWhileMoving()
        {
            // Hanging like a club but never dead still (a slow waggle under ArmSpeed): still arms.
            var detector = new MotionSwingDetector();
            double t = 0;
            for (int i = 0; i < 60; i++)
                detector.Ingest(t += 0.01, Quaternion.CreateFromAxisAngle(Vector3.UnitX, (float)(-Math.PI / 2 + 0.05 * Math.Sin(i * 0.3))), new Vector3(0.9f, 0, 0), Vector3.UnitY);
            Assert.AreEqual(SwingPhase.Address, detector.Phase);
            // A swing right after the finish re-arms without a pause, as long as it hangs down again.
            var again = new MotionSwingDetector();
            var events = Drive(again, new[] { (0.6, 0.0), (0.8, 1.8), (0.2, 1.8), (0.25, -0.4), (0.3, 0.0), (0.15, 0.0), (0.8, 1.8), (0.2, 1.8), (0.25, -0.4), (0.4, -0.4) });
            Assert.AreEqual(2, Impacts(events).Count, "two swings with only a beat between them");

            // Upside down (top edge up, as when you read it) is vertical but not a club.
            var upsideDown = new MotionSwingDetector();
            for (int i = 0; i < 60; i++) upsideDown.Ingest(t += 0.01, Flat, Vector3.Zero, -Vector3.UnitY);
            Assert.AreEqual(SwingPhase.Settling, upsideDown.Phase);
            Assert.IsTrue(upsideDown.WrongEndDown);
        }

        [Test]
        public void AngleBetweenOrientations()
        {
            var a = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.3f);
            var b = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 1.1f);
            Assert.AreEqual(0.8, MotionSwingDetector.AngleBetween(a, b), 1e-5);
            Assert.AreEqual(0, MotionSwingDetector.AngleBetween(a, a), 1e-6);
        }

        [Test]
        public void GentlePhonePuttUsesClubSpecificThresholds()
        {
            var detector = new MotionSwingDetector();
            detector.Configure(GolfClub.Putter);
            var events = Drive(detector, new[] { (0.6, 0.0), (0.8, 0.14), (0.15, 0.14), (0.8, -0.04) });
            var impacts = Impacts(events);
            Assert.AreEqual(1, impacts.Count);
            Assert.Less(impacts[0].Power, 0.15);
        }

        [Test]
        public void HeldBackswingTimesOut()
        {
            var events = Drive(new MotionSwingDetector(), new[] { (0.6, 0.0), (0.8, 1.8), (3.5, 1.8) });
            Assert.IsEmpty(Impacts(events));
            Assert.IsTrue(events.Any(e => e.Kind == SwingEventKind.Cancel));
        }
    }
}
