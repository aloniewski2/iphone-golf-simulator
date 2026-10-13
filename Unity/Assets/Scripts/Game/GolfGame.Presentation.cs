using System.Linq;
using UnityEngine;
using GolfArcade.Multiplayer;
using GolfArcade.Tennis;
using GolfArcade.UI;
namespace GolfArcade.Game {
    /// A hole's opening: the flyover (HoleIntro: a glide down the hole under its name, then what matters on it, the green
    /// last), the golfer up on the tee (over the shoulder down the hole, then full length to the camera with their nameplate:
    /// three seconds in which their player may tap an emote — nobody emotes unless they do — and a picked emote is played
    /// out), then the address. Skip ends the flyover only. Online the flyover is skipped only once every golfer has asked
    /// (the host counts the votes; NetworkGolfRound).
    public sealed partial class GolfGame {
        readonly PresentationDirector golfPresentation = new();
        PresentationCut golfVenueCut, golfWalkCut, golfStartCut;
        float lastPracticeSwing=-100;
        bool golfCueLed; int golfBeat; bool golfSkipped, pendingNativeIntro, overviewReturn, introReady;
        System.Numerics.Quaternion? lastReadyGrip;
        bool presentationMotionConsumed;
        /// The intro emote picked on the phone (an emote id), null when none has been: then the golfer just stands.
        string introPick;
        /// The hole this phone has voted to skip the flyover of (online).
        int skipVotedFor=-1;
        HoleIntro holeIntro; IntroGraphics introGraphics; IntroGraphics.Tag introTag; IntroGraphics.Plate introPlate; HoleIntro.Feature introTagFeature;
        int introShot=-1; float introClock;
        string GolfVenueKey => "golf.venue." + course.Key + "." + hole.Number;
        public bool PresentationBusy => Current==State.Intro;
        public bool PresentationPlaying => golfPresentation.Playing;
        /// Which part of the opening is on: 0 the flyover, 1 the golfer's introduction, 2 the address.
        public int PresentationBeatIndex => Current==State.Intro ? golfBeat : -1;
        public float PresentationElapsed => golfPresentation.Elapsed;
        public float PresentationDuration => golfPresentation.Duration;
        /// The flyover's highlights for this hole (in order, the green last).
        public System.Collections.Generic.IReadOnlyList<HoleIntro.Feature> IntroHighlights => holeIntro?.Highlights;
        bool NetworkIntro => networkConfigured&&SportsMultiplayer.Active&&SportsMultiplayer.Instance.GolfState!=null;
        void BeginGolfPresentation() {
            golfPresentation.Cancel(); golfBeat=0; golfSkipped=false; overviewReturn=false; introPick=null; playingIntroEmote=null;
            golfVenueCut=PresentationPolicy.HoleFlyover?PresentationPolicy.Cut(GolfVenueKey):PresentationCut.Off;
            golfWalkCut=PresentationPolicy.Cut("golf.walkon");
            golfStartCut=PresentationPolicy.Cut("golf.start");
            // a multiplayer round plays the cut its host chose, as single player would have (NetworkIntroFull)
            var state=networkConfigured&&SportsMultiplayer.Active?SportsMultiplayer.Instance.GolfState:null;
            if(state!=null&&state.hole==holeIndex) {
                var cut=state.introFull?PresentationCut.Full:PresentationCut.Short;
                golfVenueCut=PresentationPolicy.HoleFlyover?cut:PresentationCut.Off; golfWalkCut=cut; golfStartCut=cut;
            }
            StartIntroGraphics();
            BeginGolfBeat();
        }
        void StartIntroGraphics() {
            hud.HideHoleIntro();   // (the opening's graphics are IntroGraphics' now)
            holeIntro=new HoleIntro(hole); introShot=-1; introClock=0;
            if(!introGraphics && hud.IntroLayer) introGraphics=IntroGraphics.On(hud.IntroLayer);
            introGraphics?.Clear(); introTag=null; introPlate=null;
        }
        void EndIntroGraphics() { introGraphics?.Clear(); introTag=null; introPlate=null; introNamePlate=null; IntroLens.Off(rig.Camera); }
        void BeginGolfBeat() {
            if(golfBeat>2) {
                EndShowcase(); hud.HideNameplate(); hud.HideSkipVotes(); EndIntroGraphics();
                if(overviewReturn) { Current=State.Aim; Swing.Armed=true; if(presentationMotionConsumed) NativeReady(); hud.HideHoleIntro(); hud.ShowPlayHud(true); FrameAim(); RefreshControls(); }
                else { BeginAim(false); if(introReady) NativeReady(presentationMotionConsumed?null:lastReadyGrip); }
                presentationMotionConsumed=false;
                return;
            }
            var beat=golfBeat==0?PresentationBeat.Venue:golfBeat==1?PresentationBeat.WalkOn:PresentationBeat.Start;
            var cut=golfBeat==0?golfVenueCut:golfBeat==1?golfWalkCut:golfStartCut;
            float duration=PresentationDirector.Budget(beat,true,cut);
            golfCueLed=false; int entered=golfBeat;
            if(golfBeat==1) { meeting=cheered=false; introNamePlate=null; hud.HideSkipVotes(); introGraphics?.HideTag(introTag,introClock); introTag=null; introGraphics?.HidePlate(introPlate,introClock); }
            if(golfBeat==2) { introGraphics?.HidePlate(introNamePlate,introClock); IntroLens.Off(rig.Camera); }
            PresentationPolicy.Event(beat.ToString(),"begin",golfPresentation.Sequence+1,cut.ToString());
            // the flyover's first shot is on screen from its first frame (no frame of the old view before the cut)
            if(golfBeat==0 && cut!=PresentationCut.Off) { FrameFlyover(0); rig.ApplyFrame(); }
            golfPresentation.Begin(beat,cut,duration,.3f,()=>{},()=>{
                PresentationPolicy.Event(beat.ToString(),golfSkipped?"skipped":"complete",golfPresentation.Sequence);
                if(!golfSkipped && !overviewReturn && cut==PresentationCut.Full && entered<2) PresentationPolicy.Seen(entered==0?GolfVenueKey:"golf.walkon");
                if(!golfSkipped && !overviewReturn && cut==PresentationCut.Full && entered==2) PresentationPolicy.Seen("golf.start");
                golfBeat=overviewReturn?3:entered+1; BeginGolfBeat();
            },shared:PresentationPolicy.Multiplayer);
        }
        void TickGolfPresentation(float dt) {
            if(Current!=State.Intro)return;
            introClock+=Mathf.Max(0,dt);
            // online: every golfer asked to skip the flyover, so the host moved the golfer's introduction up
            if(golfBeat==0 && NetworkIntro && SportsMultiplayer.Instance.GolfState.introSkip==holeIndex) JumpToIntro();
            if(golfBeat==0) { FrameFlyover(golfPresentation.Elapsed); ShowSkipVotes(); }
            else if(golfBeat==1) MeetThePlayer(golfPresentation.Elapsed);
            else {
                EndShowcase(); hud.HideNameplate(); hud.HideHoleIntro(); hud.ShowPlayHud(true);
                golfer.SetVisible(true); golfer.Stand(ball.position,TeeAim());
                rig.RestoreFov(); rig.ResetZoom(); FrameAim();
            }
            introGraphics?.Tick(introClock);
            RefreshIntroControls();
            if(!golfCueLed && golfBeat<2 && golfPresentation.Duration-golfPresentation.Elapsed<=.15f) {golfCueLed=true;PresentationStinger.Lead();}
            golfPresentation.Tick(dt);
            if(Input.GetMouseButtonDown(0)) SkipPresentation();
        }
        /// The flyover `s` seconds in: HoleIntro's lens, the hole's plate over the opening, a tag on each highlight.
        void FrameFlyover(float s) {
            bool full=golfVenueCut==PresentationCut.Full||overviewReturn;
            holeIntro.Pose(full,s,rig.Camera.aspect,out var at,out var look,out var fov);
            rig.Cue(at,look); rig.SetVerticalFov(fov);
            if(introGraphics==null) return;
            int shot=holeIntro.ShotAt(full,s,out var local,out var len);
            if(shot!=introShot) {
                introShot=shot; introGraphics.HideTag(introTag,introClock); introTag=null;
                if(shot==0) {
                    string wind=Wind.IsCalm?"CALM":Wind.Describe(heading).ToUpperInvariant();
                    introPlate=introGraphics.ShowPlate($"HOLE {hole.PlayNumber}",hole.Name.ToUpperInvariant(),$"PAR {hole.Par}   ·   {hole.Length:F0} YD   ·   {wind}",IntroGraphics.Sky,false,introClock);
                } else {
                    introGraphics.HidePlate(introPlate,introClock);
                    introTagFeature=holeIntro.FeatureOf(shot);
                    introTag=introGraphics.ShowTag(introTagFeature.Label,introTagFeature.Sub,HoleIntro.ColourOf(introTagFeature.Kind),introClock);
                }
            }
            if(shot==0 && local>len-.35f) introGraphics.HidePlate(introPlate,introClock);
            if(introTag!=null) {
                if(local>len-.25f) introGraphics.HideTag(introTag,introClock);
                introGraphics.Pin(introTag,rig.Camera,introTagFeature.At+Vector3.up*(introTagFeature.Kind=="green"?1.6f:1f),introClock);
            }
        }
        /// The host's choice of a hole's intro: single player's (full the first time this phone has seen the hole, short after).
        public bool NetworkIntroFull(int index) => course!=null&&index>=0&&index<course.Holes.Length&&PresentationPolicy.HoleFlyover
            &&PresentationPolicy.CutAlone("golf.venue."+course.Key+"."+course.Holes[index].Number)==PresentationCut.Full;
        /// How long each part of that intro runs, as BeginGolfBeat times it: the flyover, the address; and the golfer's emote
        /// (seat, slot) when their player picks one.
        public double NetworkVenueSeconds(int index,bool full) => PresentationPolicy.HoleFlyover?PresentationDirector.Budget(PresentationBeat.Venue,true,full?PresentationCut.Full:PresentationCut.Short):0;
        public double NetworkStartSeconds(bool full) => PresentationDirector.Budget(PresentationBeat.Start,true,full?PresentationCut.Full:PresentationCut.Short);
        public double NetworkEmoteSeconds(int seat,int slot) => IntroEmoteSeconds(NetworkIntroEmote(seat,slot),seat);

