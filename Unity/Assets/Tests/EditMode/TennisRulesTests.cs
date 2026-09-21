using GolfArcade.Tennis;
using NUnit.Framework;
using UnityEngine;

namespace GolfArcade.Tests
{
    public class TennisRulesTests
    {
        TennisHit Hit(float age = .32f, float offset = 0, float balance = 1, float reach = 1, float power = .8f, float stamina = 1)
            => TennisRules.Evaluate(age,new Vector2(offset,0),balance,reach,power,stamina);
        [Test] public void CenterAndTimingImproveTheHit()
        {
            Assert.Greater(Hit().Quality, Hit(offset:.10f).Quality);
            Assert.Greater(Hit().Quality, Hit(age:.19f).Quality);
            Assert.IsFalse(Hit(age:0).Contact);
            Assert.IsFalse(Hit(offset:.3f).Contact);
        }
        [Test] public void PositionAffectsPowerAndAccuracy()
        {
            Assert.Greater(Hit().Speed, Hit(balance:.3f,reach:.3f).Speed);
            Assert.Less(Hit().ErrorDegrees, Hit(balance:.3f,reach:.3f).ErrorDegrees);
        }
        [Test] public void SwingPowerDeterminesSpeed()
        { Assert.Greater(Hit(power:1).Speed, Hit(power:.2f).Speed); }
        [Test] public void FastRunningCostsMoreAndRestRecovers()
        {
            Assert.Less(TennisRules.StaminaStep(1,7,1), TennisRules.StaminaStep(1,3,1));
            Assert.Greater(TennisRules.StaminaStep(.4f,0,1),.4f);
            Assert.AreEqual(0,TennisRules.StaminaStep(.01f,7,10));
        }
        [Test] public void StaminaDrainIsTimeBased()
        {
            float a=1,b=1;
            for(int i=0;i<60;i++) a=TennisRules.StaminaStep(a,5,1f/60);
            for(int i=0;i<120;i++) b=TennisRules.StaminaStep(b,5,1f/120);
            Assert.That(a,Is.EqualTo(b).Within(.0001f));
        }
        [Test] public void SweptContactFindsFastBallsButRejectsMisses()
        {
            Assert.IsTrue(TennisRules.CrossStringBed(Vector3.forward*2,Vector3.back*2,Vector3.zero,Vector3.zero,Vector3.forward,Vector3.right,Vector3.up,out var offset));
            Assert.AreEqual(Vector2.zero,offset);
            Assert.IsFalse(TennisRules.CrossStringBed(new Vector3(1,0,2),new Vector3(1,0,-2),Vector3.zero,Vector3.zero,Vector3.forward,Vector3.right,Vector3.up,out _));
        }
    }
}
