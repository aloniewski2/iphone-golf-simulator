using GolfArcade.Game;
using UnityEngine;
using UnityEngine.UI;

namespace GolfArcade.Tennis
{
    /// The pre-match show, TV style, before the first serve:
    ///
    ///   1. drone flyover: in from over the sea, around the resort, down to the court, under
    ///      a TROPICAL OPEN title card
    ///   2. the rival's intro: a close shot, their name card, their signature emote
    ///   3. the player's intro, the same
    ///   4. the umpire calls play; the camera swoops down into the gameplay view
    ///
    /// Letterbox bars frame it as a broadcast. Any swing (or a tap / Space) skips straight to
    /// play. Runs on game time, so it waits while the session is paused.
    public sealed class TennisPresentation : MonoBehaviour
    {
        public const float FullLength = 7.2f;
        public const float Drone=3, RivalIntro=1.2f, PlayerIntro=1.2f, Umpire=.6f, Swoop=1.2f;
        float drone = 3f, rivalIntro = 1.2f, playerIntro = 1.2f, umpireSeconds = .6f, swoop = 1.2f;
        public const float Length = FullLength;
        float TotalLength => drone + rivalIntro + playerIntro + umpireSeconds + swoop;
        readonly PresentationDirector director = new();
        int beatIndex; bool skipped, cueLed;
        PresentationCut venueCut, walkCut, startCut;
        string VenueKey => "tennis.venue." + TennisVenue.Selected;
        string WalkKey => "tennis.walkon";
        public void RestartShared() {
            director.Cancel(); t=0; skipped=false; rivalEmoted=playerEmoted=umpireCalled=chosenPlayed=false;
            chosenEmote=null; shownName=null; Configure(true); hud.MatchVisible=false;
        }
        void Configure(bool shared = false) {
            venueCut = shared ? PresentationCut.Short : PresentationPolicy.Cut(VenueKey);
            walkCut = shared ? PresentationCut.Short : PresentationPolicy.Cut(WalkKey);
            startCut = shared ? PresentationCut.Short : PresentationPolicy.Cut("tennis.start");
            drone=PresentationDirector.Budget(PresentationBeat.Venue,false,venueCut);
            float walk=PresentationDirector.Budget(PresentationBeat.WalkOn,false,walkCut);
            rivalIntro=walk*.4f; playerIntro=walk*.4f; umpireSeconds=walk*.2f;
            swoop=PresentationDirector.Budget(PresentationBeat.Start,false,startCut);
            beatIndex=0; BeginBeat();
        }
        void BeginBeat() {
            if(beatIndex>2) { if(practiced && game.Player) {game.Player.CancelSwing();practiced=false;} t=TotalLength; SetBars(0); hud.MatchVisible=true; return; }
            var beat=beatIndex==0?PresentationBeat.Venue:beatIndex==1?PresentationBeat.WalkOn:PresentationBeat.Start;
            var cut=beatIndex==0?venueCut:beatIndex==1?walkCut:startCut;
            float duration=beatIndex==0?drone:beatIndex==1?rivalIntro+playerIntro+umpireSeconds:swoop;
            cueLed=false; settleCaptured=false; int entered=beatIndex;
            PresentationPolicy.Event(beat.ToString(),"begin",director.Sequence+1,cut.ToString());
            director.Begin(beat,cut,duration,.3f,()=>{},()=> {
                if(!skipped) PresentationPolicy.Event(beat.ToString(),"complete",director.Sequence);
                if(!skipped && cut==PresentationCut.Full && entered<2) PresentationPolicy.Seen(entered==0?VenueKey:WalkKey);
                if(!skipped && cut==PresentationCut.Full && entered==2) PresentationPolicy.Seen("tennis.start");
                beatIndex=entered+1; BeginBeat();
            }, shared:PresentationPolicy.Multiplayer);
        }

        TennisGame game;
        TennisHud hud;
        TennisUmpire umpire;
        float t;
        bool settleCaptured, practiced;
        HeroTennisDriver.Clip? chosenEmote;
        bool chosenPlayed;
        public bool CanChooseEmote => Playing && t < drone + rivalIntro + playerIntro && !chosenEmote.HasValue;
        public bool ChooseEmote(HeroTennisDriver.Clip clip) {
            if (!CanChooseEmote) return false;
            chosenEmote = clip; return true;
        }
        bool rivalEmoted, playerEmoted, umpireCalled;
        RectTransform barTop, barBottom, titleCard, nameCard;
        Text title, subtitle, nameText, roleText;
        UiGradient nameGradient;
        Vector3 lastPos; Quaternion lastRot; float lastFov = 50;

