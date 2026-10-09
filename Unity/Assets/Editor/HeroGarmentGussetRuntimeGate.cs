using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GolfArcade.Tennis;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GolfArcade.EditorTools
{
    // One actual production-path gate. Place under Assets/Editor only when root integrates it.
    [InitializeOnLoad]
    public static class HeroGarmentGussetRuntimeGate
    {
        [Serializable] public sealed class Report
        {
            public bool passed, fullMatchFullReused, unchangedPoseSkipped, independentRenderers, cleanDestruction;
            public string error;
            public int fullVertices, matchVertices, fullSubmeshes, matchSubmeshes;
            public HeroGarmentGussetLifetime.CaptureStats baseline, beforeUnchanged, afterUnchanged, beforeReturn, afterReturn, afterDestroy;
        }
        const string Flag = "HeroGarmentGussetRuntimeGate";
        static IEnumerator standalone;
        static readonly Stack<IEnumerator> steps = new();
        static int lastFrame = -1, waitingFrames;
        static HeroGarmentGussetRuntimeGate() { EditorApplication.update += Tick; }

        // -executeMethod GolfArcade.EditorTools.HeroGarmentGussetRuntimeGate.RunStandalone
        // Requires VISUAL_UNDERARM_GUSSET=1 and VISUAL_UNDERARM_GUSSET_STATS=1 at launch.
        public static void RunStandalone()
        {
            SessionState.SetBool(Flag, true);
            SessionState.SetString(Flag + "out", Path.GetFullPath(Environment.GetEnvironmentVariable("VISUAL_CHARACTER_OUT") ?? "../proof/gusset-runtime-gate"));
            EditorSceneManager.OpenScene("Assets/Scenes/Tennis.unity");
            EditorApplication.isPlaying = true;
        }
        static void Tick()
        {
            if (!SessionState.GetBool(Flag, false) || !EditorApplication.isPlaying || lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            var game = Object.FindFirstObjectByType<TennisGame>();
            if (!game || !game.Initialized)
            {
                if (++waitingFrames > 600) { Debug.LogError("Gusset runtime gate: scene initialization timeout"); Finish(1); }
                return;
            }
            try
            {
                if (standalone == null)
                {
                    HeroGarmentGussetLifetime.BeginCaptureStats();
                    standalone = RunGate(game, SessionState.GetString(Flag + "out", "")); steps.Push(standalone);
                }
                while (steps.Count > 0)
                {
                    var next = steps.Peek();
                    if (!next.MoveNext()) { steps.Pop(); continue; }
                    if (next.Current is IEnumerator nested) { steps.Push(nested); continue; }
                    return;
                }
                HeroGarmentGussetLifetime.EndCaptureStats(); Finish(0);
            }
            catch (Exception e) { Debug.LogException(e); Finish(1); }
        }
        static void Finish(int code) { SessionState.SetBool(Flag, false); EditorApplication.Exit(code); }
        static void Require(bool value, string message) { if (!value) throw new InvalidOperationException("Gusset runtime gate: " + message); }
        static bool Exact(Vector3[] a, Vector3[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i].x != b[i].x || a[i].y != b[i].y || a[i].z != b[i].z) return false;
            return true;
        }
        static HeroTennisDriver Female(TennisGame game, GameObject actorObject)
        {
            var actor = actorObject.AddComponent<TennisActor>();
            actor.Build(true, new Color(.88f, .58f, .30f), false); actor.enabled = false;
            var driver = TennisHeroSetup.Attach(game, actor, false);
            Require(driver && driver.matchLook && driver.matchLook.female, "actual female factory attachment missing");
            driver.enabled = false;
            var corrective = driver.matchLook.GetComponent<HeroGarmentPoseCorrectives>();
            Require(corrective, "production sleeve corrective missing"); corrective.enabled = false;
            return driver;
        }
        static void Detail(HeroGarmentLOD lod, Camera camera, bool match)
        {
            HeroGarmentLOD.ForceFullDetail = !match; HeroGarmentLOD.ForceMatchDetailForReview = match;
            lod.Prepare(camera); Require(lod.UsingMatchDetail == match, "production LOD selection failed");
        }

        // At the end of VisualOverhaulCharacterCapture.Capture, before Finish/Exit:
        // yield return HeroGarmentGussetRuntimeGate.RunGate(game, output);
        // Does not reset capture telemetry. Assertions use deltas and write a separate JSON gate.
        public static IEnumerator RunGate(TennisGame game, string output)
        {
            Require(Environment.GetEnvironmentVariable("VISUAL_UNDERARM_GUSSET") == "1", "launch with VISUAL_UNDERARM_GUSSET=1");
            Require(HeroGarmentGussetLifetime.GetCaptureStats().enabled, "launch with VISUAL_UNDERARM_GUSSET_STATS=1");
            Require(game && game.Initialized && game.GameplayCamera, "initialized TennisGame and camera required");
            Directory.CreateDirectory(output);
            // Flush previously scheduled destruction before taking the cache baseline.
            yield return null;
            bool oldFull = HeroGarmentLOD.ForceFullDetail, oldMatch = HeroGarmentLOD.ForceMatchDetailForReview;
            bool oldManual = game.ManualSimulation, oldGameEnabled = game.enabled;
            var frozen = new Dictionary<Behaviour, bool>();
            foreach (var component in Object.FindObjectsByType<HeroTennisDriver>(FindObjectsSortMode.None)) frozen[component] = component.enabled;
            foreach (var component in Object.FindObjectsByType<HeroGarmentPoseCorrectives>(FindObjectsSortMode.None)) frozen[component] = component.enabled;
            foreach (var component in Object.FindObjectsByType<HeroGarmentLOD>(FindObjectsSortMode.None)) frozen[component] = component.enabled;
            foreach (var pair in frozen) pair.Key.enabled = false;
            game.ManualSimulation = true; game.enabled = false;
            var report = new Report { baseline = HeroGarmentGussetLifetime.GetCaptureStats() };
            var a = new GameObject("Gusset runtime gate female A");
            var b = new GameObject("Gusset runtime gate female B");
            Exception failure = null;
            Mesh aFull = null, aMatch = null, bFull = null;
            try
            {
                var da = Female(game, a); var db = Female(game, b);
                var ha = da.matchLook; var hb = db.matchLook;
                var la = ha.GetComponent<HeroGarmentLOD>(); var lb = hb.GetComponent<HeroGarmentLOD>();
                var ca = ha.GetComponent<HeroGarmentPoseCorrectives>();
                var ta = ha.kit.First(r => r && r.name == "Kit_Top");
                var tb = hb.kit.First(r => r && r.name == "Kit_Top");
                Require(la && lb, "production LOD components missing");
                Detail(la, game.GameplayCamera, false);
                float time = da.ContactOf(HeroTennisDriver.Clip.Forehand);
                Require(da.LengthOf(HeroTennisDriver.Clip.Forehand) > 0, "production Forehand unavailable");
                int fullSourceSubmeshes = ta.sharedMesh.subMeshCount;
                da.Sample(HeroTennisDriver.Clip.Forehand, time);
                aFull = ta.sharedMesh; report.fullVertices = aFull.vertexCount; report.fullSubmeshes = aFull.subMeshCount;
                Require(aFull.subMeshCount == fullSourceSubmeshes && aFull.triangles.Length / 3 == 27470, "full subdivision lost material submeshes or triangles");
                Require(report.fullVertices == 18401, "full gusset topology was not installed");
                var fullPositions = aFull.vertices;
                report.beforeUnchanged = HeroGarmentGussetLifetime.GetCaptureStats();
                ca.Apply(); ca.Apply();
                report.afterUnchanged = HeroGarmentGussetLifetime.GetCaptureStats();
                report.unchangedPoseSkipped = report.afterUnchanged.skippedCalls - report.beforeUnchanged.skippedCalls == 2
                    && report.afterUnchanged.solves == report.beforeUnchanged.solves && Exact(fullPositions, aFull.vertices);
                Require(report.unchangedPoseSkipped, "two unchanged final-pose Apply calls did not reuse corrected output");
                Require(report.afterUnchanged.managedApplyBytes == report.beforeUnchanged.managedApplyBytes, "unchanged Apply allocated managed bytes");

                Detail(la, game.GameplayCamera, true); int matchSourceSubmeshes = ta.sharedMesh.subMeshCount; ca.Apply();
                aMatch = ta.sharedMesh; report.matchVertices = aMatch.vertexCount; report.matchSubmeshes = aMatch.subMeshCount;
                Require(aMatch.subMeshCount == matchSourceSubmeshes && aMatch.triangles.Length / 3 == 23946, "match subdivision lost material submeshes or triangles");
                Require(report.matchVertices == 15989 && aMatch != aFull, "match gusset topology was not independently installed");
                report.beforeReturn = HeroGarmentGussetLifetime.GetCaptureStats();
                Detail(la, game.GameplayCamera, false); ca.Apply();
                report.afterReturn = HeroGarmentGussetLifetime.GetCaptureStats();
                report.fullMatchFullReused = ta.sharedMesh == aFull && report.afterReturn.builds == report.beforeReturn.builds && Exact(fullPositions, aFull.vertices);
                Require(report.fullMatchFullReused, "return to full rebuilt/lost the owned mesh or changed its deterministic output");
                // Also return to match: both cached states must survive repeated crossings.
                Detail(la, game.GameplayCamera, true); ca.Apply();
                Require(ta.sharedMesh == aMatch && HeroGarmentGussetLifetime.GetCaptureStats().builds == report.beforeReturn.builds, "return to match rebuilt its state");
                Detail(la, game.GameplayCamera, false); ca.Apply();
                var beforeB = aFull.vertices;
                Detail(lb, game.GameplayCamera, false);
                var beforeChangedPose = HeroGarmentGussetLifetime.GetCaptureStats();
                db.Sample(HeroTennisDriver.Clip.EmoteSpike, db.LengthOf(HeroTennisDriver.Clip.EmoteSpike) * .5f);
                Require(HeroGarmentGussetLifetime.GetCaptureStats().managedApplyBytes == beforeChangedPose.managedApplyBytes, "changed-pose Apply allocated managed bytes");
                bFull = tb.sharedMesh;
                report.independentRenderers = bFull != aFull && bFull.vertexCount == 18401 && Exact(beforeB, aFull.vertices);
                Require(report.independentRenderers, "second female shared or modified first female's corrected mesh");
                var installed = HeroGarmentGussetLifetime.GetCaptureStats();
                Require(installed.retainedRendererCaches == report.baseline.retainedRendererCaches + 2 && installed.retainedMeshes == report.baseline.retainedMeshes + 3,
                    "two females did not own exactly two renderer caches and three full/match meshes");
            }
            catch (Exception e) { failure = e; report.error = e.ToString(); }
            // Stop all fallback evaluation before deferred destruction. Allow Unity to run OnDestroy.
            a.SetActive(false); b.SetActive(false); Object.Destroy(a); Object.Destroy(b);
            yield return null; yield return null;
            try
            {
                report.afterDestroy = HeroGarmentGussetLifetime.GetCaptureStats();
                report.cleanDestruction = report.afterDestroy.retainedRendererCaches == report.baseline.retainedRendererCaches
                    && report.afterDestroy.retainedMeshes == report.baseline.retainedMeshes && !aFull && !aMatch && !bFull;
                Require(report.cleanDestruction, "hero destruction retained caches or native owned meshes");
                report.passed = failure == null;
            }
            catch (Exception e) { if (failure == null) failure = e; report.error = (report.error ?? "") + "\n" + e; }
            finally
            {
                HeroGarmentLOD.ForceFullDetail = oldFull; HeroGarmentLOD.ForceMatchDetailForReview = oldMatch;
                game.ManualSimulation = oldManual; game.enabled = oldGameEnabled;
                foreach (var pair in frozen) if (pair.Key) pair.Key.enabled = pair.Value;
                File.WriteAllText(Path.Combine(output, "gusset-runtime-gate.json"), JsonUtility.ToJson(report, true));
            }
            if (failure != null) throw failure;
            Debug.Log("GATE: gusset runtime ownership/LOD PASS");
        }
    }
}
