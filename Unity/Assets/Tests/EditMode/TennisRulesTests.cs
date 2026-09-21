using GolfArcade.Tennis;
using NUnit.Framework;
using UnityEngine;

namespace GolfArcade.Tests
{
    public class TennisRulesTests
    {
        [Test] public void PhoneFacingSelectsStrokeWithoutDependingOnCourtSide() {
            Assert.IsFalse(TennisRules.UseBackhand(1,true,false));
            Assert.IsTrue(TennisRules.UseBackhand(-1,false,false));
            Assert.IsTrue(TennisRules.UseBackhand(1,false,true));
            Assert.IsFalse(TennisRules.UseBackhand(-1,true,true));
        }
        [Test] public void ReachAssistAcceptsNearbySwingsButNotFarOrUntimedBalls()
        {
            Assert.IsTrue(TennisRules.AssistedContact(new Vector3(.7f,1,1),new Vector3(.7f,1,.5f),Vector3.zero,.18f,false,out _));
            Assert.IsFalse(TennisRules.AssistedContact(new Vector3(3,1,1),new Vector3(3,1,.5f),Vector3.zero,.18f,false,out _));
            Assert.IsFalse(TennisRules.AssistedContact(new Vector3(1,1,1),new Vector3(1,1,.5f),Vector3.zero,0,false,out _));
            Assert.IsTrue(TennisRules.AssistedContact(new Vector3(0,2.5f,1),new Vector3(0,2.5f,.5f),Vector3.zero,.18f,true,out _));
            Assert.IsFalse(TennisRules.AssistedContact(new Vector3(0,2.5f,1),new Vector3(0,2.5f,.5f),Vector3.zero,.18f,false,out _));
        }
        TennisHit Hit(float age = TennisRules.SweetTime, float offset = 0, float balance = 1, float reach = 1, float power = .8f, float stamina = 1)
            => TennisRules.Evaluate(age,new Vector2(offset,0),balance,reach,power,stamina);
        [Test] public void CenterAndTimingImproveTheHit()
        {
            Assert.Greater(Hit().Quality, Hit(offset:.10f).Quality);
            Assert.Greater(Hit().Quality, Hit(age:.10f).Quality);
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
        [Test] public void HardStrokesHaveSmallerContactWindowAndReach() {
            Assert.IsTrue(Hit(age:.29f,power:0).Contact);
            Assert.IsFalse(Hit(age:.29f,power:1).Contact);
            Vector3 ball=new Vector3(1,1.1f,.65f);
            Assert.IsTrue(TennisRules.AssistedContact(ball,ball,Vector3.zero,.18f,false,out _,0));
            Assert.IsFalse(TennisRules.AssistedContact(ball,ball,Vector3.zero,.18f,false,out _,1));
        }
        [Test] public void ShotAimingLandsLeftAndRightWithoutWeakLobs() {
            Vector3 start=new Vector3(0,1,-10.5f);
            foreach(float power in new[]{0f,.5f,1f}) foreach(float aim in new[]{-1f,0f,1f}) {
                Vector3 target=TennisRules.ShotTarget(aim,power);
                Vector3 velocity=TennisRules.ShotVelocity(start,target,Hit(power:power).Speed);
                float t=(target.z-start.z)/velocity.z;
                Vector3 end=start+velocity*t+Vector3.down*(4.905f*t*t);
                Assert.Less(Vector3.Distance(end,target),.001f);
                Assert.Less(t,1.4f,"Returns must not float for several seconds");
                Assert.Less(start.y+velocity.y*velocity.y/19.62f,3.5f);
            }
        }
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
        [Test] public void ArcadeStrokeIsFastAndContactPhaseStaysAligned()
        {
            Assert.Less(TennisRules.StrokeDuration,.5f);
            Assert.That(TennisRules.StrokePhase(TennisRules.SweetTime),Is.EqualTo(.5f).Within(.0001f));
            Assert.AreEqual(0,TennisRules.StrokePhase(0));
            Assert.AreEqual(1,TennisRules.StrokePhase(TennisRules.StrokeDuration));
            float previous=0;
            for(int i=0;i<=100;i++) { float phase=TennisRules.StrokePhase(i*TennisRules.StrokeDuration/100); Assert.GreaterOrEqual(phase,previous); previous=phase; }
            Assert.Greater(TennisRules.SprintSpeed,TennisRules.RunSpeed);
        }
        [Test] public void SweptContactFindsFastBallsButRejectsMisses()
        {
            Assert.IsTrue(TennisRules.CrossStringBed(Vector3.forward*2,Vector3.back*2,Vector3.zero,Vector3.zero,Vector3.forward,Vector3.right,Vector3.up,out var offset));
            Assert.AreEqual(Vector2.zero,offset);
            Assert.IsFalse(TennisRules.CrossStringBed(new Vector3(1,0,2),new Vector3(1,0,-2),Vector3.zero,Vector3.zero,Vector3.forward,Vector3.right,Vector3.up,out _));
        }
    }
}
