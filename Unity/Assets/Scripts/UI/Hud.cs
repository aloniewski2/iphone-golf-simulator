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
        public HoldButton AimLeft, AimRight, ClubUp, ClubDown, SwingHold, PlayAgain, GolferBody, GolferSkin;
        public RawImage Minimap;

        RectTransform safeArea;
        Rect appliedSafeArea;
        Text holeText, scoreText, distanceText, clubText, statusText, bannerText, tempoText, windText, controllerText;
        Image meterFill, meterMark, meterOverswing, windArrow, golferSkinSwatch;
        RectTransform meterRect;
        Vector2 meterHome;
        float meterLoad;
        CanvasGroup bannerGroup;
        RectTransform scorecard;
        float bannerUntil;

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
            var holeCard = Card("Hole card", new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -40), new Vector2(640, 90));
            holeCard.rectTransform.pivot = new Vector2(0, 1);
            holeText = Label("Hole", 40, TextAnchor.MiddleLeft, new Vector2(0, 0), new Vector2(1, 1), new Vector2(28, 0), Vector2.zero, holeCard.transform);
            var scoreCard = Card("Score card", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-30, -40), new Vector2(360, 90));
            scoreCard.rectTransform.pivot = new Vector2(1, 1);
            scoreText = Label("Score", 38, TextAnchor.MiddleRight, new Vector2(0, 0), new Vector2(1, 1), new Vector2(-28, 0), Vector2.zero, scoreCard.transform);

            // Shot card: distance to the pin, club and its yardage, wind.
            var shotCard = Card("Shot card", new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -150), new Vector2(640, 210));
            shotCard.rectTransform.pivot = new Vector2(0, 1);
            distanceText = Label("Distance", 52, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -18), new Vector2(600, 60), shotCard.transform);
            clubText = Label("Club", 36, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -84), new Vector2(600, 50), shotCard.transform);
            clubText.color = UiKit.Muted;
            windArrow = Panel("Wind arrow", new Color(1f, 1f, 1f, 0.95f), new Vector2(0, 0), new Vector2(0, 0), new Vector2(60, 40), new Vector2(56, 56), shotCard.transform);
            windArrow.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            windArrow.sprite = ArrowSprite(); windArrow.type = Image.Type.Simple;
            windText = Label("Wind", 32, TextAnchor.MiddleLeft, new Vector2(0, 0), new Vector2(0, 0), new Vector2(100, 14), new Vector2(500, 50), shotCard.transform);
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
            GolferBody = Button("GOLFER ♂", new Vector2(1, 1), new Vector2(-166, -630), new Vector2(272, 70), 28);
            GolferSkin = Button("SKIN", new Vector2(1, 1), new Vector2(-166, -712), new Vector2(272, 70), 28);
            golferSkinSwatch = Panel("Swatch", Color.white, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-12, 0), new Vector2(40, 40), GolferSkin.transform);
            golferSkinSwatch.rectTransform.pivot = new Vector2(1, 0.5f);
            golferSkinSwatch.raycastTarget = false;

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
        public void SetWind(float relativeDegrees, string label, bool calm)
        {
            windText.text = label;
            windArrow.enabled = !calm;
            windArrow.rectTransform.localRotation = Quaternion.Euler(0, 0, -relativeDegrees);
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

        public void SetHole(int number, int par, double yards) => holeText.text = $"HOLE {number}   ·   PAR {par}   ·   {yards:F0} YD";
        public void SetScore(int strokes, int toPar, int holeStrokes) => scoreText.text = $"{holeStrokes} strokes   {(toPar > 0 ? "+" : "")}{(toPar == 0 ? "E" : toPar.ToString())}";
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
        public void SetMeter(float load, float? mark = null)
        {
            meterLoad = Mathf.Clamp01(load);
            meterFill.rectTransform.sizeDelta = new Vector2(48, meterLoad * 888);
            var calm = new Color(0.35f, 0.85f, 0.35f, 0.95f);
            var warm = new Color(1f, 0.9f, 0.25f, 0.95f);
            var hot = new Color(1f, 0.55f, 0.2f);
            meterFill.color = load > 0.98f ? hot : meterLoad < 0.6f ? Color.Lerp(calm, warm, meterLoad / 0.6f) : Color.Lerp(warm, hot, (meterLoad - 0.6f) / 0.4f);
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
            GolferBody.gameObject.SetActive(aiming);
            GolferSkin.gameObject.SetActive(aiming);
            AimLeft.gameObject.SetActive(aiming && touchButtons);
            AimRight.gameObject.SetActive(aiming && touchButtons);
            ClubUp.gameObject.SetActive(aiming && touchButtons);
            ClubDown.gameObject.SetActive(aiming && touchButtons);
            SwingHold.gameObject.SetActive(aiming && debugSwingButton);
        }

        public void SetGolferStyle(string bodyLabel, Color skin)
        {
            GolferBody.GetComponentInChildren<Text>().text = $"GOLFER {bodyLabel}";
            golferSkinSwatch.color = skin;
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
