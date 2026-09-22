using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GolfArcade.EditorTools
{
    /// One-shot project wiring so a fresh clone plays: the Golf scene with its single
    /// bootstrap object, build settings, and iPhone player settings. Safe to run again.
    /// Run from the menu or headless: -executeMethod GolfArcade.EditorTools.ProjectSetup.Run
    public static class ProjectSetup
    {
        const string ScenePath = "Assets/Scenes/Golf.unity";

        [MenuItem("Golf Arcade/Set Up Project")]
        public static void Run()
        {
            EnsureScene();
            ConfigurePlayer();
            EnsureShadersShip();
            AssetDatabase.SaveAssets();
            Debug.Log("Golf Arcade project set up.");
        }

        static void EnsureScene()
        {
            Scene scene;
            if (System.IO.File.Exists(ScenePath))
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            else
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
            if (!Object.FindFirstObjectByType<Game.GolfGame>())
            {
                var go = new GameObject("Golf Game");
                go.AddComponent<Game.GolfGame>();
            }
            EditorSceneManager.SaveScene(scene, ScenePath);
            if (!EditorBuildSettings.scenes.Any(s => s.path == ScenePath))
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        /// Every material in the game is made at runtime with Shader.Find, and a player build
        /// strips any shader no asset references — so without this the course, ball and golfer
        /// come out invisible on the phone while the UI (always-included shaders) still draws.
        static readonly string[] RuntimeShaders = { "Standard", "Unlit/Color", "GolfArcade/VertexColorUnlit", "GolfArcade/ParticleSoft", "GolfArcade/HoleMask", "GolfArcade/HoleInside" };

        public static void EnsureShadersShip()
        {
            var graphics = new SerializedObject(UnityEngine.Rendering.GraphicsSettings.GetGraphicsSettings());
            var list = graphics.FindProperty("m_AlwaysIncludedShaders");
            foreach (var name in RuntimeShaders)
            {
                var shader = Shader.Find(name);
                if (!shader) { Debug.LogError($"Shader {name} not found"); continue; }
                bool present = false;
                for (int i = 0; i < list.arraySize; i++)
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue == shader) present = true;
                if (present) continue;
                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
            }
            graphics.ApplyModifiedPropertiesWithoutUndo();
        }

        static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "aloniewski";
            PlayerSettings.productName = "Golf Arcade";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "com.aloniewski.golfarcade.unity");
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.iOS.targetOSVersionString = "17.0";
            PlayerSettings.iOS.appleEnableAutomaticSigning = true;
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneOnly;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetArchitecture(NamedBuildTarget.iOS, 1); // ARM64
            PlayerSettings.iOS.requiresFullScreen = true;
            PlayerSettings.statusBarHidden = true;
            EditorUserBuildSettings.selectedBuildTargetGroup = BuildTargetGroup.iOS;
        }
    }
}
