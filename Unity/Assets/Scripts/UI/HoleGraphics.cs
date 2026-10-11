using System.Collections.Generic;
using GolfArcade.Course;
using UnityEngine;
using UnityEngine.UI;

namespace GolfArcade.UI
{
    /// A rectangle painted with colours that change across it (left to right, or top to bottom): the
    /// background and the ribbons of the between-holes screens.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class GradientGraphic : MaskableGraphic
    {
        public Color[] Stops = { Color.white, Color.white };
        public bool Vertical;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = GetPixelAdjustedRect();
            int n = Mathf.Max(2, Stops.Length);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                var c = (Color32)(Stops[Mathf.Min(i, Stops.Length - 1)] * color);
                if (Vertical)
                {
                    float y = Mathf.Lerp(r.yMax, r.yMin, t);
                    vh.AddVert(new Vector3(r.xMin, y), c, Vector2.zero); vh.AddVert(new Vector3(r.xMax, y), c, Vector2.zero);
                }
                else
                {
                    float x = Mathf.Lerp(r.xMin, r.xMax, t);
                    vh.AddVert(new Vector3(x, r.yMax), c, Vector2.zero); vh.AddVert(new Vector3(x, r.yMin), c, Vector2.zero);
                }
            }
            for (int i = 0; i < n - 1; i++)
            {
                int a = i * 2;
                vh.AddTriangle(a, a + 1, a + 3); vh.AddTriangle(a, a + 3, a + 2);
            }
        }
    }

    /// Slanted bands across a rectangle, as in the candy-coloured backgrounds of party games.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class StripesGraphic : MaskableGraphic
    {
        public float Band = 70, Period = 140, Slant = 0.47f;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = GetPixelAdjustedRect();
            float drift = r.height * Slant;
            int count = Mathf.CeilToInt((r.width + drift) / Period) + 2;
            var c = (Color32)color;
            for (int i = 0; i < count; i++)
            {
                float x = r.xMin - drift + i * Period;
                int a = vh.currentVertCount;
                vh.AddVert(new Vector3(x, r.yMin), c, Vector2.zero); vh.AddVert(new Vector3(x + Band, r.yMin), c, Vector2.zero);
                vh.AddVert(new Vector3(x + Band + drift, r.yMax), c, Vector2.zero); vh.AddVert(new Vector3(x + drift, r.yMax), c, Vector2.zero);
                vh.AddTriangle(a, a + 2, a + 1); vh.AddTriangle(a, a + 3, a + 2);
            }
        }
    }

    /// Confetti, drawn as one mesh of small spinning rectangles that fall and sway; `Burst` throws a handful.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ConfettiGraphic : MaskableGraphic
    {
        struct Piece { public Vector2 At, Speed; public float Spin, Angle, W, H, Sway, Phase; public Color32 Colour; }
        Piece[] pieces = new Piece[0];
        float born;
        static readonly Color32[] Palette =
        {
            new(215, 240, 68, 255), new(255, 210, 31, 255), new(255, 91, 74, 255), new(101, 209, 211, 255), new(255, 255, 255, 255), new(255, 154, 213, 255),
        };

        public bool Playing => pieces.Length > 0 && Time.unscaledTime - born < 5f;

        public void Burst(int count, int seed)
        {
            var rng = new System.Random(seed);
            var r = rectTransform.rect;
            float F() => (float)rng.NextDouble();
            pieces = new Piece[count];
            for (int i = 0; i < count; i++)
                pieces[i] = new Piece
                {
                    At = new Vector2(r.xMin + F() * r.width, r.yMax + F() * r.height * 0.35f),
                    Speed = new Vector2((F() - .5f) * 120, -(260 + F() * 380)),
                    Spin = (F() - .5f) * 700, Angle = F() * 360, W = 14 + F() * 22, H = 8 + F() * 14, Sway = 30 + F() * 60, Phase = F() * 6.28f,
                    Colour = Palette[i % Palette.Length],
                };
            born = Time.unscaledTime;
            SetVerticesDirty();
        }

        public void Clear() { pieces = new Piece[0]; SetVerticesDirty(); }

        void Update() { if (pieces.Length > 0) SetVerticesDirty(); }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            float t = Time.unscaledTime - born;
            float fade = Mathf.Clamp01((5f - t) / 1.2f);
            foreach (var p in pieces)
            {
                var at = p.At + p.Speed * t + new Vector2(Mathf.Sin(t * 2.4f + p.Phase) * p.Sway, 0);
                float a = (p.Angle + p.Spin * t) * Mathf.Deg2Rad, cos = Mathf.Cos(a), sin = Mathf.Sin(a);
                var c = p.Colour; c.a = (byte)(255 * fade);
                int b = vh.currentVertCount;
                foreach (var corner in new[] { new Vector2(-p.W, -p.H), new Vector2(p.W, -p.H), new Vector2(p.W, p.H), new Vector2(-p.W, p.H) })
                {
                    var q = corner * 0.5f;
                    vh.AddVert(new Vector3(at.x + q.x * cos - q.y * sin, at.y + q.x * sin + q.y * cos), c, Vector2.zero);
                }
                vh.AddTriangle(b, b + 1, b + 2); vh.AddTriangle(b, b + 2, b + 3);
            }
        }
    }

    /// A hole as a top-down picture, drawn from the hole's own numbers (shore, islets, centreline, hazards, green):
    /// the sea, the land with its sandy rim, the fairway, bunkers, water and lava, the green and its flag, the tee, and
    /// the line from tee to pin dotted. A long thin hole is drawn wider than it is (the way a scorecard's hole maps are):
    /// the shape is the same and the room is used. The teeing ground is at the bottom and the pin at the top.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HoleMapGraphic : MaskableGraphic
    {
        /// The picture is drawn in four layers so that what lies on the land can be clipped to it (the water inlets and lava pools
        /// are drawn as ovals, and only the part over the island shows): Base (sea, land, rim), LandShape (the land alone, the mask),
        /// Ground (fairway, green and hazards, inside the mask), Marks (the line, the tee and the flag, which may stand out over the sea).
        public enum Layer { All, Base, LandShape, Ground, Marks }
        public Layer Part = Layer.All;

        Hole hole;
        float stretch = 1, scale = 1, pad = 56;
        Vector2 middle;       // the hole's middle in course yards (x right, d down the hole)
        Vector2[] line = new Vector2[0];
        float[] along = new float[0];
        float lineLength;
        bool magma;

        static readonly Color SeaTop = Hex("2aa7d8"), SeaBottom = Hex("1478b8"), Grass = Hex("74cf5a"), Fairway = Hex("9be26a"), Rim = Hex("f6e8ac"),
            Sand = Hex("f7e6a6"), SandEdge = Hex("e2c871"), GreenFill = Hex("c4f08c"), GreenEdge = Hex("e9ffc6"), Ink = Hex("10243D");
        static Color Hex(string h) { int v = System.Convert.ToInt32(h, 16); return new Color(((v >> 16) & 255) / 255f, ((v >> 8) & 255) / 255f, (v & 255) / 255f); }

        public void SetHole(Hole h, float padding = 56)
        {
            hole = h; pad = padding; magma = h != null && h.Theme == "magma";
            SetVerticesDirty();
        }

        /// 0 at the tee to 1 at the pin: where a point on the centreline is, in this graphic's local space.
        public Vector2 PointAt(float fraction)
        {
            if (line.Length < 2) return Vector2.zero;
            float at = Mathf.Clamp01(fraction) * lineLength;
            for (int i = 1; i < line.Length; i++)
            {
                float seg = along[i] - along[i - 1];
                if (at <= along[i] || i == line.Length - 1) return Vector2.Lerp(line[i - 1], line[i], seg > 0 ? Mathf.Clamp01((at - along[i - 1]) / seg) : 0);
            }
            return line[line.Length - 1];
        }

        /// The centreline from the tee as far as `fraction`, for the trail the ball leaves.
        public List<Vector2> TrailTo(float fraction)
        {
            var trail = new List<Vector2>();
            if (line.Length < 2) return trail;
            float at = Mathf.Clamp01(fraction) * lineLength;
            trail.Add(line[0]);
            for (int i = 1; i < line.Length; i++)
            {
                if (at >= along[i]) { trail.Add(line[i]); continue; }
                float seg = along[i] - along[i - 1];
                trail.Add(Vector2.Lerp(line[i - 1], line[i], seg > 0 ? (at - along[i - 1]) / seg : 0));
                break;
            }
            return trail;
        }

        protected override void OnRectTransformDimensionsChange() { base.OnRectTransformDimensionsChange(); SetVerticesDirty(); }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = GetPixelAdjustedRect();
            // the sea, a little lighter at the top (a lava lake, dark and hot, for the Magma Open)
            var top = magma ? Hex("c4400e") : SeaTop; var bottom = magma ? Hex("7a1d0b") : SeaBottom;
            bool all = Part == Layer.All, baseLayer = all || Part == Layer.Base, ground = all || Part == Layer.Ground, marks = all || Part == Layer.Marks, shape = Part == Layer.LandShape;
            if (baseLayer) Quad(vh, new Vector2(r.xMin, r.yMax), new Vector2(r.xMax, r.yMax), new Vector2(r.xMax, r.yMin), new Vector2(r.xMin, r.yMin), top, top, bottom, bottom);
            if (hole == null) return;

            var land = new List<CoursePoint[]>();
            if (hole.Shore != null) land.Add(hole.Shore);
            foreach (var islet in hole.Islets) land.Add(islet);
            double minX = double.MaxValue, maxX = double.MinValue, minD = double.MaxValue, maxD = double.MinValue;
            void Grow(CoursePoint p) { minX = System.Math.Min(minX, p.X); maxX = System.Math.Max(maxX, p.X); minD = System.Math.Min(minD, p.D); maxD = System.Math.Max(maxD, p.D); }
            foreach (var poly in land) foreach (var p in poly) Grow(p);
            foreach (var p in hole.Centerline) Grow(p);
            double bw = System.Math.Max(1, maxX - minX), bh = System.Math.Max(1, maxD - minD);
            float s0 = (float)System.Math.Min((r.width - 2 * pad) / bw, (r.height - 2 * pad) / bh);
            stretch = Mathf.Clamp(0.5f * (r.width - 2 * pad) / ((float)bw * s0), 1f, 2.6f);
            scale = Mathf.Min((r.width - 2 * pad) / ((float)bw * stretch), (r.height - 2 * pad) / (float)bh);
            middle = new Vector2((float)(minX + maxX) / 2, (float)(minD + maxD) / 2);
            Vector2 Local(double x, double d) => r.center + new Vector2((float)((x - middle.x) * scale * stretch), (float)((d - middle.y) * scale));

            // the centreline, tee to pin
            line = new Vector2[hole.Centerline.Length]; along = new float[line.Length]; lineLength = 0;
            for (int i = 0; i < line.Length; i++)
            {
                line[i] = Local(hole.Centerline[i].X, hole.Centerline[i].D);
                if (i > 0) { lineLength += Vector2.Distance(line[i], line[i - 1]); along[i] = lineLength; }
            }

            var rim = magma ? Hex("2b1a14") : Rim;
            var grass = magma ? Hex("5aa85a") : Grass;
            if (baseLayer || shape)
                foreach (var poly in land)
                {
                    var pts = Simplify(poly, Local, 2.5f);
                    if (baseLayer) Outline(vh, pts, 22, new Color(rim.r, rim.g, rim.b, .55f));
                    Fill(vh, pts, shape ? Color.white : grass);
                    if (baseLayer) Outline(vh, pts, 8, rim);
                }
            if (shape) return;
            var pin = line[line.Length - 1]; var tee = line[0];
            if (ground)
            {
                // the fairway along the centreline, then what lies on it
                float fw = Mathf.Max(10, (float)hole.FairwayWidth * scale * stretch * .8f);
                var fair = magma ? Hex("82c47c") : Fairway;
                Polyline(vh, line, fw, fair, true);
                foreach (var h in hole.Hazards)
                {
                    var c = Local(h.X, h.Distance);
                    // water is the sea's own colour at that height (an inlet cut through the land); the rest have an edge
                    var sea = Color.Lerp(top, bottom, Mathf.InverseLerp(r.yMax, r.yMin, c.y));
                    Color fill = h.Kind switch { HazardKind.Bunker => Sand, HazardKind.Water => sea, HazardKind.Lava => Hex("ff8a1e"), _ => Hex("d6f2ff") };
                    Color edge = h.Kind switch { HazardKind.Bunker => SandEdge, HazardKind.Water => Hex("bdeaff"), HazardKind.Lava => Hex("ffd26a"), _ => Color.white };
                    float rx = (float)h.Width / 2 * scale * stretch, ry = (float)h.Length / 2 * scale;
                    Ellipse(vh, c, rx + 4, ry + 4, edge); Ellipse(vh, c, rx, ry, fill);
                }
                float gr = (float)hole.GreenRadius * scale;
                Ellipse(vh, pin, Mathf.Max(18, gr * stretch) + 4, Mathf.Max(18, gr) + 4, GreenEdge); Ellipse(vh, pin, Mathf.Max(18, gr * stretch), Mathf.Max(18, gr), GreenFill);
            }
            if (!marks) return;
            Dotted(vh, line, 5, 3, 14, new Color(1, 1, 1, .75f));
            // the tee, and the flag in the cup
            Quad(vh, tee + new Vector2(-26, 15), tee + new Vector2(26, 15), tee + new Vector2(26, -15), tee + new Vector2(-26, -15), Ink, Ink, Ink, Ink);
            Quad(vh, tee + new Vector2(-21, 10), tee + new Vector2(21, 10), tee + new Vector2(21, -10), tee + new Vector2(-21, -10), Color.white, Color.white, Color.white, Color.white);
            Polyline(vh, new[] { pin, pin + new Vector2(0, 92) }, 8, Color.white, true);
            var flag = new Color(1f, .23f, .19f);
            Tri(vh, pin + new Vector2(0, 92), pin + new Vector2(58, 72), pin + new Vector2(0, 52), Color.white);
            Tri(vh, pin + new Vector2(4, 86), pin + new Vector2(48, 72), pin + new Vector2(4, 58), flag);
            Ellipse(vh, pin, 10, 10, Ink);
        }

        // ---- Drawing

        static void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color ca, Color cb, Color cc, Color cd)
        {
            int i = vh.currentVertCount;
            vh.AddVert(a, ca, Vector2.zero); vh.AddVert(b, cb, Vector2.zero); vh.AddVert(c, cc, Vector2.zero); vh.AddVert(d, cd, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3);
        }

        static void Tri(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color color)
        {
            int i = vh.currentVertCount;
            vh.AddVert(a, color, Vector2.zero); vh.AddVert(b, color, Vector2.zero); vh.AddVert(c, color, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2);
        }

        static void Ellipse(VertexHelper vh, Vector2 c, float rx, float ry, Color color)
        {
            const int n = 40;
            int i = vh.currentVertCount;
            vh.AddVert(c, color, Vector2.zero);
            for (int k = 0; k < n; k++)
            {
                float a = k * Mathf.PI * 2 / n;
                vh.AddVert(c + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry), color, Vector2.zero);
            }
            for (int k = 0; k < n; k++) vh.AddTriangle(i, i + 1 + k, i + 1 + (k + 1) % n);
        }

        static void Segment(VertexHelper vh, Vector2 a, Vector2 b, float width, Color color)
        {
            var d = b - a; if (d.sqrMagnitude < 1e-6f) return;
            var n = new Vector2(-d.y, d.x).normalized * width / 2;
            Quad(vh, a + n, b + n, b - n, a - n, color, color, color, color);
        }

        static void Polyline(VertexHelper vh, IList<Vector2> pts, float width, Color color, bool round)
        {
            for (int i = 1; i < pts.Count; i++) Segment(vh, pts[i - 1], pts[i], width, color);
            if (round) foreach (var p in pts) Ellipse(vh, p, width / 2, width / 2, color);
        }

        static void Outline(VertexHelper vh, IList<Vector2> ring, float width, Color color)
        {
            for (int i = 0; i < ring.Count; i++) Segment(vh, ring[i], ring[(i + 1) % ring.Count], width, color);
            foreach (var p in ring) Ellipse(vh, p, width / 2, width / 2, color);
        }

        static void Dotted(VertexHelper vh, IList<Vector2> pts, float dot, float on, float off, Color color)
        {
            float carry = 0;
            for (int i = 1; i < pts.Count; i++)
            {
                var a = pts[i - 1]; var b = pts[i]; float len = Vector2.Distance(a, b);
                if (len < 1e-3f) continue;
                for (float at = carry; at < len; at += on + off) Ellipse(vh, Vector2.Lerp(a, b, at / len), dot / 2, dot / 2, color);
                carry = (carry - len) % (on + off); if (carry < 0) carry += on + off;
            }
        }

        /// The ring in local space, with points closer than `gap` to the one before dropped (the shore has hundreds).
        static List<Vector2> Simplify(CoursePoint[] poly, System.Func<double, double, Vector2> local, float gap)
        {
            var ring = new List<Vector2>();
            foreach (var p in poly)
            {
                var v = local(p.X, p.D);
                if (ring.Count == 0 || Vector2.Distance(v, ring[ring.Count - 1]) >= gap) ring.Add(v);
            }
            if (ring.Count > 3 && Vector2.Distance(ring[0], ring[ring.Count - 1]) < gap) ring.RemoveAt(ring.Count - 1);
            return ring;
        }

        /// A filled polygon by ear clipping (the shores are concave), only reflex corners tested against each ear.
        static void Fill(VertexHelper vh, List<Vector2> ring, Color color)
        {
            int n = ring.Count;
            if (n < 3) return;
            float area = 0;
            for (int i = 0; i < n; i++) { var a = ring[i]; var b = ring[(i + 1) % n]; area += a.x * b.y - b.x * a.y; }
            var order = new List<int>(n);
            for (int i = 0; i < n; i++) order.Add(area > 0 ? i : n - 1 - i);     // counter-clockwise
            int baseIndex = vh.currentVertCount;
            foreach (var p in ring) vh.AddVert(p, color, Vector2.zero);
            bool Reflex(int at) { int m = order.Count; var a = ring[order[(at + m - 1) % m]]; var b = ring[order[at]]; var c = ring[order[(at + 1) % m]]; return Cross(b - a, c - b) < 0; }
            int guard = n * n + 10;
            int cursor = 0;
            while (order.Count > 3 && guard-- > 0)
            {
                int m = order.Count;
                int ia = (cursor + m - 1) % m, ib = cursor % m, ic = (cursor + 1) % m;
                var A = ring[order[ia]]; var B = ring[order[ib]]; var C = ring[order[ic]];
                bool ear = Cross(B - A, C - B) > 0;
                if (ear)
                    for (int k = 0; k < m && ear; k++)
                    {
                        if (k == ia || k == ib || k == ic) continue;
                        if (!Reflex(k)) continue;
                        if (Inside(ring[order[k]], A, B, C)) ear = false;
                    }
                if (ear) { vh.AddTriangle(baseIndex + order[ia], baseIndex + order[ib], baseIndex + order[ic]); order.RemoveAt(ib); cursor = ib % order.Count; }
                else cursor = (cursor + 1) % m;
            }
            if (order.Count == 3) vh.AddTriangle(baseIndex + order[0], baseIndex + order[1], baseIndex + order[2]);
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
        static bool Inside(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross(b - a, p - a), d2 = Cross(c - b, p - b), d3 = Cross(a - c, p - c);
            return d1 >= 0 && d2 >= 0 && d3 >= 0;
        }
    }

    /// The line the ball has rolled along (ink under lime), drawn each frame up to where it is.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TrailGraphic : MaskableGraphic
    {
        public List<Vector2> Points = new();

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Draw(vh, 20, new Color(0.063f, 0.141f, 0.239f));
            Draw(vh, 11, new Color(0.843f, 0.941f, 0.267f));
        }

        void Draw(VertexHelper vh, float width, Color color)
        {
            for (int i = 1; i < Points.Count; i++)
            {
                var a = Points[i - 1]; var b = Points[i]; var d = b - a;
                if (d.sqrMagnitude < 1e-6f) continue;
                var n = new Vector2(-d.y, d.x).normalized * width / 2;
                int v = vh.currentVertCount;
                vh.AddVert(a + n, color, Vector2.zero); vh.AddVert(b + n, color, Vector2.zero); vh.AddVert(b - n, color, Vector2.zero); vh.AddVert(a - n, color, Vector2.zero);
                vh.AddTriangle(v, v + 1, v + 2); vh.AddTriangle(v, v + 2, v + 3);
            }
            foreach (var p in Points)
            {
                int v = vh.currentVertCount;
                vh.AddVert(p, color, Vector2.zero);
                for (int k = 0; k < 16; k++) { float a = k * Mathf.PI * 2 / 16; vh.AddVert(p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * width / 2, color, Vector2.zero); }
                for (int k = 0; k < 16; k++) vh.AddTriangle(v, v + 1 + k, v + 1 + (k + 1) % 16);
            }
        }
    }
}
