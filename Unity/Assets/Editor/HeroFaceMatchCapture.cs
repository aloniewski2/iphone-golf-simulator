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
    /// Face-only proof in the unchanged court lighting and post-processing.
    [InitializeOnLoad]
    public static class HeroFaceMatchCapture
    {
        const string Flag = "HeroFaceMatchCapture";
        static IEnumerator script;
        static readonly Stack<IEnumerator> steps = new();
        static readonly List<string> audit = new();
        static int lastFrame = -1;
        static string output;
        static HeroFaceMatchCapture() { EditorApplication.update += Tick; }
        public static void Run()
        {
            foreach (string key in new[] { "VISUAL_CHARACTER_SKIN_POLISH", "VISUAL_BLINK_NORMAL_FIELD" })
                if (Environment.GetEnvironmentVariable(key) != null) throw new InvalidOperationException(key + " must be UNSET");
            output = Path.GetFullPath(Environment.GetEnvironmentVariable("FACE_MATCH_OUT") ?? "../work/face-match/before/raw");
            Directory.CreateDirectory(output); SessionState.SetString(Flag + "out", output);
            SessionState.SetBool(Flag, true); EditorSceneManager.OpenScene("Assets/Scenes/Tennis.unity"); EditorApplication.isPlaying = true;
        }
        static void Tick()
        {
            if (!SessionState.GetBool(Flag, false) || !EditorApplication.isPlaying || Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;
            var game = Object.FindFirstObjectByType<TennisGame>(); if (!game || !game.Initialized) return;
            try {
                if (script == null) { output = SessionState.GetString(Flag + "out", ""); script = Capture(game); steps.Push(script); }
                while (steps.Count > 0) {
                    var step = steps.Peek(); if (!step.MoveNext()) { steps.Pop(); continue; }
                    if (step.Current is IEnumerator nested) { steps.Push(nested); continue; } return;
                }
                Finish(0);
            } catch (Exception e) { audit.Add(e.ToString()); Debug.LogException(e); Finish(1); }
        }
        static void Finish(int code)
        {
            File.WriteAllLines(Path.Combine(output, "audit.txt"), audit);
            SessionState.SetBool(Flag, false); Time.captureFramerate = 0; EditorApplication.Exit(code);
        }
        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }
        static void Save(Camera cam, string name, int width, int height)
        {
            var previous = cam.targetTexture; var active = RenderTexture.active;
            var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, 4);
            cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0); tex.Apply();
            File.WriteAllBytes(Path.Combine(output, name + ".png"), tex.EncodeToPNG());
            Object.DestroyImmediate(tex); cam.targetTexture = previous; RenderTexture.active = active; RenderTexture.ReleaseTemporary(rt);
        }
        static void Frame(MatchHeroLook hero, out Vector3 at, out Vector3 up, out Vector3 forward, out Vector3 right)
        {
            var head = hero.Bone(HumanBodyBones.Head); int index = Array.IndexOf(hero.body.bones, head);
            var mat = hero.body.sharedMaterial; var map = head.localToWorldMatrix * hero.body.sharedMesh.bindposes[index];
            Vector3 r = mat.GetVector("_HeadRight"), u = mat.GetVector("_HeadUp"), f = mat.GetVector("_HeadForward"), e = mat.GetVector("_HeadEye");
            at = map.MultiplyPoint3x4((Vector3)mat.GetVector("_HeadOrigin") + r * e.x + u * e.y + f * e.z);
            up = map.MultiplyVector(u).normalized; forward = map.MultiplyVector(f).normalized; right = map.MultiplyVector(r).normalized;
            at -= up * .027f;
            // Reference fitting lowers the orbital centre 10 mm. Keep the
            // portrait camera at its baseline head-relative height.
            if (hero.body.sharedMesh.name.Contains("reference face fit")) at += up * .010f;
        }
        static void Aim(Camera cam, MatchHeroLook hero, bool quarter)
        {
            Frame(hero, out var at, out var up, out var f, out var r);
            var from = at + (quarter ? f * .8660254f + r * .5f : f) * 1.06f;
            cam.transform.SetPositionAndRotation(from, Quaternion.LookRotation(at - from, up)); cam.fieldOfView = 24;
        }
        static void MeshAudit(MatchHeroLook hero, string sex)
        {
            var head = hero.Bone(HumanBodyBones.Head); int headIndex = Array.IndexOf(hero.body.bones, head);
            var mat = hero.body.sharedMaterial;
            var headRest = hero.body.sharedMesh.bindposes[headIndex].inverse;
            Vector3 origin = mat.GetVector("_HeadOrigin"), right = mat.GetVector("_HeadRight"), up = mat.GetVector("_HeadUp"), forward = mat.GetVector("_HeadForward"), eye = mat.GetVector("_HeadEye");
            foreach (var skin in new[] { hero.body, hero.face as SkinnedMeshRenderer }) {
                if (!skin || !skin.sharedMesh) continue;
                var mesh = skin.sharedMesh;
                audit.Add(sex + " " + skin.name + " vertices=" + mesh.vertexCount + " shapes=" + string.Join(",", Enumerable.Range(0, mesh.blendShapeCount).Select(i => mesh.GetBlendShapeName(i))));
                var map = headRest * mesh.bindposes[Array.IndexOf(skin.bones, head)];
                var points = mesh.vertices.Select(p => { var d = map.MultiplyPoint3x4(p) - origin; return new Vector3(Vector3.Dot(d,right),Vector3.Dot(d,up),Vector3.Dot(d,forward))-eye; }).ToArray();
                var rows = new List<string> { "index,x,y,z,u,v,nx,ny,nz" }; var uv = mesh.uv; var normals=mesh.normals;
                for(int i=0;i<points.Length;i++){var p=points[i];var n=map.inverse.transpose.MultiplyVector(normals[i]);rows.Add($"{i},{p.x:R},{p.y:R},{p.z:R},{(uv.Length>i?uv[i].x:0):R},{(uv.Length>i?uv[i].y:0):R},{Vector3.Dot(n,right):R},{Vector3.Dot(n,up):R},{Vector3.Dot(n,forward):R}");}
                File.WriteAllLines(Path.Combine(output, sex + (skin==hero.body?"_head":"_features")+".csv"),rows);
                if(skin==hero.body){
                    int blink=mesh.GetBlendShapeIndex("Hero_Blink");
                    if(blink>=0){var dp=new Vector3[mesh.vertexCount];mesh.GetBlendShapeFrameVertices(blink,0,dp,new Vector3[mesh.vertexCount],new Vector3[mesh.vertexCount]);
                        var closed=new List<string>{"index,x,y,z"};var vertices=mesh.vertices;
                        for(int i=0;i<vertices.Length;i++){var d=map.MultiplyPoint3x4(vertices[i]+dp[i])-origin;var p=new Vector3(Vector3.Dot(d,right),Vector3.Dot(d,up),Vector3.Dot(d,forward))-eye;closed.Add($"{i},{p.x:R},{p.y:R},{p.z:R}");}
                        File.WriteAllLines(Path.Combine(output,sex+"_closed.csv"),closed);
                    }
                }
                for(int sub=0;sub<mesh.subMeshCount;sub++)File.WriteAllText(Path.Combine(output,sex+(skin==hero.body?"_head":"_features")+"_"+sub+".txt"),string.Join(",",mesh.GetTriangles(sub)));
                File.WriteAllLines(Path.Combine(output,sex+(skin==hero.body?"_head":"_features")+"_materials.txt"),skin.sharedMaterials.Select(m=>m?m.name:"null"));
            }
            audit.Add(sex + " skin=" + ColorUtility.ToHtmlStringRGB(hero.body.sharedMaterial.GetColor("_BaseColor")) + "; skinFinish=" + hero.body.sharedMaterial.GetFloat("_SkinFinish"));
        }
        static IEnumerator Capture(TennisGame game)
        {
            audit.Add("VISUAL_CHARACTER_SKIN_POLISH=UNSET; VISUAL_BLINK_NORMAL_FIELD=UNSET; production lights/post-processing unchanged.");
            Time.captureFramerate = 30;
            var presentation = game.GetComponent<TennisPresentation>(); if (presentation) presentation.Finish();
            yield return Frames(40);
            var cam = game.GameplayCamera; var gamePos = cam.transform.position; var gameRot = cam.transform.rotation; float gameFov = cam.fieldOfView;
            game.ManualSimulation = true; game.enabled = false;
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) canvas.enabled = false;
            foreach (bool female in new[] { false, true }) {
                string sex = female ? "female" : "male";
                game.SelectCharacter(female); if (presentation) presentation.Finish(); yield return Frames(4);
                var actor = game.Player; actor.enabled = false;
                var driver = actor.GetComponentInChildren<HeroTennisDriver>(); driver.enabled = false;
                var hero = driver.matchLook; var performance = hero.GetComponent<MatchHeroFacePerformance>();
                if (performance) { performance.ForcedBlink = 0; performance.enabled=false; }
                hero.SetFacePerformance(0,Vector2.zero); HeroFaceReferenceFit.ApplyExpression(hero,0,Vector2.zero,0);
                foreach (var skin in hero.GetComponentsInChildren<SkinnedMeshRenderer>()) skin.updateWhenOffscreen = true;
                driver.Sample(HeroTennisDriver.Clip.Idle, .5f); yield return Frames(3);
                MeshAudit(hero, sex);
                cam.transform.SetPositionAndRotation(gamePos, gameRot); cam.fieldOfView = gameFov;
                yield return Frames(3); Save(cam, sex + "_gameplay", 1920, 1080);
                // The idle racket crosses the face. Hide only its renderers in
                // portrait proofs; the unchanged gameplay capture above retains it.
                var racketRenderers = hero.GetComponentsInChildren<Renderer>().Where(r => r.name.IndexOf("racket", StringComparison.OrdinalIgnoreCase) >= 0 || r.transform.parent && r.transform.parent.name.IndexOf("racket", StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
                foreach (var r in racketRenderers) r.enabled = false;
                if (hero.racketFrame) hero.racketFrame.enabled = false;
                if (hero.racketGrip) foreach (var r in hero.racketGrip.GetComponentsInChildren<Renderer>()) r.enabled = false;
                audit.Add(sex + " portrait-only racket renderer visibility disabled to expose face; equipment assets unchanged.");
                foreach (bool quarter in new[] { false, true }) {
                    Aim(cam, hero, quarter); yield return Frames(3); Save(cam, sex + (quarter ? "_threequarter" : "_front"), 900, 1056);
                }
                // Separate deterministic exercise of the existing face interface.
                if (Environment.GetEnvironmentVariable("FACE_MATCH_FILM") == "1") {
                    Directory.CreateDirectory(Path.Combine(output, sex + "_idle"));
                    if (performance) performance.enabled = false;
                    var trace = new List<string> { "frame,time,blink,gazeX,gazeY,brow,bodyFullBlink,bodyHalfBlink,faceGazeRight,faceGazeLeft,faceBrowUp,faceBrowDown" };
                    for (int frame = 0; frame < 90; frame++) {
                        float time = frame / 30f;
                        driver.Sample(HeroTennisDriver.Clip.Idle, time);
                        float blink = Mathf.Clamp01(1 - Mathf.Abs(time - .9f) / .12f);
                        var gaze = new Vector2(Mathf.Sin(time * 3) * .0015f, Mathf.Cos(time * 2) * .0007f);
                        float brow = time > 1.4f ? Mathf.Sin((time - 1.4f) * 4) * .00065f : 0;
                        hero.SetFacePerformance(blink, gaze, brow); HeroFaceReferenceFit.ApplyExpression(hero,blink,gaze,brow); Aim(cam, hero, false); yield return null;
                        Save(cam, sex + "_idle/" + frame.ToString("0000"), 600, 704);
                        float W(SkinnedMeshRenderer skin,string name){int id=skin.sharedMesh.GetBlendShapeIndex(name);return id<0?0:skin.GetBlendShapeWeight(id);}
                        var feature=hero.face as SkinnedMeshRenderer;
                        trace.Add($"{frame},{time:F4},{blink:F4},{gaze.x:F6},{gaze.y:F6},{brow:F6},{W(hero.body,"Hero_Blink"):F3},{W(hero.body,"Hero_Blink_Half"):F3},{W(feature,"Face_GazeRight"):F3},{W(feature,"Face_GazeLeft"):F3},{W(feature,"Face_BrowUp"):F3},{W(feature,"Face_BrowDown"):F3}");
                    }
                    File.WriteAllLines(Path.Combine(output, sex + "_idle/trace.csv"), trace);
                    hero.SetFacePerformance(0, Vector2.zero);
                }
                audit.Add(sex + " captured: front/30-degree three-quarter/unchanged gameplay camera; no skin tint override.");
            }
            audit.Add("skinPolishGlobal=" + Shader.GetGlobalFloat("_HeroSkinPolish"));
        }
    }
}
