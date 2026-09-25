using UnityEngine;
using UnityEngine.UI;

namespace GolfArcade.UI
{
    /// The word that pops at impact, Wii Sports style: PERFECT! in gold, GREAT! in green, GOOD in
    /// white, THIN in orange, big and outlined, punching in past full size and settling, with the
    /// ball's shape (DRAW, SLICE…) in a blue pill under it. Then it floats up and fades.
    public sealed class StrikePopup : MonoBehaviour
    {
        /// How big it pops, against its full size (smaller on the phone than on the big screen).
        public float Size = 1f;
        RectTransform root, pill;
        Text word, shape;
        CanvasGroup group;
        float shownAt = -99f;
        const float Pop = 0.28f, Hold = 1.5f, Fade = 0.45f;

        public static StrikePopup Create(Transform parent)
        {
            var go = new GameObject("Strike popup", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var p = go.AddComponent<StrikePopup>();
            p.root = (RectTransform)go.transform;
            p.root.anchorMin = p.root.anchorMax = p.root.pivot = new Vector2(0.5f, 0.5f);
            p.root.anchoredPosition = new Vector2(0, 430); p.root.sizeDelta = new Vector2(900, 260);
            p.group = go.AddComponent<CanvasGroup>();
            p.group.alpha = 0; p.group.blocksRaycasts = false; p.group.interactable = false;
            p.word = UiKit.Chunky(p.root, "Word", 132, UiKit.ArcadeYellow, UiKit.ArcadeInk, 6f);
            p.word.rectTransform.anchorMin = new Vector2(0, 0.35f); p.word.rectTransform.anchorMax = Vector2.one;
            p.word.horizontalOverflow = HorizontalWrapMode.Overflow;
            p.pill = UiKit.Pill(p.root, "Shape", UiKit.ArcadeBlue, new Vector2(0.5f, 0.5f), new Vector2(0, -78), new Vector2(240, 60), out var fill, 4f);
            p.shape = UiKit.Label(fill.transform, "Shape", 34, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, UiKit.Display, false);
            p.shape.color = Color.white; p.shape.raycastTarget = false;
            return p;
        }

        public void Show(string text, Color color, string shapeText)
        {
            word.text = text; word.color = color;
            pill.gameObject.SetActive(!string.IsNullOrEmpty(shapeText));
            if (!string.IsNullOrEmpty(shapeText))
            {
                shape.text = shapeText;
                pill.sizeDelta = new Vector2(Mathf.Max(200, shape.preferredWidth + 60), 60);
            }
            shownAt = Time.unscaledTime;
            transform.SetAsLastSibling();
            Update();
        }

        public void Hide() { shownAt = -99f; group.alpha = 0; }

        public bool Showing => group.alpha > 0.01f;

        void Update()
        {
            float t = Time.unscaledTime - shownAt;
            if (t > Pop + Hold + Fade) { group.alpha = 0; return; }
            // punch in: overshoot to 1.25 and settle, with a little twist that straightens
            float s = t < Pop ? EaseOutBack(t / Pop) : 1f;
            root.localScale = Vector3.one * Mathf.Max(0.01f, s) * Size;
            root.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(-9f, -3f, Mathf.Clamp01(t / Pop)));
            float fade = Mathf.Clamp01((t - Pop - Hold) / Fade);
            group.alpha = 1f - fade;
            root.anchoredPosition = new Vector2(0, 430 + fade * 60f);
        }

        static float EaseOutBack(float x)
        {
            const float c1 = 2.4f, c3 = c1 + 1f;
            x = Mathf.Clamp01(x) - 1f;
            return 1f + c3 * x * x * x + c1 * x * x;
        }
    }

    /// The club face while you set up and swing: a pill with a green "square" window in the
    /// middle and a marker that slides left as the face closes (a draw, then a hook) and right as
    /// it opens (a fade, then a slice), so rolling the wrists becomes something you can learn.
    public sealed class FaceDial : MonoBehaviour
    {
        RectTransform marker;
        Text caption;
        float shown, target;
        const float Width = 380, Range = 45f;

        public static FaceDial Create(Transform parent, float deadZoneDegrees, float bigCurveFaceDegrees)
        {
            var go = new GameObject("Face dial", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var d = go.AddComponent<FaceDial>();
            var root = (RectTransform)go.transform;
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0); root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = new Vector2(0, 520); root.sizeDelta = new Vector2(Width + 20, 110);
            var bar = UiKit.Pill(root, "Bar", UiKit.ArcadeInk, new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(Width + 20, 46), out var fill, 4f);
            float Px(float deg) => Mathf.Clamp(deg / Range, -1, 1) * Width / 2;
            // zones: hook / draw / square / fade / slice
            void Zone(string name, float from, float to, Color c)
            {
                var z = UiKit.Panel(fill.transform, name, c, new Vector2(0.5f, 0), new Vector2(0.5f, 1), new Vector2((Px(from) + Px(to)) / 2, 0), new Vector2(Px(to) - Px(from), -12), false);
                z.raycastTarget = false;
            }
            Zone("Hook", -Range, -bigCurveFaceDegrees, new Color(1f, 0.45f, 0.25f, 0.85f));
            Zone("Draw", -bigCurveFaceDegrees, -deadZoneDegrees, new Color(1f, 0.82f, 0.23f, 0.85f));
            Zone("Square", -deadZoneDegrees, deadZoneDegrees, new Color(0.35f, 0.9f, 0.45f, 0.95f));
            Zone("Fade", deadZoneDegrees, bigCurveFaceDegrees, new Color(1f, 0.82f, 0.23f, 0.85f));
            Zone("Slice", bigCurveFaceDegrees, Range, new Color(1f, 0.45f, 0.25f, 0.85f));
            var m = UiKit.Panel(bar, "Marker", Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16, 62));
            m.raycastTarget = false;
            d.marker = m.rectTransform;
            d.caption = UiKit.Label(root, "Caption", 26, TextAnchor.MiddleCenter, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -12), new Vector2(0, 34), UiKit.Display);
            d.caption.color = Color.white; d.caption.raycastTarget = false;
            go.SetActive(false);
            return d;
        }

        /// Face angle in degrees (positive open), or null to hide the dial.
        public void Set(double? faceDegrees)
        {
            gameObject.SetActive(faceDegrees.HasValue);
            if (faceDegrees.HasValue) target = (float)faceDegrees.Value;
        }

        void Update()
        {
            shown = Mathf.Lerp(shown, target, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 18f));
            marker.anchoredPosition = new Vector2(Mathf.Clamp(shown / Range, -1, 1) * Width / 2, 0);
            float a = Mathf.Abs(shown);
            caption.text = a < 10 ? "FACE  SQUARE" : shown < 0 ? (a < 24 ? "FACE  CLOSED · DRAW" : "FACE  SHUT · HOOK") : (a < 24 ? "FACE  OPEN · FADE" : "FACE  WIDE OPEN · SLICE");
        }
    }

    /// A broadcast's replay bug: a red dot and REPLAY in the top corner while one plays.
    public static class ReplayBadge
    {
        public static RectTransform Create(Transform parent)
        {
            var root = UiKit.Pill(parent, "Replay badge", new Color(0.86f, 0.12f, 0.16f), new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(300, 76), out var fill, 5f);
            var t = UiKit.Label(fill.transform, "Text", 40, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, UiKit.Display, false);
            t.text = "▶  REPLAY"; t.color = Color.white; t.raycastTarget = false;
            root.gameObject.SetActive(false);
            return root;
        }
    }
}
