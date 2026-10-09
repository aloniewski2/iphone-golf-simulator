using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using GolfArcade.Game;
using GolfArcade.Tennis;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GolfArcade.EditorTools
{
    /// Stills for the character / cloth shader pass, in Play mode on the real Tennis scene:
    ///   CSS_OUT=work/character-shader/proof Unity -projectPath Unity -executeMethod GolfArcade.EditorTools.CharacterShaderStill.Run
    /// match_*  : the match as played (game camera, then the same camera moved in on the player's shoulder).
    /// locker_* : the locker view (MatchHeroProof's front 3/4), both sexes. The ball is hidden there, as in the phone's locker.
    /// Every shot is rendered twice in the same frame, with and without the hero rim light (name_norim.png); CSS_TAG suffixes the file names.
    [InitializeOnLoad]
    public static class CharacterShaderStill
    {
        const string Flag = "CharacterShaderStill";
        static IEnumerator script;
        static string Out => Path.GetFullPath(Environment.GetEnvironmentVariable("CSS_OUT") ?? "../work/character-shader/proof");
        static string Tag => Environment.GetEnvironmentVariable("CSS_TAG") ?? "";
        static readonly StringBuilder report = new StringBuilder();
        static UnityEngine.Rendering.RenderPipelineAsset restorePipeline;

        static CharacterShaderStill() { EditorApplication.update += Tick; }

        public static void Run()
        {
            Directory.CreateDirectory(Out);
            EditorSceneManager.OpenScene("Assets/Scenes/Tennis.unity");
            SessionState.SetBool(Flag, true);
            EditorApplication.isPlaying = true;
        }

        static void Tick()
        {
            if (!SessionState.GetBool(Flag, false) || !EditorApplication.isPlaying) return;
            var game = Object.FindFirstObjectByType<TennisGame>();
            if (!game || !game.Initialized) return;
            if (script == null) script = Script(game);
            try { if (!script.MoveNext()) Finish(0); }
            catch (Exception e) { Debug.LogException(e); Finish(1); }
        }

        static void Finish(int code)
        {
            SessionState.SetBool(Flag, false);
            try { File.WriteAllText(Out + "/report" + Tag + ".txt", report.ToString()); } catch (Exception e) { Debug.LogException(e); code = 1; }
            Time.captureFramerate = 0;
            if (restorePipeline) QualitySettings.renderPipeline = restorePipeline;
            if (Application.isBatchMode) EditorApplication.Exit(code); else EditorApplication.isPlaying = false;
        }

        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }
        static Camera Cam(TennisGame game) => game.GameplayCamera ? game.GameplayCamera : Camera.main;

        static void Save(Camera cam, string name, int w, int h)
        {
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, 4);
            var prev = cam.targetTexture; cam.targetTexture = rt; cam.Render(); cam.targetTexture = prev;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false); var active = RenderTexture.active;
            RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply(); RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(rt);
            File.WriteAllBytes(Out + "/" + name + Tag + ".png", tex.EncodeToPNG()); Object.DestroyImmediate(tex);
        }

        static void Shot(TennisGame game, string name, Vector3 from, Vector3 look, float fov, int w = 1080, int h = 1080)
        {
            var cam = Cam(game); var p = cam.transform.position; var r = cam.transform.rotation; var f = cam.fieldOfView;
            cam.transform.SetPositionAndRotation(from, Quaternion.LookRotation(look - from)); cam.fieldOfView = fov;
            var rimLight = GameObject.Find("Hero rim") ? GameObject.Find("Hero rim").GetComponent<HeroRimLight>() : null;
            if (rimLight) rimLight.Aim();   // the light follows Camera.main in LateUpdate; the camera was just moved within this frame
            report.AppendLine($"shot {name}: camera at {cam.transform.position} fwd {cam.transform.forward}; rim shines along {(rimLight ? rimLight.transform.forward : Vector3.zero)}; Camera.main is cam: {Camera.main == cam}");
            Save(cam, name, w, h);
            // the same frame without the rim light, rendered immediately (nothing moves between the two), for an exact difference image
            var rim = GameObject.Find("Hero rim");
            if (rim) { rim.SetActive(false); Save(cam, name + "_norim", w, h); rim.SetActive(true); }
            cam.transform.SetPositionAndRotation(p, r); cam.fieldOfView = f;
        }

        static void Audit(string tag, HeroTennisDriver d)
        {
            report.AppendLine("== " + tag);
            foreach (var r in d.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled && !(r is TrailRenderer)))
                foreach (var m in r.sharedMaterials)
                    if (m) report.AppendLine($"  {r.name,-14} {m.name,-34} {m.shader.name,-32} layers={Convert.ToString((long)r.renderingLayerMask, 2)} base={ColorUtility.ToHtmlStringRGB(m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white)}" +
                        (m.HasProperty("_Smoothness") ? $" smooth={m.GetFloat("_Smoothness"):0.00}" : "") + (m.HasProperty("_Wrap") ? $" wrap={m.GetFloat("_Wrap"):0.00}" : "") + (m.HasProperty("_RimStrength") ? $" rim={m.GetFloat("_RimStrength"):0.00}" : ""));
        }

        static IEnumerator Script(TennisGame game)
        {
            Time.captureFramerate = 60;
            // The game's own pipeline asset (GraphicsSettings default = TennisURP), whatever quality level the editor happens to be on.
            restorePipeline = QualitySettings.renderPipeline;
            QualitySettings.renderPipeline = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline;
            var presentation = game.GetComponent<TennisPresentation>();
            if (presentation) presentation.Finish();
            yield return Frames(30);
            var rim = GameObject.Find("Hero rim");
            report.AppendLine("hero rim: " + (rim ? $"active={rim.activeSelf} intensity={rim.GetComponent<Light>().intensity} colour=#{ColorUtility.ToHtmlStringRGB(rim.GetComponent<Light>().color)} layer={rim.GetComponent<Light>().renderingLayerMask}" : "none"));
            var sun = RenderSettings.sun; report.AppendLine("sun intensity " + (sun ? sun.intensity.ToString() : "-"));
            report.AppendLine("pipeline " + UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.name + " useRenderingLayers=" +
                ((UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset)UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline).useRenderingLayers);

            // which rendering layers the scene's renderers and lights are on (the rim light must reach only the heroes)
            report.AppendLine("lights:");
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) report.AppendLine($"  {l.name,-20} {l.type} enabled={l.enabled} intensity={l.intensity:0.00} layers={l.renderingLayerMask}");
            report.AppendLine("renderers by (shader, layer mask):");
            foreach (var g in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(r => r.sharedMaterial && !(r is ParticleSystemRenderer) && !(r is TrailRenderer))
                .GroupBy(r => r.sharedMaterial.shader.name + " | " + r.renderingLayerMask)) report.AppendLine($"  {g.Count(),5}  {g.Key}   e.g. {g.First().name}");
            // ---- the match as played: male player (default white kit), game camera, then in on the shoulder
            game.SelectCharacter(false); yield return Frames(40);
            var d = game.Player.GetComponentInChildren<HeroTennisDriver>();
            d.matchLook.SetSkin(HeroKit.Hex("C47A4C"));
            yield return Frames(10);
            Audit("match male", d);
            { var c = Cam(game); var rg = GameObject.Find("Hero rim");
              report.AppendLine($"camera pos {c.transform.position} fwd {c.transform.forward} | hero pos {d.transform.position} fwd {d.transform.forward} | rim shines along {(rg ? rg.transform.forward : Vector3.zero)} | Camera.main={(Camera.main ? Camera.main.name : "none")} gameplay={c.name}"); }
            var t = d.transform; Vector3 pp = game.Player.transform.position;
            // the game's own cameras (TennisGame.UpdateCamera): behind the player, and the serving shoulder view (right-hander: toss side -x)
            Shot(game, "match_game", new Vector3(0, 5.8f, pp.z - 5.8f), new Vector3(0, .95f, pp.z + 7.2f), 54, 1280, 720);
            Shot(game, "match_serve", pp + new Vector3(-2.1f, 2.5f, -4.4f), pp + new Vector3(.3f, 1.1f, 2.4f), 52);
            Shot(game, "match_shoulder", t.TransformPoint(-.85f, 1.55f, -2.4f), t.position + Vector3.up * 1.18f, 32);
            Shot(game, "match_back34", t.TransformPoint(-1.6f, 1.25f, -3.2f), t.position + Vector3.up * .95f, 36);

            // ---- the locker (ball hidden, as in the phone's locker)
            var ballRoot = GameObject.Find("Tennis ball");
            var ballRenderers = ballRoot ? ballRoot.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled).ToArray() : new Renderer[0];
            foreach (var r in ballRenderers) r.enabled = false;
            foreach (bool female in new[] { false, true })
            {
                string sex = female ? "female" : "male";
                game.SelectCharacter(female); yield return Frames(40);
                d = game.Player.GetComponentInChildren<HeroTennisDriver>();
                d.matchLook.SetSkin(HeroKit.Hex(female ? "EEBB8F" : "C47A4C"));
                yield return Frames(10);
                Audit("locker " + sex, d);
                t = d.transform;
                Shot(game, $"locker_{sex}_front34", t.TransformPoint(1.15f, 1.10f, 3.4f), t.position + Vector3.up * .86f, 32);
                Shot(game, $"locker_{sex}_front34L", t.TransformPoint(-1.15f, 1.10f, 3.4f), t.position + Vector3.up * .86f, 32);
                Shot(game, $"locker_{sex}_torsoL", t.TransformPoint(-.75f, 1.38f, 1.9f), t.position + Vector3.up * 1.12f, 30);
                Shot(game, $"locker_{sex}_torso", t.TransformPoint(.75f, 1.38f, 1.9f), t.position + Vector3.up * 1.12f, 30);
            }
            foreach (var r in ballRenderers) if (r) r.enabled = true;
        }
    }
}
