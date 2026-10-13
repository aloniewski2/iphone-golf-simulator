using System;
using System.Linq;
using UnityEngine;

namespace GolfArcade.Tennis
{
    /// CLOTHING V2: the shirt yields where the forearm / hand presses into it. Kit_Top carries blend shapes CV2_PoseNN built at anchor poses
    /// (work/clothing-v2/py/correctives.py); each frame they are blended by the same 28 segment-direction features as GolfGarmentCorrectives
    /// (inverse-distance weights over the anchors, neutral anchors pull the blend to zero in ordinary poses). Garment only: body, rig and clips untouched.
    [DefaultExecutionOrder(1220), DisallowMultipleComponent]
    public sealed class KitV2PoseCorrectives : MonoBehaviour
    {
        [Serializable] sealed class Anchor { public string shape; public float[] feature; }
        [Serializable] sealed class Profile { public Anchor[] anchors; }
        static readonly string[,] Segments = { { "Spine", "Chest" }, { "Chest", "Neck" }, { "LeftShoulder", "LeftUpperArm" }, { "LeftUpperArm", "LeftLowerArm" }, { "LeftLowerArm", "LeftHand" },
            { "RightShoulder", "RightUpperArm" }, { "RightUpperArm", "RightLowerArm" }, { "RightLowerArm", "RightHand" } };
        public MatchHeroLook look;
        Profile profile; SkinnedMeshRenderer top; Transform[,] joints; int[] shapes; float[] weights;
        readonly Vector3[] directions = new Vector3[8]; readonly float[] features = new float[28];

        public static void Attach(MatchHeroLook look)
        {
            if (!look || !look.kitV2) return;
            var c = look.GetComponent<KitV2PoseCorrectives>() ?? look.gameObject.AddComponent<KitV2PoseCorrectives>();
            c.look = look;
        }

        bool Init()
        {
            if (profile != null) return true;
            if (!look) look = GetComponent<MatchHeroLook>();
            if (!look || !look.kitV2 || look.kit == null) return false;
            top = look.kit.FirstOrDefault(r => r && r.name == "Kit_Top");
            if (!top || !top.sharedMesh || top.sharedMesh.blendShapeCount == 0) return false;
            string sport = look.golfKit ? "Golf" : "Tennis", sex = look.female ? "Female" : "Male";
            var text = Resources.Load<TextAsset>($"{sport}/KitsV2/{sex}/KitV2_{sport}_{sex}_Top_correctives");
            if (!text) return false;
            var p = JsonUtility.FromJson<Profile>(text.text);
            if (p?.anchors == null || p.anchors.Length == 0) return false;
            var all = look.GetComponentsInChildren<Transform>(true);
            joints = new Transform[8, 2];
            for (int i = 0; i < 8; i++) for (int j = 0; j < 2; j++) { joints[i, j] = all.FirstOrDefault(t => t.name == Segments[i, j]); if (!joints[i, j]) return false; }
            shapes = new int[p.anchors.Length]; weights = new float[p.anchors.Length];
            for (int a = 0; a < shapes.Length; a++) shapes[a] = string.IsNullOrEmpty(p.anchors[a].shape) ? -1 : top.sharedMesh.GetBlendShapeIndex(p.anchors[a].shape);
            profile = p;
            return true;
        }

        public void Apply()
        {
            if (!Init()) return;
            for (int i = 0; i < 8; i++) directions[i] = (joints[i, 1].position - joints[i, 0].position).normalized;
            int f = 0; for (int i = 0; i < 8; i++) for (int j = i + 1; j < 8; j++) features[f++] = Vector3.Dot(directions[i], directions[j]);
            double sum = 0;
            for (int a = 0; a < weights.Length; a++)
            {
                double d = 0; var fa = profile.anchors[a].feature;
                for (int k = 0; k < features.Length; k++) { double e = features[k] - fa[k]; d += e * e; }
                d /= features.Length; weights[a] = (float)(1 / Math.Pow(Math.Max(d, 1e-10), 2)); sum += weights[a];
            }
            for (int a = 0; a < weights.Length; a++) if (shapes[a] >= 0) top.SetBlendShapeWeight(shapes[a], (float)(100 * weights[a] / sum));
        }

        void LateUpdate() => Apply();
    }
}
