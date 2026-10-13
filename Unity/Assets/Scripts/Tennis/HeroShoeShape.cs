using System.Collections.Generic;
using UnityEngine;

namespace GolfArcade.Tennis
{
    /// Bald-base pass 2 (2026-10-12): the tennis shoe reads as a clean sneaker rather than a box.
    ///
    /// Garment only, once per source mesh, in the shoe's bind pose: seen from above, the toe tapers and its two front corners round off,
    /// and the heel rounds a little. The upper and the sole (both submeshes) take the same plan-view map, so they stay stacked, and no
    /// vertex moves further than its own clearance to the foot / sock inside (minus Keep), so nothing can show through. Height, UVs,
    /// weights and bind poses are unchanged; normals and tangents go through the map's Jacobian.
    public static class HeroShoeShape
    {
        const float Toe = .14f, Corner = .45f, Heel = .06f, Keep = .004f;
        static readonly Dictionary<Mesh, Mesh> cache = new();

        /// `inside`: the bind-pose renderers the shoe must keep covering (the body and this side's sock).
        public static Mesh Prepare(SkinnedMeshRenderer shoe, Transform root, params SkinnedMeshRenderer[] inside)
        {
            var source = shoe ? shoe.sharedMesh : null;
            if (!source || !source.isReadable || !root) return source;
            if (cache.TryGetValue(source, out var ready) && ready) return ready;
            Matrix4x4 toRoot = root.worldToLocalMatrix * shoe.transform.localToWorldMatrix, fromRoot = toRoot.inverse;
            var sv = source.vertices; int n = sv.Length; var q = new Vector3[n];
            for (int i = 0; i < n; i++) q[i] = toRoot.MultiplyPoint3x4(sv[i]);
            // plan axes: the shoe's long axis (principal direction on the ground) and its side; the toe is the end with the lower upper
            Vector2 mean = Vector2.zero; foreach (var p in q) mean += new Vector2(p.x, p.z); mean /= n;
            float cxx = 0, cxz = 0, czz = 0; foreach (var p in q) { float x = p.x - mean.x, z = p.z - mean.y; cxx += x * x; cxz += x * z; czz += z * z; }
            float angle = .5f * Mathf.Atan2(2 * cxz, cxx - czz); var along = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            float fMin = float.MaxValue, fMax = float.MinValue; foreach (var p in q) { float f = Vector2.Dot(new Vector2(p.x, p.z) - mean, along); fMin = Mathf.Min(fMin, f); fMax = Mathf.Max(fMax, f); }
            float TopNear(float f0) { float top = float.MinValue; foreach (var p in q) { float f = Vector2.Dot(new Vector2(p.x, p.z) - mean, along); if (Mathf.Abs(f - f0) < .03f) top = Mathf.Max(top, p.y); } return top; }
            if (TopNear(fMax - .015f) > TopNear(fMin + .015f)) { along = -along; (fMin, fMax) = (-fMax, -fMin); }
            var across = new Vector2(-along.y, along.x);
            // half width along the shoe (1 cm stations), from every vertex of both submeshes
            int stations = Mathf.CeilToInt((fMax - fMin) / .01f) + 1; var half = new float[stations];
            int Station(float f) => Mathf.Clamp(Mathf.RoundToInt((f - fMin) / .01f), 0, stations - 1);
            float sMid = 0; foreach (var p in q) sMid += Vector2.Dot(new Vector2(p.x, p.z) - mean, across); sMid /= n;
            foreach (var p in q) { var d = new Vector2(p.x, p.z) - mean; int k = Station(Vector2.Dot(d, along)); half[k] = Mathf.Max(half[k], Mathf.Abs(Vector2.Dot(d, across) - sMid)); }
            // smooth the 1 cm stations (a stepped width would kink the taper between them)
            var smooth = (float[])half.Clone(); for (int k = 1; k + 1 < stations; k++) smooth[k] = Mathf.Max(half[k], .5f * (half[k - 1] + half[k + 1]));
            float Half(float f) { float x = Mathf.Clamp((f - fMin) / .01f, 0, stations - 1.001f); int k = (int)x; return Mathf.Lerp(smooth[k], smooth[Mathf.Min(k + 1, stations - 1)], x - k); }
            float widest = 0; foreach (var h in smooth) widest = Mathf.Max(widest, h);
            float length = fMax - fMin, toeStart = fMax - .45f * length, heelEnd = fMin + .22f * length;
            float Smooth(float a, float b, float x) { float t = Mathf.Clamp01((x - a) / (b - a)); return t * t * (3 - 2 * t); }
            Vector3 Plan(Vector3 p)
            {
                var d = new Vector2(p.x, p.z) - mean; float f = Vector2.Dot(d, along), s = Vector2.Dot(d, across) - sMid;
                // measured against at least 60 % of the widest point: at the very tip the width collapses and would shear the taper
                float w = Mathf.Max(Half(f), .6f * widest), r = Mathf.Clamp01(Mathf.Abs(s) / w);
                float toe = Smooth(toeStart, fMax, f), heel = 1 - Smooth(fMin, heelEnd, f);
                float narrow = Toe * toe * toe + Heel * heel * heel;
                s *= 1 - narrow * r;                                                           // taper, strongest at the outer edge
                f -= Corner * Toe * toe * toe * r * r * (f - toeStart) * .5f;                   // pull the toe's front corners back: a round toe
                f += Corner * Heel * heel * heel * r * r * (heelEnd - f) * .5f;
                var o = mean + along * f + across * (s + sMid);
                return new Vector3(o.x, p.y, o.y);
            }
            // clearance to what the shoe covers, in root space
            var inner = new List<Vector3>(); float top = float.MinValue; foreach (var p in q) top = Mathf.Max(top, p.y);
            foreach (var r in inside)
            {
                if (!r || !r.sharedMesh || !r.sharedMesh.isReadable) continue;
                var m = root.worldToLocalMatrix * r.transform.localToWorldMatrix;
                foreach (var v in r.sharedMesh.vertices) { var p = m.MultiplyPoint3x4(v); if (p.y < top + .02f) inner.Add(p); }
            }
            const float cell = .02f; var grid = new Dictionary<Vector3Int, List<Vector3>>();
            Vector3Int Cell(Vector3 p) => new Vector3Int(Mathf.FloorToInt(p.x / cell), Mathf.FloorToInt(p.y / cell), Mathf.FloorToInt(p.z / cell));
            foreach (var p in inner) { var c = Cell(p); if (!grid.TryGetValue(c, out var l)) grid[c] = l = new List<Vector3>(); l.Add(p); }
            float Clearance(Vector3 p)
            {
                float best = cell * 1.5f; var c = Cell(p);
                for (int x = -1; x <= 1; x++) for (int y = -1; y <= 1; y++) for (int z = -1; z <= 1; z++)
                    if (grid.TryGetValue(new Vector3Int(c.x + x, c.y + y, c.z + z), out var l)) foreach (var o in l) best = Mathf.Min(best, (o - p).magnitude);
                return best;
            }
            // soft cap: the move eases off before it reaches the clearance (no kink where the cap starts)
            Vector3 Warp(Vector3 p, float allow) { var t = Plan(p); var d = t - p; float m = d.magnitude; if (m < 1e-7f) return t; float capped = allow * (1 - Mathf.Exp(-m / Mathf.Max(allow, 1e-5f))); return p + d * (Mathf.Min(m, capped) / m); }
            var after = new Vector3[n]; var normals = source.normals; var tangents = source.tangents;
            var newNormals = (Vector3[])normals.Clone(); var newTangents = (Vector4[])tangents.Clone();
            int moved = 0; float largest = 0, minimumJacobian = 1; const float e = .0001f;
            for (int i = 0; i < n; i++)
            {
                float allow = inner.Count > 0 ? Mathf.Max(0, Clearance(q[i]) - Keep) : .01f;
                var w = Warp(q[i], allow);
                if ((w - q[i]).sqrMagnitude < 1e-12f) { after[i] = sv[i]; continue; }
                moved++; largest = Mathf.Max(largest, (w - q[i]).magnitude); after[i] = fromRoot.MultiplyPoint3x4(w);
                var j = Matrix4x4.identity;
                j.SetColumn(0, (Warp(q[i] + Vector3.right * e, allow) - w) / e); j.SetColumn(1, (Warp(q[i] + Vector3.up * e, allow) - w) / e); j.SetColumn(2, (Warp(q[i] + Vector3.forward * e, allow) - w) / e);
                minimumJacobian = Mathf.Min(minimumJacobian, j.determinant);
                var rootJ = fromRoot * j * toRoot;
                if (i < normals.Length) newNormals[i] = rootJ.inverse.transpose.MultiplyVector(normals[i]).normalized;
                if (i < tangents.Length) { var t3 = rootJ.MultiplyVector(new Vector3(tangents[i].x, tangents[i].y, tangents[i].z)).normalized; newTangents[i] = new Vector4(t3.x, t3.y, t3.z, tangents[i].w); }
            }
            ready = Object.Instantiate(source); ready.name = source.name + " (sneaker shape)"; ready.hideFlags = HideFlags.DontSave;
            ready.vertices = after;
            if (normals.Length == n) ready.normals = newNormals;
            if (tangents.Length == n) ready.tangents = newTangents;
            ready.RecalculateBounds();
            cache[source] = ready;
            Debug.Log($"[HeroShoeShape] {source.name}: moved={moved}/{n} largest={largest * 1000:F1}mm minJacobian={minimumJacobian:F3} length={length * 1000:F0}mm inside={inner.Count}");
            return ready;
        }
    }
}
