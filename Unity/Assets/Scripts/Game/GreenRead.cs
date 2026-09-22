using System.Collections.Generic;
using GolfArcade.Course;
using UnityEngine;

namespace GolfArcade.Game
{
    /// The read, the way the golf games show it: a grid of lines draped over the green's every
    /// bump, a line each yard each way, coloured by how steeply the ground tips there (white
    /// flat, through yellow and amber to red, as PGA TOUR 2K colours its grid), with beads
    /// riding on top that drift down the fall line, faster where it is steeper (Wii Sports'
    /// and Mario Golf's sliding lights). Both come from the same surface the roll uses, so what
    /// the player sees is exactly what the putt will do.
    public sealed class GreenRead : MonoBehaviour
    {
        const float LineSpacing = 1f, LineWidth = 0.05f, Sample = 0.5f, Lift = 0.035f;
        const float BeadSpacing = 2f, BeadSize = 0.16f, BeadRun = 2f;

        Hole hole;
        ISurface surface;
        float radius;
        Vector3 centre;
        Mesh beadMesh;
        MeshRenderer lines, beads;
        readonly List<Vector3> beadVerts = new();
        readonly List<Color> beadColors = new();
        struct Bead { public Vector3 Home; public float Along; }
        readonly List<Bead> beadList = new();

        public static GreenRead Create(Transform parent)
        {
            var go = new GameObject("Green read");
            go.transform.SetParent(parent, false);
            var read = go.AddComponent<GreenRead>();
            read.lines = Renderer(go.transform, "Contours");
            read.beads = Renderer(go.transform, "Beads");
            read.beadMesh = read.beads.GetComponent<MeshFilter>().mesh;
            read.beadMesh.MarkDynamic();
            go.SetActive(false);
            return read;
        }

        static MeshRenderer Renderer(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().mesh = new Mesh { name = name };
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Material();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return mr;
        }

        static Material material;
        public static Material Material()
        {
            if (material) return material;
            var shader = Shader.Find("GolfArcade/VertexColorUnlit");
            if (!shader) Debug.LogError("GolfArcade/VertexColorUnlit missing from the build — run Golf Arcade → Set Up Project");
            return material = new Material(shader ?? Shader.Find("Unlit/Color"));
        }

        /// Lay the read over `hole`'s green (the green and its fringe), on the ground as drawn.
        public void Show(Hole hole)
        {
            this.hole = hole;
            surface = hole.Surface ?? FlatSurface.Instance;
            radius = (float)hole.GreenRadius + 2.5f;
            centre = new Vector3((float)hole.Pin.X, 0, (float)hole.Pin.D);
            gameObject.SetActive(true);
            BuildContours();
            SeedBeads();
        }

        public void Hide() => gameObject.SetActive(false);

        /// White on the flat, yellow at about 1½ %, red from 3½ % up.
        public static Color SlopeColor(double slope, float alpha = 0.85f)
        {
            float s = Mathf.Clamp01((float)slope / 0.035f);
            Color c = s < 0.45f ? Color.Lerp(new Color(1, 1, 1), new Color(1, 0.92f, 0.35f), s / 0.45f)
                    : Color.Lerp(new Color(1, 0.92f, 0.35f), new Color(1, 0.25f, 0.2f), (s - 0.45f) / 0.55f);
            c.a = alpha;
            return c;
        }

        Vector3 Ground(float x, float d) => new(x, (float)HoleView.GroundHeight(new CoursePoint(x, d)) + Lift, d);
        bool OnGreen(float x, float d) => (x - centre.x) * (x - centre.x) + (d - centre.z) * (d - centre.z) <= radius * radius;

        void BuildContours()
        {
            var verts = new List<Vector3>(); var colors = new List<Color>(); var tris = new List<int>();
            float first = Mathf.Ceil((centre.x - radius) / LineSpacing) * LineSpacing;
            for (float x = first; x <= centre.x + radius; x += LineSpacing) Strip(verts, colors, tris, x, true);
            first = Mathf.Ceil((centre.z - radius) / LineSpacing) * LineSpacing;
            for (float d = first; d <= centre.z + radius; d += LineSpacing) Strip(verts, colors, tris, d, false);
            var mesh = lines.GetComponent<MeshFilter>().mesh;
            mesh.Clear();
            mesh.SetVertices(verts); mesh.SetColors(colors); mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
        }

