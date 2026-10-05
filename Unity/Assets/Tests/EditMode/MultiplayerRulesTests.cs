using System;
using System.Linq;
using NUnit.Framework;
using GolfArcade.Multiplayer;
using GolfArcade.Tennis;

namespace GolfArcade.Tests {
    public class MultiplayerRulesTests {
        static NetworkInput Input(NetworkTennisMatch m,string action,long id=1,double age=0) => new() {action=action,eventID=id,time=m.State.time,point=m.State.point,contact=m.State.contact,power=.7f,age=age};
        static void Step(NetworkTennisMatch m,double seconds){for(int i=0;i<Math.Ceiling(seconds*120);i++)m.Step(1.0/120);}
        static void Step(NetworkGolfRound m,double seconds){for(int i=0;i<Math.Ceiling(seconds*20);i++)m.Step(.05);}
        static void FinishFlight(NetworkGolfRound m)=>Step(m,m.State.shot.duration+2.1);
        static NetworkInput Golf(NetworkGolfRound m,long id=1)=>new() {action="swing",eventID=id,time=m.State.time,power=.6f};
        static NetworkConfiguration Config(string sport="tennis")=>new() {lobbyID="party",matchID="contest",hostID="a",localID="a",sport=sport,venue="resort",participants=new[] {new NetworkParticipant {id="a",seat=0},new NetworkParticipant {id="b",seat=1},new NetworkParticipant {id="watch",seat=-1}}};

