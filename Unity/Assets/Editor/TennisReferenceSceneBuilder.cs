using GolfArcade.Tennis;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GolfArcade.EditorTools
{
    /// Builds Assets/Scenes/TennisReference.unity: the tennis game plus the reference-matching
    /// stage (TennisReferenceMatch). Kept out of the build settings -- it is a review scene, not
    /// part of the game.
    public static class TennisReferenceSceneBuilder
    {
        public const string Path = "Assets/Scenes/TennisReference.unity";

        [MenuItem("Golf Arcade/Tennis/Open Reference Match Scene")]
        public static void Open()
        {
            if (!System.IO.File.Exists(Path)) Build();
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(Path);
        }

        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject("Tennis reference match");
            var game = go.AddComponent<TennisGame>();
            game.ManualSimulation = true;
            go.AddComponent<TennisReferenceMatch>();
            EditorSceneManager.SaveScene(scene, Path);
            AssetDatabase.SaveAssets();
            Debug.Log("REFERENCE scene saved " + Path);
        }
    }
}
