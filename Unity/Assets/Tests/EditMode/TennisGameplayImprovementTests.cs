using NUnit.Framework;
using UnityEngine;
using GolfArcade.Tennis;

namespace GolfArcade.Tests
{
    public class TennisGameplayImprovementTests
    {
        [Test] public void PerfectServeUsesTwentyFiveRealMillisecondsAndStrictToss()
        {
            float boundary = .025f * TennisRules.ServePace;
            foreach (float sign in new[] {-1f, 1f}) {
                Assert.IsTrue(TennisRules.JudgeServeStrike(sign * boundary, .96f, new Vector2(0,.8f), true,true,false,.5f,.5f).Perfect);
                Assert.IsFalse(TennisRules.JudgeServeStrike(sign * (boundary+.0001f), .96f, new Vector2(0,.8f), true,true,false,.5f,.5f).Perfect);
            }
            Assert.IsFalse(TennisRules.JudgeServeStrike(0,.94f,new Vector2(0,.8f),true,true,false,.5f,.5f).Perfect);
            Assert.IsTrue(TennisRules.JudgeServeStrike(boundary+.005f,.96f,new Vector2(0,.8f),true,true,false,.5f,.5f).Legal);
        }

        [Test] public void PerfectServePaceSurvivesNetClearanceAcrossTheServiceBox()
        {
            foreach (bool deuce in new[] {true,false}) foreach (float x in new[] {-1f,0,1}) foreach (float depth in new[] {0f,.5f,1f})
            foreach (bool second in new[] {true,false}) {
                Vector3 start=new Vector3(TennisRules.ServerStanceX(true,deuce),2.45f,-TennisRules.ServeDepth);
                Vector3 target=TennisRules.ServeAimPoint(new Vector2(x,depth),true,deuce);
                float normal=42*(second?.82f:1), perfect=53*(second?.82f:1);
                var fast=TennisRules.PacedServe(start,target,perfect,second?.75f:.12f);
                var good=TennisRules.PacedServe(start,target,normal,second?.75f:.12f);
                Assert.That(new Vector2(fast.Velocity.x,fast.Velocity.z).magnitude,Is.EqualTo(perfect).Within(.001f));
                Assert.Greater(TennisRules.NetClearance(start,fast.Velocity,fast.Spin),TennisRules.NetHeight+TennisRules.NetMargin);
                Assert.IsTrue(TennisBall.Landing(start,fast.Velocity,fast.Spin,out var landing,out var time));
                Assert.Less(Vector3.Distance(landing,target),.015f);
                Assert.IsTrue(TennisRules.ServeIsIn(landing,true,deuce));
                TennisBall.Landing(start,good.Velocity,good.Spin,out _,out var goodTime);
                Assert.Less(time/goodTime,.81f);
                var bounce=fast.Velocity;var at=start;TennisBall.Integrate(ref at,ref bounce,fast.Spin,time);
                float spin=fast.Spin; TennisBall.Bounce(ref bounce,ref spin,.6f);
                Assert.LessOrEqual(Mathf.Abs(spin),.45f,"The temporary serve downforce ends at the bounce");
                float postGravity=9.81f+TennisBall.Magnus*spin*new Vector2(bounce.x,bounce.z).magnitude;
                float postApex=TennisRules.BallRadius+bounce.y*bounce.y/(2*postGravity);
                Assert.Less(postApex,TennisRules.ReachOverhead,"The faster serve must not manufacture a smashable rebound");
            }
        }

        [Test] public void OrdinaryServeReturnsAreNotLobsAndLobsCannotRepeat()
        {
            foreach (float skill in new[] {0f,.45f,1f}) for (int i=0;i<200;i++) {
                var p=OpponentProfile.FromDifficulty(skill);
                var d=TennisOpponent.Decide(0,0,0,0,0,p,1,i/200f,(i*71%200)/200f,.8f,p.Reach,0,.6f,true,-11.2f,false);
                Assert.AreNotEqual(TennisReturnKind.Lob,d.Kind);
                var repeat=TennisOpponent.Decide(0,0,0,0,0,p,1,i/200f,(i*71%200)/200f,.2f,p.Reach,0,.4f,false,-3,true);
                Assert.AreNotEqual(TennisReturnKind.Lob,repeat.Kind);
            }
        }

