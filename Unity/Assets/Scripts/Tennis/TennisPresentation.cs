using GolfArcade.Game;
using GolfArcade.UI;
using UnityEngine;

namespace GolfArcade.Tennis
{
    /// The pre-match show, before the first serve:
    ///
    ///   1. a short drone flyover in to the court, the event's plate popping up low left
    ///   2. each player in turn, full length to the camera at eye level (long lens, the venue soft behind) with their
    ///      plate: three seconds in which their player may tap an emote on the phone — nobody emotes unless they do, and
    ///      a picked emote is played out (the beat runs on to its end)
    ///   3. the stare-down, eye to eye: the rival close, then you, both names low, a VS between
    ///   4. the umpire calls play; the camera swoops down into the gameplay view
    ///
    /// Alone, the rival goes first; in a versus match seat 0 goes first on both phones, so each player's pick shows on both
    /// screens in the same beat. A swing (or a tap / Space) skips the flyover only; in versus both players must ask.
    /// Runs on game time, so it waits while the session is paused.
    public sealed class TennisPresentation : MonoBehaviour
    {
        /// The full show without emotes picked.
        public const float FullLength = 3.2f + 2 * PresentationDirector.IntroWindow + PresentationDirector.TennisStare + 1.2f;
        public const float Length = FullLength;
        float drone = 3.2f, window = PresentationDirector.IntroWindow, stare = PresentationDirector.TennisStare, swoop = 1.2f;
        /// When each introduction ends (seconds into the introductions): the first, then the second.
        float firstEnd, secondEnd;
        float WalkLength => secondEnd + stare;
        float TotalLength => drone + WalkLength + swoop;
        readonly PresentationDirector director = new();
        int beatIndex; bool skipped, cueLed, sharedCut;
        PresentationCut venueCut, walkCut, startCut;
        string VenueKey => "tennis.venue." + TennisVenue.Selected;
        string WalkKey => "tennis.walkon";
        /// VERSUS: the intro again from the top, chosen as single player chooses it (full the first time, then the short cut).
        public void Restart() { Reset(); introRound++; localSkipVote = false; Configure(false); hud.MatchVisible = false; }
        public void RestartShared() { Reset(); Configure(true); hud.MatchVisible = false; }
        void Reset()
        {
            director.Cancel(); t = 0; skipped = false; rivalEmoted = playerEmoted = umpireCalled = chosenPlayed = false;
            chosenEmote = null; playerStarted = rivalStarted = null; shown = -1; graphics?.Clear(); plate = left = right = null;
            if (game && game.GameplayCamera) IntroLens.Off(game.GameplayCamera);
        }
        void Configure(bool shared = false)
        {
            venueCut = shared ? PresentationCut.Short : PresentationPolicy.Cut(VenueKey);
            walkCut = shared ? PresentationCut.Short : PresentationPolicy.Cut(WalkKey);
            startCut = shared ? PresentationCut.Short : PresentationPolicy.Cut("tennis.start");
            drone = PresentationDirector.Budget(PresentationBeat.Venue, false, venueCut);
            bool intros = walkCut != PresentationCut.Off;
            window = intros ? PresentationDirector.IntroWindow : 0; stare = intros ? PresentationDirector.TennisStare : 0;
            swoop = PresentationDirector.Budget(PresentationBeat.Start, false, startCut);
            // the legacy host-rules match (NetworkTennisMatch) caps its shared intro at 3.5 s: it keeps a quick cut
            sharedCut = shared;
            if (shared) { drone = 1.2f; window = .6f; stare = .3f; swoop = .8f; }
            firstEnd = window; secondEnd = 2 * window;
            beatIndex = 0; BeginBeat();
        }
        void BeginBeat()
        {
            if (beatIndex > 2) { if (practiced && game.Player) { game.Player.CancelSwing(); practiced = false; } t = TotalLength; Wrap(); hud.MatchVisible = true; return; }
            var beat = beatIndex == 0 ? PresentationBeat.Venue : beatIndex == 1 ? PresentationBeat.WalkOn : PresentationBeat.Start;
            var cut = beatIndex == 0 ? venueCut : beatIndex == 1 ? walkCut : startCut;
            // versus: this phone has seen its flyover through, so it no longer holds the other phone's skip back
            if (beatIndex >= 1 && game.Versus && !localSkipVote) { localSkipVote = true; game.VersusSkipVote(introRound); }
            float duration = beatIndex == 0 ? drone : beatIndex == 1 ? WalkLength : swoop;
            cueLed = false; settleCaptured = false; int entered = beatIndex;
            PresentationPolicy.Event(beat.ToString(), "begin", director.Sequence + 1, cut.ToString());
            director.Begin(beat, cut, duration, .3f, () => { }, () => {
                if (!skipped) PresentationPolicy.Event(beat.ToString(), "complete", director.Sequence);
                if (!skipped && cut == PresentationCut.Full && entered < 2) PresentationPolicy.Seen(entered == 0 ? VenueKey : WalkKey);
                if (!skipped && cut == PresentationCut.Full && entered == 2) PresentationPolicy.Seen("tennis.start");
                beatIndex = entered + 1; BeginBeat();
            }, shared: PresentationPolicy.Multiplayer);
        }

