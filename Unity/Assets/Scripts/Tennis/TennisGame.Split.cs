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

        /// NativeSportsSession tells us which display the game was routed to and its shape, once that is known (and again if it changes).
        public void SetSplitDisplay(int display, float aspect) {
            splitDisplayAspect = Mathf.Max(.5f, aspect);
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