        [Test] public void NormalReturnArcsStayBelowOverheadHeight()
        {
            var start=new Vector3(0,1.15f,10.6f);
            for(int i=0;i<200;i++) {
                var p=OpponentProfile.FromDifficulty(.45f);
                var d=TennisOpponent.Decide(0,0,0,0,0,p,1,i/200f,(i*71%200)/200f,.8f,p.Reach,0,.6f,true,-11.2f,false);
                var v=TennisRules.RallyArcVelocity(start,d.Landing,d.Speed,d.Spin,.28f);
                float gravity=9.81f+TennisBall.Magnus*d.Spin*new Vector2(v.x,v.z).magnitude;
                float apex=start.y+Mathf.Max(0,v.y)*Mathf.Max(0,v.y)/(2*gravity);
                Assert.Less(apex,3.3f,$"return {i}, speed={d.Speed}, apex={apex}");
            }
        }

        [Test] public void EveryCampaignStyleKeepsBaselineServeReturnsLow()
        {
            var rows=new System.Text.StringBuilder("rival,serveReturns,intentionalLobs,ordinaryHighArcs,maxOrdinaryApex,rallyLobs,rallyReturns\n");
            foreach(var rival in TennisRoster.All) {
                var p=rival.Profile;var rng=new System.Random(71+rival.Key.Length);
                int high=0,lobs=0,rallyLobs=0;float max=0;
                for(int i=0;i<200;i++) {
                    float R()=>(float)rng.NextDouble();
                    var d=TennisOpponent.Decide(0,0,0,0,0,p,1,R(),R(),.8f,p.Reach,0,.8f,true,-11.2f,false);
                    if(d.Kind==TennisReturnKind.Lob) lobs++;
                    else {
                        var from=new Vector3(0,Mathf.Lerp(.6f,2.3f,(i%10)/9f),10.6f);
                        var v=TennisRules.RallyArcVelocity(from,d.Landing,d.Speed,d.Spin,.28f);
                        float g=9.81f+TennisBall.Magnus*d.Spin*new Vector2(v.x,v.z).magnitude;
                        float apex=from.y+Mathf.Pow(Mathf.Max(0,v.y),2)/(2*g);max=Mathf.Max(max,apex);if(apex>=3.3f)high++;
                    }
                    var rally=TennisOpponent.Decide(0,0,0,0,0,p,1,R(),R(),.4f,p.Reach,0,.5f,false,-11.2f,false);
                    if(rally.Kind==TennisReturnKind.Lob)rallyLobs++;
                }
                Assert.AreEqual(0,lobs,rival.Key); Assert.AreEqual(0,high,rival.Key);
                rows.AppendLine(System.FormattableString.Invariant($"{rival.Key},200,{lobs},{high},{max:F3},{rallyLobs},200"));
            }
            string dir=System.Environment.GetEnvironmentVariable("GAMEPLAY_PROOF_DIR") ?? "Library/Captures/gameplay-improvements";
            System.IO.Directory.CreateDirectory(dir);System.IO.File.WriteAllText(dir+"/ai-return-audit.csv",rows.ToString());
        }

        [Test] public void PlacementSeparatesDepthFromDirection()
        {
            foreach(float x in new[] {-1f,0,1}) foreach(float depth in new[] {0f,.5f,1}) {
                var t=TennisRules.PlacementTarget(x,depth);
                Assert.AreEqual(Mathf.Sign(x),Mathf.Sign(t.x));
                Assert.That(t.z,Is.EqualTo(Mathf.Lerp(3.2f,10.8f,depth)).Within(.001f));
                Assert.IsTrue(TennisRules.BounceIsIn(t));
            }
        }

        [Test] public void ShoulderFrameDoesNotReactToOrdinaryHits()
        {
            var player=new Vector3(2,.035f,-11.2f);
            TennisShoulderCamera.Frame(player,false,Vector3.zero,false,16f/9,out var p,out var look,out var fov);
            TennisShoulderCamera.Frame(player,false,new Vector3(5,8,10),false,16f/9,out var p2,out var look2,out var fov2);
            Assert.AreEqual(p,p2); Assert.AreEqual(look,look2); Assert.AreEqual(fov,fov2);
            Assert.That(p.y-player.y,Is.InRange(2f,2.4f)); Assert.That(player.z-p.z,Is.InRange(3.5f,4.5f));
            TennisShoulderCamera.Frame(player,true,Vector3.zero,false,16f/9,out var left,out _,out _);
            Assert.Less(left.x,player.x); Assert.Greater(p.x,player.x);
        }
    }
}
