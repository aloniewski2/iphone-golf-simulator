using System.Collections.Generic;
using GolfArcade.Course;
using UnityEngine;

namespace GolfArcade.Game
{
    /// The read, left in the grass: faint chevrons — the grain — scattered over the green, each
    /// pointing down the fall line and drifting slowly that way. Where the green tips harder they
    /// stand out more, are longer and drift faster; where it is flat there is nothing to see. No
    /// line and no colours: the player reads which way and how much the ground falls and plays
    /// the break themselves. They come from the same surface the roll uses, so the read is honest.
    public sealed class GreenRead : MonoBehaviour
    {
        const float Spacing = 1.4f, Jitter = 0.45f, Lift = 0.035f, Run = 0.8f, Stroke = 0.04f;
        /// Steeper than this (2.5 %) the grain is as strong as it gets; flatter than FlatSlope it doesn't show.
        const float FullSlope = 0.025f, FlatSlope = 0.004f;
        static readonly Color Grain = new(0.94f, 1f, 0.88f);

        Hole hole;
        ISurface surface;
        float radius;
        Vector3 centre;
        Mesh mesh;
        readonly List<Vector3> verts = new();
        readonly List<Color> colors = new();
        readonly List<int> tris = new();
        struct Mark { public Vector3 Home, Down; public float Strength, Along; }
        readonly List<Mark> marks = new();

        public static GreenRead Create(Transform parent)
        {
            var go = new GameObject("Green read");
            go.transform.SetParent(parent, false);
            var read = go.AddComponent<GreenRead>();
            var grain = new GameObject("Grain");
            grain.transform.SetParent(go.transform, false);
            read.mesh = new Mesh { name = "Grain" };
            read.mesh.MarkDynamic();
            grain.AddComponent<MeshFilter>().mesh = read.mesh;
            var mr = grain.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Material();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            go.SetActive(false);
            return read;
        }

        static Material material;
        public static Material Material()
        {
            if (material) return material;
            var shader = Shader.Find("GolfArcade/VertexColorUnlit");
            if (!shader) Debug.LogError("GolfArcade/VertexColorUnlit missing from the build — run Golf Arcade → Set Up Project");
            return material = new Material(shader ?? Shader.Find("Unlit/Color"));
        }

        /// Lay the grain over `hole`'s green (the green and its fringe), on the ground as drawn.
        public void Show(Hole hole)
        {
            this.hole = hole;
            surface = hole.Surface ?? FlatSurface.Instance;
            radius = (float)hole.GreenRadius + 1.5f;
            centre = new Vector3((float)hole.Pin.X, 0, (float)hole.Pin.D);
            gameObject.SetActive(true);
            Seed();
        }

        public void Hide() => gameObject.SetActive(false);

        /// How plainly the ground at a slope shows: nothing on the flat, all of it from FullSlope up.
        public static float Strength(double slope) => Mathf.Clamp01(((float)slope - FlatSlope) / (FullSlope - FlatSlope));

        Vector3 Ground(float x, float d) => new(x, (float)HoleView.GroundHeight(new CoursePoint(x, d)) + Lift, d);
        bool OnGreen(float x, float d) => (x - centre.x) * (x - centre.x) + (d - centre.z) * (d - centre.z) <= radius * radius;

        /// A mark every Spacing yards or so (a jittered grid, so it reads as grass, not graph paper),
        /// each with the fall line and the steepness where it sits.
        void Seed()
        {
            marks.Clear();
            var rng = new System.Random(hole.Number * 7919);
            for (float x = centre.x - radius; x <= centre.x + radius; x += Spacing)
                for (float d = centre.z - radius; d <= centre.z + radius; d += Spacing)
                {
                    float jx = x + (float)(rng.NextDouble() - 0.5) * 2 * Jitter, jd = d + (float)(rng.NextDouble() - 0.5) * 2 * Jitter;
                    if (!OnGreen(jx, jd)) continue;
                    var g = surface.Gradient(new CoursePoint(jx, jd));
                    double slope = System.Math.Sqrt(g.dx * g.dx + g.dd * g.dd);
                    float strength = Strength(slope);
                    if (strength <= 0) continue;
                    marks.Add(new Mark
                    {
                        Home = new Vector3(jx, 0, jd),
                        Down = new Vector3((float)(-g.dx / slope), 0, (float)(-g.dd / slope)),
                        Strength = strength,
                        Along = (float)rng.NextDouble() * Run,
                    });
                }
            Lay();
        }

        void Update() { if (surface != null) Lay(); }

        /// Every chevron creeps down its fall line, faster on steeper ground, fading in and out over
        /// its short run so the green seems to shimmer downhill.
        void Lay()
        {
            verts.Clear(); colors.Clear(); tris.Clear();
            for (int i = 0; i < marks.Count; i++)
            {
                var m = marks[i];
                m.Along += (0.12f + 0.35f * m.Strength) * Time.deltaTime;
                if (m.Along > Run) m.Along -= Run;
                marks[i] = m;
                var tip = m.Home + m.Down * m.Along;
                if (!OnGreen(tip.x, tip.z)) continue;
                float fade = Mathf.Sin(m.Along / Run * Mathf.PI);
                var c = Grain; c.a = (0.07f + 0.23f * m.Strength) * fade;
                float arm = 0.16f + 0.2f * m.Strength;
                var side = new Vector3(-m.Down.z, 0, m.Down.x);
                // a "V" whose point leads downhill
                Arm(tip, tip - m.Down * arm * 0.8f + side * arm * 0.6f, c);
                Arm(tip, tip - m.Down * arm * 0.8f - side * arm * 0.6f, c);
            }
            mesh.Clear();
            mesh.SetVertices(verts); mesh.SetColors(colors); mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
        }

        /// One stroke of a chevron, a thin ribbon on the ground from `a` to `b`.
        void Arm(Vector3 a, Vector3 b, Color c)
        {
            var along = b - a; along.y = 0;
            var across = new Vector3(-along.z, 0, along.x).normalized * (Stroke / 2);
            var pa = Ground(a.x, a.z); var pb = Ground(b.x, b.z);
            int v = verts.Count;
            verts.Add(pa - across); verts.Add(pa + across); verts.Add(pb + across); verts.Add(pb - across);
            var tail = c; tail.a *= 0.35f;
            colors.Add(c); colors.Add(c); colors.Add(tail); colors.Add(tail);
            tris.AddRange(new[] { v, v + 1, v + 2, v, v + 2, v + 3 });
        }
    }
}
