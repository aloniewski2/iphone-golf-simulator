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
    /// in a light sheet like a caddie's card. From the top: the hole and whether the TV is
    /// there; par, the distance to the pin and the wind; the live minimap (the game's own map
    /// camera and marks, re-framed wide for the card); the clubs as cards with their pictures
    /// (Resources/Clubs, Higgsfield product shots of the game's clubs) and what each carries;
    /// the aim pad — ticks to turn the line and change club, the knob to nudge, its ring filling
    /// with the backswing; and a line saying what the game wants next.
    public sealed class ControllerSheet
    {
        public HoldButton AimLeft, AimRight, ClubUp, ClubDown, Knob;
        /// The knob's drag: x turns the aim.
        public Joystick Stick;
        public HoldButton[] Clubs;
        public Action<int> OnClub;
        /// Where the minimap goes (Hud moves its RawImage in and back out).
        public RectTransform MapSlot { get; private set; }
        /// The map card's size, for framing the map camera to it.
        public static readonly Vector2 MapSize = new(992, 560);

        // the sheet's own light palette
        static readonly Color Paper = UiKit.Hex("F6F6F1"), CardWhite = UiKit.Hex("FFFFFF"), CardEdge = UiKit.Hex("E3E4DE");
        static readonly Color Ink = UiKit.Hex("141614"), Muted = UiKit.Hex("5E635C"), Rule = UiKit.Hex("E1E2DC");
        static readonly Color Green = UiKit.Hex("2F6B3C"), GreenLight = UiKit.Hex("5E9E5A"), GreenSoft = UiKit.Hex("E7EFE3"), PadFill = UiKit.Hex("EEF0EA");
        static readonly Color Amber = UiKit.Hex("E3A11B");
        const float Margin = 44;
        static readonly string[] Pictures = { "Clubs/driver", "Clubs/iron", "Clubs/wedge", "Clubs/putter" };

        readonly Transform parent;
        RectTransform root, needle;
        Image ring, statusDot, tvDot;
        Text holeText, infoText, windText, aimText, statusTitle, statusDetail, tvText;
        RectTransform windArrow;
        Image[] clubEdges, clubFills, clubChecks; Text[] clubNames, clubYards;
        int par, strokes; string distance = "";

        public ControllerSheet(Transform safeArea)
        {
            parent = safeArea;
            Build();
        }

        public void Destroy() { if (root) UnityEngine.Object.Destroy(root.gameObject); root = null; }
        public bool Alive => root;

        Text T(Transform p, string name, string text, int size, Font face, Color color, TextAnchor anchor, Vector2 anchorAt, Vector2 pos, Vector2 box)
        {
            var t = UiKit.Label(p, name, size, anchor, anchorAt, anchorAt, pos, box, face, false);
            t.text = text; t.color = color; t.raycastTarget = false;
            return t;
        }

        /// A rounded box across the sheet (margins each side), `top` down from the top.
        Image Row(Transform p, string name, Color c, float top, float height)
        {
            var img = UiKit.Panel(p, name, c, new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, Vector2.zero);
            var rt = img.rectTransform;
            rt.pivot = new Vector2(0.5f, 1);
            rt.offsetMin = new Vector2(Margin, -top - height); rt.offsetMax = new Vector2(-Margin, -top);
            return img;
        }

        static Image Dot(Transform p, string name, Color c, Vector2 anchor, Vector2 pos, float size)
        {
            var d = UiKit.Panel(p, name, c, anchor, anchor, pos, new Vector2(size, size));
            d.sprite = UiKit.Circle; d.type = Image.Type.Simple; d.rectTransform.pivot = new Vector2(0.5f, 0.5f); d.raycastTarget = false;
            return d;
        }

        void Build()
        {
            var go = new GameObject("Controller");
            go.transform.SetParent(parent, false);
            root = go.AddComponent<RectTransform>();
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
            // past the safe area to the screen's edges, so the notch and home bar sit on paper
            root.offsetMin = new Vector2(0, -400); root.offsetMax = new Vector2(0, 400);
            var paper = go.AddComponent<Image>(); paper.color = Paper;   // (and it takes the taps meant for nothing)
            var sheet = new GameObject("Sheet").AddComponent<RectTransform>();
            sheet.SetParent(root, false);
            sheet.anchorMin = Vector2.zero; sheet.anchorMax = Vector2.one; sheet.offsetMin = new Vector2(0, 400); sheet.offsetMax = new Vector2(0, -400);

            // ---- the hole, and the TV
            holeText = T(sheet, "Hole", "Hole", 84, UiKit.Display, Ink, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(Margin, -34), new Vector2(560, 100));
            var pill = UiKit.Panel(sheet, "TV", GreenSoft, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-Margin, -40), new Vector2(330, 84));
            pill.rectTransform.pivot = new Vector2(1, 1); pill.raycastTarget = false;
            tvDot = Dot(pill.transform, "Dot", GreenLight, new Vector2(0, 0.5f), new Vector2(40, 0), 24);
            tvText = T(pill.transform, "Label", "TV connected", 34, UiKit.Strong, Ink, TextAnchor.MiddleLeft, new Vector2(0, 0.5f), new Vector2(66, 0), new Vector2(260, 48));
            tvText.rectTransform.pivot = new Vector2(0, 0.5f);
            // a little screen on a stand
            var tv = UiKit.Panel(sheet, "TV icon", Ink, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-Margin - 330 - 34, -58), new Vector2(64, 44), false);
            tv.rectTransform.pivot = new Vector2(1, 1); tv.raycastTarget = false;
            var glass = UiKit.Panel(tv.transform, "Glass", Paper, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false); glass.rectTransform.offsetMin = new Vector2(5, 5); glass.rectTransform.offsetMax = new Vector2(-5, -5); glass.raycastTarget = false;
            var stand = UiKit.Panel(tv.transform, "Stand", Ink, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, -10), new Vector2(28, 5), false);
            stand.rectTransform.pivot = new Vector2(0.5f, 0.5f); stand.raycastTarget = false;
            Row(sheet, "Rule", Rule, 156, 3).raycastTarget = false;

            // ---- par, the distance to the pin, the wind
            infoText = T(sheet, "Info", "", 46, UiKit.Ui, Muted, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(Margin + 8, -222), new Vector2(700, 70));
            infoText.rectTransform.pivot = new Vector2(0, 0.5f);
            windText = T(sheet, "Wind", "", 46, UiKit.Strong, Ink, TextAnchor.MiddleRight, new Vector2(1, 1), new Vector2(-Margin - 8, -222), new Vector2(240, 70));
            windText.rectTransform.pivot = new Vector2(1, 0.5f);
            var arrow = T(sheet, "Arrow", "➤", 50, UiKit.Display, GreenLight, TextAnchor.MiddleCenter, new Vector2(1, 1), new Vector2(-Margin - 180, -222), new Vector2(64, 64));
            arrow.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            windArrow = arrow.rectTransform;

            // ---- the map
            var mapCard = Row(sheet, "Map", UiKit.Hex("7CC4E8"), 290, MapSize.y);
            mapCard.raycastTarget = false;
            var mask = UiKit.Panel(mapCard.transform, "Mask", Color.white, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            mask.sprite = UiKit.RoundedLarge; mask.gameObject.AddComponent<Mask>().showMaskGraphic = false; mask.raycastTarget = false;
            mapCard.sprite = UiKit.RoundedLarge;
            MapSlot = mask.rectTransform;

            // ---- the clubs
            T(sheet, "Clubs heading", "Clubs", 42, UiKit.Strong, Ink, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(Margin + 6, -880), new Vector2(400, 56));
            int n = Pictures.Length;
            Clubs = new HoldButton[n]; clubEdges = new Image[n]; clubFills = new Image[n]; clubChecks = new Image[n]; clubNames = new Text[n]; clubYards = new Text[n];
            var row = new GameObject("Club row").AddComponent<RectTransform>();
            row.SetParent(sheet, false);
            row.anchorMin = new Vector2(0, 1); row.anchorMax = new Vector2(1, 1); row.pivot = new Vector2(0.5f, 1);
            row.offsetMin = new Vector2(Margin, -946 - 340); row.offsetMax = new Vector2(-Margin, -946);
            const float gap = 18;
            for (int i = 0; i < n; i++)
            {
                var edge = UiKit.Panel(row, $"Club {i}", CardEdge, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
                var ert = edge.rectTransform;
                ert.anchorMin = new Vector2(i / (float)n, 0); ert.anchorMax = new Vector2((i + 1) / (float)n, 1);
                ert.offsetMin = new Vector2(i == 0 ? 0 : gap / 2, 0); ert.offsetMax = new Vector2(i == n - 1 ? 0 : -gap / 2, 0);
                var fill = UiKit.Panel(edge.transform, "Fill", CardWhite, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                fill.raycastTarget = false;
                var picture = new GameObject("Picture").AddComponent<RawImage>();
                picture.transform.SetParent(edge.transform, false);
                var tex = Resources.Load<Texture2D>(Pictures[i]);
                if (tex) tex.wrapMode = TextureWrapMode.Clamp;   // (a shaft off one edge mustn't bleed round to the other)
                picture.texture = tex;
                picture.raycastTarget = false;
                var prt = picture.rectTransform;
                prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 1); prt.pivot = new Vector2(0.5f, 1);
                prt.anchoredPosition = new Vector2(0, -18); prt.sizeDelta = new Vector2(196, 196);
                if (!picture.texture) picture.enabled = false;
                clubNames[i] = T(edge.transform, "Name", "", 36, UiKit.Display, Ink, TextAnchor.MiddleCenter, new Vector2(0.5f, 0), new Vector2(0, 66), new Vector2(230, 50));
                clubNames[i].rectTransform.pivot = new Vector2(0.5f, 0);
                clubYards[i] = T(edge.transform, "Yards", "", 30, UiKit.Ui, Muted, TextAnchor.MiddleCenter, new Vector2(0.5f, 0), new Vector2(0, 22), new Vector2(230, 44));
                clubYards[i].rectTransform.pivot = new Vector2(0.5f, 0);
                var check = Dot(edge.transform, "Check", Green, new Vector2(1, 1), new Vector2(-34, -34), 50);
                var tick = T(check.transform, "Tick", "✓", 30, UiKit.Display, Color.white, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(50, 50));
                tick.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                var hold = edge.gameObject.AddComponent<HoldButton>();
                hold.Fill = fill; hold.RestColor = CardWhite;
                int index = i;
                hold.Pressed = () => OnClub?.Invoke(index);
                Clubs[i] = hold; clubEdges[i] = edge; clubFills[i] = fill; clubChecks[i] = check;
            }

            // ---- the aim pad
            var pad = Row(sheet, "Aim", PadFill, 1318, 520);
            pad.sprite = UiKit.RoundedLarge; pad.raycastTarget = false;
            T(pad.transform, "Title", "Aim", 46, UiKit.Display, Ink, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(40, -30), new Vector2(300, 60));
            aimText = T(pad.transform, "Value", "", 46, UiKit.Display, Ink, TextAnchor.UpperRight, new Vector2(1, 1), new Vector2(-40, -30), new Vector2(360, 60));
            aimText.rectTransform.pivot = new Vector2(1, 1);
            const float D = 340;
            var dial = Dot(pad.transform, "Dial", UiKit.Hex("9DB99A"), new Vector2(0.5f, 1), new Vector2(0, -262), D);
            var face = Dot(dial.transform, "Face", UiKit.Hex("F4F6F1"), new Vector2(0.5f, 0.5f), Vector2.zero, D - 6);
            ring = UiKit.Panel(dial.transform, "Power", GreenLight, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            ring.sprite = UiKit.ThinRing; ring.rectTransform.offsetMin = new Vector2(-14, -14); ring.rectTransform.offsetMax = new Vector2(14, 14);
            ring.type = Image.Type.Filled; ring.fillMethod = Image.FillMethod.Radial360; ring.fillOrigin = (int)Image.Origin360.Top; ring.fillClockwise = true; ring.fillAmount = 0;
            ring.raycastTarget = false;
            HoldButton Tick(Vector2 at, bool upright, string name)
            {
                var hit = UiKit.Panel(dial.transform, name, Color.clear, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), at, new Vector2(110, 110));
                hit.sprite = UiKit.Circle; hit.type = Image.Type.Simple; hit.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                var mark = UiKit.Panel(hit.transform, "Mark", UiKit.Hex("6F7A6C"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, upright ? new Vector2(5, 40) : new Vector2(40, 5), false);
                mark.rectTransform.pivot = new Vector2(0.5f, 0.5f); mark.raycastTarget = false;
                var hold = hit.gameObject.AddComponent<HoldButton>();
                hold.Fill = hit; hold.RestColor = Color.clear;
                return hold;
            }
            float r = D / 2 - 42;
            AimLeft = Tick(new Vector2(-r, 0), false, "Aim left");
            AimRight = Tick(new Vector2(r, 0), false, "Aim right");
            ClubUp = Tick(new Vector2(0, r), true, "Club up");
            ClubDown = Tick(new Vector2(0, -r), true, "Club down");
            var halo = Dot(dial.transform, "Halo", UiKit.Hex("DCE3D8"), new Vector2(0.5f, 0.5f), Vector2.zero, 128);
            var knob = Dot(halo.transform, "Knob", Green, new Vector2(0.5f, 0.5f), Vector2.zero, 106);
            knob.raycastTarget = true;
            Knob = halo.gameObject.AddComponent<HoldButton>();
            halo.raycastTarget = true;
            Knob.Fill = knob; Knob.RestColor = Green;
            Stick = halo.gameObject.AddComponent<Joystick>();
            Stick.Radius = r;
            needle = Dot(dial.transform, "Needle", GreenLight, new Vector2(0.5f, 0.5f), Vector2.zero, 20).rectTransform;
            T(pad.transform, "Hint", "Nudge to aim. Release to keep.", 32, UiKit.Body, Muted, TextAnchor.LowerCenter, new Vector2(0.5f, 0), new Vector2(0, 26), new Vector2(800, 44)).rectTransform.pivot = new Vector2(0.5f, 0);

            // ---- what the game wants next
            var status = Row(sheet, "Status", GreenSoft, 1862, 168);
            status.sprite = UiKit.RoundedLarge; status.raycastTarget = false;
            var line = new GameObject("Line").AddComponent<RectTransform>();
            line.SetParent(status.transform, false);
            line.anchorMin = line.anchorMax = new Vector2(0.5f, 1); line.pivot = new Vector2(0.5f, 1);
            line.anchoredPosition = new Vector2(0, -26); line.sizeDelta = new Vector2(900, 60);
            statusDot = Dot(line, "Dot", GreenLight, new Vector2(0.5f, 0.5f), Vector2.zero, 34);
            statusTitle = T(line, "Title", "", 44, UiKit.Display, Ink, TextAnchor.MiddleLeft, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(840, 60));
            statusTitle.rectTransform.pivot = new Vector2(0, 0.5f);
            statusDetail = T(status.transform, "Detail", "", 32, UiKit.Body, Muted, TextAnchor.UpperCenter, new Vector2(0.5f, 1), new Vector2(0, -98), new Vector2(900, 44));
            statusDetail.rectTransform.pivot = new Vector2(0.5f, 1);
        }

        public void SetHole(int number, int par, string picture)
        {
            holeText.text = $"Hole {number}";
            this.par = par;
            Info();
        }

        public void SetScore(int toPar, int holeStrokes) { strokes = holeStrokes; Info(); }

        /// How far is left to the pin (the phone HUD's big number).
        public void SetDistance(double amount, string unit)
        {
            distance = $"{amount:F0} {unit.ToLowerInvariant()}";
            Info();
        }

        void Info()
        {
            infoText.text = $"Par {par}" + (distance.Length > 0 ? $"   ·   {distance}" : "") + (strokes > 0 ? $"   ·   Stroke {strokes + 1}" : "");
        }

        /// The big screen: live when the course is on it, a preview in the editor and reviews.
        public void SetScreen(bool live)
        {
            tvText.text = live ? "TV connected" : "Preview";
            tvDot.color = live ? GreenLight : Amber;
        }

        public void SetWind(float relativeDegrees, double mph, bool calm)
        {
            windText.text = calm ? "Calm" : $"{mph:F0} mph";
            windArrow.gameObject.SetActive(!calm);
            windArrow.anchoredPosition = new Vector2(-Margin - 8 - windText.preferredWidth - 44, windArrow.anchoredPosition.y);
            windArrow.localRotation = Quaternion.Euler(0, 0, 90 - relativeDegrees); // the glyph points right at rest
        }

        /// The clubs' full-swing yardages, and which one is in hand.
        public void SetClubs(int selected, string[] yards)
        {
            for (int i = 0; i < Clubs.Length; i++)
            {
                bool on = i == selected;
                clubEdges[i].color = on ? Green : CardEdge;
                var inset = on ? 5f : 2f;
                clubFills[i].rectTransform.offsetMin = new Vector2(inset, inset); clubFills[i].rectTransform.offsetMax = new Vector2(-inset, -inset);
                Clubs[i].RestColor = clubFills[i].color = on ? GreenSoft : CardWhite;
                clubChecks[i].gameObject.SetActive(on);
                clubNames[i].text = GolfArcade.Shot.GolfClubs.DisplayName(GolfArcade.Shot.GolfClubs.All[i]);
                clubYards[i].text = yards[i];
            }
        }

        /// Where the line points, degrees right of straight down the hole: in words, and the dot on the dial's rim.
        public void SetHeading(float relativeDegrees)
        {
            float d = Mathf.Repeat(relativeDegrees + 180f, 360f) - 180f;
            aimText.text = Mathf.Abs(d) < 0.5f ? "Straight" : $"{Mathf.Abs(d):F0}° {(d > 0 ? "right" : "left")}";
            float a = -d * Mathf.Deg2Rad * 3f;   // (opened up so a few degrees show)
            float rim = 170f - 3f;
            needle.anchoredPosition = new Vector2(-Mathf.Sin(a) * rim, Mathf.Cos(a) * rim);
        }

        public void SetMeter(float load, Color color)
        {
            ring.fillAmount = Mathf.Clamp01(load);
            ring.color = load > 0.01f ? color : GreenLight;
        }

        /// The game's status line, as a heading and a line under it. "Ready" is the swing's cue.
        public void SetStatus(string text)
        {
            text ??= "";
            bool ready = text.StartsWith("Ready");
            string title, detail;
            if (ready) { title = "Ready for your swing"; detail = "Swing your phone to hit the ball."; }
            else
            {
                int cut = text.IndexOf(" — ", StringComparison.Ordinal);
                title = cut > 0 ? text.Substring(0, cut) : text;
                detail = cut > 0 ? char.ToUpperInvariant(text[cut + 3]) + text.Substring(cut + 4) : "";
            }
            statusTitle.text = title;
            statusDetail.text = detail;
            statusDot.color = ready ? GreenLight : Amber;
            statusDot.gameObject.SetActive(title.Length > 0);
            // the dot and the heading centred together
            float w = Mathf.Min(statusTitle.preferredWidth, 800f), total = 34 + 22 + w;
            statusDot.rectTransform.anchoredPosition = new Vector2(-total / 2 + 17, 0);
            statusTitle.rectTransform.anchoredPosition = new Vector2(-total / 2 + 56, 0);
        }
    }
}
