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

        // ---- Tokens. One navy world, hue-biased neutrals, a single sky accent; the meter's
        // green/amber/red is semantic and stays out of this palette.
        public static readonly Color Ground = Hex("0E1A2B");
        public static readonly Color Surface = Hex("16263C", 0.92f);
        public static readonly Color SurfaceRaised = Hex("1F3352", 0.96f);
        public static readonly Color Hairline = new(1f, 1f, 1f, 0.12f);
        public static readonly Color Ink = Hex("F4F7FB");
        public static readonly Color InkMuted = Hex("9FB3CF");
        public static readonly Color Accent = Hex("5AB0FF");
        public static readonly Color AccentStrong = Hex("2F8DF0");

        // Names the HUD grew up with, mapped onto the tokens.
        public static readonly Color CardFill = Surface;
        public static readonly Color CardEdge = Hairline;
        public static readonly Color ButtonFill = Hex("1A2C47", 0.9f);
        public static readonly Color ButtonPressed = new(0.35f, 0.69f, 1f, 0.9f);
        public static readonly Color Muted = InkMuted;

        // ---- The tournament look, for what is broadcast over the course (the title over the
        // flyover, the nameplates, the scoreboard, the shot card): Wii-bright cobalt panels with a
        // white rim, sunshine-yellow for you, navy ink on the yellow.
        public static readonly Color ArcadeBlue = Hex("2F5FE0", 0.92f);
        public static readonly Color ArcadeBlueDeep = Hex("1C3FA8");
        public static readonly Color ArcadeSky = Hex("9CC2FF", 0.55f);
        public static readonly Color ArcadeRim = Hex("FFFFFF", 0.95f);
        public static readonly Color ArcadeYellow = Hex("FFD23A");
        public static readonly Color ArcadeYellowDeep = Hex("F2A81D");
        public static readonly Color ArcadeInk = Hex("132A6B");

        public static Color Hex(string hex, float alpha = 1f)
        {
            int v = System.Convert.ToInt32(hex, 16);
            return new Color(((v >> 16) & 255) / 255f, ((v >> 8) & 255) / 255f, (v & 255) / 255f, alpha);
        }

        // ---- Type: Nunito (OFL, Assets/Resources/Fonts), a rounded face that suits the
        // stylised resort world. Real weights, so nothing is faux-bold.
        public static Font Display => display ??= Face("Nunito-ExtraBold");
        public static Font Strong => strong ??= Face("Nunito-Bold");
        public static Font Ui => ui ??= Face("Nunito-SemiBold");
        public static Font Body => body ??= Face("Nunito-Regular");
        /// The default face for labels.
        public static Font Font => Ui;
        static Font display, strong, ui, body, fallback;
        static Font Face(string name) => Resources.Load<Font>("Fonts/" + name) ?? (fallback ??= Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        /// A filled circle, for swatches and dots.
        public static Sprite Circle => circle ??= MakeRounded(64, 32);
        static Sprite circle;
        /// A ring, for the aim dial's power arc (drawn Filled/Radial360).
        public static Sprite Ring => ringSprite ??= MakeRing(160, 14);
        static Sprite ringSprite;

        static Sprite MakeRing(int n, int band)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            float r = n / 2f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - r) * (x + 0.5f - r) + (y + 0.5f - r) * (y + 0.5f - r));
                    float a = Mathf.Clamp01(r - 1 - d) * Mathf.Clamp01(d - (r - band));
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(255 * a));
                }
            tex.SetPixels32(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }

        /// A 9-sliced rounded rectangle, drawn once. Tint it for any pill, card or button.
        public static Sprite Rounded => rounded ??= MakeRounded(64, 22);
        static Sprite rounded;
        /// The same with a big radius, for the light controller's cards.
        public static Sprite RoundedLarge => roundedLarge ??= MakeRounded(128, 40);
        static Sprite roundedLarge;
        /// A hairline ring, for the controller's power arc.
        public static Sprite ThinRing => thinRing ??= MakeRing(256, 9);
        static Sprite thinRing;

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

        public static Text Label(Transform parent, string name, int size, TextAnchor anchor, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 sizeDelta, Font face = null, bool shadow = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = face ? face : Font; t.fontSize = size; t.alignment = anchor; t.color = Ink;
            t.fontStyle = FontStyle.Normal;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            if (shadow)
            {
                // Over the 3-D scene type needs a lift; on the dark sheets it doesn't.
                var sh = go.AddComponent<Shadow>();
                sh.effectColor = new Color(0, 0, 0, 0.55f); sh.effectDistance = new Vector2(0, -2);
            }
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

        /// A chunky arcade pill: a white rim around a fill, with a soft drop shadow under it — the
        /// shape the tournament graphics are made of. Size and place the returned root; content
        /// goes on `fill`. `round` false gives a rounded box instead of a capsule.
        public static RectTransform Pill(Transform parent, string name, Color fillColor, Vector2 anchor, Vector2 pos, Vector2 size, out Image fill, float rim = 5f, bool round = true)
        {
            var root = new GameObject(name).AddComponent<RectTransform>();
            root.SetParent(parent, false);
            root.anchorMin = root.anchorMax = anchor; root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = pos; root.sizeDelta = size;
            Image Layer(Transform to, string n, Color c, float inset, float drop)
            {
                var img = Panel(to, n, c, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                img.rectTransform.offsetMin = new Vector2(inset, inset - drop); img.rectTransform.offsetMax = new Vector2(-inset, -inset - drop);
                img.raycastTarget = false;
                if (round) img.sprite = Circle;
                return img;
            }
            Layer(root, "Shadow", new Color(0.03f, 0.08f, 0.25f, 0.3f), 0, 7);
            var edge = Layer(root, "Rim", ArcadeRim, 0, 0);
            fill = Layer(edge.transform, "Fill", fillColor, rim, 0);
            return root;
        }

        /// Big outlined type for the arcade graphics: the face's fill with a heavy navy outline and
        /// a drop under it, like a sports title.
        public static Text Chunky(Transform parent, string name, int size, Color fill, Color outline, float weight = 4f)
        {
            var t = Label(parent, name, size, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Display, false);
            t.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            t.color = fill; t.raycastTarget = false;
            var o = t.gameObject.AddComponent<Outline>(); o.effectColor = outline; o.effectDistance = new Vector2(weight, -weight);
            var o2 = t.gameObject.AddComponent<Outline>(); o2.effectColor = outline; o2.effectDistance = new Vector2(-weight, weight);
            var drop = t.gameObject.AddComponent<Shadow>(); drop.effectColor = new Color(outline.r, outline.g, outline.b, 0.7f); drop.effectDistance = new Vector2(0, -weight * 2.2f);
            return t;
        }

        public static HoldButton Button(Transform parent, string label, Vector2 anchor, Vector2 pos, Vector2 size, int fontSize = 80, Color? tint = null, Font face = null, bool shadow = true)
        {
            var img = Card(parent, label, anchor, anchor, pos, size, tint ?? ButtonFill);
            img.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var hold = img.gameObject.AddComponent<HoldButton>();
            hold.Fill = img.transform.Find("Fill").GetComponent<Image>();
            hold.RestColor = tint ?? ButtonFill;
            var t = Label(img.transform, "Label", fontSize, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size, face ?? Strong, shadow);
            t.text = label;
            t.raycastTarget = false;
            return hold;
        }
    }
}
