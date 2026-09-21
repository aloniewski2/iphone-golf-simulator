using UnityEngine;
using UnityEngine.UI;

namespace GolfArcade.UI
{
    /// The few uGUI builders every screen here is made of: a canvas that scales to a phone, a
    /// label with a shadow, a translucent panel, a press-and-hold button. Everything is built in
    /// code so there is nothing to wire in a scene.
    public static class UiKit
    {
        public static readonly Vector2 PhoneReference = new(1080, 2340);

        public static Font Font => font ??= Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        static Font font;

        // The mockup's look: deep navy translucent cards with a faint light edge, white type,
        // sky-blue accents.
        public static readonly Color CardFill = new(0.05f, 0.09f, 0.17f, 0.66f);
        public static readonly Color CardEdge = new(1f, 1f, 1f, 0.16f);
        public static readonly Color ButtonFill = new(0.07f, 0.12f, 0.22f, 0.78f);
        public static readonly Color ButtonPressed = new(0.30f, 0.64f, 1f, 0.85f);
        public static readonly Color Accent = new(0.30f, 0.64f, 1f);
        public static readonly Color Muted = new(0.75f, 0.84f, 1f);

        /// A 9-sliced rounded rectangle, drawn once. Tint it for any pill, card or button.
        public static Sprite Rounded => rounded ??= MakeRounded(64, 22);
        static Sprite rounded;

        static Sprite MakeRounded(int n, int r)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    // distance outside the rounded rect, anti-aliased over a pixel
                    float cx = Mathf.Clamp(x + 0.5f, r, n - r), cy = Mathf.Clamp(y + 0.5f, r, n - r);
                    float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy)) - r;
                    byte a = (byte)(255 * Mathf.Clamp01(0.5f - d));
                    px[y * n + x] = new Color32(255, 255, 255, a);
                }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
        }

        /// A screen-space overlay canvas scaled against a portrait phone, plus an EventSystem if
        /// the scene has none yet.
        public static Canvas Canvas(GameObject go)
        {
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = PhoneReference;
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            if (!Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>())
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }
            return canvas;
        }

        public static Text Label(Transform parent, string name, int size, TextAnchor anchor, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 sizeDelta)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = Font; t.fontSize = size; t.alignment = anchor; t.color = Color.white;
            t.fontStyle = FontStyle.Bold;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.7f); shadow.effectDistance = new Vector2(2, -2);
            var rt = t.rectTransform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = anchorMin;
            rt.anchoredPosition = pos; rt.sizeDelta = sizeDelta;
            return t;
        }

        /// A flat colour rectangle; rounded by default (9-sliced), square when `rounded` is false
        /// for hairlines and marks.
        public static Image Panel(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 sizeDelta, bool rounded = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            if (rounded) { img.sprite = Rounded; img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 1f; }
            var rt = img.rectTransform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = anchorMin;
            rt.anchoredPosition = pos; rt.sizeDelta = sizeDelta;
            return img;
        }

        /// A card: a faint light edge with a navy fill inset inside it. Content parented to the
        /// returned (outer) image draws over the fill.
        public static Image Card(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 sizeDelta, Color? fill = null)
        {
            var edge = Panel(parent, name, CardEdge, anchorMin, anchorMax, pos, sizeDelta);
            var inner = Panel(edge.transform, "Fill", fill ?? CardFill, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            inner.rectTransform.offsetMin = new Vector2(3, 3); inner.rectTransform.offsetMax = new Vector2(-3, -3);
            inner.raycastTarget = false;
            return edge;
        }

        public static HoldButton Button(Transform parent, string label, Vector2 anchor, Vector2 pos, Vector2 size, int fontSize = 80, Color? tint = null)
        {
            var img = Card(parent, label, anchor, anchor, pos, size, tint ?? ButtonFill);
            img.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var hold = img.gameObject.AddComponent<HoldButton>();
            hold.Fill = img.transform.Find("Fill").GetComponent<Image>();
            hold.RestColor = tint ?? ButtonFill;
            var t = Label(img.transform, "Label", fontSize, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            t.text = label;
            t.raycastTarget = false;
            return hold;
        }
    }
}
