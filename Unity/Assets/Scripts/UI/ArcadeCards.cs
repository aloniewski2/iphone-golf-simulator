using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GolfArcade.UI
{
    /// The HUD's icons: white silhouettes (Resources/UI/Icons, cut from one Higgsfield sheet —
    /// blender/previews/ui_options/icons_sheet.png) tinted where they're used.
    public static class Icons
    {
        /// Shrinks `text` to fit its box, from `max` down to `min` points (best fit only works
        /// on wrapped, truncated text).
        public static void Fit(Text text, int min, int max)
        {
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true; text.resizeTextMinSize = min; text.resizeTextMaxSize = max;
        }

        static readonly Dictionary<string, Sprite> cache = new();

        public static Sprite Get(string name)
        {
            if (cache.TryGetValue(name, out var s)) return s;
            var tex = Resources.Load<Texture2D>($"UI/Icons/{name}");
            s = tex ? Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f)) : null;
            cache[name] = s;
            return s;
        }

        public static Image Place(Transform parent, string name, Color color, Vector2 anchor, Vector2 pos, float size)
        {
            var img = new GameObject($"Icon {name}").AddComponent<Image>();
            img.transform.SetParent(parent, false);
            img.sprite = Get(name); img.color = color; img.raycastTarget = false; img.preserveAspect = true;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(size, size);
            return img;
        }
    }

    /// The yardage card, top left under the scoreboard (the user's pick, option A "Arcade"):
    /// the distance huge with a red flag, the club in hand on a yellow pill, and three rows — how
    /// far it plays (uphill adds), the wind with its arrow, and the lie. On the green the rows
    /// read the putt instead: slope, break, lie.
    public sealed class YardageCard
    {
        public const float Width = 460, Height = 430;
        public readonly RectTransform Root;
        readonly Text number, unit, caption, club;
        readonly Image clubIcon;
        readonly Row[] rows = new Row[3];

        sealed class Row
        {
            public Image Icon, Arrow;
            public Text Label, Value, Note;
        }

        static readonly Color RowFill = new(0.10f, 0.24f, 0.66f, 1f);
        static readonly Color NoteGreen = UiKit.Hex("7CF07A");

        public YardageCard(Transform parent, Vector2 topLeft, Sprite arrow)
        {
            Root = UiKit.Pill(parent, "Shot card", UiKit.ArcadeBlue, new Vector2(0, 1), Vector2.zero, new Vector2(Width, Height), out var fill, 5f, false);
            Root.pivot = new Vector2(0, 1); Root.anchoredPosition = topLeft;
            foreach (var img in Root.GetComponentsInChildren<Image>()) img.sprite = UiKit.RoundedLarge;
            var f = fill.rectTransform;

            number = UiKit.Label(f, "Distance", 124, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, 0), new Vector2(300, 140), UiKit.Display);
            number.rectTransform.pivot = new Vector2(0, 1); number.color = Color.white;
            unit = UiKit.Label(f, "Unit", 40, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(140, 46), UiKit.Display);
            unit.rectTransform.pivot = new Vector2(0, 1); unit.color = Color.white;
            caption = UiKit.Label(f, "Caption", 30, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(170, 40), UiKit.Display);
            caption.rectTransform.pivot = new Vector2(0, 1); caption.color = Color.white;
            Icons.Place(f, "flag", UiKit.Hex("E8352F"), new Vector2(1, 1), new Vector2(-52, -58), 78);

            var pill = UiKit.Pill(f, "Club pill", UiKit.ArcadeYellow, new Vector2(0.5f, 1), new Vector2(0, -176), new Vector2(Width - 44, 62), out var pillFill, 3f);
            clubIcon = Icons.Place(pillFill.transform, "iron", UiKit.ArcadeInk, new Vector2(0, 0.5f), new Vector2(38, 0), 44);
            club = UiKit.Label(pillFill.transform, "Club", 34, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, new Vector2(72, 0), Vector2.zero, UiKit.Display, false);
            club.rectTransform.offsetMax = new Vector2(-12, 0); club.color = UiKit.ArcadeInk;
            Icons.Fit(club, 20, 34);

            for (int i = 0; i < rows.Length; i++)
            {
                var r = rows[i] = new Row();
                var bar = UiKit.Pill(f, $"Row {i}", RowFill, new Vector2(0.5f, 1), new Vector2(0, -250 - i * 64), new Vector2(Width - 44, 56), out var barFill, 2.5f);
                var bt = barFill.transform;
                r.Icon = Icons.Place(bt, "wind", Color.white, new Vector2(0, 0.5f), new Vector2(34, 0), 36);
                r.Label = UiKit.Label(bt, "Label", 27, TextAnchor.MiddleLeft, new Vector2(0, 0), new Vector2(0, 1), new Vector2(66, 0), new Vector2(200, 0), UiKit.Display, false);
                r.Label.rectTransform.pivot = new Vector2(0, 0.5f); r.Label.color = Color.white;
                r.Value = UiKit.Label(bt, "Value", 30, TextAnchor.MiddleRight, new Vector2(1, 0), new Vector2(1, 1), new Vector2(-96, 0), new Vector2(240, 0), UiKit.Display, false);
                r.Value.rectTransform.pivot = new Vector2(1, 0.5f); r.Value.color = Color.white;
                r.Note = UiKit.Label(bt, "Note", 26, TextAnchor.MiddleRight, new Vector2(1, 0), new Vector2(1, 1), new Vector2(-18, 0), new Vector2(76, 0), UiKit.Display, false);
                r.Note.rectTransform.pivot = new Vector2(1, 0.5f); r.Note.color = NoteGreen;
                r.Arrow = new GameObject("Arrow").AddComponent<Image>();
                r.Arrow.transform.SetParent(bt, false);
                r.Arrow.sprite = arrow; r.Arrow.color = Color.white; r.Arrow.raycastTarget = false;
                var at = r.Arrow.rectTransform; at.anchorMin = at.anchorMax = new Vector2(1, 0.5f); at.anchoredPosition = new Vector2(-50, 0); at.sizeDelta = new Vector2(34, 34);
                r.Arrow.enabled = false;
            }
        }

        public void SetDistance(double amount, string unitText, string captionText)
        {
            number.text = $"{amount:F0}";
            unit.text = unitText.ToUpperInvariant();
            caption.text = captionText.ToUpperInvariant();
            float x = 20 + Mathf.Min(300, number.preferredWidth) + 12;
            unit.rectTransform.anchoredPosition = new Vector2(x, -54);
            caption.rectTransform.anchoredPosition = new Vector2(x, -96);
        }

        public void SetClub(string text, bool putter)
        {
            club.text = text.ToUpperInvariant();
            clubIcon.sprite = Icons.Get(putter ? "putter" : "iron");
        }

        /// A row: its icon, label and value; `note` in green at the end (the uphill yards), or
        /// an arrow turned `arrowDegrees` (the wind) — one or the other.
        public void SetRow(int i, string icon, string label, string value, string note = null, float? arrowDegrees = null)
        {
            var r = rows[i];
            r.Icon.sprite = Icons.Get(icon);
            r.Label.text = label.ToUpperInvariant();
            r.Value.text = value.ToUpperInvariant();
            r.Note.text = note ?? "";
            r.Arrow.enabled = arrowDegrees.HasValue && note == null;
            if (arrowDegrees.HasValue) r.Arrow.rectTransform.localRotation = Quaternion.Euler(0, 0, -arrowDegrees.Value);
            // with nothing at the end the value takes the room
            r.Value.rectTransform.anchoredPosition = new Vector2(note == null && !arrowDegrees.HasValue ? -22 : -96, 0);
        }
    }

    /// After the strike (option A): the grade on a badge, the shape on a pill, six tiles —
    /// swing speed, backswing, tempo, face, carry, total — and a little top-down picture of the
    /// ball's line bending away from the straight one to the flag. Carry and total fill in as
    /// the ball comes down and runs out; the line draws as it flies.
    public sealed class SwingCard : MonoBehaviour
    {
        public const float Width = 440, Height = 700;
        RectTransform root, curvePanel;
        Image badge;
        Text grade, shape;
        readonly List<(Image icon, Text title, Text value)> tiles = new();
        readonly List<Image> line = new(), edge = new(), aim = new();
        RectTransform flag, ball;
        static readonly Color Fairway = UiKit.Hex("5DBB4A"), FairwayLight = UiKit.Hex("78CF5C");

        public static SwingCard Create(Transform parent, float margin)
        {
            var root = UiKit.Pill(parent, "Swing card", UiKit.ArcadeBlue, new Vector2(1, 1), Vector2.zero, new Vector2(Width, Height), out var fill, 5f, false);
            root.pivot = new Vector2(1, 1); root.anchoredPosition = new Vector2(-margin, -margin);
            foreach (var img in root.GetComponentsInChildren<Image>()) img.sprite = UiKit.RoundedLarge;
            var c = root.gameObject.AddComponent<SwingCard>();
            c.root = root;
            var f = fill.rectTransform;
            var badgeRoot = UiKit.Pill(f, "Grade", UiKit.ArcadeYellow, new Vector2(0.5f, 1), new Vector2(0, -58), new Vector2(Width - 44, 82), out c.badge, 4f);
            Icons.Place(c.badge.transform, "star", UiKit.ArcadeInk, new Vector2(0, 0.5f), new Vector2(46, 0), 46);
            c.grade = UiKit.Chunky(c.badge.transform, "Word", 46, UiKit.ArcadeInk, new Color(1, 1, 1, 0.0f), 0f);
            c.grade.rectTransform.offsetMin = new Vector2(40, 0);
            var shapeRoot = UiKit.Pill(f, "Shape", UiKit.Hex("2A84E6"), new Vector2(0.5f, 1), new Vector2(0, -140), new Vector2(230, 56), out var shapeFill, 3f);
            c.shape = UiKit.Label(shapeFill.transform, "Shape", 30, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, UiKit.Display, false);
            c.shape.color = Color.white;
            const float tw = (Width - 44 - 12) / 2f, th = 98;
            for (int i = 0; i < 6; i++)
            {
                float x = (i % 2 == 0 ? -1 : 1) * (tw / 2 + 6), y = -186 - (i / 2) * (th + 12) - th / 2;
                var tile = UiKit.Panel(f, $"Tile {i}", Color.white, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(x, y), new Vector2(tw, th));
                tile.sprite = UiKit.RoundedLarge; tile.rectTransform.pivot = new Vector2(0.5f, 0.5f); tile.raycastTarget = false;
                var disc = UiKit.Panel(tile.transform, "Disc", UiKit.ArcadeBlue, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(38, 0), new Vector2(54, 54));
                disc.sprite = UiKit.Circle; disc.type = Image.Type.Simple; disc.raycastTarget = false; disc.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                var icon = Icons.Place(disc.transform, "speed", Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, 36);
                var title = UiKit.Label(tile.transform, "Title", 19, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(72, -14), new Vector2(-80, 26), UiKit.Display, false);
                title.rectTransform.pivot = new Vector2(0, 1); title.rectTransform.sizeDelta = new Vector2(-80, 26); title.color = UiKit.ArcadeBlue;
                Icons.Fit(title, 12, 19);
                var value = UiKit.Label(tile.transform, "Value", 31, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(72, -42), new Vector2(-80, 44), UiKit.Display, false);
                value.rectTransform.pivot = new Vector2(0, 1); value.rectTransform.sizeDelta = new Vector2(-80, 44); value.color = UiKit.ArcadeInk;
                Icons.Fit(value, 18, 31);
                c.tiles.Add((icon, title, value));
            }
            // the ball's line from above
            var panel = UiKit.Panel(f, "Curve", Fairway, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 22 + 80), new Vector2(Width - 44, 160));
            panel.sprite = UiKit.RoundedLarge; panel.rectTransform.pivot = new Vector2(0.5f, 0.5f); panel.raycastTarget = false;
            var stripe = UiKit.Panel(panel.transform, "Fairway", FairwayLight, new Vector2(0, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(-40, 70));
            stripe.sprite = UiKit.RoundedLarge; stripe.raycastTarget = false;
            c.curvePanel = panel.rectTransform;
            c.flag = Icons.Place(panel.transform, "flag", UiKit.Hex("E8352F"), new Vector2(0.5f, 0.5f), Vector2.zero, 40).rectTransform;
            var b = UiKit.Panel(panel.transform, "Ball", Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18, 18));
            b.sprite = UiKit.Circle; b.type = Image.Type.Simple; b.raycastTarget = false;
            c.ball = b.rectTransform;
            root.gameObject.SetActive(false);
            return c;
        }

        public void Show(string gradeText, Color gradeColor, string shapeText, (string icon, string title, string value)[] items)
        {
            gameObject.SetActive(true);
            grade.text = gradeText; badge.color = gradeColor;
            shape.text = shapeText;
            for (int i = 0; i < tiles.Count; i++)
            {
                bool on = i < items.Length;
                tiles[i].title.transform.parent.gameObject.SetActive(on);
                if (!on) continue;
                tiles[i].icon.sprite = Icons.Get(items[i].icon);
                tiles[i].title.text = items[i].title.ToUpperInvariant();
                tiles[i].value.text = items[i].value.ToUpperInvariant();
            }
            SetCurve(null, 0, 0, 0);
        }

        public void SetTile(int i, string value) { if (i < tiles.Count) tiles[i].value.text = value.ToUpperInvariant(); }

        public void Hide() => gameObject.SetActive(false);

        /// The line so far: `points` are (along the aim, right of it) in yards from the ball; the
        /// first `count` have been flown. `reach` is how far down the aim the picture spans and
        /// `target` how far down it the flag stands (where a straight one would have come down).
        public void SetCurve(IList<Vector2> points, int count, float reach, float target)
        {
            var size = curvePanel.rect.size;
            float halfW = size.x / 2 - 34, halfH = size.y / 2 - 24;
            reach = Mathf.Max(reach, 1f);
            float widest = 6f;
            if (points != null) foreach (var p in points) widest = Mathf.Max(widest, Mathf.Abs(p.y));
            // across is stretched to show the bend (a 10-yard fade has to read), within the panel
            float across = Mathf.Min(halfH / widest, 2.2f * (2 * halfW) / reach);
            Vector2 At(Vector2 p) => new(-halfW + 2 * halfW * Mathf.Clamp01(p.x / reach), -Mathf.Clamp(p.y * across, -halfH, halfH));
            // the straight line to the flag, dashed
            int dashes = 14;
            for (int i = 0; i < dashes; i++)
            {
                var a = new Vector2(-halfW + 2 * halfW * (target / reach) * i / dashes, 0);
                var b = new Vector2(-halfW + 2 * halfW * (target / reach) * (i + 0.55f) / dashes, 0);
                Stretch(Bar(aim, i, new Color(1, 1, 1, 0.55f)), a, b, 4f);
            }
            for (int i = dashes; i < aim.Count; i++) aim[i].gameObject.SetActive(false);
            flag.anchoredPosition = new Vector2(-halfW + 2 * halfW * Mathf.Clamp01(target / reach) + 12, 18);
            int n = 0;
            if (points != null)
                for (int i = 1; i < Mathf.Min(count, points.Count); i++, n++)
                {
                    var a = At(points[i - 1]); var b = At(points[i]);
                    Stretch(Bar(edge, n, new Color(UiKit.ArcadeInk.r, UiKit.ArcadeInk.g, UiKit.ArcadeInk.b, 0.6f)), a, b, 10f);
                    Stretch(Bar(line, n, Color.white), a, b, 6f);
                }
            for (int i = n; i < line.Count; i++) { line[i].gameObject.SetActive(false); edge[i].gameObject.SetActive(false); }
            foreach (var l in line) l.transform.SetAsLastSibling();
            ball.SetAsLastSibling(); flag.SetAsLastSibling();
            ball.anchoredPosition = points != null && count > 0 ? At(points[Mathf.Min(count, points.Count) - 1]) : new Vector2(-halfW, 0);
        }

        Image Bar(List<Image> pool, int i, Color c)
        {
            while (pool.Count <= i)
            {
                var img = UiKit.Panel(curvePanel, "Line", Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one, false);
                img.raycastTarget = false; pool.Add(img);
            }
            pool[i].color = c; pool[i].gameObject.SetActive(true);
            return pool[i];
        }

        static void Stretch(Image bar, Vector2 a, Vector2 b, float thickness)
        {
            var rt = bar.rectTransform;
            var d = b - a;
            rt.anchoredPosition = (a + b) / 2; rt.sizeDelta = new Vector2(d.magnitude + thickness * 0.6f, thickness);
            rt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        }
    }

    /// A hole or the round finished: Adnan's results screen (his ClubResultsScreen) on the Island Sports Club
    /// system. The course dimmed under the lagoon, what finished in caps, the headline huge in sun yellow
    /// ("Birdie!", "3 under par"), the card on a cream slab — hole, par, you (a sun disc round a birdie, a
    /// coral square round a bogey) — the total against par, the round's stats on small cream cards, and the
    /// way on: NEXT HOLE (the sun slab) while there is one, PLAY AGAIN (cream), MAIN MENU (quiet). It rises in
    /// over the course, shrunk to fit a short screen.
    public sealed class RoundCard
    {
        public readonly RectTransform Root;
        public HoldButton PlayAgain { get; private set; }
        public HoldButton Menu { get; private set; }
        /// Null on the round's last hole.
        public HoldButton NextHole { get; private set; }

        public struct Highlight { public string Icon, Title, Value; }

        /// A row on the card for each golfer in a round with others: their name in their colour
        /// and their strokes per hole. Without them the card has the one YOU row and the total.
        public struct Row
        {
            public string Name; public Color Color; public int?[] Strokes;
            /// Text for each hole instead of strokes (a contest's "12 ft", "285 yd"), or null.
            public string[] Cells;
        }

        /// What PLAY AGAIN says (NEXT ROUND in the Open).
        readonly string againLabel;

        public RoundCard(Transform parent, string title, string headline, int[] holes, int[] pars, int?[] strokes, int total, int toPar, Highlight[] highlights, bool nextHole, Row[] players = null, string againLabel = null)
        {
            this.againLabel = againLabel ?? "Play again";
            players ??= new[] { new Row { Name = "You", Color = Club.Sun, Strokes = strokes } };
            bool solo = players.Length == 1;
            int tableRows = 2 + players.Length;
            const float W = 980;
            float y = 0;
            // the course under the lagoon; touches stop here
            var sheet = UiKit.Panel(parent, "Scorecard", Club.Deep(0.72f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false);
            Root = sheet.rectTransform;
            var card = Club.Box(Root, "Round card", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(W, 1200));
            var f = card;
            float inner = W;

            // what finished, and the headline
            var what = Club.Words(f, "Title", title.ToUpperInvariant(), Club.Caps, 40, Club.White(0.8f), TextAnchor.UpperLeft);
            Top(what.rectTransform, y, 50); y -= 58;
            var head = Club.Words(f, "Headline", headline ?? ToParWords(toPar), Club.Display, 150, Club.Sun, TextAnchor.UpperLeft, 18);
            Icons.Fit(head, 80, 150);
            Top(head.rectTransform, y, 170); y -= 170 + 26;

            // the card: a cream slab
            int n = holes.Length;
            const float rowH = 92, labelW = 170;
            float colW = Mathf.Min(140, (inner - 48 - labelW) / Mathf.Max(1, n));
            float tableH = tableRows * rowH + 36;
            var slab = Club.Box(f, "Card", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(0, tableH));
            var bas = Club.Paint(slab, "Base", Club.CreamDeep, 60); bas.rectTransform.offsetMin = new Vector2(0, -18); bas.rectTransform.offsetMax = new Vector2(0, -18);
            var face = Club.Paint(slab, "Face", Club.Cream, 60);
            var strip = Club.Paint(face.transform, "Strip", Club.Green);
            face.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            strip.rectTransform.anchorMin = new Vector2(0, 1); strip.rectTransform.offsetMin = new Vector2(0, -18);
            var tt = face.transform;
            float left = -inner / 2 + 24;
            Text Cell(string text, float x, float yy, float w, int size, Color c, Font font = null)
            {
                var l = UiKit.Label(tt, "Cell", size, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, yy), new Vector2(w, rowH), font ?? Club.Title, false);
                l.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                l.text = text; l.color = c; l.raycastTarget = false; return l;
            }
            Image Mark(float x, float yy, Color c, bool round, float size)
            {
                var m = UiKit.Panel(tt, "Mark", c, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, yy), new Vector2(size, size));
                m.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                if (round) { m.sprite = UiKit.Circle; m.type = Image.Type.Simple; }
                m.raycastTarget = false; return m;
            }
            float r0 = (tableRows * rowH) / 2 - rowH / 2 - 9, r1 = r0 - rowH;
            Cell("HOLE", left + labelW / 2, r0, labelW, 34, Club.Muted, Club.Caps);
            Cell("PAR", left + labelW / 2, r1, labelW, 34, Club.Muted, Club.Caps);
            for (int i = 0; i < n; i++)
            {
                float x = left + labelW + 12 + colW * (i + 0.5f);
                Mark(x, r0, Club.Ink, true, 62);
                Cell($"{holes[i]}", x, r0, colW, 32, Color.white, Club.Caps);
                Cell($"{pars[i]}", x, r1, colW, 42, Club.Ink);
            }
            for (int p = 0; p < players.Length; p++)
            {
                float r2 = r1 - rowH * (p + 1);
                var band = UiKit.Panel(tt, players[p].Name, players[p].Color, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(left + labelW / 2, r2), new Vector2(labelW, rowH - 14));
                band.rectTransform.pivot = new Vector2(0.5f, 0.5f); band.sprite = UiKit.Circle; band.type = Image.Type.Sliced; band.raycastTarget = false;
                var name = Cell(players[p].Name, left + labelW / 2, r2, labelW - 24, 38, Club.Ink);
                Icons.Fit(name, 18, 38);
                for (int i = 0; i < n; i++)
                {
                    float x = left + labelW + 12 + colW * (i + 0.5f);
                    if (players[p].Cells != null)
                    {
                        var cell = Cell(i < players[p].Cells.Length ? players[p].Cells[i] ?? "–" : "–", x, r2, colW, 32, Club.Ink);
                        Icons.Fit(cell, 16, 32);
                    }
                    else if (i < players[p].Strokes.Length && players[p].Strokes[i] is int st)
                    {
                        int d = st - pars[i];
                        if (d < 0) Mark(x, r2, Club.Sun, true, 70);
                        else if (d > 0) Mark(x, r2, d == 1 ? Club.Coral : UiKit.Hex("C23A2E"), false, 64);
                        Cell($"{st}", x, r2, colW, 46, d > 0 ? Color.white : Club.Ink);
                    }
                    else Cell("–", x, r2, colW, 46, Club.Muted);
                }
            }
            y -= tableH + 18 + 26;

            // the total, and against par (with others, the standings are the tiles below)
            if (solo) AddTotal(f, ref y, total, toPar);

            AddRest(f, y, inner, highlights, nextHole, card, parent, W);
            Club.Enter(card, 0);
        }

        static void Top(RectTransform rt, float y, float h)
        {
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0.5f, 1);
            rt.anchoredPosition = new Vector2(0, y); rt.sizeDelta = new Vector2(0, h);
        }

        void AddTotal(RectTransform f, ref float y, int total, int toPar)
        {
            var row = Club.Box(f, "Total", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(0, 110));
            var bg = Club.Paint(row, "Fill", Club.Deep(0.85f), 40);
            var edge = Club.Paint(row, "Edge", Club.White(0.14f), 40, true);
            var tl = Club.Words(row, "Label", "Total", Club.Title, 56, Color.white, TextAnchor.MiddleLeft);
            tl.rectTransform.offsetMin = new Vector2(40, 0);
            var tv = Club.Words(row, "Value", $"{total}", Club.Display, 90, Color.white, TextAnchor.MiddleRight);
            tv.rectTransform.offsetMax = new Vector2(-40, 0);
            var chip = Club.Box(row, "To par", new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-170, 0), new Vector2(170, 70));
            var ci = chip.gameObject.AddComponent<Image>(); ci.sprite = UiKit.Circle; ci.type = Image.Type.Sliced; ci.raycastTarget = false;
            ci.color = toPar < 0 ? Club.Sun : toPar == 0 ? Club.Cream : Club.Coral;
            var cv = Club.Words(chip, "Text", GolfArcade.Course.Scorecard.FormatToPar(toPar), Club.Title, 44, toPar > 0 ? Color.white : Club.Ink, TextAnchor.MiddleCenter);
            y -= 110 + 26;
        }

        void AddRest(RectTransform f, float y, float inner, Highlight[] highlights, bool nextHole, RectTransform card, Transform parent, float W)
        {
            // the round's stats, three to a row, on small cream cards
            float hw = (inner - 2 * 18) / 3f;
            const float tileH = 156, tileGap = 22;
            for (int i = 0; i < highlights.Length; i++)
            {
                var h = highlights[i];
                int col = i % 3;
                float ty = y - (i / 3) * (tileH + tileGap);
                var tile = Club.Box(f, $"Highlight {i}", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-inner / 2 + hw / 2 + col * (hw + tileGap - 9), ty), new Vector2(hw, tileH));
                var tb = Club.Paint(tile, "Base", Club.CreamDeep, 40); tb.rectTransform.offsetMin = new Vector2(0, -10); tb.rectTransform.offsetMax = new Vector2(0, -10);
                Club.Paint(tile, "Face", Club.Cream, 40);
                var disc = UiKit.Panel(tile, "Disc", Club.Ink, new Vector2(0, 1), new Vector2(0, 1), new Vector2(50, -50), new Vector2(64, 64));
                disc.sprite = UiKit.Circle; disc.type = Image.Type.Simple; disc.raycastTarget = false; disc.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                Icons.Place(disc.transform, h.Icon, Club.Sun, new Vector2(0.5f, 0.5f), Vector2.zero, 38);
                var ht = Club.Words(tile, "Title", h.Title.ToUpperInvariant(), Club.Caps, 22, Club.Muted, TextAnchor.MiddleLeft);
                ht.rectTransform.anchorMin = new Vector2(0, 1); ht.rectTransform.anchorMax = new Vector2(1, 1); ht.rectTransform.pivot = new Vector2(0, 0.5f);
                ht.rectTransform.anchoredPosition = new Vector2(92, -50); ht.rectTransform.sizeDelta = new Vector2(-104, 56);
                Icons.Fit(ht, 15, 22);
                var hv = Club.Words(tile, "Value", h.Value, Club.Title, 50, Club.Ink, TextAnchor.MiddleCenter);
                hv.rectTransform.anchorMin = new Vector2(0, 0); hv.rectTransform.anchorMax = new Vector2(1, 0); hv.rectTransform.pivot = new Vector2(0.5f, 0);
                hv.rectTransform.anchoredPosition = new Vector2(0, 14); hv.rectTransform.sizeDelta = new Vector2(-20, 66);
                Icons.Fit(hv, 26, 50);
            }
            int rows = (highlights.Length + 2) / 3;
            y -= rows * tileH + Mathf.Max(0, rows - 1) * tileGap + 40;

            // the way on
            HoldButton Button(string name, string text, Club.Style style, float size, float x, float w, string art)
            {
                var b = Club.Button(f, name, text, style, size, null, art);
                var rt = (RectTransform)b.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1); rt.pivot = new Vector2(0.5f, 1);
                rt.anchoredPosition = new Vector2(x, y); rt.sizeDelta = new Vector2(w, 140);
                return b;
            }
            float bw = (inner - 24) / 2f;
            if (nextHole)
            {
                NextHole = Button("Next hole", "Next hole", Club.Style.Primary, 28, 0, inner, "play");
                y -= 140 + 34;
                PlayAgain = Button("Play again", againLabel, Club.Style.Secondary, 20, -bw / 2 - 12, bw, null);
            }
            else PlayAgain = Button("Play again", againLabel, Club.Style.Primary, 22, -bw / 2 - 12, bw, null);
            Menu = Button("Main menu", "Main menu", Club.Style.Quiet, 20, bw / 2 + 12, bw, null);
            y -= 140 + 30;
            card.sizeDelta = new Vector2(W, -y);

            // as small as a short screen needs
            float room = parent is RectTransform area && area.rect.height > 0 ? area.rect.height - 60 : -y;
            card.localScale = Vector3.one * Mathf.Min(1f, room / -y);
        }

        public static string ToParWords(int toPar) => toPar == 0 ? "Even par" : toPar < 0 ? $"{-toPar} under par" : $"{toPar} over par";

        public void Destroy() { if (Root) UnityEngine.Object.Destroy(Root.gameObject); }
    }

    /// A card arriving: in from a little under its size with an overshoot, over a quarter of a
    /// second.
    sealed class CardPop : MonoBehaviour
    {
        public float Size = 1f;
        float start = -1;

        void OnEnable() { start = Time.unscaledTime; Tick(); }
        void Update() => Tick();

        void Tick()
        {
            float u = Mathf.Clamp01((Time.unscaledTime - start) / 0.28f);
            float s = Mathf.Lerp(0.82f, 1f, 1f - Mathf.Pow(1f - u, 3f)) + 0.06f * Mathf.Sin(u * Mathf.PI);
            transform.localScale = Vector3.one * s * Size;
            if (u >= 1) { transform.localScale = Vector3.one * Size; enabled = false; }
        }
    }
}
