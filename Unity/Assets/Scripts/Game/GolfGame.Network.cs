using System;
using System.Linq;
using GolfArcade.Multiplayer;
using GolfArcade.Course;
using GolfArcade.Shot;
using UnityEngine;
namespace GolfArcade.Game {
    public sealed partial class GolfGame {
        int networkHole=-1,networkTurn=-1,networkClub=-1;long networkShot=-1,networkCardRevision=-1;Vector3 networkBallPrevious;
        bool networkConfigured;
        Scorecard networkCard;
        public void ConfigureNetwork(NetworkConfiguration c){networkConfigured=true;networkHole=-1;networkTurn=-1;networkClub=-1;networkCardRevision=-1;networkShot=-1;Swing.Armed=false;}
        bool NetworkFrame() {
            if(!networkConfigured)return false;
            if(!SportsMultiplayer.Active||SportsMultiplayer.Instance.Configuration.sport!="golf")return true;
            var net=SportsMultiplayer.Instance;var state=net.GolfState;if(state==null)return true;
            if(state.hole!=networkHole){networkHole=state.hole;StartHole(state.hole);rig.SnapNext();}
            if(state.phase=="intro") {
                Swing.Armed=false;
                if(Current==State.Intro) TickGolfPresentation(state.paused?0:Time.unscaledDeltaTime);
                hud.SetStatus("Ready · everyone starts together"); rig.ApplyFrame(); return true;
            }
            var active=state.golfers.FirstOrDefault(p=>p.seat==state.turn);if(active==null)return true;
            bool myTurn=net.LocalSeat==state.turn&&!state.paused&&!state.complete&&state.phase=="aim";
            Wind=new Wind(state.windSpeed,state.windDirection);ballAt=new CoursePoint(active.x,active.d);holeStrokes=active.strokes;club=(GolfClub)active.club;heading=active.heading;
            if(networkCardRevision!=state.revision||networkTurn!=state.turn){networkCardRevision=state.revision;networkCard=new Scorecard(course);for(int i=0;i<active.card.Length;i++)if(active.card[i]>0)Card.Record(i,active.card[i]);}
            if(networkClub!=active.club||networkTurn!=state.turn){networkClub=active.club;Swing.SetClub(club);}
            Swing.Armed=myTurn;if(myTurn){Swing.Update();PollControllerButtons();}
            if(networkTurn!=state.turn){networkTurn=state.turn;golfer.Settle();rig.SnapNext();}
            var shot=net.GolfShot;
            if(state.phase=="flight"&&shot!=null&&shot.id==state.shotID&&shot.path!=null&&shot.path.Length>1) {
                if(networkShot!=shot.id){networkShot=shot.id;golfer.Strike();sounds.PlayStrike((GolfClub)shot.club,.7);}
                double at=Math.Max(0,state.time+(state.paused?0:net.RenderAdvance)-shot.start);
                double index=shot.duration<=0?shot.path.Length-1:Math.Min(shot.path.Length-1,at/shot.duration*(shot.path.Length-1));
                int lo=(int)index,hi=Math.Min(lo+1,shot.path.Length-1);
                var p=NetworkVector.Lerp(shot.path[lo],shot.path[hi],(float)(index-lo));
                var position=HoleView.ToWorld(new CoursePoint(p.x,p.z),p.y+.06);
                ball.gameObject.SetActive(true);ball.position=position;
                rig.Follow(position, (position-networkBallPrevious)/Mathf.Max(.001f,Time.unscaledDeltaTime), shot.club==(int)GolfClub.Putter);networkBallPrevious=position;
                Current=State.Flight;
            } else {
                PlaceBall(ballAt,0);ball.gameObject.SetActive(!active.holed);golfer.SetVisible(true);
                golfer.Stand(ball.position,AimDirection());
                rig.FrameAddress(ball.position,AimDirection(),hole.LieAt(ballAt)==CourseLie.Green);
                Current=state.complete?State.RoundDone:State.Aim;
                if(myTurn){float sweep=(Input.GetKey(KeyCode.LeftArrow)?-1:0)+(Input.GetKey(KeyCode.RightArrow)?1:0);if(sweep!=0)NativeAim(sweep);}
            }
            hud.SetScore(Card.Total,Card.ToPar,holeStrokes);hud.SetStatus(state.paused?"Waiting for a player":state.complete?"Round complete":myTurn?"Your turn":$"{NetworkName(state.turn)}'s turn");
            if(myTurn)UpdateAimVisuals();else aimLine.positionCount=0;
            RefreshControls();rig.ApplyFrame();return true;
        }
        string NetworkName(int seat)=>SportsMultiplayer.Instance.Configuration.participants.FirstOrDefault(p=>p.seat==seat)?.name??"Player";
        bool NetworkShot(GolfArcade.Swing.SwingImpact impact) {
            if(!SportsMultiplayer.Active)return false;
            SportsMultiplayer.Instance.Submit(new NetworkInput {action="swing",power=(float)impact.Power,aim=(float)impact.StartLineDegrees/8,facing=(float)impact.CurveDegrees/10});return true;
        }
    }
}
