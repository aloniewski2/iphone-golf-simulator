using System;
using System.Collections.Generic;
using UnityEngine;

namespace GolfArcade.Hub
{
    /// The Plaza's sign icons, drawn once as anti-aliased white shapes (signed distances, 256 px): the hanger on LOCKER, the crown
    /// on CLUBHOUSE, the club crest, the emote figure on the stage, the sport marks on the PLAY hall arches.
    public static class HubIcons
    {
        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

        public static Texture2D Get(string name) => cache.TryGetValue(name, out var t) && t ? t : cache[name] = Draw(name);

        // ---- distance helpers, in icon units (0..1, y up)
        static float Seg(Vector2 p, Vector2 a, Vector2 b) { var pa = p - a; var ba = b - a; float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba)); return (pa - ba * h).magnitude; }
        static float Circle(Vector2 p, Vector2 c, float r) => (p - c).magnitude - r;
        static float Ring(Vector2 p, Vector2 c, float r, float w) => Mathf.Abs((p - c).magnitude - r) - w;
        /// Signed distance to a closed polygon (negative inside).
        static float Poly(Vector2 p, IList<Vector2> v)
        {
            float d = Vector2.Dot(p - v[0], p - v[0]); float s = 1;
            for (int i = 0, j = v.Count - 1; i < v.Count; j = i, i++)
            {
                var e = v[j] - v[i]; var w = p - v[i];
                var b = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / Vector2.Dot(e, e));
                d = Mathf.Min(d, Vector2.Dot(b, b));
                bool c1 = p.y >= v[i].y, c2 = p.y < v[j].y, c3 = e.x * w.y > e.y * w.x;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s *= -1;
            }
            return s * Mathf.Sqrt(d);
        }
        static Vector2[] Star(Vector2 c, float r0, float r1, int points, float turn = 0)
        {
            var v = new Vector2[points * 2];
            for (int i = 0; i < v.Length; i++) { float a = turn + i * Mathf.PI / points; float r = i % 2 == 0 ? r0 : r1; v[i] = c + new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * r; }
            return v;
        }

        static float Shape(string name, Vector2 p)
        {
            switch (name)
            {
                case "hanger":
                {
                    // hook, then the triangle shoulders and the bar
                    float hook = Mathf.Abs((p - new Vector2(.5f, .76f)).magnitude - .085f) - .028f;
                    if (p.x > .5f && p.y < .76f) hook = 9;                       // open the hook on the lower right
                    hook = Mathf.Min(hook, Seg(p, new Vector2(.5f, .675f), new Vector2(.5f, .6f)) - .028f);
                    float body = Mathf.Min(Seg(p, new Vector2(.5f, .6f), new Vector2(.12f, .3f)), Seg(p, new Vector2(.5f, .6f), new Vector2(.88f, .3f)));
                    body = Mathf.Min(body, Seg(p, new Vector2(.12f, .3f), new Vector2(.88f, .3f))) - .032f;
                    return Mathf.Min(hook, body);
                }
                case "crown":
                {
                    var v = new[] { new Vector2(.14f, .26f), new Vector2(.10f, .70f), new Vector2(.32f, .50f), new Vector2(.50f, .78f), new Vector2(.68f, .50f), new Vector2(.90f, .70f), new Vector2(.86f, .26f) };
                    float crown = Poly(p, v);
                    crown = Mathf.Min(crown, Circle(p, new Vector2(.10f, .72f), .055f));
                    crown = Mathf.Min(crown, Circle(p, new Vector2(.50f, .80f), .06f));
                    crown = Mathf.Min(crown, Circle(p, new Vector2(.90f, .72f), .055f));
                    float band = Mathf.Max(Mathf.Abs(p.y - .2f) - .04f, Mathf.Abs(p.x - .5f) - .36f);
                    return Mathf.Min(crown, band);
                }
                case "crest":
                {
                    // a round badge: ring, a big five-point star, two small stars under it (sport neutral)
                    float d = Ring(p, new Vector2(.5f, .5f), .42f, .035f);
                    d = Mathf.Min(d, Poly(p, Star(new Vector2(.5f, .55f), .24f, .1f, 5)));
                    d = Mathf.Min(d, Poly(p, Star(new Vector2(.36f, .24f), .065f, .027f, 5)));
                    d = Mathf.Min(d, Poly(p, Star(new Vector2(.64f, .24f), .065f, .027f, 5)));
                    return d;
                }
                case "figure":
                {
                    // a person mid-dance: head, body, arms up, legs apart
                    float d = Circle(p, new Vector2(.5f, .80f), .085f);
                    d = Mathf.Min(d, Seg(p, new Vector2(.5f, .66f), new Vector2(.5f, .42f)) - .07f);
                    d = Mathf.Min(d, Seg(p, new Vector2(.5f, .62f), new Vector2(.26f, .74f)) - .045f);
                    d = Mathf.Min(d, Seg(p, new Vector2(.5f, .62f), new Vector2(.76f, .56f)) - .045f);
                    d = Mathf.Min(d, Seg(p, new Vector2(.5f, .42f), new Vector2(.34f, .14f)) - .05f);
                    d = Mathf.Min(d, Seg(p, new Vector2(.5f, .42f), new Vector2(.68f, .16f)) - .05f);
                    return d;
                }
                case "racket":
                {
                    float head = Mathf.Abs(new Vector2((p.x - .56f) / .23f, (p.y - .62f) / .3f).magnitude - 1) * .23f - .03f;
                    float handle = Seg(p, new Vector2(.48f, .33f), new Vector2(.32f, .08f)) - .045f;
                    float strings = 9;
                    for (int i = -2; i <= 2; i++) strings = Mathf.Min(strings, Mathf.Abs(p.x - (.56f + i * .08f)) - .008f, Mathf.Abs(p.y - (.62f + i * .1f)) - .008f);
                    bool insideHead = new Vector2((p.x - .56f) / .23f, (p.y - .62f) / .3f).magnitude < 1;
                    return Mathf.Min(Mathf.Min(head, handle), insideHead ? strings : 9);
                }
                case "flag":
                {
                    float pole = Seg(p, new Vector2(.36f, .1f), new Vector2(.36f, .9f)) - .035f;
                    float flag = Poly(p, new[] { new Vector2(.38f, .9f), new Vector2(.82f, .76f), new Vector2(.38f, .6f) });
                    float hole = Mathf.Max(Mathf.Abs(new Vector2((p.x - .36f) / .28f, (p.y - .1f) / .06f).magnitude - 1) * .06f - .018f, -9);
                    return Mathf.Min(Mathf.Min(pole, flag), hole);
                }
                case "play":
                    return Poly(p, new[] { new Vector2(.3f, .18f), new Vector2(.3f, .82f), new Vector2(.82f, .5f) });
                case "gear":
                {
                    float r = (p - new Vector2(.5f, .5f)).magnitude; float a = Mathf.Atan2(p.y - .5f, p.x - .5f);
                    float teeth = .34f + .06f * Mathf.Clamp(Mathf.Cos(a * 8) * 3, -1, 1);
                    return Mathf.Max(r - teeth, -(r - .14f));
                }
            }
            return 9;
        }

        static Texture2D Draw(string name, int size = 256)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "Hub icon " + name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            var px = new Color32[size * size]; float aa = 1.5f / size;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2((x + .5f) / size, (y + .5f) / size);
                    float d = Shape(name, p);
                    byte a = (byte)(Mathf.Clamp01(.5f - d / aa) * 255);
                    px[y * size + x] = new Color32(255, 255, 255, a);
                }
            tex.SetPixels32(px); tex.Apply(true, true);
            return tex;
        }
    }
}
