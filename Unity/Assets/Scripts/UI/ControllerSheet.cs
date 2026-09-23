using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GolfArcade.UI
{
    /// The dial's knob as a joystick: drag it off centre and `Value` is where it sits, -1..1
    /// each way, springing back when let go. A press without a drag still counts as a press.
    public sealed class Joystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public float Radius = 150f;
        public Vector2 Value { get; private set; }
        RectTransform rt;
        Vector2 home;

        void Awake() { rt = (RectTransform)transform; home = rt.anchoredPosition; }

        public void OnPointerDown(PointerEventData e) { Move(e); }
        public void OnDrag(PointerEventData e) { Move(e); }
        public void OnPointerUp(PointerEventData e) { Value = Vector2.zero; rt.anchoredPosition = home; }

        void Move(PointerEventData e)
        {
            var dial = (RectTransform)rt.parent;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(dial, e.position, e.pressEventCamera, out var local)) return;
            var offset = Vector2.ClampMagnitude(local - (dial.rect.center + home), Radius);
            rt.anchoredPosition = home + offset;
            Value = offset / Radius;
        }
    }

    /// The phone as the controller while the course plays on the big screen: the whole phone,
    /// bright and chunky like a Wii menu (the user's pick of the Higgsfield mock-ups). On a sky
    /// with puffy clouds: a "HOLE 12" badge and whether the TV is on; pills for par, the distance
    /// to the pin and the wind; the live minimap in a white frame (the game's map camera and
    /// marks); the clubs as cards with cartoon club icons (Resources/Clubs/*_toon), the one in hand
    /// yellow with a star; and the aim pad — a glossy joystick whose knob nudges the line, its
    /// arrows turning it (◀ ▶) and changing club (▲ ▼), a ring round it filling yellow with the
    /// backswing, and a pill under it saying where the line points.
    public sealed class ControllerSheet
    {
        public HoldButton AimLeft, AimRight, ClubUp, ClubDown, Knob;
        /// The knob's drag: x turns the aim.
        public Joystick Stick;
        public HoldButton[] Clubs;
        public Action<int> OnClub;
        /// Where the minimap goes (Hud moves its RawImage in and back out).
        public RectTransform MapSlot { get; private set; }
        /// The map's picture size, for framing the map camera to it.
        public static readonly Vector2 MapSize = new(972, 700);

        static readonly Color SkyTop = UiKit.Hex("2F9DEB"), SkyBottom = UiKit.Hex("93D5FA");
        static readonly Color Blue = UiKit.Hex("2A84E6"), BlueDeep = UiKit.Hex("1557B8"), CardBlue = UiKit.Hex("DCEEFD");
        static readonly Color Yellow = UiKit.Hex("FFD23A"), Green = UiKit.Hex("36B34A"), Amber = UiKit.Hex("F2A81D");
        const float Width = 992;
        static readonly string[] Pictures = { "Clubs/driver_toon", "Clubs/iron_toon", "Clubs/wedge_toon", "Clubs/putter_toon" };

        readonly Transform parent;
        RectTransform root, sheet;
        Image ring;
        Image tvFill;
        Text holeText, parText, yardsText, windText, aimText, tvText;
        RectTransform windArrow;
        Image[] clubFills, clubGlows; GameObject[] clubStars; Text[] clubNames;
        static Sprite skySprite;

        public ControllerSheet(Transform safeArea)
        {
            parent = safeArea;
            Build();
        }

        public void Destroy() { if (root) UnityEngine.Object.Destroy(root.gameObject); root = null; }
        public bool Alive => root;

        /// A pill (white rim, blue fill, drop shadow) centred at `y` down from the top.
        RectTransform Pill(string name, Color fill, float x, float y, Vector2 size, out Image fillImg, float rim = 6f, bool round = true)
            => UiKit.Pill(sheet, name, fill, new Vector2(0.5f, 1), new Vector2(x, -y), size, out fillImg, rim, round);

        Text Chunky(Transform p, string name, string text, int size, float weight = 4f, Color? fill = null, Color? outline = null)
        {
            var t = UiKit.Chunky(p, name, size, fill ?? Color.white, outline ?? BlueDeep, weight);
            t.text = text;
            return t;
        }

        static Image Blob(Transform p, string name, Color c, Vector2 pos, Vector2 size)
        {
            var img = UiKit.Panel(p, name, c, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
            img.sprite = UiKit.Circle; img.type = Image.Type.Simple;   // a true circle or ellipse, not a capsule
            img.rectTransform.pivot = new Vector2(0.5f, 0.5f); img.raycastTarget = false;
            return img;
        }

        /// A puffy cartoon cloud of overlapping circles, anchored at a fraction of the screen.
        void Cloud(Vector2 at, float scale)
        {
            var c = new GameObject("Cloud").AddComponent<RectTransform>();
            c.SetParent(root, false);
            c.anchorMin = c.anchorMax = at; c.sizeDelta = Vector2.zero; c.localScale = Vector3.one * scale;
            var white = new Color(1, 1, 1, 0.92f);
            Blob(c, "Base", white, new Vector2(0, -20), new Vector2(300, 90));
            Blob(c, "Puff", white, new Vector2(-70, 5), new Vector2(130, 120));
            Blob(c, "Puff", white, new Vector2(20, 30), new Vector2(170, 160));
            Blob(c, "Puff", white, new Vector2(100, 0), new Vector2(110, 100));
        }

        static Sprite Sky()
        {
            if (skySprite) return skySprite;
            var tex = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < 64; y++) tex.SetPixel(0, y, Color.Lerp(SkyBottom, SkyTop, y / 63f));
            tex.Apply();
            return skySprite = Sprite.Create(tex, new Rect(0, 0, 1, 64), new Vector2(0.5f, 0.5f));
        }

        void Build()
        {
            var go = new GameObject("Controller");
            go.transform.SetParent(parent, false);
            root = go.AddComponent<RectTransform>();
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
            // past the safe area to the screen's edges, so the notch and home bar sit on sky
            root.offsetMin = new Vector2(0, -400); root.offsetMax = new Vector2(0, 400);
            var sky = go.AddComponent<Image>(); sky.sprite = Sky();   // (and it takes the taps meant for nothing)
            Cloud(new Vector2(0.02f, 0.86f), 1.0f); Cloud(new Vector2(0.98f, 0.70f), 0.9f); Cloud(new Vector2(0.0f, 0.42f), 0.8f);
            Cloud(new Vector2(1.0f, 0.30f), 1.1f); Cloud(new Vector2(0.08f, 0.16f), 0.9f);
            sheet = new GameObject("Sheet").AddComponent<RectTransform>();
            sheet.SetParent(root, false);
            sheet.anchorMin = Vector2.zero; sheet.anchorMax = Vector2.one; sheet.offsetMin = new Vector2(0, 400); sheet.offsetMax = new Vector2(0, -400);

            // ---- the hole badge, with its flag, and the TV
            var badge = Pill("Hole", Blue, -20, 110, new Vector2(560, 150), out var badgeFill, 9);
            var hole = Blob(badgeFill.transform, "Cup", Green, Vector2.zero, new Vector2(84, 40));
            hole.rectTransform.anchorMin = hole.rectTransform.anchorMax = new Vector2(0, 0.5f); hole.rectTransform.anchoredPosition = new Vector2(78, -24);
            var pole = UiKit.Panel(hole.transform, "Pole", Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-6, 0), new Vector2(7, 86), false);
            pole.rectTransform.pivot = new Vector2(0.5f, 0); pole.raycastTarget = false;
            var flag = UiKit.Panel(pole.transform, "Flag", UiKit.Hex("E8352F"), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(3, 0), new Vector2(46, 32), false);
            flag.rectTransform.pivot = new Vector2(0, 1); flag.rectTransform.localRotation = Quaternion.Euler(0, 0, -6); flag.raycastTarget = false;
            holeText = Chunky(badgeFill.transform, "Label", "HOLE", 92, 5f);
            holeText.rectTransform.offsetMin = new Vector2(120, 0);
            var tv = Pill("TV", Green, 420, 70, new Vector2(170, 76), out tvFill, 5);
            tvText = Chunky(tvFill.transform, "Label", "TV ON", 30, 2f, Color.white, UiKit.Hex("1E7A30"));

            // ---- par, the distance, the wind
            float chipW = (Width - 40) / 3f, chipY = 272;
            Pill("Par", Blue, -chipW - 20, chipY, new Vector2(chipW, 108), out var parFill);
            parText = Chunky(parFill.transform, "Label", "PAR", 46, 3f);
            Pill("Yards", Blue, 0, chipY, new Vector2(chipW, 108), out var yardsFill);
            Blob(yardsFill.transform, "Ball", Color.white, new Vector2(-chipW / 2 + 58, 0), new Vector2(54, 54)).rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            yardsText = Chunky(yardsFill.transform, "Label", "", 46, 3f);
            yardsText.rectTransform.offsetMin = new Vector2(60, 0);
            Pill("Wind", Blue, chipW + 20, chipY, new Vector2(chipW, 108), out var windFill);
            var arrow = Chunky(windFill.transform, "Arrow", "➤", 50, 3f, UiKit.Hex("6FE3FF"));
            arrow.rectTransform.anchorMin = arrow.rectTransform.anchorMax = new Vector2(0, 0.5f); arrow.rectTransform.sizeDelta = new Vector2(70, 70);
            arrow.rectTransform.anchoredPosition = new Vector2(62, 0);
            windArrow = arrow.rectTransform;
            windText = Chunky(windFill.transform, "Label", "", 46, 3f);
            windText.rectTransform.offsetMin = new Vector2(70, 0);

            // ---- the map, in a thick white frame
            var frame = Pill("Map", Color.white, 0, 362 + (MapSize.y + 20) / 2, new Vector2(Width, MapSize.y + 20), out var mapFill, 0, false);
            mapFill.sprite = UiKit.RoundedLarge; mapFill.color = Color.white;
            var mask = UiKit.Panel(mapFill.transform, "Mask", Color.white, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            mask.sprite = UiKit.RoundedLarge; mask.rectTransform.offsetMin = new Vector2(10, 10); mask.rectTransform.offsetMax = new Vector2(-10, -10);
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = false; mask.raycastTarget = false;
            foreach (var img in frame.GetComponentsInChildren<Image>()) if (img.name == "Rim" || img.name == "Shadow") img.sprite = UiKit.RoundedLarge;
            MapSlot = mask.rectTransform;

            // ---- the clubs
            float clubsY = 362 + MapSize.y + 20 + 70;
            var heading = Chunky(sheet, "Clubs heading", "CLUBS", 54, 4f);
            heading.rectTransform.anchorMin = heading.rectTransform.anchorMax = new Vector2(0.5f, 1);
            heading.rectTransform.sizeDelta = new Vector2(400, 80); heading.rectTransform.anchoredPosition = new Vector2(0, -clubsY);
            foreach (float side in new[] { -1f, 1f })
            {
                var rule = UiKit.Panel(sheet, "Rule", new Color(1, 1, 1, 0.85f), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(side * 250, -clubsY), new Vector2(240, 7));
                rule.rectTransform.pivot = new Vector2(0.5f, 0.5f); rule.raycastTarget = false;
            }
            int n = Pictures.Length;
            Clubs = new HoldButton[n]; clubFills = new Image[n]; clubGlows = new Image[n]; clubStars = new GameObject[n]; clubNames = new Text[n];
            float cardW = (Width - 3 * 22) / 4f, cardH = 330, cardY = clubsY + 60 + cardH / 2;
            for (int i = 0; i < n; i++)
            {
                float x = -Width / 2 + cardW / 2 + i * (cardW + 22);
                var glow = UiKit.Panel(sheet, "Glow", new Color(Yellow.r, Yellow.g, Yellow.b, 0.45f), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(x, -cardY), new Vector2(cardW + 34, cardH + 34));
                glow.rectTransform.pivot = new Vector2(0.5f, 0.5f); glow.sprite = UiKit.RoundedLarge; glow.raycastTarget = false;
                var card = Pill($"Club {i}", CardBlue, x, cardY, new Vector2(cardW, cardH), out var fill, 7, false);
                foreach (var img in card.GetComponentsInChildren<Image>()) img.sprite = UiKit.RoundedLarge;
                var picture = new GameObject("Picture").AddComponent<RawImage>();
                picture.transform.SetParent(fill.transform, false);
                var tex = Resources.Load<Texture2D>(Pictures[i]);
                if (tex) tex.wrapMode = TextureWrapMode.Clamp;
                picture.texture = tex; picture.enabled = tex; picture.raycastTarget = false;
                var prt = picture.rectTransform;
                prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 1); prt.pivot = new Vector2(0.5f, 1);
                prt.anchoredPosition = new Vector2(0, -12); prt.sizeDelta = new Vector2(190, 190);
                clubNames[i] = Chunky(fill.transform, "Name", "", 32, 3f);
                var nrt = clubNames[i].rectTransform;
                nrt.anchorMin = new Vector2(0, 0); nrt.anchorMax = new Vector2(1, 0); nrt.pivot = new Vector2(0.5f, 0);
                nrt.offsetMin = new Vector2(0, 22); nrt.offsetMax = new Vector2(0, 108);
                clubNames[i].lineSpacing = 0.9f;
                var star = Chunky(sheet, "Star", "★", 70, 3f, Yellow, Amber);
                star.rectTransform.anchorMin = star.rectTransform.anchorMax = new Vector2(0.5f, 1);
                star.rectTransform.sizeDelta = new Vector2(90, 90); star.rectTransform.anchoredPosition = new Vector2(x - cardW / 2 + 16, -(cardY - cardH / 2) + 6);
                star.rectTransform.localRotation = Quaternion.Euler(0, 0, 12);
                var hold = card.gameObject.AddComponent<HoldButton>();
                var hit = card.gameObject.AddComponent<Image>(); hit.color = Color.clear;
                hold.Fill = fill; hold.RestColor = CardBlue;
                int index = i;
                hold.Pressed = () => OnClub?.Invoke(index);
                Clubs[i] = hold; clubFills[i] = fill; clubGlows[i] = glow; clubStars[i] = star.gameObject;
            }

            // ---- aim: the joystick pad, its power ring, and where the line points
            float aimY = cardY + cardH / 2 + 70;
            var aimHeading = Chunky(sheet, "Aim heading", "AIM", 56, 4f);
            aimHeading.rectTransform.anchorMin = aimHeading.rectTransform.anchorMax = new Vector2(0.5f, 1);
            aimHeading.rectTransform.sizeDelta = new Vector2(300, 80); aimHeading.rectTransform.anchoredPosition = new Vector2(0, -aimY);
            const float D = 360;
            float padY = aimY + 52 + D / 2;
            var pad = Pill("Aim pad", Blue, 0, padY, new Vector2(D, D), out var padFill, 12);
            foreach (var layer in pad.GetComponentsInChildren<Image>()) layer.type = Image.Type.Simple;   // round, not a capsule
            // a gloss across the top of the pad
            Blob(padFill.transform, "Gloss", new Color(1, 1, 1, 0.16f), new Vector2(0, D * 0.18f), new Vector2(D * 0.78f, D * 0.42f));
            ring = UiKit.Panel(pad, "Power", Yellow, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            ring.sprite = UiKit.ThinRing; ring.rectTransform.offsetMin = new Vector2(-18, -18); ring.rectTransform.offsetMax = new Vector2(18, 18);
            ring.type = Image.Type.Filled; ring.fillMethod = Image.FillMethod.Radial360; ring.fillOrigin = (int)Image.Origin360.Top; ring.fillClockwise = true; ring.fillAmount = 0;
            ring.raycastTarget = false;
            HoldButton Arrow(string glyph, Vector2 at, string name)
            {
                var hit = UiKit.Panel(padFill.transform, name, Color.clear, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), at, new Vector2(96, 96));
                hit.sprite = UiKit.Circle; hit.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                var t = UiKit.Label(hit.transform, "Glyph", 40, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, UiKit.Display, false);
                t.text = glyph; t.color = new Color(1, 1, 1, 0.9f); t.raycastTarget = false;
                t.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                var hold = hit.gameObject.AddComponent<HoldButton>();
                hold.Fill = hit; hold.RestColor = Color.clear;
                return hold;
            }
            float r = D / 2 - 58;
            AimLeft = Arrow("◀", new Vector2(-r, 0), "Aim left");
            AimRight = Arrow("▶", new Vector2(r, 0), "Aim right");
            ClubUp = Arrow("▲", new Vector2(0, r), "Club up");
            ClubDown = Arrow("▼", new Vector2(0, -r), "Club down");
            // the knob: a glossy white ball in a soft well
            var well = Blob(padFill.transform, "Well", new Color(0.05f, 0.25f, 0.6f, 0.35f), new Vector2(0, -4), new Vector2(160, 160));
            var knob = Blob(well.transform, "Knob", Color.white, new Vector2(0, 4), new Vector2(146, 146));
            knob.raycastTarget = true;
            Blob(knob.transform, "Shade", UiKit.Hex("D6E6F7"), new Vector2(0, -16), new Vector2(118, 96));
            Blob(knob.transform, "Shine", Color.white, new Vector2(-8, 18), new Vector2(84, 64));
            Knob = knob.gameObject.AddComponent<HoldButton>();
            Knob.Fill = knob; Knob.RestColor = Color.white;
            Stick = knob.gameObject.AddComponent<Joystick>();
            Stick.Radius = r;
            // where the line points, on a pill tucked under the pad
            var pill = Pill("Aim", BlueDeep, 0, padY + D / 2 + 6, new Vector2(300, 84), out var pillFill, 6);
            aimText = Chunky(pillFill.transform, "Label", "STRAIGHT", 38, 3f);
        }

        public void SetHole(int number, int par, string picture)
        {
            holeText.text = $"HOLE {number}";
            parText.text = $"PAR {par}";
        }

        public void SetScore(int toPar, int holeStrokes) { }

        /// How far is left to the pin (the phone HUD's big number).
        public void SetDistance(double amount, string unit) => yardsText.text = $"{amount:F0} {unit.ToUpperInvariant()}";

        /// The big screen: live when the course is on it, a preview in the editor and reviews.
        public void SetScreen(bool live)
        {
            tvText.text = live ? "TV ON" : "PREVIEW";
            tvFill.color = live ? Green : Amber;
        }

        public void SetWind(float relativeDegrees, double mph, bool calm)
        {
            windText.text = calm ? "CALM" : $"{mph:F0} MPH";
            windArrow.gameObject.SetActive(!calm);
            windArrow.localRotation = Quaternion.Euler(0, 0, 90 - relativeDegrees); // the glyph points right at rest
        }

        /// The clubs' full-swing yardages, and which one is in hand.
        public void SetClubs(int selected, string[] yards)
        {
            for (int i = 0; i < Clubs.Length; i++)
            {
                bool on = i == selected;
                Clubs[i].RestColor = clubFills[i].color = on ? Yellow : CardBlue;
                clubGlows[i].gameObject.SetActive(on);
                clubStars[i].SetActive(on);
                var club = GolfArcade.Shot.GolfClubs.All[i];
                string name = club == GolfArcade.Shot.GolfClub.Wedge ? "WEDGE" : GolfArcade.Shot.GolfClubs.DisplayName(club).ToUpperInvariant();
                clubNames[i].text = $"{name}\n<size=26>{yards[i].ToUpperInvariant()}</size>";
                Clubs[i].transform.localScale = Vector3.one * (on ? 1.05f : 1f);
            }
        }

        /// Where the line points, degrees right of straight down the hole.
        public void SetHeading(float relativeDegrees)
        {
            float d = Mathf.Repeat(relativeDegrees + 180f, 360f) - 180f;
            aimText.text = Mathf.Abs(d) < 0.5f ? "STRAIGHT" : $"{Mathf.Abs(d):F0}° {(d > 0 ? "RIGHT" : "LEFT")}";
        }

        /// The backswing: the ring round the pad fills.
        public void SetMeter(float load, Color color) => ring.fillAmount = Mathf.Clamp01(load);

        /// (The TV says what's next; the sheet keeps to the controls.)
        public void SetStatus(string text) { }
    }
}
