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
        public void OnPointerDown(PointerEventData e) { IsHeld = true; Pressed?.Invoke(); }
        public void OnPointerUp(PointerEventData e) { if (IsHeld) Released?.Invoke(); IsHeld = false; }
        public void OnPointerExit(PointerEventData e) { if (IsHeld) Released?.Invoke(); IsHeld = false; }
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
        Text holeText, scoreText, distanceText, clubText, statusText, bannerText, tempoText, windText;
        Image meterFill, meterMark, meterOverswing, windArrow;
        CanvasGroup bannerGroup;
        RectTransform scorecard;
        float bannerUntil;
        Font font;

        public static Hud Create()
        {
            var go = new GameObject("HUD");
            var hud = go.AddComponent<Hud>();
            hud.Build();
            return hud;
        }

        void Build()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 2340);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            if (!FindFirstObjectByType<EventSystem>())
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }

            // Every element hangs off this, and it shrinks to the phone's safe area.
            safeArea = new GameObject("Safe area").AddComponent<RectTransform>();
            safeArea.SetParent(transform, false);
            safeArea.anchorMin = Vector2.zero; safeArea.anchorMax = Vector2.one;
            safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
            ApplySafeArea();

            // Top bar: hole, then score.
            holeText = Label("Hole", 48, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -60), new Vector2(700, 60));
            scoreText = Label("Score", 40, TextAnchor.UpperRight, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -60), new Vector2(500, 60));
            distanceText = Label("Distance", 54, TextAnchor.UpperCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(900, 70));
            clubText = Label("Club", 44, TextAnchor.UpperCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -225), new Vector2(900, 60));

            // Wind: an arrow that points where the wind blows, as seen from behind the ball.
            windArrow = Panel("Wind arrow", new Color(1f, 1f, 1f, 0.95f), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-380, -345), new Vector2(80, 80));
            windArrow.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            windArrow.sprite = ArrowSprite();
            windText = Label("Wind", 38, TextAnchor.MiddleLeft, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-80, -315), new Vector2(500, 60));
            windText.color = new Color(0.85f, 0.95f, 1f);
            statusText = Label("Status", 40, TextAnchor.MiddleCenter, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 420), new Vector2(1000, 60));
            statusText.color = new Color(1, 1, 1, 0.9f);
            tempoText = Label("Tempo", 34, TextAnchor.MiddleCenter, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 360), new Vector2(1000, 50));
            tempoText.color = new Color(1, 0.95f, 0.7f, 0.95f);

            // Power meter, left edge: a tall bar that fills from the bottom.
            var meterBg = Panel("Meter", new Color(0, 0, 0, 0.45f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(70, 0), new Vector2(60, 900));
            meterOverswing = Panel("Overswing", new Color(0.9f, 0.2f, 0.15f, 0.5f), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -6), new Vector2(48, 110), meterBg.transform);
            meterFill = Panel("Fill", new Color(0.35f, 0.85f, 0.35f, 0.95f), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 6), new Vector2(48, 0), meterBg.transform);
            meterFill.rectTransform.pivot = new Vector2(0.5f, 0);
            meterMark = Panel("Mark", Color.white, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 6), new Vector2(56, 6), meterBg.transform);
            meterMark.enabled = false;
            var meterLabel = Label("Power", 30, TextAnchor.MiddleCenter, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, -30), new Vector2(200, 40), meterBg.transform);
            meterLabel.text = "POWER";

            // Minimap, top right under the score.
            var mapGo = new GameObject("Minimap");
            mapGo.transform.SetParent(safeArea, false);
            Minimap = mapGo.AddComponent<RawImage>();
            var mapRt = Minimap.rectTransform;
            mapRt.anchorMin = mapRt.anchorMax = new Vector2(1, 1);
            mapRt.pivot = new Vector2(1, 1);
            mapRt.anchoredPosition = new Vector2(-30, -130);
            mapRt.sizeDelta = new Vector2(260, 420);
            Minimap.color = new Color(1, 1, 1, 0.92f);

            // Buttons along the bottom.
            AimLeft = Button("◀", new Vector2(0, 0), new Vector2(150, 170), new Vector2(240, 240));
            AimRight = Button("▶", new Vector2(1, 0), new Vector2(-150, 170), new Vector2(240, 240));
            ClubUp = Button("▲", new Vector2(0.5f, 0), new Vector2(-95, 200), new Vector2(150, 110));
            ClubDown = Button("▼", new Vector2(0.5f, 0), new Vector2(95, 200), new Vector2(150, 110));
            SwingHold = Button("HOLD TO SWING", new Vector2(0.5f, 0), new Vector2(0, 90), new Vector2(360, 90), 30);

            // Banner in the middle.
            var bannerGo = Panel("Banner", new Color(0, 0, 0, 0.55f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 250), new Vector2(900, 140));
            bannerGroup = bannerGo.gameObject.AddComponent<CanvasGroup>();
            bannerGroup.alpha = 0;
            bannerText = Label("BannerText", 56, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(880, 130), bannerGo.transform);

            // Scorecard, filled in when the round ends.
            var card = Panel("Scorecard", new Color(0.05f, 0.12f, 0.06f, 0.9f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 600));
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
            foreach (Transform child in scorecard) Destroy(child.gameObject);
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

        public void SetHole(int number, int par, double yards) => holeText.text = $"Hole {number}  ·  Par {par}  ·  {yards:F0} yd";
        public void SetScore(int strokes, int toPar, int holeStrokes) => scoreText.text = $"{holeStrokes} strokes   {(toPar > 0 ? "+" : "")}{(toPar == 0 ? "E" : toPar.ToString())}";
        public void SetDistance(string text) => distanceText.text = text;
        public void SetClub(string text) => clubText.text = text;
        public void SetStatus(string text) => statusText.text = text;
        public void SetTempo(string text) => tempoText.text = text;

        public void SetMeter(float load, float? mark = null)
        {
            meterFill.rectTransform.sizeDelta = new Vector2(48, Mathf.Clamp01(load) * 888);
            meterFill.color = load > 0.98f ? new Color(1f, 0.55f, 0.2f) : new Color(0.35f, 0.85f, 0.35f, 0.95f);
            meterMark.enabled = mark.HasValue;
            if (mark.HasValue) meterMark.rectTransform.anchoredPosition = new Vector2(0, 6 + Mathf.Clamp01(mark.Value) * 888);
        }

        public void ShowBanner(string text, float seconds = 2f)
        {
            bannerText.text = text;
            bannerGroup.alpha = 1;
            bannerUntil = Time.time + seconds;
        }

        public void ShowSwingControls(bool aiming, bool debugSwingButton)
        {
            AimLeft.gameObject.SetActive(aiming);
            AimRight.gameObject.SetActive(aiming);
            ClubUp.gameObject.SetActive(aiming);
            ClubDown.gameObject.SetActive(aiming);
            SwingHold.gameObject.SetActive(aiming && debugSwingButton);
        }

        void Update()
        {
            ApplySafeArea();
            if (bannerGroup.alpha > 0 && Time.time > bannerUntil)
                bannerGroup.alpha = Mathf.MoveTowards(bannerGroup.alpha, 0, Time.deltaTime * 3);
        }

        Text Label(string name, int size, TextAnchor anchor, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 sizeDelta, Transform parent = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent ? parent : safeArea, false);
            var t = go.AddComponent<Text>();
            t.font = font; t.fontSize = size; t.alignment = anchor; t.color = Color.white;
            t.fontStyle = FontStyle.Bold;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.7f); shadow.effectDistance = new Vector2(2, -2);
            var rt = t.rectTransform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = anchorMin;
            rt.anchoredPosition = pos; rt.sizeDelta = sizeDelta;
            return t;
        }

        Image Panel(string name, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 sizeDelta, Transform parent = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent ? parent : safeArea, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            var rt = img.rectTransform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = anchorMin;
            rt.anchoredPosition = pos; rt.sizeDelta = sizeDelta;
            return img;
        }

        HoldButton Button(string label, Vector2 anchor, Vector2 pos, Vector2 size, int fontSize = 80, Transform parent = null)
        {
            var img = Panel(label, new Color(1, 1, 1, 0.22f), anchor, anchor, pos, size, parent);
            img.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var hold = img.gameObject.AddComponent<HoldButton>();
            var t = Label("Label", fontSize, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size, img.transform);
            t.text = label;
            t.raycastTarget = false;
            return hold;
        }
    }
}
