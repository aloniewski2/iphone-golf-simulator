using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GolfArcade.UI
{
    /// One card or chip of the round's events: a twist or the weather, as the HUD shows it.
    public readonly struct EventFace
    {
        public readonly string Title, Line, Icon;
        public readonly Color Colour;
        public EventFace(string title, string line, string icon, Color colour) { Title = title; Line = line; Icon = icon; Colour = colour; }
    }

    /// The icons the events need that UI/Icons doesn't have, painted in code at the same size and
    /// in the same white-on-clear style (Icons.Get falls back to these): a moon, the sun, a cloud,
    /// rain, a lightning bolt, a bouncing ball, a bending curve.
    public static class EventIcons
    {
        const int N = 128;
        static readonly Dictionary<string, Func<Vector2, float>> Shapes = new()
        {
            ["moon"] = p => Mathf.Max(Circle(p, new Vector2(0, 0), 0.38f), -Circle(p, new Vector2(0.17f, 0.11f), 0.32f)),
            ["sun"] = p =>
            {
                float d = Circle(p, Vector2.zero, 0.2f);
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI / 4;
                    var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    d = Mathf.Min(d, Segment(p, dir * 0.29f, dir * 0.42f) - 0.045f);
                }
                return d;
            },
            ["cloud"] = p => Cloud(p),
            ["rain"] = p =>
            {
                float d = Cloud(p - new Vector2(0, 0.14f));
                for (int i = -1; i <= 1; i++)
                    d = Mathf.Min(d, Segment(p, new Vector2(i * 0.17f + 0.04f, -0.12f), new Vector2(i * 0.17f - 0.04f, -0.34f)) - 0.035f);
                return d;
            },
            ["bolt"] = p => Polygon(p, new[] { new Vector2(0.06f, 0.44f), new Vector2(-0.22f, -0.02f), new Vector2(-0.02f, -0.02f), new Vector2(-0.1f, -0.44f), new Vector2(0.24f, 0.08f), new Vector2(0.04f, 0.08f) }),
            ["bounce"] = p =>
            {
                float d = Circle(p, new Vector2(0.28f, 0.22f), 0.13f);
                // the hops it took to get there, a dotted arc each
                for (int k = 0; k < 7; k++)
                {
                    float u = k / 6f;
                    var c = new Vector2(Mathf.Lerp(-0.4f, 0.12f, u), -0.3f + 0.46f * Mathf.Abs(Mathf.Sin(u * Mathf.PI * 1.5f)) * (0.55f + 0.45f * u));
                    d = Mathf.Min(d, Circle(p, c, 0.045f));
                }
                return Mathf.Min(d, Segment(p, new Vector2(-0.44f, -0.38f), new Vector2(0.44f, -0.38f)) - 0.025f);
            },
            ["curve"] = p =>
            {
                // a bending flight and its arrowhead
                float r = Mathf.Abs(Circle(p, new Vector2(0.42f, -0.1f), 0.62f)) - 0.05f;
                float sector = Mathf.Max(r, Mathf.Max(-(p.y + 0.36f), p.y - 0.3f));
                var tip = new Vector2(-0.07f, 0.36f);
                float head = Polygon(p, new[] { tip + new Vector2(0.0f, 0.12f), tip + new Vector2(-0.14f, -0.08f), tip + new Vector2(0.14f, -0.04f) });
                return Mathf.Min(Mathf.Min(sector, head), Circle(p, new Vector2(-0.1f, -0.34f), 0.08f));
            },
        };

        static readonly Dictionary<string, Sprite> painted = new();

        public static Sprite Paint(string name)
        {
            if (painted.TryGetValue(name, out var s)) return s;
            if (!Shapes.TryGetValue(name, out var shape)) return null;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "Icon " + name };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    var p = new Vector2((x + 0.5f) / N - 0.5f, (y + 0.5f) / N - 0.5f);
                    float a = Mathf.Clamp01(0.5f - shape(p) * N);
                    px[y * N + x] = new Color32(255, 255, 255, (byte)(255 * a));
                }
            tex.SetPixels32(px); tex.Apply();
            s = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f));
            painted[name] = s;
            return s;
        }

        static float Circle(Vector2 p, Vector2 c, float r) => (p - c).magnitude - r;
        static float Cloud(Vector2 p) => Mathf.Min(Mathf.Min(Circle(p, new Vector2(-0.18f, -0.02f), 0.16f), Circle(p, new Vector2(0.02f, 0.08f), 0.21f)),
                                                   Mathf.Min(Circle(p, new Vector2(0.21f, -0.03f), 0.15f), Box(p, new Vector2(0.01f, -0.09f), new Vector2(0.3f, 0.09f))));
        static float Box(Vector2 p, Vector2 c, Vector2 half) { var q = new Vector2(Mathf.Abs(p.x - c.x), Mathf.Abs(p.y - c.y)) - half; return Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0); }
        static float Segment(Vector2 p, Vector2 a, Vector2 b) { var pa = p - a; var ba = b - a; float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / ba.sqrMagnitude); return (pa - ba * h).magnitude; }
        static float Polygon(Vector2 p, Vector2[] v)
        {
            float d = float.MaxValue; bool inside = false;
            for (int i = 0, j = v.Length - 1; i < v.Length; j = i++)
            {
                d = Mathf.Min(d, Segment(p, v[j], v[i]));
                if ((v[i].y > p.y) != (v[j].y > p.y) && p.x < (v[j].x - v[i].x) * (p.y - v[i].y) / (v[j].y - v[i].y) + v[i].x) inside = !inside;
            }
            return inside ? -d : d;
        }
    }

    /// The round's twists dealt before the first tee shot, the way a party game deals its
    /// modifiers: a ROUND TWISTS tab, then a card for each, spinning through every twist there is
    /// like a slot machine, ratcheting slower and slower until it stamps down on the one dealt.
    /// It runs on its own clock over whatever the hole's opening shows, then tucks away into the
    /// HUD's chips (GolfShotHud.SetConditions).
    public sealed class TwistReveal : MonoBehaviour
    {
        public Action Ratchet, Stamp;
        RectTransform root, header;
        readonly List<Card> cards = new();
        EventFace[] dealt, pool;
        /// Seconds into the reveal, a frame's worth at most at a time: building the hole stalls the
        /// frame it starts in, and the cards still spin from the start.
        float elapsed;
        const float Hold = 4.4f;

        sealed class Card
        {
            public RectTransform Root;
            public Image Fill, Disc, Icon;
            public Text Title, Line;
            public float SpinFrom, LandAt, NextFlip;
            public int Face = -1; public bool Landed;
        }

        public static TwistReveal Create(Transform parent)
        {
            var go = new GameObject("Twist reveal", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var r = go.AddComponent<TwistReveal>();
            r.root = (RectTransform)go.transform;
            r.root.anchorMin = r.root.anchorMax = r.root.pivot = new Vector2(0.5f, 0.27f);
            r.root.sizeDelta = new Vector2(1000, 420);
            go.SetActive(false);
            return r;
        }

        public bool Showing => gameObject.activeSelf;

        /// Deal `twists` (spinning through `everything`), starting now.
        public void Show(EventFace[] twists, EventFace[] everything)
        {
            foreach (Transform child in root) Destroy(child.gameObject);
            cards.Clear();
            dealt = twists; pool = everything.Length > 0 ? everything : twists;
            if (dealt.Length == 0) { gameObject.SetActive(false); return; }
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            header = UiKit.Pill(root, "Header", UiKit.ArcadeBlueDeep, new Vector2(0.5f, 1f), new Vector2(0, -30), new Vector2(420, 64), out var headerFill, 4f);
            var word = UiKit.Chunky(headerFill.transform, "Word", 34, UiKit.ArcadeYellow, UiKit.ArcadeInk, 3f);
            word.text = "ROUND TWISTS";
            float width = 400, gap = 36, x0 = -(dealt.Length - 1) * (width + gap) / 2;
            for (int i = 0; i < dealt.Length; i++)
            {
                var c = new Card();
                c.Root = UiKit.Pill(root, "Card " + i, Color.white, new Vector2(0.5f, 0.5f), new Vector2(x0 + i * (width + gap), -40), new Vector2(width, 250), out c.Fill, 7f, false);
                foreach (var img in c.Root.GetComponentsInChildren<Image>()) img.sprite = UiKit.RoundedLarge;
                c.Disc = UiKit.Panel(c.Fill.transform, "Disc", Color.white, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -22), new Vector2(92, 92), false);
                c.Disc.sprite = UiKit.Circle; c.Disc.rectTransform.pivot = new Vector2(0.5f, 1f); c.Disc.raycastTarget = false;
                c.Icon = Icons.Place(c.Disc.transform, "star", Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, 62);
                c.Title = UiKit.Chunky(c.Fill.transform, "Title", 44, Color.white, UiKit.ArcadeInk, 3.5f);
                c.Title.rectTransform.offsetMin = new Vector2(16, 70); c.Title.rectTransform.offsetMax = new Vector2(-16, -120);
                Icons.Fit(c.Title, 26, 44);
                c.Line = UiKit.Label(c.Fill.transform, "Line", 22, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(1, 0), new Vector2(0, 14), new Vector2(0, 60), UiKit.Strong, false);
                c.Line.rectTransform.pivot = new Vector2(0.5f, 0); c.Line.rectTransform.offsetMin = new Vector2(22, 12); c.Line.rectTransform.offsetMax = new Vector2(-22, 70);
                Icons.Fit(c.Line, 15, 22);
                c.SpinFrom = 0.25f + 0.12f * i; c.LandAt = 1.25f + 0.6f * i;
                cards.Add(c);
            }
            foreach (var g in root.GetComponentsInChildren<Graphic>()) g.raycastTarget = false;
            elapsed = 0;
            Tick();
        }

        public void Hide() { gameObject.SetActive(false); }

        void Update() { elapsed += Mathf.Min(Time.unscaledDeltaTime, 1 / 20f); Tick(); }

        void Tick()
        {
            if (dealt == null) return;
            float t = elapsed;
            if (t > Hold + 0.35f) { gameObject.SetActive(false); return; }
            // the header pops in; at the end everything shrinks away toward the HUD's corner
            float leave = Mathf.Clamp01((t - Hold) / 0.35f);
            if (header) header.localScale = Vector3.one * Pop(t / 0.3f);
            root.localScale = Vector3.one * (1 - leave);
            root.anchoredPosition = new Vector2(-leave * 500, leave * 320);
            for (int i = 0; i < cards.Count; i++)
            {
                var c = cards[i];
                if (t < c.SpinFrom) { c.Root.localScale = Vector3.zero; continue; }
                if (!c.Landed && t >= c.LandAt)
                {
                    c.Landed = true; Dress(c, dealt[i]); Stamp?.Invoke();
                }
                if (!c.Landed && t >= c.NextFlip)
                {
                    // slower and slower as it comes to its stop
                    float left = Mathf.Clamp01((c.LandAt - t) / (c.LandAt - c.SpinFrom));
                    c.NextFlip = t + Mathf.Lerp(0.2f, 0.06f, left);
                    int face; do face = UnityEngine.Random.Range(0, pool.Length); while (pool.Length > 1 && face == c.Face);
                    c.Face = face; Dress(c, pool[face]); Ratchet?.Invoke();
                }
                float s = c.Landed ? Pop((t - c.LandAt) / 0.35f) * 1.0f : 0.92f + 0.04f * Mathf.Sin(t * 40f);
                if (!c.Landed && t - c.SpinFrom < 0.2f) s *= Pop((t - c.SpinFrom) / 0.2f);
                c.Root.localScale = Vector3.one * s;
                c.Root.localRotation = Quaternion.Euler(0, 0, c.Landed ? Mathf.Lerp(-6f, i % 2 == 0 ? -2f : 2f, Mathf.Clamp01((t - c.LandAt) / 0.3f)) : 0);
            }
        }

        static void Dress(Card c, EventFace f)
        {
            c.Fill.color = f.Colour;
            c.Icon.sprite = Icons.Get(f.Icon); c.Icon.color = f.Colour;
            c.Title.text = f.Title.ToUpperInvariant();
            c.Line.text = f.Line;
            bool light = f.Colour.grayscale > 0.62f;
            c.Line.color = light ? UiKit.ArcadeInk : Color.white;
        }

        /// An arcade pop: from small past full size and back, as `t` runs 0 → 1.
        static float Pop(float t)
        {
            t = Mathf.Clamp01(t);
            const float k = 1.9f;
            float u = t - 1;
            return 0.6f + 0.4f * (1 + (k + 1) * u * u * u + k * u * u);
        }
    }

    /// A word that pops over the picture when the ball meets something about the hole — BOING!
    /// off a balloon, BEEP BEEP! off a cart — and floats up and away.
    public sealed class Callout : MonoBehaviour
    {
        Text word; RectTransform rt; float shown = 99f;

        public static Callout Create(Transform parent)
        {
            var go = new GameObject("Callout", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var c = go.AddComponent<Callout>();
            c.rt = (RectTransform)go.transform;
            c.rt.anchorMin = c.rt.anchorMax = c.rt.pivot = new Vector2(0.5f, 0.72f);
            c.rt.sizeDelta = new Vector2(900, 140);
            c.word = UiKit.Chunky(go.transform, "Word", 86, Color.white, UiKit.ArcadeInk, 5f);
            c.word.raycastTarget = false;
            go.SetActive(false);
            return c;
        }

        public void Show(string text, Color colour)
        {
            word.text = text.ToUpperInvariant(); word.color = colour;
            shown = 0;
            gameObject.SetActive(true); transform.SetAsLastSibling();
            Pose();
        }

        void Update() { shown += Mathf.Min(Time.unscaledDeltaTime, 1 / 20f); Pose(); }

        void Pose()
        {
            float t = shown;
            if (t > 1.6f) { gameObject.SetActive(false); return; }
            float s = t < 0.25f ? Mathf.Lerp(0.3f, 1.2f, t / 0.25f) : Mathf.Lerp(1.2f, 1f, Mathf.Clamp01((t - 0.25f) / 0.2f));
            rt.localScale = Vector3.one * s;
            rt.anchoredPosition = new Vector2(0, t * 40f);
            rt.localRotation = Quaternion.Euler(0, 0, -5f + 2f * Mathf.Sin(t * 9f));
            word.canvasRenderer.SetAlpha(Mathf.Clamp01((1.6f - t) / 0.35f));
        }
    }
}
