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
        public const float Drone = 5.2f, RivalIntro = 3.8f, PlayerIntro = 3.8f, Umpire = 1.3f, Swoop = 1.3f;
        public const float Length = Drone + RivalIntro + PlayerIntro + Umpire + Swoop;

        TennisGame game;
        TennisHud hud;
        TennisUmpire umpire;
        float t;
        bool rivalEmoted, playerEmoted, umpireCalled;
        RectTransform barTop, barBottom, titleCard, nameCard;
        Text title, subtitle, nameText, roleText;
        UiGradient nameGradient;
        Vector3 lastPos; Quaternion lastRot; float lastFov = 50;

        public bool Playing => t < Length;
        public float Remaining => Mathf.Max(0, Length - t);

        public void Build(TennisGame owner, TennisHud hudOwner, TennisUmpire chairUmpire)
        {
            game = owner; hud = hudOwner; umpire = chairUmpire;
            var root = hud.Root;
            barTop = Bar(root, 1); barBottom = Bar(root, 0);

            var titleBody = hud.Chunky("Title card", root, new Vector2(.5f, .5f), new Vector2(560, 96), new Vector2(0, 150), TennisHud.SkyTop, TennisHud.SkyBottom, 5);
            titleCard = (RectTransform)titleBody.transform.parent;
            title = hud.Label(titleBody.rectTransform, "TROPICAL OPEN", 58, new Vector2(0, 8), new Vector2(560, 70), Color.white, TextAnchor.MiddleCenter, 3f);
            UiGradient.On(title, TennisHud.GoldTop, TennisHud.GoldBottom);
            var subBody = hud.Chunky("Title ribbon", titleCard, new Vector2(.5f, 0), new Vector2(300, 30), new Vector2(0, -6), TennisHud.GoldTop, TennisHud.GoldBottom, 3);
            subtitle = hud.Label(subBody.rectTransform, "CENTRE COURT  ·  SUNSET SESSION", 15, new Vector2(0, 1), new Vector2(300, 30), TennisHud.Navy, TextAnchor.MiddleCenter, 0);

            var nameBody = hud.Chunky("Name card", root, new Vector2(0, 0), new Vector2(420, 92), new Vector2(0, 130), TennisHud.SunTop, TennisHud.SunBottom, 5);
            nameCard = (RectTransform)nameBody.transform.parent;
            nameGradient = nameBody.GetComponent<UiGradient>();
            nameText = hud.Label(nameBody.rectTransform, "", 60, new Vector2(0, 6), new Vector2(420, 70), Color.white, TextAnchor.MiddleCenter, 3f);
            var roleBody = hud.Chunky("Role ribbon", nameCard, new Vector2(.5f, 0), new Vector2(240, 30), new Vector2(0, -8), new Color(.16f, .24f, .55f), TennisHud.NavyDeep, 3);
            roleText = hud.Label(roleBody.rectTransform, "", 16, new Vector2(0, 1), new Vector2(240, 30), Color.white, TextAnchor.MiddleCenter, 1.2f);
            titleCard.gameObject.SetActive(false); nameCard.gameObject.SetActive(false);
            hud.MatchVisible = false;
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
            t = Length;
            titleCard.gameObject.SetActive(false); nameCard.gameObject.SetActive(false);
            SetBars(0); hud.MatchVisible = true;
        }

        /// Jump to the swoop into play.
        public void Skip()
        {
            if (!Playing || t >= Length - Swoop) return;
            t = Length - Swoop;
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
            t += dt;
            Vector3 pos, look; float fov;
            var player = game.Player.transform; var rival = game.Opponent.transform;
            float bars = 1;

            if (t < Drone)
            {
                // In from the sea behind the far baseline, banking round the resort, settling
                // high over the court's side.
                float k = Smooth(Mathf.Clamp01(t / Drone));
                // High over the bay first, so the whole island reads, then banking low over
                // the jungle and in over the stands.
                pos = Bezier(new Vector3(40, 95, 210), new Vector3(-150, 70, 90), new Vector3(-70, 24, -40), new Vector3(-14, 7.5f, -22), k);
                look = Vector3.Lerp(new Vector3(0, -10, -20), new Vector3(0, .5f, 0), k);
                fov = Mathf.Lerp(46, 50, k);
                Card(titleCard, t, Drone, fromLeft: false);
            }
            else if (t < Drone + RivalIntro)
            {
                float s = t - Drone, k = Smooth(Mathf.Clamp01(s / RivalIntro));
                Vector3 face = rival.forward;
                Vector3 side = Vector3.Cross(Vector3.up, face);
                // Full body, head to toe, from chest height (a camera above the chest
                // foreshortens the trunk), with a slow push in: the whole emote reads.
                pos = rival.position + face * Mathf.Lerp(5.6f, 4.7f, k) + side * Mathf.Lerp(-1.6f, -1.0f, k) + Vector3.up * 1.25f;
                look = rival.position + Vector3.up * 1.0f;
                fov = 40;
                if (!rivalEmoted && s > .35f) { rivalEmoted = true; game.Opponent.PlayIntro(); }
                ShowName(hud.OpponentName, "THE RIVAL", TennisHud.SeaTop, TennisHud.SeaBottom);
                Card(nameCard, s, RivalIntro, fromLeft: false);
            }
            else if (t < Drone + RivalIntro + PlayerIntro)
            {
                float s = t - Drone - RivalIntro, k = Smooth(Mathf.Clamp01(s / PlayerIntro));
                Vector3 face = player.forward;
                Vector3 side = Vector3.Cross(Vector3.up, face);
                // Full body, head to toe, from chest height (a camera above the chest
                // foreshortens the trunk), with a slow push in: the whole emote reads.
                pos = player.position + face * Mathf.Lerp(5.6f, 4.7f, k) + side * Mathf.Lerp(1.6f, 1.0f, k) + Vector3.up * 1.25f;
                look = player.position + Vector3.up * 1.0f;
                fov = 40;
                if (!playerEmoted && s > .35f) { playerEmoted = true; game.Player.PlayIntro(); }
                ShowName(hud.PlayerName, "THE CHALLENGER", TennisHud.SunTop, TennisHud.SunBottom);
                Card(nameCard, s, PlayerIntro, fromLeft: true);
            }
            else if (t < Length - Swoop)
            {
                // The umpire, from the court, as he calls play.
                float s = t - (Length - Swoop - Umpire);
                Vector3 seat = TennisUmpire.Seat + Vector3.up * .5f;
                pos = seat + new Vector3(3.6f, -.6f, -2.2f + s * .5f);
                look = seat;
                fov = 34;
                nameCard.gameObject.SetActive(false);
                if (!umpireCalled) { umpireCalled = true; if (umpire) umpire.Call(); hud.ShowCall("PLAY", null, true); }
            }
            else
            {
                // Swoop from wherever the show was into the gameplay camera.
                float s = Mathf.Clamp01((t - (Length - Swoop)) / Swoop), k = Smooth(s);
                if (!umpireCalled) { umpireCalled = true; hud.ShowCall("PLAY", null, true); }
                titleCard.gameObject.SetActive(false); nameCard.gameObject.SetActive(false);
                Vector3 high = playTarget + new Vector3(0, 6, -6);
                pos = Bezier(lastPos, lastPos + Vector3.up * 2, high, playTarget, k);
                camera.transform.position = pos;
                camera.transform.rotation = Quaternion.Slerp(lastRot, Quaternion.LookRotation(playLook - playTarget), k);
                camera.fieldOfView = Mathf.Lerp(lastFov, playFov, k);
                bars = 1 - k;
                SetBars(bars);
                if (t >= Length) { hud.MatchVisible = true; SetBars(0); }
                return true;
            }
            camera.transform.position = lastPos = pos;
            camera.transform.rotation = lastRot = Quaternion.LookRotation(look - pos);
            camera.fieldOfView = lastFov = fov;
            SetBars(Mathf.Clamp01(t / .4f) * bars);
            return true;
        }

        void SetBars(float amount)
        {
            float h = 74 * amount;
            barTop.sizeDelta = new Vector2(0, h); barBottom.sizeDelta = new Vector2(0, h);
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
            float enter = Mathf.Clamp01((s - .25f) / .5f), exit = Mathf.Clamp01((s - (length - .45f)) / .35f);
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
