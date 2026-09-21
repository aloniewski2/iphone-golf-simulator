using GolfArcade.Tennis;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GolfArcade.EditorTools
{
    public static class TennisSceneBuilder
    {
        [MenuItem("Golf Arcade/Tennis/Open Rally Lab")]
        public static void Open()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene("Assets/Scenes/Tennis.unity");
        }

        // CLI setup; invoked explicitly, never overwrites a scene during normal domain reloads.
        public static void Build()
        {
            AssetDatabase.Refresh();
            if (!System.IO.File.Exists("Assets/Scenes/Tennis.unity"))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                new GameObject("Tennis rally prototype").AddComponent<TennisGame>();
                EditorSceneManager.SaveScene(scene, "Assets/Scenes/Tennis.unity");
            }
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == "Assets/Scenes/Tennis.unity")) scenes.Add(new EditorBuildSettingsScene("Assets/Scenes/Tennis.unity",true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
        }
    }
}
