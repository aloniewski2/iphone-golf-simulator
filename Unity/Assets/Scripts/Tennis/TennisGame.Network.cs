using System.Linq;
using GolfArcade.Multiplayer;
using UnityEngine;
namespace GolfArcade.Tennis {
    public sealed partial class TennisGame {
        int networkLocalSide,networkNearSide;readonly int[] networkSwings=new int[2],networkServes=new int[2];
        string networkScore="",networkPhase="";Vector3 networkPreviousA,networkPreviousB;
        bool networkConfigured;
        public float TossSeenAgo=>Mathf.Clamp(Lag+TossInputDelay,0,.25f);
        public void ConfigureNetwork(NetworkConfiguration c) {
            networkConfigured=true;
            NativeControlled=true;AutoPlay=false;Drill=false;AimPractice=false;
            networkLocalSide=c.Seat(c.localID);networkNearSide=networkLocalSide<0?0:networkLocalSide;
            var near=c.participants.First(p=>p.seat==networkNearSide);var far=c.participants.First(p=>p.seat==1-networkNearSide);
            Player.Build(near.female,PlayerSkinFor(near.female),near.left,PlayerBody(near.female));
            Opponent.Build(far.female,PlayerSkinFor(far.female),far.left,PlayerBody(far.female));
            if(hud){hud.PlayerName=near.name;hud.OpponentName=far.name;hud.MatchVisible=true;}
            if(presentation)presentation.Finish();if(replay)replay.Stop();
            networkPreviousA=Player.transform.position;networkPreviousB=Opponent.transform.position;
        }
        public void BeginSharedPresentation() { if(presentation) presentation.RestartShared(); }
        public void PredictNetworkSwing(float power) {
            if(networkLocalSide<0||!Player)return;
            if(Flow==Phase.PlayerServeToss)Player.Serve(Mathf.Clamp01(power));else Player.Swing(Mathf.Clamp01(power),false);
        }
        Vector3 NetworkPosition(NetworkVector v) {
            float sign=networkNearSide==0?1:-1;return new Vector3(v.x*sign,v.y,v.z*sign);
        }
        bool NetworkFrame() {
            if(!networkConfigured)return false;
            if(!SportsMultiplayer.Active||SportsMultiplayer.Instance.Configuration.sport!="tennis")return true;
            var net=SportsMultiplayer.Instance;var s=net.TennisState;if(s==null||!Player||!Opponent)return true;
            int n=networkNearSide,f=1-n;var a=s.players[n];var b=s.players[f];
            float predicted=a.x;
            if(!net.IsHost&&networkLocalSide>=0&&!s.paused&&!s.complete) {
                float desired=net.LocalTarget*3.6f;
                if(s.phase=="rally"&&s.receiver==n){float arrival=Mathf.Abs(s.velocity.z)>.5f?(a.z-s.ball.z)/s.velocity.z:0;desired=Mathf.Clamp(desired*.35f+(s.ball.x+s.velocity.x*Mathf.Max(0,arrival))*.65f,-4.8f,4.8f);}
                predicted=NetworkMath.Toward(a.x,desired,(float)net.RenderAdvance*(a.diveUntil>s.time?9:5.6f));
            }
            Vector3 pa=NetworkPosition(new NetworkVector(predicted,.035f,a.z)),pb=NetworkPosition(new NetworkVector(b.x,.035f,b.z));
            float smoothing=net.IsHost?1:1-Mathf.Exp(-Time.unscaledDeltaTime*24);
            Player.transform.position=Vector3.Lerp(Player.transform.position,pa,smoothing);Opponent.transform.position=Vector3.Lerp(Opponent.transform.position,pb,smoothing);
            Player.transform.rotation=Quaternion.identity;Opponent.transform.rotation=Quaternion.Euler(0,180,0);
            for(int i=0;i<2;i++) {
                var actor=i==n?Player:Opponent;var p=s.players[i];
                if(p.serves!=networkServes[i]){networkServes[i]=p.serves;actor.Serve(p.power);}
                else if(p.swings!=networkSwings[i]){networkSwings[i]=p.swings;if(!(i==networkLocalSide&&actor.Swinging))actor.Swing(p.power,false);}
            }
            float dt=Mathf.Max(.001f,Time.unscaledDeltaTime);
            Player.Tick(dt,(Player.transform.position-networkPreviousA).x/dt);Opponent.Tick(dt,(Opponent.transform.position-networkPreviousB).x/dt);
            networkPreviousA=Player.transform.position;networkPreviousB=Opponent.transform.position;
            var v=s.ball;float advance=s.paused?0:(float)net.RenderAdvance;
            if(s.phase=="rally"){v.x+=s.velocity.x*advance;v.z+=s.velocity.z*advance;v.y=Mathf.Max(TennisRules.BallRadius,v.y+s.velocity.y*advance-4.905f*advance*advance);}
            BallPosition=NetworkPosition(v);BallVelocity=NetworkPosition(s.velocity);previousBall=BallPosition;contactHitter=null;
            if(ball)ball.position=BallPosition;
            match=s.score;if(n==1){(match.PlayerPoints,match.OpponentPoints)=(match.OpponentPoints,match.PlayerPoints);(match.PlayerGames,match.OpponentGames)=(match.OpponentGames,match.PlayerGames);(match.PlayerSets,match.OpponentSets)=(match.OpponentSets,match.PlayerSets);match.PlayerServes=!match.PlayerServes;match.PlayerWonMatch=!match.PlayerWonMatch;if(match.SetScores!=null)match.SetScores=match.SetScores.Select(score=>{var halves=score.Split('–');return halves.Length==2?halves[1]+"–"+halves[0]:score;}).ToList();}
            bool localServing=s.server==n;
            if(tossMeter) {
                if(networkLocalSide>=0&&localServing&&s.phase=="serve"&&!s.complete)
                    tossMeter.RunAt(Player.transform.position,(float)(s.time-s.phaseAt+(s.paused?0:net.RenderAdvance)));
                else if(tossMeter.gameObject.activeSelf)tossMeter.Hide();
            }
            TossAccuracy=s.tossAccuracy;
            Flow=s.phase=="rally"?Phase.Rally:s.phase=="point"?Phase.PointOver:s.complete?Phase.MatchOver:localServing?(s.phase=="toss"?Phase.PlayerServeToss:Phase.PlayerServeHold):Phase.OpponentServe;
            SecondServe=s.secondServe;incoming=s.receiver==n;Stamina=a.stamina;
            Feedback=net.Stale?"Connection interrupted":s.paused?"Waiting for a player":networkLocalSide<0?"Watching":s.reason;
            string phase=s.phase=="intro"?"intro":networkLocalSide<0?"watching":s.complete?"finished":s.phase=="rally"?"rally":s.phase=="point"?"point":localServing?(s.phase=="toss"?"toss|":"serve|")+(match.DeuceCourt?"deuce":"ad"):"receive";
            if(phase!=networkPhase){networkPhase=phase;PhaseChanged?.Invoke(phase);}
            string score=match.Scoreboard;if(score!=networkScore){networkScore=score;ScoreChanged?.Invoke($"{match.PlayerGames},{match.OpponentGames},{score}");}
            if(networkLocalSide<0) {
                var camera=GameplayCamera;if(camera){camera.transform.position=new Vector3(0,9,-18);camera.transform.LookAt(new Vector3(0,.8f,0));camera.fieldOfView=48;}
            } else UpdateCamera(false);
            if(landingRing)landingRing.enabled=false;if(aimRing)aimRing.enabled=false;
            UpdateHud();return true;
        }
    }
}
