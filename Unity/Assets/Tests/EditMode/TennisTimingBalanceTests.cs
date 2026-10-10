using GolfArcade.Tennis;
using GolfArcade.Multiplayer;
using NUnit.Framework;
using UnityEngine;

namespace GolfArcade.Tests {
 public class TennisTimingBalanceTests {
  [Test] public void WiderContactStillNeedsASwingWithinReach() {
   foreach(float side in new[]{-1f,1f}) {
    Vector3 b=new Vector3(side*1.72f,1.1f,.65f);
    Assert.IsTrue(TennisRules.AssistedContact(b,b,Vector3.zero,.025f,false,out _));
    Assert.IsTrue(TennisRules.AssistedContact(b,b,Vector3.zero,.48f,false,out _));
    Assert.IsFalse(TennisRules.AssistedContact(b,b,Vector3.zero,0,false,out _));
    Assert.IsFalse(TennisRules.AssistedContact(b,b,Vector3.zero,.55f,false,out _));
    b.x=side*2.5f;
    Assert.IsFalse(TennisRules.AssistedContact(b,b,Vector3.zero,.18f,false,out _));
   }
  }
  [Test] public void CleanTimingWinsPaceAndPlacementWithoutUnreturnableStreakSpeed() {
   var clean=TennisRules.AssistedHit(.02f,.65f,1,.2f,1);
   var scrappy=TennisRules.AssistedHit(.25f,.65f,1,1,1);
   Assert.Greater(clean.Speed,scrappy.Speed*1.4f);
   Assert.LessOrEqual(TennisRules.Supercharge(TennisRules.Evaluate(.18f,Vector2.zero,1,1,1,1)).Speed,34f);
   foreach(float aim in new[]{-1f,1f}) foreach(float depth in new[]{0f,1f}) {
    var intended=TennisRules.PlacementTarget(aim,depth);
    float cleanError=0,poorError=0;
    foreach(float x in new[]{-1f,0f,1f}) foreach(float z in new[]{-1f,0f,1f}) {
     var precise=TennisRules.TimedPlacement(intended,clean.Timing,x,z);
     var loose=TennisRules.TimedPlacement(intended,scrappy.Timing,x,z);
     cleanError+=Vector3.Distance(intended,precise);poorError+=Vector3.Distance(intended,loose);
     Assert.Less(Vector3.Distance(intended,precise),.08f);
     Assert.IsTrue(TennisRules.BounceIsIn(loose));
    }
    Assert.Greater(poorError,cleanError*5);
   }
  }
  [Test] public void TimingAndPaceFallOffSmoothlyOnBothSides() {
   float previous=100;
   for(float late=0;late<.34f;late+=.005f) {
    var a=TennisRules.AssistedHit(late,.65f,1,.65f,1);
    var b=TennisRules.AssistedHit(-late,.65f,1,.65f,1);
    Assert.AreEqual(a.Speed,b.Speed);Assert.LessOrEqual(a.Speed,previous);previous=a.Speed;
    Assert.Greater(a.Timing,0);
   }
  }
  [Test] public void FastestNormalRallyCanBeReachedFromAReadyPosition() {
   foreach(float aim in new[]{-1f,0f,1f}) {
    var start=new Vector3(0,1.1f,-10.5f);
    var target=TennisRules.PlacementTarget(aim,.85f);
    var velocity=TennisRules.RallyVelocity(start,target,34f,.3f,1f);
    var mirroredStart=new Vector3(-start.x,start.y,-start.z);
    var mirroredVelocity=new Vector3(-velocity.x,velocity.y,-velocity.z);
    var plan=TennisRules.PlanIntercept(mirroredStart,mirroredVelocity,.3f,.75f,
     new Vector2(0,TennisRules.BaselineZ),TennisRules.ReactionTime,TennisRules.SprintSpeed,false);
    Assert.IsTrue(plan.Reachable,"a ready receiver must have a reachable return even at the pace cap");
    Assert.Greater(plan.Time,.65f,"retain reaction time across the full court");
   }
  }
  // Landing is returned in the hitter's own frame (x to their right, z away from them), so both seats read alike.
  static (float speed,Vector2 land,string grade) OnlineShot(int seat,float due,float power,float delay=0) {
   var match=new NetworkTennisMatch();var s=match.State;var p=s.players[seat];float sign=seat==0?1:-1;
   s.phase="rally";s.receiver=seat;p.x=0;p.target=0;p.aim=.9f;p.depth=.8f;
   s.ball=new NetworkVector(0,1.5f,p.z+sign*(.65f+16*due));s.velocity=new NetworkVector(0,1,-16*sign);
   for(int i=0;i<Mathf.RoundToInt(delay*120);i++)match.Step(1.0/120);
   long contact=s.contact;
   Assert.IsTrue(match.Input(seat,new NetworkInput {action="swing",eventID=1,point=s.point,contact=s.contact,time=0,power=power,aim=.9f},s.time));
   for(int i=0;i<65&&s.contact==contact;i++)match.Step(1.0/120);
   Assert.Greater(s.contact,contact);
   var b=s.ball;var v=s.velocity;
   float flight=(v.y+Mathf.Sqrt(v.y*v.y+19.62f*(b.y-.034f)))/9.81f;
   var land=new Vector2((b.x+v.x*flight)*sign,(b.z+v.z*flight)*sign);
   return (Mathf.Sqrt(v.x*v.x+v.z*v.z),land,s.reason);
  }
  [Test] public void OnlinePacketDelayDoesNotChangeTheTimingReward() {
   foreach(int seat in new[]{0,1}) foreach(float due in new[]{.18f,-.03f}) {
    var immediate=OnlineShot(seat,due,.65f);
    var delayed=OnlineShot(seat,due,.65f,.1f);
    Assert.AreEqual(immediate.grade,delayed.grade);
    Assert.That(Vector2.Distance(delayed.land,immediate.land),Is.LessThan(.15f));
   }
  }
  [Test] public void OnlineTimingRewardsBothSeatsEqually() {
   foreach(int seat in new[]{0,1}) {
    var clean=OnlineShot(seat,.18f,.2f);var poor=OnlineShot(seat,-.03f,1);
    Assert.AreEqual("PERFECT!",clean.grade);
    Assert.AreNotEqual("PERFECT!",poor.grade);
    Assert.Greater(clean.speed,poor.speed);
   }
  }
  [Test] public void OnlineTimingAimsTheBallForBothSeats() {
   // A swing on the ball goes straight, to the depth the swing's power picks; one that meets the ball late goes to the
   // hitter's right, one that meets it early to their left. The same for the seat at either end of the court.
   foreach(int seat in new[]{0,1}) {
    var straight=OnlineShot(seat,.18f,.2f);
    Assert.That(Mathf.Abs(straight.land.x),Is.LessThan(.15f),$"seat {seat} on the ball goes straight");
    Assert.That(straight.land.y,Is.EqualTo(TennisRules.PlacementTarget(0,TennisRules.DepthFromPower(.2f)).z).Within(.2f),$"seat {seat} depth follows power");
    var late=OnlineShot(seat,-.03f,.2f);
    Assert.Greater(late.land.x,2f,$"seat {seat} a late swing goes to the hitter's right");
    var power=OnlineShot(seat,.18f,1f);
    Assert.Greater(power.land.y,straight.land.y+1.5f,$"seat {seat} a full swing lands deeper than a soft one");
    // ...and the ball is always inside the lines.
    foreach(var shot in new[]{straight,late,power}) Assert.IsTrue(TennisRules.BounceIsIn(new Vector3(shot.land.x,TennisRules.BallRadius,shot.land.y)));
   }
  }
 }
}