        public bool Playing => t < TotalLength;
        public float Remaining => Mathf.Max(0, TotalLength - t);

        public void Build(TennisGame owner, TennisHud hudOwner, TennisUmpire chairUmpire)
        {
            game = owner; hud = hudOwner; umpire = chairUmpire;
            var root = hud.Root;
            barTop = Bar(root, 1); barBottom = Bar(root, 0);

            var titleBody = hud.Chunky("Title card", root, new Vector2(.5f, .5f), new Vector2(560, 96), new Vector2(0, 150), TennisHud.SkyTop, TennisHud.SkyBottom, 5);
            titleCard = (RectTransform)titleBody.transform.parent;
            title = hud.Label(titleBody.rectTransform, TennisVenue.Title, 58, new Vector2(0, 8), new Vector2(560, 70), Color.white, TextAnchor.MiddleCenter, 3f);
            UiGradient.On(title, TennisHud.GoldTop, TennisHud.GoldBottom);
            var subBody = hud.Chunky("Title ribbon", titleCard, new Vector2(.5f, 0), new Vector2(300, 30), new Vector2(0, -6), TennisHud.GoldTop, TennisHud.GoldBottom, 3);
            subtitle = hud.Label(subBody.rectTransform, TennisVenue.CourtName + "  ·  " + TennisVenue.Session, 15, new Vector2(0, 1), new Vector2(300, 30), TennisHud.Navy, TextAnchor.MiddleCenter, 0);

            var nameBody = hud.Chunky("Name card", root, new Vector2(0, 0), new Vector2(420, 92), new Vector2(0, 130), TennisHud.SunTop, TennisHud.SunBottom, 5);
            nameCard = (RectTransform)nameBody.transform.parent;
            nameGradient = nameBody.GetComponent<UiGradient>();
            nameText = hud.Label(nameBody.rectTransform, "", 60, new Vector2(0, 6), new Vector2(420, 70), Color.white, TextAnchor.MiddleCenter, 3f);
            var roleBody = hud.Chunky("Role ribbon", nameCard, new Vector2(.5f, 0), new Vector2(240, 30), new Vector2(0, -8), new Color(.16f, .24f, .55f), TennisHud.NavyDeep, 3);
            roleText = hud.Label(roleBody.rectTransform, "", 16, new Vector2(0, 1), new Vector2(240, 30), Color.white, TextAnchor.MiddleCenter, 1.2f);
            titleCard.gameObject.SetActive(false); nameCard.gameObject.SetActive(false);
            hud.MatchVisible = false;
            Configure();
        }

        static RectTransform Bar(RectTransform root, float edge)
        {
            var img = new GameObject("Letterbox").AddComponent<Image>();
            img.transform.SetParent(root, false); img.color = new Color(.01f, .02f, .06f, 1); img.raycastTarget = false;
            var rt = img.rectTransform; rt.anchorMin = new Vector2(0, edge); rt.anchorMax = new Vector2(1, edge); rt.pivot = new Vector2(.5f, edge);
            rt.sizeDelta = new Vector2(0, 0);
            return rt;
        }

        /// End at once (autoplay, tests): no cards, no bars, the match HUD up.
        public void Finish()
        {
            if (!Playing) return;
            director.Cancel(); t = TotalLength;

            titleCard.gameObject.SetActive(false); nameCard.gameObject.SetActive(false);
            SetBars(0); hud.MatchVisible = true;
        }

        /// Jump to the swoop into play.
        public void Skip() {
            if(!Playing || PresentationPolicy.Multiplayer || t<.3f) return;
            if(beatIndex==2) { if(game.Player && !game.Player.Swinging) { practiced=true; game.Player.Swing(.2f,false); } return; }
            skipped=true; director.Cancel(); PresentationStinger.Stop();
            titleCard.gameObject.SetActive(false); nameCard.gameObject.SetActive(false);
            // Skip input is consumed by the caller. Retain a camera settle before the serve.
            drone=rivalIntro=playerIntro=umpireSeconds=0; swoop=.3f; t=0; beatIndex=2; BeginBeat();
            PresentationPolicy.Event("Intro","skip",director.Sequence);
        }

