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
    /// HERO_DETAIL stills, in Play mode on the real Tennis scene (the same set before and after the pass, so the images compare one to one):
    ///   HDS_OUT=work/character-shader/proof/detail/after Unity -batchmode -projectPath Unity -executeMethod GolfArcade.EditorTools.HeroDetailStill.Run
    /// locker_*  : the locker view close-ups (ball hidden, as in the phone's locker): male torso, female skort, a shoe each, the face each.
    /// match_*   : the match as played, from behind at the baseline (the game camera, then closer on the player's back).
    /// report.txt: every material on both heroes (shader, keywords, the numbers the gate reads) and the rim-light state.
    [InitializeOnLoad]
    public static class HeroDetailStill
    {
        const string Flag = "HeroDetailStill";
        static IEnumerator script;
        static string Out => Path.GetFullPath(Environment.GetEnvironmentVariable("HDS_OUT") ?? "../work/character-shader/proof/detail/after");
        static readonly StringBuilder report = new StringBuilder();
        static UnityEngine.Rendering.RenderPipelineAsset restorePipeline;

        static HeroDetailStill() { EditorApplication.update += Tick; }

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
            try { File.WriteAllText(Out + "/report.txt", report.ToString()); } catch (Exception e) { Debug.LogException(e); code = 1; }
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
            File.WriteAllBytes(Out + "/" + name + ".png", tex.EncodeToPNG()); Object.DestroyImmediate(tex);
        }

        static void Shot(TennisGame game, string name, Vector3 from, Vector3 look, float fov, int w = 1080, int h = 1080, bool rimPair = false)
        {
            var cam = Cam(game); var p = cam.transform.position; var r = cam.transform.rotation; var f = cam.fieldOfView;
            cam.transform.SetPositionAndRotation(from, Quaternion.LookRotation(look - from)); cam.fieldOfView = fov;
            var rimLight = GameObject.Find("Hero rim") ? GameObject.Find("Hero rim").GetComponent<HeroRimLight>() : null;
            if (rimLight) rimLight.Aim();
            report.AppendLine($"shot {name}: camera at {cam.transform.position} fwd {cam.transform.forward} fov {fov}");
            Save(cam, name, w, h);
            var rim = GameObject.Find("Hero rim");
            if (rimPair && rim) { rim.SetActive(false); Save(cam, name + "_norim", w, h); rim.SetActive(true); }
            cam.transform.SetPositionAndRotation(p, r); cam.fieldOfView = f;
        }

        static void Audit(string tag, HeroTennisDriver d)
        {
            report.AppendLine("== " + tag);
            foreach (var r in d.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled && !(r is TrailRenderer)))
                foreach (var m in r.sharedMaterials)
                {
                    if (!m) continue;
                    string Tex(string prop) => m.HasProperty(prop) && m.GetTexture(prop) ? m.GetTexture(prop).name : "-";
                    report.AppendLine($"  {r.name,-14} {m.name,-30} {m.shader.name,-30} base=#{ColorUtility.ToHtmlStringRGB(m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white)}" +
                        (m.HasProperty("_Smoothness") ? $" smooth={m.GetFloat("_Smoothness"):0.000}" : "") + (m.HasProperty("_Wrap") ? $" wrap={m.GetFloat("_Wrap"):0.00}" : "") +
                        (m.HasProperty("_RimStrength") ? $" rim={m.GetFloat("_RimStrength"):0.00}" : "") + (m.HasProperty("_BumpScale") ? $" bump={m.GetFloat("_BumpScale"):0.00}" : "") +
                        (m.HasProperty("_Subsurface") ? $" sss=({m.GetColor("_Subsurface").r:0.00},{m.GetColor("_Subsurface").g:0.00},{m.GetColor("_Subsurface").b:0.00})" : "") +
                        $" kw=[{string.Join(" ", m.shaderKeywords)}] baseMap={Tex("_BaseMap")} bumpMap={Tex("_BumpMap")} weave={Tex("_WeaveMap")}" +
                        (m.HasProperty("_WeaveTile") ? $" tile={m.GetFloat("_WeaveTile"):0.0} weaveN={m.GetFloat("_WeaveNormal"):0.00} thread={m.GetFloat("_WeaveThread"):0.00} sheen={m.GetFloat("_SheenStrength"):0.00}" : ""));
                }
        }

        static IEnumerator Script(TennisGame game)
        {
            Time.captureFramerate = 60;
            restorePipeline = QualitySettings.renderPipeline;
            QualitySettings.renderPipeline = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline;   // the game's own pipeline asset, whatever level the editor is on
            var presentation = game.GetComponent<TennisPresentation>();
            if (presentation) presentation.Finish();
            yield return Frames(30);
            var rim = GameObject.Find("Hero rim");
            report.AppendLine("hero rim: " + (rim ? $"active={rim.activeSelf} intensity={rim.GetComponent<Light>().intensity} colour=#{ColorUtility.ToHtmlStringRGB(rim.GetComponent<Light>().color)}" : "none"));
            var sun = RenderSettings.sun; report.AppendLine("sun intensity " + (sun ? sun.intensity.ToString() : "-"));
            report.AppendLine("pipeline " + UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.name);

            // ---- the match as played: male player (default white kit), from behind at the baseline
            game.SelectCharacter(false); yield return Frames(40);
            var d = game.Player.GetComponentInChildren<HeroTennisDriver>();
            d.matchLook.SetSkin(HeroKit.Hex("C47A4C"));
            yield return Frames(10);
            Audit("match male", d);
            var t = d.transform; Vector3 pp = game.Player.transform.position;
            Shot(game, "match_behind", new Vector3(0, 5.8f, pp.z - 5.8f), new Vector3(0, .95f, pp.z + 7.2f), 54, 1280, 720, true);
            Shot(game, "match_behind_close", t.TransformPoint(-1.2f, 1.45f, -2.9f), t.position + Vector3.up * 1.05f, 30, 1080, 1080, true);

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
                var head = d.matchLook.Bone(HumanBodyBones.Head); Vector3 hp = head ? head.position : t.position + Vector3.up * 1.62f;
                if (!female)
                {
                    Shot(game, "locker_male_torso", t.TransformPoint(.75f, 1.38f, 1.9f), t.position + Vector3.up * 1.12f, 30, 1080, 1080, true);
                    Shot(game, "locker_male_torsoL", t.TransformPoint(-.75f, 1.38f, 1.9f), t.position + Vector3.up * 1.12f, 30);
                    Shot(game, "locker_male_shorts", t.TransformPoint(.55f, 1.05f, 1.45f), t.position + Vector3.up * .78f, 30);
                    Shot(game, "locker_male_shoe", t.TransformPoint(.62f, .34f, .95f), t.TransformPoint(.20f, .07f, .06f), 28);
                    Shot(game, "locker_male_face", hp + t.TransformDirection(new Vector3(.28f, .02f, .95f)), hp + t.TransformDirection(new Vector3(0, .05f, 0)), 20);
                }
                else
                {
                    Shot(game, "locker_female_skort", t.TransformPoint(.55f, 1.02f, 1.35f), t.position + Vector3.up * .80f, 30, 1080, 1080, true);
                    Shot(game, "locker_female_torso", t.TransformPoint(.75f, 1.32f, 1.9f), t.position + Vector3.up * 1.08f, 30);
                    Shot(game, "locker_female_shoe", t.TransformPoint(.62f, .34f, .95f), t.TransformPoint(.12f, .07f, .05f), 28);
                    Shot(game, "locker_female_face", hp + t.TransformDirection(new Vector3(.28f, .02f, .95f)), hp + t.TransformDirection(new Vector3(0, .05f, 0)), 20);
                }
            }
            foreach (var r in ballRenderers) if (r) r.enabled = true;
        }
    }
}
