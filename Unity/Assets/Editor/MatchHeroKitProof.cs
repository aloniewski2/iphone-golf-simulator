using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GolfArcade.Game;
using GolfArcade.Tennis;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GolfArcade.EditorTools
{
    /// DRESS_MATCH_HEROES proof, in Play mode on the real Tennis scene (the game's own camera, the phone-launch path), graphics batch mode:
    ///   MH_DRESS_OUT=work/hero-dressed/proof/unity Unity -batchmode -projectPath Unity -executeMethod GolfArcade.EditorTools.MatchHeroKitProof.Run
    /// 1. LOCKER, both sexes: the kit at its default (no picks) and with coral shirt / navy shorts / lime shoes, front 3/4, back 3/4, and full-body close-ups;
    ///    the pixel test of gate ROLES_TINT (everything that differs between the two renders lies on the kit renderers' screen mask).
    /// 2. CAMPAIGN: ConfigureMatch(Campaign, "Nadia" | "Viktor"): the rival wears its roster kit colours (TennisRoster.Shirt / Shorts / Shoes), the player the locker's.
    /// 3. RALLY: self-play from the first serve; the player's serve, forehand, backhand and the rival's strokes are captured at the gameplay contact (kit moving with the clip).
    /// Writes <out>/*.png and <out>/dressed_proof.json (what each frame is, and the pixel-test numbers).
    [InitializeOnLoad]
    public static class MatchHeroKitProof
    {
        const string Flag = "MatchHeroKitProof";
        static IEnumerator script;
        static string Out => Path.GetFullPath(Environment.GetEnvironmentVariable("MH_DRESS_OUT") ?? "../work/hero-dressed/proof/unity");
        [Serializable] class Row { public string file, what; public int w, h, kitPixels, changed, outsideMask; }
        [Serializable] class Rows { public Row[] shots; public string[] log; }
        static readonly List<Row> rows = new List<Row>(); static readonly List<string> log = new List<string>();

        static MatchHeroKitProof() { EditorApplication.update += Tick; }

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
            catch (Exception e) { Debug.LogException(e); log.Add("EXCEPTION " + e); Finish(1); }
        }

        static void Finish(int code)
        {
            SessionState.SetBool(Flag, false);
            try { File.WriteAllText(Out + "/dressed_proof.json", JsonUtility.ToJson(new Rows { shots = rows.ToArray(), log = log.ToArray() }, true)); }
            catch (Exception e) { Debug.LogException(e); code = 1; }
            Time.captureFramerate = 0;
            if (Application.isBatchMode) EditorApplication.Exit(code); else EditorApplication.isPlaying = false;
        }

        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }
        static Camera Cam(TennisGame game) => game.GameplayCamera ? game.GameplayCamera : Camera.main;

        static Texture2D Grab(Camera cam, Vector3 from, Vector3 look, float fov, int w, int h)
        {
            var p = cam.transform.position; var r = cam.transform.rotation; var f = cam.fieldOfView;
            cam.transform.SetPositionAndRotation(from, Quaternion.LookRotation(look - from)); cam.fieldOfView = fov;
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, 1);
            var prev = cam.targetTexture; cam.targetTexture = rt; cam.Render(); cam.targetTexture = prev;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false); var active = RenderTexture.active;
            RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply(); RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(rt);
            cam.transform.SetPositionAndRotation(p, r); cam.fieldOfView = f;
            return tex;
        }

        static void Write(Texture2D t, string name, string what, Row row = null)
        {
            File.WriteAllBytes($"{Out}/{name}.png", t.EncodeToPNG());
            var r = row ?? new Row(); r.file = name + ".png"; r.what = what; r.w = t.width; r.h = t.height; rows.Add(r);
        }

        /// The kit renderers drawn flat magenta (unlit) for one render: where the kit is on screen. Restores their materials afterwards.
        static Texture2D KitMask(Camera cam, Vector3 from, Vector3 look, float fov, int w, int h, SkinnedMeshRenderer[] kit)
        {
            var flat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = Color.magenta }; flat.SetColor("_BaseColor", Color.magenta);
            var saved = kit.Select(k => k.sharedMaterials).ToArray();
            for (int i = 0; i < kit.Length; i++) kit[i].sharedMaterials = kit[i].sharedMaterials.Select(_ => flat).ToArray();
            var t = Grab(cam, from, look, fov, w, h);
            for (int i = 0; i < kit.Length; i++) kit[i].sharedMaterials = saved[i];
            Object.DestroyImmediate(flat);
            return t;
        }

        static (int kitPx, int changed, int outside) PixelTest(Texture2D a, Texture2D b, Texture2D withMask)
        {
            var pa = a.GetPixels32(); var pb = b.GetPixels32(); var pm = withMask.GetPixels32(); int w = a.width, h = a.height;
            var mask = new bool[pa.Length]; int kitPx = 0;
            for (int i = 0; i < pa.Length; i++) { mask[i] = Mathf.Abs(pa[i].r - pm[i].r) + Mathf.Abs(pa[i].g - pm[i].g) + Mathf.Abs(pa[i].b - pm[i].b) > 30; if (mask[i]) kitPx++; }
            int changed = 0, outside = 0;
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                int i = y * w + x; int d = Mathf.Abs(pa[i].r - pb[i].r) + Mathf.Abs(pa[i].g - pb[i].g) + Mathf.Abs(pa[i].b - pb[i].b);
                if (d <= 24) continue;
                changed++; bool near = false;
                for (int dy = -2; dy <= 2 && !near; dy++) for (int dx = -2; dx <= 2; dx++) { int xx = x + dx, yy = y + dy; if (xx >= 0 && yy >= 0 && xx < w && yy < h && mask[yy * w + xx]) { near = true; break; } }
                if (!near) outside++;
            }
            return (kitPx, changed, outside);
        }

        static IEnumerator Script(TennisGame game)
        {
            Time.captureFramerate = 60;
            var presentation = game.GetComponent<TennisPresentation>();
            if (presentation) presentation.Finish();
            yield return Frames(30);
            var ballRoot = GameObject.Find("Tennis ball");
            var ballRenderers = ballRoot ? ballRoot.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled).ToArray() : new Renderer[0];
            foreach (var r in ballRenderers) r.enabled = false;

            // ---------------------------------------------------------------- 1. LOCKER
            foreach (bool female in new[] { false, true })
            {
                string sex = female ? "female" : "male";
                foreach (bool picks in new[] { false, true })
                {
                    var kit = picks ? TennisLook.Kit.From("FF7F50", "1E2A6E", "7CFC00", female ? "E8508F" : "2E6BD6", female ? 1 : 3) : TennisLook.Kit.From("", "", "", female ? "E8508F" : "2E6BD6", female ? 1 : 3);
                    var look = HeroKit.Style.From(female ? 1 : 3, 2, 1, kit, female ? 1 : 0, female); look.SkinTint = HeroKit.Hex(female ? "EEBB8F" : "C47A4C");
                    game.SelectCharacter(female); game.SetPlayerLook(look);
                    yield return Frames(40);
                    var d = game.Player.GetComponentInChildren<HeroTennisDriver>(); var t = d.transform; var cam = Cam(game);
                    var kitR = d.matchLook.kit;
                    string tag = picks ? "coral_navy_lime" : "default";
                    log.Add($"{sex} {tag}: kit renderers {kitR.Length}, roles " + string.Join(",", MatchHeroLook.KitRoles.Select(r => { d.matchLook.TryGetKitColour(r, out var c); return r + "=" + ColorUtility.ToHtmlStringRGB(c); })));
                    var views = new[] { ("front34", t.TransformPoint(1.15f, 1.10f, 3.4f), t.position + Vector3.up * .86f, 32f), ("back34", t.TransformPoint(-1.15f, 1.20f, -3.4f), t.position + Vector3.up * .86f, 32f),
                                        ("side", t.TransformPoint(3.4f, 1.0f, 0.2f), t.position + Vector3.up * .86f, 32f), ("feet", t.TransformPoint(.9f, .55f, 1.8f), t.position + Vector3.up * .22f, 28f) };
                    foreach (var (name, from, look2, fov) in views)
                    {
                        var img = Grab(cam, from, look2, fov, 1080, 1080); Write(img, $"locker_{sex}_{tag}_{name}", $"locker {sex} {tag} {name}"); Object.DestroyImmediate(img);
                    }
                }
                // pixel test: default vs picks, the same hero, the same frame, the same camera
                {
                    var d = game.Player.GetComponentInChildren<HeroTennisDriver>(); var t = d.transform; var cam = Cam(game);
                    Vector3 from = t.TransformPoint(1.15f, 1.10f, 3.4f), look2 = t.position + Vector3.up * .86f;
                    var kitR = d.matchLook.kit;
                    d.matchLook.SetKit(null, null, null); var a = Grab(cam, from, look2, 32, 1080, 1080);
                    d.matchLook.SetKit("FF7F50", "1E2A6E", "7CFC00"); var b = Grab(cam, from, look2, 32, 1080, 1080);
                    var m = KitMask(cam, from, look2, 32, 1080, 1080, kitR);
                    var (kitPx, changed, outside) = PixelTest(a, b, m);
                    log.Add($"PIXEL TEST {sex}: kit pixels {kitPx}, pixels changed by the picks {changed}, outside the kit mask (2 px dilation) {outside}");
                    Write(a, $"pixeltest_{sex}_default", "pixel test A (default kit)", new Row { kitPixels = kitPx, changed = changed, outsideMask = outside });
                    Write(b, $"pixeltest_{sex}_picks", "pixel test B (coral / navy / lime)"); Write(m, $"pixeltest_{sex}_kitmask", "kit renderers drawn flat magenta");
                    Object.DestroyImmediate(a); Object.DestroyImmediate(b); Object.DestroyImmediate(m);
                }
            }
            foreach (var r in ballRenderers) if (r) r.enabled = true;

            // ---------------------------------------------------------------- 2. CAMPAIGN (the rival wears its roster kit)
            foreach (var (key, playerFemale) in new[] { ("Nadia", false), ("Viktor", true) })
            {
                var kit = TennisLook.Kit.From("FFFFFF", "1E2A5A", "F28C28", playerFemale ? "E8508F" : "2E6BD6", playerFemale ? 1 : 3);
                var look = HeroKit.Style.From(playerFemale ? 1 : 3, 2, 1, kit, playerFemale ? 1 : 0, playerFemale); look.SkinTint = HeroKit.Hex(playerFemale ? "EEBB8F" : "C47A4C");
                game.SelectCharacter(playerFemale); game.SetPlayerLook(look);
                game.ConfigureMatch(TennisGame.Mode.Campaign, key, key, "ROUND");
                if (presentation) presentation.Finish();
                yield return Frames(45);
                var pd = game.Player.GetComponentInChildren<HeroTennisDriver>(); var rd = game.Opponent.GetComponentInChildren<HeroTennisDriver>();
                var roster = TennisRoster.All.First(r => r.Key == key);
                log.Add($"campaign vs {key}: roster kit shirt={roster.Shirt} shorts={roster.Shorts} shoes={roster.Shoes}; rival roles " + string.Join(",", MatchHeroLook.KitRoles.Select(r => { rd.matchLook.TryGetKitColour(r, out var c); return r + "=" + ColorUtility.ToHtmlStringRGB(c); })));
                GameCapture.Save($"{Out}/campaign_vs_{key}_wide.png", 1280, 720); rows.Add(new Row { file = $"campaign_vs_{key}_wide.png", what = $"campaign vs {key}, game camera", w = 1280, h = 720 });
                var cam = Cam(game);
                var img = Grab(cam, rd.transform.TransformPoint(1.15f, 1.10f, 3.4f), rd.transform.position + Vector3.up * .86f, 32, 1080, 1080); Write(img, $"campaign_vs_{key}_rival", $"rival {key} close-up"); Object.DestroyImmediate(img);
                img = Grab(cam, pd.transform.TransformPoint(-1.25f, 1.45f, -3.4f), pd.transform.position + Vector3.up * .9f, 34, 1080, 1080); Write(img, $"campaign_vs_{key}_player", "player close-up"); Object.DestroyImmediate(img);
            }

            // ---------------------------------------------------------------- 3. RALLY: the kit moves with the clips (contact frames)
            {
                game.SelectCharacter(false);
                var kit = TennisLook.Kit.From("FF7F50", "1E2A6E", "7CFC00", "2E6BD6", 3);
                var look = HeroKit.Style.From(3, 2, 1, kit, 0, false); look.SkinTint = HeroKit.Hex("C47A4C"); game.SetPlayerLook(look);
                game.ConfigureMatch(TennisGame.Mode.Campaign, "Nadia", "Nadia", "ROUND");
                if (presentation) presentation.Finish();
                yield return Frames(45);
                var pd = game.Player.GetComponentInChildren<HeroTennisDriver>(); var rd = game.Opponent.GetComponentInChildren<HeroTennisDriver>(); var cam = Cam(game);
                game.AutoPlay = true; game.AutoPlayLean = true;
                var seen = new Dictionary<string, int>(); var armed = new Dictionary<HeroTennisDriver, bool> { { pd, true }, { rd, true } };
                int startHits = game.Hits;
                for (int frame = 0; frame < 60 * 120; frame++)
                {
                    yield return null;
                    foreach (var (drv, role) in new[] { (pd, "player"), (rd, "rival") })
                    {
                        if (!drv) continue; var a = drv.actor;
                        if (!a.Swinging) { armed[drv] = true; continue; }
                        if (!armed[drv] || a.SignedTimeToContact > .0085f) continue;
                        armed[drv] = false; string clip = drv.PlayingClip; string k2 = role + ":" + clip;
                        seen.TryGetValue(k2, out int n); if (n >= 1) continue; seen[k2] = n + 1;
                        var tf = drv.transform; bool player = role == "player";
                        Vector3 from = player ? tf.TransformPoint(-1.5f, 1.55f, -3.2f) : tf.TransformPoint(1.5f, 1.45f, 3.0f);
                        var img = Grab(cam, from, tf.position + Vector3.up * 1.0f, 36, 1080, 1080); Write(img, $"rally_{role}_{clip}", $"{role} {(drv.matchLook.female ? "female" : "male")} {clip} at the gameplay contact (clip time {drv.PlayingClipTime:0.000} s)"); Object.DestroyImmediate(img);
                    }
                    if (game.Hits - startHits >= 30 && seen.Keys.Count(k => k.StartsWith("player:")) >= 3) break;
                }
                game.AutoPlay = false;
                log.Add("rally captured: " + string.Join(", ", seen.Keys));
            }
        }
    }
}