        static float Smooth(float x) => x * x * (3 - 2 * x);
        static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float k)
        {
            float u = 1 - k;
            return u * u * u * a + 3 * u * u * k * b + 3 * u * k * k * c + k * k * k * d;
        }

        /// Drives the camera while playing; returns false once play has begun.
        public bool Drive(Camera camera, Vector3 playTarget, Vector3 playLook, float playFov, float dt)
        {
            if (!Playing) return false;
            if(!cueLed && beatIndex<2 && director.Duration-director.Elapsed<=.15f) { cueLed=true; PresentationStinger.Lead(); }
            director.Tick(dt);
            t = beatIndex==0 ? director.Elapsed : beatIndex==1 ? drone+director.Elapsed : beatIndex==2 ? drone+rivalIntro+playerIntro+umpireSeconds+director.Elapsed : TotalLength;
            Vector3 pos, look; float fov;
            var player = game.Player.transform; var rival = game.Opponent.transform;
            float bars = 1;

            if (t < drone)
            {
                // In from the sea behind the far baseline, banking round the resort, settling
                // high over the court's side.
                float k = Smooth(Mathf.Clamp01(t / drone));
                // High over the bay first, so the whole island reads, then banking low over
                // the jungle and in over the stands.
                // (each venue flies its own path: TennisVenue.drone)
                TennisVenue.Drone(k, out pos, out look, out fov);
                Card(titleCard, t, drone, fromLeft: false);
            }
            else if (t < drone + rivalIntro)
            {
                float s = t - drone, k = Smooth(Mathf.Clamp01(s / rivalIntro));
                Vector3 face = rival.forward;
                Vector3 side = Vector3.Cross(Vector3.up, face);
                // Full body, head to toe, from chest height (a camera above the chest
                // foreshortens the trunk), with a slow push in: the whole emote reads.
                pos = rival.position + face * Mathf.Lerp(5.6f, 4.7f, k) + side * Mathf.Lerp(-1.6f, -1.0f, k) + Vector3.up * 1.25f;
                look = rival.position + Vector3.up * 1.0f;
                fov = 40;
                if (!rivalEmoted && s > .35f)
                {
                    rivalEmoted = true; game.Opponent.PlayIntro();
                    var driver = game.Opponent.GetComponentInChildren<HeroTennisDriver>();

                }
                ShowName(hud.OpponentName, rivalRole, TennisHud.SeaTop, TennisHud.SeaBottom);
                Card(nameCard, s, rivalIntro, fromLeft: false);
            }
            else if (t < drone + rivalIntro + playerIntro)
            {
                float s = t - drone - rivalIntro, k = Smooth(Mathf.Clamp01(s / playerIntro));
                Vector3 face = player.forward;
                Vector3 side = Vector3.Cross(Vector3.up, face);
                // Full body, head to toe, from chest height (a camera above the chest
                // foreshortens the trunk), with a slow push in: the whole emote reads.
                pos = player.position + face * Mathf.Lerp(5.6f, 4.7f, k) + side * Mathf.Lerp(1.6f, 1.0f, k) + Vector3.up * 1.25f;
                look = player.position + Vector3.up * 1.0f;
                fov = 40;
                if (chosenEmote.HasValue && !chosenPlayed) {
                    var driver = game.Player.GetComponentInChildren<HeroTennisDriver>();
                    if (driver && driver.PlayEmote(chosenEmote.Value)) {
                        chosenPlayed = true; playerEmoted = true;

                    }
                }
                if (!chosenEmote.HasValue && !playerEmoted && s > .35f) {
                    playerEmoted = true;
                    game.PlayEquippedIntro();
                }
                ShowName(hud.PlayerName, "THE CHALLENGER", TennisHud.SunTop, TennisHud.SunBottom);
                Card(nameCard, s, playerIntro, fromLeft: true);
            }
            else if (t < TotalLength - swoop)
            {
                // The umpire, from the court, as he calls play.
                float s = t - (TotalLength - swoop - umpireSeconds);
                pos = new Vector3(0, 4.5f, -15);
                look = Vector3.Lerp(player.position, rival.position, .5f) + Vector3.up;
                fov = 48;
                nameCard.gameObject.SetActive(false);
                if (!umpireCalled) { umpireCalled = true; if (umpire) umpire.Call(); hud.ShowCall("PLAY", null, true); }
            }
            else
            {
                if(!settleCaptured) { settleCaptured=true; lastPos=camera.transform.position; lastRot=camera.transform.rotation; lastFov=camera.fieldOfView; if(lastRot==default(Quaternion)) lastRot=Quaternion.LookRotation(playLook-playTarget); }
                // swoop from wherever the show was into the gameplay camera.
                float s = Mathf.Clamp01((t - (TotalLength - swoop)) / swoop), k = Smooth(s);
                if (!umpireCalled) { umpireCalled = true; hud.ShowCall("PLAY", null, true); }
                titleCard.gameObject.SetActive(false); nameCard.gameObject.SetActive(false);
                Vector3 high = playTarget + new Vector3(0, 6, -6);
                pos = Bezier(lastPos, lastPos + Vector3.up * 2, high, playTarget, k);
                camera.transform.position = pos;
                camera.transform.rotation = Quaternion.Slerp(lastRot, Quaternion.LookRotation(playLook - playTarget), k);
                camera.fieldOfView = Mathf.Lerp(lastFov, playFov, k);
                bars = 1 - k;
                SetBars(bars);
                if (t >= TotalLength) { hud.MatchVisible = true; SetBars(0); }
                return true;
            }
            camera.transform.position = lastPos = pos;
            camera.transform.rotation = lastRot = Quaternion.LookRotation(look - pos);
            camera.fieldOfView = lastFov = fov;
            SetBars(Mathf.Clamp01(t / .4f) * bars);
            return true;
        }

