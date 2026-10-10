using GolfArcade.Course;
using UnityEngine;
using UnityEngine.UI;

namespace GolfArcade.UI
{
    /// The screen between two holes. Building the next hole takes a few seconds of the main thread, so the game puts this
    /// up first, lets it be drawn, and only then builds. It is a postcard of the hole that is coming: its number on a red
    /// flag and its name on a ribbon, the hole drawn from above from its own numbers (the sea, the island, the fairway, the
    /// hazards, the green and its flag), par, length and what is in the way, and a ball that rolls from the tee to the pin as
    /// the hole comes together, so the wait has a clear end. It covers the whole display (an overlay canvas on the display the
    /// course camera draws to, so the TV gets it as well as the phone).
    public sealed class HoleLoadingCover : MonoBehaviour
    {
        public struct Info
        {
            public Hole Hole;
            public string Course, Name, Blurb;
            /// The hole's place in its round (1 for the first), how many holes the round has, par and length in yards.
            public int Ordinal, Count, Par;
            public double Yards;
        }

        public const float FadeIn = 0.14f, FadeOut = 0.32f;
        static readonly Color Ink = UiKit.Hex("10243D"), Lime = UiKit.Hex("D7F044"), Cream = UiKit.Hex("FFF9EE"), FlagRed = UiKit.Hex("D9161C");

        Camera view;
        Canvas canvas;
        CanvasScaler scaler;
        CanvasGroup group;
        RectTransform root, tab, dots, flagBadge, ribbon, card, cardShadow, chips, note, statusRow, ball;
        Text courseText, flagNumber, title, noteText, status;
        HoleMapGraphic map;
        HoleMapGraphic[] layers = new HoleMapGraphic[0];
        TrailGraphic trail;
        Image statusDot;
        bool portraitBuilt = true, laidOut;
        float shownAt = -1, hideAt = -1, shown, target, progress, lastWidth, lastHeight;
        bool visible;
        Info info;

        /// Up on screen, or fading out.
        public bool Visible => visible || group.alpha > 0.001f;
        /// The cover is fully opaque: nothing under it can be seen, so the game may stall.
        public bool Opaque => visible && group.alpha >= 0.999f;
        /// Seconds it has been up (unscaled), 0 when it is not.
        public float ShownFor => visible && shownAt >= 0 ? Time.unscaledTime - shownAt : 0;
        public float Progress => progress;
        public string Status => status ? status.text : "";
        public string Title => title ? title.text : "";
        public string Number => flagNumber ? flagNumber.text : "";

        public static HoleLoadingCover Create(Camera camera)
        {
            var go = new GameObject("Hole loading cover", typeof(RectTransform));
            var canvas = UiKit.Canvas(go);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            canvas.targetDisplay = camera ? camera.targetDisplay : 0;
            var c = go.AddComponent<HoleLoadingCover>();
            c.view = camera; c.canvas = canvas; c.scaler = go.GetComponent<CanvasScaler>();
            c.Build();
            go.SetActive(false);
            return c;
        }

        // ---- Building

