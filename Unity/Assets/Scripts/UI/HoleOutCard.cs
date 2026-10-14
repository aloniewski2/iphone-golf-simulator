using System;
using UnityEngine;
using UnityEngine.UI;

namespace GolfArcade.UI
{
    /// The size of the surface a capture is rendering into, while it renders (GameCapture.Save sets it): the
    /// full-screen cards lay out against it instead of against the editor window.
    public static class OffscreenSize
    {
        public static Vector2Int? Override;
        public static int Width(Camera c) => Override?.x ?? c.pixelWidth;
        public static int Height(Camera c) => Override?.y ?? c.pixelHeight;
    }

    /// The ball drops: everything that follows a hole out, in the way the party games do it. The score is stamped across the
    /// screen on a ribbon (BIRDIE!, in gold for an eagle or an ace, sky blue for par, orange for a bogey) with the strokes
    /// on a medal and confetti, and the golfer is left in the clear; along the bottom is Wii Sports' scoreboard with Mario
    /// Kart's bands of colour: HOLE, PAR and YOU in rows, the holes of the round in columns with the one just played lit and a
    /// gold TOTAL, under par ringed in lime, over par in a coral square; above it the emotes and NEXT HOLE with a countdown.
    /// It replaces the shot's stats panel, the landing badge and the scorecard between holes (the full card is for the
    /// round's end). The big screen and the phone share it, laid out for each.
    public sealed class HoleOutCard : MonoBehaviour
    {
        /// One hole of the round on the board.
        public struct Tile
        {
            public int Ordinal, Par;
            public int? Strokes;
            public bool Current;
        }

        public sealed class Data
        {
            /// BIRDIE!, PAR, BOGEY …
            public string Score;
            /// HOLE 2 OF 5  ·  NEEDLE
            public string Eyebrow;
            /// PAR 4  ·  HOLED FROM 14 FT
            public string Detail;
            /// The hole against par (the ribbon's colour), the round against par and the holes played.
            public int Strokes, Par, HoleToPar, ToPar, Thru;
            public Tile[] Tiles = Array.Empty<Tile>();
            /// The button: NEXT HOLE (or SCORECARD on the last), and what comes up: 3 · THE STEPS · PAR 3 · 148 YD.
            public string NextLabel, NextDetail;
            public string[] Emotes;
        }

        static readonly Color Ink = UiKit.Hex("10243D"), Deep = UiKit.Hex("081A2E"), Lime = UiKit.Hex("D7F044"), LimeDeep = UiKit.Hex("9FBF1C"), Cream = UiKit.Hex("FFF9EE"), Coral = UiKit.Hex("FF5B4A");

        Camera view;
        Canvas canvas;
        CanvasScaler scaler;
        RectTransform root, ribbonWrap, wordRect, medalRect, stickerWrap, bottomWrap;
        CanvasGroup stickerFade, emoteFade;
        ConfettiGraphic confetti;
        HoldButton next;
        Image countdownFill;
        Text nextLabelText;
        Data data;
        Action proceed;
        Func<int, bool> emote;
        bool portraitBuilt, confettiThrown;
        float shownAt, builtWidth, builtHeight;

        public bool Showing => gameObject.activeSelf;
        public string ScoreWord => data?.Score;
        public string NextText => nextLabelText ? nextLabelText.text : "";

        public static HoleOutCard Create(Transform parent, Camera camera)
        {
            var go = new GameObject("Hole out card", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var canvas = UiKit.Canvas(go);
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera; canvas.planeDistance = .5f;
            canvas.sortingOrder = 55; canvas.targetDisplay = camera.targetDisplay;
            var c = go.AddComponent<HoleOutCard>();
            c.view = camera; c.canvas = canvas; c.scaler = go.GetComponent<CanvasScaler>();
            go.SetActive(false);
            return c;
        }

        public void Show(Data card, Action onNext, Func<int, bool> onEmote)
        {
            data = card; proceed = onNext; emote = onEmote;
            shownAt = Time.unscaledTime; confettiThrown = false;
            gameObject.SetActive(true);
            Rebuild(Portrait);
            LateUpdate();
        }

        public void Hide() { gameObject.SetActive(false); }

        bool Portrait => view && (OffscreenSize.Override is Vector2Int o ? o.x < o.y * 1.2f : view.aspect < 1.2f);

        /// Lays the card out against the surface it is being drawn to now (for the offscreen captures).
        public void RefreshLayout() { if (gameObject.activeInHierarchy) LateUpdate(); }

        /// The seconds left before the game goes on by itself (of `total`), as the bar under NEXT HOLE.
        public void SetCountdown(float seconds, float total)
        {
            if (!countdownFill) return;
            countdownFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(seconds / Mathf.Max(0.01f, total)), 1);
        }

