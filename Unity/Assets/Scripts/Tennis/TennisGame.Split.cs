using GolfArcade.Multiplayer;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
namespace GolfArcade.Tennis {
    /// One TV, two players: the display is split down the middle and each half shows the whole court from behind that player's baseline
    /// (placements from TennisSplitCamera). Only the phone that has the TV and views the match as "split" builds this; the single
    /// shared view stays as the fallback (set SplitScreen to false).
    public sealed partial class TennisGame {
        /// False falls back to one camera for both players.
        public static bool SplitScreen = true;
        Camera splitCamera; GameObject splitMarks; Canvas splitCanvas; Text[] splitNames;
        float splitDisplayAspect = 16f / 9;
        /// The second player's camera, if the split screen is in use (NativeSportsSession keeps it on the same display).
        public Camera SplitCamera => splitCamera;
        bool UsesSplit => SplitScreen && networkView == NetworkConfiguration.ViewSplit && splitCamera;
        /// True while both halves are on screen: whatever is drawn only for "the camera" (the crowd) must then draw for both.
        public bool SplitActive => splitCamera && splitCamera.enabled;
        /// Distance from a point to the nearest camera that is drawing the match: the gameplay camera, or either half of a split screen.
        public float DistanceToNearestCamera(Vector3 point) {
            var main = GameplayCamera;
            float distance = main ? Vector3.Distance(main.transform.position, point) : 10f;
            return SplitActive ? Mathf.Min(distance, Vector3.Distance(splitCamera.transform.position, point)) : distance;
        }

        // Seat 1's camera is a copy of the gameplay camera (same look, same post-processing) on its own object; a divider and the two
        // players' names mark the halves.
        void BuildSplit(string nameLeft, string nameRight) {
            var main = GameplayCamera;
            if (!main || splitCamera) return;
            splitCamera = new GameObject("Tennis split camera (seat 1)").AddComponent<Camera>();
            splitCamera.CopyFrom(main); splitCamera.tag = "Untagged"; splitCamera.enabled = false; splitCamera.targetDisplay = main.targetDisplay;
            var data = main.GetUniversalAdditionalCameraData(); var other = splitCamera.GetUniversalAdditionalCameraData();
            other.renderPostProcessing = data.renderPostProcessing; other.antialiasing = data.antialiasing; other.renderShadows = data.renderShadows;
            other.volumeLayerMask = data.volumeLayerMask; other.requiresDepthTexture = data.requiresDepthTexture;
            other.requiresColorTexture = data.requiresColorTexture; other.stopNaN = data.stopNaN; other.dithering = data.dithering;

            var canvas = new GameObject("Tennis split marks").AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 5;
            var scaler = canvas.gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1280, 720);
            splitCanvas = canvas; canvas.targetDisplay = main.targetDisplay; splitMarks = canvas.gameObject; splitMarks.SetActive(false);
            var divider = new GameObject("Divider").AddComponent<Image>(); divider.transform.SetParent(canvas.transform, false);
            divider.color = new Color(1, 1, 1, .85f); divider.raycastTarget = false;
            var line = divider.rectTransform; line.anchorMin = new Vector2(.5f, 0); line.anchorMax = new Vector2(.5f, 1); line.pivot = new Vector2(.5f, .5f);
            line.sizeDelta = new Vector2(5, 0); line.anchoredPosition = Vector2.zero;
            var font = Resources.Load<Font>("Tennis/UI/Fonts/Rubik-Bold") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            splitNames = new Text[2];
            for (int i = 0; i < 2; i++) {
                var text = new GameObject(i == 0 ? "Left player" : "Right player").AddComponent<Text>(); text.transform.SetParent(canvas.transform, false);
                text.font = font; text.fontSize = 30; text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
                text.color = i == 0 ? new Color(.84f, .94f, .27f) : new Color(1, .62f, .42f);
                text.text = (i == 0 ? nameLeft : nameRight)?.ToUpperInvariant() ?? "";
                var outline = text.gameObject.AddComponent<Outline>(); outline.effectColor = TennisHud.Navy; outline.effectDistance = new Vector2(2, -2);
                // Bottom centre of each half: the scoreboard owns the top left and the point call the top middle.
                var rect = text.rectTransform; rect.anchorMin = rect.anchorMax = new Vector2(i == 0 ? .25f : .75f, 0); rect.pivot = new Vector2(.5f, 0);
                rect.sizeDelta = new Vector2(360, 40); rect.anchoredPosition = new Vector2(0, 24);
                splitNames[i] = text;
            }
        }

