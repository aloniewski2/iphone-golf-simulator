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
        Text holeText, scoreText, distanceText, clubText, statusText, bannerText, tempoText, windText, controllerText;
        Text meterYards;
        Image meterFill, meterMark, meterOverswing, windArrow;
        RectTransform meterRect;
        Vector2 meterHome;
        float meterLoad;
        CanvasGroup bannerGroup;
        RectTransform scorecard, holeCard, scoreCard, shotCard;
        float bannerUntil;

        /// The phone-as-controller layout while the course is on the big screen; null otherwise.
        public ControllerSheet Controller { get; private set; }
        public bool AimLeftHeld => AimLeft.IsHeld || (Controller != null && Controller.AimLeft.IsHeld);
        public bool AimRightHeld => AimRight.IsHeld || (Controller != null && Controller.AimRight.IsHeld);
        /// The joystick's push, -1..1 right, when the controller layout is up.
        public float AimStick => Controller != null && Controller.Stick ? Controller.Stick.Value.x : 0f;

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

            // Top row: the hole card on the left, the score card on the right.
            var holeCardImg = Card("Hole card", new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -40), new Vector2(640, 90));
            holeCardImg.rectTransform.pivot = new Vector2(0, 1);
            holeCard = holeCardImg.rectTransform;
            holeText = Label("Hole", 40, TextAnchor.MiddleLeft, new Vector2(0, 0), new Vector2(1, 1), new Vector2(28, 0), Vector2.zero, holeCard);
            var scoreCardImg = Card("Score card", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-30, -40), new Vector2(360, 90));
            scoreCardImg.rectTransform.pivot = new Vector2(1, 1);
            scoreCard = scoreCardImg.rectTransform;
            scoreText = Label("Score", 38, TextAnchor.MiddleRight, new Vector2(0, 0), new Vector2(1, 1), new Vector2(-28, 0), Vector2.zero, scoreCard);

            // Shot card: distance to the pin, club and its yardage, wind.
            var shotCardImg = Card("Shot card", new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -150), new Vector2(640, 210));
            shotCardImg.rectTransform.pivot = new Vector2(0, 1);
            shotCard = shotCardImg.rectTransform;
            distanceText = Label("Distance", 52, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -18), new Vector2(600, 60), shotCard);
            clubText = Label("Club", 36, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -84), new Vector2(600, 50), shotCard);
            clubText.color = UiKit.Muted;
            windArrow = Panel("Wind arrow", new Color(1f, 1f, 1f, 0.95f), new Vector2(0, 0), new Vector2(0, 0), new Vector2(60, 40), new Vector2(56, 56), shotCard);
            windArrow.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            windArrow.sprite = ArrowSprite(); windArrow.type = Image.Type.Simple;
            windText = Label("Wind", 32, TextAnchor.MiddleLeft, new Vector2(0, 0), new Vector2(0, 0), new Vector2(100, 14), new Vector2(500, 50), shotCard);
            windText.color = UiKit.Muted;
            statusText = Label("Status", 40, TextAnchor.MiddleCenter, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 420), new Vector2(1000, 60));
            statusText.color = new Color(1, 1, 1, 0.9f);
            tempoText = Label("Tempo", 34, TextAnchor.MiddleCenter, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 360), new Vector2(1000, 50));
            tempoText.color = new Color(1, 0.95f, 0.7f, 0.95f);

            // Power meter, left edge: a rounded track that fills from the bottom, quarter ticks,
            // and a red band at the top where a swing goes wild.
            var meterBg = Card("Meter", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(40, 0), new Vector2(60, 900));
            meterRect = meterBg.rectTransform;
            meterHome = meterRect.anchoredPosition;
            meterOverswing = Panel("Overswing", new Color(0.9f, 0.2f, 0.15f, 0.55f), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -6), new Vector2(48, 110), meterBg.transform);
            meterFill = Panel("Fill", new Color(0.35f, 0.85f, 0.35f, 0.95f), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 6), new Vector2(48, 0), meterBg.transform);
            meterFill.rectTransform.pivot = new Vector2(0.5f, 0);
            for (int q = 1; q < 4; q++)
            {
                var tick = Panel($"Tick {q}", new Color(1, 1, 1, 0.35f), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 6 + 888 * q / 4f), new Vector2(48, 3), meterBg.transform, rounded: false);
                tick.rectTransform.pivot = new Vector2(0.5f, 0.5f); tick.raycastTarget = false;
            }
            meterMark = Panel("Mark", Color.white, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 6), new Vector2(64, 6), meterBg.transform, rounded: false);
            meterMark.enabled = false;
            var meterLabel = Label("Power", 26, TextAnchor.MiddleCenter, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, -32), new Vector2(200, 40), meterBg.transform);
            meterLabel.text = "POWER"; meterLabel.color = UiKit.Muted;
            // the gauge: how far this much backswing carries, riding the top of the fill
            meterYards = UiKit.Label(meterBg.transform, "Yards", 28, TextAnchor.MiddleLeft, new Vector2(1, 0), new Vector2(1, 0), new Vector2(94, 6), new Vector2(160, 40), UiKit.Strong);
            meterYards.color = new Color(1f, 0.72f, 0.25f); meterYards.enabled = false;

            // Minimap, top right under the score, in a rounded frame.
            var mapFrame = Card("Minimap frame", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-30, -150), new Vector2(272, 432));
            mapFrame.rectTransform.pivot = new Vector2(1, 1);
            var mapMask = Panel("Mask", Color.white, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, mapFrame.transform);
            mapMask.rectTransform.offsetMin = new Vector2(6, 6); mapMask.rectTransform.offsetMax = new Vector2(-6, -6);
            mapMask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var mapGo = new GameObject("Minimap");
            mapGo.transform.SetParent(mapMask.transform, false);
            Minimap = mapGo.AddComponent<RawImage>();
            var mapRt = Minimap.rectTransform;
            mapRt.anchorMin = Vector2.zero; mapRt.anchorMax = Vector2.one;
            mapRt.offsetMin = mapRt.offsetMax = Vector2.zero;
            Minimap.color = new Color(1, 1, 1, 0.96f);

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

            // Scorecard, filled in when the round ends.
            var card = Card("Scorecard", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 600), new Color(0.05f, 0.09f, 0.17f, 0.94f));
            card.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            scorecard = card.rectTransform;
            scorecard.gameObject.SetActive(false);
        }

        /// Fits the HUD to the phone's safe area (notch at the top, home indicator at the bottom).
        void ApplySafeArea()
        {
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
            windText.text = label;
            windArrow.enabled = !calm;
            windArrow.rectTransform.localRotation = Quaternion.Euler(0, 0, -relativeDegrees);
            Controller?.SetWind(relativeDegrees, mph, calm);
        }

        /// Lays out the round's card: a row per hole, the totals, and a button to go again.
        public void ShowScorecard(GolfArcade.Course.Scorecard card)
        {
            foreach (Transform child in scorecard) if (child.name != "Fill") Destroy(child.gameObject);
            var rows = new System.Collections.Generic.List<(string hole, string par, string score)>(card.Rows());
            float rowHeight = 62, top = 130, bottom = 260;
            scorecard.sizeDelta = new Vector2(900, top + rowHeight * (rows.Count + 2) + bottom);
            float y = -30;
            var title = Label("Title", 52, TextAnchor.UpperCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(860, 60), scorecard);
            title.text = card.Course.Name;
            y -= 90;
            Row(y, "HOLE", "PAR", "SCORE", 34, new Color(0.8f, 0.9f, 0.8f)); y -= rowHeight;
            foreach (var r in rows) { Row(y, r.hole, r.par, r.score, 44, Color.white); y -= rowHeight; }
            Row(y, "TOTAL", card.Course.Par.ToString(), card.Total.ToString(), 44, new Color(1f, 0.95f, 0.6f)); y -= rowHeight;
            var toPar = Label("ToPar", 60, TextAnchor.UpperCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, y - 10), new Vector2(860, 80), scorecard);
            toPar.text = GolfArcade.Course.Scorecard.FormatToPar(card.ToPar);
            PlayAgain = Button("PLAY AGAIN", new Vector2(0.5f, 0), new Vector2(0, 90), new Vector2(420, 100), 40, scorecard);
            scorecard.gameObject.SetActive(true);
        }

        public void HideScorecard() => scorecard.gameObject.SetActive(false);

        /// The screen before a round: who you are, which hole, and whether the picture goes to
        /// the big screen. One dark sheet over a slow aerial of the chosen hole; the course
        /// cards carry the Blender renders of the holes. The game sets the callbacks and calls
        /// `Refresh` to light the current choices.
        public sealed class MenuView
        {
            public HoldButton Golfer, HoleSeven, HoleTwelve, BothHoles, AirPlay, Play;
            public Text BigScreenStatus, Build;
            internal RectTransform Root;
            internal Image SevenEdge, TwelveEdge, SevenBadge, TwelveBadge, BothCheck;
            internal Text GolferText, PlayText;

            public void Refresh(string golfer, int holes, string bigScreen)
            {
                GolferText.text = golfer;
                bool seven = holes == 7 || holes == 0, twelve = holes == 12 || holes == 0;
                SevenEdge.color = seven ? UiKit.Accent : UiKit.Hairline; SevenBadge.enabled = seven;
                TwelveEdge.color = twelve ? UiKit.Accent : UiKit.Hairline; TwelveBadge.enabled = twelve;
                BothCheck.color = holes == 0 ? UiKit.Accent : new Color(1, 1, 1, 0.08f);
                PlayText.text = holes == 0 ? "Play the round" : $"Play Hole {holes}";
                BigScreenStatus.text = bigScreen;
            }
        }

        MenuView menu;

        public MenuView ShowMenu()
        {
            HideMenu();
            // The sheet: the flyover shows through, dimmed to a ground the type can sit on.
            var sheet = Panel("Menu", new Color(UiKit.Ground.r, UiKit.Ground.g, UiKit.Ground.b, 0.86f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, rounded: false);
            var root = sheet.rectTransform;
            menu = new MenuView { Root = root };
            const float left = 70, width = 940;
            Text T(string name, string text, int size, Font face, Color color, float x, float y, float w, TextAnchor anchor = TextAnchor.UpperLeft, float h = 0)
            {
                var t = UiKit.Label(root, name, size, anchor, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, y), new Vector2(w, h > 0 ? h : size * 1.3f), face, false);
                t.text = text; t.color = color; return t;
            }

            float y = -60;
            T("Eyebrow", "CLIFFSIDE   ·   TWO HOLES", 26, UiKit.Strong, UiKit.Accent, left, y, width);
            y -= 44;
            T("Title", "Golf Arcade", 96, UiKit.Display, UiKit.Ink, left - 4, y, width, TextAnchor.UpperLeft, 120);
            y -= 122;
            T("Subtitle", "Swing the phone like a club. Read the green, hit the shot.", 32, UiKit.Body, UiKit.InkMuted, left, y, width);
            y -= 110;

            // Golfer: who you are in a line, and the way to the picker.
            T("Golfer heading", "GOLFER", 26, UiKit.Strong, UiKit.InkMuted, left, y, width);
            y -= 52;
            var who = UiKit.Panel(root, "Golfer", UiKit.Surface, new Vector2(0, 1), new Vector2(0, 1), new Vector2(left, y), new Vector2(width, 100));
            var whoHold = who.gameObject.AddComponent<HoldButton>();
            whoHold.Fill = who; whoHold.RestColor = UiKit.Surface;
            menu.Golfer = whoHold;
            menu.GolferText = UiKit.Label(who.transform, "Summary", 32, TextAnchor.MiddleLeft, new Vector2(0, 0), new Vector2(1, 1), new Vector2(28, 0), Vector2.zero, UiKit.Ui, false);
            menu.GolferText.raycastTarget = false;
            var change = UiKit.Label(who.transform, "Change", 28, TextAnchor.MiddleRight, new Vector2(0, 0), new Vector2(1, 1), new Vector2(-28, 0), Vector2.zero, UiKit.Strong, false);
            change.text = "Change  ›"; change.color = UiKit.Accent; change.raycastTarget = false;
            y -= 100 + 56;

            // The hole: course cards with the Blender renders, and a round of both.
            T("Hole heading", "HOLE", 26, UiKit.Strong, UiKit.InkMuted, left, y, width);
            y -= 52;
            HoldButton CourseCard(string name, string picture, string title, string detail, float x, out Image edge, out Image badge)
            {
                const float w = 456, h = 470, pic = 300;
                edge = UiKit.Panel(root, name, UiKit.Hairline, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, y), new Vector2(w, h));
                var fill = UiKit.Panel(edge.transform, "Fill", UiKit.Surface, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                fill.rectTransform.offsetMin = new Vector2(3, 3); fill.rectTransform.offsetMax = new Vector2(-3, -3); fill.raycastTarget = false;
                // The render, clipped to a rounded frame inset from the card's edge.
                var frame = UiKit.Panel(edge.transform, "Picture", Color.white, new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, Vector2.zero);
                frame.rectTransform.pivot = new Vector2(0.5f, 1);
                frame.rectTransform.sizeDelta = new Vector2(-28, pic); frame.rectTransform.anchoredPosition = new Vector2(0, -14);
                frame.raycastTarget = false;
                frame.gameObject.AddComponent<Mask>().showMaskGraphic = false;
                var img = new GameObject("Render").AddComponent<RawImage>();
                img.transform.SetParent(frame.transform, false);
                img.rectTransform.anchorMin = Vector2.zero; img.rectTransform.anchorMax = Vector2.one; img.rectTransform.offsetMin = img.rectTransform.offsetMax = Vector2.zero;
                img.texture = Resources.Load<Texture2D>(picture); img.raycastTarget = false;
                if (!img.texture) img.color = UiKit.SurfaceRaised;
                var t1 = UiKit.Label(edge.transform, "Title", 40, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -14 - pic - 18), new Vector2(w - 48, 50), UiKit.Strong, false);
                t1.text = title; t1.raycastTarget = false;
                var t2 = UiKit.Label(edge.transform, "Detail", 28, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -14 - pic - 74), new Vector2(w - 48, 40), UiKit.Body, false);
                t2.text = detail; t2.color = UiKit.InkMuted; t2.raycastTarget = false;
                badge = UiKit.Panel(edge.transform, "Badge", UiKit.Accent, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24 - 44, -24 - 44), new Vector2(44, 44));
                badge.sprite = UiKit.Circle; badge.type = Image.Type.Simple; badge.raycastTarget = false;
                var check = UiKit.Label(badge.transform, "Check", 26, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, UiKit.Display, false);
                check.text = "✓"; check.color = UiKit.Ground; check.raycastTarget = false;
                var hold = edge.gameObject.AddComponent<HoldButton>();
                hold.Fill = fill; hold.RestColor = UiKit.Surface;
                return hold;
            }
            menu.HoleSeven = CourseCard("Hole 7", "Course/hole_07_card", "Hole 7", "Cliffside  ·  Par 4  ·  418 yd", left, out menu.SevenEdge, out menu.SevenBadge);
            menu.HoleTwelve = CourseCard("Hole 12", "Course/hole_12_card", "Hole 12", "Island Carry  ·  Par 3  ·  198 yd", left + width - 456, out menu.TwelveEdge, out menu.TwelveBadge);
            y -= 470 + 24;
            var both = UiKit.Panel(root, "Both", UiKit.Surface, new Vector2(0, 1), new Vector2(0, 1), new Vector2(left, y), new Vector2(width, 92));
            var bothHold = both.gameObject.AddComponent<HoldButton>();
            bothHold.Fill = both; bothHold.RestColor = UiKit.Surface;
            menu.BothHoles = bothHold;
            menu.BothCheck = UiKit.Panel(both.transform, "Check", UiKit.Accent, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(24, 0), new Vector2(44, 44));
            menu.BothCheck.sprite = UiKit.Circle; menu.BothCheck.type = Image.Type.Simple; menu.BothCheck.raycastTarget = false;
            var bothMark = UiKit.Label(menu.BothCheck.transform, "Mark", 26, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, UiKit.Display, false);
            bothMark.text = "✓"; bothMark.color = UiKit.Ground; bothMark.raycastTarget = false;
            var bothText = UiKit.Label(both.transform, "Label", 32, TextAnchor.MiddleLeft, new Vector2(0, 0), new Vector2(1, 1), new Vector2(88, 0), Vector2.zero, UiKit.Ui, false);
            bothText.text = "Play both as a round   ·   Par 7"; bothText.raycastTarget = false;
            y -= 92 + 56;

            // Big screen.
            T("Screen heading", "BIG SCREEN", 26, UiKit.Strong, UiKit.InkMuted, left, y, width);
            y -= 52;
            T("Screen title", "AirPlay to your Mac", 34, UiKit.Strong, UiKit.Ink, left, y, 640);
            menu.BigScreenStatus = T("BigScreen", "", 26, UiKit.Body, UiKit.InkMuted, left, y - 46, 660, TextAnchor.UpperLeft, 110);
            menu.BigScreenStatus.horizontalOverflow = HorizontalWrapMode.Wrap;
            menu.AirPlay = UiKit.Button(root, "Set up", new Vector2(0, 1), new Vector2(left + width - 110, y - 40), new Vector2(220, 80), 30, UiKit.SurfaceRaised, UiKit.Strong, false);

            // Play: the one accent on the sheet.
            menu.Play = UiKit.Button(root, "Play", new Vector2(0.5f, 0), new Vector2(0, 60 + 65), new Vector2(width, 130), 46, UiKit.AccentStrong, UiKit.Display, false);
            // Which build this is, for checking what a phone is running: under the Play button.
            menu.Build = UiKit.Label(root, "Build", 20, TextAnchor.MiddleCenter, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 34), new Vector2(width, 28), UiKit.Body, false);
            menu.Build.color = UiKit.InkMuted;
            menu.PlayText = menu.Play.GetComponentInChildren<Text>();
            return menu;
        }

        /// The golfer picker: the figure stands in the top half of the screen (the camera frames
        /// it), and a sheet at the bottom holds body, skin, hair style and hair colour. Every
        /// tap changes the figure at once.
        public sealed class GolferPicker
        {
            public HoldButton Male, Female, Done;
            public HoldButton[] Skins, Hairs, HairColors;
            internal RectTransform Root, SkinRing, HairColorRing;
            internal Image MaleFill, FemaleFill;
            internal Text MaleText, FemaleText;
            internal Image[] HairFills; internal Text[] HairTexts;

            public void Refresh(bool female, int skin, int hair, int hairColor)
            {
                Male.RestColor = MaleFill.color = female ? Color.clear : UiKit.AccentStrong;
                Female.RestColor = FemaleFill.color = female ? UiKit.AccentStrong : Color.clear;
                MaleText.color = female ? UiKit.InkMuted : UiKit.Ink;
                FemaleText.color = female ? UiKit.Ink : UiKit.InkMuted;
                SkinRing.anchoredPosition = ((RectTransform)Skins[skin].transform).anchoredPosition;
                for (int i = 0; i < Hairs.Length; i++)
                {
                    Hairs[i].RestColor = HairFills[i].color = i == hair ? UiKit.AccentStrong : Color.clear;
                    HairTexts[i].color = i == hair ? UiKit.Ink : UiKit.InkMuted;
                }
                HairColorRing.anchoredPosition = ((RectTransform)HairColors[hairColor].transform).anchoredPosition;
            }
        }

        GolferPicker picker;

        public GolferPicker ShowGolferPicker(Color[] skinTones, string[] hairNames, Color[] hairColors)
        {
            HideGolferPicker();
            const float left = 70, width = 940, height = 1040;
            var sheet = Panel("Golfer picker", UiKit.Surface, new Vector2(0, 0), new Vector2(1, 0), Vector2.zero, new Vector2(0, height));
            sheet.color = new Color(UiKit.Ground.r, UiKit.Ground.g, UiKit.Ground.b, 0.94f);
            var root = sheet.rectTransform;
            picker = new GolferPicker { Root = root };
            Text T(string name, string text, int size, Font face, Color color, float x, float y, float w)
            {
                var t = UiKit.Label(root, name, size, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, y), new Vector2(w, size * 1.3f), face, false);
                t.text = text; t.color = color; return t;
            }
            // A row of pills in one track: the selected one is filled.
            HoldButton[] Segmented(string name, string[] labels, float y, out Image[] fills, out Text[] texts)
            {
                var track = UiKit.Panel(root, name, UiKit.Surface, new Vector2(0, 1), new Vector2(0, 1), new Vector2(left, y), new Vector2(width, 92));
                var edge = UiKit.Panel(track.transform, "Edge", UiKit.Hairline, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero); edge.raycastTarget = false;
                var inner = UiKit.Panel(track.transform, "Inner", UiKit.Surface, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                inner.rectTransform.offsetMin = new Vector2(2, 2); inner.rectTransform.offsetMax = new Vector2(-2, -2); inner.raycastTarget = false;
                var holds = new HoldButton[labels.Length]; fills = new Image[labels.Length]; texts = new Text[labels.Length];
                float cell = (width - 12) / labels.Length;
                for (int i = 0; i < labels.Length; i++)
                {
                    var b = UiKit.Panel(track.transform, labels[i], Color.clear, Vector2.zero, Vector2.zero, new Vector2(6 + cell * i + 2, 6), new Vector2(cell - 4, 80));
                    fills[i] = b;
                    var hold = b.gameObject.AddComponent<HoldButton>(); hold.Fill = b; hold.RestColor = Color.clear;
                    texts[i] = UiKit.Label(b.transform, "Label", 30, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, UiKit.Strong, false);
                    texts[i].text = labels[i]; texts[i].raycastTarget = false;
                    holds[i] = hold;
                }
                return holds;
            }
            HoldButton[] Dots(string name, Color[] colors, float y, out RectTransform ringRect)
            {
                float step = Mathf.Min(112, (width - 80) / (colors.Length - 1)), span = step * (colors.Length - 1);
                var ring = UiKit.Panel(root, name + " ring", UiKit.Ink, new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(94, 94));
                ring.sprite = UiKit.Circle; ring.type = Image.Type.Simple; ring.rectTransform.pivot = new Vector2(0.5f, 0.5f); ring.raycastTarget = false;
                ringRect = ring.rectTransform;
                var holds = new HoldButton[colors.Length];
                for (int i = 0; i < colors.Length; i++)
                {
                    var dot = UiKit.Panel(root, $"{name} {i}", colors[i], new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-span / 2 + step * i, y - 40), new Vector2(76, 76));
                    dot.sprite = UiKit.Circle; dot.type = Image.Type.Simple; dot.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    var hold = dot.gameObject.AddComponent<HoldButton>(); hold.Fill = dot; hold.RestColor = colors[i];
                    holds[i] = hold;
                }
                return holds;
            }

            float y = -44;
            T("Title", "Your golfer", 52, UiKit.Display, UiKit.Ink, left, y, width);
            y -= 90;
            T("Body heading", "BODY", 26, UiKit.Strong, UiKit.InkMuted, left, y, width); y -= 46;
            var bodies = Segmented("Body", new[] { "Male", "Female" }, y, out var bodyFills, out var bodyTexts);
            picker.Male = bodies[0]; picker.Female = bodies[1]; picker.MaleFill = bodyFills[0]; picker.FemaleFill = bodyFills[1]; picker.MaleText = bodyTexts[0]; picker.FemaleText = bodyTexts[1];
            y -= 92 + 40;
            T("Skin heading", "SKIN", 26, UiKit.Strong, UiKit.InkMuted, left, y, width); y -= 46;
            picker.Skins = Dots("Skin", skinTones, y, out picker.SkinRing);
            y -= 80 + 40;
            T("Hair heading", "HAIR", 26, UiKit.Strong, UiKit.InkMuted, left, y, width); y -= 46;
            picker.Hairs = Segmented("Hair", hairNames, y, out picker.HairFills, out picker.HairTexts);
            y -= 92 + 40;
            T("Hair colour heading", "HAIR COLOUR", 26, UiKit.Strong, UiKit.InkMuted, left, y, width); y -= 46;
            picker.HairColors = Dots("Hair colour", hairColors, y, out picker.HairColorRing);
            picker.Done = UiKit.Button(root, "Done", new Vector2(0.5f, 0), new Vector2(0, 60 + 60), new Vector2(width, 120), 44, UiKit.AccentStrong, UiKit.Display, false);
            return picker;
        }

        public void HideGolferPicker()
        {
            if (picker != null && picker.Root) Destroy(picker.Root.gameObject);
            picker = null;
        }

        /// The card over the hole's showcase: its render, name, par and yardage, a line about it
        /// and today's wind — the picker's card, presented. Letterbox bars make the cinematic.
        CanvasGroup introGroup;

        public void ShowHoleIntro(int number, string name, int par, double yards, string picture, string blurb, string wind)
        {
            HideHoleIntro();
            var go = new GameObject("Hole intro");
            go.transform.SetParent(safeArea, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            introGroup = go.AddComponent<CanvasGroup>();
            introGroup.alpha = 0; introGroup.blocksRaycasts = false; introGroup.interactable = false;
            var bars = UiKit.Ground;
            UiKit.Panel(go.transform, "Bar top", bars, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -120), new Vector2(0, 120), rounded: false).raycastTarget = false;
            UiKit.Panel(go.transform, "Bar bottom", bars, new Vector2(0, 0), new Vector2(1, 0), Vector2.zero, new Vector2(0, 120), rounded: false).raycastTarget = false;

            const float width = 940, height = 340, pic = 252;
            var card = Card("Card", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 160), new Vector2(width, height), new Color(UiKit.Ground.r, UiKit.Ground.g, UiKit.Ground.b, 0.9f), go.transform);
            card.rectTransform.pivot = new Vector2(0.5f, 0);
            var frame = UiKit.Panel(card.transform, "Picture", Color.white, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(24, 0), new Vector2(pic * 1.5f, pic));
            frame.rectTransform.pivot = new Vector2(0, 0.5f); frame.raycastTarget = false;
            frame.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var img = new GameObject("Render").AddComponent<RawImage>();
            img.transform.SetParent(frame.transform, false);
            img.rectTransform.anchorMin = Vector2.zero; img.rectTransform.anchorMax = Vector2.one; img.rectTransform.offsetMin = img.rectTransform.offsetMax = Vector2.zero;
            img.texture = Resources.Load<Texture2D>(picture); img.raycastTarget = false;
            if (!img.texture) img.color = UiKit.SurfaceRaised;
            float x = 24 + pic * 1.5f + 28, w = width - x - 24;
            var eyebrow = UiKit.Label(card.transform, "Eyebrow", 26, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -30), new Vector2(w, 34), UiKit.Strong, false);
            eyebrow.text = $"HOLE {number}   ·   PAR {par}   ·   {yards:F0} YD"; eyebrow.color = UiKit.Accent;
            var title = UiKit.Label(card.transform, "Title", 56, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x - 2, -64), new Vector2(w, 70), UiKit.Display, false);
            title.text = name;
            var text = UiKit.Label(card.transform, "Blurb", 24, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -134), new Vector2(w, 132), UiKit.Body, false);
            text.text = blurb; text.color = UiKit.InkMuted; text.horizontalOverflow = HorizontalWrapMode.Wrap; text.lineSpacing = 1.08f;
            var windLine = UiKit.Label(card.transform, "Wind", 25, TextAnchor.LowerLeft, new Vector2(0, 0), new Vector2(0, 0), new Vector2(x, 22), new Vector2(w, 34), UiKit.Strong, false);
            windLine.text = wind; windLine.color = UiKit.Ink;
        }

        public void SetHoleIntroAlpha(float alpha) { if (introGroup) introGroup.alpha = alpha; }

        public void HideHoleIntro()
        {
            if (introGroup) Destroy(introGroup.gameObject);
            introGroup = null;
        }

        public void HideMenu()
        {
            if (menu != null && menu.Root) Destroy(menu.Root.gameObject);
            menu = null;
        }

        /// Show or hide everything but the menu (the cards, meter and map stay out of the way).
        public void ShowPlayHud(bool on)
        {
            foreach (Transform child in safeArea)
                if (child.name != "Menu" && child.name != "Golfer picker" && child.name != "Scorecard" && child.name != "Banner" && child.name != "Hole intro") child.gameObject.SetActive(on);
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
            public Vector3 Ball, Landing, Load;
            public bool ShowBall, ShowLanding, ShowLoad;
            internal int Version;
            public void Changed() => Version++;
        }
        public MapPlan Map { get; } = new();

        RectTransform mapBall, mapLanding, mapLoad, mapShapes;
        Image mapZoneFill, mapZoneEdge;
        readonly System.Collections.Generic.List<RectTransform> mapDots = new(), mapReach = new();
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
            mapLoad = MapMark(map, "Load", amber, 18, UiKit.Circle, true);
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
            Place(mapBall, plan.Ball, plan.ShowBall);
            Place(mapLanding, plan.Landing, plan.ShowLanding);
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

        /// The read, in the wind line's place while putting: no wind matters on the green.
        public void SetRead(string text)
        {
            windText.text = text;
            windArrow.enabled = false;
        }

        void Row(float y, string hole, string par, string score, int size, Color color)
        {
            var a = Label("Hole", size, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(90, y), new Vector2(300, 60), scorecard);
            var b = Label("Par", size, TextAnchor.UpperCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(60, y), new Vector2(200, 60), scorecard);
            var c = Label("Score", size, TextAnchor.UpperRight, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-90, y), new Vector2(200, 60), scorecard);
            a.text = hole; b.text = par; c.text = score;
            a.color = b.color = c.color = color;
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

        public void SetHole(int number, int par, double yards, string picture = null)
        {
            holeText.text = $"HOLE {number}   ·   PAR {par}   ·   {yards:F0} YD";
            Controller?.SetHole(number, par, picture);
        }
        public void SetScore(int strokes, int toPar, int holeStrokes)
        {
            scoreText.text = $"{holeStrokes} strokes   {(toPar > 0 ? "+" : "")}{(toPar == 0 ? "E" : toPar.ToString())}";
            Controller?.SetScore(toPar, holeStrokes);
        }
        public void SetDistance(string text) => distanceText.text = text;
        public void SetClub(string text) => clubText.text = text;
        public void SetStatus(string text) => statusText.text = text;
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
            meterYards.enabled = yards != null && meterLoad > 0.02f;
            if (meterYards.enabled) { meterYards.text = yards; meterYards.rectTransform.anchoredPosition = new Vector2(94, 6 + meterLoad * 888); }
            meterFill.rectTransform.sizeDelta = new Vector2(48, meterLoad * 888);
            var calm = new Color(0.35f, 0.85f, 0.35f, 0.95f);
            var warm = new Color(1f, 0.9f, 0.25f, 0.95f);
            var hot = new Color(1f, 0.55f, 0.2f);
            meterFill.color = load > 0.98f ? hot : meterLoad < 0.6f ? Color.Lerp(calm, warm, meterLoad / 0.6f) : Color.Lerp(warm, hot, (meterLoad - 0.6f) / 0.4f);
            Controller?.SetMeter(meterLoad, meterFill.color);
            meterMark.enabled = mark.HasValue;
            if (mark.HasValue) meterMark.rectTransform.anchoredPosition = new Vector2(0, 6 + Mathf.Clamp01(mark.Value) * 888);
        }

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
        public void EnterControllerLayout()
        {
            if (Controller != null && Controller.Alive) return;
            Controller = new ControllerSheet(safeArea);
            Controller.AimLeft.Pressed = AimLeft.Pressed; Controller.AimRight.Pressed = AimRight.Pressed;
            Controller.ClubUp.Pressed = ClubUp.Pressed; Controller.ClubDown.Pressed = ClubDown.Pressed;
            Controller.Knob.Pressed = SwingHold.Pressed; Controller.Knob.Released = SwingHold.Released;
            foreach (var rt in new[] { holeCard, scoreCard, shotCard, meterRect }) rt.gameObject.SetActive(false);
            ((RectTransform)Minimap.transform.parent.parent).anchoredPosition = new Vector2(-30, -36);
            statusText.rectTransform.anchoredPosition = new Vector2(0, ControllerSheet.SheetHeight + 190);
            tempoText.rectTransform.anchoredPosition = new Vector2(0, ControllerSheet.SheetHeight + 140);
        }

        public void LeaveControllerLayout()
        {
            if (Controller == null) return;
            Controller.Destroy(); Controller = null;
            foreach (var rt in new[] { holeCard, scoreCard, shotCard, meterRect }) rt.gameObject.SetActive(true);
            ((RectTransform)Minimap.transform.parent.parent).anchoredPosition = new Vector2(-30, -150);
            statusText.rectTransform.anchoredPosition = new Vector2(0, 420);
            tempoText.rectTransform.anchoredPosition = new Vector2(0, 360);
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