        // ----- The golfer's intro emote -----

        /// The emote the golfer up plays on the tee: the one their player picked (online, as the host has it), else none.
        string IntroEmote {
            get {
                if(NetworkIntro) { var s=SportsMultiplayer.Instance.GolfState; return s.introSlot>=0?NetworkIntroEmote(s.turn,s.introSlot):null; }
                return introPick;
            }
        }
        string NetworkIntroEmote(int seat,int slot) {
            var ids=NetworkEmotesOf(seat);
            return ids[Mathf.Clamp(slot<0?0:slot,0,ids.Length-1)];
        }
        /// A golfer's three equipped emotes in a multiplayer round (their own locker's, as the lobby has them).
        static string[] NetworkEmotesOf(int seat) => TennisEmotes.Normalize(SportsMultiplayer.Instance?.Configuration?.participants?.FirstOrDefault(p=>p.seat==seat)?.loadout?.emotes);
        /// That emote's clip on the golfer (seconds once through); the tennis length when the figure has no such clip.
        float IntroEmoteSeconds(string id,int seat=-1) {
            var figure=seat>=0&&partyGolfers.TryGetValue(seat,out var party)&&party?party:golfer;
            float length=figure?figure.MoveLength(TennisEmotes.ExportName(id)??""):0;
            return length>0?length:TennisEmotes.Duration(id);
        }
        string playingIntroEmote;
        /// Start the golfer's picked intro emote (Wave when the figure has no clip for it).
        void PlayIntroEmote() {
            playingIntroEmote=IntroEmote;
            string move=TennisEmotes.ExportName(playingIntroEmote);
            if(move==null || !golfer.HasMove(move) || !golfer.Perform(move,loop:false)) golfer.Perform("Wave");
        }
        /// Seconds into the golfer's introduction their three-second window closes.
        static float PickCloses => PresentationDirector.GolfOverShoulder+PresentationDirector.IntroWindow;
        /// "intro" while the golfer up can still pick the emote they greet the gallery with (the phone shows their equipped
        /// three): through the flyover and their own three seconds on the tee.
        public string IntroEmoteWindow {
            get {
                if(Current!=State.Intro || overviewReturn || golfWalkCut==PresentationCut.Off || golfBeat>1 || (golfBeat==1 && golfPresentation.Elapsed>=PickCloses)) return "";
                if(NetworkIntro) {
                    var net=SportsMultiplayer.Instance; var s=net.GolfState;
                    return s.phase=="intro" && s.introSlot<0 && net.Configuration.Controls(net.Configuration.localID,s.turn) ? "intro" : "";
                }
                return introPick==null ? "intro" : "";
            }
        }
        /// The phone's emote button during the opening: that emote is the golfer's intro (once a hole).
        public bool ChooseIntroEmote(int slot) {
            if(slot<0 || slot>2 || IntroEmoteWindow!="intro") return false;
            if(NetworkIntro) {
                var net=SportsMultiplayer.Instance; var s=net.GolfState;
                net.Submit(new NetworkInput{action="golfIntroEmote",value=slot,actorSeat=s.turn});
                return true;
            }
            introPick=resultEmotes[slot];
            return true;
        }