        [Test] public void TennisRejectsObserverAndInvalidInput(){var m=new NetworkTennisMatch();Assert.False(m.Input(-1,Input(m,"toss"),0));Assert.False(m.Input(2,Input(m,"toss"),0));var x=Input(m,"toss");x.power=float.NaN;Assert.False(m.Input(0,x,0));Assert.AreEqual("serve",m.State.phase);}
        [Test] public void OldPointAndContactCannotTriggerNewSwing(){var m=new NetworkTennisMatch();var x=Input(m,"toss");x.point--;Assert.False(m.Input(0,x,0));x=Input(m,"toss");x.contact++;Assert.False(m.Input(0,x,0));}
        [Test] public void ImpossibleTimestampsAreRejected(){var m=new NetworkTennisMatch();var x=Input(m,"toss");x.time=.1;Assert.False(m.Input(0,x,0));x.time=-.6;Assert.False(m.Input(0,x,0));}
        [Test] public void OnlyServerCanTossAndAConfirmedServeLaunches(){var m=new NetworkTennisMatch();Step(m,.25);Assert.False(m.Input(1,Input(m,"toss"),m.State.time));Assert.True(m.Input(0,Input(m,"toss"),m.State.time));Step(m,.75);Assert.True(m.Input(0,Input(m,"swing"),m.State.time));Assert.AreEqual("rally",m.State.phase);Assert.AreEqual(1,m.State.receiver);Assert.Greater(m.State.velocity.z,0);}
        [Test] public void FarSideAlsoServesAfterServiceChanges(){var m=new NetworkTennisMatch(1,3);for(int p=0;p<4;p++){Assert.True(m.Input(0,Input(m,"toss",p*2+1),m.State.time));Step(m,1.8);Assert.True(m.Input(0,Input(m,"toss",p*2+2),m.State.time));Step(m,4);}Assert.AreEqual(1,m.State.server);Assert.True(m.Input(1,Input(m,"toss",100),m.State.time));Step(m,.75);Assert.True(m.Input(1,Input(m,"swing",100),m.State.time));Assert.AreEqual("rally",m.State.phase);Assert.Less(m.State.velocity.z,0);}
        [Test] public void DoubleFaultProducesExactlyOnePoint(){var m=new NetworkTennisMatch();m.Input(0,Input(m,"toss"),0);Step(m,1.8);Assert.True(m.State.secondServe);Assert.AreEqual(0,m.State.score.OpponentPoints);m.Input(0,Input(m,"toss",2),m.State.time);Step(m,1.8);Assert.AreEqual(1,m.State.score.OpponentPoints);Assert.AreEqual("point",m.State.phase);}
        [Test] public void ProvisionalSwingCannotReturnBall(){var m=new NetworkTennisMatch();m.State.phase="rally";m.State.receiver=1;m.State.ball=new(m.State.players[1].x,1,m.State.players[1].z);m.State.velocity=default;m.Input(1,Input(m,"beginSwing"),m.State.time);Step(m,.2);Assert.AreEqual(0,m.State.contact);}
        [Test] public void ConfirmedSwingReturnsOnceAndDuplicateIsIgnored(){var m=new NetworkTennisMatch();m.State.phase="rally";m.State.receiver=1;m.State.ball=new(m.State.players[1].x,1,m.State.players[1].z);m.State.velocity=default;var x=Input(m,"swing");Assert.True(m.Input(1,x,m.State.time));Assert.False(m.Input(1,x,m.State.time));Step(m,.2);Assert.AreEqual(1,m.State.contact);Assert.AreEqual(0,m.State.receiver);}
        [Test] public void ServeCannotBeVolleyedThroughHistoryRewind(){var m=new NetworkTennisMatch();m.State.phase="rally";m.State.receiver=1;m.State.serveFlight=true;m.State.ball=new(m.State.players[1].x,1,m.State.players[1].z);Step(m,.1);m.Input(1,Input(m,"swing",1,.1),m.State.time);Step(m,.2);Assert.AreEqual(0,m.State.contact);}
        [Test] public void PauseFreezesOfficialTimeAndRejectsInput(){var m=new NetworkTennisMatch();m.State.paused=true;Step(m,1);Assert.AreEqual(0,m.State.time);Assert.False(m.Input(0,Input(m,"toss"),0));}
        [Test] public void TennisDropInterruptsWithoutAwardingWin(){var m=new NetworkTennisMatch();string result=null;m.Result=s=>result=s;m.Drop(1);Assert.True(m.State.complete);Assert.False(m.State.score.Complete);Assert.AreEqual("interrupted",result);}
        [Test] public void TennisResultIsEmittedOnceOnRealScoringCompletion(){var m=new NetworkTennisMatch(1,1);int results=0;m.Result=_=>results++;for(int p=0;p<4;p++){m.Input(0,Input(m,"toss",p*2+1),m.State.time);Step(m,1.8);m.Input(0,Input(m,"toss",p*2+2),m.State.time);Step(m,4);}Assert.True(m.State.score.Complete);Assert.False(m.State.score.PlayerWonMatch);Assert.AreEqual(1,results);Step(m,10);m.Drop(0);Assert.AreEqual(1,results);}
        [Test] public void GolfRejectsOutOfTurnAndSpectatorShots(){var m=new NetworkGolfRound(new[]{0,1,2,3},20);Assert.False(m.Input(-1,Golf(m),0));Assert.False(m.Input(1,Golf(m),0));Assert.AreEqual(0,m.State.shotID);}
        [Test] public void GolfAuthorityGeneratesBoundedPathAndCommitsOneShot(){var m=new NetworkGolfRound(new[]{0,1,2,3},20);int calls=0;m.Shot=_=>calls++;var x=Golf(m);Assert.True(m.Input(0,x,0));Assert.False(m.Input(0,x,0));Assert.AreEqual(1,calls);Assert.AreEqual(1,m.State.shotID);Assert.That(m.State.shot.path.Length,Is.InRange(2,360));Assert.GreaterOrEqual(m.State.golfers[0].strokes,1);}
        [Test] public void GolfAllFourTeeOffBeforeSecondShot(){var m=new NetworkGolfRound(new[]{0,1,2,3},20);for(int seat=0;seat<4;seat++){Assert.AreEqual(seat,m.State.turn);Assert.True(m.Input(seat,Golf(m),m.State.time));FinishFlight(m);}Assert.True(m.State.golfers.All(p=>p.strokes>=1));}
        [Test] public void GolfRepeatedEventAfterFlightCannotApplyAgain(){var m=new NetworkGolfRound(new[]{0,1},20);m.Input(0,Golf(m),0);FinishFlight(m);m.Input(1,Golf(m),m.State.time);FinishFlight(m);int seat=m.State.turn;Assert.False(m.Input(seat,Golf(m),m.State.time));}
        [Test] public void GolfTimeoutAddsStrokeAndMovesTurn(){var m=new NetworkGolfRound(new[]{0,1,2,3},20);Step(m,60.1);Assert.AreEqual(1,m.State.golfers[0].strokes);Assert.AreEqual(1,m.State.turn);Assert.Null(m.State.shot);}
        [Test] public void GolfDnfSkipsPlayerAndOnlyInterruptsWithOneRemaining(){var m=new NetworkGolfRound(new[]{0,1,2,3},20);m.Drop(0);Assert.AreEqual(1,m.State.turn);Assert.True(m.State.golfers[0].dnf);Assert.False(m.State.complete);m.Drop(1);Assert.False(m.State.complete);m.Drop(2);Assert.True(m.State.complete);Assert.AreEqual("interrupted",m.State.phase);}
        [Test] public void GolfSameSeedAndShotsGiveIdenticalOfficialOutcome(){var a=new NetworkGolfRound(new[]{0,1,2,3},20);var b=new NetworkGolfRound(new[]{0,1,2,3},20);a.Input(0,Golf(a),0);b.Input(0,Golf(b),0);Assert.AreEqual(a.State.shot.nextX,b.State.shot.nextX);Assert.AreEqual(a.State.shot.nextD,b.State.shot.nextD);Assert.AreEqual(a.State.shot.penalty,b.State.shot.penalty);}
        [Test] public void GolfInvalidRosterRejected(){Assert.Throws<ArgumentException>(()=>new NetworkGolfRound(new[]{0,0},1));Assert.Throws<ArgumentException>(()=>new NetworkGolfRound(new[]{0,4},1));}
        [Test] public void FullPowerServeReachesLegalFirstBounce(){var m=new NetworkTennisMatch();Step(m,.25);m.Input(0,Input(m,"toss"),m.State.time);Step(m,.75);var hit=Input(m,"swing");hit.power=1;Assert.True(m.Input(0,hit,m.State.time));for(int i=0;i<240&&m.State.bounces==0&&m.State.phase=="rally";i++)m.Step(1.0/120);Assert.AreEqual("rally",m.State.phase);Assert.AreEqual(1,m.State.bounces);Assert.False(m.State.secondServe);}