        /// One grid line as a thin flat ribbon, a vertex pair every half yard, each pair
        /// coloured by the slope there.
        void Strip(List<Vector3> verts, List<Color> colors, List<int> tris, float at, bool alongD)
        {
            float half = Mathf.Sqrt(Mathf.Max(0, radius * radius - (at - (alongD ? centre.x : centre.z)) * (at - (alongD ? centre.x : centre.z))));
            float from = (alongD ? centre.z : centre.x) - half, to = (alongD ? centre.z : centre.x) + half;
            if (to - from < Sample) return;
            var across = alongD ? new Vector3(LineWidth / 2, 0, 0) : new Vector3(0, 0, LineWidth / 2);
            int start = verts.Count, n = 0;
            for (float s = from; s <= to + 1e-3f; s += Sample, n++)
            {
                float x = alongD ? at : s, d = alongD ? s : at;
                var p = Ground(x, d);
                var c = SlopeColor(surface.Slope(new CoursePoint(x, d)));
                verts.Add(p - across); verts.Add(p + across);
                colors.Add(c); colors.Add(c);
                if (n > 0)
                {
                    int v = start + 2 * n;
                    tris.AddRange(new[] { v - 2, v - 1, v, v - 1, v + 1, v });
                }
            }
        }

        void SeedBeads()
        {
            beadList.Clear();
            for (float x = centre.x - radius; x <= centre.x + radius; x += BeadSpacing)
                for (float d = centre.z - radius; d <= centre.z + radius; d += BeadSpacing)
                {
                    if (!OnGreen(x, d)) continue;
                    beadList.Add(new Bead { Home = new Vector3(x, 0, d), Along = Random.value * BeadRun });
                }
            LayBeads();
        }

        void Update() { if (surface != null) LayBeads(); }

        /// Every bead slides down the fall line from its home, faster on steeper ground, and
        /// starts over after a couple of yards; on the flat it sits still.
        void LayBeads()
        {
            beadVerts.Clear(); beadColors.Clear();
            var tris = new List<int>(beadList.Count * 6);
            for (int i = 0; i < beadList.Count; i++)
            {
                var b = beadList[i];
                var g = surface.Gradient(new CoursePoint(b.Home.x, b.Home.z));
                double slope = System.Math.Sqrt(g.dx * g.dx + g.dd * g.dd);
                float speed = slope < 0.002 ? 0 : 0.4f + (float)slope * 45f; // yd/s: 2 % drifts at ~1.3 yd/s
                b.Along += speed * Time.deltaTime;
                if (b.Along > BeadRun) b.Along -= BeadRun;
                var downhill = slope > 1e-6 ? new Vector3((float)(-g.dx / slope), 0, (float)(-g.dd / slope)) : Vector3.zero;
                var at = b.Home + downhill * b.Along;
                if (!OnGreen(at.x, at.z)) { b.Along = 0; at = b.Home; }
                beadList[i] = b;
                float fade = speed > 0 ? Mathf.Sin(b.Along / BeadRun * Mathf.PI) : 0.6f; // in and out at the ends of its run
                var c = SlopeColor(slope, 0.25f + 0.75f * fade);
                var p = Ground(at.x, at.z) + Vector3.up * 0.01f;
                int v = beadVerts.Count;
                beadVerts.Add(p + new Vector3(-BeadSize / 2, 0, -BeadSize / 2)); beadVerts.Add(p + new Vector3(BeadSize / 2, 0, -BeadSize / 2));
                beadVerts.Add(p + new Vector3(BeadSize / 2, 0, BeadSize / 2)); beadVerts.Add(p + new Vector3(-BeadSize / 2, 0, BeadSize / 2));
                beadColors.Add(c); beadColors.Add(c); beadColors.Add(c); beadColors.Add(c);
                tris.AddRange(new[] { v, v + 2, v + 1, v, v + 3, v + 2 });
            }
            beadMesh.Clear();
            beadMesh.SetVertices(beadVerts); beadMesh.SetColors(beadColors); beadMesh.SetTriangles(tris, 0);
            beadMesh.RecalculateBounds();
        }
    }
}