        void OnDisable() { director.Cancel(); }
        void SetBars(float amount)
        {
            float h = 74 * amount;
            barTop.sizeDelta = new Vector2(0, h); barBottom.sizeDelta = new Vector2(0, h);
        }

        string rivalRole = "THE RIVAL";
        /// Bill the match: the round under the title card, and the rival's billing on their
        /// name card ("QUARTERFINAL", or "THE CHAMPION" for the boss).
        public void Bill(string round, string role)
        {
            if (subtitle) subtitle.text = TennisVenue.CourtName + "  ·  " + round;
            rivalRole = role;
        }

        string shownName;
        void ShowName(string name, string role, Color top, Color bottom)
        {
            if (shownName == name) return;
            shownName = name;
            nameText.text = name; roleText.text = role;
            nameGradient.Top = top; nameGradient.Bottom = bottom; nameGradient.GetComponent<Graphic>().SetVerticesDirty();
        }

        /// Slide a card in with a springy overshoot, hold, slide it out.
        void Card(RectTransform card, float s, float length, bool fromLeft)
        {
            card.gameObject.SetActive(s < length - .05f);
            float width = hud.Root.rect.width;
            float enter = Mathf.Clamp01(s / Mathf.Min(.2f, length*.2f)), exit = Mathf.Clamp01((s - (length - .2f)) / .2f);
            float spring = enter < 1 ? 1 - Mathf.Exp(-enter * 7) * Mathf.Cos(enter * 11) : 1;
            float side = fromLeft ? -1 : 1;
            bool title = card == titleCard;
            float restX = title ? 0 : side * (width * .5f - 260) + width * .5f;
            float offX = title ? 0 : side * (width * .5f + 300) + width * .5f;
            float x = Mathf.LerpUnclamped(offX, restX, spring) + side * exit * 700;
            if (title)
            {
                card.anchoredPosition = new Vector2(0, 150);
                card.localScale = Vector3.one * (enter < 1 ? Mathf.LerpUnclamped(.3f, 1, spring) : 1) * (1 - exit);
                card.localRotation = Quaternion.Euler(0, 0, (1 - Mathf.Clamp01(spring)) * -8);
            }
            else
            {
                card.anchoredPosition = new Vector2(x, 130);
                card.localRotation = Quaternion.Euler(0, 0, side * (1 - Mathf.Clamp01(spring)) * 10);
            }
        }
    }
}