        // ----- Skipping -----

        public void SkipPresentation() {
            if(Current!=State.Intro || golfPresentation.Elapsed<.3f && golfBeat==0) return;
            // a multiplayer round: this phone's vote; the flyover ends once every golfer has voted
            if(NetworkIntro) { VoteSkipIntro(); return; }
            if(PresentationPolicy.Multiplayer) return;
            if(golfBeat==2) { if(Time.unscaledTime-lastPracticeSwing>.6f) {lastPracticeSwing=Time.unscaledTime;golfer.Strike();} return; }
            // the golfer's introduction always plays: skip ends the flyover only
            if(golfBeat==1) return;
            golfSkipped=true; JumpToIntro();
        }
        /// From the flyover straight to the golfer's introduction (or back to the shot, from the overview).
        void JumpToIntro() {
            golfPresentation.Cancel(); PresentationStinger.Stop(); EndShowcase(); hud.HideSkipVotes();
            introGraphics?.HidePlate(introPlate,introClock); introGraphics?.HideTag(introTag,introClock); introTag=null;
            PresentationPolicy.Event("Venue","skip",golfPresentation.Sequence);
            golfBeat=overviewReturn?3:1; BeginGolfBeat();
        }
        void VoteSkipIntro() {
            var net=SportsMultiplayer.Instance; var s=net.GolfState;
            if(s.phase!="intro" || golfBeat!=0 || skipVotedFor==s.hole) return;
            var me=net.Configuration.localID;
            int seat=net.Configuration.Controls(me,s.turn)?s.turn:net.Configuration.participants.Where(p=>p.seat>=0&&net.Configuration.Controls(me,p.seat)).Select(p=>p.seat).DefaultIfEmpty(-1).First();
            if(seat<0) return;   // (a spectator has no say)
            skipVotedFor=s.hole;
            net.Submit(new NetworkInput{action="golfSkipIntro",value=s.hole,actorSeat=seat});
        }
        /// Online, how many golfers have asked to skip this hole's flyover, of how many must.
        public (int voted,int needed,bool mine) SkipVotes {
            get {
                if(!NetworkIntro) return (0,0,false);
                var s=SportsMultiplayer.Instance.GolfState;
                var playing=s.golfers.Where(g=>!g.dnf).ToArray();
                return (playing.Count(g=>g.skipVote==s.hole),playing.Length,skipVotedFor==s.hole);
            }
        }
        /// For the phone's skip button: "" when there is nothing to skip (the golfer's introduction always plays), else
        /// "voted|needed|mine" (alone: "0|1|0").
        public string IntroSkipState {
            get {
                if(Current!=State.Intro || golfBeat!=0 || (PresentationPolicy.Multiplayer && !NetworkIntro)) return "";
                var v=SkipVotes; return v.needed>0?$"{v.voted}|{v.needed}|{(v.mine?1:0)}":"0|1|0";
            }
        }
        void ShowSkipVotes() {
            var v=SkipVotes;
            if(v.needed>0 && v.voted>0 && v.voted<v.needed) hud.ShowSkipVotes(v.voted,v.needed); else hud.HideSkipVotes();
        }
        /// The phone sheet during the opening: skip (while the flyover plays; online with who is waiting) and the intro emotes.
        void RefreshIntroControls() {
            var sheet=hud!=null?hud.Controller:null;
            if(sheet==null || !sheet.Alive || !sheet.ShowingIntro) return;
            string skip=null;
            if(golfBeat==0) {
                var v=SkipVotes;
                skip=v.needed==0?"SKIP INTRO  ▶":v.mine?$"WAITING  ·  {v.voted}/{v.needed}":$"SKIP INTRO  ·  {v.voted}/{v.needed}";
            }
            int picked=-1;
            if(NetworkIntro) picked=SportsMultiplayer.Instance.GolfState.introSlot; else if(introPick!=null) picked=System.Array.IndexOf(resultEmotes,introPick);
            bool mine=!NetworkIntro || SportsMultiplayer.Instance.Configuration.Controls(SportsMultiplayer.Instance.Configuration.localID,SportsMultiplayer.Instance.GolfState.turn);
            var emotes=NetworkIntro?NetworkEmotesOf(SportsMultiplayer.Instance.GolfState.turn):resultEmotes;
            sheet.SetIntroControls(skip,mine&&!overviewReturn?emotes.Select(TennisEmotes.Name).ToArray():null,IntroEmoteWindow=="intro",picked);
        }

        public void Overview() {
            if(Current!=State.Aim || PresentationPolicy.Multiplayer)return;
            overviewReturn=true; golfSkipped=false; golfBeat=0; golfVenueCut=PresentationCut.Full;
            StartIntroGraphics();
            Current=State.Intro; Swing.Armed=false; BeginGolfBeat(); RefreshControls();
        }
        public void TryNativePresentation() {
            if(!pendingNativeIntro || !NativeShotReady || PresentationPolicy.Multiplayer)return;
            pendingNativeIntro=false; introReady=NativeShotReady;
            // Setup has completed at the original address; no scoring or turn changes occur.
            meeting=cheered=false; golfer.SetVisible(false); hud.ShowPlayHud(false);
            Enter(State.Intro); BeginGolfPresentation(); RefreshControls();
        }
        void OnDisable() { golfPresentation.Cancel(); }
    }
}
