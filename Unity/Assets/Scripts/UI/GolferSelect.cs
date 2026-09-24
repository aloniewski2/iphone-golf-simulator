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
        readonly RectTransform kitRing, shirtRing;
        readonly Text kitName, shirtName;
        readonly string[] kitNames, shirtNames;
        float punch;
        int shownBody = -1;

        const float CardW = 1000, RowH = 150, Swatch = 92, SwatchStep = 118;
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

            // the style card: a row of swatches for the kit and one for the shirt
            // (its top band runs under the name plate and holds the dots; the plate and the card
            // hide the golfer's feet)
            const float TopBand = 96;
            float cardH = TopBand + RowH + 18 + RowH + 24;
            float cardY = 124 + 74 + 34 + cardH / 2;
            var card = UiKit.Pill(Root, "Style card", UiKit.ArcadeBlue, new Vector2(0.5f, 0), new Vector2(0, cardY), new Vector2(CardW, cardH), out var cardFill, 5f, false);
            foreach (var img in card.GetComponentsInChildren<Image>()) img.sprite = UiKit.RoundedLarge;
            var cf = cardFill.rectTransform;
            HoldButton[] Row(string label, float y, string[] names, Color[] colors, out RectTransform ring, out Text chosen)
            {
                var bar = UiKit.Pill(cf, label, RowFill, new Vector2(0.5f, 1), new Vector2(0, y - RowH / 2), new Vector2(CardW - 48, RowH), out var barFill, 3f, false);
                foreach (var img in bar.GetComponentsInChildren<Image>()) img.sprite = UiKit.RoundedLarge;
                var bt = barFill.transform;
                var l = UiKit.Label(bt, "Label", 34, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -22), new Vector2(200, 40), UiKit.Display, false);
                l.text = label; l.color = Color.white;
                chosen = UiKit.Label(bt, "Chosen", 24, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -66), new Vector2(200, 34), UiKit.Display, false);
                chosen.color = UiKit.ArcadeYellow;
                Icons.Fit(chosen, 16, 24);
                float x0 = (CardW - 48) / 2 - 24 - SwatchStep * (colors.Length - 1) - Swatch / 2;
                ring = UiKit.Panel(bt, "Ring", UiKit.ArcadeYellow, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(Swatch + 26, Swatch + 26), false).rectTransform;
                ring.GetComponent<Image>().sprite = UiKit.Circle; ring.GetComponent<Image>().raycastTarget = false; ring.pivot = new Vector2(0.5f, 0.5f);
                ring.SetAsFirstSibling();   // (under the swatches)
                var holds = new HoldButton[colors.Length];
                for (int i = 0; i < colors.Length; i++)
                {
                    float x = x0 + SwatchStep * i;
                    var rim = UiKit.Panel(bt, $"{label} {names[i]}", Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 0), new Vector2(Swatch, Swatch), false);
                    rim.sprite = UiKit.Circle; rim.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    var fill = UiKit.Panel(rim.transform, "Colour", colors[i], new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(Swatch - 14, Swatch - 14), false);
                    fill.sprite = UiKit.Circle; fill.rectTransform.pivot = new Vector2(0.5f, 0.5f); fill.raycastTarget = false;
                    var hold = rim.gameObject.AddComponent<HoldButton>();
                    hold.Fill = rim; hold.RestColor = Color.white;
                    holds[i] = hold;
                }
                return holds;
            }
            Kits = Row("KIT", -TopBand, kitNames, kitColors, out kitRing, out kitName);
            Shirts = Row("SHIRT", -TopBand - RowH - 18, shirtNames, shirtColors, out shirtRing, out shirtName);

            // the name plate on the card's top edge, a tab over it and a dot for each golfer
            float plateY = cardY + cardH / 2;
            plate = UiKit.Pill(Root, "Name plate", UiKit.ArcadeYellow, new Vector2(0.5f, 0), new Vector2(0, plateY), new Vector2(640, 118), out var plateFill, 6f);
            plateName = UiKit.Chunky(plateFill.transform, "Name", 58, UiKit.ArcadeInk, new Color(1, 1, 1, 0), 0f);
            var tab = UiKit.Pill(Root, "Tab", UiKit.ArcadeBlueDeep, new Vector2(0.5f, 0), new Vector2(0, plateY + 59 + 26), new Vector2(300, 54), out var tabFill, 3f);
            var tabWord = UiKit.Label(tabFill.transform, "Word", 26, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, UiKit.Display, false);
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
            Place(kitRing, Kits[kit]); kitName.text = kitNames[kit].ToUpperInvariant();
            Place(shirtRing, Shirts[shirt]); shirtName.text = shirtNames[shirt].ToUpperInvariant();
        }

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
