using UnityEngine;
using UnityEngine.UI;

namespace GolfArcade.UI
{
    /// Where the ball finished, stamped over the course when it comes to rest (the user's pick,
    /// option A of the landing popups): a tilted badge in the colour of the ground it found with a
    /// round medallion on its left, under it a yellow pill with what is left, and a white strip
    /// with the carry and the total. It pops in with a bounce, holds, and shrinks away.
    public sealed class LandingBadge : MonoBehaviour
    {
        public enum Kind { Green, Fairway, Rough, Bunker, Water, OutOfBounds, Holed, Putt, Turn }

        /// A Turn badge's colour: the player's who is up.
        public Color TurnColor = Color.white;

        /// How big it stamps, against its full size (smaller on the phone than on the big screen).
        public float Size = 1f;

        RectTransform root, badge, medallion;
        Image fill, disc, icon, drop, dropTip;
        Text word, bang, detail, stats;
        RectTransform detailPill, statsStrip;
        float shownAt = -99f, holdFor;

        const float BadgeHeight = 150, Medallion = 176;

        public static LandingBadge Create(Transform parent)
        {
            var go = new GameObject("Landing badge", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var b = go.AddComponent<LandingBadge>();
            b.root = (RectTransform)go.transform;
            b.root.anchorMin = b.root.anchorMax = b.root.pivot = new Vector2(0.5f, 0.5f);
            b.root.anchoredPosition = new Vector2(0, 170); b.root.sizeDelta = new Vector2(760, 420);

            b.badge = UiKit.Pill(b.root, "Badge", Color.green, new Vector2(0.5f, 0.5f), new Vector2(40, 60), new Vector2(620, BadgeHeight), out b.fill, 7f, false);
            foreach (var img in b.badge.GetComponentsInChildren<Image>()) img.sprite = UiKit.RoundedLarge;
            b.word = UiKit.Chunky(b.fill.transform, "Word", 72, Color.white, UiKit.ArcadeInk, 4f);
            // one line's height, so a long word shrinks rather than wraps
            b.word.rectTransform.offsetMin = new Vector2(96, 22); b.word.rectTransform.offsetMax = new Vector2(-18, -22);
            Icons.Fit(b.word, 36, 72);

            b.medallion = UiKit.Pill(b.root, "Medallion", Color.white, new Vector2(0.5f, 0.5f), new Vector2(40 - 310, 64), new Vector2(Medallion, Medallion), out b.disc, 8f);
            foreach (var img in b.medallion.GetComponentsInChildren<Image>()) img.type = Image.Type.Simple;
            b.icon = Icons.Place(b.disc.transform, "flag", Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, 100);
            // the water drop, drawn: a round bottom and a point on top
            b.drop = UiKit.Panel(b.disc.transform, "Drop", Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(70, 70), false);
            b.drop.sprite = UiKit.Circle; b.drop.rectTransform.pivot = new Vector2(0.5f, 0.5f); b.drop.raycastTarget = false;
            b.dropTip = UiKit.Panel(b.disc.transform, "Tip", Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 14), new Vector2(50, 50), false);
            b.dropTip.rectTransform.pivot = new Vector2(0.5f, 0.5f); b.dropTip.rectTransform.localRotation = Quaternion.Euler(0, 0, 45); b.dropTip.raycastTarget = false;
            b.dropTip.transform.SetAsFirstSibling();
            b.bang = UiKit.Chunky(b.disc.transform, "Bang", 110, Color.white, new Color(0.45f, 0.05f, 0.05f), 4f);
            b.bang.text = "!";

            b.detailPill = UiKit.Pill(b.root, "Detail", UiKit.ArcadeYellow, new Vector2(0.5f, 0.5f), new Vector2(60, -54), new Vector2(540, 70), out var detailFill, 4f);
            b.detail = UiKit.Label(detailFill.transform, "Text", 34, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, UiKit.Display, false);
            b.detail.color = UiKit.ArcadeInk; b.detail.rectTransform.offsetMin = new Vector2(20, 0); b.detail.rectTransform.offsetMax = new Vector2(-20, 0);
            Icons.Fit(b.detail, 20, 34);

            b.statsStrip = UiKit.Pill(b.root, "Stats", Color.white, new Vector2(0.5f, 0.5f), new Vector2(60, -128), new Vector2(420, 56), out var statsFill, 3f);
            b.stats = UiKit.Label(statsFill.transform, "Text", 28, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, UiKit.Display, false);
            b.stats.color = UiKit.ArcadeInk;
            Icons.Fit(b.stats, 18, 28);

            foreach (var g in go.GetComponentsInChildren<Graphic>()) g.raycastTarget = false;
            go.SetActive(false);
            return b;
        }

