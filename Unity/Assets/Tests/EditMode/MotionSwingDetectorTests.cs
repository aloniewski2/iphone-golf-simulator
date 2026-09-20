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
        static List<SwingEvent> Drive(MotionSwingDetector detector, (double seconds, double angle)[] legs, double faceRollDegrees = 0, int rollFromLeg = int.MaxValue)
        {
            var events = new List<SwingEvent>();
            double time = 0, angle = 0, roll = 0;
            var axis = Vector3.UnitX;
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
                    var twist = Quaternion.CreateFromAxisAngle(Vector3.UnitY, (float)roll);
                    var attitude = Quaternion.Normalize(twist * swing);
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
        public void FullSwingLoadsThenImpactsWithSpeedBasedPower()
        {
            var detector = new MotionSwingDetector { FullSpeed = 14 };
            var events = Drive(detector, FullSwing);
            var loads = events.Where(e => e.Kind == SwingEventKind.Load).Select(e => e.Load).ToList();
            Assert.Greater(loads.Count, 10);
            Assert.AreEqual(0.9, loads.Max(), 0.05);
            var impacts = Impacts(events);
            Assert.AreEqual(1, impacts.Count);
            Assert.AreEqual(8.8 / 14, impacts[0].Power, 0.05);
            Assert.AreEqual(0.9, impacts[0].Backswing, 0.05);
            Assert.IsFalse(events.Any(e => e.Kind == SwingEventKind.Cancel));
            Assert.AreEqual(SwingPhase.Address, detector.Phase, "the finish position becomes the new address");
        }

        [Test]
        public void FasterSwingHitsHarderAndClampsAtFullPower()
        {
            double slow = Impacts(Drive(new MotionSwingDetector(), new[] { (0.6, 0.0), (0.8, 1.8), (0.2, 1.8), (0.5, -0.4), (0.6, -0.4) }))[0].Power;
            double fast = Impacts(Drive(new MotionSwingDetector(), new[] { (0.6, 0.0), (0.8, 1.8), (0.2, 1.8), (0.1, -0.4), (0.6, -0.4) }))[0].Power;
            Assert.Less(slow, fast);
            Assert.AreEqual(1, fast, "22 rad/s exceeds full speed");
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

        [Test]
        public void OverswingGoesWild()
        {
            var detector = new MotionSwingDetector { FullSpeed = 10 };
            var impact = Impacts(Drive(detector, new[] { (0.6, 0.0), (0.8, 1.8), (0.2, 1.8), (0.1, -0.4), (0.6, -0.4) }))[0];
            Assert.Greater(impact.Overswing, 0.5, "22 rad/s against a 10 rad/s club");
            Assert.AreEqual(1, impact.Power);
            Assert.Greater(Math.Abs(impact.CurveDegrees), 5);
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
