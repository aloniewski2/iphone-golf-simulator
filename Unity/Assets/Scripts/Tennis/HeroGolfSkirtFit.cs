using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GolfArcade.Tennis
{
    /// Bald-base pass 2026-10-12: the female golf skirt sits close to the hips and only flares gently, so the hands stay visible beside it.
    ///
    /// Measured in its bind pose, the skirt flared from ~15 cm round the hip axis at the waist to ~31 cm at the hem, over hips / thighs of
    /// ~17 cm: about 14 cm of air at the hem, wider than where the hanging hands are (~26 cm). Garment-only, once per source mesh:
    /// every skirt vertex keeps its height and direction round the hip axis; only its distance beyond the body's own envelope
    /// (+ Margin) shrinks: nothing at the waistband, down to Keep from just below the hip joint to the hem, so the radius grows
    /// steadily from hips to hem (about 16 -> 21 -> 23 cm) instead of ballooning over the hips. The map is monotonic in that distance, so the
    /// skirt's walls and folds keep their order and nothing moves inside the body. Topology, UVs, weights, bind poses and the pose
    /// corrective blend shapes are kept; shape deltas, normals and tangents go through the same map.
    public static class HeroGolfSkirtFit
    {
        const float Margin = .012f, Keep = .36f, RowHeight = .01f;
        const int Sectors = 24;
        static readonly string[] EnvelopeBones = { "Hips", "Spine", "LeftUpperLeg", "RightUpperLeg", "LeftLowerLeg", "RightLowerLeg" };
        static readonly Dictionary<Mesh, Mesh> cache = new();

        public static Mesh Prepare(MatchHeroLook hero, SkinnedMeshRenderer skirt)
        {
            var source = skirt ? skirt.sharedMesh : null; var body = hero ? hero.body : null;
            if (!source || !source.isReadable || !body || !body.sharedMesh || !body.sharedMesh.isReadable) return source;
            if (cache.TryGetValue(source, out var ready) && ready) return ready;
            var root = hero.transform;
            Matrix4x4 toRoot = root.worldToLocalMatrix * skirt.transform.localToWorldMatrix, fromRoot = toRoot.inverse;
            Matrix4x4 bodyToRoot = root.worldToLocalMatrix * body.transform.localToWorldMatrix;
            int hipsIndex = System.Array.IndexOf(body.bones, hero.Bone(HumanBodyBones.Hips));
            if (hipsIndex < 0) return source;
            Vector3 hip = bodyToRoot.MultiplyPoint3x4(body.sharedMesh.bindposes[hipsIndex].inverse.GetColumn(3));

            // The body's outer envelope round the hip axis (hips, spine and legs only: the hanging arms are not part of it), as a support
            // function: how far the body reaches in each direction. Like a band round both thighs, it spans the gap between the legs,
            // so the skirt drapes across it instead of tucking in.
            var wanted = new HashSet<int>(EnvelopeBones.Select(n => System.Array.FindIndex(body.bones, b => b && b.name == n)).Where(i => i >= 0));
            var bv = body.sharedMesh.vertices; var bw = body.sharedMesh.boneWeights;
            var sv = source.vertices;
            float yLow = float.MaxValue, yHigh = float.MinValue;
            foreach (var p in sv) { float y = toRoot.MultiplyPoint3x4(p).y; yLow = Mathf.Min(yLow, y); yHigh = Mathf.Max(yHigh, y); }
            int rows = Mathf.CeilToInt((yHigh - yLow) / RowHeight) + 3;
            var envelope = new float[rows, Sectors];
            int Row(float y) => Mathf.Clamp(Mathf.FloorToInt((y - yLow) / RowHeight) + 1, 0, rows - 1);
            for (int i = 0; i < bv.Length; i++)
            {
                if (!wanted.Contains(bw[i].boneIndex0) || bw[i].weight0 < .5f) continue;
                var q = bodyToRoot.MultiplyPoint3x4(bv[i]);
                if (q.y < yLow - RowHeight || q.y > yHigh + RowHeight) continue;
                float dx = q.x - hip.x, dz = q.z - hip.z; int row = Row(q.y);
                for (int s = 0; s < Sectors; s++)
                {
                    float a = (s + .5f) / Sectors * 2 * Mathf.PI - Mathf.PI;
                    envelope[row, s] = Mathf.Max(envelope[row, s], dx * Mathf.Cos(a) + dz * Mathf.Sin(a));
                }
            }
            // one row and one sector either side: covers the gap between the thighs and the cell size
            var grown = new float[rows, Sectors];
            for (int row = 0; row < rows; row++) for (int s = 0; s < Sectors; s++)
            {
                float m = 0;
                for (int dr = -1; dr <= 1; dr++) for (int ds = -1; ds <= 1; ds++)
                {
                    int rr = row + dr; if (rr < 0 || rr >= rows) continue;
                    m = Mathf.Max(m, envelope[rr, ((s + ds) % Sectors + Sectors) % Sectors]);
                }
                grown[row, s] = m;
            }
            float Envelope(float y, float angle)
            {
                float fy = Mathf.Clamp((y - yLow) / RowHeight + .5f, 0, rows - 1.001f), fs = (angle + Mathf.PI) / (2 * Mathf.PI) * Sectors - .5f;
                int r0 = (int)fy, s0 = Mathf.FloorToInt(fs); float ty = fy - r0, ts = fs - s0;
                int a = ((s0 % Sectors) + Sectors) % Sectors, b = (a + 1) % Sectors;
                float lo = Mathf.Lerp(grown[r0, a], grown[r0, b], ts), hi = Mathf.Lerp(grown[r0 + 1, a], grown[r0 + 1, b], ts);
                return Mathf.Lerp(lo, hi, ty);
            }
            float hem = yLow, band = yHigh - .015f, full = hip.y - .02f;   // the waistband keeps its fit; full compression just below the hip joint
            Vector3 Warp(Vector3 p)
            {
                var q = toRoot.MultiplyPoint3x4(p);
                float t = Mathf.Clamp01((band - q.y) / Mathf.Max(.01f, band - full));
                float keep = Mathf.Lerp(1, Keep, t * t * (3 - 2 * t));
                if (keep >= .9999f) return p;
                float dx = q.x - hip.x, dz = q.z - hip.z; float r = Mathf.Sqrt(dx * dx + dz * dz);
                if (r < 1e-5f) return p;
                float inner = Envelope(q.y, Mathf.Atan2(dz, dx)) + Margin;
                if (r <= inner) return p;
                float scale = (inner + (r - inner) * keep) / r;
                return fromRoot.MultiplyPoint3x4(new Vector3(hip.x + dx * scale, q.y, hip.z + dz * scale));
            }

            int n = sv.Length; var after = new Vector3[n];
            var normals = source.normals; var tangents = source.tangents;
            var newNormals = (Vector3[])normals.Clone(); var newTangents = (Vector4[])tangents.Clone();
            int moved = 0; float largest = 0, minimumJacobian = 1; const float e = .0001f;
            for (int i = 0; i < n; i++)
            {
                after[i] = Warp(sv[i]);
                if ((after[i] - sv[i]).sqrMagnitude < 1e-14f) { after[i] = sv[i]; continue; }
                moved++; largest = Mathf.Max(largest, (after[i] - sv[i]).magnitude);
                var j = Matrix4x4.identity;
                j.SetColumn(0, (Warp(sv[i] + Vector3.right * e) - after[i]) / e);
                j.SetColumn(1, (Warp(sv[i] + Vector3.up * e) - after[i]) / e);
                j.SetColumn(2, (Warp(sv[i] + Vector3.forward * e) - after[i]) / e);
                minimumJacobian = Mathf.Min(minimumJacobian, j.determinant);
                if (i < normals.Length) newNormals[i] = j.inverse.transpose.MultiplyVector(normals[i]).normalized;
                if (i < tangents.Length) { var t3 = j.MultiplyVector(new Vector3(tangents[i].x, tangents[i].y, tangents[i].z)).normalized; newTangents[i] = new Vector4(t3.x, t3.y, t3.z, tangents[i].w); }
            }
            ready = Object.Instantiate(source); ready.name = source.name + " (close golf skirt)"; ready.hideFlags = HideFlags.DontSave;
            ready.vertices = after;
            if (normals.Length == n) ready.normals = newNormals;
            if (tangents.Length == n) ready.tangents = newTangents;
            ready.ClearBlendShapes();
            for (int shape = 0; shape < source.blendShapeCount; shape++)
                for (int frame = 0; frame < source.GetBlendShapeFrameCount(shape); frame++)
                {
                    var dp = new Vector3[n]; var dn = new Vector3[n]; var dt = new Vector3[n];
                    source.GetBlendShapeFrameVertices(shape, frame, dp, dn, dt);
                    for (int i = 0; i < n; i++) if (dp[i].sqrMagnitude > 1e-14f) dp[i] = Warp(sv[i] + dp[i]) - after[i];
                    ready.AddBlendShapeFrame(source.GetBlendShapeName(shape), source.GetBlendShapeFrameWeight(shape, frame), dp, dn, dt);
                }
            ready.RecalculateBounds();
            cache[source] = ready;
            Debug.Log($"[HeroGolfSkirtFit] {source.name}: moved={moved}/{n} largest={largest * 100:F1}cm minJacobian={minimumJacobian:F3} hip={hip.y:F3} hem={hem:F3} shapes={source.blendShapeCount}");
            return ready;
        }
    }
}
