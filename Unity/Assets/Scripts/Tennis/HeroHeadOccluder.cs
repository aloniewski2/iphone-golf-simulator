using System.Collections.Generic;
using UnityEngine;

namespace GolfArcade.Tennis
{
    /// Locked V4 head fix (Plan 1B): the hair shell leaves a thin gap under the hat band and the skull
    /// behind it is open, so from an elevated back view the inside of the face and the eye spheres
    /// show through. This fills the head with an inner shell measured from the hero's own skin, hair
    /// and eye vertices (a star-shaped hull from the head centre, shrunk just inside every surface),
    /// shaded with the hair material. Nothing on the locked meshes changes; from outside it is only
    /// ever seen through that gap, where it reads as hair.
    public static class HeroHeadOccluder
    {
        public static Transform Build(ModularHeroLook look)
        {
            var anim = look.animator; if (!anim) return null;
            var head = anim.GetBoneTransform(HumanBodyBones.Head); if (!head) return null;
            var existing = head.Find("Head occluder (inner shell)"); if (existing) return existing;
            var heads = new HashSet<Transform>(head.GetComponentsInChildren<Transform>(true));
            var skin = new List<Vector3>(); var all = new List<Vector3>(); Material hairMat = null;
            foreach (var r in look.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                bool isSkin = r.name.StartsWith("Body_Skin"), isHair = r.name.StartsWith("Hair"), isEye = r.name.StartsWith("Body_EyeSphere");
                if (!isSkin && !isHair && !isEye) continue;
                if (isHair && r.sharedMaterial) hairMat = r.sharedMaterial;
                var src = r.sharedMesh; if (!src) continue;
                var baked = new Mesh(); r.BakeMesh(baked, false);
                var v = baked.vertices; var w = src.boneWeights; var bones = r.bones;
                for (int i = 0; i < v.Length; i++)
                {
                    float hw = 0;
                    if (w.Length == v.Length)
                    {
                        var b = w[i];
                        if (b.weight0 > 0 && bones[b.boneIndex0] && heads.Contains(bones[b.boneIndex0])) hw += b.weight0;
                        if (b.weight1 > 0 && bones[b.boneIndex1] && heads.Contains(bones[b.boneIndex1])) hw += b.weight1;
                        if (b.weight2 > 0 && bones[b.boneIndex2] && heads.Contains(bones[b.boneIndex2])) hw += b.weight2;
                        if (b.weight3 > 0 && bones[b.boneIndex3] && heads.Contains(bones[b.boneIndex3])) hw += b.weight3;
                    }
                    if (hw < .95f) continue;
                    var world = r.transform.position + r.transform.rotation * v[i];
                    var local = head.InverseTransformPoint(world);
                    all.Add(local); if (isSkin) skin.Add(local);
                }
                Object.Destroy(baked);
            }
            if (System.Environment.GetEnvironmentVariable("HERO_DEBUG") == "1")
                foreach (var r in look.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (!r.name.StartsWith("Body_Skin")) continue; var m = r.sharedMesh; var bw = m.boneWeights; var bones = r.bones;
                    for (int sm = 0; sm < m.subMeshCount; sm++)
                    {
                        var idx = new HashSet<int>(m.GetTriangles(sm)); int hv = 0;
                        foreach (var i in idx) { var b = bw[i]; if (bones[b.boneIndex0] && heads.Contains(bones[b.boneIndex0])) hv++; }
                        Debug.Log($"[Skin] submesh {sm} mat={(sm < r.sharedMaterials.Length ? r.sharedMaterials[sm].name : "?")} verts={idx.Count} head={hv}");
                    }
                }
            if (skin.Count < 50) return null;
            Vector3 c = Vector3.zero; foreach (var p in skin) c += p; c /= skin.Count;

            // Unit sphere directions (subdivided octahedron), radius = nearest surface in a narrow cone.
            var mesh = Sphere(3, out var dirs);
            var radii = new float[dirs.Length]; float cosCone = Mathf.Cos(14 * Mathf.Deg2Rad);
            for (int k = 0; k < dirs.Length; k++)
            {
                float best = float.MaxValue;
                foreach (var p in all)
                {
                    var d = p - c; float m = d.magnitude; if (m < 1e-5f) continue;
                    if (Vector3.Dot(d / m, dirs[k]) > cosCone && m < best) best = m;
                }
                radii[k] = best;
            }
            // Fill any direction with no surface from its neighbours' minimum.
            float fallback = float.MaxValue; foreach (var r in radii) if (r < fallback) fallback = r;
            var verts = new Vector3[dirs.Length];
            for (int k = 0; k < dirs.Length; k++)
            {
                float r = radii[k] < float.MaxValue ? radii[k] : fallback;
                verts[k] = c + dirs[k] * Mathf.Max(0, r * .9f - .006f / head.lossyScale.x);   // inset in metres (the head bone is scaled 100x)
            }
            mesh.vertices = verts; mesh.RecalculateNormals(); mesh.RecalculateBounds(); mesh.name = "Hero head occluder";
            if (System.Environment.GetEnvironmentVariable("HERO_DEBUG") == "1") Debug.Log($"[HeadOcc] skin={skin.Count} all={all.Count} bounds={mesh.bounds.size:F4} headScale={head.lossyScale.x}");
            var go = new GameObject("Head occluder (inner shell)");
            go.transform.SetParent(head, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = hairMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        /// Octahedron subdivided `level` times, projected to the unit sphere.
        static Mesh Sphere(int level, out Vector3[] dirs)
        {
            var v = new List<Vector3> { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
            var t = new List<int> { 0,4,3, 0,3,5, 0,5,2, 0,2,4, 1,3,4, 1,5,3, 1,2,5, 1,4,2 };
            var cache = new Dictionary<long, int>();
            int Mid(int a, int b)
            {
                long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                if (cache.TryGetValue(key, out int i)) return i;
                v.Add(((v[a] + v[b]) * .5f).normalized); cache[key] = v.Count - 1; return v.Count - 1;
            }
            for (int l = 0; l < level; l++)
            {
                var nt = new List<int>();
                for (int i = 0; i < t.Count; i += 3)
                {
                    int a = t[i], b = t[i + 1], c = t[i + 2], ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                    nt.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                t = nt;
            }
            for (int i = 0; i < v.Count; i++) v[i] = v[i].normalized;
            dirs = v.ToArray();
            var m = new Mesh(); m.SetVertices(v); m.SetTriangles(t, 0); return m;
        }
    }
}
