using UnityEngine;
using UnityEngine.UI;

namespace GolfArcade.Tennis
{
    /// The toss meter, flat on the court under the server's feet: a bar that is red at both
    /// ends and fades to green in the middle, with a ticker sweeping back and forth across it.
    /// The player presses TOSS on the phone; the closer the ticker is to the middle, the closer
    /// the serve lands to where they aimed. It is judged here, against this meter, at the
    /// moment the player saw it (the TV's measured delay is taken off), so what they see is
    /// what counts.
    public sealed class TennisTossMeter : MonoBehaviour
    {
        /// Seconds for the ticker to go end to end and back. It moves at a steady speed (not
        /// easing at the ends like a pendulum), so a given distance from the middle is always
        /// the same number of milliseconds and the timing can be learned.
        public const float Period = 1.6f;
        /// End-to-end speed, in half-widths per second.
        public const float Speed = 4 / Period;
        const float Width = 2.0f, Depth = .3f;

        RectTransform ticker, frame;
        Canvas canvas;
        float clock, frozenAt = -1, frozenValue, shownFor;
        bool running;

        public static TennisTossMeter Create(Transform owner)
        {
            var go = new GameObject("Toss meter");
            var meter = go.AddComponent<TennisTossMeter>();
            meter.Build();
            go.SetActive(false);
            return meter;
        }

        /// The ticker's position, -1 (left end) .. 1 (right end), `ago` seconds before now.
        /// It starts at the left end, so the first pass through the middle comes after a beat.
        public float Value(float ago = 0) => Triangle((clock - ago) / Period);

        static float Triangle(float cycles)
        {
            float p = Mathf.Repeat(cycles + .75f, 1f);
            return p < .25f ? 4 * p : p < .75f ? 2 - 4 * p : 4 * p - 4;
        }

        /// Which way the ticker was moving `ago` seconds before now (+1 right, -1 left).
        public float Direction(float ago = 0)
        {
            float p = Mathf.Repeat((clock - ago) / Period + .75f, 1f);
            return p < .25f || p >= .75f ? 1 : -1;
        }

        /// How late (seconds; negative = early) a press read at `ago` was against the nearest
        /// pass through the middle.
        public float Lateness(float ago = 0) => Value(ago) * Direction(ago) / Speed;

        /// Show it running under `feet`.
        public void Run(Vector3 feet)
        {
            if (!running) { clock = 0; frozenAt = -1; }
            running = true; gameObject.SetActive(true);
            Place(feet);
        }

        public void Place(Vector3 feet) => transform.position = feet + new Vector3(0, .02f, -.42f);

        /// TOSS: read the meter as the player saw it `seenAgo` seconds ago, freeze it there for
        /// a moment, and return the accuracy (1 = dead centre).
        public float Press(float seenAgo)
        {
            frozenValue = Mathf.Clamp(Value(seenAgo), -1, 1);
            frozenAt = clock; running = false;
            return 1 - Mathf.Abs(frozenValue);
        }

        public void Hide() { running = false; frozenAt = -1; gameObject.SetActive(false); }

        void Update()
        {
            if (running) clock += Time.deltaTime;
            else if (frozenAt >= 0) { shownFor = Time.deltaTime; clock += shownFor; if (clock - frozenAt > .9f) Hide(); }
            float v = frozenAt >= 0 ? frozenValue : Value();
            ticker.anchoredPosition = new Vector2(v * (Width * 100 / 2 - 6), 0);
            // A pop on the press.
            float since = frozenAt >= 0 ? clock - frozenAt : 1;
            frame.localScale = Vector3.one * (since < .3f ? 1 + .12f * (1 - since / .3f) : 1);
        }

        void Build()
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)transform;
            rt.sizeDelta = new Vector2(Width * 100, Depth * 100);
            rt.localScale = Vector3.one * .01f;
            // Lying on the court, readable from behind the server.
            rt.rotation = Quaternion.Euler(90, 0, 0);

            frame = Panel("Frame", rt, new Vector2(Width * 100 + 10, Depth * 100 + 10), new Color(.03f, .06f, .2f, .95f));
            var bar = Panel("Bar", frame, new Vector2(Width * 100, Depth * 100), Color.white);
            bar.GetComponent<Image>().sprite = GradientSprite();
            // The perfect zone, edged in gold: a press inside it is a perfect toss.
            float zone = Width * 100 * (1 - TennisRules.ServePerfectToss);
            Panel("Perfect zone", bar, new Vector2(zone, Depth * 100), new Color(.75f, 1f, .7f, .55f));
            foreach (float side in new[] { -1f, 1f })
                Panel("Perfect edge", bar, new Vector2(4, Depth * 100 + 10), new Color(1f, .82f, .2f)).anchoredPosition = new Vector2(side * zone / 2, 0);
            // A bold gold needle: plain white reads grey once the court's grading is applied.
            ticker = Panel("Ticker", bar, new Vector2(17, Depth * 100 + 32), Color.clear);
            Panel("Ticker edge", ticker, new Vector2(17, Depth * 100 + 32), new Color(.03f, .06f, .2f, 1f));
            Panel("Ticker needle", ticker, new Vector2(10, Depth * 100 + 25), new Color(1f, .93f, .35f));
        }

        static RectTransform Panel(string name, RectTransform parent, Vector2 size, Color color)
        {
            var img = new GameObject(name).AddComponent<Image>();
            img.transform.SetParent(parent, false);
            img.color = color; img.raycastTarget = false;
            var rt = img.rectTransform; rt.sizeDelta = size; rt.anchoredPosition = Vector2.zero;
            return rt;
        }

        /// Red at the ends, through orange and yellow, to green in the middle.
        static Sprite GradientSprite()
        {
            var t = new Texture2D(256, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            Color red = new(.92f, .10f, .12f), orange = new(.98f, .45f, .10f), yellow = new(.96f, .86f, .16f), green = new(.18f, .86f, .26f);
            for (int x = 0; x < 256; x++)
            {
                float d = Mathf.Abs(x / 255f - .5f) * 2;   // 0 middle .. 1 ends
                Color c = d < .28f ? Color.Lerp(green, yellow, d / .28f)
                        : d < .6f ? Color.Lerp(yellow, orange, (d - .28f) / .32f)
                        : Color.Lerp(orange, red, (d - .6f) / .4f);
                t.SetPixel(x, 0, c);
            }
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, 256, 1), new Vector2(.5f, .5f), 100);
        }
    }
}