        TennisGame game;
        TennisHud hud;
        TennisUmpire umpire;
        IntroGraphics graphics; IntroGraphics.Plate plate, left, right; int shown = -1;
        float t;
        bool settleCaptured, practiced;
        HeroTennisDriver.Clip? chosenEmote;
        bool chosenPlayed, rivalEmoted, playerEmoted, umpireCalled;
        /// When each player's emote started (seconds into their own introduction), null while they have not.
        float? playerStarted, rivalStarted;
        Vector3 lastPos; Quaternion lastRot; float lastFov = 50;

        /// Alone the rival goes first; in versus seat 0 goes first on both phones.
        bool RivalFirst => !game.Versus || game.VersusSeat == 1;
        /// Seconds into the introductions the player's own begins.
        float PlayerFrom => RivalFirst ? firstEnd : 0;
        /// The player may pick their emote through the flyover and their own three seconds.
        public bool CanChooseEmote => Playing && !chosenEmote.HasValue && window > 0
            && (beatIndex == 0 || beatIndex == 1 && director.Elapsed < PlayerFrom + window);
        public bool ChooseEmote(HeroTennisDriver.Clip clip) { if (!CanChooseEmote) return false; chosenEmote = clip; return true; }
        static float EmoteSeconds(TennisActor actor, HeroTennisDriver.Clip clip)
        {
            var driver = actor ? actor.GetComponentInChildren<HeroTennisDriver>() : null;
            float length = driver ? driver.EmoteDuration(clip) : 0;
            return length > 0 ? length : 2.5f;
        }

        // versus: the flyover is skipped once both phones have asked (or seen it through); the introductions still play
        int introRound, remoteSkipRound = -1; bool localSkipVote;
        bool RemoteSkipVote => remoteSkipRound >= introRound;
        /// The other phone asked to skip (or finished) the flyover of its intro number `round`.
        public void RemoteSkip(int round) { remoteSkipRound = Mathf.Max(remoteSkipRound, round); TryVersusSkip(); }
        void TryVersusSkip() { if (Playing && beatIndex == 0 && localSkipVote && RemoteSkipVote) { skipped = true; JumpToIntros(); } }
        /// The drone flyover is on (the only part a skip ends).
        public bool InFlyover => Playing && beatIndex == 0;
        /// Versus, during the flyover: who has asked to skip it (both must).
        public (int voted, int needed, bool mine) SkipVotes => game.Versus && Playing && beatIndex == 0
            ? ((localSkipVote ? 1 : 0) + (RemoteSkipVote ? 1 : 0), 2, localSkipVote) : (0, 0, false);

        public bool Playing => t < TotalLength;
        public float Remaining => Mathf.Max(0, TotalLength - t);

        public void Build(TennisGame owner, TennisHud hudOwner, TennisUmpire chairUmpire)
        {
            game = owner; hud = hudOwner; umpire = chairUmpire;
            graphics = IntroGraphics.On(hud.Root);
            hud.MatchVisible = false;
            Configure();
        }

        /// End at once (autoplay, tests): no graphics, the match HUD up.
        public void Finish()
        {
            if (!Playing) return;
            director.Cancel(); t = TotalLength; Wrap(); hud.MatchVisible = true;
        }
        void Wrap() { graphics?.Clear(); plate = left = right = null; if (game && game.GameplayCamera) IntroLens.Off(game.GameplayCamera); }

