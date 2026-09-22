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

    /// The phone as the controller while the course plays on the big screen: the course view in
    /// the top of the screen with the shot's predicted arc and a target callout on its landing
    /// spot, a hole card and the minimap up top, the wind in a pill, and a sheet below with the
    /// clubs in a row and an aim dial — arrows to turn the line, the power ring filling with the
    /// backswing, the knob to swing when there is no phone-club.
    public sealed class ControllerSheet
    {
        public const float SheetHeight = 960f;
        public HoldButton AimLeft, AimRight, ClubUp, ClubDown, Knob;
        /// The knob's drag: x turns the aim.
        public Joystick Stick;
        public HoldButton[] Clubs;
        public Action<int> OnClub;

        readonly Transform parent;
        RectTransform root, sheet, needle, target;
        Image ring;
        Text holeText, parText, windText, targetText;
        RectTransform windArrow;
        Image[] clubFills; Text[] clubNames, clubYards;

        public ControllerSheet(Transform safeArea)
        {
            parent = safeArea;
            Build();
        }

        public void Destroy() { if (root) UnityEngine.Object.Destroy(root.gameObject); root = null; }
        public bool Alive => root;

        void Build()
        {
            var go = new GameObject("Controller");
            go.transform.SetParent(parent, false);
            root = go.AddComponent<RectTransform>();
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = root.offsetMax = Vector2.zero;

            // ---- hole card, top left: the hole's picture as a round thumb, number and par with the score
            var hole = UiKit.Card(root, "Hole", new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -36), new Vector2(470, 150), new Color(UiKit.Ground.r, UiKit.Ground.g, UiKit.Ground.b, 0.82f));
            hole.rectTransform.pivot = new Vector2(0, 1);
            var thumbFrame = UiKit.Panel(hole.transform, "Thumb frame", UiKit.Hairline, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(20, 0), new Vector2(110, 110));
            thumbFrame.sprite = UiKit.Circle; thumbFrame.type = Image.Type.Simple; thumbFrame.rectTransform.pivot = new Vector2(0, 0.5f); thumbFrame.raycastTarget = false;
            var thumbMask = UiKit.Panel(thumbFrame.transform, "Mask", Color.white, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            thumbMask.sprite = UiKit.Circle; thumbMask.type = Image.Type.Simple; thumbMask.rectTransform.offsetMin = new Vector2(3, 3); thumbMask.rectTransform.offsetMax = new Vector2(-3, -3);
            thumbMask.gameObject.AddComponent<Mask>().showMaskGraphic = false; thumbMask.raycastTarget = false;
            var raw = new GameObject("Picture").AddComponent<RawImage>();
            raw.transform.SetParent(thumbMask.transform, false);
            raw.rectTransform.anchorMin = Vector2.zero; raw.rectTransform.anchorMax = Vector2.one; raw.rectTransform.offsetMin = new Vector2(-30, -20); raw.rectTransform.offsetMax = new Vector2(30, 20);
            raw.raycastTarget = false;
            thumbPicture = raw;
            holeText = UiKit.Label(hole.transform, "Hole", 44, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(152, -26), new Vector2(300, 54), UiKit.Display);
            parText = UiKit.Label(hole.transform, "Par", 30, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(152, -84), new Vector2(300, 40), UiKit.Ui);
            parText.color = UiKit.InkMuted;

            // ---- wind pill, right, just above the sheet
            var wind = UiKit.Card(root, "Wind", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-30, SheetHeight + 30), new Vector2(300, 110), new Color(UiKit.Ground.r, UiKit.Ground.g, UiKit.Ground.b, 0.82f));
            wind.rectTransform.pivot = new Vector2(1, 0);
            var arrow = UiKit.Label(wind.transform, "Arrow", 44, TextAnchor.MiddleCenter, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(64, 0), new Vector2(60, 60), UiKit.Display, false);
            arrow.text = "➤"; arrow.color = UiKit.Accent; arrow.raycastTarget = false; arrow.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            windArrow = arrow.rectTransform;
            windText = UiKit.Label(wind.transform, "Wind", 38, TextAnchor.MiddleLeft, new Vector2(0, 0), new Vector2(1, 1), new Vector2(112, 0), Vector2.zero, UiKit.Display);

            // ---- the target callout, over the landing spot in the course view
            var callout = UiKit.Card(root, "Target", new Vector2(0, 0), new Vector2(0, 0), Vector2.zero, new Vector2(250, 118), new Color(UiKit.Ground.r, UiKit.Ground.g, UiKit.Ground.b, 0.86f));
            callout.rectTransform.pivot = new Vector2(0.5f, 0);
            target = callout.rectTransform;
            var tLabel = UiKit.Label(callout.transform, "Label", 22, TextAnchor.UpperCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -16), new Vector2(240, 30), UiKit.Strong, false);
            tLabel.text = "TARGET"; tLabel.color = UiKit.InkMuted;
            targetText = UiKit.Label(callout.transform, "Yards", 44, TextAnchor.UpperCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -46), new Vector2(240, 56), UiKit.Display, false);
            targetText.color = Game.LandingZone.Amber;
            var pointer = UiKit.Panel(callout.transform, "Pointer", new Color(UiKit.Ground.r, UiKit.Ground.g, UiKit.Ground.b, 0.86f), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, -2), new Vector2(26, 26), rounded: false);
            pointer.rectTransform.pivot = new Vector2(0.5f, 0.5f); pointer.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
            foreach (var g in callout.GetComponentsInChildren<Graphic>()) g.raycastTarget = false;
            target.gameObject.SetActive(false);

            // ---- the sheet
            var sheetImg = UiKit.Panel(root, "Sheet", new Color(UiKit.Ground.r, UiKit.Ground.g, UiKit.Ground.b, 0.97f), new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, -60), new Vector2(0, SheetHeight + 60));
            sheet = sheetImg.rectTransform;
            var lip = UiKit.Panel(sheet, "Lip", UiKit.Hairline, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -2), new Vector2(0, 2), rounded: false);
            lip.raycastTarget = false;

            // clubs in a row
            string[] names = { "DR", "7I", "SW", "PT" };
            Clubs = new HoldButton[4]; clubFills = new Image[4]; clubNames = new Text[4]; clubYards = new Text[4];
            float spacing = 235, span = spacing * 3;
            for (int i = 0; i < 4; i++)
            {
                var card = UiKit.Panel(sheet, $"Club {i}", UiKit.Surface, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-span / 2 + spacing * i, -40), new Vector2(214, 210));
                card.rectTransform.pivot = new Vector2(0.5f, 1);
                var edge = UiKit.Panel(card.transform, "Edge", UiKit.Hairline, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero); edge.raycastTarget = false;
                var fill = UiKit.Panel(card.transform, "Fill", UiKit.Surface, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                fill.rectTransform.offsetMin = new Vector2(3, 3); fill.rectTransform.offsetMax = new Vector2(-3, -3); fill.raycastTarget = false;
                var hold = card.gameObject.AddComponent<HoldButton>();
                hold.Fill = fill; hold.RestColor = UiKit.Surface;
                int index = i;
                hold.Pressed = () => OnClub?.Invoke(index);
                clubNames[i] = UiKit.Label(card.transform, "Name", 46, TextAnchor.UpperCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(200, 56), UiKit.Display, false);
                clubNames[i].text = names[i]; clubNames[i].raycastTarget = false;
                clubYards[i] = UiKit.Label(card.transform, "Yards", 28, TextAnchor.LowerCenter, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 26), new Vector2(200, 36), UiKit.Strong, false);
                clubYards[i].raycastTarget = false;
                Clubs[i] = hold; clubFills[i] = fill;
            }

            // the aim dial
            float dialY = 470;
            var dialOuter = UiKit.Panel(sheet, "Dial", UiKit.Hairline, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, dialY), new Vector2(600, 600));
            dialOuter.sprite = UiKit.Circle; dialOuter.type = Image.Type.Simple; dialOuter.rectTransform.pivot = new Vector2(0.5f, 0.5f); dialOuter.raycastTarget = false;
            var dialInner = UiKit.Panel(dialOuter.transform, "Face", UiKit.Surface, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            dialInner.sprite = UiKit.Circle; dialInner.type = Image.Type.Simple; dialInner.rectTransform.offsetMin = new Vector2(3, 3); dialInner.rectTransform.offsetMax = new Vector2(-3, -3); dialInner.raycastTarget = false;
            ring = UiKit.Panel(dialOuter.transform, "Power", new Color(0.35f, 0.85f, 0.35f, 0.95f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            ring.sprite = UiKit.Ring; ring.rectTransform.offsetMin = new Vector2(-10, -10); ring.rectTransform.offsetMax = new Vector2(10, 10);
            ring.type = Image.Type.Filled; ring.fillMethod = Image.FillMethod.Radial360; ring.fillOrigin = (int)Image.Origin360.Top; ring.fillClockwise = true; ring.fillAmount = 0;
            ring.raycastTarget = false;
            HoldButton Arrow(string glyph, Vector2 at, string name)
            {
                var pad = UiKit.Panel(dialOuter.transform, name, Color.clear, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), at, new Vector2(150, 150));
                pad.sprite = UiKit.Circle; pad.type = Image.Type.Simple; pad.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                var hold = pad.gameObject.AddComponent<HoldButton>();
                hold.Fill = pad; hold.RestColor = Color.clear;
                var t = UiKit.Label(pad.transform, "Glyph", 54, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, UiKit.Display, false);
                t.text = glyph; t.color = UiKit.InkMuted; t.raycastTarget = false;
                return hold;
            }
            AimLeft = Arrow("◀", new Vector2(-200, 0), "Aim left");
            AimRight = Arrow("▶", new Vector2(200, 0), "Aim right");
            ClubUp = Arrow("▲", new Vector2(0, 200), "Club up");
            ClubDown = Arrow("▼", new Vector2(0, -200), "Club down");
            var knob = UiKit.Panel(dialOuter.transform, "Knob", UiKit.Ink, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(220, 220));
            knob.sprite = UiKit.Circle; knob.type = Image.Type.Simple; knob.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            Knob = knob.gameObject.AddComponent<HoldButton>();
            Knob.Fill = knob; Knob.RestColor = UiKit.Ink;
            Stick = knob.gameObject.AddComponent<Joystick>();
            var needleImg = UiKit.Panel(dialOuter.transform, "Needle", UiKit.Accent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(26, 26));
            needleImg.sprite = UiKit.Circle; needleImg.type = Image.Type.Simple; needleImg.rectTransform.pivot = new Vector2(0.5f, 0.5f); needleImg.raycastTarget = false;
            needle = needleImg.rectTransform;
            var aim = UiKit.Label(sheet, "Aim label", 44, TextAnchor.MiddleCenter, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, dialY - 300 - 30), new Vector2(400, 60), UiKit.Display, false);
            aim.text = "AIM"; aim.rectTransform.pivot = new Vector2(0.5f, 0.5f); aim.raycastTarget = false;
        }

        RawImage thumbPicture;

        public void SetHole(int number, int par, string picture)
        {
            holeText.text = $"HOLE {number}";
            parText.text = $"PAR {par}";
            thumbPicture.texture = Resources.Load<Texture2D>(picture);
        }

        public void SetScore(int toPar, int holeStrokes)
        {
            string tp = toPar == 0 ? "E" : (toPar > 0 ? "+" : "") + toPar;
            parText.text = parText.text.Split('·')[0].Trim() + $"   ·   {holeStrokes} {(holeStrokes == 1 ? "stroke" : "strokes")}   ·   {tp}";
        }

        public void SetWind(float relativeDegrees, double mph, bool calm)
        {
            windText.text = calm ? "Calm" : $"{mph:F0} MPH";
            windArrow.gameObject.SetActive(!calm);
            windArrow.localRotation = Quaternion.Euler(0, 0, 90 - relativeDegrees); // the glyph points right at rest
        }

        /// The clubs' full-swing yardages, and which one is in hand.
        public void SetClubs(int selected, string[] yards)
        {
            for (int i = 0; i < Clubs.Length; i++)
            {
                bool on = i == selected;
                Clubs[i].RestColor = clubFills[i].color = on ? UiKit.Ink : UiKit.Surface;
                clubNames[i].color = on ? UiKit.Ground : UiKit.Ink;
                clubYards[i].color = on ? UiKit.AccentStrong : UiKit.InkMuted;
                clubYards[i].text = yards[i];
                Clubs[i].transform.localScale = Vector3.one * (on ? 1.06f : 1f);
            }
        }

        /// Where the line points, degrees right of straight down the hole: the dot rides the ring.
        public void SetHeading(float relativeDegrees)
        {
            float a = -relativeDegrees * Mathf.Deg2Rad;
            needle.anchoredPosition = new Vector2(-Mathf.Sin(a) * 262, Mathf.Cos(a) * 262);
        }

        public void SetMeter(float load, Color color)
        {
            ring.fillAmount = Mathf.Clamp01(load);
            ring.color = color;
        }

        /// The callout over the landing spot, from a world point through `camera`; hidden when
        /// the spot is off the course view or behind.
        public void SetTarget(Camera camera, Vector3 world, string text, bool show)
        {
            if (!show || !camera) { target.gameObject.SetActive(false); return; }
            // Through the camera's viewport (its own rect may be the top of the screen), then
            // onto the safe area's rect, so it lands the same in the editor's captures and on a phone.
            var vp = camera.WorldToViewportPoint(world);
            if (vp.z <= 0 || vp.y < 0.04f || vp.y > 0.98f || vp.x < 0 || vp.x > 1) { target.gameObject.SetActive(false); return; }
            // Anchored by fraction of the screen, so it lands the same whatever the canvas is sized.
            float sx = Mathf.Clamp(camera.rect.x + vp.x * camera.rect.width, 0.13f, 0.87f), sy = camera.rect.y + vp.y * camera.rect.height;
            target.anchorMin = target.anchorMax = new Vector2(sx, sy);
            target.anchoredPosition = new Vector2(0, 28);
            targetText.text = text;
            target.gameObject.SetActive(true);
        }
    }
}
