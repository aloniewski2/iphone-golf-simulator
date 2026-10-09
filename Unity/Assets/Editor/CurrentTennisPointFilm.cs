#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using GolfArcade.Game;
using GolfArcade.Tennis;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GolfArcade.EditorTools
{
    /// Records the current game, camera, HUD, physics and autoplay controller.
    /// HERO_VENUE=resort|skyscraper|volcano FILM_OUT=<new directory>
    /// Optional FILM_AUDIO=1, FILM_SEX=male|female, FILM_MIN_SHOTS=5, FILM_MAX_ATTEMPTS=4.
    /// Output: frames/game_00000.jpg at 30 fps, trace.csv, audit.txt and optional audio.wav.
    [InitializeOnLoad]
    public static class CurrentTennisPointFilm
    {
        const string Flag = "CurrentTennisPointFilm";
        static IEnumerator script;
        static int lastFrame = -1;
        static WavWriter audio;
        static bool audioRunning;
        static string output;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        static CurrentTennisPointFilm() { EditorApplication.update += Tick; }
        public static void Run()
        {
            script = null; lastFrame = -1;
            EditorSceneManager.OpenScene("Assets/Scenes/Tennis.unity");
            SessionState.SetBool(Flag, true); EditorApplication.isPlaying = true;
        }
        static void Tick()
        {
            if (!SessionState.GetBool(Flag, false) || !EditorApplication.isPlaying || lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            var game = Object.FindFirstObjectByType<TennisGame>();
            if (!game || !game.Initialized) return;
            try { if (script == null) script = Record(game); if (!script.MoveNext()) Finish(0); }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (!string.IsNullOrEmpty(output)) File.WriteAllText(Path.Combine(output, "error.txt"), e.ToString());
                Finish(1);
            }
        }
        static void Finish(int code)
        {
            EndAudio(); SessionState.SetBool(Flag, false); Time.captureFramerate = 0;
            if (Application.isBatchMode) EditorApplication.Exit(code); else EditorApplication.isPlaying = false;
        }
        static string Env(string key, string fallback) => Environment.GetEnvironmentVariable(key) ?? fallback;
        static int Number(string key, int fallback, int min, int max) => int.TryParse(Env(key, ""), out int n) ? Mathf.Clamp(n, min, max) : fallback;
        static string F(float f) => f.ToString("0.00000", Inv);
        static string Csv(string s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";
        static string V(Vector3 v) => F(v.x) + "," + F(v.y) + "," + F(v.z);
        static bool Ended(TennisGame game) => game.Flow == TennisGame.Phase.PointOver || game.Flow == TennisGame.Phase.MatchOver;

        sealed class Take
        {
            public string directory, audioStatus = "disabled";
            public int attempt, frames, ticks, shots, resultTicks;
            public bool sawServe, sawRally, playerSwing, opponentSwing, complete, representative;
        }

        static IEnumerator Record(TennisGame game)
        {
            output = Path.GetFullPath(Env("FILM_OUT", "../proof/current-tennis-point"));
            Directory.CreateDirectory(output);
            if (Directory.Exists(Path.Combine(output, "frames"))) throw new IOException("FILM_OUT already contains frames; choose a new output directory.");
            int minShots = Number("FILM_MIN_SHOTS", 5, 2, 40), maxAttempts = Number("FILM_MAX_ATTEMPTS", 4, 1, 12);
            int maxTicks = Number("FILM_MAX_POINT_SECONDS", 45, 10, 120) * 60;
            Time.captureFramerate = 60;
            TennisGame.AutoPlayTimingJitter = .20f; // Existing human-like input timing option; real game decides misses and point outcome.
            game.ManualSimulation = false; game.AutoPlay = false; game.NativeControlled = true; game.SetControllerSetup(false);
            var presentation = game.GetComponent<TennisPresentation>(); if (presentation) presentation.Finish();
            string sex = Env("FILM_SEX", "");
            if (sex == "male" || sex == "female") game.SelectCharacter(sex == "female");
            // Public reset to the current point's real serve, before recording begins.
            game.Refeed();
            for (int warm = 0; warm < 30; warm++) yield return null;
            if (!game.GameplayCamera || Camera.main != game.GameplayCamera) throw new InvalidOperationException("Expected the live gameplay camera to be Camera.main.");
            game.GameplayCamera.aspect = 1280f / 720f;
            game.AutoPlayLean = true; game.AutoPlay = true;
            var attempts = new List<Take>(); Take best = null;
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                int waiting = 0;
                while (!game.Serving && waiting++ < 60 * 20) yield return null;
                if (!game.Serving) break;
                var take = new Take { attempt = attempt, directory = Path.Combine(output, "attempt_" + attempt.ToString("00")) };
                if (Directory.Exists(take.directory)) throw new IOException("Attempt output already exists: " + take.directory);
                Directory.CreateDirectory(Path.Combine(take.directory, "frames"));
                var trace = new List<string> { "tick,unityFrame,seconds,videoFrame,phase,rallyShots,secondServe,score,feedback,playerStroke,playerSwing,playerClip,playerClipTime,opponentStroke,opponentSwing,opponentClip,opponentClipTime,ballX,ballY,ballZ,ballVX,ballVY,ballVZ,playerX,playerY,playerZ,opponentX,opponentY,opponentZ" };
                var audioAudit = new List<string> { "tick,unityFrame,videoSeconds,dspSeconds,sampleFramesThisTick,totalSampleFrames" };
                var playerDriver = game.Player.GetComponentInChildren<HeroTennisDriver>();
                var opponentDriver = game.Opponent.GetComponentInChildren<HeroTennisDriver>();
                take.audioStatus = BeginAudio(Path.Combine(take.directory, "audio.wav"));
                for (int tick = 0; tick < maxTicks; tick++)
                {
                    yield return null; take.ticks++;
                    int audioFrames = CaptureAudio();
                    audioAudit.Add(string.Join(",", tick, Time.frameCount, F((tick + 1) / 60f), AudioSettings.dspTime.ToString("0.000000", Inv), audioFrames, audio != null ? audio.SampleFrames : 0));
                    take.sawServe |= game.Serving; take.sawRally |= game.Flow == TennisGame.Phase.Rally;
                    take.shots = Mathf.Max(take.shots, game.RallyShots);
                    take.playerSwing |= game.Player.Swinging; take.opponentSwing |= game.Opponent.Swinging;
                    if (Ended(game)) take.resultTicks++;
                    int videoFrame = tick % 2 == 1 ? take.frames : -1;
                    trace.Add(string.Join(",", tick, Time.frameCount, F((tick + 1) / 60f), videoFrame, game.Flow, game.RallyShots, game.SecondServe,
                        Csv(game.Match.Scoreboard), Csv(game.Feedback), game.Player.Kind, game.Player.Swinging,
                        playerDriver ? playerDriver.PlayingClip.ToString() : "", playerDriver ? F(playerDriver.PlayingClipTime) : "",
                        game.Opponent.Kind, game.Opponent.Swinging, opponentDriver ? opponentDriver.PlayingClip.ToString() : "", opponentDriver ? F(opponentDriver.PlayingClipTime) : "",
                        V(game.BallPosition), V(game.BallVelocity), V(game.Player.transform.position), V(game.Opponent.transform.position)));
                    if (videoFrame >= 0)
                    {
                        string path = Path.Combine(take.directory, "frames", "game_" + take.frames.ToString("00000") + ".jpg");
                        if (GameCapture.Save(path, 1280, 720) == null) throw new InvalidOperationException("Gameplay capture failed.");
                        take.frames++;
                    }
                    // An even number of simulation frames keeps WAV and 30 fps frames aligned.
                    if (take.resultTicks >= 120 && tick % 2 == 1)
                    {
                        take.complete = take.sawServe && take.sawRally; break;
                    }
                }
                bool emptyAudio = audioRunning && audio.SampleFrames == 0;
                if (audioRunning) take.audioStatus = emptyAudio ? "unavailable: AudioRenderer started but returned zero sample frames" : "recorded " + audio.SampleFrames + " sample frames at " + audio.SampleRate + " Hz / " + audio.Channels + " channels";
                EndAudio();
                if (emptyAudio) File.Delete(Path.Combine(take.directory, "audio.wav"));
                File.WriteAllLines(Path.Combine(take.directory, "audio-audit.csv"), audioAudit);
                File.WriteAllLines(Path.Combine(take.directory, "trace.csv"), trace);
                take.representative = take.complete && take.shots >= minShots && take.playerSwing && take.opponentSwing;
                attempts.Add(take);
                Debug.Log($"[CurrentTennisPointFilm] attempt={attempt} shots={take.shots} complete={take.complete} frames={take.frames} representative={take.representative}");
                if (take.complete && (best == null || take.shots > best.shots)) best = take;
                if (take.representative || game.Flow == TennisGame.Phase.MatchOver) break;
                if (!Ended(game)) throw new TimeoutException("Live point exceeded FILM_MAX_POINT_SECONDS; partial take retained.");
            }
            if (best == null) throw new InvalidOperationException("No complete live serve-to-score point recorded; attempts retained.");
            Directory.Move(Path.Combine(best.directory, "frames"), Path.Combine(output, "frames"));
            File.Copy(Path.Combine(best.directory, "trace.csv"), Path.Combine(output, "trace.csv"));
            File.Copy(Path.Combine(best.directory, "audio-audit.csv"), Path.Combine(output, "audio-audit.csv"));
            if (File.Exists(Path.Combine(best.directory, "audio.wav"))) File.Move(Path.Combine(best.directory, "audio.wav"), Path.Combine(output, "audio.wav"));
            var notes = new List<string> {
                "Actual live TennisGame AutoPlay / AutoPlayLean; game physics, animation, camera and HUD retained. No injected ball, sampled pose or material override.",
                "Venue="+TennisVenue.Current+"; sex="+(game.FemalePlayer ? "female":"male"),
                "Simulation=60 fps; video=30 fps; resolution=1280x720; format=JPG quality92; AutoPlayTimingJitter=0.20 seconds.",
                "Selected attempt="+best.attempt+"; frames="+best.frames+"; seconds="+F(best.frames/30f)+"; maxRallyShots="+best.shots+"; scoreHoldSeconds="+F(best.resultTicks/60f),
                "GATE: complete live serve/rally/point result PASS",
                "Representative rally="+best.representative+"; minimum requested shots="+minShots,
                "Audio="+best.audioStatus,
                "VISUAL_CHARACTER_SKIN_POLISH="+Env("VISUAL_CHARACTER_SKIN_POLISH","unset")+"; VISUAL_CHARACTER_NORMALS="+Env("VISUAL_CHARACTER_NORMALS","unset")
            };
            foreach (var take in attempts) notes.Add("Attempt "+take.attempt+": shots="+take.shots+", complete="+take.complete+", frames="+take.frames+", representative="+take.representative);
            File.WriteAllLines(Path.Combine(output, "audit.txt"), notes);
            Debug.Log("[CurrentTennisPointFilm] complete " + output);
        }

        static string BeginAudio(string path)
        {
            if (Env("FILM_AUDIO", "0") != "1") return "disabled";
            if (!AudioRenderer.Start()) return "unavailable: AudioRenderer.Start returned false";
            audioRunning = true;
            var mode = AudioSettings.GetConfiguration().speakerMode;
            int channels = mode == AudioSpeakerMode.Mono ? 1 : mode == AudioSpeakerMode.Quad ? 4 : mode == AudioSpeakerMode.Surround ? 5 : mode == AudioSpeakerMode.Mode5point1 ? 6 : mode == AudioSpeakerMode.Mode7point1 ? 8 : 2;
            audio = new WavWriter(path, AudioSettings.outputSampleRate, channels);
            return "recording";
        }
        static int CaptureAudio()
        {
            if (!audioRunning) return 0;
            int samples = AudioRenderer.GetSampleCountForCaptureFrame();
            if (samples <= 0) return 0;
            using (var buffer = new NativeArray<float>(samples * audio.Channels, Allocator.Temp))
            {
                if (!AudioRenderer.Render(buffer)) throw new InvalidOperationException("AudioRenderer.Render failed; partial audio and frames retained.");
                audio.Write(buffer);
            }
            return samples;
        }
        static void EndAudio()
        {
            if (audioRunning) AudioRenderer.Stop(); audioRunning = false;
            if (audio != null) { audio.Dispose(); audio = null; }
        }
        sealed class WavWriter : IDisposable
        {
            readonly BinaryWriter writer;
            public readonly int SampleRate, Channels;
            public long SampleFrames { get; private set; }
            public WavWriter(string path, int rate, int channels)
            {
                SampleRate = rate; Channels = channels; writer = new BinaryWriter(File.Create(path));
                writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(0); writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write(16); writer.Write((short)1); writer.Write((short)channels); writer.Write(rate); writer.Write(rate * channels * 2);
                writer.Write((short)(channels * 2)); writer.Write((short)16); writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(0);
            }
            public void Write(NativeArray<float> samples)
            {
                for (int i = 0; i < samples.Length; i++) writer.Write((short)Mathf.RoundToInt(Mathf.Clamp(samples[i], -1, 1) * 32767));
                SampleFrames += samples.Length / Channels;
            }
            public void Dispose()
            {
                int bytes = checked((int)(SampleFrames * Channels * 2));
                writer.BaseStream.Position = 4; writer.Write(36 + bytes); writer.BaseStream.Position = 40; writer.Write(bytes); writer.Dispose();
            }
        }
    }
}
#endif