        [Test] public void NetworkTossIgnoresClaimedAccuracyAndUsesTheCompensatedSweep() {
            var start=new NetworkTennisMatch();var cheat=Input(start,"toss");cheat.value=1;
            Assert.True(start.Input(0,cheat,0));Assert.AreEqual(0,start.State.tossAccuracy);
            foreach(double transmission in new[]{0,.05,.1}) {
                var m=new NetworkTennisMatch();Step(m,.35+transmission);
                var toss=Input(m,"toss",age:.1);toss.time-=transmission;
                Assert.True(m.Input(0,toss,m.State.time));Assert.That(m.State.tossAccuracy,Is.EqualTo(1).Within(.001));
            }
        }

        [Test] public void NetworkTossMissesScatterTheActualServeLanding() {
            float previous=-1;
            foreach(double wait in new[]{.25,.35,.45,.5}) {
                var m=new NetworkTennisMatch();Step(m,wait);m.Input(0,Input(m,"toss"),m.State.time);
                Step(m,.75);m.Input(0,Input(m,"swing"),m.State.time);
                var b=m.State.ball;var v=m.State.velocity;
                float flight=(v.y+(float)Math.Sqrt(v.y*v.y+2*9.81f*(b.y-.034f)))/9.81f;
                float dx=b.x+v.x*flight+1.2f,dz=b.z+v.z*flight-(2.4f+.75f*3.4f);
                float error=(float)Math.Sqrt(dx*dx+dz*dz);
                Assert.Greater(error,previous,$"meter age {wait}");previous=error;
            }
        }
        [Test] public void FourGolfersCanFinishAllHolesAndReceiveMatchingTotals(){var m=new NetworkGolfRound(new[]{0,1,2,3},1);int results=0;m.Result=_=>results++;for(int i=0;i<260000&& !m.State.complete;i++)m.Step(.1);Assert.True(m.State.complete);Assert.AreEqual("complete",m.State.phase);Assert.AreEqual(1,results);foreach(var p in m.State.golfers){Assert.True(p.card.All(score=>score>0));Assert.AreEqual(p.card.Sum(),p.total);}Assert.AreEqual(m.State.golfers[0].total,m.State.golfers[3].total);}
        [Test] public void ConfigurationAllowsWatchersButRejectsDuplicateSeatsAndFivePeople(){var c=Config();Assert.True(c.Valid);c.participants[2].seat=1;Assert.False(c.Valid);c=Config();c.participants=new[]{c.participants[0],c.participants[1],c.participants[2],new NetworkParticipant{id="d"},new NetworkParticipant{id="e"}};Assert.False(c.Valid);}
    }
}
