using UnityEngine;
namespace GolfArcade.Game {
    public sealed partial class GolfGame {
        readonly PresentationDirector golfPresentation = new();
        PresentationCut golfVenueCut, golfWalkCut, golfStartCut;
        float lastPracticeSwing=-100;
        bool golfCueLed; int golfBeat; bool golfSkipped, pendingNativeIntro, overviewReturn, introReady;
        System.Numerics.Quaternion? lastReadyGrip;
        bool presentationMotionConsumed;
        string GolfVenueKey => "golf.venue." + course.Key + "." + hole.Number;
        public bool PresentationBusy => Current==State.Intro;
        public bool PresentationPlaying => golfPresentation.Playing;
        void BeginGolfPresentation() {
            golfPresentation.Cancel(); golfBeat=0; golfSkipped=false; overviewReturn=false;
            golfVenueCut=PresentationPolicy.HoleFlyover?PresentationPolicy.Cut(GolfVenueKey):PresentationCut.Off;
            golfWalkCut=PresentationPolicy.Cut("golf.walkon");
            golfStartCut=PresentationPolicy.Cut("golf.start");
            BeginGolfBeat();
        }
        void BeginGolfBeat() {
            if(golfBeat>2) {
                EndShowcase(); hud.HideNameplate();
                if(overviewReturn) { Current=State.Aim; Swing.Armed=true; if(presentationMotionConsumed) NativeReady(); hud.HideHoleIntro(); hud.ShowPlayHud(true); FrameAim(); RefreshControls(); }
                else { BeginAim(false); if(introReady) NativeReady(presentationMotionConsumed?null:lastReadyGrip); }
                presentationMotionConsumed=false;
                return;
            }
            var beat=golfBeat==0?PresentationBeat.Venue:golfBeat==1?PresentationBeat.WalkOn:PresentationBeat.Start;
            var cut=golfBeat==0?golfVenueCut:golfBeat==1?golfWalkCut:golfStartCut;
            float duration=PresentationDirector.Budget(beat,true,cut);
            if(golfBeat==0 && holeIndex>0 && cut==PresentationCut.Full) duration=3;
            golfCueLed=false; int entered=golfBeat;
            PresentationPolicy.Event(beat.ToString(),"begin",golfPresentation.Sequence+1,cut.ToString());
            golfPresentation.Begin(beat,cut,duration,.3f,()=>{},()=>{
                PresentationPolicy.Event(beat.ToString(),golfSkipped?"skipped":"complete",golfPresentation.Sequence);
                if(!golfSkipped && !overviewReturn && cut==PresentationCut.Full && entered<2) PresentationPolicy.Seen(entered==0?GolfVenueKey:"golf.walkon");
                if(!golfSkipped && !overviewReturn && cut==PresentationCut.Full && entered==2) PresentationPolicy.Seen("golf.start");
                golfBeat=overviewReturn?3:entered+1; BeginGolfBeat();
            },shared:PresentationPolicy.Multiplayer);
        }
        void TickGolfPresentation(float dt) {
            if(Current!=State.Intro)return;
            if(golfBeat==0) {
                rig.Showcase(hole,golfPresentation.Fraction);
                hud.SetHoleIntroAlpha(1);
            } else if(golfBeat==1) {
                MeetThePlayer(golfPresentation.Elapsed);
            } else {
                EndShowcase(); hud.HideNameplate(); hud.HideHoleIntro(); hud.ShowPlayHud(true);
                golfer.SetVisible(true); golfer.Stand(ball.position,TeeAim());
                rig.RestoreFov(); rig.ResetZoom(); FrameAim();
            }
            if(!golfCueLed && golfBeat<2 && golfPresentation.Duration-golfPresentation.Elapsed<=.15f) {golfCueLed=true;PresentationStinger.Lead();}
            golfPresentation.Tick(dt);
            if(Input.GetMouseButtonDown(0)) SkipPresentation();
        }
        public void SkipPresentation() {
            if(Current!=State.Intro || PresentationPolicy.Multiplayer || golfPresentation.Elapsed<.3f)return;
            if(golfBeat==2) { if(Time.unscaledTime-lastPracticeSwing>.6f) {lastPracticeSwing=Time.unscaledTime;golfer.Strike();} return; }
            golfSkipped=true; golfPresentation.Cancel(); PresentationStinger.Stop(); EndShowcase();
            golfBeat=overviewReturn?3:2; BeginGolfBeat();
        }
        public void Overview() {
            if(Current!=State.Aim || NativeCalibrating || PresentationPolicy.Multiplayer)return;
            overviewReturn=true; golfSkipped=false; golfBeat=0; golfVenueCut=PresentationCut.Full;
            hud.ShowHoleIntro(Tournament,hole.Number,hole.Name,hole.Par,hole.Length,Wind.Describe(heading));
            Current=State.Intro; Swing.Armed=false; BeginGolfBeat(); RefreshControls();
        }
        public void TryNativePresentation() {
            if(!pendingNativeIntro || NativeCalibrating || !NativeShotReady || PresentationPolicy.Multiplayer)return;
            pendingNativeIntro=false; introReady=NativeShotReady;
            // Setup has completed at the original address; no scoring or turn changes occur.
            meeting=cheered=false; golfer.SetVisible(false); hud.ShowPlayHud(false);
            hud.ShowHoleIntro(Tournament,hole.Number,hole.Name,hole.Par,hole.Length,Wind.Describe(heading));
            Enter(State.Intro); BeginGolfPresentation(); RefreshControls();
        }
        void OnDisable() { golfPresentation.Cancel(); }
    }
}
