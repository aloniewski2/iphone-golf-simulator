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

    /// A hole or the round finished (option A): the title with a trophy, the headline on a yellow
    /// ribbon (the hole's score — "BIRDIE!" — or where the round ended against par), the card —
    /// hole, par, you, a gold ring round a birdie and a blue square round a bogey — the total and
    /// how it stands against par, the round's stats in tiles, and the way on: the next hole
    /// while there is one, the same holes again, or the main menu. It pops up over the course,
    /// shrunk to fit a short screen.
    public sealed class RoundCard
    {
        public readonly RectTransform Root;
        public readonly HoldButton PlayAgain, Menu;
        /// Null on the round's last hole.
        public readonly HoldButton NextHole;

        public struct Highlight { public string Icon, Title, Value; }

        public RoundCard(Transform parent, string title, string headline, int[] holes, int[] pars, int?[] strokes, int total, int toPar, Highlight[] highlights, bool nextHole)
        {
            const float W = 980;
            float y = 0;
            // the course dimmed behind it; touches stop here
            var sheet = UiKit.Panel(parent, "Scorecard", new Color(0.02f, 0.06f, 0.18f, 0.6f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false);
            Root = sheet.rectTransform;
            var card = UiKit.Pill(Root, "Round card", UiKit.ArcadeBlue, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(W, 1180), out var fill, 6f, false);
            foreach (var img in card.GetComponentsInChildren<Image>()) img.sprite = UiKit.RoundedLarge;
            var f = fill.rectTransform;
            float inner = W - 48;

            // the title
            y -= 26;
            var head = UiKit.Pill(f, "Title", UiKit.ArcadeBlueDeep, new Vector2(0.5f, 1), new Vector2(0, y - 60), new Vector2(inner, 120), out var headFill, 4f);
            var cup = UiKit.Panel(headFill.transform, "Cup", UiKit.ArcadeYellow, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(64, 0), new Vector2(84, 84));
            cup.sprite = UiKit.Circle; cup.type = Image.Type.Simple; cup.raycastTarget = false;
            Icons.Place(cup.transform, "trophy", UiKit.ArcadeInk, new Vector2(0.5f, 0.5f), Vector2.zero, 54);
            var t = UiKit.Chunky(headFill.transform, "Name", 60, Color.white, UiKit.ArcadeInk, 4f);
            t.text = title.ToUpperInvariant(); t.rectTransform.offsetMin = new Vector2(120, 16); t.rectTransform.offsetMax = new Vector2(-110, -16);
            Icons.Fit(t, 34, 60);
            Icons.Place(headFill.transform, "flag", UiKit.Hex("E8352F"), new Vector2(1, 0.5f), new Vector2(-58, 4), 64);
            y -= 120 + 22;

            // the headline, on the ribbon, a star either side
            var ribbon = UiKit.Pill(f, "Ribbon", UiKit.ArcadeYellow, new Vector2(0.5f, 1), new Vector2(0, y - 56), new Vector2(inner - 120, 112), out var ribbonFill, 4f, false);
            var rt = UiKit.Chunky(ribbonFill.transform, "Headline", 62, UiKit.ArcadeInk, new Color(1, 1, 1, 0), 0f);
            rt.text = (headline ?? ToParWords(toPar)).ToUpperInvariant();
            rt.rectTransform.offsetMin = new Vector2(90, 12); rt.rectTransform.offsetMax = new Vector2(-90, -12);
            Icons.Fit(rt, 36, 62);
            Icons.Place(ribbonFill.transform, "star", UiKit.ArcadeBlue, new Vector2(0, 0.5f), new Vector2(56, 0), 54);
            Icons.Place(ribbonFill.transform, "star", UiKit.ArcadeBlue, new Vector2(1, 0.5f), new Vector2(-56, 0), 54);
            y -= 112 + 22;

            // the card
            int n = holes.Length;
            const float rowH = 92, labelW = 170;
            float colW = Mathf.Min(140, (inner - 24 - labelW) / Mathf.Max(1, n));
            var table = UiKit.Panel(f, "Card", Color.white, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, y - (3 * rowH + 24) / 2), new Vector2(inner, 3 * rowH + 24));
            table.sprite = UiKit.RoundedLarge; table.raycastTarget = false; table.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var tt = table.transform;
            float left = -inner / 2 + 12;
            Text Cell(string text, float x, float yy, float w, int size, Color c)
            {
                var l = UiKit.Label(tt, "Cell", size, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, yy), new Vector2(w, rowH), UiKit.Display, false);
                l.text = text; l.color = c; l.raycastTarget = false; return l;
            }
            Image Mark(float x, float yy, Color c, bool round, float size)
            {
                var m = UiKit.Panel(tt, "Mark", c, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, yy), new Vector2(size, size));
                if (round) { m.sprite = UiKit.Circle; m.type = Image.Type.Simple; }
                m.raycastTarget = false; return m;
            }
            float r0 = (3 * rowH) / 2 - rowH / 2, r1 = 0, r2 = -rowH;
            var youBand = UiKit.Panel(tt, "You", UiKit.ArcadeYellow, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(left + labelW / 2, r2), new Vector2(labelW, rowH - 10));
            youBand.raycastTarget = false;
            Cell("HOLE", left + labelW / 2, r0, labelW, 34, UiKit.ArcadeBlue);
            Cell("PAR", left + labelW / 2, r1, labelW, 34, UiKit.ArcadeBlue);
            Cell("YOU", left + labelW / 2, r2, labelW, 34, UiKit.ArcadeInk);
            for (int i = 0; i < n; i++)
            {
                float x = left + labelW + 12 + colW * (i + 0.5f);
                Mark(x, r0, UiKit.ArcadeBlue, true, 62);
                Cell($"{holes[i]}", x, r0, colW, 30, Color.white);
                Cell($"{pars[i]}", x, r1, colW, 38, UiKit.ArcadeInk);
                if (strokes[i] is int s)
                {
                    int d = s - pars[i];
                    bool lit = d < 0 || d > 0;
                    if (d < 0) Mark(x, r2, UiKit.ArcadeYellow, true, 66);
                    else if (d > 0) Mark(x, r2, d == 1 ? UiKit.ArcadeBlue : UiKit.ArcadeBlueDeep, false, 64);
                    Cell($"{s}", x, r2, colW, 40, d > 0 ? Color.white : UiKit.ArcadeInk);
                }
                else Cell("–", x, r2, colW, 40, UiKit.ArcadeInk);
            }
            y -= 3 * rowH + 24 + 20;

            // the total, and against par
            var tot = UiKit.Pill(f, "Total", UiKit.ArcadeBlueDeep, new Vector2(0.5f, 1), new Vector2(0, y - 50), new Vector2(inner, 100), out var totFill, 4f);
            var tl = UiKit.Label(totFill.transform, "Label", 44, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, new Vector2(40, 0), Vector2.zero, UiKit.Display, false);
            tl.text = "TOTAL"; tl.color = Color.white;
            var tv = UiKit.Label(totFill.transform, "Value", 70, TextAnchor.MiddleRight, Vector2.zero, Vector2.one, new Vector2(-50, 0), Vector2.zero, UiKit.Display, false);
            tv.text = $"{total}"; tv.color = Color.white;
            var chip = UiKit.Pill(totFill.rectTransform, "To par", toPar < 0 ? UiKit.ArcadeYellow : toPar == 0 ? Color.white : UiKit.ArcadeBlue, new Vector2(1, 0.5f), new Vector2(-230, 0), new Vector2(150, 64), out var chipFill, 3f, false);
            var cv = UiKit.Label(chipFill.transform, "Text", 38, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, UiKit.Display, false);
            cv.text = GolfArcade.Course.Scorecard.FormatToPar(toPar); cv.color = toPar > 0 ? Color.white : UiKit.ArcadeInk;
            y -= 100 + 22;

            // the round's stats, three to a row
            float hw = (inner - 2 * 18) / 3f;
            const float tileH = 150, tileGap = 18;
            for (int i = 0; i < highlights.Length; i++)
            {
                var h = highlights[i];
                int col = i % 3;
                float ty = y - (i / 3) * (tileH + tileGap) - tileH / 2;
                var tile = UiKit.Panel(f, $"Highlight {i}", Color.white, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-inner / 2 + hw / 2 + col * (hw + tileGap), ty), new Vector2(hw, tileH));
                tile.sprite = UiKit.RoundedLarge; tile.raycastTarget = false; tile.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                var disc = UiKit.Panel(tile.transform, "Disc", UiKit.ArcadeBlue, new Vector2(0, 1), new Vector2(0, 1), new Vector2(46, -46), new Vector2(60, 60));
                disc.sprite = UiKit.Circle; disc.type = Image.Type.Simple; disc.raycastTarget = false; disc.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                Icons.Place(disc.transform, h.Icon, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, 38);
                var ht = UiKit.Label(tile.transform, "Title", 22, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(84, -46), new Vector2(-92, 56), UiKit.Display, false);
                ht.rectTransform.pivot = new Vector2(0, 0.5f); ht.rectTransform.sizeDelta = new Vector2(-92, 56); ht.text = h.Title.ToUpperInvariant(); ht.color = UiKit.ArcadeBlue;
                Icons.Fit(ht, 16, 22);
                var hv = UiKit.Label(tile.transform, "Value", 42, TextAnchor.MiddleCenter, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 42), new Vector2(0, 60), UiKit.Display, false);
                hv.text = h.Value.ToUpperInvariant(); hv.color = UiKit.ArcadeInk;
            }
            int rows = (highlights.Length + 2) / 3;
            y -= rows * tileH + Mathf.Max(0, rows - 1) * tileGap + 30;

            // the way on
            HoldButton Button(string name, string icon, string text, Color color, Color ink, float x, float w)
            {
                var b = UiKit.Pill(f, name, color, new Vector2(0.5f, 1), new Vector2(x, y - 58), new Vector2(w, 116), out var bf, 5f);
                var disc = UiKit.Panel(bf.transform, "Disc", ink, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(64, 0), new Vector2(66, 66));
                disc.sprite = UiKit.Circle; disc.type = Image.Type.Simple; disc.raycastTarget = false; disc.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                Icons.Place(disc.transform, icon, color, new Vector2(0.5f, 0.5f), new Vector2(icon == "play" ? 3 : 0, 0), 36);
                var l = UiKit.Label(bf.transform, "Label", 40, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, new Vector2(40, 0), Vector2.zero, UiKit.Display, false);
                l.text = text; l.color = ink; l.raycastTarget = false;
                var hit = b.gameObject.AddComponent<Image>(); hit.color = Color.clear;
                var hold = b.gameObject.AddComponent<HoldButton>();
                hold.Fill = bf; hold.RestColor = color;
                return hold;
            }
            float bw = (inner - 20) / 2f;
            var light = UiKit.Hex("DCEEFD");
            if (nextHole)
            {
                NextHole = Button("Next hole", "play", "NEXT HOLE", UiKit.ArcadeYellow, UiKit.ArcadeInk, 0, inner);
                y -= 116 + 20;
                PlayAgain = Button("Play again", "swing", "PLAY AGAIN", light, UiKit.ArcadeBlue, -bw / 2 - 10, bw);
            }
            else PlayAgain = Button("Play again", "play", "PLAY AGAIN", UiKit.ArcadeYellow, UiKit.ArcadeInk, -bw / 2 - 10, bw);
            Menu = Button("Main menu", "menu", "MAIN MENU", light, UiKit.ArcadeBlue, bw / 2 + 10, bw);
            y -= 116 + 30;
            card.sizeDelta = new Vector2(W, -y);

            // up with a bounce, as small as a short screen needs
            float room = parent is RectTransform area && area.rect.height > 0 ? area.rect.height - 60 : -y;
            card.gameObject.AddComponent<CardPop>().Size = Mathf.Min(1f, room / -y);
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
