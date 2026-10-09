using GolfArcade.Tennis;
using NUnit.Framework;
using UnityEngine;

namespace GolfArcade.Tests
{
    public class TennisAbilitiesTests
    {
        [TestCase(-4f)] [TestCase(4f)]
        public void CurveballPredictionMatchesFlightAndLandsInCourt(float curve)
        {
            Vector3 start = new(.8f, 1.2f, -9), target = new(2.5f, TennisRules.BallRadius, 10);
            var velocity = TennisAbilities.CurveVelocity(start, target, curve);
            Assert.IsTrue(TennisBall.Landing(start, velocity, 0, out var predicted, out _, curve));
            Assert.Less(Vector3.Distance(predicted, target), .02f);
            Assert.IsTrue(TennisRules.BounceIsIn(predicted));
            Assert.Greater(TennisRules.NetClearance(start, velocity, 0), TennisRules.NetHeight);
            // A straight prediction must disagree: this is actual lateral flight, not a trail effect.
            TennisBall.Landing(start, velocity, 0, out var straight, out _);
            Assert.Greater(Mathf.Abs(straight.x - predicted.x), 2f);
        }
        [Test] public void RescueLobHasTimeAndHeightToRecover()
        {
            Vector3 start = new(1, 1, -9), target = new(-2, TennisRules.BallRadius, 10);
            var velocity = TennisAbilities.LobVelocity(start, target, 6);
            Assert.IsTrue(TennisBall.Landing(start, velocity, 0, out var landing, out var duration));
            Assert.Greater(duration, 2f);
            Assert.Less(Vector3.Distance(landing, target), .02f);
            Assert.That(start.y + velocity.y * velocity.y / (2 * 9.81f), Is.EqualTo(6).Within(.01f));
        }
    }
}