        static readonly Color GreenFill = UiKit.Hex("5CC23A"), RoughFill = UiKit.Hex("2F6E2B"), SandFill = UiKit.Hex("F1D38C"),
                              WaterFill = UiKit.Hex("2A8BEA"), RedFill = UiKit.Hex("E0352F"), FlagRed = UiKit.Hex("E8352F");

        /// Stamps it: `word` on the badge, `detailText` on the yellow pill, `statsText` on the white
        /// strip (none if null), held `seconds`.
        public void Show(Kind kind, string wordText, string detailText, string statsText, float seconds = 2.4f)
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            // the ground's colour, the medallion's, and dark words on the light ones
            var (fillColor, discColor, iconName, iconColor, ink) = kind switch
            {
                Kind.Green or Kind.Putt => (GreenFill, UiKit.Hex("E9F8DE"), "flag", FlagRed, true),
                Kind.Fairway => (UiKit.ArcadeBlue, Color.white, "ball", UiKit.ArcadeBlue, false),
                Kind.Rough => (RoughFill, UiKit.Hex("3E8A38"), "grass", UiKit.Hex("B8F06A"), false),
                Kind.Bunker => (SandFill, UiKit.Hex("FFF3D6"), "ball", UiKit.Hex("C9A152"), true),
                Kind.Water => (WaterFill, Color.white, null, WaterFill, false),
                Kind.OutOfBounds => (RedFill, UiKit.Hex("FF5A4E"), null, Color.white, false),
                Kind.Turn => (TurnColor, Color.white, "swing", TurnColor, false),
                _ => (UiKit.ArcadeYellow, UiKit.ArcadeBlue, "trophy", UiKit.ArcadeYellow, true),
            };
            fill.color = fillColor;
            disc.color = discColor;
            icon.enabled = iconName != null;
            if (iconName != null) { icon.sprite = Icons.Get(iconName); icon.color = iconColor; }
            drop.enabled = dropTip.enabled = kind == Kind.Water;
            drop.color = dropTip.color = iconColor;
            bang.enabled = kind == Kind.OutOfBounds;
            word.text = wordText.ToUpperInvariant();
            word.color = ink ? UiKit.ArcadeInk : Color.white;
            foreach (var o in word.GetComponents<Shadow>()) o.enabled = !ink;
            detail.text = detailText.ToUpperInvariant();
            detailPill.gameObject.SetActive(!string.IsNullOrEmpty(detailText));
            stats.text = statsText?.ToUpperInvariant() ?? "";
            statsStrip.gameObject.SetActive(!string.IsNullOrEmpty(statsText));
            shownAt = Time.unscaledTime; holdFor = seconds;
            Tick();
        }

        public void Hide() { shownAt = -99f; gameObject.SetActive(false); }

        void Update() => Tick();

        /// The stamp: in from nothing with an overshoot and a turn to its tilt, a hold, and a quick
        /// shrink away.
        void Tick()
        {
            float t = Time.unscaledTime - shownAt;
            if (t > holdFor + 0.3f) { gameObject.SetActive(false); return; }
            float s;
            if (t < 0.32f)
            {
                float u = t / 0.32f;
                s = 1f - Mathf.Pow(1f - u, 3f) + 0.18f * Mathf.Sin(u * Mathf.PI);
            }
            else if (t < holdFor) s = 1f;
            else s = Mathf.Lerp(1f, 0f, (t - holdFor) / 0.3f);
            root.localScale = Vector3.one * Mathf.Max(0.01f, s) * Size;
            float tilt = t < 0.32f ? Mathf.Lerp(-14f, -4f, t / 0.32f) : -4f;
            badge.localRotation = Quaternion.Euler(0, 0, tilt);
            medallion.localRotation = Quaternion.Euler(0, 0, tilt * 0.5f);
        }
    }
}
