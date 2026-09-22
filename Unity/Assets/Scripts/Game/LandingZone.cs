using System.Collections.Generic;
using UnityEngine;

namespace GolfArcade.Game
{
    /// The landing zone in the course view: an amber ring on the ground where a full swing
    /// comes down, with a soft fill, breathing slowly so the eye finds it. Amber is the target's
    /// own colour — the callout's yardage matches it — kept apart from the sky accent.
    public sealed class LandingZone : MonoBehaviour
    {
        public static readonly Color Amber = new(1f, 0.72f, 0.25f);
        Mesh mesh;
        float radius = 3f;

        public static LandingZone Create(Transform parent)
        {
            var go = new GameObject("Landing zone");
            go.transform.SetParent(parent, false);
            var z = go.AddComponent<LandingZone>();
            z.mesh = new Mesh { name = "Landing zone" };
            go.AddComponent<MeshFilter>().mesh = z.mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = GreenRead.Material();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            z.Build(1f);
            return z;
        }

        /// Ring radius in yards; the mesh is unit-sized and scaled.
        public void SetRadius(float yards) { radius = Mathf.Max(1.5f, yards); transform.localScale = Vector3.one * radius; }

        void Build(float pulse)
        {
            const int segs = 56;
            var verts = new List<Vector3>(); var colors = new List<Color>(); var tris = new List<int>();
            // soft fill
            var fill = Amber; fill.a = 0.16f;
            verts.Add(Vector3.zero); colors.Add(fill);
            for (int i = 0; i <= segs; i++)
            {
                float a = i * Mathf.PI * 2 / segs;
                verts.Add(new Vector3(Mathf.Cos(a) * 0.86f, 0, Mathf.Sin(a) * 0.86f)); colors.Add(fill);
            }
            for (int i = 1; i <= segs; i++) tris.AddRange(new[] { 0, i, i + 1 });
            // the ring, a band whose width breathes
            float inner = 0.86f, outer = 1f + 0.06f * pulse;
            var ring = Amber; ring.a = 0.95f;
            int start = verts.Count;
            for (int i = 0; i <= segs; i++)
            {
                float a = i * Mathf.PI * 2 / segs;
                verts.Add(new Vector3(Mathf.Cos(a) * inner, 0, Mathf.Sin(a) * inner)); colors.Add(ring);
                verts.Add(new Vector3(Mathf.Cos(a) * outer, 0, Mathf.Sin(a) * outer)); colors.Add(ring);
            }
            for (int i = 0; i < segs; i++)
            {
                int v = start + i * 2;
                tris.AddRange(new[] { v, v + 2, v + 1, v + 1, v + 2, v + 3 });
            }
            mesh.Clear();
            mesh.SetVertices(verts); mesh.SetColors(colors); mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
        }

        void Update() => Build(0.5f + 0.5f * Mathf.Sin(Time.time * 2.2f));
    }
}
