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
    /// score, distance and club, a Wii-style power meter that fills with the backswing, aim and
    /// club buttons, a banner for results, and a minimap.
    public sealed class Hud : MonoBehaviour
    {
        public HoldButton AimLeft, AimRight, ClubUp, ClubDown, SwingHold;
        public RawImage Minimap;

        Text holeText, scoreText, distanceText, clubText, statusText, bannerText, tempoText;
        Image meterFill, meterMark, meterOverswing;
        CanvasGroup bannerGroup;
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

            // Top bar: hole, then score.
            holeText = Label("Hole", 48, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -60), new Vector2(700, 60));
            scoreText = Label("Score", 40, TextAnchor.UpperRight, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -60), new Vector2(500, 60));
            distanceText = Label("Distance", 54, TextAnchor.UpperCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(900, 70));
            clubText = Label("Club", 44, TextAnchor.UpperCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -225), new Vector2(900, 60));
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
            mapGo.transform.SetParent(transform, false);
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
            if (bannerGroup.alpha > 0 && Time.time > bannerUntil)
                bannerGroup.alpha = Mathf.MoveTowards(bannerGroup.alpha, 0, Time.deltaTime * 3);
        }

        Text Label(string name, int size, TextAnchor anchor, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 sizeDelta, Transform parent = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent ? parent : transform, false);
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
            go.transform.SetParent(parent ? parent : transform, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            var rt = img.rectTransform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = anchorMin;
            rt.anchoredPosition = pos; rt.sizeDelta = sizeDelta;
            return img;
        }

        HoldButton Button(string label, Vector2 anchor, Vector2 pos, Vector2 size, int fontSize = 80)
        {
            var img = Panel(label, new Color(1, 1, 1, 0.22f), anchor, anchor, pos, size);
            img.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var hold = img.gameObject.AddComponent<HoldButton>();
            var t = Label("Label", fontSize, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size, img.transform);
            t.text = label;
            t.raycastTarget = false;
            return hold;
        }
    }
}
