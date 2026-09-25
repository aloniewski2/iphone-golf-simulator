using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GolfArcade.UI
{
    /// A press-and-hold button: aiming sweeps while the thumb is down, like holding the Wii
    /// remote's D-pad.
    public sealed class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public bool IsHeld { get; private set; }
        public Action Pressed, Released;
        /// The button's fill, lit while held.
        public Image Fill;
        public Color RestColor = UiKit.ButtonFill;
        public void OnPointerDown(PointerEventData e) { IsHeld = true; Light(true); Pressed?.Invoke(); }
        public void OnPointerUp(PointerEventData e) { if (IsHeld) Released?.Invoke(); IsHeld = false; Light(false); }
        public void OnPointerExit(PointerEventData e) { if (IsHeld) Released?.Invoke(); IsHeld = false; Light(false); }
        void Light(bool on) { if (Fill) Fill.color = on ? UiKit.ButtonPressed : RestColor; }
    }

    /// The whole on-screen layer, built in code so there is nothing to wire in a scene: hole and
    /// score, distance and club, the hole's wind as an arrow relative to the aim, a Wii-style
    /// power meter that fills with the backswing, aim and club buttons, a banner for results, a
    /// minimap, and the scorecard at the end of the round. Everything sits inside the phone's
    /// safe area so the notch and the home indicator never cover it.
    public sealed class Hud : MonoBehaviour
    {
        public HoldButton AimLeft, AimRight, ClubUp, ClubDown, SwingHold, PlayAgain;
        public RawImage Minimap;

        RectTransform safeArea;
        Rect appliedSafeArea;
        Text statusText, bannerText, tempoText, controllerText;
        YardageCard yardage;
        Image meterGauge;
        RectTransform minimapHolder;
        const float Margin = 36f;
        // The power arc: a stretch of a circle whose centre is out in the picture, so the arc bows
        // along the left edge from low (empty) to high (full). Positions are relative to the
        // meter's anchor, the left edge half-way up.
        const float ArcRadius = 520f, ArcStart = 235f, ArcSweep = 110f, ArcBand = 34f;
        static readonly Vector2 ArcCentre = new(532f, -90f);
        const int ArcSegments = 48;
        /// The middle of the arc's band at `load` (0 empty → 1 full), pushed `inward` toward the centre.
        static Vector2 ArcPoint(float load, float inward = 0)
        {
            float a = (ArcStart - Mathf.Clamp01(load) * ArcSweep) * Mathf.Deg2Rad;
            float r = ArcRadius - ArcBand / 2 - inward;
            return ArcCentre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
        }
        static Color ArcColor(float t)
        {
            var calm = new Color(0.3f, 0.85f, 0.35f); var warm = new Color(1f, 0.86f, 0.2f); var hot = new Color(1f, 0.42f, 0.15f);
            return t < 0.55f ? Color.Lerp(calm, warm, t / 0.55f) : Color.Lerp(warm, hot, (t - 0.55f) / 0.45f);
        }
        readonly System.Collections.Generic.List<Image> arcSegments = new();
        Image arcTip, arcStartCap;
        RectTransform gaugeRoot;
        static readonly Color LandingZoneAmber = new(1f, 0.72f, 0.25f, 0.97f);

        /// A pill: the circle sprite sliced, so the ends stay round at any length.
        Image Capsule(Transform parent, string name, Color color, Vector2 pos, Vector2 size)
        {
            var img = UiKit.Panel(parent, name, color, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size, false);
            img.sprite = UiKit.Circle; img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 1f; img.raycastTarget = false;
            return img;
        }
        Text meterYards;
        Transform meterTrack;
        Image meterFill, meterMark;
        RectTransform meterRect;
        Vector2 meterHome;
        float meterLoad;
        CanvasGroup bannerGroup;
        RectTransform board, shotCard;
        float bannerUntil;
        StrikePopup strikePopup;
        PinLocator pinLocator;
        TvMap tvMap;
        FaceDial faceDial;
        RectTransform replayBadge;

        /// The minimap's picture in the corner, and in the controller's map card.
        public static readonly Vector2 MinimapSize = new(260, 420), ControllerMapSize = ControllerSheet.MapSize;
        /// The phone-as-controller layout while the course is on the big screen; null otherwise.
        public ControllerSheet Controller { get; private set; }
        public bool AimLeftHeld => AimLeft.IsHeld || (Controller != null && Controller.AimLeft.IsHeld);
        public bool AimRightHeld => AimRight.IsHeld || (Controller != null && Controller.AimRight.IsHeld);
        /// The joystick's push, -1..1 right, when the controller layout is up (a little play in
        /// the middle, so pushing it up to look doesn't also turn the aim).
        public float AimStick => Controller != null && Controller.Stick ? DeadZone(Controller.Stick.Value.x) : 0f;
        /// The joystick pushed up (1) or down (-1): looking up the hole, or down at the ball.
        public float LookStick => Controller != null && Controller.Stick ? DeadZone(Controller.Stick.Value.y) : 0f;
        static float DeadZone(float v) => Mathf.Sign(v) * Mathf.Max(0f, Mathf.Abs(v) - 0.2f) / 0.8f;

        public static Hud Create()
        {
            var go = new GameObject("HUD");
            var hud = go.AddComponent<Hud>();
            hud.Build();
            return hud;
        }

        void Build()
        {
            UiKit.Canvas(gameObject);

            // Every element hangs off this, and it shrinks to the phone's safe area.
            safeArea = new GameObject("Safe area").AddComponent<RectTransform>();
            safeArea.SetParent(transform, false);
            safeArea.anchorMin = Vector2.zero; safeArea.anchorMax = Vector2.one;
            safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
            ApplySafeArea();

            // The play HUD, dressed like a tournament broadcast: top left the scoreboard (you and
            // par, hole by hole), under it the distance card (a big yardage, the club in a yellow
            // pill, the wind) in the same cobalt with a white rim; top right the minimap; down the
            // left edge the power meter.
            board = new GameObject("Scoreboard").AddComponent<RectTransform>();
            board.SetParent(safeArea, false);
            board.anchorMin = board.anchorMax = board.pivot = new Vector2(0, 1);
            board.anchoredPosition = new Vector2(Margin, -Margin); board.sizeDelta = new Vector2(420, BoardHeight);

            // The yardage card (the user's pick, option A): the distance, the club, how it plays,
            // the wind, the lie.
            yardage = new YardageCard(safeArea, new Vector2(Margin, -Margin - BoardHeight - 22), ArrowSprite());
            shotCard = yardage.Root;

            statusText = Label("Status", 38, TextAnchor.MiddleCenter, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 420), new Vector2(1000, 60));
            statusText.color = new Color(1, 1, 1, 0.9f);
            tempoText = Label("Tempo", 30, TextAnchor.MiddleCenter, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 362), new Vector2(1000, 50));
            tempoText.color = new Color(1, 0.95f, 0.7f, 0.95f);

            // The power meter: a chunky arc bowing down the left edge (the Everybody's Golf gauge):
            // white-rimmed navy, filling from its low end green → yellow → orange as the
            // backswing grows. The numbered checkpoints sit on the arc (SetCheckpoints), and a
            // bouncy yellow tag rides the tip of the fill with the yards this load carries.
            var meter = new GameObject("Meter").AddComponent<RectTransform>();
            meter.SetParent(safeArea, false);
            meter.anchorMin = meter.anchorMax = new Vector2(0, 0.5f); meter.pivot = new Vector2(0, 0.5f);
            meter.anchoredPosition = new Vector2(Margin - 6, -40); meter.sizeDelta = Vector2.zero;
            meterRect = meter;
            meterHome = meterPhoneHome = meterRect.anchoredPosition;
            meterTrack = meter;
            Image Ring(string name, Color c, float radius, float band)
            {
                int px = 1024, b = Mathf.Max(2, Mathf.RoundToInt(band / (2 * radius) * px));
                var img = UiKit.Panel(meter, name, c, new Vector2(0, 0.5f), new Vector2(0, 0.5f), ArcCentre, new Vector2(2 * radius, 2 * radius), false);
                img.sprite = UiKit.RingOf(px, b); img.type = Image.Type.Filled; img.fillMethod = Image.FillMethod.Radial360;
                img.fillOrigin = (int)Image.Origin360.Top; img.fillClockwise = true; img.fillAmount = ArcSweep / 360f;
                img.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                img.rectTransform.localRotation = Quaternion.Euler(0, 0, ArcStart - 90f);
                img.raycastTarget = false;
                return img;
            }
            Image Cap(string name, Color c, Vector2 at, float size)
            {
                var d = UiKit.Panel(meter, name, c, new Vector2(0, 0.5f), new Vector2(0, 0.5f), at, new Vector2(size, size));
                d.sprite = UiKit.Circle; d.type = Image.Type.Simple; d.rectTransform.pivot = new Vector2(0.5f, 0.5f); d.raycastTarget = false;
                return d;
            }
            var shadow = Ring("Shadow", new Color(0.03f, 0.08f, 0.25f, 0.28f), ArcRadius + 16, ArcBand + 30);
            shadow.rectTransform.anchoredPosition += new Vector2(4, -8);
            Ring("Rim", UiKit.ArcadeRim, ArcRadius + 16, ArcBand + 32);
            foreach (float end in new[] { 0f, 1f }) Cap("Rim cap", UiKit.ArcadeRim, ArcPoint(end), ArcBand + 32);
            Ring("Track", UiKit.ArcadeInk, ArcRadius + 6, ArcBand + 12);
            foreach (float end in new[] { 0f, 1f }) Cap("Track cap", UiKit.ArcadeInk, ArcPoint(end), ArcBand + 12);
            for (int i = 0; i < ArcSegments; i++)
            {
                var seg = Ring($"Fill {i}", ArcColor((i + 0.5f) / ArcSegments), ArcRadius, ArcBand);
                seg.rectTransform.localRotation = Quaternion.Euler(0, 0, ArcStart - i * ArcSweep / ArcSegments - 90f);
                seg.fillAmount = ArcSweep / ArcSegments / 360f + 0.0015f;
                seg.enabled = false;
                arcSegments.Add(seg);
            }
            arcStartCap = Cap("Fill start", ArcColor(0), ArcPoint(0), ArcBand);
            arcStartCap.enabled = false;
            arcTip = Cap("Fill tip", ArcColor(0), ArcPoint(0), ArcBand);
            arcTip.enabled = false;
            meterFill = arcTip;
            meterMark = Cap("Mark", Color.white, ArcPoint(0), 16);
            meterMark.enabled = false;
            var meterLabel = Label("Power", 22, TextAnchor.MiddleCenter, new Vector2(0, 0.5f), new Vector2(0, 0.5f), ArcPoint(0) + new Vector2(18, -58), new Vector2(160, 32), meter);
            meterLabel.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            meterLabel.text = "POWER"; meterLabel.font = UiKit.Display; meterLabel.color = Color.white;
            // the live gauge: how far this much backswing carries, a tilted yellow tag at the tip
            gaugeRoot = UiKit.Pill(meter, "Gauge", UiKit.ArcadeYellow, new Vector2(0, 0.5f), Vector2.zero, new Vector2(150, 58), out meterGauge, 5f);
            gaugeRoot.localRotation = Quaternion.Euler(0, 0, 12f);
            meterYards = UiKit.Label(meterGauge.transform, "Yards", 32, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, UiKit.Display, false);
            meterYards.color = UiKit.ArcadeInk; meterYards.raycastTarget = false;
            gaugeRoot.gameObject.SetActive(false);

            // Minimap, top right under the score: the course from above with rounded corners, a
            // crisp light edge and a soft shadow, no frame of its own.
            var mapShadow = Panel("Minimap shadow", new Color(0.03f, 0.08f, 0.25f, 0.3f), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-Margin + 4, -Margin - 8), new Vector2(272, 432));
            mapShadow.rectTransform.pivot = new Vector2(1, 1); mapShadow.raycastTarget = false;
            var mapFrame = Panel("Minimap frame", UiKit.ArcadeRim, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-Margin, -Margin), new Vector2(272, 432));
            mapFrame.rectTransform.pivot = new Vector2(1, 1);
            mapFrame.transform.SetParent(mapShadow.transform, true);   // they move together (the controller layout moves the frame's parent)
            var mapMask = Panel("Mask", Color.white, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, mapFrame.transform);
            mapMask.rectTransform.offsetMin = new Vector2(6, 6); mapMask.rectTransform.offsetMax = new Vector2(-6, -6);
            mapMask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var mapGo = new GameObject("Minimap");
            mapGo.transform.SetParent(mapMask.transform, false);
            Minimap = mapGo.AddComponent<RawImage>();
            var mapRt = Minimap.rectTransform;
            mapRt.anchorMin = Vector2.zero; mapRt.anchorMax = Vector2.one;
            mapRt.offsetMin = mapRt.offsetMax = Vector2.zero;
            Minimap.color = Color.white;
            minimapHolder = mapShadow.rectTransform;

            // Buttons along the bottom.
            AimLeft = Button("◀", new Vector2(0, 0), new Vector2(150, 170), new Vector2(240, 240));
            AimRight = Button("▶", new Vector2(1, 0), new Vector2(-150, 170), new Vector2(240, 240));
            ClubUp = Button("▲", new Vector2(0.5f, 0), new Vector2(-95, 200), new Vector2(150, 110));
            ClubDown = Button("▼", new Vector2(0.5f, 0), new Vector2(95, 200), new Vector2(150, 110));
            SwingHold = Button("HOLD TO SWING", new Vector2(0.5f, 0), new Vector2(0, 90), new Vector2(360, 90), 30);

            // Who you play as: body and skin tone, under the minimap. Cycles on a tap.

            // How to pair a phone as the club (desktop only), along the bottom edge.
            controllerText = Label("Controller", 28, TextAnchor.LowerCenter, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 18), new Vector2(1040, 40));
            controllerText.color = new Color(1, 1, 1, 0.75f);
            controllerText.text = "";

            // Banner in the middle.
            var bannerGo = Card("Banner", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 250), new Vector2(900, 140));
            bannerGroup = bannerGo.gameObject.AddComponent<CanvasGroup>();
            bannerGroup.alpha = 0;
            bannerText = Label("BannerText", 56, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(880, 130), bannerGo.transform);

            // The strike's verdict, the face while you swing, the replay bug.
            strikePopup = StrikePopup.Create(safeArea);
            landingBadge = LandingBadge.Create(safeArea);
            faceDial = FaceDial.Create(safeArea, 10f, 24f);
            replayBadge = ReplayBadge.Create(safeArea);
            pinLocator = PinLocator.Create(transform);
            swingCard = SwingCard.Create(safeArea, Margin);
            tvMap = TvMap.Create(safeArea, Margin);
            ScaleHud(PhoneHudScale);
        }

        /// On the phone the play HUD is drawn smaller than on the big screen: there the picture is
        /// the course, and the cards keep to its corners. (The menus and the round's card are
        /// whole screens and keep their size.)
        const float PhoneHudScale = 0.7f;
        float hudScale = 1f;

        /// Sizes the play HUD: the scoreboard and the yardage card under it, the swing card, the
        /// minimap and the targets at `s`; the meter, the face dial and the buttons, which want
        /// to stay big enough to read and press, part of the way.
        void ScaleHud(float s)
        {
            hudScale = s;
            float held = Mathf.Lerp(1f, s, 0.6f);
            board.localScale = Vector3.one * s;
            shotCard.localScale = Vector3.one * s;
            shotCard.anchoredPosition = new Vector2(Margin, -Margin - (BoardHeight + 22) * s);
            swingCard.transform.localScale = Vector3.one * s;
            minimapHolder.localScale = Vector3.one * s;
            meterRect.localScale = Vector3.one * held;
            faceDial.transform.localScale = Vector3.one * held;
            strikePopup.Size = held;
            landingBadge.Size = Mathf.Lerp(1f, s, 0.5f);
            bannerGroup.transform.localScale = Vector3.one * held;
            foreach (var b in new[] { AimLeft, AimRight, ClubUp, ClubDown, SwingHold }) b.transform.localScale = Vector3.one * held;
        }

        /// Fits the HUD to the phone's safe area (notch at the top, home indicator at the bottom).
        void ApplySafeArea()
        {
            if (onTv) return;   // (the TV has no notch)
            var area = Screen.safeArea;
            if (area == appliedSafeArea || Screen.width == 0 || Screen.height == 0) return;
            appliedSafeArea = area;
            safeArea.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            safeArea.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
        }

        /// Wind for the shot being set up: `relativeDegrees` is where it blows toward, right of
        /// the aim line, so straight up the screen is a helping wind.
        public void SetWind(float relativeDegrees, string label, bool calm, double mph = 0)
        {
            if (calm) yardage.SetRow(1, "wind", "Wind", "Calm");
            else yardage.SetRow(1, "wind", "Wind", $"{mph:F0} mph", null, relativeDegrees);
            lastWind = (true, relativeDegrees, mph, calm);
            Controller?.SetWind(relativeDegrees, mph, calm);
        }

        /// Lays out the round's card: a row per hole, the totals, and a button to go again.
        RoundCard roundCard;
        /// Pressed on the card after a hole: the same holes again, back to the menu, or on to
        /// the next hole (null on the round's last).
        public HoldButton RoundMenu, NextHole;

        /// The card after a hole or the round (option A): `title` over it, `headline` on its
        /// ribbon (null: the round against par), the round's stats in tiles, and NEXT HOLE when
        /// `nextHole`.
        public void ShowScorecard(GolfArcade.Course.Scorecard card, string title, string headline, RoundCard.Highlight[] highlights, bool nextHole, RoundCard.Row[] players = null)
        {
            HideScorecard();
            var holes = card.Course.Holes;
            var numbers = new int[holes.Length]; var pars = new int[holes.Length]; var strokes = new int?[holes.Length];
            for (int i = 0; i < holes.Length; i++) { numbers[i] = holes[i].Number; pars[i] = holes[i].Par; strokes[i] = card.StrokesOn(i); }
            roundCard = new RoundCard(safeArea, title, headline, numbers, pars, strokes, card.Total, card.ToPar, highlights, nextHole, players);
            roundCard.Root.SetAsLastSibling();
            PlayAgain = roundCard.PlayAgain; RoundMenu = roundCard.Menu; NextHole = roundCard.NextHole;
        }

        public void HideScorecard() { roundCard?.Destroy(); roundCard = null; }

        HomeScreen home;
        CourseScreen courses;

        /// The first screen (UI/HomeScreen.cs): the logo, the big screen, and PLAY between
        /// COURSE and GOLFER, over the golfer on the first tee.
        public HomeScreen ShowMenu()
        {
            HideMenu();
            home = new HomeScreen(safeArea);
            return home;
        }

        /// The course screen (UI/HomeScreen.cs), a dot for each of `holes`, over the hole the
        /// camera circles.
        public CourseScreen ShowCourses(int holes)
        {
            HideCourses();
            courses = new CourseScreen(safeArea, holes);
            return courses;
        }

        public void HideCourses() { courses?.Destroy(); courses = null; }

        GolferSelect golferSelect;

        /// The golfer select screen (UI/GolferSelect.cs) over the golfer on the tee.
        public GolferSelect ShowGolferSelect(string[] kitNames, Color[] kitColors, string[] shirtNames, Color[] shirtColors)
        {
            HideGolferSelect();
            golferSelect = new GolferSelect(safeArea, kitNames, kitColors, shirtNames, shirtColors);
            return golferSelect;
        }

        public void HideGolferSelect() { golferSelect?.Destroy(); golferSelect = null; }

        /// The title over the hole's flyover, the way the tennis broadcast opens on its island:
        /// the tournament's name in chunky yellow on a cobalt badge with a white rim, and under it
        /// a yellow tab with the hole — number, name, par, yards — and a small one with today's
        /// wind. No letterbox: the whole picture is the course. It pops in with the flyover.
        CanvasGroup introGroup;
        RectTransform introBadge;

        public void ShowHoleIntro(string tournament, int number, string name, int par, double yards, string wind)
        {
            HideHoleIntro();
            var go = new GameObject("Hole intro");
            go.transform.SetParent(safeArea, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            introGroup = go.AddComponent<CanvasGroup>();
            introGroup.alpha = 0; introGroup.blocksRaycasts = false; introGroup.interactable = false;

            introBadge = new GameObject("Badge").AddComponent<RectTransform>();
            introBadge.SetParent(go.transform, false);
            introBadge.anchorMin = introBadge.anchorMax = new Vector2(0.5f, 0.66f);
            introBadge.sizeDelta = new Vector2(900, 330);
            var title = UiKit.Label(introBadge, "Measure", 100, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, UiKit.Display, false);
            title.text = tournament.ToUpperInvariant();
            float w = Mathf.Min(1000, title.preferredWidth + 110);
            Destroy(title.gameObject);
            UiKit.Pill(introBadge, "Title", UiKit.ArcadeBlue, new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(w, 168), out var titleFill, 8f);
            // a lighter band across the top half, the badge's shine
            var shine = UiKit.Panel(titleFill.transform, "Shine", new Color(1, 1, 1, 0.14f), new Vector2(0, 0.5f), new Vector2(1, 1), Vector2.zero, Vector2.zero);
            shine.sprite = UiKit.Circle; shine.rectTransform.offsetMin = new Vector2(22, 0); shine.rectTransform.offsetMax = new Vector2(-22, -10); shine.raycastTarget = false;
            var big = UiKit.Chunky(titleFill.transform, "Tournament", 96, UiKit.ArcadeYellow, UiKit.ArcadeInk, 5f);
            big.text = tournament.ToUpperInvariant();
            big.rectTransform.offsetMin = new Vector2(0, 6); big.rectTransform.offsetMax = new Vector2(0, 6);

            var hole = UiKit.Label(introBadge, "Measure", 30, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, UiKit.Display, false);
            hole.text = $"HOLE {number}   ·   {name.ToUpperInvariant()}   ·   PAR {par}   ·   {yards:F0} YD";
            float hw = hole.preferredWidth + 60;
            Destroy(hole.gameObject);
            UiKit.Pill(introBadge, "Hole", UiKit.ArcadeYellow, new Vector2(0.5f, 0.5f), new Vector2(0, -52), new Vector2(hw, 62), out var holeFill, 4f);
            var holeLine = UiKit.Label(holeFill.transform, "Text", 30, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, UiKit.Display, false);
            holeLine.text = $"HOLE {number}   ·   {name.ToUpperInvariant()}   ·   PAR {par}   ·   {yards:F0} YD"; holeLine.color = UiKit.ArcadeInk;

            var windMeasure = UiKit.Label(introBadge, "Measure", 25, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, UiKit.Strong, false);
            windMeasure.text = wind.ToUpperInvariant();
            float ww = windMeasure.preferredWidth + 48;
            Destroy(windMeasure.gameObject);
            UiKit.Pill(introBadge, "Wind", UiKit.ArcadeBlueDeep, new Vector2(0.5f, 0.5f), new Vector2(0, -120), new Vector2(ww, 46), out var windFill, 3f);
            var windLine = UiKit.Label(windFill.transform, "Text", 25, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, UiKit.Strong, false);
            windLine.text = wind.ToUpperInvariant(); windLine.color = Color.white;
        }

        /// Fades the title in or out; on the way in it pops, a little past full size and back.
        public void SetHoleIntroAlpha(float alpha)
        {
            if (!introGroup) return;
            introGroup.alpha = alpha;
            if (introBadge) introBadge.localScale = Vector3.one * Pop(alpha);
        }

        /// An arcade pop: from 0.6 up past 1 and settling back, as `t` runs 0 → 1.
        static float Pop(float t)
        {
            t = Mathf.Clamp01(t);
            const float c = 1.9f;
            float u = t - 1;
            return 0.6f + 0.4f * (1 + (c + 1) * u * u * u + c * u * u);
        }

        /// The tournament title is on screen.
        public bool HoleIntroShowing => introGroup && introGroup.alpha > 0.01f;

        public void HideHoleIntro()
        {
            if (introGroup) Destroy(introGroup.gameObject);
            introGroup = null; introBadge = null;
        }

        /// The player's nameplate for the introductions on the tee: a chunky yellow plate with the
        /// name and a small blue tab over it, low on the screen like the broadcast's.
        CanvasGroup nameplate;
        RectTransform nameplateBody;

        public void ShowNameplate(string name, string tab)
        {
            HideNameplate();
            var go = new GameObject("Nameplate");
            go.transform.SetParent(safeArea, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0); rt.sizeDelta = new Vector2(600, 220);
            rt.anchoredPosition = new Vector2(0, 360);
            nameplate = go.AddComponent<CanvasGroup>();
            nameplate.alpha = 0; nameplate.blocksRaycasts = false; nameplate.interactable = false;
            nameplateBody = rt;
            UiKit.Pill(rt, "Plate", UiKit.ArcadeYellow, new Vector2(0.5f, 0.5f), new Vector2(0, -12), new Vector2(360, 118), out var fill, 7f);
            var t = UiKit.Chunky(fill.transform, "Name", 76, Color.white, UiKit.ArcadeYellowDeep, 4f);
            t.text = name.ToUpperInvariant();
            t.rectTransform.offsetMin = new Vector2(0, 4); t.rectTransform.offsetMax = new Vector2(0, 4);
            var measure = UiKit.Label(rt, "Measure", 24, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, UiKit.Display, false);
            measure.text = tab.ToUpperInvariant();
            float tw = measure.preferredWidth + 40;
            Destroy(measure.gameObject);
            UiKit.Pill(rt, "Tab", UiKit.ArcadeBlueDeep, new Vector2(0.5f, 0.5f), new Vector2(0, 66), new Vector2(tw, 42), out var tabFill, 3f);
            var tl = UiKit.Label(tabFill.transform, "Text", 24, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, UiKit.Display, false);
            tl.text = tab.ToUpperInvariant(); tl.color = UiKit.ArcadeYellow;
        }

        public void SetNameplateAlpha(float alpha)
        {
            if (!nameplate) return;
            nameplate.alpha = alpha;
            nameplateBody.localScale = Vector3.one * Pop(alpha);
        }

        public void HideNameplate()
        {
            if (nameplate) Destroy(nameplate.gameObject);
            nameplate = null; nameplateBody = null;
        }

        public void HideMenu() { home?.Destroy(); home = null; }

        /// Show or hide everything but the menu (the cards, meter and map stay out of the way).
        bool playHud = true;
        public void ShowPlayHud(bool on)
        {
            playHud = on;
            foreach (Transform child in safeArea)
                if (child.name != "Menu" && child.name != "Course select" && child.name != "Golfer select" && child.name != "Scorecard" && child.name != "Landing badge" && child.name != "Banner" && child.name != "Hole intro" && child.name != "Nameplate" && child.name != "Swing card"
                    && child.name != "Replay badge" && child.name != "Face dial" && child.name != "TV map" && !(onTv && child == minimapHolder)) child.gameObject.SetActive(on);
            tvMap.gameObject.SetActive(on && onTv && !flightMode);
        }

        // ---- The minimap, Wii Golf style: where the shot can go before you hit it, and where it
        // went after. While aiming: the club's reach as an arc (as far as a full swing with it
        // flies, whichever way you turn), dots along the full swing's flight, and the zone it
        // comes down in — shaded amber, the same zone as the ring on the course — with an amber
        // dot sliding out along the dots as the backswing loads. Once it is struck: the ball's
        // own path traced in the shot's colour as it flies, left there until the next shot.
        // Drawn as UI over the map so it stays crisp, placed by the map camera's viewport.

        /// What the minimap shows beyond the course, set by the game. Call `Changed` after
        /// altering the plan so the shapes are redrawn.
        public sealed class MapPlan
        {
            /// The full swing's flight while aiming (dots), the club's reach (an arc of dots),
            /// and the ball's own flight once struck (a line).
            public readonly System.Collections.Generic.List<Vector3> Path = new(), Reach = new(), Trace = new();
            public Color TraceColor = Color.white;
            /// Where a slightly-off full swing still comes down: an ellipse `ZoneAcross` yards
            /// either side of the line and `ZoneAlong` short and long, on the aim `ZoneHeading`.
            public Vector3 ZoneCentre; public float ZoneAcross, ZoneAlong, ZoneHeading; public bool ShowZone;
            public Vector3 Ball, Landing, Load, Pin;
            public bool ShowBall, ShowLanding, ShowLoad;
            /// The meter's checkpoint targets, where each comes down (empty hides them).
            public readonly System.Collections.Generic.List<Vector3> Targets = new();
            internal int Version;
            public void Changed() => Version++;
        }
        public MapPlan Map { get; } = new();

        RectTransform mapPin, mapBall, mapLanding, mapLoad, mapShapes;
        Image mapZoneFill, mapZoneEdge;
        readonly System.Collections.Generic.List<RectTransform> mapDots = new(), mapReach = new(), mapTargets = new();
        readonly System.Collections.Generic.List<Image> mapTraceUnder = new(), mapTraceLine = new();
        int mapDrawn = -1;
        Camera mapDrawnWith;
        const int MapDots = 16, ReachDots = 17;
        static readonly Color MapInk = new(0.06f, 0.1f, 0.18f, 0.9f);

        RectTransform MapMark(Transform parent, string name, Color color, float size, Sprite sprite, bool ringed)
        {
            var img = UiKit.Panel(parent, name, color, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
            img.sprite = sprite; img.type = Image.Type.Simple; img.rectTransform.pivot = new Vector2(0.5f, 0.5f); img.raycastTarget = false;
            if (ringed)
            {
                var edge = UiKit.Panel(img.transform, "Edge", MapInk, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                edge.sprite = UiKit.Ring; edge.type = Image.Type.Simple; edge.rectTransform.offsetMin = new Vector2(-4, -4); edge.rectTransform.offsetMax = new Vector2(4, 4); edge.raycastTarget = false;
            }
            img.gameObject.SetActive(false);
            return img.rectTransform;
        }

        void BuildMinimapMarks()
        {
            var map = Minimap.rectTransform;
            // bottom to top: the zone, the reach, the trace, the flight's dots, the marks
            var holder = UiKit.Panel(map, "Shot shapes", Color.clear, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false);
            holder.raycastTarget = false;
            mapShapes = holder.rectTransform;
            mapShapes.offsetMin = mapShapes.offsetMax = Vector2.zero; mapShapes.pivot = new Vector2(0.5f, 0.5f);
            var amber = Game.LandingZone.Amber;
            mapZoneFill = MapMark(mapShapes, "Zone", new Color(amber.r, amber.g, amber.b, 0.34f), 10, UiKit.Circle, false).GetComponent<Image>();
            mapZoneEdge = MapMark(mapShapes, "Zone edge", new Color(amber.r, amber.g, amber.b, 0.95f), 10, UiKit.Ring, false).GetComponent<Image>();
            for (int i = 0; i < ReachDots; i++) mapReach.Add(MapMark(mapShapes, $"Reach {i}", new Color(1, 1, 1, 0.8f), 7, UiKit.Circle, false));
            for (int i = 0; i < MapDots; i++) mapDots.Add(MapMark(map, $"Path {i}", new Color(1, 1, 1, 0.95f), 10, UiKit.Circle, false));
            mapLanding = MapMark(map, "Landing", amber, 24, UiKit.Ring, false);
            for (int i = 0; i < 3; i++) mapTargets.Add(MapMark(map, $"Target {i + 1}", Color.white, 13, UiKit.Circle, true));
            mapLoad = MapMark(map, "Load", amber, 18, UiKit.Circle, true);
            mapPin = MapMark(map, "Pin", UiKit.Hex("E8352F"), 20, UiKit.Circle, true);
            mapBall = MapMark(map, "Ball", Color.white, 22, UiKit.Circle, true);
        }

        /// A world point in the map's own rect coordinates (its centre is 0,0).
        Vector2 MapPoint(Camera map, Vector3 world)
        {
            var r = Minimap.rectTransform.rect;
            var vp = map.WorldToViewportPoint(world);
            return new Vector2(r.xMin + vp.x * r.width, r.yMin + vp.y * r.height);
        }

        /// One stretch of the trace: a thin bar from `a` to `b`.
        Image TraceBar(System.Collections.Generic.List<Image> pool, int i, Color c, string name)
        {
            while (pool.Count <= i)
            {
                var bar = UiKit.Panel(mapShapes, name, c, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one, false);
                bar.rectTransform.pivot = new Vector2(0.5f, 0.5f); bar.raycastTarget = false;
                pool.Add(bar);
            }
            pool[i].color = c;
            return pool[i];
        }

        static void Stretch(Image bar, Vector2 a, Vector2 b, float width)
        {
            var d = b - a;
            var rt = bar.rectTransform;
            rt.anchoredPosition = (a + b) / 2f;
            rt.sizeDelta = new Vector2(d.magnitude + width * 0.6f, width);
            rt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            bar.gameObject.SetActive(true);
        }

        public void DrawMinimap(Camera map)
        {
            if (!map) return;
            if (mapBall == null) BuildMinimapMarks();
            var plan = Map;
            bool Place(RectTransform mark, Vector3 world, bool show)
            {
                var vp = map.WorldToViewportPoint(world);
                bool inside = show && vp.x >= 0 && vp.x <= 1 && vp.y >= 0 && vp.y <= 1;
                mark.gameObject.SetActive(inside);
                if (inside) { mark.anchorMin = mark.anchorMax = new Vector2(vp.x, vp.y); mark.anchoredPosition = Vector2.zero; }
                return inside;
            }
            Place(mapPin, plan.Pin, plan.Pin != Vector3.zero);
            Place(mapBall, plan.Ball, plan.ShowBall);
            Place(mapLanding, plan.Landing, plan.ShowLanding);
            if (onTv && tvMap.gameObject.activeSelf) tvMap.Draw(map, Minimap.texture, plan.Pin, plan.Ball, plan.ShowBall, plan.Landing, plan.ShowLanding);
            Place(mapLoad, plan.Load, plan.ShowLoad);
            if (plan.Version == mapDrawn && map == mapDrawnWith) return;
            mapDrawn = plan.Version; mapDrawnWith = map;

            // the zone: an ellipse on the aim, sized off the map's own scale
            mapZoneFill.gameObject.SetActive(plan.ShowZone); mapZoneEdge.gameObject.SetActive(plan.ShowZone);
            if (plan.ShowZone)
            {
                float h = plan.ZoneHeading * Mathf.Deg2Rad;
                var ahead = new Vector3(Mathf.Sin(h), 0, Mathf.Cos(h)); var right = new Vector3(ahead.z, 0, -ahead.x);
                var c = MapPoint(map, plan.ZoneCentre);
                var along = MapPoint(map, plan.ZoneCentre + ahead * plan.ZoneAlong) - c;
                var across = MapPoint(map, plan.ZoneCentre + right * plan.ZoneAcross) - c;
                var size = new Vector2(Mathf.Max(8f, 2f * across.magnitude), Mathf.Max(8f, 2f * along.magnitude));
                var turn = Quaternion.Euler(0, 0, Mathf.Atan2(along.y, along.x) * Mathf.Rad2Deg - 90f);
                foreach (var img in new[] { mapZoneFill, mapZoneEdge })
                {
                    var rt = img.rectTransform;
                    rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.anchoredPosition = c; rt.sizeDelta = size; rt.localRotation = turn;
                }
            }
            // the checkpoints' targets
            for (int i = 0; i < mapTargets.Count; i++) Place(mapTargets[i], i < plan.Targets.Count ? plan.Targets[i] : Vector3.zero, i < plan.Targets.Count);
            // the reach: a dotted arc as far as a full swing with this club carries
            for (int i = 0; i < ReachDots; i++) Place(mapReach[i], i < plan.Reach.Count ? plan.Reach[i] : Vector3.zero, i < plan.Reach.Count);
            // the trace: the ball's own flight, a dark edge under the shot's colour
            int n = 0;
            var line = plan.TraceColor; line.a = 1f;
            for (int i = 1; i < plan.Trace.Count; i++, n++)
            {
                var a = MapPoint(map, plan.Trace[i - 1]); var b = MapPoint(map, plan.Trace[i]);
                Stretch(TraceBar(mapTraceUnder, n, new Color(MapInk.r, MapInk.g, MapInk.b, 0.5f), "Trace edge"), a, b, 8f);
                Stretch(TraceBar(mapTraceLine, n, line, "Trace"), a, b, 4.5f);
            }
            for (int i = n; i < mapTraceUnder.Count; i++) mapTraceUnder[i].gameObject.SetActive(false);
            for (int i = n; i < mapTraceLine.Count; i++) mapTraceLine[i].gameObject.SetActive(false);
            // the lines draw after the edges: keep every coloured stretch above every dark one
            if (n > 0 && mapTraceLine[0].transform.GetSiblingIndex() < mapTraceUnder[n - 1].transform.GetSiblingIndex())
                foreach (var bar in mapTraceLine) bar.transform.SetAsLastSibling();

            // dots spaced evenly along the flight, none right at the ball or the zone's middle
            var path = plan.Path;
            if (path.Count < 2) { foreach (var d in mapDots) d.gameObject.SetActive(false); return; }
            float total = 0; for (int i = 1; i < path.Count; i++) total += Vector3.Distance(path[i - 1], path[i]);
            for (int k = 0; k < MapDots; k++)
            {
                float want = total * (k + 1) / (MapDots + 1), run = 0; Vector3 at = path[path.Count - 1];
                for (int i = 1; i < path.Count; i++)
                {
                    float seg = Vector3.Distance(path[i - 1], path[i]);
                    if (run + seg >= want) { at = Vector3.Lerp(path[i - 1], path[i], seg > 0 ? (want - run) / seg : 0); break; }
                    run += seg;
                }
                Place(mapDots[k], at, true);
            }
        }

        // ---- The meter's checkpoints: three targets up the power meter — a third, two thirds and a
        // full backswing — each a numbered badge with the yards a full-speed swing loaded that far
        // carries, and the same numbered badges out on the course where those shots come down
        // (and dots on the minimap), so you know which mark to stop the backswing at. The badges
        // the fill has reached turn amber, the colour of the load dot.
        sealed class Checkpoint { public float Load; public RectTransform Root; public Image Disc, Pill; public Text Number, Yards; }
        readonly System.Collections.Generic.List<Checkpoint> checkpoints = new();
        readonly System.Collections.Generic.List<(RectTransform root, Image disc, Text number, RectTransform stem, RectTransform badge)> courseTargets = new();
        readonly System.Collections.Generic.List<Text> flagYards = new();

        /// A course marker's flag: a rounded white-rimmed badge on a pole, its number big and its
        /// yards under it (the pin's in yellow).
        (RectTransform, Image, Text, Text) Flag(Transform parent, int number)
        {
            var rim = UiKit.Panel(parent, "Flag", UiKit.ArcadeRim, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(84, 112));
            rim.rectTransform.pivot = new Vector2(0.5f, 0f); rim.raycastTarget = false;
            var fill = UiKit.Panel(rim.transform, "Fill", UiKit.ArcadeBlue, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            fill.rectTransform.offsetMin = new Vector2(5, 5); fill.rectTransform.offsetMax = new Vector2(-5, -5); fill.raycastTarget = false;
            var n = UiKit.Label(fill.transform, "Number", 44, TextAnchor.UpperCenter, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -2), new Vector2(0, 52), UiKit.Display, false);
            n.rectTransform.pivot = new Vector2(0.5f, 1); n.text = number.ToString(); n.color = Color.white; n.raycastTarget = false;
            var y = UiKit.Label(fill.transform, "Yards", 21, TextAnchor.LowerCenter, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 6), new Vector2(0, 50), UiKit.Display, false);
            y.rectTransform.pivot = new Vector2(0.5f, 0); y.color = Color.white; y.raycastTarget = false; y.lineSpacing = 0.85f;
            return (rim.rectTransform, fill, n, y);
        }
        static readonly Color BadgeRest = new(1f, 1f, 1f, 0.95f);

        (RectTransform, Image, Text) Badge(Transform parent, string name, int number, float size)
        {
            var disc = UiKit.Panel(parent, name, BadgeRest, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
            disc.sprite = UiKit.Circle; disc.type = Image.Type.Simple; disc.raycastTarget = false;
            disc.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var edge = UiKit.Panel(disc.transform, "Edge", new Color(0.06f, 0.1f, 0.18f, 0.9f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            edge.sprite = UiKit.Ring; edge.type = Image.Type.Simple; edge.raycastTarget = false;
            edge.rectTransform.offsetMin = new Vector2(-3, -3); edge.rectTransform.offsetMax = new Vector2(3, 3);
            var t = UiKit.Label(disc.transform, "Number", Mathf.RoundToInt(size * 0.62f), TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, UiKit.Display, false);
            t.text = number.ToString(); t.color = UiKit.Ground; t.raycastTarget = false;
            return (disc.rectTransform, disc, t);
        }

        /// The checkpoints at these loads, labelled with these yardages; null hides them (putts).
        /// Each is a numbered stop on the meter's track with a thin line across it, and its
        /// yardage in a pill beside the meter.
        /// `loads` where each sits on the meter (0–1), `yards` its label; `pin` the one on the
        /// pin, drawn in yellow (-1 for none).
        public void SetCheckpoints(float[] loads, string[] yards, int pin = -1)
        {
            if (loads == null) { foreach (var c in checkpoints) c.Root.gameObject.SetActive(false); return; }
            while (checkpoints.Count < loads.Length)
            {
                int n = checkpoints.Count + 1;
                var holder = new GameObject($"Checkpoint {n}").AddComponent<RectTransform>();
                holder.SetParent(meterTrack, false);
                holder.anchorMin = holder.anchorMax = new Vector2(0, 0.5f); holder.pivot = new Vector2(0.5f, 0.5f); holder.sizeDelta = Vector2.one;
                var (_, disc, number) = Badge(holder, "Stop", n, 46);
                // (the yardage rides the flag on the course now; the pill stays hidden)
                var pill = Capsule(holder, "Pill", new Color(UiKit.Ground.r, UiKit.Ground.g, UiKit.Ground.b, 0.78f), new Vector2(40, 0), new Vector2(104, 38));
                pill.rectTransform.pivot = new Vector2(0, 0.5f);
                var y = UiKit.Label(pill.transform, "Yards", 24, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, UiKit.Strong, false);
                y.color = UiKit.Ink; y.raycastTarget = false;
                checkpoints.Add(new Checkpoint { Root = holder, Disc = disc, Number = number, Pill = pill, Yards = y });
            }
            for (int i = 0; i < checkpoints.Count; i++)
            {
                var c = checkpoints[i];
                bool on = i < loads.Length;
                c.Root.gameObject.SetActive(on);
                if (!on) continue;
                c.Load = loads[i];
                // on the arc; the outer one set back a little from the rounded end
                c.Root.anchoredPosition = ArcPoint(Mathf.Min(loads[i], 0.97f));
                c.Yards.text = yards[i];
                c.Pill.gameObject.SetActive(false);
                c.Pill.rectTransform.sizeDelta = new Vector2(Mathf.Max(92, c.Yards.preferredWidth + 30), 38);
                c.Pill.color = i == pin ? UiKit.ArcadeYellow : new Color(UiKit.Ground.r, UiKit.Ground.g, UiKit.Ground.b, 0.78f);
                c.Yards.color = i == pin ? UiKit.ArcadeInk : UiKit.Ink;
                c.Yards.font = i == pin ? UiKit.Display : UiKit.Strong;
            }
            pinCheckpoint = pin;
            LightCheckpoints();
        }
        int pinCheckpoint = -1;

        void LightCheckpoints()
        {
            foreach (var c in checkpoints)
            {
                if (!c.Root.gameObject.activeSelf) continue;
                bool reached = meterLoad > 0.02f && meterLoad >= c.Load - 0.004f;
                c.Disc.color = reached ? UiKit.ArcadeYellow : BadgeRest;
            }
        }

        /// The same numbered targets out on the course, pinned to where each checkpoint's shot
        /// comes down; empty or `show` false hides them.
        public void SetCourseTargets(Camera view, System.Collections.Generic.IList<Vector3> spots, bool show)
        {
            while (courseTargets.Count < (spots?.Count ?? 0))
            {
                // on the whole canvas (the picture runs under the notch), placed by viewport so
                // it lands true on any screen; under the cards and the meter
                var holder = new GameObject($"Target {courseTargets.Count + 1}").AddComponent<RectTransform>();
                holder.SetParent(transform, false); holder.SetAsFirstSibling();
                holder.pivot = new Vector2(0.5f, 0.5f); holder.sizeDelta = Vector2.one;
                // a white pole standing on the spot, a shadow at its foot, the flag at the top
                var foot = UiKit.Panel(holder, "Foot", new Color(0.03f, 0.1f, 0.03f, 0.35f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34, 12));
                foot.sprite = UiKit.Circle; foot.type = Image.Type.Simple; foot.rectTransform.pivot = new Vector2(0.5f, 0.5f); foot.raycastTarget = false;
                var stem = UiKit.Panel(holder, "Pole", Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(7, 110), false);
                stem.rectTransform.pivot = new Vector2(0.5f, 0f); stem.raycastTarget = false;
                var (badge, disc, number, yards) = Flag(holder, courseTargets.Count + 1);
                courseTargets.Add((holder, disc, number, stem.rectTransform, badge));
                flagYards.Add(yards);
            }
            // Where each spot is on the screen. A short club's three spots stand well apart up the
            // screen and each badge sits on its own; a long club's are nearly one point near the
            // horizon, so the badges fan out in a row above them, a thin stem down to each spot.
            var canvas = ((RectTransform)transform).rect.size;
            var at = new Vector2[courseTargets.Count];
            var shown = new bool[courseTargets.Count];
            for (int i = 0; i < courseTargets.Count; i++)
            {
                shown[i] = show && view && spots != null && i < spots.Count;
                if (!shown[i]) continue;
                var vp = view.WorldToViewportPoint(spots[i]);
                shown[i] = vp.z > 0.5f && vp.x > -0.05f && vp.x < 1.05f && vp.y > -0.05f && vp.y < 1.05f
                           // a spot behind a cliff or a bank is out of sight: the meter and the map still have it
                           && !Physics.Linecast(view.transform.position, spots[i] + Vector3.up * 0.6f, ~(1 << Game.BallLook.OverlayLayer), QueryTriggerInteraction.Ignore);
                at[i] = new Vector2(vp.x, vp.y);
            }
            bool crowded = false;
            for (int i = 0; i < at.Length; i++)
                for (int j = i + 1; j < at.Length; j++)
                    if (shown[i] && shown[j] && Vector2.Scale(at[i] - at[j], canvas).magnitude < 96f) crowded = true;
            int count = 0; foreach (var b in shown) if (b) count++;
            for (int i = 0, k = 0; i < courseTargets.Count; i++)
            {
                var (root, disc, _, stem, badge) = courseTargets[i];
                root.gameObject.SetActive(shown[i]);
                if (!shown[i]) continue;
                root.anchorMin = root.anchorMax = at[i]; root.anchoredPosition = Vector2.zero;
                root.localScale = Vector3.one * Mathf.Lerp(1f, hudScale, 0.6f);
                // the pole stands up from its spot; crowded (a long club's spots nearly one point at
                // the horizon) the poles lean apart so the flags stand side by side
                bool pin = i == pinCheckpoint;
                float flagHeight = pin ? 136 : 112;
                // as tall a pole as the screen has room for above the spot (the green is often
                // up at the horizon, under the cards); no room at all and the flag hangs below it
                float room = canvas.y - 40f - at[i].y * canvas.y - flagHeight;
                bool below = room < 26f;
                float pole = below ? 34f : Mathf.Min(104f, room);
                var top = new Vector2(crowded ? (k - (count - 1) / 2f) * 96f : 0, below ? -pole : pole);
                k++;
                stem.sizeDelta = new Vector2(7, top.magnitude);
                stem.localRotation = Quaternion.Euler(0, 0, -Mathf.Atan2(top.x, top.y) * Mathf.Rad2Deg);
                badge.anchoredPosition = below ? top - new Vector2(0, flagHeight) : top - new Vector2(0, 6f);
                string label = i < checkpoints.Count ? checkpoints[i].Yards.text : "";
                // "PIN 198" → PIN / 198 / yd;  "178 yd" → 178 / yd
                flagYards[i].text = pin ? label.Replace("PIN ", "PIN\n") + "\nyd" : label.Replace(" yd", "\nyd");
                badge.sizeDelta = new Vector2(84, flagHeight);
                disc.color = pin ? UiKit.ArcadeYellow : UiKit.ArcadeBlue;
                courseTargets[i].number.color = flagYards[i].color = pin ? UiKit.ArcadeInk : Color.white;
                // a checkpoint the backswing has passed: its flag lights up
                bool reached = i < checkpoints.Count && checkpoints[i].Disc.color == UiKit.ArcadeYellow;
                badge.localScale = Vector3.one * (reached ? 1.12f : 1f);
            }
        }

        // ---- The shot as Golf Dreams shows it: while the ball is away the aiming HUD clears and
        // a row of swing-stat tiles runs across the top (a coloured title strip over a white
        // value), and a yardage label rides beside the ball, counting as it flies and runs.
        Text ballTag;
        bool flightMode;

        /// The aiming HUD out of the way for the shot (cards, meter, map, prompts), or back.
        public void FlightMode(bool on)
        {
            if (flightMode == on) return;
            flightMode = on;
            // (the scoreboard and the distance card stay in their corner through the shot)
            if (Controller == null || onTv) meterRect.gameObject.SetActive(!on);
            minimapHolder.gameObject.SetActive(!on && !onTv && playHud);
            tvMap.gameObject.SetActive(!on && onTv && playHud);
            statusText.enabled = tempoText.enabled = !on;
            if (on) foreach (var b in new[] { AimLeft, AimRight, ClubUp, ClubDown, SwingHold }) b.gameObject.SetActive(false);
        }

        SwingCard swingCard;

        /// After the strike, the swing card (option A): the grade and the shape, six tiles, the line.
        public SwingCard ShowSwingCard(string grade, Color gradeColor, string shape, (string icon, string title, string value)[] tiles)
        {
            swingCard.Show(grade, gradeColor, shape, tiles);
            swingCard.transform.SetAsLastSibling();
            return swingCard;
        }

        public void HideShotStats() => swingCard.Hide();

        /// The yardage beside the ball, placed off `view`; null text hides it.
        /// The pin on the course picture, or where to look for it (null `view` hides it).
        public void SetPinMarker(Camera view, Vector3 pin, string text) => pinLocator.Set(view, pin, text);

        public void SetBallTag(Camera view, Vector3 ball, string text)
        {
            if (ballTag == null)
            {
                ballTag = UiKit.Label(transform, "Ball tag", 36, TextAnchor.LowerLeft, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(260, 48), UiKit.Display);
                ballTag.rectTransform.pivot = new Vector2(0, 0); ballTag.color = Color.white; ballTag.raycastTarget = false;
                ballTag.transform.SetSiblingIndex(1);   // over the picture, under the HUD
            }
            bool on = text != null && view;
            if (on)
            {
                var vp = view.WorldToViewportPoint(ball);
                on = vp.z > 0.3f && vp.x > -0.1f && vp.x < 1.1f && vp.y > -0.1f && vp.y < 1.1f;
                if (on) { ballTag.rectTransform.anchorMin = ballTag.rectTransform.anchorMax = new Vector2(vp.x, vp.y); ballTag.rectTransform.anchoredPosition = new Vector2(18, 16); ballTag.text = text; }
            }
            ballTag.gameObject.SetActive(on);
        }



        /// A white arrow pointing up, drawn once into a small texture.
        static Sprite ArrowSprite()
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    bool shaft = x >= 27 && x <= 37 && y >= 6 && y <= 40;
                    float half = 20f * (60 - y) / 24f;
                    bool head = y >= 36 && y <= 60 && Mathf.Abs(x - 31.5f) <= half;
                    px[y * n + x] = shaft || head ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
                }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        }

        /// The hole, for the controller sheet (the scoreboard has it on the phone HUD).
        public void SetHole(int number, int par, double yards, string picture = null, string name = null)
        {
            lastHole = (number, par, yards, name);
            Controller?.SetHole(number, par, yards, name);
        }
        (int number, int par, double yards, string name) lastHole;
        /// The score, for the controller sheet (SetScoreboard draws it on the phone HUD).
        public void SetScore(int strokes, int toPar, int holeStrokes) => Controller?.SetScore(toPar, holeStrokes);

        // ---- The scoreboard, the tennis broadcast's in golf: a tab with the tournament and the
        // hole, then a row for you (yellow) and a row for par (blue), a chip per hole of the round
        // — yellow, the one being played lit — and the totals in white at the end.
        const float BoardHeight = 196f;

        /// `strokes` per hole, null where not played (the one being played shows its live count).
        public void SetScoreboard(string title, int[] pars, int?[] strokes, int current)
        {
            foreach (Transform child in board) Destroy(child.gameObject);
            const float row = 58f, name = 150f, chip = 58f, gap = 10f, total = 78f, pad = 14f, top = 34f;
            int holes = pars.Length;
            bool totals = holes > 1;
            float width = pad + name + gap + holes * (chip + gap) + (totals ? total + gap : 0) + pad - gap + 6;
            board.sizeDelta = new Vector2(width, BoardHeight);
            var backRoot = UiKit.Pill(board, "Board", UiKit.ArcadeSky, new Vector2(0, 1), Vector2.zero, new Vector2(width, BoardHeight - 18), out _, 4f, round: false);
            backRoot.pivot = new Vector2(0, 1); backRoot.anchoredPosition = new Vector2(0, -18);
            // the tab
            var tabText = UiKit.Label(board, "Title", 21, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, UiKit.Display, false);
            tabText.text = title.ToUpperInvariant();
            float tabW = tabText.preferredWidth + 36;
            var tab = UiKit.Pill(board, "Tab", UiKit.ArcadeBlueDeep, new Vector2(0, 1), new Vector2(18 + tabW / 2, -18), new Vector2(tabW, 38), out var tabFill, 3f);
            tabText.transform.SetParent(tabFill.transform, false);
            var tr = tabText.rectTransform; tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = tr.offsetMax = Vector2.zero;
            tabText.color = UiKit.ArcadeYellow;
            int you = 0, parSum = 0;
            for (int i = 0; i < holes; i++) { if (strokes[i] is int n) { you += n; parSum += pars[i]; } }
            if (strokes.Length > current && strokes[current] == null) parSum += pars[current];
            for (int r = 0; r < 2; r++)
            {
                bool player = r == 0;
                float y = -18 - top - r * (row + 12) - row / 2;
                float x = pad + name / 2;
                var plate = UiKit.Pill(board, player ? "You" : "Par", player ? UiKit.ArcadeYellow : UiKit.ArcadeBlue, new Vector2(0, 1), new Vector2(x, y), new Vector2(name, row), out var plateFill, 4f);
                var label = UiKit.Chunky(plateFill.transform, "Name", 32, player ? Color.white : Color.white, player ? UiKit.ArcadeYellowDeep : UiKit.ArcadeBlueDeep, 2.5f);
                label.text = player ? "YOU" : "PAR";
                x = pad + name + gap + chip / 2;
                for (int i = 0; i < holes; i++, x += chip + gap)
                {
                    bool live = i == current, played = strokes[i].HasValue;
                    string value = player ? (played ? strokes[i].Value.ToString() : "–") : pars[i].ToString();
                    var fill = live ? UiKit.ArcadeYellow : played || !player ? UiKit.Hex("FFE58F") : UiKit.Hex("E9EEF8");
                    var c = UiKit.Pill(board, $"Hole {i}", fill, new Vector2(0, 1), new Vector2(x, y), new Vector2(chip, row), out var cf, live ? 4f : 3f);
                    var t = UiKit.Label(cf.transform, "Value", 32, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, UiKit.Display, false);
                    t.text = value; t.color = UiKit.ArcadeInk;
                }
                if (!totals) continue;
                x += total / 2 - chip / 2;
                var tot = UiKit.Pill(board, "Total", Color.white, new Vector2(0, 1), new Vector2(x, y), new Vector2(total, row), out var tf, 3f, round: false);
                var tt = UiKit.Label(tf.transform, "Value", 32, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, UiKit.Display, false);
                tt.text = (player ? you : parSum).ToString(); tt.color = UiKit.ArcadeInk;
            }
        }

        /// The distance card's big number: `amount` in `unit` ("YD", "FT"), `caption` over it.
        public void SetDistance(double amount, string unit, string caption)
        {
            yardage.SetDistance(amount, unit, caption);
            lastDistance = (amount, unit);
            Controller?.SetDistance(amount, unit);
        }

        /// The club and what it carries, on the yellow pill.
        public void SetClub(string text, bool putter = false) => yardage.SetClub(text, putter);

        /// One of the yardage card's rows (0 plays / slope, 1 wind / break, 2 lie).
        public void SetCardRow(int row, string icon, string label, string value, string note = null, float? arrowDegrees = null)
            => yardage.SetRow(row, icon, label, value, note, arrowDegrees);

        public void SetStatus(string text) { statusText.text = text; Controller?.SetStatus(text); }
        public void SetTempo(string text) => tempoText.text = text;
        public void SetControllerHint(string text) => controllerText.text = text;
        /// What the player is being told right now — the banner while it shows, else the status.
        public string CurrentMessage => bannerGroup.alpha > 0.5f ? bannerText.text : statusText.text;

        /// The Wii meter fills with the backswing; as it climbs the fill warms from green through
        /// yellow to orange and the whole bar starts to tremble, so the tension of a big swing is
        /// in the picture as well as in the hand and the ear.
        public void SetMeter(float load, float? mark = null, string yards = null)
        {
            meterLoad = Mathf.Clamp01(load);
            bool gauge = yards != null && meterLoad > 0.02f;
            gaugeRoot.gameObject.SetActive(gauge);
            if (gauge)
            {
                meterYards.text = yards;
                gaugeRoot.sizeDelta = new Vector2(Mathf.Max(124, meterYards.preferredWidth + 44), 58);
                // just inside the tip, toward the picture, so the arc stays visible
                gaugeRoot.anchoredPosition = ArcPoint(meterLoad, 105f) + new Vector2(0, 26f);
            }
            LightCheckpoints();
            // the fill: whole segments up to the load, the last one cut at it, and round ends
            float span = meterLoad * ArcSegments;
            for (int i = 0; i < arcSegments.Count; i++)
            {
                float part = Mathf.Clamp01(span - i);
                arcSegments[i].enabled = part > 0.001f;
                arcSegments[i].fillAmount = part * ArcSweep / ArcSegments / 360f + (part >= 1 ? 0.0015f : 0);
            }
            arcStartCap.enabled = arcTip.enabled = meterLoad > 0.004f;
            arcTip.rectTransform.anchoredPosition = ArcPoint(meterLoad);
            arcTip.color = ArcColor(meterLoad);
            Controller?.SetMeter(meterLoad, ArcColor(meterLoad));
            meterMark.enabled = mark.HasValue;
            if (mark.HasValue) meterMark.rectTransform.anchoredPosition = ArcPoint(mark.Value);
        }

        /// The word at impact (PERFECT!, GREAT!, GOOD, THIN) and the ball's shape under it.
        public void ShowStrike(string word, Color color, string shape) => strikePopup.Show(word, color, shape);

        LandingBadge landingBadge;
        /// Where the ball finished, stamped over the course (UI/LandingBadge.cs, option A).
        public void ShowLanding(LandingBadge.Kind kind, string word, string detail, string stats, float seconds = 2.4f)
        {
            bannerGroup.alpha = 0;   // (it takes the banner's place)
            landingBadge.Show(kind, word, detail, stats, seconds);
        }
        public void HideLanding() => landingBadge.Hide();

        /// Whose shot it is, in their colour (two or more golfers on the phone, alternating).
        public void ShowTurn(string word, string detail, Color color)
        {
            landingBadge.TurnColor = color;
            ShowLanding(LandingBadge.Kind.Turn, word, detail, null, 1.8f);
        }

        /// The club face while setting up and swinging (degrees, positive open); null hides it.
        public void SetFace(double? degrees) => faceDial.Set(Controller == null ? degrees : null);

        public void ShowReplay(bool on) { replayBadge.gameObject.SetActive(on); if (on) replayBadge.SetAsLastSibling(); }

        public void ShowBanner(string text, float seconds = 2f)
        {
            bannerText.text = text;
            bannerGroup.alpha = 1;
            bannerUntil = Time.time + seconds;
        }

        /// `touchButtons` false hides the aim/club buttons — they live on the phone when it is
        /// the club and this screen is only the display.
        public void ShowSwingControls(bool aiming, bool debugSwingButton, bool touchButtons = true)
        {
            bool phoneHud = Controller == null;
            AimLeft.gameObject.SetActive(phoneHud && aiming && touchButtons);
            AimRight.gameObject.SetActive(phoneHud && aiming && touchButtons);
            ClubUp.gameObject.SetActive(phoneHud && aiming && touchButtons);
            ClubDown.gameObject.SetActive(phoneHud && aiming && touchButtons);
            SwingHold.gameObject.SetActive(phoneHud && aiming && debugSwingButton);
            if (Controller != null)
            {
                foreach (var b in new[] { Controller.AimLeft, Controller.AimRight, Controller.ClubUp, Controller.ClubDown }) b.gameObject.SetActive(aiming && touchButtons);
                Controller.Knob.gameObject.SetActive(aiming); // the joystick aims; it only swings without a phone-club
                Controller.Knob.Pressed = debugSwingButton ? SwingHold.Pressed : null;
                Controller.Knob.Released = debugSwingButton ? SwingHold.Released : null;
            }
        }

        /// The big screen has the course: the phone becomes the controller. The phone HUD's cards
        /// and meter step aside for the sheet, which takes over the same buttons.
        /// The course has gone to the big screen: the phone becomes the controller. `tv` is the
        /// camera drawing the course on the TV (null for a preview on the phone): with one, the
        /// whole broadcast HUD — scoreboard, distance card, power meter, the strike's word,
        /// banners, the flags on the course, the ball's yardage — goes to the TV with the course,
        /// and the controller has the phone to itself. In a preview the sheet covers the phone HUD.
        public void EnterControllerLayout(Camera tv = null)
        {
            if (Controller != null && Controller.Alive) return;
            liveTv = tv; onTv = false;
            if (liveTv)
            {
                Controller = new ControllerSheet(PhoneLayer());
                HudOnTv(true);
            }
            else
            {
                Controller = new ControllerSheet(safeArea);
                foreach (var rt in new[] { board, shotCard, meterRect }) rt.gameObject.SetActive(false);
            }
            Controller.AimLeft.Pressed = AimLeft.Pressed; Controller.AimRight.Pressed = AimRight.Pressed;
            Controller.ClubUp.Pressed = ClubUp.Pressed; Controller.ClubDown.Pressed = ClubDown.Pressed;
            Controller.Knob.Pressed = SwingHold.Pressed; Controller.Knob.Released = SwingHold.Released;
            // the live minimap moves into the sheet's map card, and back out when it goes
            mapHome = Minimap.transform.parent;
            Minimap.transform.SetParent(Controller.MapSlot, false);
            Controller.SetStatus(statusText.text);
            if (lastHole.number > 0) Controller.SetHole(lastHole.number, lastHole.par, lastHole.yards, lastHole.name);
            if (lastDistance.unit != null) Controller.SetDistance(lastDistance.amount, lastDistance.unit);
            if (lastWind.set) Controller.SetWind(lastWind.degrees, lastWind.mph, lastWind.calm);
            if (!onTv) bannerGroup.transform.SetAsLastSibling();   // "Birdie!" still shows over the sheet
        }

        Transform mapHome;
        (double amount, string unit) lastDistance;
        (bool set, float degrees, double mph, bool calm) lastWind;

        public void LeaveControllerLayout()
        {
            if (Controller == null) return;
            if (mapHome) Minimap.transform.SetParent(mapHome, false);
            Controller.Destroy(); Controller = null;
            HudOnTv(false);
            liveTv = null;
            if (phoneLayer) { Destroy(phoneLayer.parent.gameObject); phoneLayer = null; }
            foreach (var rt in new[] { board, shotCard, meterRect }) rt.gameObject.SetActive(true);
            minimapHolder.gameObject.SetActive(!flightMode && playHud);
        }

        // ---- The HUD on the big screen. The course camera draws to display 1; a camera of the
        // HUD's own, far out of the course's way and drawing after it, carries this canvas there.
        // The controller gets a canvas of its own on the phone.
        bool onTv;
        Vector2 meterPhoneHome;
        Camera liveTv, tvHudCamera;
        RectTransform phoneLayer;
        /// True while the HUD is on the TV (the course is live there and the phone is the club).
        public bool OnTv => onTv;

        /// While the course is live on the big screen: `on` puts the HUD there (a round being
        /// played, the controller in hand); off brings it back to the phone, which is where the
        /// menu, the golfer picker and the round's card have to be — they are touched.
        public void HudOnTv(bool on)
        {
            on &= liveTv != null;
            if (on == onTv) return;
            onTv = on;
            ShowOnTv(on ? liveTv : null);
            minimapHolder.gameObject.SetActive(!on && !flightMode && playHud);   // (not over the menu)
            // the phone's map is in the controller; the big screen gets its own, with the pin on it
            tvMap.gameObject.SetActive(on && !flightMode && playHud);
        }

        void ShowOnTv(Camera course)
        {
            var canvas = GetComponent<Canvas>();
            var scaler = GetComponent<CanvasScaler>();
            if (course)
            {
                if (!tvHudCamera)
                {
                    tvHudCamera = new GameObject("TV HUD camera").AddComponent<Camera>();
                    tvHudCamera.transform.SetParent(transform.parent, false);
                    tvHudCamera.transform.position = new Vector3(0, -5000, 0);
                    tvHudCamera.clearFlags = CameraClearFlags.Depth;
                    tvHudCamera.nearClipPlane = 0.01f; tvHudCamera.farClipPlane = 5f;
                }
                tvHudCamera.targetDisplay = course.targetDisplay;
                tvHudCamera.depth = course.depth + 10;
                tvHudCamera.gameObject.SetActive(true);
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = tvHudCamera; canvas.planeDistance = 1f;
                // a landscape screen: sized off its height, a size up from the phone so the corner
                // and the meter read from the sofa
                scaler.matchWidthOrHeight = 1f;
                scaler.referenceResolution = new Vector2(UiKit.PhoneReference.x, 1700);
                safeArea.anchorMin = Vector2.zero; safeArea.anchorMax = Vector2.one;
                safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
                // the meter drops clear of the yardage card on the shorter screen
                meterHome = meterPhoneHome + new Vector2(0, -170);
                ScaleHud(1f);
            }
            else
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.worldCamera = null;
                scaler.matchWidthOrHeight = 0.5f;
                scaler.referenceResolution = UiKit.PhoneReference;
                if (tvHudCamera) tvHudCamera.gameObject.SetActive(false);
                meterHome = meterPhoneHome;
                ScaleHud(PhoneHudScale);
                appliedSafeArea = default;
                ApplySafeArea();
            }
        }

        /// The phone's own canvas for the controller, inside the phone's safe area.
        Transform PhoneLayer()
        {
            if (phoneLayer) return phoneLayer;
            var go = new GameObject("Phone controller");
            go.transform.SetParent(transform.parent, false);
            UiKit.Canvas(go).sortingOrder = 20;
            phoneLayer = new GameObject("Safe area").AddComponent<RectTransform>();
            phoneLayer.SetParent(go.transform, false);
            var area = Screen.safeArea;
            if (Screen.width > 0 && Screen.height > 0)
            {
                phoneLayer.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
                phoneLayer.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            }
            else { phoneLayer.anchorMin = Vector2.zero; phoneLayer.anchorMax = Vector2.one; }
            phoneLayer.offsetMin = phoneLayer.offsetMax = Vector2.zero;
            return phoneLayer;
        }

        void Update()
        {
            ApplySafeArea();
            // Tremble: nothing below 40 % load, up to ±5 px at the top of the backswing.
            float tremble = Mathf.InverseLerp(0.4f, 1f, meterLoad) * 5f;
            meterRect.anchoredPosition = meterHome + (tremble > 0
                ? new Vector2(Mathf.Sin(Time.unscaledTime * 63f), Mathf.Cos(Time.unscaledTime * 47f)) * tremble
                : Vector2.zero);
            if (bannerGroup.alpha > 0 && Time.time > bannerUntil)
                bannerGroup.alpha = Mathf.MoveTowards(bannerGroup.alpha, 0, Time.deltaTime * 3);
        }

        Text Label(string name, int size, TextAnchor anchor, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 sizeDelta, Transform parent = null)
            => UiKit.Label(parent ? parent : safeArea, name, size, anchor, anchorMin, anchorMax, pos, sizeDelta);

        Image Panel(string name, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 sizeDelta, Transform parent = null, bool rounded = true)
            => UiKit.Panel(parent ? parent : safeArea, name, color, anchorMin, anchorMax, pos, sizeDelta, rounded);

        Image Card(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 sizeDelta, Color? fill = null, Transform parent = null)
            => UiKit.Card(parent ? parent : safeArea, name, anchorMin, anchorMax, pos, sizeDelta, fill);

        HoldButton Button(string label, Vector2 anchor, Vector2 pos, Vector2 size, int fontSize = 80, Transform parent = null)
            => UiKit.Button(parent ? parent : safeArea, label, anchor, pos, size, fontSize);
    }
}
