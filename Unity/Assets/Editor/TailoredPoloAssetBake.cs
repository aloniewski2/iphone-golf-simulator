using System;
using System.IO;
using System.Linq;
using GolfArcade.Tennis;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
namespace GolfArcade.EditorTools
{
    public static class TailoredPoloAssetBake
    {
        public static void Run()
        {
            int code = 0;
            try
            {
                string sex = Environment.GetEnvironmentVariable("TAILORED_BAKE_SEX") ?? "Male";
                foreach (bool distance in new[] { false, true })
                {
                    string suffix = distance ? "Distance" : "Full";
                    string profilePath = Environment.GetEnvironmentVariable("VISUAL_TAILORED_" + (sex == "Female" ? "FEMALE_" : "") + "MATRIX" + (distance ? "_DISTANCE" : "") + "_PROFILE");
                    if (string.IsNullOrEmpty(profilePath)) throw new InvalidOperationException("Both authored polo profiles are required");
                    var profile = JsonUtility.FromJson<TailoredPoloDeformation.Profile>(File.ReadAllText(profilePath));
                    string root = "Assets/Resources/Tennis/KitsTailored/";
                    var source = AssetDatabase.LoadAssetAtPath<GameObject>(root + sex + (distance ? "Distance" : "") + ".fbx");
                    var renderer = source.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r => r.name == "Kit_Top");
                    var basis = Object.Instantiate(renderer.sharedMesh);
                    basis.name = "TailoredPolo32 " + sex + " " + suffix;
                    var mesh = TailoredPoloDeformation.BuildMesh(profile, basis, renderer.bones);
                    Object.DestroyImmediate(basis);
                    mesh.hideFlags = HideFlags.None;
                    string path = root + sex + "Polo" + suffix + ".asset";
                    mesh.name = "TailoredPolo32 " + sex + " " + suffix + " (polo matrix)";
                    UnityEditorInternal.InternalEditorUtility.SaveToSerializedFileAndForget(new Object[] { mesh }, path, false);
                    Object.DestroyImmediate(mesh);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                    mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if (!mesh || mesh.vertexCount != profile.vertexCount || mesh.blendShapeCount != profile.anchors.Length)
                        throw new InvalidOperationException("Binary garment reload failed: " + path);
                    profile.boneNames = renderer.bones.Select(b => b.name).ToArray();
                    profile.vertices = null; profile.normals = null; profile.baseVertices = null; profile.baseNormals = null;
                    foreach (var shape in profile.anchors) { shape.deltas = null; shape.normalDeltas = null; }
                    File.WriteAllText(root + sex + "Polo" + suffix + "Controls.json", JsonUtility.ToJson(profile));
                    Debug.Log($"[TailoredPoloAssetBake] {suffix}: {mesh.vertexCount} vertices, {mesh.triangles.Length / 3} triangles, {mesh.blendShapeCount} fixed morphs, {profile.boneNames.Length} original bones");
                }
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
            catch (Exception e) { Debug.LogException(e); code = 1; }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }
    }
}
