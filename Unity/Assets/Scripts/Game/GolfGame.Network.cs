using System;
using System.Linq;
using System.Collections.Generic;
using GolfArcade.Profile;
using GolfArcade.Tennis;
using GolfArcade.UI;
using GolfArcade.Multiplayer;
using GolfArcade.Course;
using GolfArcade.Shot;
using UnityEngine;
namespace GolfArcade.Game {
    public sealed partial class GolfGame {
        int networkHole=-1,networkTurn=-1,networkClub=-1;long networkShot=-1,networkCardRevision=-1;Vector3 networkBallPrevious;
        bool networkConfigured;
        readonly Dictionary<int,GolferView> partyGolfers=new();
        readonly Dictionary<int,long> partyEmotes=new();
        GolferView soloGolfer;
        long resultShot=-1;
        Vector3 partyOrigin;
        bool wasNetworkResult;
        NetworkConfiguration partyConfiguration;
        Scorecard networkCard;
        public void ConfigureNetwork(NetworkConfiguration c) {
            networkConfigured=true;networkHole=-1;networkTurn=-1;networkClub=-1;networkCardRevision=-1;networkShot=-1;resultShot=-1;Swing.Armed=false;
            partyConfiguration=c;networkCard=new Scorecard(course);
            if(!soloGolfer)soloGolfer=golfer;
            soloGolfer.SetVisible(false);
            foreach(var old in partyGolfers.Values)if(old)Destroy(old.gameObject);
            partyGolfers.Clear();partyEmotes.Clear();
            foreach(var p in c.participants.Where(p=>p.seat>=0)) {
                var look=new CharacterLook {Body=p.female?1:0,Skin=p.loadout?.skinHex??"",Shirt=p.loadout?.shirtHex??"",Shorts=p.loadout?.shortsHex??""};
                var style=GolferView.Look.Player;style.Hero=GolferStyle.HeroOf(look);
                var model=GolferView.CreatePlayer(transform,style);model.name="Party golfer · "+p.name;
                partyGolfers[p.seat]=model;partyEmotes[p.seat]=0;model.Perform("Idle");
            }
            golfer=partyGolfers.Values.First();
        }
        public bool CanNetworkEmote(int seat) {
            var n=SportsMultiplayer.Instance;var s=n?.GolfState;
            var player=s?.golfers.FirstOrDefault(p=>p.seat==seat);
            return n && n.Running && player!=null && !player.dnf && !s.paused && !s.complete && s.phase!="intro"
                && (seat!=s.turn || s.phase=="result") && s.time-player.emoteAt>=1.5 && n.Configuration.Controls(n.Configuration.localID,seat);
        }
        public bool PlayNetworkEmote(int seat,int slot) {
            if(slot<0||slot>2||!CanNetworkEmote(seat))return false;
            SportsMultiplayer.Instance.Submit(new NetworkInput {action="emote",value=slot,actorSeat=seat});return true;
        }
        void PlaceParty(NetworkGolfState state,Vector3 direction) {
            var right=Vector3.Cross(Vector3.up,direction).normalized;int index=0;
            foreach(var p in state.golfers) {
                if(!partyGolfers.TryGetValue(p.seat,out var model))continue;
                model.SetVisible(!p.dnf);if(p.dnf)continue;
                if(p.seat!=state.turn) {
                    // A semicircle behind the shot, clear of both the club and the ball path.
                    float side=index==0?-2.4f:index==1?2.4f:0;
                    var at=partyOrigin+right*side-direction*(index==2?3.4f:1.9f);index++;
                    model.transform.position=HoleView.ToWorld(HoleView.ToCourse(at));
                    model.transform.rotation=Quaternion.LookRotation(direction);
                    if(model.Performing==null)model.Perform("Idle");
                }
                if(p.emoteID>partyEmotes[p.seat]) {
                    partyEmotes[p.seat]=p.emoteID;
                    var participant=partyConfiguration.participants.First(x=>x.seat==p.seat);
                    var ids=TennisEmotes.Normalize(participant.loadout?.emotes);
                    if(p.emoteSlot>=0&&p.emoteSlot<3)model.Perform(TennisEmotes.ExportName(ids[p.emoteSlot]),(float)Math.Max(0,state.time-p.emoteAt),false);
                }
            }
        }
        bool NetworkFrame() {
            if(!networkConfigured)return false;
            if(!SportsMultiplayer.Active||SportsMultiplayer.Instance.Configuration.sport!="golf")return true;
            var net=SportsMultiplayer.Instance;var state=net.GolfState;if(state==null)return true;
            if(state.hole!=networkHole){networkHole=state.hole;StartHole(state.hole);rig.SnapNext();}
            if(state.phase=="intro") {
                Swing.Armed=false;
                partyOrigin=HoleView.ToWorld(hole.Tee);heading=hole.Tee.HeadingTo(hole.Pin);
                golfer=partyGolfers[state.turn];golfer.Stand(partyOrigin,AimDirection());PlaceParty(state,AimDirection());
                if(Current==State.Intro) TickGolfPresentation(state.paused?0:Time.unscaledDeltaTime);
                hud.SetStatus("Ready · everyone starts together"); rig.ApplyFrame(); return true;
            }
            var active=state.golfers.FirstOrDefault(p=>p.seat==state.turn);if(active==null)return true;
            bool myTurn=net.Configuration.Controls(net.Configuration.localID,state.turn)&&!state.paused&&!state.complete&&state.phase=="aim";
            bool armMotion=myTurn && (!NativeShotReady || networkTurn!=state.turn || networkClub!=active.club);
            Wind=new Wind(state.windSpeed,state.windDirection);ballAt=new CoursePoint(active.x,active.d);holeStrokes=active.strokes;club=(GolfClub)active.club;heading=active.heading;
            golfer=partyGolfers[state.turn];
            if(networkCardRevision!=state.revision||networkTurn!=state.turn){networkCardRevision=state.revision;networkCard=new Scorecard(course);for(int i=0;i<active.card.Length;i++)if(active.card[i]>0)networkCard.Record(i,active.card[i]);}
            if(networkClub!=active.club||networkTurn!=state.turn){networkClub=active.club;Swing.SetClub(club);golfer.SetClub(club,false);}
            Swing.Armed=myTurn;if(myTurn){Swing.Update();PollControllerButtons();}
            if(networkTurn!=state.turn){networkTurn=state.turn;golfer.Settle();golfer.SetClub(club,false);rig.SnapNext();}
            NativeShotReady=myTurn;
            if(state.phase=="aim")partyOrigin=HoleView.ToWorld(ballAt);
            if(state.phase!="result" && wasNetworkResult) {wasNetworkResult=false;if(shotResultPanel)shotResultPanel.gameObject.SetActive(false);hud.ShowPlayHud(true);}
            PlaceParty(state,AimDirection());
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
            } else if(state.phase=="result" && shot!=null && shot.id==state.shotID) {
                Current=State.Result;wasNetworkResult=true;
                golfer.transform.rotation=Quaternion.LookRotation(AimDirection());
                if(resultShot!=shot.id) {
                    resultShot=shot.id;golfer.Perform("Idle");
                    hud.HideLanding();hud.HideShotStats();hud.ShowPlayHud(false);aimLine.positionCount=0;effects.ClearTracer();
                    if(!shotResultPanel)shotResultPanel=ShotResultPanel.Create(transform,rig.Camera);
                    string outcome=shot.holed?"IN THE HOLE!":shot.penalty>0?"PENALTY +"+shot.penalty:shot.lie.ToUpperInvariant();
                    ShotResultTitle=outcome;
                    ShotStatistics=$"Carry {shot.carry:F0} yd · Roll {shot.roll:F0} yd · Total {shot.carry+shot.roll:F0} yd";
                    shotResultPanel.ShowGolf(NetworkName(shot.seat),outcome,shot.carry,shot.roll,shot.apex,
                        new CoursePoint(shot.restX,shot.restD).DistanceTo(hole.Pin),shot.club==(int)GolfClub.Putter,shot.holed);
                    rig.SnapNext();
                }
                shotResultPanel.SetCountdown((float)Math.Max(0,state.resultUntil-state.time),"NEXT PLAYER");
                rig.FrameGolfParty(partyOrigin,AimDirection(),true);
            } else {
                PlaceBall(ballAt,0);ball.gameObject.SetActive(!active.holed);golfer.SetVisible(true);
                golfer.Stand(ball.position,AimDirection());
                rig.FrameGolfParty(ball.position,AimDirection(),false);
                Current=state.complete?State.RoundDone:State.Aim;
                if(armMotion) NativeReady();
                if(myTurn){float sweep=(Input.GetKey(KeyCode.LeftArrow)?-1:0)+(Input.GetKey(KeyCode.RightArrow)?1:0);if(sweep!=0)NativeAim(sweep);}
            }
            hud.SetScore(networkCard.Total,networkCard.ToPar,holeStrokes);hud.SetStatus(state.paused?"Waiting for a player":state.complete?"Round complete":myTurn?"Your turn":$"{NetworkName(state.turn)}'s turn");
            if(myTurn)UpdateAimVisuals();else aimLine.positionCount=0;
            var plan=hud.Map;plan.Ball=ball.position;plan.Pin=HoleView.ToWorld(hole.Pin);plan.ShowBall=true;
            plan.Landing=landingMarker.position;plan.ShowLanding=myTurn;
            if(framedMapVersion!=plan.Version)FrameMinimap();hud.DrawMinimap(minimapCamera);
            RefreshControls();rig.ApplyFrame();return true;
        }
        void FillPartyReading(ControllerReading reading) {
            var net=SportsMultiplayer.Instance;var state=net?.GolfState;
            if(state==null)return;
            var shot=state.shot;
            reading.party=new PartyControllerReading {
                turn=state.turn,player=NetworkName(state.turn),phase=state.phase,shotID=state.shotID,
                myTurn=net.Configuration.Controls(net.Configuration.localID,state.turn),
                shared=net.Configuration.participants.Any(p=>p.controllerID==net.Configuration.localID),
                resultSeconds=state.phase=="result"?Math.Max(0,state.resultUntil-state.time):0,
                carry=shot?.carry??0,roll=shot?.roll??0,apex=shot?.apex??0,total=(shot?.carry??0)+(shot?.roll??0),
                putt=shot?.club==(int)GolfClub.Putter,
                outcome=shot==null?"":shot.holed?"IN THE HOLE!":shot.penalty>0?"PENALTY +"+shot.penalty:shot.lie.ToUpperInvariant(),
                players=state.golfers.Where(p=>!p.dnf).Select(p=>new PartyControllerPlayer {
                    seat=p.seat,name=NetworkName(p.seat),controlled=net.Configuration.Controls(net.Configuration.localID,p.seat),canEmote=CanNetworkEmote(p.seat),
                    emotes=TennisEmotes.Normalize(net.Configuration.participants.First(n=>n.seat==p.seat).loadout?.emotes)
                }).ToArray()
            };
        }
        string NetworkName(int seat)=>SportsMultiplayer.Instance.Configuration.participants.FirstOrDefault(p=>p.seat==seat)?.name??"Player";
        bool NetworkShot(GolfArcade.Swing.SwingImpact impact) {
            if(!SportsMultiplayer.Active)return false;
            var net=SportsMultiplayer.Instance;
            net.Submit(new NetworkInput {action="swing",power=(float)impact.Power,aim=(float)impact.StartLineDegrees/8,
                facing=(float)impact.CurveDegrees/10,actorSeat=net.GolfState?.turn ?? -1});return true;
        }
    }
}
