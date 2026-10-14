using System.Collections.Generic;
using System.Linq;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;

namespace GolfArcade.PlayTests
{
    /// Which part of a frame took the time, for the frames that hitch: Unity's own markers, read each frame (ProfilerRecorder).
    public sealed class HitchProbe : MonoBehaviour
    {
        static readonly string[] Names = {
            "Update.ScriptRunBehaviourUpdate", "PreLateUpdate.ScriptRunBehaviourLateUpdate", "PostLateUpdate.FinishFrameRendering",
            "Camera.Render", "Canvas.SendWillRenderCanvases", "UGUI.Rendering.UpdateBatches", "Canvas.BuildBatch", "GC.Collect", "GC.Alloc",
            "Loading.ReadObject", "Shader.CreateGPUProgram", "CreateGPUProgram", "Gfx.WaitForPresentOnGfxThread", "Gfx.WaitForGfxCommandsFromMainThread",
            "Physics.Simulate", "Physics.SyncTransforms", "Director.ProcessFrame", "ParticleSystem.Update", "Instantiate", "GameObject.Activate",
            "Mesh.Bake", "MeshCollider.Bake", "RenderPipelineManager.DoRenderLoop_Internal()", "Inl_ScriptableRenderer.Execute", "UIR.Update",
            "AsyncUploadManager.ScheduleAsyncRead", "Texture2D.Apply", "Texture.Apply", "Graphics.Blit", "ReadPixels", "EncodeToJPG", "PlayerLoop",
            "GolfProbe.NetworkStep", "GolfProbe.Frame", "GolfProbe.Launch", "GolfProbe.StartReplay", "GolfProbe.FrameMinimap", "GolfProbe.ControllerReading",
            "GolfProbe.LineUp", "GolfProbe.PhoneMap", "GolfProbe.ShowShotResult" };
        readonly List<(string name, ProfilerRecorder rec)> recs = new();
        public readonly List<string> Report = new();
        float last;
        readonly HashSet<string> found = new(); float nextLookup;
        void OnEnable() { Lookup(); last = Time.realtimeSinceStartup; }
        /// Markers by name, whatever their category (some appear only once their code first runs: looked up again each second).
        void Lookup()
        {
            var handles = new List<ProfilerRecorderHandle>(); ProfilerRecorderHandle.GetAvailable(handles);
            foreach (var h in handles)
            {
                var name = ProfilerRecorderHandle.GetDescription(h).Name;
                if (found.Contains(name) || System.Array.IndexOf(Names, name) < 0) continue;
                found.Add(name); recs.Add((name, new ProfilerRecorder(h, 1, ProfilerRecorderOptions.Default)));
            }
        }
        void OnDisable() { foreach (var r in recs) r.rec.Dispose(); recs.Clear(); found.Clear(); }
        void Update()
        {
            float now = Time.realtimeSinceStartup, dt = now - last; last = now;
            if (now > nextLookup && found.Count < Names.Length) { nextLookup = now + 1; Lookup(); }
            if (dt < .25f) return;
            var top = recs.Where(r => r.rec.Valid && r.rec.LastValue > 30_000_000).Select(r => (r.name, ms: r.rec.LastValue / 1e6)).OrderByDescending(r => r.ms).Take(8);
            Report.Add($"{Time.realtimeSinceStartup:F2} frame {dt * 1000:F0} ms: " + string.Join(", ", top.Select(t => $"{t.name} {t.ms:F0}")));
        }
    }
}
