using System;
using System.Collections.Generic;
using UnityEngine;

namespace GolfArcade.Tennis
{
    /// Local garment fit overlays, independent of the broad sleeve-envelope blend.
    /// Uses the final posed rig features and the same verified full-to-LOD mapping.
    internal static class HeroGarmentLocalFit
    {
        [Serializable] sealed class Profile
        {
            public int vertexCount;
            public float[] vertices, normals;
            public HeroGarmentPoseCorrectives.Anchor[] anchors;
        }
        const string Prefix = "LocalUnderarm_";
        static readonly Dictionary<bool, Profile> profiles = new();
        static readonly Dictionary<Mesh, int[]> shapeIndices = new();
        static readonly bool enabled = Environment.GetEnvironmentVariable("VISUAL_UNDERARM_FIT") == "1";

        static Profile Load(bool female)
        {
            if (!enabled) return null;
            if (profiles.TryGetValue(female, out var profile)) return profile;
            var text = Resources.Load<TextAsset>("Tennis/Correctives/UnderarmTrace_" + (female ? "Female" : "Male"));
            profile = text ? JsonUtility.FromJson<Profile>(text.text) : null;
            profiles[female] = profile;
            return profile;
        }

        public static void Prepare(bool female, Mesh result, HeroGarmentPoseCorrectives.Profile basis, int[] transfer)
        {
            var p = Load(female);
            if (p == null) return;
            if (p.vertexCount != basis.vertexCount || p.vertices?.Length != basis.vertices.Length || p.anchors == null)
                throw new InvalidOperationException("Local garment fit source mismatch");
            for (int i = 0; i < p.vertices.Length; i++)
                if (Mathf.Abs(p.vertices[i] - basis.vertices[i]) > .00005f)
                    throw new InvalidOperationException("Local garment fit basis moved");
            foreach (var anchor in p.anchors)
            {
                if (anchor.feature?.Length != 28 || anchor.deltas?.Length != p.vertexCount * 3 ||
                    anchor.normalDeltas?.Length != p.vertexCount * 3)
                    throw new InvalidOperationException("Invalid local garment fit anchor");
                var delta = new Vector3[transfer.Length];
                var normals = new Vector3[transfer.Length];
                for (int i = 0; i < transfer.Length; i++)
                {
                    int j = transfer[i];
                    if (j < 0) continue;
                    delta[i] = new Vector3(anchor.deltas[j*3], anchor.deltas[j*3+1], anchor.deltas[j*3+2]);
                    normals[i] = new Vector3(anchor.normalDeltas[j*3], anchor.normalDeltas[j*3+1], anchor.normalDeltas[j*3+2]);
                }
                result.AddBlendShapeFrame(Prefix + anchor.shape, 100, delta, normals, null);
            }
            Debug.Log($"[HeroGarmentLocalFit] {p.anchors.Length} additive underarm anchors; original sleeve blending unchanged");
        }

        static float Confidence(float[] features, float[] anchor)
        {
            float squared = 0;
            for (int i = 0; i < features.Length; i++)
            { float d = features[i] - anchor[i]; squared += d * d; }
            float mse = squared / features.Length;
            return 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.002f, .04f, mse));
        }

        public static void Apply(bool female, SkinnedMeshRenderer top, float[] features)
        {
            var p = Load(female);
            if (p == null) return;
            var mesh = top.sharedMesh;
            if (!shapeIndices.TryGetValue(mesh, out var indices))
            {
                indices = new int[p.anchors.Length];
                for (int i = 0; i < indices.Length; i++) indices[i] = mesh.GetBlendShapeIndex(Prefix + p.anchors[i].shape);
                shapeIndices[mesh] = indices;
            }
            float total = 0;
            foreach (var anchor in p.anchors) total += Confidence(features, anchor.feature);
            float denominator = Mathf.Max(1, total);
            for (int i = 0; i < indices.Length; i++)
                if (indices[i] >= 0)
                    top.SetBlendShapeWeight(indices[i], 100 * Confidence(features, p.anchors[i].feature) / denominator);
        }
    }
}
