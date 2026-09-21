using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GolfArcade.EditorTools
{
    /// Small, local-only editor review queue; no arbitrary code execution or scene overwrite.
    [InitializeOnLoad]
    public static class TennisReviewCommands
    {
        const string Request = "Library/TennisReview.request";
        const string Response = "Library/TennisReview.response";
        static TennisReviewCommands() { EditorApplication.update += Poll; }
        static void Poll()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(Request)) return;
            string command = File.ReadAllText(Request).Trim(); File.Delete(Request);
            var scene = SceneManager.GetActiveScene();
            File.WriteAllText(Response,$"{command}: scene={scene.path}, dirty={scene.isDirty}, playing={EditorApplication.isPlaying}\n");
            if (command == "status") return;
            if (EditorApplication.isPlaying || scene.isDirty)
            { File.AppendAllText(Response,"BLOCKED: stop Play mode and save the scene before running review tests.\n"); return; }
            if (command == "edit-tests") TestMenu.RunEditMode();
            else if (command == "play-tests") TestMenu.RunPlayMode();
            else if (command == "open-tennis") TennisSceneBuilder.Open();
            else File.AppendAllText(Response,"Unknown command; no action taken.\n");
        }
    }
}
