using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GolfArcade.UI
{
    /// Choosing the golfer, in the arcade cards' look: SELECT GOLFER on a navy pill, the golfer
    /// large in the middle of the screen between two round arrows (drag them to turn them round),
    /// their name on a yellow plate with a dot for each golfer, the kit and shirt colours on a
    /// blue card, and LET'S GO. The game wires the buttons and calls Refresh with the choices.
    public sealed class GolferSelect
    {
        public readonly RectTransform Root;
        public readonly HoldButton Previous, Next, Go;
        public readonly HoldButton[] Kits, Shirts;
        /// Dragging across the golfer: screen pixels this frame, and the let-go.
        public Action<float> Spin;
        public Action SpinDone;

        readonly Text plateName;
        readonly RectTransform plate;
        readonly Image[] dots;
        readonly Text tabWord;
        readonly RectTransform lookPage, gearPage;
        readonly Text pageWord;
        readonly float gearTop;
        /// The rows: kit, shirt, ball, trail, club.
        readonly RowParts[] rows = new RowParts[5];

        /// A row of swatches: its buttons, the ring round the one chosen, the word under its
        /// label, and the padlocks over the ones still to be earned.
        sealed class RowParts
        {
            public HoldButton[] Holds; public RectTransform Ring; public Text Chosen; public Image[] Locks; public Image[] Fills; public string[] Names;
        }

        /// LOOK | GEAR.
        public readonly HoldButton PageButton;
        public HoldButton[] Balls = new HoldButton[0], Trails = new HoldButton[0], Clubs = new HoldButton[0];
        readonly string[] kitNames, shirtNames;
        float punch;
        int shownBody = -1;

        const float CardW = 1000, RowH = 150, Swatch = 92, SwatchStep = 118, GearRowH = 118, GearSwatch = 80, GearStep = 102;
        static readonly Color RowFill = new(0.10f, 0.24f, 0.66f, 1f);

        public GolferSelect(Transform parent, string[] kitNames, Color[] kitColors, string[] shirtNames, Color[] shirtColors)
        {
            this.kitNames = kitNames; this.shirtNames = shirtNames;
            Root = new GameObject("Golfer select").AddComponent<RectTransform>();
            Root.SetParent(parent, false);
            Root.anchorMin = Vector2.zero; Root.anchorMax = Vector2.one; Root.offsetMin = Root.offsetMax = Vector2.zero;

            // the golfer's ground: drag to turn them round
            var spinArea = UiKit.Panel(Root, "Spin area", Color.clear, new Vector2(0, 0.36f), new Vector2(1, 0.9f), Vector2.zero, Vector2.zero, false);
            spinArea.rectTransform.offsetMin = spinArea.rectTransform.offsetMax = Vector2.zero;
            var drag = spinArea.gameObject.AddComponent<SpinDrag>();
            drag.Moved = dx => Spin?.Invoke(dx);
            drag.Released = () => SpinDone?.Invoke();

            // the title
            var title = UiKit.Pill(Root, "Title", UiKit.ArcadeBlueDeep, new Vector2(0.5f, 1), new Vector2(0, -100), new Vector2(860, 128), out var titleFill, 5f);
            var disc = UiKit.Panel(titleFill.transform, "Disc", UiKit.ArcadeYellow, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(66, 0), new Vector2(86, 86));
            disc.sprite = UiKit.Circle; disc.type = Image.Type.Simple; disc.raycastTarget = false; disc.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            Icons.Place(disc.transform, "swing", UiKit.ArcadeInk, new Vector2(0.5f, 0.5f), Vector2.zero, 56);
            var t = UiKit.Chunky(titleFill.transform, "Word", 60, Color.white, UiKit.ArcadeInk, 4f);
            t.text = "SELECT GOLFER"; t.rectTransform.offsetMin = new Vector2(70, 0);
            Icons.Place(titleFill.transform, "flag", UiKit.Hex("E8352F"), new Vector2(1, 0.5f), new Vector2(-62, 4), 66);

            // the arrows either side of the golfer
            HoldButton Arrow(string name, string glyph, float anchorX, float x)
            {
                var a = UiKit.Pill(Root, name, UiKit.ArcadeBlue, new Vector2(anchorX, 0.62f), new Vector2(x, 0), new Vector2(136, 136), out var fill, 6f);
                foreach (var layer in a.GetComponentsInChildren<Image>()) layer.type = Image.Type.Simple;
                var g = UiKit.Chunky(fill.transform, "Glyph", 58, Color.white, UiKit.ArcadeInk, 3f);
                g.text = glyph;
                var hit = a.gameObject.AddComponent<Image>(); hit.color = Color.clear;
                var hold = a.gameObject.AddComponent<HoldButton>();
                hold.Fill = fill; hold.RestColor = UiKit.ArcadeBlue;
                return hold;
            }
            Previous = Arrow("Previous golfer", "◀", 0, 104);
            Next = Arrow("Next golfer", "▶", 1, -104);

            // LET'S GO, at the foot
            var go = UiKit.Pill(Root, "Lets go", UiKit.ArcadeYellow, new Vector2(0.5f, 0), new Vector2(0, 124), new Vector2(CardW, 148), out var goFill, 6f);
            var goDisc = UiKit.Panel(goFill.transform, "Disc", UiKit.ArcadeInk, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(82, 0), new Vector2(92, 92));
            goDisc.sprite = UiKit.Circle; goDisc.type = Image.Type.Simple; goDisc.raycastTarget = false; goDisc.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            Icons.Place(goDisc.transform, "play", UiKit.ArcadeYellow, new Vector2(0.5f, 0.5f), new Vector2(4, 0), 50);
            var goWord = UiKit.Chunky(goFill.transform, "Word", 62, UiKit.ArcadeInk, new Color(1, 1, 1, 0), 0f);
            goWord.text = "LET'S GO";
            var goHit = go.gameObject.AddComponent<Image>(); goHit.color = Color.clear;
            Go = go.gameObject.AddComponent<HoldButton>();
            Go.Fill = goFill; Go.RestColor = UiKit.ArcadeYellow;

            // the style card: two pages — LOOK (a row of swatches for the kit, one for the shirt)
            // and GEAR (the ball, the trail behind it, the clubs' finish) — and a padlock on
            // whatever is still to be earned. Its top band runs under the name plate.
            const float TopBand = 96;
            float cardH = TopBand + Mathf.Max(RowH + 18 + RowH, 3 * GearRowH + 2 * 14) + 24;
            float cardY = 124 + 74 + 34 + cardH / 2;
            var card = UiKit.Pill(Root, "Style card", UiKit.ArcadeBlue, new Vector2(0.5f, 0), new Vector2(0, cardY), new Vector2(CardW, cardH), out var cardFill, 5f, false);
            foreach (var img in card.GetComponentsInChildren<Image>()) img.sprite = UiKit.RoundedLarge;
            var cf = cardFill.rectTransform;
            lookPage = Page(cf, "Look"); gearPage = Page(cf, "Gear");
            Kits = Row(lookPage, "KIT", -TopBand, RowH, Swatch, SwatchStep, kitNames, kitColors, out var kit);
            Shirts = Row(lookPage, "SHIRT", -TopBand - RowH - 18, RowH, Swatch, SwatchStep, shirtNames, shirtColors, out var shirt);
            rows[0] = kit; rows[1] = shirt;
            gearPage.gameObject.SetActive(false);

            // LOOK | GEAR, beside the name plate
            var toggle = UiKit.Pill(Root, "Gear", UiKit.ArcadeBlueDeep, new Vector2(0.5f, 0), new Vector2(412, cardY + cardH / 2), new Vector2(150, 70), out var toggleFill, 3f);
            pageWord = UiKit.Label(toggleFill.transform, "Word", 28, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, UiKit.Display, false);
            pageWord.text = "GEAR"; pageWord.color = Color.white; pageWord.raycastTarget = false;
            var toggleHit = toggle.gameObject.AddComponent<Image>(); toggleHit.color = Color.clear;
            PageButton = toggle.gameObject.AddComponent<HoldButton>();
            PageButton.Fill = toggleFill; PageButton.RestColor = UiKit.ArcadeBlueDeep;
            PageButton.Pressed = () => ShowGear(!gearPage.gameObject.activeSelf);
            gearTop = -TopBand;

            // the name plate on the card's top edge, a tab over it and a dot for each golfer
            float plateY = cardY + cardH / 2;
            plate = UiKit.Pill(Root, "Name plate", UiKit.ArcadeYellow, new Vector2(0.5f, 0), new Vector2(0, plateY), new Vector2(640, 118), out var plateFill, 6f);
            plateName = UiKit.Chunky(plateFill.transform, "Name", 58, UiKit.ArcadeInk, new Color(1, 1, 1, 0), 0f);
            var tab = UiKit.Pill(Root, "Tab", UiKit.ArcadeBlueDeep, new Vector2(0.5f, 0), new Vector2(0, plateY + 59 + 26), new Vector2(300, 54), out var tabFill, 3f);
            tabWord = UiKit.Label(tabFill.transform, "Word", 26, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, UiKit.Display, false);
            tabWord.text = "YOUR GOLFER"; tabWord.color = Color.white;
            dots = new Image[2];
            for (int i = 0; i < dots.Length; i++)
            {
                var d = UiKit.Panel(Root, $"Dot {i}", Color.white, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2((i - 0.5f) * 40, plateY - 59 - 20), new Vector2(20, 20), false);
                d.sprite = UiKit.Circle; d.rectTransform.pivot = new Vector2(0.5f, 0.5f); d.raycastTarget = false;
                dots[i] = d;
            }
            tab.SetAsLastSibling();

            var animator = Root.gameObject.AddComponent<Animate>();
            animator.Tick = dt =>
            {
                punch = Mathf.MoveTowards(punch, 0, dt * 3f);
                float s = 1f + 0.12f * Mathf.Sin(punch * Mathf.PI) * punch;
                plate.localScale = new Vector3(s, s, 1);
            };
        }

        /// Lights the choices: the golfer's name and dot, a ring round the kit and the shirt.
        public void Refresh(bool female, int kit, int shirt)
        {
            int body = female ? 1 : 0;
            if (shownBody >= 0 && body != shownBody) punch = 1f;
            shownBody = body;
            plateName.text = female ? "FEMALE GOLFER" : "MALE GOLFER";
            for (int i = 0; i < dots.Length; i++) dots[i].color = i == body ? Color.white : new Color(1, 1, 1, 0.4f);
            Choose(rows[0], kit); Choose(rows[1], shirt);
        }

        static void Choose(RowParts row, int index)
        {
            if (row == null || index < 0 || index >= row.Holds.Length) return;
            Place(row.Ring, row.Holds[index]);
            row.Chosen.text = row.Names[index].ToUpperInvariant();
        }

        static RectTransform Page(RectTransform card, string name)
        {
            var page = new GameObject(name + " page").AddComponent<RectTransform>();
            page.SetParent(card, false);
            page.anchorMin = Vector2.zero; page.anchorMax = Vector2.one; page.offsetMin = page.offsetMax = Vector2.zero;
            return page;
        }

        /// A row of round swatches under a label, the ring round the chosen one under them.
        static HoldButton[] Row(RectTransform page, string label, float y, float rowH, float swatch, float step, string[] names, Color[] colors, out RowParts parts, Sprite[] faces = null)
        {
            var bar = UiKit.Pill(page, label, RowFill, new Vector2(0.5f, 1), new Vector2(0, y - rowH / 2), new Vector2(CardW - 48, rowH), out var barFill, 3f, false);
            foreach (var img in bar.GetComponentsInChildren<Image>()) img.sprite = UiKit.RoundedLarge;
            var bt = barFill.transform;
            float top = rowH > 130 ? -22 : -14;
            var l = UiKit.Label(bt, "Label", 34, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, top), new Vector2(260, 40), UiKit.Display, false);
            l.text = label; l.color = Color.white;
            var chosen = UiKit.Label(bt, "Chosen", 24, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, top - 44), new Vector2(330, 34), UiKit.Display, false);
            chosen.color = UiKit.ArcadeYellow;
            Icons.Fit(chosen, 14, 24);
            float x0 = (CardW - 48) / 2 - 24 - step * (colors.Length - 1) - swatch / 2;
            var ring = UiKit.Panel(bt, "Ring", UiKit.ArcadeYellow, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(swatch + 26, swatch + 26), false).rectTransform;
            ring.GetComponent<Image>().sprite = UiKit.Circle; ring.GetComponent<Image>().raycastTarget = false; ring.pivot = new Vector2(0.5f, 0.5f);
            ring.SetAsFirstSibling();   // (under the swatches)
            var holds = new HoldButton[colors.Length];
            var locks = new Image[colors.Length];
            var fills = new Image[colors.Length];
            for (int i = 0; i < colors.Length; i++)
            {
                float x = x0 + step * i;
                var rim = UiKit.Panel(bt, $"{label} {names[i]}", Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 0), new Vector2(swatch, swatch), false);
                rim.sprite = UiKit.Circle; rim.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                var fill = UiKit.Panel(rim.transform, "Colour", colors[i], new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(swatch - 14, swatch - 14), false);
                fill.sprite = faces != null && faces[i] ? faces[i] : UiKit.Circle; fill.rectTransform.pivot = new Vector2(0.5f, 0.5f); fill.raycastTarget = false;
                if (faces != null && faces[i]) fill.color = Color.white;
                // a padlock badge on the swatch's corner, for what is still to be earned
                var badge = UiKit.Panel(rim.transform, "Padlock", UiKit.ArcadeBlueDeep, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(swatch * 0.32f, -swatch * 0.32f), new Vector2(swatch * 0.5f, swatch * 0.5f), false);
                badge.sprite = UiKit.Circle; badge.rectTransform.pivot = new Vector2(0.5f, 0.5f); badge.raycastTarget = false;
                var badgeIcon = Icons.Place(badge.transform, "lock", Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, swatch * 0.3f);
                badgeIcon.raycastTarget = false;
                var padlock = badge;
                padlock.gameObject.SetActive(false);
                var hold = rim.gameObject.AddComponent<HoldButton>();
                hold.Fill = rim; hold.RestColor = Color.white;
                holds[i] = hold; locks[i] = padlock; fills[i] = fill;
            }
            parts = new RowParts { Holds = holds, Ring = ring, Chosen = chosen, Locks = locks, Fills = fills, Names = names };
            return holds;
        }

        /// The GEAR page's rows: the balls, the trails and the club finishes, by name and swatch
        /// (a trail's may be a face of its own, the rainbow's).
        public void AddGear(string[] balls, Color[] ballColors, string[] trails, Color[] trailColors, Sprite[] trailFaces, string[] clubs, Color[] clubColors)
        {
            Balls = Row(gearPage, "BALL", gearTop, GearRowH, GearSwatch, GearStep, balls, ballColors, out rows[2]);
            Trails = Row(gearPage, "TRAIL", gearTop - GearRowH - 14, GearRowH, GearSwatch, GearStep, trails, trailColors, out rows[3], trailFaces);
            Clubs = Row(gearPage, "CLUBS", gearTop - 2 * (GearRowH + 14), GearRowH, GearSwatch, GearStep, clubs, clubColors, out rows[4]);
        }

        /// The GEAR page (true) or the LOOK page.
        public void ShowGear(bool on)
        {
            gearPage.gameObject.SetActive(on);
            lookPage.gameObject.SetActive(!on);
            pageWord.text = on ? "LOOK" : "GEAR";
        }

        public bool ShowingGear => gearPage.gameObject.activeSelf;

        public enum Rows { Kit, Shirt, Ball, Trail, Club }

        /// Padlocks on the swatches still to be earned (true is open), dimming them.
        public void SetOpen(Rows row, bool[] open)
        {
            var r = rows[(int)row];
            if (r == null) return;
            for (int i = 0; i < r.Locks.Length && i < open.Length; i++)
            {
                r.Locks[i].gameObject.SetActive(!open[i]);
                var c = r.Fills[i].color; c.a = open[i] ? 1f : 0.35f; r.Fills[i].color = c;
            }
        }

        /// Rings the chosen ball, trail and clubs.
        public void RefreshGear(int ball, int trail, int club) { Choose(rows[2], ball); Choose(rows[3], trail); Choose(rows[4], club); }

        /// A padlocked swatch pressed: what it takes, under the row's label.
        public void SayLocked(Rows row, string requirement)
        {
            var r = rows[(int)row];
            if (r != null) r.Chosen.text = $"LOCKED: {requirement}".ToUpperInvariant();
        }

        /// Whose golfer is being picked, over the name plate ("YOUR GOLFER", or a player's name).
        public void SetTab(string text) => tabWord.text = text.ToUpperInvariant();

        static void Place(RectTransform ring, HoldButton on) => ring.anchoredPosition = ((RectTransform)on.transform).anchoredPosition;

        public void Destroy() { if (Root) UnityEngine.Object.Destroy(Root.gameObject); }

        /// Drag reports for the golfer's ground.
        sealed class SpinDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
        {
            public Action<float> Moved;
            public Action Released;
            public void OnBeginDrag(PointerEventData e) { }
            public void OnDrag(PointerEventData e) => Moved?.Invoke(e.delta.x);
            public void OnEndDrag(PointerEventData e) => Released?.Invoke();
        }

        sealed class Animate : MonoBehaviour
        {
            public Action<float> Tick;
            void Update() => Tick?.Invoke(Time.unscaledDeltaTime);
        }
    }
}