        // ---- Frame

        void LateUpdate()
        {
            if (!view || data == null) return;
            bool portrait = Portrait;
            float width = OffscreenSize.Width(view), height = OffscreenSize.Height(view);
            // one canvas unit is a thousandth of the phone's width (or of the big screen's height), whatever the
            // display: the sizing the golf HUD uses, so a capture into a render texture lays out like the glass
            float scale = Mathf.Max(.1f, portrait ? width / 1080f : height / 1080f);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = scale; canvas.scaleFactor = scale;
            canvas.targetDisplay = view.targetDisplay;
            if (portrait != portraitBuilt || !Mathf.Approximately(width, builtWidth) || !Mathf.Approximately(height, builtHeight)) Rebuild(portrait);
            Animate(Time.unscaledTime - shownAt);
        }

        static float Back(float x) { x = Mathf.Clamp01(x) - 1; const float c = 1.70158f; return 1 + (c + 1) * x * x * x + c * x * x; }
        static float Out(float x) { x = 1 - Mathf.Clamp01(x); return 1 - x * x * x; }

        /// The reveal, in beats: the ribbon sweeps in and the word is stamped on it, the medal pops, confetti flies, the hole
        /// is named, then the board rises with the way on.
        void Animate(float t)
        {
            if (GolfArcade.Game.PresentationPolicy.Constrained) t = 10;
            float w = portraitBuilt ? 1500 : 2100;
            ribbonWrap.anchoredPosition = new Vector2(Mathf.Lerp(-w, 0, Back((t - 0.0f) / 0.42f)), 0);
            float stamp = Mathf.Clamp01((t - 0.14f) / 0.36f);
            wordRect.localScale = stamp <= 0 ? Vector3.zero : Vector3.one * Mathf.Lerp(2.4f, 1f, Back(stamp));
            medalRect.localScale = Vector3.one * Mathf.Max(0, Back((t - 0.24f) / 0.4f));
            stickerFade.alpha = Mathf.Clamp01((t - 0.42f) / 0.25f);
            stickerWrap.anchoredPosition = new Vector2(0, Mathf.Lerp(40, 0, Out((t - 0.42f) / 0.3f)));
            bottomWrap.anchoredPosition = new Vector2(0, Mathf.Lerp(-640, 0, Out((t - 0.5f) / 0.5f)));
            if (!confettiThrown && t > 0.2f) { confettiThrown = true; confetti.Burst(portraitBuilt ? 90 : 110, (data.Score ?? "").Length * 31 + data.Strokes); }
        }

        // ---- Building (positions are in canvas units: 1080 across on the phone, 1080 high on the big screen)