        /// Skip the flyover: straight to the introductions (they always play). Skip input is consumed by the caller.
        public void Skip()
        {
            if (!Playing || t < .3f) return;
            if (game.Versus) { if (beatIndex == 0 && !localSkipVote) { localSkipVote = true; game.VersusSkipVote(introRound); TryVersusSkip(); } return; }
            if (PresentationPolicy.Multiplayer) return;
            if (beatIndex == 2) { if (game.Player && !game.Player.Swinging) { practiced = true; game.Player.Swing(.2f, false); } return; }
            if (beatIndex != 0) return;
            skipped = true; JumpToIntros();
        }
        void JumpToIntros()
        {
            director.Cancel(); PresentationStinger.Stop();
            graphics?.HidePlate(plate, t);
            // the flyover ends where it was (the introductions keep their place on the show's clock)
            drone = Mathf.Min(drone, t); beatIndex = walkCut == PresentationCut.Off ? 2 : 1; BeginBeat();
            PresentationPolicy.Event("Intro", "skip", director.Sequence);
        }

        static float Smooth(float x) => x * x * (3 - 2 * x);
        static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float k)
        {
            float u = 1 - k;
            return u * u * u * a + 3 * u * u * k * b + 3 * u * k * k * c + k * k * k * d;
        }
        static Vector3 Flat(Vector3 v) { v.y = 0; return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward; }
        static Vector3 Before(TennisActor a, float degrees, float back, float up)
            => a.transform.position + Quaternion.AngleAxis(degrees, Vector3.up) * Flat(a.transform.forward) * back + Vector3.up * up;

        /// Drives the camera while playing; returns false once play has begun.
        public bool Drive(Camera camera, Vector3 playTarget, Vector3 playLook, float playFov, float dt)
        {
            if (!Playing) return false;
            if (!cueLed && beatIndex < 2 && director.Duration - director.Elapsed <= .15f) { cueLed = true; PresentationStinger.Lead(); }
            director.Tick(dt);
            float e = director.Elapsed;
            t = beatIndex == 0 ? e : beatIndex == 1 ? drone + e : beatIndex == 2 ? drone + WalkLength + e : TotalLength;
            Vector3 pos, look; float fov;
            var player = game.Player; var rival = game.Opponent;

            if (beatIndex == 0)
            {
                // In from the sea, banking round the resort, settling over the court (each venue flies its own path).
                TennisVenue.Drone(Smooth(Mathf.Clamp01(t / Mathf.Max(.01f, drone))) * .92f, out pos, out look, out fov);
                if (shown != 0) { shown = 0; plate = graphics.ShowPlate(TennisVenue.CourtName.ToUpperInvariant(), TennisVenue.Title, subtitle, IntroGraphics.Sky, false, t); }
                if (t > drone - .35f) graphics.HidePlate(plate, t);
                IntroLens.Off(camera);
            }
            else if (beatIndex == 1 && e < secondEnd)
            {
                // the players in turn, each with three seconds to pick an emote (played out if picked)
                bool first = e < firstEnd;
                bool isRival = first == RivalFirst;
                var a = isRival ? rival : player;
                float from = first ? 0 : firstEnd, s = e - from;
                int which = first ? 1 : 2;
                if (shown != which)
                {
                    shown = which; graphics.HidePlate(plate, t);
                    plate = isRival ? graphics.ShowPlate(rivalRole, hud.OpponentName, rivalDetail, IntroGraphics.Grape, true, t)
                                    : graphics.ShowPlate("THE CHALLENGER", hud.PlayerName, playerDetail, IntroGraphics.Coral, false, t);
                }
                Emote(isRival, s, first);
                float span = first ? firstEnd : secondEnd - firstEnd, k = Smooth(Mathf.Clamp01(s / Mathf.Max(.01f, span)));
                float side = isRival ? 1 : -1, back = Mathf.Lerp(5.4f, 4.8f, k);
                // full length, at chest height, a little to the side: the whole player and their emote in frame
                pos = Before(a, side * Mathf.Lerp(16, 9, k), back, 1.25f);
                look = a.transform.position + Vector3.up * .95f;
                fov = 30;
                IntroLens.Focus(camera, back);
                if (span - s < .3f) graphics.HidePlate(plate, t);
            }
            else if (beatIndex == 1)
            {
                // the stare-down, eye to eye: the rival close, then you, each looking down the lens at the other (waist up,
                // on a normal lens: a long one across the court blows the painted backdrop up into a smear)
                float s = e - secondEnd; bool rivalUp = s < stare / 2;
                if (shown != 3)
                {
                    shown = 3; graphics.HidePlate(plate, t);
                    left = graphics.ShowPlate("", hud.PlayerName, "", IntroGraphics.Coral, false, t, 70);
                    right = graphics.ShowPlate("", hud.OpponentName, "", IntroGraphics.Grape, true, t, 70);
                    graphics.ShowVs(t);
                }
                var a = rivalUp ? rival : player;
                float k = Smooth(Mathf.Clamp01((rivalUp ? s : s - stare / 2) / Mathf.Max(.01f, stare / 2)));
                float back = Mathf.Lerp(3f, 2.55f, k);
                pos = Before(a, (rivalUp ? 1 : -1) * 6, back, 1.5f);
                look = a.transform.position + Vector3.up * 1.36f; fov = 27;
                // each to their own name's side of the picture (the rival right, you left), clear of the VS between them
                look -= Vector3.Cross(Vector3.up, Flat(look - pos)) * (rivalUp ? .42f : -.42f);
                IntroLens.Focus(camera, back);
                if (stare - s < .3f) { graphics.HidePlate(left, t); graphics.HidePlate(right, t); }
            }
            else
            {
                if (!settleCaptured) { settleCaptured = true; lastPos = camera.transform.position; lastRot = camera.transform.rotation; lastFov = camera.fieldOfView; if (lastRot == default(Quaternion)) lastRot = Quaternion.LookRotation(playLook - playTarget); }
                // swoop from wherever the show was into the gameplay camera; the umpire calls play
                float s = Mathf.Clamp01((t - (TotalLength - swoop)) / Mathf.Max(.01f, swoop)), k = Smooth(s);
                if (!umpireCalled) { umpireCalled = true; if (umpire) umpire.Call(); hud.ShowCall("PLAY", null, true); graphics.Clear(); IntroLens.Off(camera); }
                Vector3 high = playTarget + new Vector3(0, 6, -6);
                camera.transform.position = Bezier(lastPos, lastPos + Vector3.up * 2, high, playTarget, k);
                camera.transform.rotation = Quaternion.Slerp(lastRot, Quaternion.LookRotation(playLook - playTarget), k);
                camera.fieldOfView = Mathf.Lerp(lastFov, playFov, k);
                if (t >= TotalLength) { hud.MatchVisible = true; Wrap(); }
                graphics.Tick(t);
                return true;
            }
            graphics.Tick(t);
            camera.transform.position = lastPos = pos;
            camera.transform.rotation = lastRot = Quaternion.LookRotation(look - pos);
            camera.fieldOfView = lastFov = fov;
            return true;
        }

