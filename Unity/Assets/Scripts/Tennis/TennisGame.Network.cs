using System.Linq;
using GolfArcade.Multiplayer;
using UnityEngine;
namespace GolfArcade.Tennis {
    public sealed partial class TennisGame {
        // networkLocalSide: whose controller this phone is (-1: watching). networkNearSide: whose end of the court is drawn in front.
        // They differ when one TV shows both players (view "split"): the court is then drawn from seat 0's end whoever this phone's player is.
        // networkView: "near" (this phone's own TV, its player in front), "split" (the one TV shows both players), "none" (this phone draws
        // nothing: it is only a controller) or "" (spectators, older owners).
        int networkLocalSide,networkNearSide;string networkView="";readonly int[] networkSwings=new int[2],networkServes=new int[2];
        string networkScore="",networkPhase="";Vector3 networkPreviousA,networkPreviousB;
        bool networkConfigured;
        long networkContact=-1; int networkBounces;
        public float TossSeenAgo=>Mathf.Clamp(Lag+TossInputDelay,0,.25f);
        public void ConfigureNetwork(NetworkConfiguration c) {
            networkConfigured=true;networkContact=-1;networkBounces=0;
            NativeControlled=true;AutoPlay=false;Drill=false;AimPractice=false;
            networkLocalSide=c.Seat(c.localID);networkView=c.LocalView??"";
            networkNearSide=NetworkConfiguration.NearSide(networkLocalSide,networkView);
            var near=c.participants.First(p=>p.seat==networkNearSide);var far=c.participants.First(p=>p.seat==1-networkNearSide);
            Player.Build(near.female,PlayerSkinFor(near.female),near.left,PlayerBody(near.female));
            Opponent.Build(far.female,PlayerSkinFor(far.female),far.left,PlayerBody(far.female));
            if(hud){hud.PlayerName=near.name;hud.OpponentName=far.name;hud.MatchVisible=true;}
            if(presentation)presentation.Finish();if(replay)replay.Stop();
            networkPreviousA=Player.transform.position;networkPreviousB=Opponent.transform.position;
            if(networkView==NetworkConfiguration.ViewSplit&&SplitScreen)BuildSplit(near.name,far.name);   // one TV, two halves
        }
        public void BeginSharedPresentation() { if(presentation) presentation.RestartShared(); }
        public void PredictNetworkSwing(float power) {
            if(networkLocalSide<0||!Player)return;
            // The local player's own stroke starts at once, before the host's update comes back; on a shared TV that player may be the far one.
            var actor=networkLocalSide==networkNearSide?Player:Opponent;if(!actor)return;
            var state=SportsMultiplayer.Instance?SportsMultiplayer.Instance.TennisState:null;
            bool tossing=state!=null?state.server==networkLocalSide&&state.phase=="toss":Flow==Phase.PlayerServeToss;
            if(tossing)actor.Serve(Mathf.Clamp01(power));else actor.Swing(Mathf.Clamp01(power),false);
        }
        /// An online match used to be silent: the view only drew the host's snapshots, and every sound, effect and haptic lived
        /// in the single-device game. The host counts each strike (`contact`) and each bounce, so a phone plays the hit the moment
        /// its snapshot shows a new one: the sound and burst for everyone watching, and a click in the hand of the player who
        /// struck it (the one the ball is now travelling away from).
        void NetworkContactFeel(NetworkTennisState s,int local) {
            if(s.contact!=networkContact) {
                bool first=networkContact<0;networkContact=s.contact;networkBounces=s.bounces;
                if(!first&&s.phase=="rally"&&!s.paused) {
                    int hitter=1-s.receiver;
                    Timing grade=s.serveFlight?Timing.Great:TennisRules.GradeFromLabel(s.reason);
                    // A phone that only controls (the TV is another phone's) draws and sounds nothing of the court.
                    if(networkView!=NetworkConfiguration.ViewNone) {
                        if(sounds!=null)sounds.Hit(grade,s.players[hitter].power);
                        if(fx!=null)fx.Contact(BallPosition,grade,false);
                    }
                    if(hitter==local)GolfArcade.Game.Haptics.Strike(TennisRules.GradeFeel(grade),false);
                }
            } else if(s.bounces>networkBounces) {
                networkBounces=s.bounces;
                if(s.phase=="rally"&&!s.paused&&sounds!=null&&networkView!=NetworkConfiguration.ViewNone)sounds.Bounce(Mathf.Clamp01(Mathf.Sqrt(s.velocity.x*s.velocity.x+s.velocity.z*s.velocity.z)/32f));
            }
        }
        Vector3 NetworkPosition(NetworkVector v) {
            float sign=networkNearSide==0?1:-1;return new Vector3(v.x*sign,v.y,v.z*sign);
        }
        bool NetworkFrame() {
            if(!networkConfigured)return false;
            if(!SportsMultiplayer.Active||SportsMultiplayer.Instance.Configuration.sport!="tennis")return true;
            var net=SportsMultiplayer.Instance;var s=net.TennisState;if(s==null||!Player||!Opponent)return true;
            int n=networkNearSide,f=1-n,l=networkLocalSide;var a=s.players[n];var b=s.players[f];
            float nearX=a.x,farX=b.x;   // canonical x of the player drawn in front and of the one behind
            if(!net.IsHost&&l>=0&&!s.paused&&!s.complete) {
                // Move the local player's own figure toward where the controller is pointing, ahead of the host's update.
                var mine=s.players[l];
                float desired=net.LocalTarget*3.6f;
                if(s.phase=="rally"&&s.receiver==l){float arrival=Mathf.Abs(s.velocity.z)>.5f?(mine.z-s.ball.z)/s.velocity.z:0;desired=Mathf.Clamp(desired*.35f+(s.ball.x+s.velocity.x*Mathf.Max(0,arrival))*.65f,-4.8f,4.8f);}
                float predicted=NetworkMath.Toward(mine.x,desired,(float)net.RenderAdvance*(mine.diveUntil>s.time?9:5.6f));
                if(l==n)nearX=predicted;else farX=predicted;
            }
            Vector3 pa=NetworkPosition(new NetworkVector(nearX,.035f,a.z)),pb=NetworkPosition(new NetworkVector(farX,.035f,b.z));
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
            NetworkContactFeel(s,l);
            match=s.score;if(n==1){(match.PlayerPoints,match.OpponentPoints)=(match.OpponentPoints,match.PlayerPoints);(match.PlayerGames,match.OpponentGames)=(match.OpponentGames,match.PlayerGames);(match.PlayerSets,match.OpponentSets)=(match.OpponentSets,match.PlayerSets);match.PlayerServes=!match.PlayerServes;match.PlayerWonMatch=!match.PlayerWonMatch;if(match.SetScores!=null)match.SetScores=match.SetScores.Select(score=>{var halves=score.Split('–');return halves.Length==2?halves[1]+"–"+halves[0]:score;}).ToList();}
            bool nearServing=s.server==n,localServing=l>=0&&s.server==l;
            if(tossMeter) {
                // Under the server's feet: its own screen shows it to the player serving, and a shared TV shows it to both.
                bool showMeter=s.phase=="serve"&&!s.complete&&(localServing||(networkView==NetworkConfiguration.ViewSplit&&l>=0));
                if(showMeter)
                    tossMeter.RunAt((nearServing?Player:Opponent).transform.position,(float)(s.time-s.phaseAt+(s.paused?0:net.RenderAdvance)));
                else if(tossMeter.gameObject.activeSelf)tossMeter.Hide();
            }
            TossAccuracy=s.tossAccuracy;
            Flow=s.phase=="rally"?Phase.Rally:s.phase=="point"?Phase.PointOver:s.complete?Phase.MatchOver:nearServing?(s.phase=="toss"?Phase.PlayerServeToss:Phase.PlayerServeHold):Phase.OpponentServe;
            SecondServe=s.secondServe;incoming=s.receiver==n;Stamina=s.players[l>=0?l:n].stamina;
            Feedback=net.Stale?"Connection interrupted":net.Quiet?"Reconnecting…":s.paused?"Waiting for a player":networkLocalSide<0?"Watching":s.reason;
            string phase=s.phase=="intro"?"intro":networkLocalSide<0?"watching":s.complete?"finished":s.phase=="rally"?"rally":s.phase=="point"?"point":localServing?(s.phase=="toss"?"toss|":"serve|")+(match.DeuceCourt?"deuce":"ad"):"receive";
            if(phase!=networkPhase){networkPhase=phase;PhaseChanged?.Invoke(phase);}
            string score=match.Scoreboard;if(score!=networkScore){networkScore=score;ScoreChanged?.Invoke($"{match.PlayerGames},{match.OpponentGames},{score}");}
            if(networkLocalSide<0) {
                var camera=GameplayCamera;if(camera){camera.transform.position=new Vector3(0,9,-18);camera.transform.LookAt(new Vector3(0,.8f,0));camera.fieldOfView=48;}
            } else if(UsesSplit) UpdateSplit();
            else if(networkView!=NetworkConfiguration.ViewNone) UpdateCamera(false);   // a controller-only phone draws nothing
            if(landingRing)landingRing.enabled=false;if(aimRing)aimRing.enabled=false;
            UpdateHud();return true;
        }
    }
}
