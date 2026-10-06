using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GolfArcade.UI
{
    /// Adnan's "Island Sports Club" (his GolfArcade/Unity/ClubDesign.swift on the branch
    /// tennisgameplaydone-needtofixcharacters), the one visual system for every menu, in uGUI: a resort
    /// sports club with its own type (Bricolage Grotesque display, Rubik UI; static instances in
    /// Resources/Fonts/Club), a fixed palette, rendered 3D objects instead of flat icons
    /// (Resources/Club/club-*.png), a painted scene behind each area (Resources/Club/scene-*.jpg, drawn by
    /// the ClubBackdrop shader), and one motion language: cards rise in one after another, buttons press
    /// into their base, a stripe wipe between screens, and a tick / pop / whoosh to go with them.
    ///
    /// Sizes follow his phone ("compact") layout at three canvas units to his point.
    public static class Club
    {
        // ---- Palette
        public static readonly Color Lagoon = UiKit.Hex("0E2A47"), LagoonDeep = UiKit.Hex("081A2E"), Violet = UiKit.Hex("5B2BD9");
        public static readonly Color Coral = UiKit.Hex("FF5B4A"), Green = UiKit.Hex("1FBF75"), Sun = UiKit.Hex("FFD21F"), SunDeep = UiKit.Hex("D99A00");
        public static readonly Color Cream = UiKit.Hex("F7F4EC"), CreamDeep = UiKit.Hex("D8D0BC"), Ink = UiKit.Hex("16123A"), Muted = UiKit.Hex("6B6780");
        public static readonly Color Sky = UiKit.Hex("38C6FF");
        public static Color White(float a) => new(1, 1, 1, a);
        public static Color Deep(float a) => new(LagoonDeep.r, LagoonDeep.g, LagoonDeep.b, a);

        // ---- Type: Bricolage display (wght 800, wdth 78) and title (760, 92); Rubik for the rest
        public static Font Display => Face(ref display, "Bricolage-Display");
        public static Font Title => Face(ref title, "Bricolage-Title");
        public static Font Ui => Face(ref ui, "Rubik-SemiBold");
        public static Font UiMedium => Face(ref uiMedium, "Rubik-Medium");
        public static Font Caps => Face(ref caps, "Rubik-Bold");
        public static Font Heavy => Face(ref heavy, "Rubik-ExtraBold");
        static Font display, title, ui, uiMedium, caps, heavy;
        static Font Face(ref Font f, string file) => f ? f : (f = Resources.Load<Font>("Fonts/Club/" + file) ?? UiKit.Font);

        // ---- Art
        static readonly Dictionary<string, Sprite> art = new();
        /// A rendered menu object: club-<name>.png (crest, golf, play, kit, friends, trophy, lock, ...).
        public static Sprite Art(string name)
        {
            if (art.TryGetValue(name, out var s) && s) return s;
            return art[name] = Resources.Load<Sprite>("Club/club-" + name);
        }
        /// A painted scene (home, cliff, locker, trophy, stands, pavilion).
        public static Texture2D Scene(string name) => Resources.Load<Texture2D>("Club/scene-" + name);

        // ---- Shapes
        /// A rounded rectangle with a big radius drawn once, 9-sliced; `Round` sets the corner in canvas units.
        public static Sprite Rounded => rounded ? rounded : (rounded = MakeRounded(192, 80, false));
        /// The same, only its outline (3 px), for a hairline border over a see-through fill.
        public static Sprite RoundedRing => roundedRing ? roundedRing : (roundedRing = MakeRounded(192, 80, true));
        /// A soft round glow (for the lit disc behind an object).
        public static Sprite Glow => glow ? glow : (glow = MakeGlow(128));
        /// A vertical ramp, opaque at the top to clear at the bottom (flip it for the other way).
        public static Sprite Ramp => ramp ? ramp : (ramp = MakeRamp(256));
        static Sprite rounded, roundedRing, glow, ramp;

        static Sprite MakeRounded(int n, int r, bool ring)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, r, n - r), cy = Mathf.Clamp(y + 0.5f, r, n - r);
                    float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy)) - r;
                    float a = Mathf.Clamp01(0.5f - d);
                    if (ring) a *= Mathf.Clamp01(d + 3.5f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(255 * a));
                }
            tex.SetPixels32(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
        }

        static Sprite MakeGlow(int n)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2 - 1, dy = (y + 0.5f) / n * 2 - 1;
                    float a = Mathf.Clamp01(1 - Mathf.Sqrt(dx * dx + dy * dy));
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(255 * a * a));
                }
            tex.SetPixels32(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        }

        static Sprite MakeRamp(int n)
        {
            var tex = new Texture2D(4, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[4 * n];
            for (int y = 0; y < n; y++)
            {
                float t = y / (n - 1f);                          // 0 at the bottom, 1 at the top
                byte a = (byte)(255 * t * t * (3 - 2 * t));
                for (int x = 0; x < 4; x++) px[y * 4 + x] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 4, n), new Vector2(0.5f, 0.5f));
        }

        /// An image of the big rounded shape with corners of `radius` canvas units.
        public static Image Round(Image img, float radius, bool ring = false)
        {
            img.sprite = ring ? RoundedRing : Rounded; img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 80f / Mathf.Max(1f, radius);
            return img;
        }

        public static RectTransform Box(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var rt = new GameObject(name).AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Fill(Transform parent, string name, float inset = 0)
        {
            var rt = Box(parent, name, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            rt.offsetMin = new Vector2(inset, inset); rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        public static Image Paint(Transform parent, string name, Color color, float radius = 0, bool ring = false)
        {
            var img = Fill(parent, name).gameObject.AddComponent<Image>();
            img.color = color; img.raycastTarget = false;
            if (radius > 0) Round(img, radius, ring);
            return img;
        }

        /// Words: `face` at `size`, a hard drop under them when `drop` (his display titles on a scene).
        public static Text Words(Transform parent, string name, string text, Font face, int size, Color color, TextAnchor anchor, float drop = 0)
        {
            var t = Fill(parent, name).gameObject.AddComponent<Text>();
            t.font = face; t.fontSize = size; t.color = color; t.alignment = anchor; t.text = text;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false; t.lineSpacing = 0.9f;
            if (drop > 0) { var sh = t.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0, 0, 0, 0.38f); sh.effectDistance = new Vector2(0, -drop); }
            return t;
        }

        /// A rendered object on a soft glow of `tint`, with a shadow under it.
        public static Image Object(Transform parent, string name, string artName, float size, Color? tint = null)
        {
            var holder = Box(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
            if (tint is Color c)
            {
                var g = Paint(holder, "Glow", new Color(c.r, c.g, c.b, 0.3f));
                g.sprite = Glow; g.type = Image.Type.Simple;
                g.rectTransform.offsetMin = new Vector2(-size * 0.12f, -size * 0.12f); g.rectTransform.offsetMax = new Vector2(size * 0.12f, size * 0.12f);
            }
            var img = Fill(holder, "Art").gameObject.AddComponent<Image>();
            img.sprite = Art(artName); img.preserveAspect = true; img.raycastTarget = false;
            var sh = img.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0, 0, 0, 0.25f); sh.effectDistance = new Vector2(0, -size * 0.05f);
            return img;
        }

        /// A slab: `face` on a darker `base` showing under it, pressed into the base while held. Content goes on
        /// the returned face (which sinks with the press).
        public static HoldButton Slab(Transform parent, string name, Color face, Color baseColor, float radius, out RectTransform content, float baseDepth = 15)
        {
            var root = Box(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(100, 100));
            var hit = root.gameObject.AddComponent<Image>(); hit.color = Color.clear;
            var b = Paint(root, "Base", baseColor, radius);
            b.rectTransform.offsetMin = new Vector2(0, -baseDepth); b.rectTransform.offsetMax = new Vector2(0, -baseDepth);
            var f = Paint(root, "Face", face, radius);
            content = f.rectTransform;
            var hold = root.gameObject.AddComponent<HoldButton>();
            hold.Fill = f; hold.RestColor = face; hold.PressedColor = face; hold.Sink = content; hold.Sound = "pop";
            return hold;
        }

        public enum Style { Primary, Secondary, Quiet }

        /// His ClubButton: a sun-yellow slab (cream for secondary, see-through for quiet) with the title, a
        /// subtitle under it, and an object or glyph beside it. `size` is his point size of the title.
        public static HoldButton Button(Transform parent, string name, string title, Style style = Style.Primary, float size = 22, string subtitle = null, string artName = null)
        {
            var (face, bas, ink) = style switch
            {
                Style.Primary => (Sun, SunDeep, Ink),
                Style.Secondary => (Cream, CreamDeep, Ink),
                _ => (White(0.16f), new Color(0, 0, 0, 0.25f), Color.white),
            };
            var hold = Slab(parent, name, face, bas, size * 3 * 0.7f, out var content);
            float s = size * 3;
            float left = s * 0.95f;
            if (artName != null)
            {
                var o = Object(content, "Object", artName, s * 1.5f);
                var ort = (RectTransform)o.transform.parent;
                ort.anchorMin = ort.anchorMax = new Vector2(0, 0.5f); ort.anchoredPosition = new Vector2(left + s * 0.6f, 0);
                left += s * 1.5f + s * 0.4f;
            }
            var t = Words(content, "Title", title, Title, Mathf.RoundToInt(s), ink, subtitle == null && artName == null ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft);
            t.rectTransform.offsetMin = new Vector2(artName == null && subtitle == null ? s * 0.6f : left, subtitle != null ? s * 0.55f : 0);
            t.rectTransform.offsetMax = new Vector2(-s * 0.6f, 0);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            if (subtitle != null)
            {
                var st = Words(content, "Subtitle", subtitle, UiMedium, Mathf.RoundToInt(s * 0.55f), new Color(ink.r, ink.g, ink.b, 0.75f), TextAnchor.MiddleLeft);
                st.rectTransform.offsetMin = new Vector2(left, 0); st.rectTransform.offsetMax = new Vector2(-s * 0.6f, -s * 0.85f);
            }
            return hold;
        }

        public enum BadgeKind { StartHere, Locked, Soon, Done, Next, Boss, New }

        /// A small capsule: START HERE, LOCKED, COMING SOON, DONE ✓, NEXT UP, FINAL BOSS.
        public static RectTransform Badge(Transform parent, BadgeKind kind, string text = null)
        {
            var (words, fg, bg) = kind switch
            {
                BadgeKind.StartHere => ("START HERE", Ink, Sun),
                BadgeKind.Locked => ("LOCKED", Color.white, Muted),
                BadgeKind.Soon => ("COMING SOON", Color.white, Violet),
                BadgeKind.Done => ("DONE ✓", Color.white, Green),
                BadgeKind.Next => ("NEXT UP", Ink, Sun),
                BadgeKind.Boss => ("FINAL BOSS", Color.white, Coral),
                _ => ("NEW!", Ink, Sun),
            };
            words = text ?? words;
            var rt = Box(parent, "Badge", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(100, 45));
            var img = rt.gameObject.AddComponent<Image>(); img.color = bg; img.raycastTarget = false;
            img.sprite = UiKit.Circle; img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 32f / 22f;
            var t = Words(rt, "Text", words, Caps, 30, fg, TextAnchor.MiddleCenter);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            rt.sizeDelta = new Vector2(t.preferredWidth + 48, 45);
            return rt;
        }

        /// His ClubCard: a cream slab with a coloured strip along its top, a rendered object on a glow of that
        /// colour, the title and a line under it, and an optional badge; locked, it greys and shows the padlock.
        public sealed class Card
        {
            public HoldButton Button;
            public RectTransform Root, Face;
            public Text Title, Subtitle;
            public Image Art;
            RectTransform badge;
            readonly bool horizontal;
            readonly CanvasGroup group;
            readonly Image faceImage, baseImage, strip;
            readonly Color tint;
            string artName;

            public Card(Transform parent, string name, string artName, string title, string subtitle, Color tint, bool horizontal, float scale = 1f)
            {
                this.horizontal = horizontal; this.tint = tint; this.artName = artName;
                Button = Slab(parent, name, Cream, CreamDeep, 72, out Face, 18);
                Root = (RectTransform)Button.transform;
                group = Root.gameObject.AddComponent<CanvasGroup>();
                faceImage = Face.GetComponent<Image>(); baseImage = Root.Find("Base").GetComponent<Image>();
                Face.gameObject.AddComponent<Mask>().showMaskGraphic = true;
                strip = Paint(Face, "Strip", tint);
                strip.rectTransform.anchorMin = new Vector2(0, 1); strip.rectTransform.pivot = new Vector2(0.5f, 1);
                strip.rectTransform.offsetMin = new Vector2(0, -18); strip.rectTransform.offsetMax = Vector2.zero;
                float artSize = (horizontal ? 190 : 230) * scale;
                Art = Object(Face, "Object", artName, artSize, tint);
                var art = (RectTransform)Art.transform.parent;
                Title = Words(Face, "Title", title, Club.Title, Mathf.RoundToInt(66 * scale), Ink, TextAnchor.UpperLeft);
                Title.horizontalOverflow = HorizontalWrapMode.Overflow;
                Subtitle = Words(Face, "Subtitle", subtitle, UiMedium, Mathf.RoundToInt(38 * scale), Muted, TextAnchor.UpperLeft);
                if (horizontal)
                {
                    art.anchorMin = art.anchorMax = new Vector2(0, 0.5f); art.anchoredPosition = new Vector2(48 + artSize / 2, -6);
                    float x = 48 + artSize + 36;
                    Title.rectTransform.offsetMin = new Vector2(x, 0); Title.rectTransform.offsetMax = new Vector2(-36, 0);
                    Subtitle.rectTransform.offsetMin = new Vector2(x, 0); Subtitle.rectTransform.offsetMax = new Vector2(-36, 0);
                }
                else
                {
                    art.anchorMin = art.anchorMax = new Vector2(0.5f, 1); art.anchoredPosition = new Vector2(0, -40 - artSize / 2);
                    Title.rectTransform.offsetMin = new Vector2(44, 0); Title.rectTransform.offsetMax = new Vector2(-30, 0);
                    Subtitle.rectTransform.offsetMin = new Vector2(44, 0); Subtitle.rectTransform.offsetMax = new Vector2(-30, 0);
                }
                Layout();
            }

            /// Where the words go: in the middle beside the object (horizontal) or under it.
            void Layout()
            {
                float badgeH = badge ? 60 : 0;
                float titleH = Title.fontSize * 1.1f, subH = Subtitle.preferredHeight;
                if (horizontal)
                {
                    float block = badgeH + titleH + 6 + subH;
                    float top = block / 2;
                    if (badge) { badge.anchorMin = badge.anchorMax = new Vector2(0, 0.5f); badge.pivot = new Vector2(0, 1); badge.anchoredPosition = new Vector2(Title.rectTransform.offsetMin.x, top); }
                    Place(Title, top - badgeH, titleH); Place(Subtitle, top - badgeH - titleH - 6, subH + 10);
                }
                else
                {
                    var art = (RectTransform)Art.transform.parent;
                    float y = art.anchoredPosition.y - art.sizeDelta.y / 2 - 22;
                    if (badge) { badge.anchorMin = badge.anchorMax = new Vector2(0, 1); badge.pivot = new Vector2(0, 1); badge.anchoredPosition = new Vector2(44, y); y -= badgeH; }
                    Title.rectTransform.anchorMin = new Vector2(0, 1); Title.rectTransform.anchorMax = new Vector2(1, 1); Title.rectTransform.pivot = new Vector2(0.5f, 1);
                    Title.rectTransform.anchoredPosition = new Vector2(Title.rectTransform.anchoredPosition.x, y); Title.rectTransform.sizeDelta = new Vector2(-74, titleH);
                    Subtitle.rectTransform.anchorMin = new Vector2(0, 1); Subtitle.rectTransform.anchorMax = new Vector2(1, 1); Subtitle.rectTransform.pivot = new Vector2(0.5f, 1);
                    Subtitle.rectTransform.anchoredPosition = new Vector2(Subtitle.rectTransform.anchoredPosition.x, y - titleH - 4); Subtitle.rectTransform.sizeDelta = new Vector2(-74, subH + 10);
                }
            }

            static void Place(Text t, float top, float height)
            {
                var rt = t.rectTransform;
                rt.anchorMin = new Vector2(0, 0.5f); rt.anchorMax = new Vector2(1, 0.5f); rt.pivot = new Vector2(0.5f, 1);
                rt.anchoredPosition = new Vector2((rt.offsetMin.x + rt.offsetMax.x) / 2, top); rt.sizeDelta = new Vector2(rt.sizeDelta.x, height);
            }

            public Card Set(string title, string subtitle)
            {
                Title.text = title; Subtitle.text = subtitle; Layout();
                return this;
            }

            public Card SetBadge(BadgeKind? kind, string text = null)
            {
                if (badge) UnityEngine.Object.Destroy(badge.gameObject);
                badge = kind is BadgeKind k ? Club.Badge(Face, k, text) : null;
                Layout();
                return this;
            }

            /// Locked: the padlock for the object, the colours drained (his saturation 0.2, opacity 0.82).
            public Card SetLocked(bool locked)
            {
                Art.sprite = Club.Art(locked ? "lock" : artName);
                group.alpha = locked ? 0.82f : 1f;
                faceImage.color = locked ? UiKit.Hex("E4E1DA") : Cream; Button.RestColor = faceImage.color; Button.PressedColor = faceImage.color;
                strip.color = locked ? UiKit.Hex("A7A3AE") : tint;
                return this;
            }

            /// The highlighted card (the sun slab).
            public Card SetHighlight(bool on)
            {
                faceImage.color = on ? Sun : Cream; baseImage.color = on ? SunDeep : CreamDeep;
                Button.RestColor = faceImage.color; Button.PressedColor = faceImage.color;
                return this;
            }

            public Card SetArt(string name) { artName = name; Art.sprite = Club.Art(name); return this; }
        }

        /// His ClubPill: a capsule, sun when selected, else a see-through white with a hairline.
        public sealed class Pill
        {
            public HoldButton Button;
            public RectTransform Root;
            public Text Label;
            readonly Image fill, ring;
            public Image Icon;

            public Pill(Transform parent, string name, string title, float height = 108, string icon = null)
            {
                Root = Box(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300, height));
                fill = Root.gameObject.AddComponent<Image>();
                fill.sprite = UiKit.Circle; fill.type = Image.Type.Sliced; fill.pixelsPerUnitMultiplier = 32f / (height / 2);
                ring = Paint(Root, "Hairline", White(0.25f));
                ring.sprite = RoundedRing; ring.type = Image.Type.Sliced; ring.pixelsPerUnitMultiplier = 80f / (height / 2);
                float x = 0;
                if (icon != null)
                {
                    Icon = Icons.Place(Root, icon, Color.white, new Vector2(0, 0.5f), new Vector2(height * 0.55f, 0), height * 0.46f);
                    x = height * 0.45f;
                }
                Label = Words(Root, "Label", title, Ui, Mathf.RoundToInt(height * 0.44f), Color.white, TextAnchor.MiddleCenter);
                Label.horizontalOverflow = HorizontalWrapMode.Overflow;
                Label.rectTransform.offsetMin = new Vector2(x, 0);
                Button = Root.gameObject.AddComponent<HoldButton>();
                Button.Fill = fill; Button.Sink = null; Button.Sound = "tick";
                Root.sizeDelta = new Vector2(Label.preferredWidth + height * 0.9f + x, height);
                Select(false);
            }

            public void Select(bool on)
            {
                fill.color = on ? Sun : White(0.14f);
                Button.RestColor = fill.color; Button.PressedColor = on ? SunDeep : White(0.3f);
                ring.enabled = !on;
                Label.color = on ? Ink : Color.white;
                if (Icon) Icon.color = on ? Ink : Color.white;
            }
        }

        /// The top of every screen: the crest, ISLAND SPORTS CLUB over where you are ("Clubhouse › Cliffside"),
        /// and on the right your player's chip (skin, shirt, name), which is a button.
        public sealed class Header
        {
            public RectTransform Root;
            public HoldButton Chip;
            readonly Text crumbs, chipName;
            readonly Image chipSkin, chipRing;

            public Header(Transform parent, string[] breadcrumb, bool chip = true)
            {
                Root = Box(parent, "Header", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -30), new Vector2(-108, 140));
                var crest = Object(Root, "Crest", "crest", 132);
                var crt = (RectTransform)crest.transform.parent;
                crt.anchorMin = crt.anchorMax = new Vector2(0, 0.5f); crt.anchoredPosition = new Vector2(66, 0);
                crt.gameObject.name = "Crest";
                var club = Words(Root, "Club", "ISLAND SPORTS CLUB", Caps, 30, White(0.75f), TextAnchor.LowerLeft);
                club.rectTransform.offsetMin = new Vector2(162, 76); club.rectTransform.offsetMax = new Vector2(0, -14);
                crumbs = Words(Root, "Where", "", Title, 60, Color.white, TextAnchor.UpperLeft);
                crumbs.supportRichText = true; crumbs.horizontalOverflow = HorizontalWrapMode.Overflow;
                crumbs.rectTransform.offsetMin = new Vector2(162, 0); crumbs.rectTransform.offsetMax = new Vector2(0, -66);
                Where(breadcrumb);
                if (!chip) return;
                var c = Box(Root, "Profile", new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(300, 102));
                var bg = c.gameObject.AddComponent<Image>(); bg.color = White(0.14f); bg.sprite = UiKit.Circle; bg.type = Image.Type.Sliced; bg.pixelsPerUnitMultiplier = 32f / 51f;
                var ring = Paint(c, "Hairline", White(0.22f)); ring.sprite = RoundedRing; ring.type = Image.Type.Sliced; ring.pixelsPerUnitMultiplier = 80f / 51f;
                chipRing = Box(c, "Shirt", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(15 + 39, 0), new Vector2(78, 78)).gameObject.AddComponent<Image>();
                chipRing.sprite = UiKit.Circle; chipRing.raycastTarget = false;
                chipSkin = Paint(chipRing.transform, "Skin", Color.white); chipSkin.sprite = UiKit.Circle;
                chipSkin.rectTransform.offsetMin = new Vector2(9, 9); chipSkin.rectTransform.offsetMax = new Vector2(-9, -9);
                chipName = Words(c, "Name", "", Caps, 42, Color.white, TextAnchor.MiddleLeft);
                chipName.horizontalOverflow = HorizontalWrapMode.Overflow;
                chipName.rectTransform.offsetMin = new Vector2(108, 0); chipName.rectTransform.offsetMax = new Vector2(-30, 0);
                Chip = c.gameObject.AddComponent<HoldButton>();
                Chip.Fill = bg; Chip.RestColor = bg.color; Chip.PressedColor = White(0.3f); Chip.Sound = "pop";
            }

            /// Where you are: the last step white, the ones before it faded, chevrons between.
            public void Where(params string[] breadcrumb)
            {
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < breadcrumb.Length; i++)
                {
                    if (i > 0) sb.Append("<color=#FFFFFF80>  ›  </color>");
                    sb.Append(i == breadcrumb.Length - 1 ? breadcrumb[i] : $"<color=#FFFFFF99>{breadcrumb[i]}</color>");
                }
                crumbs.text = sb.ToString();
            }

            public void Player(string name, Color skin, Color shirt)
            {
                if (!Chip) return;
                chipName.text = name;
                chipSkin.color = skin; chipRing.color = shirt;
                ((RectTransform)Chip.transform).sizeDelta = new Vector2(Mathf.Min(420, chipName.preferredWidth + 150), 102);
            }
        }

        /// A gradient of the lagoon's deep colour over part of the screen (for type over a live 3-D scene):
        /// `top` true is dark at the top edge fading down over `height`, else dark at the bottom fading up.
        public static Image Shade(Transform parent, string name, bool top, float height, float alpha)
        {
            var img = Box(parent, name, new Vector2(0, top ? 1 : 0), new Vector2(1, top ? 1 : 0), new Vector2(0.5f, top ? 1 : 0), Vector2.zero, new Vector2(0, height)).gameObject.AddComponent<Image>();
            img.sprite = Ramp; img.color = Deep(alpha); img.raycastTarget = false;
            if (!top) img.rectTransform.localScale = new Vector3(1, -1, 1);
            return img;
        }

        /// Cards rising in one after another (his clubEntrance): `index` places each in the cascade.
        public static void Enter(Component c, int index) { var e = c.gameObject.AddComponent<Entrance>(); e.Index = index; }

        public sealed class Entrance : MonoBehaviour
        {
            public int Index;
            float start;
            Vector2 home; Vector3 size; bool placed;
            CanvasGroup group;
            void OnEnable() { start = Time.unscaledTime + 0.08f + Index * 0.045f; if (!TryGetComponent(out group)) group = gameObject.AddComponent<CanvasGroup>(); Tick(); }
            void Update() => Tick();
            void Tick()
            {
                var rt = (RectTransform)transform;
                if (!placed) { home = rt.anchoredPosition; size = rt.localScale; placed = true; }
                float u = Mathf.Clamp01((Time.unscaledTime - start) / 0.42f);
                // a spring's overshoot (his response 0.42, damping 0.72)
                float e = 1 - Mathf.Exp(-6f * u) * Mathf.Cos(7f * u);
                rt.anchoredPosition = home + new Vector2(0, -90f * (1 - e));
                rt.localScale = size * Mathf.Lerp(0.94f, 1f, e);
                group.alpha = Mathf.Clamp01(u * 3f);
                if (u >= 1) { rt.anchoredPosition = home; rt.localScale = size; group.alpha = 1; enabled = false; }
            }
        }
    }

    /// The menu's sounds: tick on a move, pop on a press, whoosh between screens (his MenuSounds).
    public static class ClubSound
    {
        static AudioSource source;
        static readonly Dictionary<string, AudioClip> clips = new();

        public static void Play(string name, float volume = 0.5f)
        {
            if (string.IsNullOrEmpty(name)) return;
            if (!source)
            {
                var go = new GameObject("Club sounds");
                UnityEngine.Object.DontDestroyOnLoad(go);
                source = go.AddComponent<AudioSource>();
                source.playOnAwake = false; source.spatialBlend = 0;
            }
            if (!clips.TryGetValue(name, out var clip)) clips[name] = clip = Resources.Load<AudioClip>("Club/Sounds/" + name);
            if (clip) source.PlayOneShot(clip, volume);
        }
    }

    /// The stripe wipe between screens: three diagonal bands (sun, coral, violet) sweep across and away.
    public sealed class ClubWipe : MonoBehaviour
    {
        static ClubWipe instance;
        RectTransform[] bands;
        float start = -9;

        public static void Play()
        {
            if (!instance)
            {
                var go = new GameObject("Club wipe");
                DontDestroyOnLoad(go);
                var canvas = UiKit.Canvas(go);
                canvas.sortingOrder = 500;
                go.GetComponent<GraphicRaycaster>().enabled = false;
                instance = go.AddComponent<ClubWipe>();
                var colors = new[] { Club.Sun, Club.Coral, Club.Violet };
                instance.bands = new RectTransform[3];
                for (int i = 0; i < 3; i++)
                {
                    var b = Club.Box(go.transform, $"Band {i}", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 5200));
                    var img = b.gameObject.AddComponent<Image>(); img.color = colors[i]; img.raycastTarget = false;
                    b.localRotation = Quaternion.Euler(0, 0, -20);
                    instance.bands[i] = b;
                }
            }
            ClubSound.Play("whoosh", 0.45f);
            instance.start = Time.unscaledTime;
            instance.gameObject.SetActive(true);
            instance.Update();
        }

        void Update()
        {
            float u = (Time.unscaledTime - start) / 0.55f;
            if (u > 1) { gameObject.SetActive(false); return; }
            float e = u < 0.5f ? 2 * u * u : 1 - Mathf.Pow(-2 * u + 2, 2) / 2;   // ease in-out
            float w = 1080 * 1.6f;
            for (int i = 0; i < bands.Length; i++)
            {
                float p = Mathf.Lerp(-0.6f, 1.6f, e) - i * 0.12f;
                bands[i].anchoredPosition = new Vector2(p * w - 1080 * 0.8f - 540, 0);
            }
        }
    }
}
