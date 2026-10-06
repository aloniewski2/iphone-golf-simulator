// DRAFT: safe course-only iOS/Metal exporter, without TropicalArenaImporter.Prepare().
// Execute through heavy.sh and require -buildTarget iOS. No PlayerSettings writes.
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace GolfArcade.EditorTools
{
    public static class PostcardV3IOSExport
    {
        [Serializable] sealed class Evidence
        {
            public string unityProject;public bool requiredSourcesEqualCurrentRepo;public string[] requiredSourceSha256,originalRepoSha256;
            public string gate="UNITY_IOS_EXPORT",status="FAIL",reason,unity,output,buildResult;
            public string[] graphicsApis,scenes,requiredAssets,packedSourceAssets,errors;
            public int errorCount,warningCount;public double seconds;
        }
        public static void Run()
        {
            string output=Environment.GetEnvironmentVariable("GOLF_IOS_OUT");
            string evidencePath=Environment.GetEnvironmentVariable("GOLF_IOS_REPORT");
            var e=new Evidence {unity=Application.unityVersion,unityProject=Directory.GetParent(Application.dataPath).FullName};
            int code=1;bool evidencePathValidated=false;
            try {
                if(string.IsNullOrEmpty(output)||string.IsNullOrEmpty(evidencePath))throw new InvalidOperationException("GOLF_IOS_OUT and GOLF_IOS_REPORT are required");
                output=Path.GetFullPath(output);evidencePath=Path.GetFullPath(evidencePath);e.output=output;
                string projectRoot=Environment.GetEnvironmentVariable("GOLF_PROOF_REPO_ROOT") ?? Directory.GetParent(Application.dataPath).Parent.FullName;
                string v3=Path.Combine(projectRoot,"work/postcard-look/v3")+Path.DirectorySeparatorChar;
                if(!output.StartsWith(v3,StringComparison.Ordinal)||!evidencePath.StartsWith(v3,StringComparison.Ordinal))throw new InvalidOperationException("All export/report output must be beneath this project's v3 directory");
                evidencePathValidated=true;
                if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Cannot export during Play mode");
                if(!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.iOS,BuildTarget.iOS))throw new InvalidOperationException("Installed Unity editor lacks iOS build support");
                if(EditorUserBuildSettings.activeBuildTarget!=BuildTarget.iOS)throw new InvalidOperationException("Start this Unity process with -buildTarget iOS");
                var apis=PlayerSettings.GetGraphicsAPIs(BuildTarget.iOS);e.graphicsApis=apis.Select(a=>a.ToString()).ToArray();
                if(apis.Length!=1||apis[0]!=GraphicsDeviceType.Metal)throw new InvalidOperationException("Current frozen iOS graphics API settings are not Metal-only; exporter will not change them");
                // Resources ensure the actual installed course/shader assets participate in
                // this player export. Existing enabled scene list is used without rewriting it.
                e.scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray();
                if(!e.scenes.Contains("Assets/Scenes/Golf.unity"))throw new InvalidOperationException("Golf.unity is not an enabled build scene");
                e.requiredAssets=new[]{"Assets/Resources/Course/Shaders/GolfLava.shader","Assets/Resources/Course/Shaders/GolfPlants.shader","Assets/Resources/Course/Shaders/GolfSurf.shader",
                    "Assets/Resources/Course/hole_08.fbx","Assets/Resources/Course/hole_09.fbx","Assets/Resources/Course/hole_10.fbx"};
                foreach(var path in e.requiredAssets)if(!File.Exists(Path.Combine(Directory.GetParent(Application.dataPath).FullName,path)))throw new InvalidOperationException("Installed proof asset missing: "+path);
                string FileSha(string path){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();}
                e.requiredSourceSha256=e.requiredAssets.Select(a=>FileSha(Path.Combine(Directory.GetParent(Application.dataPath).FullName,a))).ToArray();
                e.originalRepoSha256=e.requiredAssets.Select(a=>FileSha(Path.Combine(projectRoot,"Unity",a))).ToArray();
                e.requiredSourcesEqualCurrentRepo=e.requiredSourceSha256.SequenceEqual(e.originalRepoSha256);
                if(!e.requiredSourcesEqualCurrentRepo)throw new InvalidOperationException("Actual player source golf FBXs/shaders do not match the current primary repository; refresh a clone before export");
                Directory.CreateDirectory(output);
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {scenes=e.scenes,locationPathName=output,target=BuildTarget.iOS,options=BuildOptions.DetailedBuildReport});
                var s=report.summary;e.buildResult=s.result.ToString();e.errorCount=s.totalErrors;e.warningCount=s.totalWarnings;e.seconds=s.totalTime.TotalSeconds;
                e.errors=report.steps.SelectMany(step=>step.messages).Where(m=>m.type==LogType.Error||m.type==LogType.Exception).Select(m=>m.content).ToArray();
                e.packedSourceAssets=report.packedAssets.SelectMany(p=>p.contents).Select(p=>p.sourceAssetPath).Where(p=>!string.IsNullOrEmpty(p)).Distinct().OrderBy(p=>p).ToArray();
                var missing=e.requiredAssets.Where(a=>!e.packedSourceAssets.Contains(a)).ToArray();
                bool exported=Directory.Exists(Path.Combine(output,"Unity-iPhone.xcodeproj"));
                if(s.result==BuildResult.Succeeded&&s.totalErrors==0&&e.errors.Length==0&&exported&&missing.Length==0) {e.status="PASS";e.reason="Unity successfully exported the iOS player with Metal-only settings and all three actual golf shader/course asset sources included in the detailed build report";code=0;}
                else e.reason="iOS player export did not prove all requirements: result="+s.result+" errors="+s.totalErrors+" XcodeProject="+exported+" missingPackedSources="+string.Join(",",missing);
            } catch(Exception ex) {e.reason=ex.ToString();Debug.LogException(ex);}
            finally {
                if(evidencePathValidated) {Directory.CreateDirectory(Path.GetDirectoryName(evidencePath));File.WriteAllText(evidencePath,JsonUtility.ToJson(e,true)+"\n");}
                Debug.Log("[PostcardV3IOSExport] "+e.status+": "+e.reason);
                if(Application.isBatchMode)EditorApplication.Exit(code);
            }
        }
    }
}
