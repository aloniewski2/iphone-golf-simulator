using GolfArcade.Game;
using NUnit.Framework;
using UnityEngine;

namespace GolfArcade.Tests
{
    public class StandardArmTests
    {
        [TestCase(.12f, -.30f, .15f)]
        [TestCase(-.20f, .12f, .31f)]
        [TestCase(.04f, -.50f, .07f)]
        public void ReachableTargetsKeepBothArmLengths(float x, float y, float z)
        {
            Vector3 wrist = new Vector3(x,y,z);
            Vector3 elbow = StandardCharacterArms.SolveElbow(Vector3.zero, wrist, new Vector3(1,-1,-.4f), StandardCharacterArms.UpperLengthMetres, StandardCharacterArms.ForearmLengthMetres);
            Assert.That(elbow.magnitude, Is.EqualTo(StandardCharacterArms.UpperLengthMetres).Within(.0001f));
            Assert.That(Vector3.Distance(elbow, wrist), Is.EqualTo(StandardCharacterArms.ForearmLengthMetres).Within(.0001f));
        }

        [Test]
        public void DegenerateAndExtendedTargetsStayFinite()
        {
            foreach (var wrist in new[] {Vector3.zero, Vector3.up, Vector3.down*.4f})
            {
                var elbow = StandardCharacterArms.SolveElbow(Vector3.zero, wrist, Vector3.down, .285f,.265f);
                Assert.IsFalse(float.IsNaN(elbow.x) || float.IsNaN(elbow.y) || float.IsNaN(elbow.z));
            }
        }
    }
}