        static RectTransform Node(Transform parent, string name)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1);
            return rt;
        }

        static RectTransform Stretched(Transform parent, string name)
        {
            var rt = Node(parent, name);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(.5f, .5f); rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        static T Add<T>(Transform parent, string name) where T : Graphic
        {
            var rt = Stretched(parent, name);
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

        static void Put(RectTransform rt, Vector2 anchor, Vector2 pivot, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = pivot; rt.anchoredPosition = new Vector2(x, y); rt.sizeDelta = new Vector2(w, h);
        }

        static Text Label(Transform parent, string name, string text, Font face, int size, Color color, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var t = Club.Words(parent, name, text, face, size, color, anchor);
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        /// Ribbon colours by what the hole came to: gold for an ace or an eagle or better, lime a birdie, sky par, orange a bogey, red worse.
        Color[] RibbonColours()
        {
            if (data.Strokes == 1 || data.HoleToPar <= -2) return new[] { UiKit.Hex("ffc928"), UiKit.Hex("ffe98a"), UiKit.Hex("ffa72a") };
            if (data.HoleToPar < 0) return new[] { UiKit.Hex("bfe83a"), UiKit.Hex("e8f87a"), UiKit.Hex("9ee06b") };
            if (data.HoleToPar == 0) return new[] { UiKit.Hex("53cfe8"), UiKit.Hex("a6ecf6"), UiKit.Hex("4cb8e8") };
            if (data.HoleToPar == 1) return new[] { UiKit.Hex("ff9a5a"), UiKit.Hex("ffc08e"), UiKit.Hex("ff7a4a") };
            return new[] { UiKit.Hex("ff6a5a"), UiKit.Hex("ff9a8e"), UiKit.Hex("e8483a") };
        }

        void Rebuild(bool portrait)
        {
            portraitBuilt = portrait;
            builtWidth = OffscreenSize.Width(view); builtHeight = OffscreenSize.Height(view);
            if (root) { root.gameObject.SetActive(false); Destroy(root.gameObject); }   // (gone from view at once, not at the frame's end)
            float scale = Mathf.Max(.1f, portrait ? builtWidth / 1080f : builtHeight / 1080f);
            float W = builtWidth / scale, H = builtHeight / scale;
            float safeTop = 0, safeBottom = 0;
            if (view.targetDisplay == 0 && !view.targetTexture && OffscreenSize.Override == null)
            { var safe = Screen.safeArea; safeTop = Mathf.Max(0, Screen.height - safe.yMax) / scale; safeBottom = safe.yMin / scale; }
            root = Stretched(transform, "Card");
            var topLeft = new Vector2(0, 1); var bottomMid = new Vector2(.5f, 0); var center = new Vector2(.5f, .5f);

            // The golfer is left in the clear: on the phone between the banner and the way on (CameraRig.FrameHoleOut puts them
            // there), on the big screen in the left half while all of this sits in a column on the right.
            float colX = portrait ? 0 : 1000, colW = portrait ? W : 820, colMid = colX + colW / 2;
            confetti = Add<ConfettiGraphic>(root, "Confetti");

            // ---- the ribbon, the word stamped on it, the medal
            ribbonWrap = Stretched(root, "Ribbon sweep");
            float ribbonH = portrait ? 230 : 200, ribbonTop = portrait ? 240 + safeTop : 60;
            var ribbon = Node(ribbonWrap, "Ribbon");
            float ribbonLeft = portrait ? -80 : colX - 40, ribbonRight = W + 80;
            Put(ribbon, topLeft, center, (ribbonLeft + ribbonRight) / 2, -(ribbonTop + ribbonH / 2), ribbonRight - ribbonLeft, ribbonH);
            ribbon.localEulerAngles = new Vector3(0, 0, portrait ? -4f : -3f);
            var shade = Add<Image>(ribbon, "Shadow"); shade.color = new Color(.03f, .1f, .18f, .55f); shade.rectTransform.offsetMin = new Vector2(0, -22); shade.rectTransform.offsetMax = new Vector2(0, -22);
            var fill = Add<GradientGraphic>(ribbon, "Fill"); fill.Stops = RibbonColours();
            foreach (var side in new[] { 1f, 0f })
            {
                var edge = Add<Image>(ribbon, side > 0 ? "Top edge" : "Bottom edge"); edge.color = Color.white;
                edge.rectTransform.anchorMin = new Vector2(0, side); edge.rectTransform.anchorMax = new Vector2(1, side); edge.rectTransform.pivot = new Vector2(.5f, side);
                edge.rectTransform.sizeDelta = new Vector2(0, 10); edge.rectTransform.anchoredPosition = Vector2.zero;
            }
            int wordMax = portrait ? 200 : 150;
            var word = UiKit.Chunky(ribbon, "Score", wordMax, Color.white, Ink, portrait ? 8 : 7);
            word.text = data.Score; word.horizontalOverflow = HorizontalWrapMode.Overflow;
            wordRect = word.rectTransform;
            wordRect.anchorMin = wordRect.anchorMax = center; wordRect.pivot = center;
            wordRect.anchoredPosition = new Vector2(colMid - (ribbonLeft + ribbonRight) / 2 + (portrait ? 70 : 20), 0);
            wordRect.sizeDelta = new Vector2(portrait ? 760 : 640, ribbonH - 30);
            Icons.Fit(word, 50, wordMax);
            // the medal: strokes on ink, a lime ring and a white one
            float medal = portrait ? 210 : 190;
            medalRect = Node(root, "Medal");
            Put(medalRect, topLeft, center, (portrait ? 70 : 900) + medal / 2, -((portrait ? 80 + safeTop : 26) + medal / 2), medal, medal);
            medalRect.localEulerAngles = new Vector3(0, 0, -6);
            var medalShade = Disc(medalRect, "Shadow", new Color(.03f, .1f, .18f, .5f)); Grow(medalShade.rectTransform, 24, -24);
            Grow(Disc(medalRect, "White ring", Color.white).rectTransform, 24, 0);
            Grow(Disc(medalRect, "Lime ring", Lime).rectTransform, 13, 0);
            Disc(medalRect, "Face", Ink);
            var count = Label(medalRect, "Strokes", data.Strokes.ToString(), UiKit.Display, (int)(medal * .6f), Lime);
            count.rectTransform.offsetMin = new Vector2(0, medal * .1f); count.rectTransform.offsetMax = Vector2.zero;
            var caption = Label(medalRect, "Caption", data.Strokes == 1 ? "STROKE" : "STROKES", UiKit.Strong, (int)(medal * .125f), new Color(1, 1, 1, .8f));
            caption.rectTransform.offsetMin = new Vector2(0, -medal * .34f); caption.rectTransform.offsetMax = Vector2.zero;

            // ---- the hole, on a cream sticker
            stickerWrap = Stretched(root, "Sticker"); stickerFade = stickerWrap.gameObject.AddComponent<CanvasGroup>();
            var sticker = Node(stickerWrap, "Pill");
            float stickerH = portrait ? 84 : 70;
            var stickerText = Label(sticker, "Hole", data.Eyebrow, UiKit.Display, portrait ? 38 : 32, Ink);
            float stickerW = Mathf.Min(colW - 60, stickerText.preferredWidth + (portrait ? 96 : 84));
            Put(sticker, topLeft, center, colMid, -((portrait ? 500 + safeTop : 290) + stickerH / 2), stickerW, stickerH);
            sticker.localEulerAngles = new Vector3(0, 0, -2);
            var stickerBack = Rounded(sticker, "Back", Cream, 80); stickerBack.transform.SetAsFirstSibling();
            stickerText.rectTransform.anchorMin = Vector2.zero; stickerText.rectTransform.anchorMax = Vector2.one; stickerText.rectTransform.offsetMin = stickerText.rectTransform.offsetMax = Vector2.zero;

            // ---- the board, the emotes and the way on, rising together
            bottomWrap = Stretched(root, "Bottom");
            float rowA = portrait ? 84 : 84, rowB = portrait ? 100 : 100;
            float boardH = rowA * 2 + rowB;
            int n = data.Emotes?.Length ?? 0;
            float eh = portrait ? 76 : 64, gap = portrait ? 16 : 12, ctaH = portrait ? 156 : 136, space = portrait ? 36 : 40;
            float boardW = portrait ? W - 88 : colW;
            RectTransform boardAt, emotesAt, ctaAt;
            if (portrait)
            {
                float boardBottom = 80 + safeBottom;
                boardAt = Node(bottomWrap, "Board place"); Put(boardAt, bottomMid, new Vector2(.5f, 0), 0, boardBottom, boardW, boardH);
                ctaAt = Node(bottomWrap, "Next place"); Put(ctaAt, bottomMid, new Vector2(.5f, 0), 0, boardBottom + boardH + space + 8, boardW, ctaH);
                emotesAt = Node(bottomWrap, "Emotes place"); Put(emotesAt, bottomMid, new Vector2(.5f, 0), 0, boardBottom + boardH + space + 8 + ctaH + space, boardW, eh);
            }
            else
            {
                float top = 400;
                boardAt = Node(bottomWrap, "Board place"); Put(boardAt, topLeft, topLeft, colX, -top, boardW, boardH);
                emotesAt = Node(bottomWrap, "Emotes place"); Put(emotesAt, topLeft, topLeft, colX, -(top + boardH + space + 8), colW, eh);
                ctaAt = Node(bottomWrap, "Next place"); Put(ctaAt, topLeft, topLeft, colX, -(top + boardH + space + 8 + eh + space), colW, ctaH);
            }
            Board(boardAt, boardW, rowA, rowB, portrait);
            // NEXT HOLE: a lime slab with the hole coming up and a countdown
            next = Club.Slab(ctaAt, "Next hole", Lime, LimeDeep, ctaH / 2, out var face, portrait ? 14 : 12);
            var nextRt = (RectTransform)next.transform; nextRt.anchorMin = Vector2.zero; nextRt.anchorMax = Vector2.one; nextRt.offsetMin = nextRt.offsetMax = Vector2.zero;
            // the label over the hole that is coming up over the countdown, each in its own band of the slab
            bool detail = !string.IsNullOrEmpty(data.NextDetail);
            float subBand = detail ? (portrait ? 46 : 38) : 0, barBand = portrait ? 34 : 28;
            nextLabelText = Label(face, "Label", data.NextLabel + "  ▸", UiKit.Display, portrait ? 62 : 50, Ink);
            nextLabelText.rectTransform.offsetMin = new Vector2(0, subBand + barBand); nextLabelText.rectTransform.offsetMax = new Vector2(0, -4);
            if (detail)
            {
                var sub = Label(face, "Next hole", data.NextDetail, UiKit.Strong, portrait ? 32 : 26, new Color(Ink.r, Ink.g, Ink.b, .78f));
                sub.rectTransform.offsetMin = new Vector2(0, barBand); sub.rectTransform.offsetMax = new Vector2(0, -(ctaH - barBand - subBand));
                Icons.Fit(sub, 14, portrait ? 32 : 26);
            }
            var track = Rounded(face, "Countdown track", new Color(0, 0, 0, .16f), 6);
            track.rectTransform.anchorMin = new Vector2(.08f, 0); track.rectTransform.anchorMax = new Vector2(.92f, 0); track.rectTransform.pivot = new Vector2(.5f, 0);
            track.rectTransform.sizeDelta = new Vector2(0, 9); track.rectTransform.anchoredPosition = new Vector2(0, portrait ? 16 : 13);
            countdownFill = Rounded(track.transform, "Time left", Ink, 6);
            countdownFill.rectTransform.anchorMin = Vector2.zero; countdownFill.rectTransform.anchorMax = Vector2.one; countdownFill.rectTransform.offsetMin = countdownFill.rectTransform.offsetMax = Vector2.zero;
            next.Pressed = () => proceed?.Invoke();
            // the emotes: three outlined pills the golfer performs on the spot
            emoteFade = emotesAt.gameObject.AddComponent<CanvasGroup>();
            float ew = (emotesAt.rect.width - gap * (n - 1)) / Mathf.Max(1, n);
            for (int i = 0; i < n; i++)
            {
                int choice = i;
                var b = Club.Slab(emotesAt, "Emote " + (i + 1), new Color(.03f, .1f, .18f, .62f), new Color(0, 0, 0, .3f), eh / 2, out var ef, 8);
                var brt = (RectTransform)b.transform; Put(brt, topLeft, topLeft, i * (ew + gap), 0, ew, eh);
                Rounded(ef, "Ring", new Color(1, 1, 1, .6f), eh / 2, true);
                Label(ef, "Name", data.Emotes[i].ToUpperInvariant(), UiKit.Display, portrait ? 32 : 26, Color.white);
                b.Pressed = () => emote?.Invoke(choice);
            }
        }

        static void Grow(RectTransform rt, float by, float down)
        {
            rt.offsetMin = new Vector2(-by, -by + down); rt.offsetMax = new Vector2(by, by + down);
        }

        // ---- The board

        static Color[] Rows(int row, bool current, bool total)
        {
            if (total) return row == 0 ? new[] { UiKit.Hex("ffd21f"), UiKit.Hex("f0a800") } : row == 1 ? new[] { UiKit.Hex("ffe27a"), UiKit.Hex("ffc83a") } : new[] { UiKit.Hex("ffeaa0"), UiKit.Hex("ffd24a") };
            if (current) return row == 0 ? new[] { UiKit.Hex("e9fb7a"), UiKit.Hex("c8ec2e") } : row == 1 ? new[] { UiKit.Hex("f3fda0"), UiKit.Hex("dcf55e") } : new[] { Color.white, UiKit.Hex("eaf3ff") };
            return row == 0 ? new[] { UiKit.Hex("10294a"), UiKit.Hex("0a1f3b") } : row == 1 ? new[] { UiKit.Hex("1e456f"), UiKit.Hex("163a5f") } : new[] { UiKit.Hex("41a0ff"), UiKit.Hex("1f6be0"), UiKit.Hex("1a5cc8") };
        }

        /// HOLE, PAR and YOU in rows; the holes of the round in columns and a TOTAL; the hole just played lit.
        void Board(RectTransform frame, float width, float rowA, float rowB, bool portrait)
        {
            int cols = Mathf.Max(1, data.Tiles.Length);
            float labelW = portrait ? 190 : 150, totalW = portrait ? 200 : 170, radius = portrait ? 44 : 36, rim = 6;
            float boardH = rowA * 2 + rowB;
            var shade = Rounded(frame, "Shadow", new Color(0, 0, 0, .45f), radius + rim); shade.rectTransform.offsetMin = new Vector2(-6, -34); shade.rectTransform.offsetMax = new Vector2(6, -22);
            Rounded(frame, "Frame", Color.white, radius + rim);
            var cells = Club.Fill(frame, "Cells", rim);
            var cellsBack = cells.gameObject.AddComponent<Image>(); cellsBack.sprite = Club.Rounded; cellsBack.type = Image.Type.Sliced; cellsBack.pixelsPerUnitMultiplier = 80f / radius; cellsBack.color = Deep; cellsBack.raycastTarget = false;
            cells.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            float inner = width - 2 * rim, colW = (inner - labelW - totalW) / cols;
            float[] heights = { rowA, rowA, rowB };
            int fl = portrait ? 34 : 28, fv = portrait ? 52 : 44, fy = portrait ? 62 : 52;
            float mark = portrait ? 80 : 70;
            int played = 0, parTotal = 0, strokeTotal = 0;
            foreach (var t in data.Tiles) { parTotal += t.Par; if (t.Strokes is int s) { played++; strokeTotal += s; } }
            string[] labels = { "HOLE", "PAR", "YOU" };
            float y = 0;
            for (int row = 0; row < 3; row++)
            {
                float cx = 0;
                for (int col = -1; col <= cols; col++)
                {
                    bool label = col < 0, total = col >= cols;
                    var tile = !label && !total ? data.Tiles[col] : default;
                    bool current = !label && !total && tile.Current;
                    float w = label ? labelW : total ? totalW : colW;
                    var cell = Node(cells, "Cell"); Put(cell, topLeft(), topLeft(), cx, -y, w, heights[row]);
                    var bg = Add<GradientGraphic>(cell, "Back"); bg.Vertical = true; bg.Stops = Rows(row, current, total);
                    if (row == 2 && !current && !total && !label)
                    {
                        var sheen = Add<Image>(cell, "Sheen"); sheen.color = new Color(1, 1, 1, .2f);
                        sheen.rectTransform.anchorMin = new Vector2(0, .54f); sheen.rectTransform.anchorMax = Vector2.one; sheen.rectTransform.offsetMin = sheen.rectTransform.offsetMax = Vector2.zero;
                    }
                    if (label && row == 2) { var sheen = Add<Image>(cell, "Sheen"); sheen.color = new Color(1, 1, 1, .2f); sheen.rectTransform.anchorMin = new Vector2(0, .54f); sheen.rectTransform.anchorMax = Vector2.one; sheen.rectTransform.offsetMin = sheen.rectTransform.offsetMax = Vector2.zero; }
                    // thin rules between the cells
                    var right = Add<Image>(cell, "Rule"); right.color = new Color(1, 1, 1, .14f); right.rectTransform.anchorMin = new Vector2(1, 0); right.rectTransform.anchorMax = Vector2.one; right.rectTransform.sizeDelta = new Vector2(3, 0); right.rectTransform.pivot = new Vector2(1, .5f); right.rectTransform.offsetMin = right.rectTransform.offsetMax = Vector2.zero; right.rectTransform.sizeDelta = new Vector2(3, 0);
                    var under = Add<Image>(cell, "Rule"); under.color = new Color(1, 1, 1, .14f); under.rectTransform.anchorMin = Vector2.zero; under.rectTransform.anchorMax = new Vector2(1, 0); under.rectTransform.pivot = new Vector2(.5f, 0); under.rectTransform.offsetMin = under.rectTransform.offsetMax = Vector2.zero; under.rectTransform.sizeDelta = new Vector2(0, 3);
                    Color ink = current || total ? Ink : Color.white;
                    if (label)
                    {
                        var l = Label(cell, "Label", labels[row], UiKit.Display, fl, row == 0 ? Lime : Color.white, TextAnchor.MiddleLeft);
                        l.rectTransform.offsetMin = new Vector2(portrait ? 30 : 26, 0);
                    }
                    else if (row == 0) Label(cell, "Text", total ? "TOTAL" : tile.Ordinal.ToString(), UiKit.Display, total ? (int)(fl * .9f) : fv, ink);
                    else if (row == 1) Label(cell, "Text", total ? parTotal.ToString() : tile.Par.ToString(), UiKit.Display, fv, current || total ? Ink : UiKit.Hex("cfe6ff"));
                    else if (total)
                    {
                        var t = Label(cell, "Total", $"{strokeTotal}<size={(int)(fv * .6f)}>  {GolfArcade.Course.Scorecard.FormatToPar(data.ToPar)}</size>", UiKit.Display, fy, Ink);
                        t.supportRichText = true;
                    }
                    else if (tile.Strokes is int strokes)
                    {
                        int d = strokes - tile.Par;
                        if (d != 0)
                        {
                            var ring = d < 0 ? Disc(cell, "Ring", Ink) : Rounded(cell, "Ring", Ink, 18);
                            Put(ring.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), 0, 0, mark + 10, mark + 10);
                            var m = d < 0 ? Disc(cell, "Mark", Lime) : Rounded(cell, "Mark", Coral, 14);
                            Put(m.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), 0, 0, mark, mark);
                        }
                        Label(cell, "Strokes", strokes.ToString(), UiKit.Display, fy, d < 0 ? Ink : d > 0 ? Color.white : current ? Ink : Color.white);
                    }
                    else Label(cell, "None", "–", UiKit.Display, fy, new Color(ink.r, ink.g, ink.b, .45f));
                    if (current && row == 2)
                    {
                        // the lit hole's cell is ringed in lime
                        foreach (var side in new[] { 0, 1, 2, 3 })
                        {
                            var edge = Add<Image>(cell, "Lit"); edge.color = Lime;
                            var r = edge.rectTransform;
                            switch (side)
                            {
                                case 0: r.anchorMin = new Vector2(0, 1); r.anchorMax = Vector2.one; r.pivot = new Vector2(.5f, 1); r.sizeDelta = new Vector2(0, 7); break;
                                case 1: r.anchorMin = Vector2.zero; r.anchorMax = new Vector2(1, 0); r.pivot = new Vector2(.5f, 0); r.sizeDelta = new Vector2(0, 7); break;
                                case 2: r.anchorMin = Vector2.zero; r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0, .5f); r.sizeDelta = new Vector2(7, 0); break;
                                default: r.anchorMin = new Vector2(1, 0); r.anchorMax = Vector2.one; r.pivot = new Vector2(1, .5f); r.sizeDelta = new Vector2(7, 0); break;
                            }
                            r.offsetMin = r.offsetMax = Vector2.zero; r.anchoredPosition = Vector2.zero;
                        }
                    }
                    cx += w;
                }
                y += heights[row];
            }
        }

        static Vector2 topLeft() => new Vector2(0, 1);
    }
}