        // ---------------------------------------------------------------- VERSUS on one TV
        // Two single-player views side by side: the left half is this phone's player, from behind them, with single player's camera
        // and HUD; the right half is the other player, from behind them at the far end, with the same camera rule and a HUD of their
        // own (their score, their calls). The court, the ball and both players are the one match (TennisGame.Versus).
        Camera rightCamera; Canvas rightCanvas; TennisHud rightHud; GameObject versusDivider;
        Vector3 rightLook; bool rightFramed;
        /// True on the phone drawing a versus match for both players on one TV.
        public bool VersusSplit => Versus && rightCamera;
        /// The other player's half (tests).
        public Camera RightCamera => rightCamera;
        public TennisHud RightHud => rightHud;

        void BuildVersusSplit(string rightName)
        {
            var main = GameplayCamera;
            if (!main || rightCamera) return;
            rightCamera = new GameObject("Tennis versus camera (other player)").AddComponent<Camera>();
            rightCamera.CopyFrom(main); rightCamera.tag = "Untagged"; rightCamera.enabled = false; rightCamera.targetDisplay = main.targetDisplay;
            var data = main.GetUniversalAdditionalCameraData(); var other = rightCamera.GetUniversalAdditionalCameraData();
            other.renderPostProcessing = data.renderPostProcessing; other.antialiasing = data.antialiasing; other.renderShadows = data.renderShadows;
            other.volumeLayerMask = data.volumeLayerMask; other.requiresDepthTexture = data.requiresDepthTexture;
            other.requiresColorTexture = data.requiresColorTexture; other.stopNaN = data.stopNaN; other.dithering = data.dithering;
            rightCanvas = new GameObject("Tennis HUD (other player)").AddComponent<Canvas>(); rightCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = rightCanvas.gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1280, 720);
            rightCanvas.targetDisplay = main.targetDisplay;
            rightHud = rightCanvas.gameObject.AddComponent<TennisHud>(); rightHud.Build(rightCanvas);
            rightHud.PlayerName = rightName.ToUpperInvariant(); rightHud.OpponentName = hud ? hud.PlayerName : "YOU"; rightHud.EventLabel = VersusLabel(MatchSets);
            hud.ConfineTo(0, .5f); rightHud.ConfineTo(.5f, 1);
            rightHud.MatchVisible = false;
            // the line between the halves
            var marks = new GameObject("Tennis versus divider").AddComponent<Canvas>(); marks.renderMode = RenderMode.ScreenSpaceOverlay; marks.sortingOrder = 6;
            marks.targetDisplay = main.targetDisplay; versusDivider = marks.gameObject;
            var line = new GameObject("Divider").AddComponent<Image>(); line.transform.SetParent(marks.transform, false);
            line.color = new Color(1, 1, 1, .85f); line.raycastTarget = false;
            var rt = line.rectTransform; rt.anchorMin = new Vector2(.5f, 0); rt.anchorMax = new Vector2(.5f, 1); rt.sizeDelta = new Vector2(5, 0); rt.anchoredPosition = Vector2.zero;
            versusDivider.SetActive(false);
        }

        /// Before the camera is placed this frame: the halves (or the whole screen while the intro plays).
        void PrepareVersusSplit()
        {
            var main = GameplayCamera; if (!main || !rightCamera) return;
            if (rightCamera.targetDisplay == 0 && Screen.height > 0) splitDisplayAspect = Mathf.Max(.5f, (float)Screen.width / Screen.height);
            bool split = !IntroPlaying;
            if (rightCamera.enabled != split) { rightCamera.enabled = split; versusDivider.SetActive(split); rightFramed = false; }
            main.rect = split ? new Rect(0, 0, .5f, 1) : new Rect(0, 0, 1, 1);
            main.aspect = split ? splitDisplayAspect * .5f : splitDisplayAspect;
            hud.ConfineTo(0, split ? .5f : 1);
        }

        /// After: the other player's camera, single player's rule from their end of the court.
        void UpdateVersusSplit()
        {
            if (!rightCamera || !rightCamera.enabled) return;
            float half = splitDisplayAspect * .5f;
            rightCamera.rect = new Rect(.5f, 0, .5f, 1); rightCamera.aspect = half;
            TennisShoulderCamera.Frame(Mirror(Opponent.transform.position), Opponent.LeftHanded, Mirror(BallPosition), false, half,
                out Vector3 target, out Vector3 look, out float fov);
            target = Mirror(target); look = Mirror(look);
            float rate = 1 - Mathf.Exp(-Time.deltaTime * 9);
            rightCamera.transform.position = rightFramed ? Vector3.Lerp(rightCamera.transform.position, target, rate) : target;
            rightLook = rightFramed ? Vector3.Lerp(rightLook, look, rate) : look;
            rightCamera.fieldOfView = fov; rightCamera.transform.LookAt(rightLook); rightFramed = true;
            rightHud.MatchVisible = true;
            rightHud.RefreshAs(match.Mirrored(), SecondServe, 0, ScoreOnlyText);
        }

        /// NativeSportsSession tells us which display the game was routed to and its shape, once that is known (and again if it changes).
        public void SetSplitDisplay(int display, float aspect) {
            splitDisplayAspect = Mathf.Max(.5f, aspect);
            if (rightCamera) rightCamera.targetDisplay = display;
            if (rightCanvas) rightCanvas.targetDisplay = display;
            if (versusDivider) versusDivider.GetComponent<Canvas>().targetDisplay = display;
            if (splitCamera) splitCamera.targetDisplay = display;
            // The marks canvas is inactive until the first split frame, so the loop that moves every canvas to the display does not find it.
            if (splitCanvas) splitCanvas.targetDisplay = display;
            // Finding the camera placements takes a few tens of milliseconds: do it now, while loading, not in the first frame of the match.
            if (SplitScreen && networkView == NetworkConfiguration.ViewSplit) TennisSplitCamera.Near(splitDisplayAspect * .5f);
        }

        // Each frame: the split layout, or one full-screen camera while an introduction is playing.
        void UpdateSplit() {
            var main = GameplayCamera;
            if (!main) return;
            // On the phone's own screen (the editor, a phone preview) the shape follows the window, which can still be turning after the
            // scene loads; on a TV it is the display's and was set once.
            if (splitCamera.targetDisplay == 0 && Screen.height > 0) splitDisplayAspect = Mathf.Max(.5f, (float)Screen.width / Screen.height);
            if (presentation && presentation.Playing) {
                if (splitCamera.enabled) splitCamera.enabled = false;
                if (splitMarks.activeSelf) splitMarks.SetActive(false);
                main.rect = new Rect(0, 0, 1, 1); main.aspect = splitDisplayAspect;
                UpdateCamera(false);
                return;
            }
            float half = splitDisplayAspect * .5f;
            if (!splitCamera.enabled) { splitCamera.enabled = true; splitMarks.SetActive(true); }
            main.rect = new Rect(0, 0, .5f, 1); main.aspect = half;
            splitCamera.rect = new Rect(.5f, 0, .5f, 1); splitCamera.aspect = half;
            Place(main, TennisSplitCamera.For(0, half)); Place(splitCamera, TennisSplitCamera.For(1, half));
        }
        static void Place(Camera camera, TennisSplitCamera.Placement p) {
            camera.transform.position = p.position; camera.transform.LookAt(p.look); camera.fieldOfView = p.fov;
        }
    }
}
