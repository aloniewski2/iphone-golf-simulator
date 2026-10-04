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
    /// HERO_MAINSTAY proof, in Play mode on the real Tennis scene (the game's own camera, the phone-launch path):
    ///   MH_PROOF_OUT=work/hero-mainstay/proof Unity -batchmode -projectPath Unity -executeMethod GolfArcade.EditorTools.MatchHeroProof.Run
    /// 1. LOCKER: the locker's choices applied exactly as NativeSportsSession applies them (SelectCharacter + SetPlayerLook), male and female.
    /// 2. CAMPAIGN: ConfigureMatch(Campaign, "Nadia" | "Viktor"), the player the other sex, both match heroes on court.
    /// 3. RALLY: self-play from the opening serve; every stroke (serve, forehand, backhand, ...) is captured at its gameplay contact.
    /// Writes raw frames to <out>/raw, <out>/shots.json (what each frame is: hero, sex, clip, clip time, contact time), <out>/audit.txt.
    /// tools/label_sheets.py burns the clip name into the corner and builds the sheets.
    [InitializeOnLoad]
    public static class MatchHeroProof
    {
        const string Flag = "MatchHeroProof";
        static IEnumerator script;
        static string Out => Path.GetFullPath(Environment.GetEnvironmentVariable("MH_PROOF_OUT") ?? "../work/hero-mainstay/proof");

        [Serializable] class Shot { public string file, scenario, role, sex, clip, state, note; public float clipTime, contact; public int frame; public float gap; }
        [Serializable] class ShotFile { public Shot[] shots; public string[] audit; public string[] contacts; }
        static readonly List<Shot> shots = new List<Shot>();
        static readonly List<string> audit = new List<string>(), contactLog = new List<string>(), trace = new List<string>();
        static string Fmt(Vector3 v) => $"{v.x:0.000};{v.y:0.000};{v.z:0.000}";
        /// One CSV row per frame per hero: what the puppet is playing, the clip time, the layer weights, and the key world points.
        static void Trace(TennisGame game, string scenario, int frame, HeroTennisDriver d, string role)
        {
            if (!d || !d.actor) return;
            var a = d.actor; var an = d.GetComponent<Animator>();
            var L = d.matchLook;
            Vector3 hips = L.Bone(HumanBodyBones.Hips).position, rh = L.Bone(HumanBodyBones.RightHand).position, lh = L.Bone(HumanBodyBones.LeftHand).position;
            Vector3 head = L.Bone(HumanBodyBones.Head).position, lf = L.Bone(HumanBodyBones.LeftFoot).position, rf = L.Bone(HumanBodyBones.RightFoot).position;
            trace.Add($"{scenario},{role},{frame},{game.Flow},{d.State.Replace(',', ';')},{d.PlayingClip},{d.PlayingClipTime:0.0000},{d.ActionWeight:0.000},{d.UpperLayerWeight:0.000},{d.WeightSum:0.000},{a.Swinging},{a.SignedTimeToContact:0.000},{a.PrepareAmount:0.000},{a.Speed:0.00},{a.ForwardSpeed:0.00}," +
                $"{Fmt(a.transform.position)},{Fmt(hips)},{Fmt(head)},{Fmt(rh)},{Fmt(lh)},{Fmt(lf)},{Fmt(rf)},{Fmt(d.StringCentre)},{Fmt(game.BallPosition)},{Fmt(a.TossReachTarget)},{a.TossReachWanted:0.00},{d.FootSkate:0.000},{d.PlantDip:0.000},{d.LastGroundLift:0.000},{d.WeightReport().Replace(',', ';')}");
        }
        const string TraceHeader = "scenario,role,frame,flow,state,clip,clipTime,actionWeight,upperWeight,weightSum,swinging,ttc,prepare,speed,fwdSpeed,actor,hips,head,rightHand,leftHand,leftFoot,rightFoot,strings,ball,tossTarget,tossWanted,footSkate,plantDip,groundLift,weights";

        static MatchHeroProof() { EditorApplication.update += Tick; }

        public static void Run()
        {
            Directory.CreateDirectory(Out + "/raw");
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
            try
            {
                File.WriteAllText(Out + "/shots.json", JsonUtility.ToJson(new ShotFile { shots = shots.ToArray(), audit = audit.ToArray(), contacts = contactLog.ToArray() }, true));
                File.WriteAllText(Out + "/audit.txt", string.Join("\n", audit) + "\n\n-- gameplay contacts --\n" + string.Join("\n", contactLog) + "\n");
                File.WriteAllText(Out + "/trace.csv", TraceHeader + "\n" + string.Join("\n", trace) + "\n");
            }
            catch (Exception e) { Debug.LogException(e); code = 1; }
            Time.captureFramerate = 0;
            if (Application.isBatchMode) EditorApplication.Exit(code); else EditorApplication.isPlaying = false;
        }

        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        // ================================================================ the script
        static IEnumerator Script(TennisGame game)
        {
            Time.captureFramerate = 60;
            TennisGame.AutoPlayTimingJitter = .05f;
            var presentation = game.GetComponent<TennisPresentation>();
            // the broadcast intro (drone, then the rival's and the player's close shots) with the default characters: a few frames of it
            if (presentation && presentation.Playing)
            {
                var pd0 = game.Player.GetComponentInChildren<HeroTennisDriver>(); var rd0 = game.Opponent.GetComponentInChildren<HeroTennisDriver>();
                for (int f = 0; f < 720 && presentation.Playing; f++)
                {
                    yield return null;
                    if (f == 100 || f == 400 || f == 520 || f == 640) SaveGame($"intro_{f:000}", game, "intro", "broadcast intro, game camera", pd0, rd0);
                }
            }
            if (presentation) presentation.Finish();   // the rest of the proof is the match itself
            yield return Frames(30);
            audit.Add("scene: " + EditorSceneManager.GetActiveScene().path + "  quality tier=" + TennisQuality.Current + "  skinWeights=" + QualitySettings.skinWeights);
            AuditScene("start", game);

            // ---------------------------------------------------------------- 1. LOCKER
            // (the serve-hold ball hovers at the hero's hip in this stand-in locker; the phone's locker has no ball, so it is hidden for these shots)
            var ballRoot = GameObject.Find("Tennis ball");
            var ballRenderers = ballRoot ? ballRoot.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled).ToArray() : new Renderer[0];
            foreach (var r in ballRenderers) r.enabled = false;
            foreach (bool female in new[] { false, true })
            {
                string sex = female ? "female" : "male";
                var kit = TennisLook.Kit.From("FFFFFF", "1E2A5A", "F28C28", female ? "E8508F" : "2E6BD6", female ? 1 : 3);
                var look = HeroKit.Style.From(female ? 1 : 3, 2, 1, kit, female ? 1 : 0, female);   // skin, hair colour, headwear, kit, haircut, sex (the old hair / headwear choices are carried but not worn)
                look.SkinTint = HeroKit.Hex(female ? "EEBB8F" : "C47A4C");
                game.SelectCharacter(female);                // what NativeSportsSession.Load does for the locker's sex
                game.SetPlayerLook(look);                    // ...and for the locker's look
                yield return Frames(40);
                var d = game.Player.GetComponentInChildren<HeroTennisDriver>();
                AuditScene("locker_" + sex, game);
                var t = d.transform;
                Locker(game, d, $"locker_{sex}_front34", t.TransformPoint(1.15f, 1.10f, 3.4f), t.position + Vector3.up * .86f, 32, "locker");
                Locker(game, d, $"locker_{sex}_back34", t.TransformPoint(-1.15f, 1.20f, -3.4f), t.position + Vector3.up * .86f, 32, "locker");
                Locker(game, d, $"locker_{sex}_head", t.TransformPoint(.45f, 1.62f, 1.15f), t.position + Vector3.up * 1.56f, 24, "locker");
            }

            foreach (var r in ballRenderers) if (r) r.enabled = true;

            // ---------------------------------------------------------------- ROSTER: every campaign rival gets the body of its roster sex, its roster skin tone and racket colour
            audit.Add("== roster (campaign opponents, ConfigureMatch(Campaign, key))");
            int rosterOk = 0;
            foreach (var r in TennisRoster.All)
            {
                game.SelectCharacter(r.Female);   // the player is the same sex as the rival here; the rival's own body is what is checked
                game.ConfigureMatch(TennisGame.Mode.Campaign, r.Key, r.Key, "ROUND");
                if (presentation) presentation.Finish();
                yield return Frames(4);
                var rd2 = game.Opponent.GetComponentInChildren<HeroTennisDriver>(); var pd2 = game.Player.GetComponentInChildren<HeroTennisDriver>();
                var frame = rd2.matchLook.racketFrame.sharedMaterial.GetColor("_BaseColor");
                string racketHex = ColorUtility.ToHtmlStringRGB(frame), skinHex = ColorUtility.ToHtmlStringRGB(rd2.matchLook.skinTone);
                bool ok = rd2.matchLook.female == r.Female && game.Opponent.Female == r.Female && racketHex.Equals(r.Racket, StringComparison.OrdinalIgnoreCase);
                if (ok) rosterOk++;
                audit.Add($"  {r.Key,-7} rung={r.Rung:0.00} style={r.Style,-10} boss={r.Boss,-5} roster.Female={r.Female,-5} rival body={(rd2.matchLook.female ? "Female" : "Male"),-6} mesh={rd2.matchLook.body.sharedMesh.name} skin=#{skinHex} racketFrame=#{racketHex} (roster #{r.Racket}) " +
                          $"player body={(pd2.matchLook.female ? "Female" : "Male")} hair/hat/kit renderers={rd2.GetComponentsInChildren<Renderer>(true).Count(x => OldMarkers.Any(m => x.name.Contains(m) || (x.sharedMaterial && x.sharedMaterial.name.Contains(m))))} {(ok ? "OK" : "MISMATCH")}");
            }
            audit.Add($"  roster rivals with the right body + racket colour: {rosterOk}/{TennisRoster.All.Length}");

            // ---------------------------------------------------------------- 2./3. CAMPAIGN + RALLY
            TennisGame.ContactMade += (face, grade, super) => LogContact(game, true);
            TennisGame.OpponentStruck += () => LogContact(game, false);
            // pass 1 captures both campaign matches before any point has been played (no banner or effect of an earlier rally on screen); pass 2 plays the rallies
            foreach (bool rallyPass in new[] { false, true })
            foreach (var (key, playerFemale) in new[] { ("Nadia", false), ("Viktor", true) })
            {
                string scenario = "vs_" + key;
                var kit = TennisLook.Kit.From("FFFFFF", "1E2A5A", "F28C28", playerFemale ? "E8508F" : "2E6BD6", playerFemale ? 1 : 3);
                var look = HeroKit.Style.From(playerFemale ? 1 : 3, 2, 1, kit, playerFemale ? 1 : 0, playerFemale);
                look.SkinTint = HeroKit.Hex(playerFemale ? "EEBB8F" : "C47A4C");
                game.SelectCharacter(playerFemale); game.SetPlayerLook(look);
                game.ConfigureMatch(TennisGame.Mode.Campaign, key, key, "ROUND");     // exactly the phone launch path: the rival replaces the opponent
                if (presentation) presentation.Finish();                                  // the intro is a separate show; this proof is the match itself
                yield return Frames(45);
                var pd = game.Player.GetComponentInChildren<HeroTennisDriver>(); var rd = game.Opponent.GetComponentInChildren<HeroTennisDriver>();
                if (!rallyPass)
                {
                    AuditScene(scenario, game);
                    audit.Add($"  [{scenario}] state at the first capture: timeScale={Time.timeScale} flow={game.Flow} autoPlay={game.AutoPlay} replay={game.ReplayPlaying} score={game.Match.Scoreboard}");
                    SaveGame($"{scenario}_wide", game, scenario, "wide game camera", pd, rd);
                    CloseUp(game, rd, $"{scenario}_rival_front34", rd.transform.TransformPoint(1.15f, 1.10f, 3.4f), rd.transform.position + Vector3.up * .86f, 32, scenario, "RIVAL");
                    CloseUp(game, pd, $"{scenario}_player_back34", pd.transform.TransformPoint(-1.25f, 1.45f, -3.4f), pd.transform.position + Vector3.up * .9f, 34, scenario, "PLAYER");
                    continue;
                }

                // locomotion with no run clip: the player slides sideways along the baseline holding ReadyIdle (the feet re-plant); every 5th frame
                if (key == "Nadia")
                {
                    game.NativeControlled = true;   // the phone's lateral input is not overwritten by the keyboard read
                    for (int f = 0; f < 60; f++)
                    {
                        game.SetLateralInput(f < 40 ? 1 : 0, f < 40); yield return null;
                        if (f % 5 == 0) CloseUp(game, pd, $"{scenario}_slide_{f / 5:00}", pd.transform.TransformPoint(2.4f, 1.1f, -.6f), pd.transform.position + Vector3.up * .85f, 34, scenario, "PLAYER");
                    }
                    game.SetLateralInput(0, false); game.NativeControlled = false;
                }
                // the rally: self-play from the first serve
                game.AutoPlay = true; game.AutoPlayLean = true; int diveShots = 0, replayShots = 0; bool diveTried = false, replayTried = false;
                var runShots = new Dictionary<string, int> { { "PLAYER", 0 }, { "RIVAL", 0 } };
                var seen = new Dictionary<string, int>(); var armed = new Dictionary<HeroTennisDriver, bool> { { pd, true }, { rd, true } };
                int startHits = game.Hits; float wallStart = Time.time; int f0 = Time.frameCount;
                var need = new[] { "PLAYER:Male_Serve", "PLAYER:Male_Forehand", "PLAYER:Male_Backhand", "RIVAL:Female_Forehand", "RIVAL:Female_Backhand" };
                string lastRival = null; int ritual = 0, tossShots = 0;
                for (int frame = 0; frame < 60 * 150; frame++)
                {
                    yield return null;
                    Trace(game, scenario, frame, pd, "PLAYER"); Trace(game, scenario, frame, rd, "RIVAL");
                    PopFilm(game, scenario, frame, pd, "PLAYER"); PopFilm(game, scenario, frame, rd, "RIVAL");
                    // the heroes on the move with no run clip: ReadyIdle held (side-on, every 5th frame of a fast run)
                    foreach (var (drv, role) in new[] { (pd, "PLAYER"), (rd, "RIVAL") })
                    {
                        if (!drv || !drv.actor || runShots[role] >= 10 || drv.State != "Run" || drv.actor.Speed < 2.2f || frame % 5 != 0) continue;
                        runShots[role]++; var ap = drv.actor.transform.position; var t = Grab(Cam(game), ap + new Vector3(3.2f, 1.25f, 0), ap + Vector3.up * .95f, 36, 420);
                        File.WriteAllBytes($"{Out}/raw/{scenario}_run_{role.ToLower()}_{runShots[role]:00}.png", t.EncodeToPNG()); Object.DestroyImmediate(t);
                        shots.Add(new Shot { file = $"raw/{scenario}_run_{role.ToLower()}_{runShots[role]:00}.png", scenario = scenario, frame = Time.frameCount, role = role, sex = drv.matchLook.female ? "female" : "male", clip = drv.PlayingClip, clipTime = drv.PlayingClipTime, state = drv.State, note = $"on the move at {drv.actor.Speed:0.0} m/s, ReadyIdle held (no run clip) | {drv.WeightReport()}" });
                    }
                    // the explicit dive (the one stroke the match set has no clip for: the hero tilts, the swing clip plays)
                    if (key == "Nadia" && !diveTried && frame > 1200 && game.CanDive) { diveTried = game.RequestDive(); }
                    // (the camera is placed in the actor's frame: the hero root itself is rolled 58 degrees in a dive, which would put a hero-relative camera under the court)
                    if (game.DiveActive && diveShots < 8 && frame % 4 == 0) { diveShots++; var at = pd.actor.transform; CloseUp(game, pd, $"{scenario}_dive_{diveShots:00}", at.TransformPoint(3.2f, 1.3f, -1.4f), at.position + Vector3.up * .6f, 40, scenario, "PLAYER"); }
                    // the instant replay (TennisReplay re-poses every recorded transform of both actors, the match heroes' bones included): play one on demand
                    if (key == "Viktor" && !replayTried && frame == 900) { replayTried = true; var rep = game.GetComponent<TennisReplay>(); audit.Add($"[{scenario}] replay requested at frame {frame}: Play -> {(rep && rep.Play(2.6f, .5f))}"); }
                    if (game.ReplayPlaying && replayShots < 3 && frame % 30 == 0) { replayShots++; SaveGame($"{scenario}_replay_{replayShots:00}", game, scenario, "slow-motion replay camera", pd, rd); }
                    // the serve ritual (ball held, bounced, wound up and tossed): the tossing hand and the ball, every quarter second of the first serve
                    bool hold = game.Flow == TennisGame.Phase.PlayerServeHold, toss = game.Flow == TennisGame.Phase.PlayerServeToss || pd.CurrentAction == HeroTennisDriver.Clip.Serve && pd.actor.Swinging;
                    if (ritual < 14 && hold && frame % 30 == 0 || tossShots < 26 && toss && frame % 3 == 0)
                    {
                        var tf = pd.transform; if (toss) tossShots++; else ritual++;
                        CloseUp(game, pd, toss ? $"{scenario}_serve_toss_{tossShots:00}" : $"{scenario}_serve_hold_{ritual:00}", tf.TransformPoint(-1.7f, 1.55f, 2.2f), tf.position + Vector3.up * 1.35f, 38, scenario, "PLAYER");
                    }
                    if (game.LastRivalStrike != lastRival) { lastRival = game.LastRivalStrike; contactLog.Add($"RIVAL-STRIKE[{scenario}] clip={rd.PlayingClip} t={rd.PlayingClipTime:0.000}/{rd.ContactOf(rd.CurrentAction):0.000} {lastRival}"); }
                    foreach (var (drv, role) in new[] { (pd, "PLAYER"), (rd, "RIVAL") })
                    {
                        if (!drv) continue; var a = drv.actor;
                        if (!a.Swinging) { armed[drv] = true; continue; }
                        float ttc = a.SignedTimeToContact;
                        if (!armed[drv] || ttc > .0085f) continue;
                        armed[drv] = false;   // one frame per swing: the first at or past the gameplay contact
                        string clip = drv.PlayingClip; string key2 = role + ":" + clip;
                        seen.TryGetValue(key2, out int n); if (n >= 2) continue; seen[key2] = n + 1;
                        var tf = drv.transform; bool player = role == "PLAYER";
                        string name = $"{scenario}_rally_{(game.Hits - startHits):00}_{role.ToLower()}_{clip}";
                        SaveGame(name + "_wide", game, scenario, "rally contact, game camera", pd, rd, drv, ttc);
                        Vector3 from = player ? tf.TransformPoint(-1.5f, 1.55f, -3.2f) : tf.TransformPoint(1.5f, 1.45f, 3.0f);
                        CloseUp(game, drv, name + "_close", from, tf.position + Vector3.up * 1.0f, 36, scenario, role, ttc);
                    }
                    if (game.Hits - startHits >= 40 && need.All(k => seen.Keys.Any(s => s.EndsWith(k.Substring(k.IndexOf(':') + 1)) && s.StartsWith(k.Substring(0, k.IndexOf(':')))) )) break;
                }
                game.AutoPlay = false;
                audit.Add($"[{scenario}] rally: frames={Time.frameCount - f0} hits={game.Hits - startHits} honestMisses={game.HonestMisses} rivalWhiffs={game.RivalWhiffs} playerGaps[{game.PlayerGaps}] opponentGaps[{game.OpponentGaps}] captured={string.Join(",", seen.Select(kv => kv.Key + "x" + kv.Value))}");
                AuditScene(scenario + "_after_rally", game);
            }
        }

        // ================================================================ pop film
        /// A side-on close-up of each hero every frame; when the pose jumps (hips / head / either hand more than 30 cm in one frame, relative to the actor) the frame
        /// before, the frame of the jump and the two after are saved as pop_NN_*.png, so a snap can be looked at instead of inferred from numbers.
        class Film { public Texture2D prev; public Vector3[] last; public int after, id; }
        static readonly Dictionary<HeroTennisDriver, Film> films = new Dictionary<HeroTennisDriver, Film>();
        static int popCount;
        const int PopLimit = 14;
        static Texture2D Grab(Camera cam, Vector3 from, Vector3 look, float fov, int size)
        {
            var p = cam.transform.position; var r = cam.transform.rotation; var f = cam.fieldOfView;
            cam.transform.SetPositionAndRotation(from, Quaternion.LookRotation(look - from)); cam.fieldOfView = fov;
            var rt = RenderTexture.GetTemporary(size, size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, 1);
            var prev = cam.targetTexture; cam.targetTexture = rt; cam.Render(); cam.targetTexture = prev;
            var tex = new Texture2D(size, size, TextureFormat.RGB24, false); var active = RenderTexture.active;
            RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, size, size), 0, 0); tex.Apply(); RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(rt);
            cam.transform.SetPositionAndRotation(p, r); cam.fieldOfView = f;
            return tex;
        }
        static void PopFilm(TennisGame game, string scenario, int frame, HeroTennisDriver d, string role)
        {
            if (!d || !d.actor) return;
            if (game.ReplayPlaying) { if (films.TryGetValue(d, out var fr)) fr.last = null; return; }   // the replay re-poses every transform on purpose
            if (popCount >= PopLimit && (!films.TryGetValue(d, out var f0) || f0.after == 0)) return;
            if (!films.TryGetValue(d, out var f)) films[d] = f = new Film();
            var a = d.actor.transform.position; var L = d.matchLook;
            var now = new[] { L.Bone(HumanBodyBones.Hips).position - a, L.Bone(HumanBodyBones.Head).position - a, L.Bone(HumanBodyBones.RightHand).position - a, L.Bone(HumanBodyBones.LeftHand).position - a };
            float jump = 0; if (f.last != null) for (int i = 0; i < now.Length; i++) jump = Mathf.Max(jump, Vector3.Distance(now[i], f.last[i]));
            f.last = now;
            var cur = Grab(Cam(game), a + new Vector3(3.2f, 1.25f, 0), a + Vector3.up * .95f, 36, 420);
            void Write(Texture2D t, string tag) { File.WriteAllBytes($"{Out}/raw/pop_{f.id:00}_{tag}.png", t.EncodeToPNG()); }
            if (f.after > 0) { Write(cur, "c_after" + (3 - f.after)); f.after--; }
            else if (jump > .3f && popCount < PopLimit && f.prev)
            {
                f.id = ++popCount; Write(f.prev, "a_before"); Write(cur, "b_event"); f.after = 2;
                audit.Add($"POP {f.id:00} [{scenario}] frame {frame} {role} {(d.matchLook.female ? "Female" : "Male")}: {jump:0.00} m in one frame, state '{d.State}', clip {d.PlayingClip} t={d.PlayingClipTime:0.000}, weights {d.WeightReport()}");
            }
            if (f.prev) Object.DestroyImmediate(f.prev);
            f.prev = cur;
        }

        // ================================================================ capture
        static void LogContact(TennisGame game, bool player)
        {
            var actor = player ? game.Player : game.Opponent; var d = actor ? actor.GetComponentInChildren<HeroTennisDriver>() : null; if (!d) return;
            contactLog.Add($"{(player ? "PLAYER" : "RIVAL ")} {(d.matchLook.female ? "Female" : "Male  ")} clip={d.PlayingClip} clipTime={d.PlayingClipTime:0.000}s clipContact={d.ContactOf(d.CurrentAction):0.000}s stringsToBall={Vector3.Distance(d.StringCentre, game.BallPosition):0.000}m ttc={actor.SignedTimeToContact:0.000} kind={actor.Kind} bh={actor.Backhand}");
        }

        static Camera Cam(TennisGame game) => game.GameplayCamera ? game.GameplayCamera : Camera.main;

        static void Fill(Shot s, TennisGame game, HeroTennisDriver drv, float ttc)
        {
            if (!drv) return;
            s.sex = drv.matchLook.female ? "female" : "male"; s.role = drv.isPlayer ? "PLAYER" : "RIVAL";
            s.clip = drv.PlayingClip; s.clipTime = drv.PlayingClipTime; s.contact = drv.ContactOf(drv.CurrentAction); s.state = drv.State;
        }

        /// The game camera exactly as the game places it (HUD included): what the phone / TV shows.
        static void SaveGame(string name, TennisGame game, string scenario, string note, HeroTennisDriver pd, HeroTennisDriver rd, HeroTennisDriver subject = null, float ttc = 0)
        {
            string file = $"raw/{name}.png";
            GameCapture.Save(Out + "/" + file, 1280, 720);
            var s = new Shot { file = file, scenario = scenario, note = note, frame = Time.frameCount, gap = Mathf.Abs(ttc) };
            Fill(s, game, subject, ttc);
            if (!subject) { s.note += $" | player={(pd.matchLook.female ? "female" : "male")} rival={(rd.matchLook.female ? "female" : "male")}"; s.sex = "both"; s.role = "BOTH"; }
            shots.Add(s);
        }

        /// The same game camera, moved to look at one hero (no HUD): the locker view and the contact close-ups.
        static void CloseUp(TennisGame game, HeroTennisDriver d, string name, Vector3 from, Vector3 look, float fov, string scenario, string role, float ttc = 0)
        {
            var cam = Cam(game);
            var p = cam.transform.position; var r = cam.transform.rotation; var f = cam.fieldOfView;
            cam.transform.SetPositionAndRotation(from, Quaternion.LookRotation(look - from)); cam.fieldOfView = fov;
            string file = $"raw/{name}.png"; Save(cam, Out + "/" + file, 1080, 1080);
            cam.transform.SetPositionAndRotation(p, r); cam.fieldOfView = f;
            var s = new Shot { file = file, scenario = scenario, frame = Time.frameCount, note = "game camera moved to the hero", gap = Mathf.Abs(ttc) }; Fill(s, game, d, ttc);
            if (role == "RIVAL" || role == "PLAYER") s.role = role;
            shots.Add(s);
        }
        static void Locker(TennisGame game, HeroTennisDriver d, string name, Vector3 from, Vector3 look, float fov, string scenario) => CloseUp(game, d, name, from, look, fov, scenario, "PLAYER");

        static void Save(Camera cam, string path, int w, int h)
        {
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, 4);
            var prev = cam.targetTexture; cam.targetTexture = rt; cam.Render(); cam.targetTexture = prev;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false); var active = RenderTexture.active;
            RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply(); RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(rt);
            File.WriteAllBytes(path, tex.EncodeToPNG()); Object.DestroyImmediate(tex);
        }

        // ================================================================ audit
        static readonly string[] OldMarkers = { "Hero_01", "Hero01", "HairTuft", "Hero_V6", "Hero neck", "Hero side hair", "Hero lid", "Hero_Racket", "Slot_Hat", "HatLiner", "Hair_Default", "Body_Skin", "Visor", "Sweatband", "CoveredFoundation", "Hero hair" };

        /// What is on screen / loaded: the heroes (sex, avatar, clips), and every old Hero01 marker found in the live scene, in loaded meshes, materials and clips.
        static void AuditScene(string tag, TennisGame game)
        {
            audit.Add($"== {tag}");
            foreach (var d in Object.FindObjectsByType<HeroTennisDriver>(FindObjectsInactive.Include, FindObjectsSortMode.None).OrderBy(x => !x.isPlayer))
            {
                var an = d.GetComponent<Animator>();
                audit.Add($"  hero '{d.name}' role={(d.isPlayer ? "PLAYER" : "RIVAL")} sex={(d.matchLook ? (d.matchLook.female ? "Female" : "Male") : "LEGACY")} actor='{d.actor.name}' actor.Female={d.actor.Female} avatar='{(an && an.avatar ? an.avatar.name : "none")}' humanoid={(an && an.isHuman)} (Generic rig: clips play as their own transform curves) " +
                    $"skin={(d.matchLook ? ColorUtility.ToHtmlStringRGB(d.matchLook.skinTone) : "-")} body='{(d.matchLook && d.matchLook.body ? d.matchLook.body.sharedMesh.name : "-")}' tris={(d.matchLook && d.matchLook.body ? d.matchLook.body.sharedMesh.triangles.Length / 3 : 0)}");
                foreach (var line in d.SlotReport().Split('\n').Where(l => l.Length > 0))
                {
                    string clipName = line.Substring(line.IndexOf('=') + 1).Split(' ')[0];
                    var clip = d.slots.First(sl => sl.clip && sl.clip.name == clipName).clip;
                    audit.Add("      " + line + "  asset=" + AssetDatabase.GetAssetPath(clip));
                }
                var renderers = d.GetComponentsInChildren<Renderer>(true).Where(r => !(r is TrailRenderer)).Select(r => r.name + (r is SkinnedMeshRenderer s2 && s2.sharedMesh ? "(" + s2.sharedMesh.name + ")" : "")).ToArray();
                audit.Add("      renderers: " + string.Join(", ", renderers));
                audit.Add("      hair/hat/kit renderers: " + d.GetComponentsInChildren<Renderer>(true).Count(r => OldMarkers.Any(m => r.name.Contains(m) || (r.sharedMaterial && r.sharedMaterial.name.Contains(m)))));
            }
            // everything else on the two actors (the gameplay rig and any worn kit the old standard had): nothing of it may be drawn
            foreach (var actor in new[] { game.Player, game.Opponent })
            {
                if (!actor) continue;
                var hero = actor.GetComponentInChildren<HeroTennisDriver>(true);
                var others = actor.GetComponentsInChildren<Renderer>(true).Where(r => !(r is TrailRenderer) && !(r is LineRenderer) && !(r is ParticleSystemRenderer) && (!hero || !r.transform.IsChildOf(hero.transform))).ToArray();
                var drawn = others.Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
                audit.Add($"  actor '{actor.name}': renderers outside the hero {others.Length}, drawn {drawn.Length}{(drawn.Length > 0 ? " -> " + string.Join(", ", drawn.Select(r => r.name + "/" + (r.sharedMaterial ? r.sharedMaterial.name : "-"))) : "")}; shoes/shirt/shorts/polo drawn: {drawn.Count(r => System.Text.RegularExpressions.Regex.IsMatch(r.name + (r.sharedMaterial ? r.sharedMaterial.name : ""), "(?i)shoe|sneaker|shirt|polo|short|skirt|sock|sweatband|visor|cap\\b|hat\\b"))}");
            }
            var oldLive = new List<string>();
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (r is TrailRenderer || r is LineRenderer || r is ParticleSystemRenderer) continue;
                string n = r.name + "|" + (r.sharedMaterial ? r.sharedMaterial.name : "") + "|" + (r is SkinnedMeshRenderer smr && smr.sharedMesh ? smr.sharedMesh.name : r.GetComponent<MeshFilter>() && r.GetComponent<MeshFilter>().sharedMesh ? r.GetComponent<MeshFilter>().sharedMesh.name : "");
                if (OldMarkers.Any(m => n.Contains(m))) oldLive.Add(r.transform.root.name + "/" + n + (r.enabled && r.gameObject.activeInHierarchy ? " [VISIBLE]" : " [off]"));
            }
            audit.Add("  old Hero01 markers among live renderers: " + (oldLive.Count == 0 ? "none" : string.Join("; ", oldLive)));
            var heroNames = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(t => t.name.StartsWith("Hero01") || t.name.Contains("Hero_01")).Select(t => t.name).ToArray();
            audit.Add("  GameObjects named Hero01/Hero_01*: " + (heroNames.Length == 0 ? "none" : string.Join(", ", heroNames)));
            var oldClips = Resources.FindObjectsOfTypeAll<AnimationClip>().Where(c => c.name.StartsWith("Hero_")).Select(c => c.name).Distinct().ToArray();
            audit.Add("  old Hero_* AnimationClips loaded in memory: " + (oldClips.Length == 0 ? "none" : string.Join(", ", oldClips)));
            var oldMeshes = Resources.FindObjectsOfTypeAll<Mesh>().Where(m => m.name.Contains("Hero_01") || m.name.Contains("Hero01")).Select(m => m.name).Distinct().ToArray();
            audit.Add("  old Hero01 meshes loaded in memory: " + (oldMeshes.Length == 0 ? "none" : string.Join(", ", oldMeshes)));
            var opp = Resources.FindObjectsOfTypeAll<Mesh>().Where(m => AssetDatabase.GetAssetPath(m).Contains("Tennis/Opponents/")).Select(m => m.name).Distinct().ToArray();
            audit.Add("  Resources/Tennis/Opponents meshes loaded in memory: " + (opp.Length == 0 ? "none" : string.Join(", ", opp)));
            audit.Add($"  drivers in scene: {Object.FindObjectsByType<HeroTennisDriver>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length}; match={game.PlayMode}; opponentKey={game.OpponentKey}; femalePlayer={game.FemalePlayer}");
        }
    }
}