        /// A player's emote in their own introduction `s` seconds in: started when it has been picked (alone: the phone's
        /// pick; the rival in versus: the other player's, as their phone sent it), within their three seconds.
        void Emote(bool rival, float s, bool first)
        {
            HeroTennisDriver.Clip? pick = rival ? game.VersusRivalIntroClip : chosenEmote;
            float? started = rival ? rivalStarted : playerStarted;
            if (!pick.HasValue || started.HasValue || s >= window) return;
            var actor = rival ? game.Opponent : game.Player;
            var driver = actor ? actor.GetComponentInChildren<HeroTennisDriver>() : null;
            if (!driver || !driver.PlayEmote(pick.Value)) return;
            if (rival) { rivalStarted = s; rivalEmoted = true; } else { playerStarted = s; playerEmoted = chosenPlayed = true; }
            // the introduction runs on until the emote has played out (not the legacy shared cut: the host caps it)
            if (sharedCut) return;
            float end = PresentationDirector.IntroEnd(0, s, EmoteSeconds(actor, pick.Value));
            if (first) { float grow = Mathf.Max(0, end - firstEnd); firstEnd += grow; secondEnd += grow; }
            else secondEnd = Mathf.Max(secondEnd, firstEnd + end);
            director.Extend(WalkLength);
        }

        void OnDisable() { director.Cancel(); }

        string rivalRole = "THE RIVAL", subtitle = TennisVenue.CourtName.ToUpperInvariant() + "   ·   " + TennisVenue.Session.ToUpperInvariant();
        string rivalDetail => TennisVenue.Title;
        string playerDetail => TennisVenue.CourtName.ToUpperInvariant();
        /// Bill the match: the round under the venue's plate, and the rival's billing on theirs
        /// ("QUARTERFINAL", or "THE CHAMPION" for the boss).
        public void Bill(string round, string role)
        {
            subtitle = TennisVenue.CourtName.ToUpperInvariant() + "   ·   " + round.ToUpperInvariant();
            rivalRole = role;
        }
    }
}
