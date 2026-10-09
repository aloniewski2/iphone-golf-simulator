using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GolfArcade.Tennis
{
    /// Transfers accepted Eye13 eye and brow material intent to the existing
    /// default face. No mesh, texture-coordinate, face-role or animation changes.
    public static class HeroOriginalMaterial
    {
        // Default-adoption candidate. Explicit 0 is the matched original control;
        // OriginalSeam and replacement-head pilots retain their existing treatment.
        public static bool Enabled => Environment.GetEnvironmentVariable("VISUAL_ORIGINAL_MATERIAL") != "0";

        struct Region
        {
            public Vector3 centre, right, up;
            public float width, height;
        }

        public static void ConfigureFace(MatchHeroLook hero, Material[] materials)
        {
            if (!Enabled || !hero.face || materials == null) return;
            var filter = hero.face.GetComponent<MeshFilter>();
            var mesh = filter ? filter.sharedMesh : null;
            if (!mesh || !mesh.isReadable) return;
            HeroFaceBasis.Get(hero, out var faceRight, out var faceUp, out var faceForward);
            var vertices = mesh.vertices;
            int count = Mathf.Min(materials.Length, mesh.subMeshCount);
            var eyeIndices = new HashSet<int>();
            for (int s = 0; s < count; s++)
                if (materials[s] && materials[s].name.Contains("Sclera"))
                    eyeIndices.UnionWith(mesh.GetTriangles(s));
            if (eyeIndices.Count == 0) return;
            float mid = eyeIndices.Average(i => Vector3.Dot(vertices[i], faceRight));

            bool TryRegions(string kind, out Region[] regions)
            {
                regions = new Region[2];
                for (int side = 0; side < 2; side++)
                {
                    var ids = new HashSet<int>();
                    Vector3 areaNormal = Vector3.zero;
                    for (int sub = 0; sub < count; sub++)
                    {
                        if (!materials[sub] || !materials[sub].name.Contains(kind)) continue;
                        var triangles = mesh.GetTriangles(sub);
                        for (int t = 0; t < triangles.Length; t += 3)
                        {
                            int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
                            var centre = (vertices[a] + vertices[b] + vertices[c]) / 3;
                            if ((Vector3.Dot(centre, faceRight) < mid ? 0 : 1) != side) continue;
                            ids.Add(a); ids.Add(b); ids.Add(c);
                            areaNormal += Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
                        }
                    }
                    if (ids.Count < 3 || areaNormal.sqrMagnitude < 1e-15f) return false;
                    Vector3 normal = areaNormal.normalized;
                    if (Vector3.Dot(normal, faceForward) < 0) normal = -normal;
                    Vector3 up = Vector3.ProjectOnPlane(faceUp, normal).normalized;
                    if (up.sqrMagnitude < .5f) return false;
                    Vector3 right = Vector3.Cross(up, normal).normalized;
                    Vector3 centrePoint = ids.Aggregate(Vector3.zero, (sum, i) => sum + vertices[i]) / ids.Count;
                    float width = ids.Max(i => Mathf.Abs(Vector3.Dot(vertices[i] - centrePoint, right)));
                    float height = ids.Max(i => Mathf.Abs(Vector3.Dot(vertices[i] - centrePoint, up)));
                    if (kind == "Iris") width = height = Mathf.Max(width, height);
                    if (width < .0001f || height < .0001f) return false;
                    regions[side] = new Region { centre = centrePoint, right = right, up = up, width = width, height = height };
                }
                return true;
            }

            int projected = 0, treated = 0;
            foreach (string kind in new[] { "Iris", "Sclera" })
            {
                if (!TryRegions(kind, out var regions))
                {
                    Debug.LogWarning("[HeroOriginalMaterial] skipped unregistered " + kind);
                    continue;
                }
                for (int sub = 0; sub < count; sub++)
                {
                    var m = materials[sub];
                    if (!m || !m.name.Contains(kind) || !m.HasProperty("_EyeMapEnabled")) continue;
                    HeroOriginalSeamAnatomy.ConfigureFeature(m, m.name);
                    for (int side = 0; side < 2; side++)
                    {
                        string suffix = side == 0 ? "L" : "R"; var r = regions[side];
                        m.SetVector("_EyeMap" + suffix, new Vector4(r.centre.x, r.centre.y, r.centre.z, r.width));
                        m.SetVector("_EyeMapUp" + suffix, new Vector4(r.up.x, r.up.y, r.up.z, r.height));
                        m.SetVector("_EyeMapRight" + suffix, r.right);
                    }
                    m.SetFloat("_EyeMapEnabled", 1); projected++;
                }
            }
            for (int sub = 0; sub < count; sub++)
            {
                var m = materials[sub]; if (!m) continue;
                // The legacy BrowSoft is skin-following border paint, rather than
                // the solid brow retained by Eye13. Keep its existing role/color.
                if (m.name.Contains("BrowSoft")) continue;
                if (new[] { "Brow", "Pupil", "Limbal", "Catch" }.Any(n => m.name.Contains(n)))
                { HeroOriginalSeamAnatomy.ConfigureFeature(m, m.name); treated++; }
            }
            Debug.Log("[HeroOriginalMaterial] " + (hero.female ? "Female" : "Male") + ": eye projection materials=" + projected + "; eye/brow colors=" + treated + "; source geometry/UVs/roles/blink unchanged");
        }
    }
}
