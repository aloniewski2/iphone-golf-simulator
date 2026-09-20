using System.Collections.Generic;
using UnityEngine;

namespace GolfArcade.Course
{
    /// Builds a hole out of meshes at runtime: rough everywhere, a fairway ribbon along the
    /// centerline, a round green with a real cup and flag, bunkers, a tee box. World units are
    /// yards: course X is world X, course D is world Z, up is Y.
    public sealed class HoleView : MonoBehaviour
    {
        public Hole Hole { get; private set; }
        public Transform Flag { get; private set; }

        static readonly Color RoughColor = new(0.30f, 0.52f, 0.20f);
        static readonly Color FairwayColor = new(0.45f, 0.72f, 0.28f);
        static readonly Color GreenColor = new(0.55f, 0.83f, 0.36f);
        static readonly Color FringeColor = new(0.50f, 0.78f, 0.32f);
        static readonly Color SandColor = new(0.93f, 0.86f, 0.62f);
        static readonly Color TeeColor = new(0.42f, 0.70f, 0.30f);
        static readonly Color CupColor = new(0.08f, 0.08f, 0.06f);
        static readonly Color TreeColor = new(0.16f, 0.36f, 0.14f);

        public static Vector3 ToWorld(CoursePoint p, double height = 0) => new((float)p.X, (float)height, (float)p.D);
        public static CoursePoint ToCourse(Vector3 w) => new(w.x, w.z);

        public static HoleView Build(Hole hole, Transform parent)
        {
            var root = new GameObject($"Hole {hole.Number}");
            root.transform.SetParent(parent, false);
            var view = root.AddComponent<HoleView>();
            view.Hole = hole;
            view.BuildGeometry();
            return view;
        }

        void BuildGeometry()
        {
            // Rough: a big ground plane, shifted so the whole hole sits well inside it.
            var pin = ToWorld(Hole.Pin);
            var ground = Primitive(PrimitiveType.Plane, "Rough", RoughColor, transform);
            ground.transform.position = new Vector3(pin.x / 2, -0.02f, pin.z / 2);
            ground.transform.localScale = new Vector3(120, 1, 120);

            // Fairway ribbon and its fringe of lighter rough.
            AddRibbon("Fairway fringe", Hole.Centerline, Hole.FairwayWidth + 6, 0.0f, FringeColor * 0.92f);
            AddRibbon("Fairway", Hole.Centerline, Hole.FairwayWidth, 0.005f, FairwayColor);

            foreach (var h in Hole.Hazards)
            {
                var kind = h.Kind == HazardKind.Water ? "Water" : "Bunker";
                var color = h.Kind == HazardKind.Water ? new Color(0.25f, 0.55f, 0.85f) : SandColor;
                var disc = Disc(kind, (float)h.Width / 2, (float)h.Length / 2, color, 0.01f);
                disc.transform.position = new Vector3((float)h.X, 0.01f, (float)h.Distance);
            }

            // Green, cup, flag.
            var green = Disc("Green", (float)Hole.GreenRadius, (float)Hole.GreenRadius, GreenColor, 0.012f);
            green.transform.position = pin + Vector3.up * 0.012f;
            var cup = Disc("Cup", 0.15f, 0.15f, CupColor, 0.02f);
            cup.transform.position = pin + Vector3.up * 0.02f;
            var stick = Primitive(PrimitiveType.Cylinder, "Flagstick", Color.white, transform);
            stick.transform.position = pin + Vector3.up * 1.2f;
            stick.transform.localScale = new Vector3(0.05f, 1.2f, 0.05f);
            var flag = Primitive(PrimitiveType.Cube, "Flag", new Color(0.9f, 0.15f, 0.15f), transform);
            flag.transform.position = pin + new Vector3(0.5f, 2.2f, 0);
            flag.transform.localScale = new Vector3(1f, 0.3f, 0.03f);
            Flag = stick.transform;

            var tee = Primitive(PrimitiveType.Cube, "Tee box", TeeColor, transform);
            tee.transform.position = ToWorld(Hole.Tee) + new Vector3(0, 0.008f, 1.5f);
            tee.transform.localScale = new Vector3(7, 0.02f, 5);

            PlantTrees();
        }

