using System.Collections.Generic;
using UnityEngine;

namespace GolfArcade.Tennis
{
    /// Collision proxies for the parts of the locked Hero V4 the torso-slice model does not cover —
    /// head (ellipsoid), thighs and shins (capsules) — measured once from the hero's own skinned
    /// skin vertices, so the racket/arm clearance tests use the real chibi proportions.
    public sealed class HeroBodyProxy
    {
        public Transform head; public Vector3 headCentre, headRadii;   // head-bone local, per-axis radii (bone axes)
        public readonly List<(Transform a, Transform b, float r)> capsules = new List<(Transform, Transform, float)>();

        public static HeroBodyProxy Build(Animator anim, IEnumerable<SkinnedMeshRenderer> renderers)
        {
            var p = new HeroBodyProxy();
            p.head = anim.GetBoneTransform(HumanBodyBones.Head);
            var segs = new List<(Transform a, Transform b)>();
            foreach (var (u, l, f) in new[] {
                (HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot),
                (HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot) })
            {
                segs.Add((anim.GetBoneTransform(u), anim.GetBoneTransform(l)));
                segs.Add((anim.GetBoneTransform(l), anim.GetBoneTransform(f)));
            }
            var headPts = new List<Vector3>(); var segD = new List<float>[segs.Count];
            for (int i = 0; i < segs.Count; i++) segD[i] = new List<float>();
            var headSet = new HashSet<Transform>(p.head.GetComponentsInChildren<Transform>(true));
            foreach (var r in renderers)
            {
                bool skin = r.name.StartsWith("Body_Skin"), hair = r.name.StartsWith("Hair"), legs = skin || r.name.StartsWith("Shorts");
                if (!skin && !hair && !legs) continue;
                var mesh = r.sharedMesh; if (!mesh) continue;
                // Rigidly-weighted vertices, skinned exactly: bone matrix x bind pose (the rig is in cm at 100x scale).
                var v = mesh.vertices; var w = mesh.boneWeights; var bones = r.bones; var bind = mesh.bindposes;
                if (w.Length != v.Length) continue;
                for (int i = 0; i < v.Length; i++)
                {
                    var b = w[i]; if (b.weight0 < .6f || !bones[b.boneIndex0]) continue;
                    var bone = bones[b.boneIndex0]; var world = bone.localToWorldMatrix.MultiplyPoint3x4(bind[b.boneIndex0].MultiplyPoint3x4(v[i]));
                    if ((skin || hair) && headSet.Contains(bone)) headPts.Add(p.head.InverseTransformPoint(world));
                    if (!legs) continue;
                    for (int s = 0; s < segs.Count; s++)
                        if (segs[s].a == bone) segD[s].Add(SegDist(world, segs[s].a.position, segs[s].b.position));
                }
            }
            if (headPts.Count > 20)
            {
                Vector3 mn = headPts[0], mx = headPts[0];
                foreach (var h in headPts) { mn = Vector3.Min(mn, h); mx = Vector3.Max(mx, h); }
                p.headCentre = (mn + mx) * .5f; p.headRadii = (mx - mn) * .5f * .92f;   // slightly inside the hair silhouette
            }
            for (int s = 0; s < segs.Count; s++)
            {
                var d = segD[s]; if (d.Count < 10) continue; d.Sort();
                p.capsules.Add((segs[s].a, segs[s].b, d[(int)(d.Count * .7f)]));
            }
            return p;
        }

        public override string ToString()
        {
            var o = $"head c={headCentre:F5} r={headRadii:F5} scale={(head ? head.lossyScale : Vector3.zero):F3} |";
            foreach (var c in capsules) o += $" {c.a.name}->{c.b.name} r={c.r:F3} len={Vector3.Distance(c.a.position, c.b.position):F3}";
            return o;
        }
        public static float SegDist(Vector3 q, Vector3 a, Vector3 b)
        {
            var ab = b - a; float t = Mathf.Clamp01(Vector3.Dot(q - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            return Vector3.Distance(q, a + ab * t);
        }

        /// Depth of a sphere (centre q, radius r) inside the head or a leg capsule, metres.
        public float Depth(Vector3 q, float r, out string part)
        {
            float best = 0; part = null;
            if (head && headRadii.x > 0)
            {
                var l = head.InverseTransformPoint(q) - headCentre;
                var s = head.lossyScale; var rr = Vector3.Scale(headRadii, s);
                var n = new Vector3(l.x * s.x / rr.x, l.y * s.y / rr.y, l.z * s.z / rr.z);
                float m = n.magnitude; float dist = Vector3.Scale(l, s).magnitude;
                float d = m < 1e-5f ? r + Mathf.Min(rr.x, Mathf.Min(rr.y, rr.z)) : r - (m - 1) * dist / m;
                if (d > best) { best = d; part = "head"; }
            }
            foreach (var c in capsules)
            {
                float d = r + c.r - SegDist(q, c.a.position, c.b.position);
                if (d > best) { best = d; part = c.a.name.Contains("Upper") || c.a.name.Contains("Thigh") ? "thigh" : "shin"; }
            }
            return best;
        }
    }
}
