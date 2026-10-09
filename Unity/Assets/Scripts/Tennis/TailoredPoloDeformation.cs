using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace GolfArcade.Tennis
{
    /// Fixed garment morphs driven by the current skeleton. The sleeve follows the
    /// shoulder's swing without inheriting the upper arm's entire axial twist.
    /// Playback changes only morph controls; source geometry and body bindings stay fixed.
    [DefaultExecutionOrder(1210), DisallowMultipleComponent]
    public sealed class TailoredPoloDeformation : MonoBehaviour
    {
        [Serializable] public sealed class Shape { public string shape; public float[] deltas, normalDeltas; }
        [Serializable] public sealed class Control { public string bone; public int row, column; }
        [Serializable] public sealed class Profile
        {
            public int vertexCount;
            public float[] vertices, normals, projection, baseVertices, baseNormals;
            public Control[] controls;
            public Shape[] anchors;
            public string[] boneNames;
        }
        public const string Prefix = "PoloMatrix_";
        const string ResourceRoot = "Tennis/KitsTailored/";
        static readonly Dictionary<string, Profile> profiles = new();
        readonly List<Mesh> owned = new();
        public MatchHeroLook look;
        Profile profile;
        SkinnedMeshRenderer top;
        Mesh previous;
        int chest;
        int[] shapeIds, controlIds;
        Matrix4x4[] binds, matrices;
        float[] values, lastWeights;
        Vector3[] restHeads;
        readonly int[] upper = new int[2], lower = new int[2], shoulder = new int[2];

        static bool Distance(Mesh mesh) => mesh.name.IndexOf("distance", StringComparison.OrdinalIgnoreCase) >= 0;
        static bool Female(Mesh mesh) => mesh.name.IndexOf("female", StringComparison.OrdinalIgnoreCase) >= 0;
        static string ProfileVariable(Mesh mesh) => "VISUAL_TAILORED_" + (Female(mesh) ? "FEMALE_" : "") + "MATRIX" + (Distance(mesh) ? "_DISTANCE" : "") + "_PROFILE";
        static string Resource(Mesh mesh) => ResourceRoot + (Female(mesh) ? "Female" : "Male") + "Polo" + (Distance(mesh) ? "Distance" : "Full");
        static Profile Load(Mesh mesh)
        {
            string key = Resource(mesh) + "Controls";
            string json;
#if UNITY_EDITOR
            string path = Environment.GetEnvironmentVariable(ProfileVariable(mesh));
            if (!string.IsNullOrEmpty(path))
            {
                key = path;
                if (profiles.TryGetValue(key, out var cached)) return cached;
                json = File.ReadAllText(path);
            }
            else
#endif
            {
                if (profiles.TryGetValue(key, out var cached)) return cached;
                var asset = Resources.Load<TextAsset>(key);
                if (!asset) return null;
                json = asset.text;
            }
            var p = JsonUtility.FromJson<Profile>(json);
            if (p?.controls == null || p.anchors == null || p.vertexCount <= 0 || p.projection == null ||
                p.projection.Length != p.controls.Length * p.anchors.Length)
                throw new InvalidOperationException("Invalid polo deformation controls: " + key);
            foreach (var c in p.controls)
                if (c.row < 0 || c.row > 2 || c.column < 0 || c.column > 3)
                    throw new InvalidOperationException("Invalid polo matrix coefficient");
            profiles[key] = p;
            return p;
        }

        public static Mesh BuildMesh(Profile p, Mesh source, Transform[] palette)
        {
            if (source.vertexCount != p.vertexCount) throw new InvalidOperationException("Polo requires its exact full/distance basis");
            var vertices = source.vertices;
            if (p.vertices?.Length != vertices.Length * 3 || p.normals?.Length != vertices.Length * 3)
                throw new InvalidOperationException("Polo source arrays missing");
            for (int i = 0; i < vertices.Length; i++)
                if ((vertices[i] - V(p.vertices, i)).sqrMagnitude > 1e-10f)
                    throw new InvalidOperationException("Polo source position mismatch");
            int chest = Array.FindIndex(palette, bone => bone && bone.name == "Chest");
            if (chest < 0 || palette.Length != source.bindposes.Length) throw new InvalidOperationException("Polo source bone palette mismatch");
            var result = Instantiate(source);
            result.name = "TailoredPolo32 " + (Female(source) ? "Female " : "Male ") + (Distance(source) ? "Distance" : "Full") + " (polo matrix)";
            var weights = new BoneWeight[vertices.Length];
            for (int i = 0; i < weights.Length; i++) weights[i] = new BoneWeight { boneIndex0 = chest, weight0 = 1 };
            result.boneWeights = weights;
            if (p.baseVertices != null || p.baseNormals != null) {
                if (p.baseVertices?.Length != vertices.Length * 3 || p.baseNormals?.Length != vertices.Length * 3)
                    throw new InvalidOperationException("Transferred polo rest arrays disagree");
                var rest = new Vector3[vertices.Length]; var normals = new Vector3[vertices.Length];
                for (int i = 0; i < vertices.Length; i++) {
                    rest[i] = V(p.baseVertices, i); normals[i] = V(p.baseNormals, i);
                    if (!float.IsFinite(rest[i].sqrMagnitude) || !float.IsFinite(normals[i].sqrMagnitude))
                        throw new InvalidOperationException("Nonfinite transferred polo rest vertex");
                }
                result.vertices = rest; result.normals = normals;
                result.RecalculateBounds(); result.RecalculateTangents();
            }
            foreach (var shape in p.anchors)
            {
                if (shape.deltas?.Length != vertices.Length * 3 || shape.normalDeltas?.Length != vertices.Length * 3)
                    throw new InvalidOperationException("Polo shape array length mismatch");
                var dp = new Vector3[vertices.Length]; var dn = new Vector3[vertices.Length];
                for (int i = 0; i < vertices.Length; i++)
                {
                    dp[i] = V(shape.deltas, i); dn[i] = V(shape.normalDeltas, i);
                    if (!float.IsFinite(dp[i].sqrMagnitude) || !float.IsFinite(dn[i].sqrMagnitude))
                        throw new InvalidOperationException("Nonfinite polo shape");
                }
                result.AddBlendShapeFrame(Prefix + shape.shape, 100, dp, dn, null);
            }
            return result;
        }

        public static Mesh Prepare(MatchHeroLook hero, SkinnedMeshRenderer renderer, Mesh source, Transform[] palette = null)
        {
            if (!hero || hero.golfKit || !renderer || renderer.name != "Kit_Top" || !source) return source;
            var p = Load(source);
            if (p == null) return source;
            palette ??= renderer.bones;
            if (p.boneNames != null && p.boneNames.Length > 0 && !p.boneNames.SequenceEqual(palette.Select(b => b ? b.name : "")))
                throw new InvalidOperationException("Polo bone palette order changed");
            var component = hero.GetComponent<TailoredPoloDeformation>();
            if (!component) component = hero.gameObject.AddComponent<TailoredPoloDeformation>();
            component.look = hero;
            if (source.GetBlendShapeIndex(Prefix + p.anchors[0].shape) >= 0) return source;
            var baked = Resources.Load<Mesh>(Resource(source));
#if UNITY_EDITOR
            bool authoring = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(ProfileVariable(source)));
            if (authoring) baked = null;
#endif
            if (baked)
            {
                if (baked.vertexCount != p.vertexCount || baked.blendShapeCount != p.anchors.Length)
                    throw new InvalidOperationException("Baked polo asset and controls disagree");
                return baked;
            }
            var result = BuildMesh(p, source, palette);
            result.hideFlags = HideFlags.DontSave;
            component.owned.Add(result);
            Debug.Log($"[TailoredPoloDeformation] {result.name}: {result.vertexCount} vertices, {result.blendShapeCount} fixed shapes; body and skeleton unchanged");
            return result;
        }

        static Vector3 V(float[] a, int i) => new(a[i * 3], a[i * 3 + 1], a[i * 3 + 2]);
        void OnDestroy() { foreach (var mesh in owned) if (mesh) Destroy(mesh); }
        int Bone(string name) => Array.FindIndex(top.bones, bone => bone && bone.name == name);
        bool Init()
        {
            if (!look) look = GetComponent<MatchHeroLook>();
            if (!look) return false;
            if (!top) top = look.kit?.FirstOrDefault(renderer => renderer && renderer.name == "Kit_Top");
            if (!top || !top.sharedMesh) return false;
            if (previous != top.sharedMesh)
            {
                var selected = Load(top.sharedMesh);
                if (selected == null) return false;
                profile = selected;
                binds = top.sharedMesh.bindposes;
                chest = Bone("Chest");
                shapeIds = profile.anchors.Select(a => top.sharedMesh.GetBlendShapeIndex(Prefix + a.shape)).ToArray();
                if (chest < 0 || shapeIds.Any(i => i < 0)) throw new InvalidOperationException("Polo morph or palette mismatch");
                matrices = new Matrix4x4[top.bones.Length + 2];
                restHeads = binds.Select(m => m.inverse.MultiplyPoint3x4(Vector3.zero)).ToArray();
                for (int side = 0; side < 2; side++)
                {
                    string name = side == 0 ? "Left" : "Right";
                    upper[side] = Bone(name + "UpperArm"); lower[side] = Bone(name + "LowerArm"); shoulder[side] = Bone(name + "Shoulder");
                    if (upper[side] < 0 || lower[side] < 0 || shoulder[side] < 0) throw new InvalidOperationException("Polo sleeve bones missing");
                }
                controlIds = profile.controls.Select(c => c.bone == "Swing_LeftUpperArm" ? top.bones.Length :
                    c.bone == "Swing_RightUpperArm" ? top.bones.Length + 1 : Bone(c.bone)).ToArray();
                if (controlIds.Any(i => i < 0)) throw new InvalidOperationException("Polo control bone missing");
                values = new float[profile.controls.Length];
                lastWeights = new float[shapeIds.Length];
                for (int i = 0; i < lastWeights.Length; i++) lastWeights[i] = float.NaN;
                previous = top.sharedMesh;
            }
            return profile != null;
        }

        public void Apply()
        {
            if (!Init()) return;
            var toMesh = top.transform.worldToLocalMatrix;
            for (int i = 0; i < top.bones.Length; i++) matrices[i] = toMesh * top.bones[i].localToWorldMatrix * binds[i];
            for (int side = 0; side < 2; side++)
            {
                var origin = restHeads[upper[side]];
                var axis = (restHeads[lower[side]] - origin).normalized;
                var parent = matrices[shoulder[side]];
                var arm = matrices[upper[side]];
                var swing = Quaternion.FromToRotation(parent.MultiplyVector(axis), arm.MultiplyVector(axis));
                var rotation = Matrix4x4.Rotate(swing) * parent;
                var target = arm;
                for (int col = 0; col < 3; col++) for (int row = 0; row < 3; row++) target[row, col] = rotation[row, col];
                var offset = arm.MultiplyPoint3x4(origin) - target.MultiplyVector(origin);
                target.SetColumn(3, new Vector4(offset.x, offset.y, offset.z, 1));
                matrices[top.bones.Length + side] = target;
            }
            var inverseChest = matrices[chest].inverse;
            for (int i = 0; i < matrices.Length; i++) matrices[i] = inverseChest * matrices[i];
            for (int i = 0; i < values.Length; i++)
            {
                var c = profile.controls[i];
                values[i] = matrices[controlIds[i]][c.row, c.column] - (c.row == c.column ? 1 : 0);
            }
            for (int shape = 0; shape < shapeIds.Length; shape++)
            {
                double sum = 0; int offset = shape * values.Length;
                for (int j = 0; j < values.Length; j++) sum += profile.projection[offset + j] * values[j];
                float value = (float)(sum * 100);
                if (!float.IsFinite(lastWeights[shape]) || Mathf.Abs(value - lastWeights[shape]) > 1e-5f)
                {
                    top.SetBlendShapeWeight(shapeIds[shape], value); lastWeights[shape] = value;
                }
            }
        }
        void LateUpdate()
        {
            var driver = GetComponent<HeroTennisDriver>();
            if (driver && driver.isActiveAndEnabled && driver.actor && !(driver.game && driver.game.ReplayPlaying)) return;
            Apply();
        }
    }
}
