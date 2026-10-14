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
    /// A multiplayer round (Nearby or Online) played as single player plays. One phone, the host, keeps the rules and rules on every
    /// shot (NetworkGolfRound); every phone draws the round from the host's state. The player up lines the shot up and swings on their
    /// own phone with single player's set-up, controller, strike and aiming camera (the joystick up looks along the hole to the pin),
    /// and that phone tells the host the line, the club, the view and the backswing, so the TV shows them too. Each phone then flies
    /// the shot the host ruled on with single player's flight and result (Launch), rebuilt from the same swing; a phone that would
    /// make a different shot of it follows the host's path instead.
    public sealed partial class GolfGame {
        int networkHole=-1,networkTurn=-1;long networkCardRevision=-1;Vector3 networkBallPrevious;
        bool networkConfigured;
        /// This phone has built the round's current hole and told the host (a new hole waits for every golfer's phone).
        bool networkHoleReady;
        readonly Dictionary<int,GolferView> partyGolfers=new();
        readonly Dictionary<int,long> partyEmotes=new();
        GolferView soloGolfer;
        /// The turn the waiting golfers were last placed for (they are placed once a turn, never as the aim moves).
        string partyPlacedFor;
        /// Along the hole from the ball: to the pin on the green, else to the next landing (as the shot is first lined up).
        Vector3 LineOfPlay(CoursePoint from) {
            var target=hole.LieAt(from).IsPuttingSurface()?hole.Pin:hole.RecommendedTarget(from);
            double h=from.HeadingTo(target)*Math.PI/180;
            return new Vector3((float)Math.Sin(h),0,(float)Math.Cos(h));
        }
        long resultShot=-1;
        bool wasNetworkResult;
        NetworkConfiguration partyConfiguration;
        Scorecard networkCard;
        /// This phone has the player up (its own, or a guest it holds): it lines the shot up, swings and tells the host.
        bool networkMine;
        /// The aim and the club are this phone's to change: always, outside a multiplayer round.
        bool SteersHere => !networkConfigured || networkMine;
        /// The turn (hole · seat · stroke) lined up here; the shot flown here; whether it is flown as single player flies it.
        string networkLineUp; long networkFlown=-1; bool networkSolo;
        /// Another phone's player, from the host: where their view looks (as the joystick does) and their backswing.
        float networkLook, networkLoadShown;
        /// The golfer had wound up (a backswing was shown): the strike swings through from there.
        bool networkWoundUp;
        /// What this phone last told the host, and when it may tell it again.
        double sentHeading=double.NaN; int sentClub=-1; float sentLook, sentLoad, nextHeadingSend, nextLookSend, nextLoadSend, lastHeadingSendAt; bool headingMoving;

        /// Seconds until the next player is up, by the host's clock.
        float NetworkResultSeconds {
            get {
                var s=SportsMultiplayer.Instance?SportsMultiplayer.Instance.GolfState:null;var shot=s?.shot;if(s==null||shot==null)return 0;
                if(s.phase=="result")return (float)Math.Max(0,s.resultUntil-s.time);
                if(s.phase!="flight")return 0;
                double result=NetworkGolfRound.ResultSeconds+(shot.holed?NetworkGolfRound.HoledExtraSeconds:0);
                return (float)Math.Max(0,shot.start+Math.Max(shot.duration,shot.shown)-s.time+result);
            }
        }

        /// The course as loaded here, its holes' ground, greens and trees read off their models as each is played: the host rules on it.
        public Course.Course PlayingCourse => course;
        public void ConfigureNetwork(NetworkConfiguration c) {
            networkConfigured=true;events=RoundEvents.None;firstHoleIndex=-1;networkHole=-1;networkTurn=-1;networkCardRevision=-1;networkFlown=-1;resultShot=-1;Swing.Armed=false;
            networkLineUp=null;networkSolo=false;networkMine=false;networkLook=networkLoadShown=0;networkWoundUp=false;networkHoleReady=false;
            sentHeading=double.NaN;sentClub=-1;sentLook=sentLoad=0;
            partyConfiguration=c;networkCard=new Scorecard(course);partyPlacedFor=null;introBegunFor=-1;
            if(!soloGolfer)soloGolfer=golfer;
            soloGolfer.SetVisible(false);
            foreach(var old in partyGolfers.Values)if(old)Destroy(old.gameObject);
            partyGolfers.Clear();partyEmotes.Clear();
            foreach(var p in c.participants.Where(p=>p.seat>=0)) {
                // the whole locker look, as the golfer looks alone (skin, kit, hair colour and cut, shoes)
                var look=new CharacterLook {Body=p.female?1:0,Skin=p.loadout?.skinHex??"",Shirt=p.loadout?.shirtHex??"",Shorts=p.loadout?.shortsHex??"",
                    Hair=p.loadout?.hairHex??"",Haircut=p.loadout!=null&&p.loadout.haircut>=0?p.loadout.haircut:-1,Shoes=p.loadout?.shoesHex??""};
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
        /// The others stand off the golfer's back shoulder and watch, clear of the swing, the ball's line and the aiming camera. Their
        /// spots come from the hole (the ball's line of play), set once when the turn is lined up: the player up can aim anywhere and
        /// the people waiting stay exactly where they are, free to react (PartyEmotes).
        void PlaceParty(NetworkGolfState state,Vector3 at,Vector3 direction) {
            direction.y=0;direction=direction.sqrMagnitude>.001f?direction.normalized:Vector3.forward;
            string key=$"{state.hole}.{state.turn}.{at.x:F2}.{at.z:F2}";
            if(key==partyPlacedFor)return;
            partyPlacedFor=key;
            var right=Vector3.Cross(Vector3.up,direction).normalized;int index=0;
            foreach(var p in state.golfers) {
                if(!partyGolfers.TryGetValue(p.seat,out var model))continue;
                model.SetVisible(!p.dnf);if(p.dnf||p.seat==state.turn)continue;
                var spot=at-right*(2.7f+.8f*index)+direction*(index==1?-.9f:index==2?1.3f:.2f);index++;
                model.transform.position=HoleView.ToWorld(HoleView.ToCourse(spot));
                var facing=at-model.transform.position;facing.y=0;
                model.transform.rotation=Quaternion.LookRotation(facing.sqrMagnitude>.01f?facing:direction);
                if(model.Performing==null)model.Perform("Idle");
            }
        }
        /// The others' reactions, whenever they send one.
        void PartyEmotes(NetworkGolfState state) {
            foreach(var p in state.golfers) {
                if(!partyGolfers.TryGetValue(p.seat,out var model)||p.dnf||p.emoteID<=partyEmotes[p.seat])continue;
                partyEmotes[p.seat]=p.emoteID;
                var participant=partyConfiguration.participants.First(x=>x.seat==p.seat);
                var ids=TennisEmotes.Normalize(participant.loadout?.emotes);
                if(p.emoteSlot>=0&&p.emoteSlot<3)model.Perform(TennisEmotes.ExportName(ids[p.emoteSlot]),(float)Math.Max(0,state.time-p.emoteAt),false);
            }
        }
        bool NetworkFrame() {
            if(!networkConfigured)return false;
            if(!SportsMultiplayer.Active||SportsMultiplayer.Instance.Configuration.sport!="golf")return true;
            var net=SportsMultiplayer.Instance;var state=net.GolfState;if(state==null)return true;
            ErrorGuard.Pump();
            try { NetworkStep(net,state); }
            catch(Exception e) { Recover(e); }
            finally { Haptics.Suppressed=false; }
            return true;
        }
        /// Something went wrong mid-round: never the solo rules. The shot in the air follows the host's path, and the next turn is
        /// lined up again from the host's state.
        void NetworkFault() { networkSolo=false; networkLineUp=null; replayFirst=false; Time.timeScale=1; hud.ShowReplay(false); }

        void NetworkStep(SportsMultiplayer net,NetworkGolfState state) {
            // a later hole is built behind the loading screen, as in single player; the round waits for every phone
            if(loadingHole)return;
            if(state.hole!=networkHole) {
                bool first=networkHole<0;
                networkHole=state.hole;networkLineUp=null;networkSolo=false;networkHoleReady=false;
                if(cardShownFor>=0){cardShownFor=-1;hud.HideScorecard();}
                NetworkHoleEvents(state);   // the host's twists and this hole's weather
                if(first) StartHole(state.hole);   // (the first hole is built while the match itself is loading)
                else { EndNetworkShot();StartHoleBehindCover(state.hole);return; }
            }
            if(!networkHoleReady) {
                networkHoleReady=true;
                SetWind(new Wind(state.windSpeed,state.windDirection));   // the round's wind, not this phone's draw
                rig.SnapNext();
                SendHoleReady(net,state);
            }
            NetworkWeather(state);   // the host's wind (gusts and all) and weather, as they turn
            networkMine=net.Configuration.Controls(net.Configuration.localID,state.turn);
            PartyEmotes(state);
            if(state.phase=="loading") {
                // built here; another phone is still building it
                Swing.Armed=false;
                var tee=HoleView.ToWorld(hole.Tee);heading=hole.Tee.HeadingTo(hole.RecommendedTarget(hole.Tee));
                if(partyGolfers.TryGetValue(state.turn,out var first))golfer=first;
                golfer.Stand(tee,AimDirection());PlaceParty(state,tee,LineOfPlay(hole.Tee));
                hud.SetStatus("Waiting for everyone to arrive…");
                return;
            }
            if(state.phase=="intro") {
                // the intro starts together on every phone, in the cut the host chose
                if(introBegunFor!=state.hole){introBegunFor=state.hole;if(Current==State.Intro)BeginGolfPresentation();}
                Swing.Armed=false;
                var tee=HoleView.ToWorld(hole.Tee);heading=hole.Tee.HeadingTo(hole.RecommendedTarget(hole.Tee));
                if(partyGolfers.TryGetValue(state.turn,out var first))golfer=first;
                golfer.Stand(tee,AimDirection());PlaceParty(state,tee,LineOfPlay(hole.Tee));
                if(Current==State.Intro) TickGolfPresentation(state.paused?0:Mathf.Min(Time.unscaledDeltaTime,.1f));   // (a long frame — the load — must not eat the flyover)
                hud.SetStatus("Ready · everyone starts together");
                return;
            }
            if(state.phase=="holeDone") { NetworkHoleCard(state,true); return; }
            if(cardShownFor>=0&&!state.complete){cardShownFor=-1;hud.HideScorecard();}
            var active=state.golfers.FirstOrDefault(p=>p.seat==state.turn);if(active==null)return;
            if(networkCardRevision!=state.revision||networkTurn!=state.turn){
                networkCardRevision=state.revision;networkTurn=state.turn;networkCard=new Scorecard(course);
                for(int i=0;i<active.card.Length;i++)if(active.card[i]>0)networkCard.Record(i,active.card[i]);
            }
            // (the update carries the shot's swing but not its path, which comes on its own: the swing is all a phone needs to fly it)
            var shot=net.GolfShot!=null&&net.GolfShot.id==state.shotID?net.GolfShot:state.shot!=null&&state.shot.id==state.shotID?state.shot:null;
            if((state.phase=="flight"||state.phase=="result")&&shot!=null) NetworkShotFrame(net,state,shot);
            else NetworkAimFrame(net,state,active);
        }

        // ----- The shot -----

        void NetworkShotFrame(SportsMultiplayer net,NetworkGolfState state,NetworkGolfShot shot) {
            Swing.Armed=false;if(NativeShotReady)RequireNativeReady();
            // felt on the phone that swung it (a guest's phone feels its own)
            Haptics.Suppressed=!net.Configuration.Controls(net.Configuration.localID,shot.seat);
            if(networkFlown!=shot.id) {
                networkFlown=shot.id;networkFlownSeat=shot.seat;networkLineUp=null;
                networkSolo=FlyNetworkShot(shot,state);
                if(!networkSolo) {
                    if(partyGolfers.TryGetValue(shot.seat,out var shooter))golfer=shooter;
                    golfer.Strike();sounds.PlayStrike((GolfClub)shot.club,shot.impact?.power??.7);
                }
                networkLoadShown=0;
            }
            if(networkSolo) {
                // single player's flight, landing and result (the host says when the next player is up)
                Frame();
                // (Frame counts the result down and tells the host when this phone's own shot is done)
            } else FollowHostPath(net,state,shot);
            hud.SetScore(networkCard.Total,networkCard.ToPar,holeStrokes);
        }

        /// The shot the host ruled on, flown here as single player flies it: the same club, line and swing from the same spot in the
        /// same wind make the same shot. False when this phone's makes a different one of it (then the host's path is followed).
        bool FlyNetworkShot(NetworkGolfShot shot,NetworkGolfState state) {
            if(shot.impact==null||!shot.impact.Valid||shot.hole!=state.hole||!partyGolfers.TryGetValue(shot.seat,out var shooter))return false;
            var flown=(GolfClub)shot.club;if(Array.IndexOf(GolfClubs.All,flown)<0)return false;
            var impact=shot.impact.ToImpact();var origin=new CoursePoint(shot.originX,shot.originD);
            // the balloons, carts and gulls where the host had them as the ball left the club
            if(hole.Movers!=null){hole.Movers.PhaseAtLaunch=shot.moverPhase;networkMoverPhase=shot.moverPhase;}
            var trial=new CourseShot(flown,impact,shot.heading,origin,hole,1,Wind);
            if(trial.IsHoled!=shot.holed||trial.PenaltyStrokes!=shot.penalty||trial.Rest.DistanceTo(new CoursePoint(shot.restX,shot.restD))>.05) {
                Debug.LogWarning($"[Multiplayer] shot {shot.id} differs here (rest {trial.Rest.X:F2},{trial.Rest.D:F2} vs {shot.restX:F2},{shot.restD:F2}): following the host's path");
                return false;
            }
            golfer=shooter;golfer.SetVisible(true);
            if(Current!=State.Aim||ballAt.DistanceTo(origin)>.01) { ballAt=origin;PlaceBall(ballAt,0);ball.gameObject.SetActive(true); }
            heading=shot.heading;
            if(club!=flown||Current!=State.Aim) { club=flown;Swing.SetClub(club);golfer.SetClub(club,ballAt.DistanceTo(hole.Pin)<40); }
            golfer.Stand(ball.position,AimDirection());
            networkWoundUp=networkLoadShown>.02f;
            holeStrokes=Math.Max(0,shot.strokes-shot.penalty-1);   // Launch counts the stroke, the result the penalty
            plannedShot=false;
            Launch(impact);
            return true;
        }

        /// The host's path, drawn as it sends it (a phone whose own flight of the shot would differ).
        void FollowHostPath(SportsMultiplayer net,NetworkGolfState state,NetworkGolfShot shot) {
            if(state.phase=="flight"&&shot.path!=null&&shot.path.Length>1) {
                double at=Math.Max(0,state.time+(state.paused?0:net.RenderAdvance)-shot.start);
                double index=shot.duration<=0?shot.path.Length-1:Math.Min(shot.path.Length-1,at/shot.duration*(shot.path.Length-1));
                int lo=(int)index,hi=Math.Min(lo+1,shot.path.Length-1);
                var p=NetworkVector.Lerp(shot.path[lo],shot.path[hi],(float)(index-lo));
                var position=HoleView.ToWorld(new CoursePoint(p.x,p.z),p.y+.06);
                ball.gameObject.SetActive(true);ball.position=position;
                rig.Follow(position,(position-networkBallPrevious)/Mathf.Max(.001f,Time.unscaledDeltaTime),shot.club==(int)GolfClub.Putter);networkBallPrevious=position;
                Current=State.Flight;
            } else if(state.phase=="result") {
                Current=State.Result;wasNetworkResult=true;
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
                shotResultPanel.SetCountdown(Mathf.Min(3f,(float)Math.Max(0,state.resultUntil-state.time)),"NEXT PLAYER");
                rig.FrameGolfParty(HoleView.ToWorld(new CoursePoint(shot.originX,shot.originD)),AimDirection(),true);
            }
            var plan=hud.Map;plan.Ball=ball.position;plan.Pin=HoleView.ToWorld(hole.Pin);plan.ShowBall=true;plan.ShowLanding=false;
            if(framedMapVersion!=plan.Version)FrameMinimap();hud.DrawMinimap(minimapCamera);
        }

        /// The last shot's flight and result put away, for the next player's turn.
        void EndNetworkShot() {
            if(wasNetworkResult){wasNetworkResult=false;if(shotResultPanel)shotResultPanel.gameObject.SetActive(false);hud.ShowPlayHud(true);}
            replayFirst=false;
            if(Current==State.Replay){Time.timeScale=1;hud.ShowReplay(false);rig.ResetZoom();rig.RestoreFov();effects.EndFlight();hole.Windmill?.Drive(null);hole.Movers?.Hold(null);}
            if(Current==State.Flight){effects.EndFlight();hole.Windmill?.Drive(null);hole.Movers?.Hold(null);ballLook.Pov(false);aimLine.positionCount=0;}
            Haptics.Release();sounds.Release();
        }

        // ----- Lining up -----

        void NetworkAimFrame(SportsMultiplayer net,NetworkGolfState state,NetworkGolfer active) {
            if(state.complete) {
                Swing.Armed=false;if(NativeShotReady)RequireNativeReady();
                if(Current!=State.RoundDone){EndNetworkShot();if(shotResultPanel)shotResultPanel.gameObject.SetActive(false);holeOutCard?.Hide();Current=State.RoundDone;aimLine.positionCount=0;RefreshControls();}
                NetworkHoleCard(state,false);
                hud.SetStatus("Round complete");
                return;
            }
            string key=$"{state.hole}.{state.turn}.{active.strokes}";
            if(networkLineUp!=key) LineUpNetworkTurn(state,active,key);
            bool myTurn=networkMine&&!state.paused&&state.phase=="aim";
            if(networkMine) {
                Swing.Armed=myTurn;
                if(myTurn&&!NativeShotReady)NativeReady();
                else if(!myTurn&&NativeShotReady)RequireNativeReady();
            } else {
                Swing.Armed=false;if(NativeShotReady)RequireNativeReady();
                FollowNetworkAim(state,active,false);
            }
            // single player's aiming: the sweep and the look, the map, the pin and the targets, the backswing's sounds
            Frame();
            if(myTurn)SendNetworkAim(net);
            if(net.Quiet)hud.SetStatus("Reconnecting…");
            else if(state.paused)hud.SetStatus("Waiting for a player");
            else if(!sharedRound&&state.time-state.turnAt>NetworkGolfRound.TurnLimitSeconds-NetworkGolfRound.TurnWarnSeconds)
                hud.SetStatus($"{NetworkName(state.turn)} · {Math.Max(0,NetworkGolfRound.TurnLimitSeconds-(state.time-state.turnAt)):F0} s to play");
            else if(!networkMine)hud.SetStatus($"{NetworkName(state.turn)}'s turn");
            hud.SetScore(networkCard.Total,networkCard.ToPar,holeStrokes);
        }

        /// A turn begins: the ball where the host has it, and the shot set up as single player sets it up — along the hole to its
        /// next landing with the club for the distance, the climb included. The phone with the player up tells the host so; the
        /// others take the line and the club from the host as that phone changes them.
        void LineUpNetworkTurn(NetworkGolfState state,NetworkGolfer active,string key) {
            bool newPlayer=networkLineUp==null||!networkLineUp.StartsWith($"{state.hole}.{state.turn}.");
            networkLineUp=key;
            EndNetworkShot();
            if(partyGolfers.TryGetValue(state.turn,out var up))golfer=up;
            ballAt=new CoursePoint(active.x,active.d);holeStrokes=active.strokes;
            PlaceBall(ballAt,0);ball.gameObject.SetActive(!active.holed);
            golfer.SetVisible(true);golfer.Settle();
            aimedByPlayer=false;networkLook=0;networkLoadShown=0;networkWoundUp=false;
            rig.SnapNext();
            BeginAim(false);
            PlaceParty(state,ball.position,LineOfPlay(ballAt));
            if(networkMine) { sentHeading=double.NaN;sentClub=-1;sentLook=0;sentLoad=0; }
            else { RequireNativeReady();FollowNetworkAim(state,active,true); }
            var sheet=hud.Controller;
            // whose shot, on the phone's controller
            if(networkMine && sheet!=null && sheet.Alive) sheet.SetPlayer(state.golfers.Count(p=>!p.dnf)>1?NetworkName(state.turn):"");
            // The TV says whose shot it is when the players are on their own phones; on one shared phone the turn just passes.
            var cfg=SportsMultiplayer.Instance.Configuration;
            bool allOnThisPhone=state.golfers.All(p=>p.dnf||cfg.Controls(cfg.localID,p.seat));
            if(newPlayer && !allOnThisPhone && state.golfers.Count(p=>!p.dnf)>1) {
                bool green=hole.LieAt(ballAt).IsPuttingSurface();double toPin=ballAt.DistanceTo(hole.Pin);
                string where=holeStrokes==0?"Off the tee":green?$"{toPin*3:F0} ft to the hole":$"{toPin:F0} yd to the pin";
                hud.ShowTurn($"{NetworkName(state.turn)}'s shot",$"Stroke {holeStrokes+1}  ·  {where}",Lobby.PlayerColor(state.turn));
            }
        }

        /// Another phone's player lining up: their club and line, their view, their backswing.
        void FollowNetworkAim(NetworkGolfState state,NetworkGolfer active,bool force) {
            var theirs=(GolfClub)active.club;
            bool clubChanged=theirs!=club&&Array.IndexOf(GolfClubs.All,theirs)>=0;
            if(clubChanged){club=theirs;Swing.SetClub(club);golfer.SetClub(club,ballAt.DistanceTo(hole.Pin)<40);hud.SetClubSelection(club,announce:!force);}
            // The line glides to where their phone has it (run on at their turning rate for the update that is on its way), as their own
            // phone draws it: never a jump each update.
            float wanted=(float)active.heading+Mathf.Clamp(state.aimRate,-120,120)*.05f;
            float gap=Mathf.DeltaAngle((float)heading,wanted);
            bool turned=Math.Abs(gap)>.01f;
            if(turned)heading=force||Math.Abs(gap)>25?wanted:heading+gap*(1-Mathf.Exp(-Time.unscaledDeltaTime/.045f));
            if(clubChanged||turned||force){UpdateAimVisuals();FrameAim();}
            networkLook=Mathf.Clamp(state.look,-1,1);
            if(state.load>.01f)ShowWindUp(state.load);
            else if(networkLoadShown>0) {
                // they stopped (or the swing was cancelled): back to address
                networkLoadShown=0;hud.SetMeter(0);aimDots.MarkAt(null);hud.Map.ShowLoad=false;golfer.Settle();sounds.Release();
            }
        }

        /// This phone has built the hole: it says so for every golfer it holds, and the round starts the hole once every phone has.
        void SendHoleReady(SportsMultiplayer net,NetworkGolfState state) {
            var c=net.Configuration;
            foreach(var p in state.golfers) if(!p.dnf&&c.Controls(c.localID,p.seat)) net.Submit(new NetworkInput {action="golfHoleReady",value=state.hole,actorSeat=p.seat});
        }

        /// The player up on this phone: what the TV needs to show it — the club, the line, the view.
        void SendNetworkAim(SportsMultiplayer net) {
            if(!net.Running||Current!=State.Aim)return;
            int seat=net.GolfState.turn;float now=Time.unscaledTime;
            if((int)club!=sentClub) { sentClub=(int)club;net.Submit(new NetworkInput {action="golfClub",value=(int)club,actorSeat=seat}); }
            if((double.IsNaN(sentHeading)||Math.Abs(Mathf.DeltaAngle((float)sentHeading,(float)heading))>.02f)&&now>=nextHeadingSend) {
                float rate=now>lastHeadingSendAt&&!double.IsNaN(sentHeading)?Mathf.DeltaAngle((float)sentHeading,(float)heading)/Mathf.Max(.02f,now-lastHeadingSendAt):0;
                nextHeadingSend=now+1f/30;sentHeading=heading;lastHeadingSendAt=now;
                net.Submit(new NetworkInput {action="golfAimHeading",value=(float)((heading%360+360)%360),value2=Mathf.Clamp(rate,-120,120),actorSeat=seat});
                headingMoving=true;
            }
            else if(headingMoving&&now>=nextHeadingSend+1f/30) {
                // the line has stopped: say so, so the TV stops running it on
                headingMoving=false;nextHeadingSend=now+1f/30;lastHeadingSendAt=now;
                net.Submit(new NetworkInput {action="golfAimHeading",value=(float)((heading%360+360)%360),value2=0,actorSeat=seat});
            }
            if(Mathf.Abs(aimLook-sentLook)>.02f&&now>=nextLookSend) {
                nextLookSend=now+1f/30;sentLook=aimLook;
                net.Submit(new NetworkInput {action="golfLook",value=aimLook,actorSeat=seat},false);
            }
        }
        /// The backswing, for the TV: as it moves (it may drop one), and its end for certain.
        void SendNetworkLoad(float load) {
            if(!networkConfigured||!networkMine||!SportsMultiplayer.Active)return;
            var net=SportsMultiplayer.Instance;if(!net.Running||net.GolfState==null)return;
            float now=Time.unscaledTime;
            if(load>0&&(Mathf.Abs(load-sentLoad)<.02f||now<nextLoadSend))return;
            if(load<=0&&sentLoad<=0)return;
            nextLoadSend=now+1f/20;sentLoad=load;
            net.Submit(new NetworkInput {action="golfLoad",value=load,actorSeat=net.GolfState.turn},load<=0);
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
        /// The phone that swung says its result is over (its countdown ran out, an emote played out, or the player tapped on): the host
        /// passes the turn, as single player moves on to the next shot. Once a shot.
        void NetworkResultDone() {
            if(!networkConfigured||!SportsMultiplayer.Active||networkFlown<0||resultDoneSent==networkFlown)return;
            var net=SportsMultiplayer.Instance;
            if(!net.Configuration.Controls(net.Configuration.localID,networkFlownSeat))return;
            resultDoneSent=networkFlown;
            net.Submit(new NetworkInput {action="golfResultDone",value=networkFlown,actorSeat=networkFlownSeat});
        }
        long resultDoneSent=-1;int networkFlownSeat=-1;
        /// Whoever is still to hole out here after this shot (null when nobody is): the hole-out card then says NEXT PLAYER.
        string NetworkStillToHoleOut() {
            var s=SportsMultiplayer.Instance?SportsMultiplayer.Instance.GolfState:null;if(s==null)return null;
            int seat=s.shot!=null?s.shot.seat:s.turn;
            var other=s.golfers.FirstOrDefault(p=>!p.dnf&&!p.holed&&p.seat!=seat);
            return other==null?null:NetworkName(other.seat);
        }
        int cardShownFor=-1, introBegunFor=-1;
        /// Single player's card with others, from the host's cards: a row each and the standings, after every hole (more) and at the end.
        void NetworkHoleCard(NetworkGolfState state,bool more) {
            int key=more?state.hole:1000+state.hole;
            if(cardShownFor==key)return;
            cardShownFor=key;
            EndNetworkShot();if(shotResultPanel)shotResultPanel.gameObject.SetActive(false);holeOutCard?.Hide();aimLine.positionCount=0;
            var golfers=state.golfers.Where(g=>!g.dnf).ToArray();
            var rows=new RoundCard.Row[golfers.Length];var standing=new List<(string name,int toPar,int played)>();
            for(int i=0;i<golfers.Length;i++) {
                var g=golfers[i];var strokes=new int?[course.Holes.Length];int toPar=0,played=0;
                for(int h=0;h<strokes.Length&&h<g.card.Length;h++) if(g.card[h]>0){strokes[h]=g.card[h];toPar+=g.card[h]-course.Holes[h].Par;played++;}
                rows[i]=new RoundCard.Row{Name=NetworkName(g.seat),Color=Lobby.PlayerColor(g.seat),Strokes=strokes};
                standing.Add((NetworkName(g.seat),toPar,played));
            }
            standing.Sort((a,b)=>a.toPar.CompareTo(b.toPar));
            var tiles=standing.Select((p,i)=>new RoundCard.Highlight{Icon=i==0?"trophy":"ball",Title=$"{i+1}. {p.name}",Value=Scorecard.FormatToPar(p.toPar)}).ToArray();
            string headline;
            if(more) {
                var onHole=golfers.Where(g=>state.hole<g.card.Length&&g.card[state.hole]>0).OrderBy(g=>g.card[state.hole]).ToArray();
                headline=onHole.Length==0?"":onHole.Length>1&&onHole[0].card[state.hole]==onHole[1].card[state.hole]?"Hole halved":$"{NetworkName(onHole[0].seat)} wins the hole";
            } else headline=standing.Count>1&&standing[0].toPar==standing[1].toPar?"All square":$"{standing[0].name} wins";
            var mine=golfers.FirstOrDefault(g=>partyConfiguration.Controls(partyConfiguration.localID,g.seat))??golfers[0];
            var card=new Scorecard(course);for(int h=0;h<mine.card.Length;h++)if(mine.card[h]>0)card.Record(h,mine.card[h]);
            hud.ShowPlayHud(false);
            hud.ShowScorecard(card,more?$"Hole {hole.PlayNumber} complete":"Round complete",headline,tiles,more,rows);
            int holeNow=state.hole;
            if(hud.NextHole) hud.NextHole.Pressed=()=>{ if(SportsMultiplayer.Active) SportsMultiplayer.Instance.Submit(new NetworkInput{action="golfHoleCardDone",value=holeNow,actorSeat=mine.seat}); };
            // (the lobby takes everyone on after the round: no play-again or menu here)
            if(hud.PlayAgain) hud.PlayAgain.gameObject.SetActive(false);
            if(hud.RoundMenu) hud.RoundMenu.gameObject.SetActive(false);
            sounds.PlayReady();
        }
        /// Every golfer is on this phone (pass the phone): no turn clock.
        bool sharedRound=>partyConfiguration!=null&&partyConfiguration.participants.Where(p=>p.seat>=0).All(p=>partyConfiguration.Controls(partyConfiguration.localID,p.seat));
        /// The walk-on nameplate: the golfer actually on the tee.
        string NameplateName=>networkConfigured&&SportsMultiplayer.Active&&SportsMultiplayer.Instance.GolfState!=null?NetworkName(SportsMultiplayer.Instance.GolfState.turn):Match?.Current?.Name??"YOU";
        string NetworkName(int seat)=>SportsMultiplayer.Instance.Configuration.participants.FirstOrDefault(p=>p.seat==seat)?.name??"Player";
        /// The swing goes to the host with its club and line; the host rules on it and every phone flies it (FlyNetworkShot).
        bool NetworkShot(GolfArcade.Swing.SwingImpact impact) {
            if(!SportsMultiplayer.Active)return false;
            var net=SportsMultiplayer.Instance;
            net.Submit(new NetworkInput {action="swing",power=(float)impact.Power,aim=Mathf.Clamp((float)impact.StartLineDegrees/8,-1,1),
                facing=Mathf.Clamp((float)impact.CurveDegrees/10,-1,1),impact=NetworkImpact.From(impact),shotClub=(int)club,heading=heading,
                actorSeat=net.GolfState?.turn ?? -1});
            sentLoad=0;
            return true;
        }
    }
}