        static RectTransform Node(Transform parent, string name)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1);
            return rt;
        }

        static T Add<T>(Transform parent, string name) where T : Graphic
        {
            var rt = Node(parent, name);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; rt.pivot = new Vector2(.5f, .5f);
            var g = rt.gameObject.AddComponent<T>(); g.raycastTarget = false;
            return g;
        }

        static Image Rounded(Transform parent, string name, Color color, float radius, bool ring = false)
        {
            var img = Club.Paint(parent, name, color, radius, ring); img.raycastTarget = false; return img;
        }

        static Image Disc(Transform parent, string name, Color color)
        {
            var img = Rounded(parent, name, color, 0); img.sprite = UiKit.Circle; img.type = Image.Type.Simple; return img;
        }

        static Text Words(Transform parent, string name, Font face, int size, Color color, TextAnchor anchor)
        {
            var t = Club.Words(parent, name, "", face, size, color, anchor); t.horizontalOverflow = HorizontalWrapMode.Overflow; return t;
        }

        void Build()
        {
            group = gameObject.AddComponent<CanvasGroup>();
            root = Club.Fill(transform, "Cover");
            // the sea-blue sky, slanted bands and a glow in the corner: the candy colours of a party game's loading screen
            var back = Add<GradientGraphic>(root, "Back"); back.Vertical = true; back.raycastTarget = true;
            back.Stops = new[] { UiKit.Hex("17b0dd"), UiKit.Hex("1673b8"), UiKit.Hex("0f3d6e"), UiKit.Hex("0a2442") };
            var bands = Add<StripesGraphic>(root, "Bands"); bands.color = new Color(1, 1, 1, .055f);
            var glow = Disc(root, "Glow", new Color(.55f, .92f, .92f, .55f)); glow.sprite = Club.Glow;
            glow.rectTransform.anchorMin = glow.rectTransform.anchorMax = new Vector2(.9f, 1f); glow.rectTransform.sizeDelta = new Vector2(2000, 1700);

            // the course and the round's holes
            tab = Node(root, "Course tab");
            var coursePill = Rounded(tab, "Pill", Cream, 80);
            courseText = Words(tab, "Course", UiKit.Display, 40, Ink, TextAnchor.MiddleCenter);
            dots = Node(root, "Dots");

            // the hole's number on a red flag, its name on a ribbon
            flagBadge = Node(root, "Flag");
            var flagBase = Rounded(flagBadge, "Base", UiKit.Hex("7d0c10"), 26); flagBase.rectTransform.offsetMin = new Vector2(0, -14); flagBase.rectTransform.offsetMax = new Vector2(0, -14);
            Rounded(flagBadge, "Face", FlagRed, 26);
            var flagRing = Rounded(flagBadge, "Rim", new Color(1, 1, 1, .85f), 18, true); flagRing.rectTransform.offsetMin = Vector2.one * 14; flagRing.rectTransform.offsetMax = -Vector2.one * 14;
            flagNumber = Words(flagBadge, "Number", UiKit.Display, 170, Color.white, TextAnchor.MiddleCenter);
            ribbon = Node(root, "Ribbon");
            var ribShadow = Add<Image>(ribbon, "Shadow"); ribShadow.color = new Color(.03f, .1f, .18f, .55f); ribShadow.rectTransform.offsetMin = new Vector2(0, -22); ribShadow.rectTransform.offsetMax = new Vector2(0, -22);
            var ribFill = Add<GradientGraphic>(ribbon, "Fill"); ribFill.Stops = new[] { UiKit.Hex("bfe83a"), UiKit.Hex("e8f87a"), UiKit.Hex("9ee06b") };
            foreach (var side in new[] { 1f, 0f })
            {
                var edge = Add<Image>(ribbon, side > 0 ? "Top edge" : "Bottom edge"); edge.color = Color.white;
                edge.rectTransform.anchorMin = new Vector2(0, side); edge.rectTransform.anchorMax = new Vector2(1, side); edge.rectTransform.pivot = new Vector2(.5f, side);
                edge.rectTransform.sizeDelta = new Vector2(0, 10); edge.rectTransform.anchoredPosition = Vector2.zero;
            }
            title = UiKit.Chunky(ribbon, "Name", 150, Color.white, Ink, 7);
            title.horizontalOverflow = HorizontalWrapMode.Overflow; title.alignment = TextAnchor.MiddleLeft;

            // the postcard: the hole from above in a white frame, the ball rolling along it
            cardShadow = Node(root, "Postcard shadow"); Rounded(cardShadow, "Shade", new Color(.03f, .1f, .18f, .5f), 64);
            card = Node(root, "Postcard"); Rounded(card, "Frame", Color.white, 64);
            var inner = Club.Fill(card, "Picture", 26);
            var mask = inner.gameObject.AddComponent<Image>(); mask.sprite = Club.Rounded; mask.type = Image.Type.Sliced; mask.pixelsPerUnitMultiplier = 80f / 42f; mask.raycastTarget = false;
            inner.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            // the picture in layers: the sea and land, the ground inside a mask of the land, the line and the flag over all of it
            map = Add<HoleMapGraphic>(inner, "Hole"); map.Part = HoleMapGraphic.Layer.Base;
            var shape = Add<HoleMapGraphic>(inner, "Land"); shape.Part = HoleMapGraphic.Layer.LandShape;
            shape.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var ground = Add<HoleMapGraphic>(shape.transform, "Ground"); ground.Part = HoleMapGraphic.Layer.Ground;
            var marks = Add<HoleMapGraphic>(inner, "Marks"); marks.Part = HoleMapGraphic.Layer.Marks;
            layers = new[] { map, shape, ground, marks };
            trail = Add<TrailGraphic>(inner, "Trail");
            var ballRim = Disc(inner, "Ball", Ink);
            ball = ballRim.rectTransform; ball.anchorMin = ball.anchorMax = new Vector2(.5f, .5f); ball.sizeDelta = new Vector2(48, 48);
            var ballFace = Disc(ball, "Face", Color.white);
            ballFace.rectTransform.offsetMin = Vector2.one * 5; ballFace.rectTransform.offsetMax = -Vector2.one * 5;
            foreach (var spot in new[] { new Vector2(-7, 6), new Vector2(7, 3), new Vector2(0, -8) })
            {
                var d = Disc(ballFace.transform, "Dimple", new Color(.72f, .78f, .81f));
                d.rectTransform.anchorMin = d.rectTransform.anchorMax = new Vector2(.5f, .5f); d.rectTransform.sizeDelta = new Vector2(6, 6); d.rectTransform.anchoredPosition = spot;
            }

            chips = Node(root, "Chips");
            note = Node(root, "Note");
            Rounded(note, "Panel", new Color(1, .98f, .93f, .13f), 44);
            Rounded(note, "Edge", new Color(1, 1, 1, .28f), 44, true);
            noteText = Club.Words(note, "Words", "", Club.UiMedium, 42, Color.white, TextAnchor.MiddleLeft);
            noteText.rectTransform.offsetMin = new Vector2(44, 12); noteText.rectTransform.offsetMax = new Vector2(-44, -12);
            statusRow = Node(root, "Status");
            statusDot = Disc(statusRow, "Dot", Lime);
            statusDot.rectTransform.anchorMin = statusDot.rectTransform.anchorMax = new Vector2(0, .5f); statusDot.rectTransform.pivot = new Vector2(.5f, .5f);
            statusDot.rectTransform.sizeDelta = new Vector2(26, 26);
            status = Club.Words(statusRow, "Words", "", Club.UiMedium, 48, Color.white, TextAnchor.MiddleLeft);
            status.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        // ---- Layout

        bool Portrait => !view || (OffscreenSize.Override is Vector2Int o ? o.x < o.y * 1.2f : view.aspect < 1.2f);

        /// Lays the screen out against the surface it is being drawn to now (for the offscreen captures).
        public void RefreshLayout() { if (gameObject.activeInHierarchy) Layout(); }

        static void Put(RectTransform rt, Vector2 anchor, Vector2 pivot, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = pivot; rt.anchoredPosition = new Vector2(x, y); rt.sizeDelta = new Vector2(w, h);
        }

        void Layout()
        {
            bool portrait = Portrait;
            float width = view ? OffscreenSize.Width(view) : Screen.width, height = view ? OffscreenSize.Height(view) : Screen.height;
            float scale = Mathf.Max(.1f, portrait ? width / 1080f : height / 1080f);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = scale; canvas.scaleFactor = scale;
            canvas.targetDisplay = view ? view.targetDisplay : 0;
            lastWidth = width; lastHeight = height;
            float H = height / scale, W = width / scale;
            float safeTop = 0;
            if (view && view.targetDisplay == 0 && !view.targetTexture && OffscreenSize.Override == null) safeTop = Mathf.Max(0, Screen.height - Screen.safeArea.yMax) / scale;
            laidOut = true; portraitBuilt = portrait;
            var topLeft = new Vector2(0, 1); var bottomLeft = new Vector2(0, 0); var bottomMid = new Vector2(.5f, 0);
            var middleLeft = new Vector2(0, .5f);
            if (portrait)
            {
                float top = 118 + safeTop;
                courseText.fontSize = 42;
                Put(tab, topLeft, topLeft, 80, -top, Mathf.Max(260, courseText.preferredWidth + 88), 92);
                Put(dots, topLeft, topLeft, 80 + tab.sizeDelta.x + 24, -top - 31, 400, 30);
                Put(flagBadge, topLeft, topLeft, 80, -(top + 190), 250, 250); flagNumber.fontSize = 170;
                Put(ribbon, topLeft, middleLeft, 300, -(top + 190 + 125), W - 300 + 60, 210); ribbon.localEulerAngles = new Vector3(0, 0, -3f);
                title.rectTransform.offsetMin = new Vector2(70, 8); title.rectTransform.offsetMax = new Vector2(-110, -8);
                float cardTop = top + 190 + 250 + 70, bottom = 560 + 60;
                Put(cardShadow, topLeft, topLeft, 90, -(cardTop + 36), 900, H - cardTop - bottom);
                Put(card, topLeft, topLeft, 90, -cardTop, 900, H - cardTop - bottom); card.localEulerAngles = new Vector3(0, 0, -1.2f); cardShadow.localEulerAngles = card.localEulerAngles;
                Put(chips, bottomLeft, bottomLeft, 90, 440, 900, 110);
                Put(note, bottomLeft, bottomLeft, 90, 235, 900, 170); noteText.fontSize = 42;
                Put(statusRow, bottomMid, new Vector2(.5f, 0), 0, 110, 800, 64); status.fontSize = 48;
            }
            else
            {
                courseText.fontSize = 30;
                Put(tab, topLeft, topLeft, 120, -(70 + safeTop), Mathf.Max(220, courseText.preferredWidth + 64), 64);
                Put(dots, topLeft, topLeft, 120 + tab.sizeDelta.x + 22, -(70 + safeTop) - 17, 400, 30);
                Put(flagBadge, topLeft, topLeft, 120, -250, 210, 210); flagNumber.fontSize = 140;
                Put(ribbon, topLeft, middleLeft, 300, -(250 + 105), 740, 170); ribbon.localEulerAngles = new Vector3(0, 0, -3f);
                title.rectTransform.offsetMin = new Vector2(64, 6); title.rectTransform.offsetMax = new Vector2(-70, -6);
                Put(cardShadow, topLeft, topLeft, 1060, -126, 700, H - 180);
                Put(card, topLeft, topLeft, 1060, -90, 700, H - 180); card.localEulerAngles = new Vector3(0, 0, 1.6f); cardShadow.localEulerAngles = card.localEulerAngles;
                Put(chips, topLeft, topLeft, 120, -560, 800, 84);
                Put(note, topLeft, topLeft, 120, -690, 780, 190); noteText.fontSize = 38;
                Put(statusRow, bottomLeft, bottomLeft, 120, 90, 700, 64); status.fontSize = 40;
            }
            title.fontSize = portrait ? 150 : 120; Icons.Fit(title, 40, title.fontSize);
            ArrangeChips(portrait);
            // the status: a pulsing dot, then the words, together in the middle (phone) or at the left (big screen)
            float rowWidth = status.preferredWidth + 46, rowLeft = portrait ? (statusRow.rect.width - rowWidth) / 2 : 0;
            statusDot.rectTransform.anchoredPosition = new Vector2(rowLeft + 13, 0);
            status.rectTransform.offsetMin = new Vector2(rowLeft + 46, 0); status.rectTransform.offsetMax = Vector2.zero;
            status.alignment = TextAnchor.MiddleLeft;
            foreach (var layer in layers) layer.SetHole(info.Hole, portrait ? 56 : 52);
            ApplyProgress(shown);
        }

        void ArrangeChips(bool portrait)
        {
            foreach (Transform child in chips) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            float h = portrait ? 110 : 84, size = portrait ? 52 : 42, pad = portrait ? 48 : 40, x = 0;
            void Chip(string text, Color fill)
            {
                var rt = Node(chips, "Chip"); rt.anchorMin = rt.anchorMax = new Vector2(0, .5f); rt.pivot = new Vector2(0, .5f);
                Rounded(rt, "Pill", fill, 80);
                var t = Words(rt, "Text", UiKit.Display, (int)size, Ink, TextAnchor.MiddleCenter);
                t.text = text;
                float w = t.preferredWidth + 2 * pad;
                rt.sizeDelta = new Vector2(w, h); rt.anchoredPosition = new Vector2(x, 0);
                x += w + (portrait ? 20 : 16);
            }
            Chip($"PAR {info.Par}", Cream);
            Chip($"{info.Yards:F0} YD", Cream);
            if (info.Hole != null)
            {
                int wet = 0, sand = 0; bool lava = false;
                foreach (var hz in info.Hole.Hazards)
                {
                    if (hz.Kind == HazardKind.Water || hz.Kind == HazardKind.Lava) { wet++; lava |= hz.Kind == HazardKind.Lava; }
                    else if (hz.Kind == HazardKind.Bunker) sand++;
                }
                if (wet > 0) Chip($"{wet} {(lava ? "LAVA" : "WATER")}", UiKit.Hex("bdeaff"));
                else if (sand > 0) Chip($"{sand} BUNKER{(sand > 1 ? "S" : "")}", UiKit.Hex("f7e6a6"));
            }
        }

        // ---- Showing

        /// Up over everything, fading in over 0.14 s; progress starts again from nothing.
        public void Show(Info next)
        {
            info = next;
            gameObject.SetActive(true);
            canvas.targetDisplay = view ? view.targetDisplay : 0;
            courseText.text = (info.Course ?? "").ToUpperInvariant();
            flagNumber.text = info.Ordinal.ToString();
            title.text = (info.Name ?? "").ToUpperInvariant();
            noteText.text = ObjectiveOf(info.Blurb);
            note.gameObject.SetActive(!string.IsNullOrEmpty(noteText.text));
            status.text = "Packing the clubs…";
            // the round's holes: those played, the one coming, those still to play
            foreach (Transform t in dots) { t.gameObject.SetActive(false); Destroy(t.gameObject); }
            for (int i = 0; i < info.Count; i++)
            {
                int place = i + 1;
                var dot = Disc(dots, "Dot " + place, place < info.Ordinal ? Lime : place == info.Ordinal ? Ink : new Color(1, 1, 1, .25f));
                dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = new Vector2(0, .5f); dot.rectTransform.pivot = new Vector2(0, .5f);
                dot.rectTransform.sizeDelta = new Vector2(30, 30); dot.rectTransform.anchoredPosition = new Vector2(i * 46 + 8, 0);
                if (place == info.Ordinal)
                {
                    var ring = Disc(dot.transform, "Ring", Lime);
                    ring.rectTransform.offsetMin = Vector2.one * -7; ring.rectTransform.offsetMax = Vector2.one * 7;
                    ring.transform.SetAsFirstSibling();
                }
            }
            progress = target = 0; shown = 0;
            laidOut = false; Layout();
            visible = true; shownAt = Time.unscaledTime; hideAt = -1;
            group.alpha = 0; group.blocksRaycasts = true; group.interactable = true;
            SetFade(0);
        }

        /// What to do on the hole, from its blurb: "A par 4 down a clifftop: carry the inlet…" gives "Carry the inlet…".
        static string ObjectiveOf(string blurb)
        {
            if (string.IsNullOrEmpty(blurb)) return "";
            int colon = blurb.IndexOf(':');
            string s = colon > 0 && colon < blurb.Length - 12 ? blurb.Substring(colon + 1).Trim() : blurb;
            return char.ToUpperInvariant(s[0]) + s.Substring(1);
        }

        /// Where the loading has got to (0 to 1) and what is being done; the ball eases to it.
        public void SetProgress(float to, string what)
        {
            target = Mathf.Max(target, Mathf.Clamp01(to));
            if (!string.IsNullOrEmpty(what) && status.text != what) { status.text = what; if (laidOut) Layout(); }
        }

        /// Fades out over a third of a second (at once with `instant`), and the screen is gone.
        public void Hide(bool instant = false)
        {
            if (!visible) return;
            visible = false; hideAt = Time.unscaledTime;
            if (instant) { group.alpha = 0; gameObject.SetActive(false); }
        }

        void SetFade(float a) { group.alpha = a; group.blocksRaycasts = a > 0.01f; }

        void ApplyProgress(float p)
        {
            progress = p;
            if (map == null) return;
            ball.anchoredPosition = map.PointAt(p);
            trail.Points = map.TrailTo(p);
            trail.SetVerticesDirty();
            ball.localRotation = Quaternion.Euler(0, 0, -p * 720f);
        }

        void Update()
        {
            float now = Time.unscaledTime;
            if (visible || hideAt >= 0)
            {
                float width = view ? OffscreenSize.Width(view) : Screen.width, height = view ? OffscreenSize.Height(view) : Screen.height;
                if (Portrait != portraitBuilt || !Mathf.Approximately(lastWidth, width) || !Mathf.Approximately(lastHeight, height)) Layout();
            }
            if (visible)
            {
                SetFade(Mathf.Clamp01((now - shownAt) / FadeIn));
                float ease = 1 - Mathf.Exp(-5f * Time.unscaledDeltaTime);
                shown = Mathf.Lerp(shown, target, ease);
                ApplyProgress(shown);
                float pulse = Mathf.Abs(Mathf.Sin(now * 3f));
                statusDot.transform.localScale = Vector3.one * (.6f + .4f * pulse);
            }
            else if (hideAt >= 0)
            {
                float u = Mathf.Clamp01((now - hideAt) / FadeOut);
                SetFade(1 - u * u * (3 - 2 * u));
                if (u >= 1) { hideAt = -1; gameObject.SetActive(false); }
            }
        }
    }
}