        /// Tree line at the edge of the rough, so out of bounds reads at a glance.
        void PlantTrees()
        {
            var rng = new System.Random(Hole.Number * 7919);
            double edge = Hole.FairwayWidth / 2 + Hole.RoughWidth;
            for (int i = 1; i < Hole.Centerline.Length; i++)
            {
                var a = Hole.Centerline[i - 1]; var b = Hole.Centerline[i];
                double dx = b.X - a.X, dd = b.D - a.D, len = a.DistanceTo(b);
                if (len < 1) continue;
                double nx = -dd / len, nd = dx / len;
                for (double s = 0; s <= len; s += 14)
                {
                    foreach (int side in new[] { -1, 1 })
                    {
                        double jitter = rng.NextDouble() * 6;
                        var p = new CoursePoint(a.X + dx * s / len + nx * side * (edge + 4 + jitter), a.D + dd * s / len + nd * side * (edge + 4 + jitter));
                        float h = 5 + (float)rng.NextDouble() * 4;
                        var trunk = Primitive(PrimitiveType.Cylinder, "Trunk", new Color(0.35f, 0.24f, 0.12f), transform);
                        trunk.transform.position = ToWorld(p, h * 0.25);
                        trunk.transform.localScale = new Vector3(0.4f, h * 0.25f, 0.4f);
                        var crown = Primitive(PrimitiveType.Sphere, "Crown", TreeColor, transform);
                        crown.transform.position = ToWorld(p, h * 0.5 + 1.5);
                        crown.transform.localScale = Vector3.one * (h * 0.7f);
                    }
                }
            }
        }

        void AddRibbon(string name, CoursePoint[] line, double width, float y, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(0, y, 0);
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Mat(color);
            mf.sharedMesh = RibbonMesh(line, (float)width);
        }

        /// A flat strip of constant width along a polyline, with round caps and joins.
        static Mesh RibbonMesh(CoursePoint[] line, float width)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            float r = width / 2;
            // Round discs at every station cover the joins and the ends.
            foreach (var p in line) AddDisc(verts, tris, ToWorld(p), r, r);
            for (int i = 1; i < line.Length; i++)
            {
                var a = ToWorld(line[i - 1]); var b = ToWorld(line[i]);
                var dir = (b - a).normalized;
                var n = new Vector3(-dir.z, 0, dir.x) * r;
                int v = verts.Count;
                verts.Add(a - n); verts.Add(a + n); verts.Add(b + n); verts.Add(b - n);
                tris.AddRange(new[] { v, v + 1, v + 2, v, v + 2, v + 3 });
            }
            var mesh = new Mesh { name = "Ribbon" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static void AddDisc(List<Vector3> verts, List<int> tris, Vector3 center, float rx, float rz, int segments = 40)
        {
            int c = verts.Count;
            verts.Add(center);
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments;
                verts.Add(center + new Vector3(Mathf.Cos(a) * rx, 0, Mathf.Sin(a) * rz));
            }
            for (int i = 1; i <= segments; i++) tris.AddRange(new[] { c, c + i + 1, c + i });
        }

        GameObject Disc(string name, float rx, float rz, Color color, float y)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var verts = new List<Vector3>(); var tris = new List<int>();
            AddDisc(verts, tris, Vector3.zero, rx, rz);
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts); mesh.SetTriangles(tris, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = Mat(color);
            return go;
        }

        public static GameObject Primitive(PrimitiveType type, string name, Color color, Transform parent)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            var collider = go.GetComponent<Collider>();
            if (collider) Destroy(collider);
            go.GetComponent<Renderer>().sharedMaterial = Mat(color);
            return go;
        }

        static readonly Dictionary<Color, Material> materials = new();

        public static Material Mat(Color color)
        {
            if (materials.TryGetValue(color, out var m) && m) return m;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            m = new Material(shader) { color = color };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.1f);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.1f);
            materials[color] = m;
            return m;
        }
    }
}
