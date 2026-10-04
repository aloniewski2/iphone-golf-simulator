using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
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
    /// SERVE_AND_FEET proof, in Play mode on the real Tennis scene (the game's own camera, the phone-launch path), both sexes:
    ///   SF_PROOF_OUT=<abs path>/work/serve-and-feet/proof SF_REPO=<repo root> Unity -batchmode -projectPath Unity -executeMethod GolfArcade.EditorTools.ServeAndFeetProof.Run
    /// For each of two campaign matches (male player vs Nadia, female player vs Viktor):
    ///   WALK  the server walks along the baseline before the toss (the game's own serve walk-in): close-ups side-on every 6th frame;
    ///   SERVE the first serve: stance, bounce ritual, toss, coil, jump, contact, landing, two views every 3rd frame;
    ///   RALLY self-play until 14 serves of each role are logged: run close-ups side-on, every contact logged, one trace row per hero per frame.
    /// Foot heights: the lowest skinned vertex of each foot (vertex lists from the Blender skin export, checked against the imported mesh), above the READY standing floor.
    [InitializeOnLoad]
    public static class ServeAndFeetProof
    {
        const string Flag = "ServeAndFeetProof";
        static IEnumerator script;
        static string Out => Path.GetFullPath(Environment.GetEnvironmentVariable("SF_PROOF_OUT") ?? "../work/serve-and-feet/proof");
        static string Repo => ServeAndFeetWire.Repo;
        static bool Quick => Environment.GetEnvironmentVariable("SF_QUICK") == "1";
        static bool DiagWalk => Environment.GetEnvironmentVariable("SF_DIAG") == "walk";
        static readonly List<string> diag = new List<string>();
        static T Priv<T>(object o, string name) { var f = o.GetType().GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance); return (T)f.GetValue(o); }
        /// walk-in diagnostics: where the actor really is, where the toes and ankles really are, and what the foot lock and the clip clock are doing
        static void Diag(string scenario, int frame, HeroTennisDriver d)
        {
            var feet = Priv<Transform[]>(d, "feet"); var locked = Priv<bool[]>(d, "locked"); var lockW = Priv<float[]>(d, "lockW"); var stepping = Priv<bool[]>(d, "stepping"); var time = Priv<double[]>(d, "time");
            var ci = CultureInfo.InvariantCulture; var a = d.actor.transform.position;
            string P(Transform t) => t ? t.position.x.ToString("0.0000", ci) + "," + t.position.z.ToString("0.0000", ci) : ",";
            diag.Add(string.Join(",", scenario, frame, Time.deltaTime.ToString("0.00000", ci), a.x.ToString("0.0000", ci), a.z.ToString("0.0000", ci), d.actor.Speed.ToString("0.000", ci), P(feet[0]), P(feet[1]), P(feet[2]), P(feet[3]),
                locked[0] ? 1 : 0, locked[1] ? 1 : 0, lockW[0].ToString("0.00", ci), lockW[1].ToString("0.00", ci), stepping[0] ? 1 : 0, stepping[1] ? 1 : 0, time[(int)HeroTennisDriver.Clip.Walk].ToString("0.0000", ci), d.FootSkate.ToString("0.000", ci), d.WeightReport().Replace(',', ';'),
                d.transform.position.x.ToString("0.0000", ci), d.transform.position.z.ToString("0.0000", ci), Priv<float>(d, "paceRatio").ToString("0.000", ci), Priv<Vector3>(d, "smoothLocal").x.ToString("0.0000", ci), Priv<Vector3>(d, "smoothLocal").z.ToString("0.0000", ci),
                time[(int)HeroTennisDriver.Clip.RunForward].ToString("0.0000", ci), time[(int)HeroTennisDriver.Clip.RunLeft].ToString("0.0000", ci), time[(int)HeroTennisDriver.Clip.RunRight].ToString("0.0000", ci), d.actor.ForwardSpeed.ToString("0.000", ci)));
        }
        static bool DiagRun => Environment.GetEnvironmentVariable("SF_DIAG") == "run";

        [Serializable] class Shot { public string file, scenario, kind, role, sex, clip, state, note; public float clipTime, contact, hipsYaw, chestYaw; public int frame; }
        [Serializable] class ShotFile { public Shot[] shots; public string[] audit; public string[] contacts; public string[] serves; }
        static readonly List<Shot> shots = new List<Shot>();
        static readonly List<string> audit = new List<string>(), contactLog = new List<string>(), serveLog = new List<string>(), trace = new List<string>();
        static int serveContactsPlayer, serveContactsRival; static string serveMode = "auto";
        static string Fmt(Vector3 v) => $"{v.x:0.000};{v.y:0.000};{v.z:0.000}";

        // ================================================================ foot probe (lowest skinned vertex of each foot)
        /// Foot vertices = the vertices whose largest skin weight is a Foot / Toes bone of that side (the same definition the Blender gates use), found from the imported mesh's own bone weights.
        class FootProbe
        {
            public SkinnedMeshRenderer body; public int[] L, R; public Mesh baked; public bool ok; public string why = ""; public int nL, nR;
            public static FootProbe Make(HeroTennisDriver d)
            {
                var p = new FootProbe { body = d.matchLook.body, baked = new Mesh() };
                var mesh = p.body.sharedMesh; var bw = mesh.boneWeights; var bones = p.body.bones;
                if (bw.Length != mesh.vertexCount) { p.why = "mesh has no bone weights readable"; return p; }
                var l = new List<int>(); var r = new List<int>();
                for (int i = 0; i < bw.Length; i++)
                {
                    int bi = bw[i].boneIndex0; if (bi < 0 || bi >= bones.Length || !bones[bi]) continue;
                    string n = bones[bi].name;
                    if (n == "LeftFoot" || n == "LeftToes") l.Add(i); else if (n == "RightFoot" || n == "RightToes") r.Add(i);
                }
                p.L = l.ToArray(); p.R = r.ToArray(); p.nL = p.L.Length; p.nR = p.R.Length;
                if (p.nL < 100 || p.nR < 100) { p.why = $"too few foot vertices ({p.nL}/{p.nR})"; return p; }
                p.ok = true; return p;
            }
            public void Floor(HeroTennisDriver d)
            {
                d.Sample(HeroTennisDriver.Clip.Ready, 0f);
                var h = Heights(d); floorBase = Mathf.Min(h.x, h.y);
            }
            public float floorBase;
            /// (left, right) lowest-vertex height above the actor's ground, metres
            public Vector2 Heights(HeroTennisDriver d)
            {
                body.BakeMesh(baked, false);
                var v = baked.vertices; var t = body.transform; float gy = d.actor.transform.position.y;
                float lo = float.MaxValue, ro = float.MaxValue;
                foreach (int i in L) lo = Mathf.Min(lo, t.TransformPoint(v[i]).y - gy);
                foreach (int i in R) ro = Mathf.Min(ro, t.TransformPoint(v[i]).y - gy);
                return new Vector2(lo, ro);
            }
        }
        static readonly Dictionary<HeroTennisDriver, FootProbe> probes = new Dictionary<HeroTennisDriver, FootProbe>();
        static FootProbe Probe(HeroTennisDriver d)
        {
            if (!probes.TryGetValue(d, out var p))
            {
                p = FootProbe.Make(d); probes[d] = p;
                if (p.ok) p.Floor(d);
                audit.Add($"  foot probe {(d.matchLook.female ? "Female" : "Male")} {(d.isPlayer ? "PLAYER" : "RIVAL")}: {(p.ok ? $"ok, {p.nL}/{p.nR} foot vertices, floor {p.floorBase * 1000:0.00} mm" : "DISABLED: " + p.why)}");
            }
            return p;
        }

        // ================================================================ trace
        const string TraceHeader = "scenario,role,sex,frame,t,flow,state,clip,clipTime,actionWeight,speed,fwdSpeed,speedMag,footSkate,hipsYaw,chestYaw,footL_mm,footR_mm,swinging,ttc,prepare,actorX,actorZ,weights,fc";
        static void Trace(TennisGame game, string scenario, int frame, HeroTennisDriver d, string role, bool bake)
        {
            if (!d || !d.actor) return;
            var a = d.actor; var yaw = d.BodyYaw();
            string fl = "", fr = "";
            if (bake) { var p = Probe(d); if (p.ok) { var h = p.Heights(d); fl = ((h.x - p.floorBase) * 1000f).ToString("0.0", CultureInfo.InvariantCulture); fr = ((h.y - p.floorBase) * 1000f).ToString("0.0", CultureInfo.InvariantCulture); } }
            trace.Add(string.Join(",", scenario, role, d.matchLook.female ? "F" : "M", frame, Time.time.ToString("0.000", CultureInfo.InvariantCulture), game.Flow, d.State.Replace(',', ';'), d.PlayingClip, d.PlayingClipTime.ToString("0.0000", CultureInfo.InvariantCulture),
                d.ActionWeight.ToString("0.000", CultureInfo.InvariantCulture), a.Speed.ToString("0.00", CultureInfo.InvariantCulture), a.ForwardSpeed.ToString("0.00", CultureInfo.InvariantCulture),
                new Vector2(a.Speed, a.ForwardSpeed).magnitude.ToString("0.00", CultureInfo.InvariantCulture), d.FootSkate.ToString("0.000", CultureInfo.InvariantCulture),
                yaw.x.ToString("0.0", CultureInfo.InvariantCulture), yaw.y.ToString("0.0", CultureInfo.InvariantCulture), fl, fr, a.Swinging, a.SignedTimeToContact.ToString("0.000", CultureInfo.InvariantCulture),
                a.PrepareAmount.ToString("0.000", CultureInfo.InvariantCulture), a.transform.position.x.ToString("0.000", CultureInfo.InvariantCulture), a.transform.position.z.ToString("0.000", CultureInfo.InvariantCulture), d.WeightReport().Replace(',', ';'), Time.frameCount));
        }

        // ================================================================ run loop
        static ServeAndFeetProof() { EditorApplication.update += Tick; }

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
                File.WriteAllText(Out + "/shots.json", JsonUtility.ToJson(new ShotFile { shots = shots.ToArray(), audit = audit.ToArray(), contacts = contactLog.ToArray(), serves = serveLog.ToArray() }, true));
                File.WriteAllText(Out + "/audit.txt", string.Join("\n", audit) + "\n\n-- gameplay contacts --\n" + string.Join("\n", contactLog) + "\n\n-- serves --\n" + string.Join("\n", serveLog) + "\n");
                File.WriteAllText(Out + "/trace.csv", TraceHeader + "\n" + string.Join("\n", trace) + "\n");
            }
            catch (Exception e) { Debug.LogException(e); code = 1; }
            Time.captureFramerate = 0;
            if (Application.isBatchMode) EditorApplication.Exit(code); else EditorApplication.isPlaying = false;
        }

        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        /// One frame per swing: the first at or past the gameplay contact (the same rule as the HERO_MAINSTAY proof). Serves are counted per role.
        static readonly Dictionary<HeroTennisDriver, bool> armed = new Dictionary<HeroTennisDriver, bool>();
        /// The last frames of (time, ttc, strings centre, ball) per hero, so the gap can be read at the instant of contact and not on whichever frame the ball is already launched.
        struct Hist { public float t, ttc; public Vector3 strings, ball; }
        static readonly Dictionary<HeroTennisDriver, List<Hist>> hist = new Dictionary<HeroTennisDriver, List<Hist>>();
        static void PerFrame(TennisGame game, HeroTennisDriver pd, HeroTennisDriver rd)
        {
            foreach (var drv in new[] { pd, rd })
            {
                if (!drv || !drv.actor) continue; var a = drv.actor;
                if (!hist.TryGetValue(drv, out var hl)) hist[drv] = hl = new List<Hist>();
                hl.Add(new Hist { t = Time.time, ttc = a.SignedTimeToContact, strings = drv.StringCentre, ball = game.BallPosition }); if (hl.Count > 6) hl.RemoveAt(0);
                if (!a.Swinging) { armed[drv] = true; continue; }
                if (!armed.TryGetValue(drv, out bool ar)) ar = true;
                if (!ar || a.SignedTimeToContact > .0085f) continue;
                armed[drv] = false; LogContact(game, drv.isPlayer);
            }
        }

        // ================================================================ the script
        static IEnumerator Script(TennisGame game)
        {
            Time.captureFramerate = 60;
            TennisGame.AutoPlayTimingJitter = .05f;
            var presentation = game.GetComponent<TennisPresentation>();
            if (presentation) presentation.Finish();
            yield return Frames(30);
            audit.Add("scene: " + EditorSceneManager.GetActiveScene().path + "  quality tier=" + TennisQuality.Current + "  unity=" + Application.unityVersion);

            foreach (var (key, playerFemale) in new[] { ("Nadia", false), ("Viktor", true) })
            {
                string scenario = "vs_" + key;
                var kit = TennisLook.Kit.From("FFFFFF", "1E2A5A", "F28C28", playerFemale ? "E8508F" : "2E6BD6", playerFemale ? 1 : 3);
                var look = HeroKit.Style.From(playerFemale ? 1 : 3, 2, 1, kit, playerFemale ? 1 : 0, playerFemale);
                look.SkinTint = HeroKit.Hex(playerFemale ? "EEBB8F" : "C47A4C");
                game.SelectCharacter(playerFemale); game.SetPlayerLook(look);
                game.ConfigureMatch(TennisGame.Mode.Campaign, key, key, "ROUND");
                if (presentation) presentation.Finish();
                yield return Frames(45);
                var pd = game.Player.GetComponentInChildren<HeroTennisDriver>(); var rd = game.Opponent.GetComponentInChildren<HeroTennisDriver>();
                audit.Add($"== {scenario}: player {(pd.matchLook.female ? "Female" : "Male")}, rival {(rd.matchLook.female ? "Female" : "Male")}");
                foreach (var d in new[] { pd, rd })
                {
                    audit.Add($"  {(d.isPlayer ? "PLAYER" : "RIVAL")} legacy look={(d.look != null)} matchLook={(d.matchLook != null)} walkClipSpeed={d.walkClipSpeed:0.000} runClipSpeed={d.runClipSpeed:0.000} serve ritual stance/release/trophy={d.serveStanceTime:0.000}/{d.serveReleaseTime:0.000}/{d.serveTrophyTime:0.000}");
                    foreach (var line in d.SlotReport().Split('\n').Where(l => l.Length > 0))
                    {
                        string clipName = line.Substring(line.IndexOf('=') + 1).Split(' ')[0];
                        var clip = d.slots.First(sl => sl.clip && sl.clip.name == clipName).clip;
                        audit.Add("      " + line + "  asset=" + AssetDatabase.GetAssetPath(clip));
                    }
                    Probe(d);
                }
                SaveGame($"{scenario}_wide", game, scenario, "wide", "wide game camera, HUD on", pd);

                // ---------------------------------------------------------------- WALK: the serve walk-in along the baseline
                int guard = 0; while (game.Flow != TennisGame.Phase.PlayerServeHold && guard++ < 600) yield return null;
                game.AutoPlay = false;
                int walkShots = 0; int walkFrames = 0;
                game.ServeNudge = Environment.GetEnvironmentVariable("SF_NUDGE") == "-1" ? -1f : 1f;
                for (int f = 0; f < 150; f++)
                {
                    yield return null; walkFrames++; PerFrame(game, pd, rd);
                    Trace(game, scenario, f, pd, "PLAYER", true); Trace(game, scenario, f, rd, "RIVAL", true);
                    if (DiagWalk) Diag(scenario, f, pd);
                    if (f >= 20 && f % 6 == 0 && walkShots < 18)
                    {
                        walkShots++; var ap = pd.actor.transform.position;
                        CloseUp(game, pd, $"{scenario}_walk_{walkShots:00}", ap + new Vector3(0.3f, 0.95f, 3.1f), ap + Vector3.up * .8f, 36, scenario, "walk", "PLAYER", 720);
                    }
                }
                game.ServeNudge = 0f;
                if (DiagWalk) { File.WriteAllText(Out + "/walkdiag_" + key + ".csv", "scenario,frame,dt,actorX,actorZ,speed,heelLx,heelLz,toeLx,toeLz,heelRx,heelRz,toeRx,toeRz,lockedL,lockedR,lockWL,lockWR,stepL,stepR,walkTime,footSkate,weights,heroX,heroZ,paceRatio,smoothX,smoothZ,tRunF,tRunL,tRunR,fwdSpeed\n" + string.Join("\n", diag) + "\n"); diag.Clear(); if (key == "Viktor" || Environment.GetEnvironmentVariable("SF_DIAG_ONE") == "1") Finish(0); }
                audit.Add($"  [{scenario}] walk: {walkShots} close-ups; player x={pd.actor.transform.position.x:0.00}");
                for (int f = 0; f < 90; f++) { yield return null; PerFrame(game, pd, rd); Trace(game, scenario, 200 + f, pd, "PLAYER", true); Trace(game, scenario, 200 + f, rd, "RIVAL", false); }

                // ---------------------------------------------------------------- SERVE: the first serve of the match (the game's own auto toss with accuracy 1, the swing started by hand like AutoPlay does)
                game.AutoPlay = false; serveMode = "perfect-showcase";
                int serveShot = 0; bool swung = false; int after = 0; int serveFrames = 0; float tossStart = -1; bool commanded = false;
                for (int f = 0; f < 60 * 14 && after < 120; f++)
                {
                    yield return null; serveFrames++; PerFrame(game, pd, rd);
                    Trace(game, scenario, 300 + f, pd, "PLAYER", true); Trace(game, scenario, 300 + f, rd, "RIVAL", false);
                    if (game.Flow == TennisGame.Phase.PlayerServeToss)
                    {
                        if (tossStart < 0) tossStart = Time.time;
                        if (!commanded && Time.time - tossStart >= .55f && !pd.actor.Swinging) { game.BeginSwing(0, .3f, 1); game.RequestSwing(.8f, 0, .3f, 1); commanded = true; }
                    }
                    bool serving = game.Flow == TennisGame.Phase.PlayerServeHold || game.Flow == TennisGame.Phase.PlayerServeToss || pd.CurrentAction == HeroTennisDriver.Clip.Serve;
                    if (pd.actor.Swinging && pd.CurrentAction == HeroTennisDriver.Clip.Serve) swung = true;
                    if (swung) after++;
                    bool detail = pd.actor.PrepareAmount > .001f || pd.actor.Swinging || swung;
                    if (serving && ((detail && f % 3 == 0) || (!detail && f % 20 == 0)) && serveShot < 64)
                    {
                        serveShot++;
                        CloseUp(game, pd, $"{scenario}_serve_{serveShot:00}_back", pd.actor.transform.TransformPoint(1.9f, 1.75f, -3.1f), pd.actor.transform.position + Vector3.up * 1.15f, 40, scenario, "serve", "PLAYER", 720);
                        CloseUp(game, pd, $"{scenario}_serve_{serveShot:00}_high", pd.actor.transform.TransformPoint(0.2f, 4.6f, -2.0f), pd.actor.transform.position + Vector3.up * .9f, 40, scenario, "serve_high", "PLAYER", 720);
                    }
                }
                audit.Add($"  [{scenario}] serve: {serveShot} sample times, swung={swung}, tossAccuracy={game.TossAccuracy:0.00}");
                SaveGame($"{scenario}_after_serve", game, scenario, "after_serve", "game camera after the serve", pd);

                // ---------------------------------------------------------------- PERFECT-TOSS SERVES: the same serve again, 14 times (the player's own contact against the toss it aims at)
                serveMode = "perfect"; int perfect = 0, perfectTry = 0;
                while (perfect < 14 && perfectTry++ < 40)
                {
                    game.StartPlayerServe();
                    int before = serveContactsPlayer; float ts = -1; bool cmd = false;
                    for (int f = 0; f < 60 * 12; f++)
                    {
                        yield return null; PerFrame(game, pd, rd); if (f % 3 == 0) Trace(game, scenario, 2000 + perfectTry * 1000 + f, pd, "PLAYER", true);
                        if (game.Flow == TennisGame.Phase.PlayerServeToss)
                        {
                            if (ts < 0) ts = Time.time;
                            if (!cmd && Time.time - ts >= .5f + .25f * UnityEngine.Random.value && !pd.actor.Swinging) { game.BeginSwing(0, .3f, 1); game.RequestSwing(.8f, 0, .3f, 1); cmd = true; }
                        }
                        if (serveContactsPlayer > before && f > 30) { for (int k = 0; k < 120; k++) { yield return null; PerFrame(game, pd, rd); } break; }
                    }
                    if (serveContactsPlayer > before) perfect++;
                }
                audit.Add($"  [{scenario}] perfect-toss serves logged: {perfect} in {perfectTry} tries");
                serveMode = "auto";

                // ---------------------------------------------------------------- RALLY: self-play until enough serves of each role are logged
                game.AutoPlay = true; game.AutoPlayLean = true; serveMode = "auto";
                var runWin = new Dictionary<HeroTennisDriver, int>(); var runShotsN = new Dictionary<string, int> { { "PLAYER", 0 }, { "RIVAL", 0 } };
                int startHits = game.Hits; int rallyFrames = 0;
                int overFrames = 0, restarts = 0;
                for (int frame = 0; frame < (Quick ? 60 * 25 : 60 * 1500); frame++)
                {
                    yield return null; rallyFrames++; PerFrame(game, pd, rd);
                    // a finished match plays no more serves: start the same match again (the same two heroes) to collect more serves
                    overFrames = game.Flow == TennisGame.Phase.MatchOver ? overFrames + 1 : 0;
                    if (overFrames > 180 && !Quick)
                    {
                        overFrames = 0; restarts++;
                        game.AutoPlay = false; game.ConfigureMatch(TennisGame.Mode.Campaign, key, key, "ROUND");
                        if (presentation) presentation.Finish();
                        for (int k = 0; k < 45; k++) yield return null;
                        pd = game.Player.GetComponentInChildren<HeroTennisDriver>(); rd = game.Opponent.GetComponentInChildren<HeroTennisDriver>();
                        audit.Add($"  [{scenario}] match restarted ({restarts}) at rally frame {rallyFrames}: player {(pd.matchLook.female ? "Female" : "Male")}, rival {(rd.matchLook.female ? "Female" : "Male")}");
                        game.AutoPlay = true; game.AutoPlayLean = true; continue;
                    }
                    if (DiagRun) { Diag(scenario, rallyFrames, pd); if (rallyFrames >= 1800) { File.WriteAllText(Out + "/rundiag_" + key + ".csv", "scenario,frame,dt,actorX,actorZ,speed,heelLx,heelLz,toeLx,toeLz,heelRx,heelRz,toeRx,toeRz,lockedL,lockedR,lockWL,lockWR,stepL,stepR,walkTime,footSkate,weights,heroX,heroZ,paceRatio,smoothX,smoothZ,tRunF,tRunL,tRunR,fwdSpeed\n" + string.Join("\n", diag) + "\n"); Finish(0); yield break; } }
                    foreach (var (drv, role) in new[] { (pd, "PLAYER"), (rd, "RIVAL") })
                    {
                        float sp = new Vector2(drv.actor.Speed, drv.actor.ForwardSpeed).magnitude;
                        bool bake = sp > .4f || frame % 8 == 0 || (role == "PLAYER" && game.Serving);
                        Trace(game, scenario, 1000 + frame, drv, role, bake);
                        // run close-up windows: side-on to the motion, every 2nd frame for 16 frames
                        if (!runWin.TryGetValue(drv, out int w)) w = 0;
                        if (w > 0)
                        {
                            runWin[drv] = w - 1;
                            if (w % 2 == 0)
                            {
                                var a = drv.actor; var v = a.transform.right * a.Speed + a.transform.forward * a.ForwardSpeed; v.y = 0; var dir = v.sqrMagnitude > .01f ? v.normalized : a.transform.right;
                                var side = Vector3.Cross(Vector3.up, dir); if (Vector3.Dot(side, new Vector3(0, 0, role == "PLAYER" ? 1 : -1)) < 0) side = -side;
                                runShotsN[role]++;
                                CloseUp(game, drv, $"{scenario}_run_{role.ToLower()}_{runShotsN[role]:000}", a.transform.position + side * 3.0f + Vector3.up * .95f, a.transform.position + Vector3.up * .8f, 38, scenario, "run", role, 720);
                            }
                        }
                        else if (sp > 3.2f && drv.State == "Run" && !game.ReplayPlaying && runShotsN[role] < (role == "PLAYER" ? 56 : 40) && w == 0 && frame % 7 == 0 && Time.frameCount > 0 && RunWeight(drv) > .9f && UnityEngine.Random.value < .5f) runWin[drv] = 16;
                    }
                    if (serveContactsRival >= (scenario == "vs_Nadia" ? 8 : 16) && serveContactsPlayer >= 40) break;
                    if (Quick && serveContactsPlayer >= 1) break;
                }
                audit.Add($"  [{scenario}] rally: frames={rallyFrames} hits={game.Hits - startHits} serve contacts logged player={serveContactsPlayer} rival={serveContactsRival}; run close-ups player={runShotsN["PLAYER"]} rival={runShotsN["RIVAL"]}; honestMisses={game.HonestMisses}");
                game.AutoPlay = false;
            }
            audit.Add($"total serve contacts: player {serveContactsPlayer}, rival {serveContactsRival}");
        }

        static float RunWeight(HeroTennisDriver d)
        {
            float s = 0; foreach (var tok in d.WeightReport().Split(' ')) if (tok.StartsWith("Run")) { var kv = tok.Split('='); if (kv.Length == 2 && float.TryParse(kv[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float w)) s += w; }
            return s;
        }

        // ================================================================ capture
        static void LogContact(TennisGame game, bool player)
        {
            var actor = player ? game.Player : game.Opponent; var d = actor ? actor.GetComponentInChildren<HeroTennisDriver>() : null; if (!d) return;
            bool serve = d.PlayingClip.EndsWith("_Serve");
            float cont = d.ContactOf(d.CurrentAction), gap = Vector3.Distance(d.StringCentre, game.BallPosition);
            ContactGap(d, out float gap0, out float gapMin);
            string line = $"{(player ? "PLAYER" : "RIVAL ")} {(d.matchLook.female ? "Female" : "Male  ")} clip={d.PlayingClip} clipTime={d.PlayingClipTime:0.000}s clipContact={cont:0.000}s delta={d.PlayingClipTime - cont:+0.000;-0.000}s stringsToBall={gap:0.000}m ttc={actor.SignedTimeToContact:0.000} kind={actor.Kind}";
            contactLog.Add(line);
            if (serve)
            {
                serveLog.Add($"{(player ? "PLAYER" : "RIVAL")},{(d.matchLook.female ? "F" : "M")},{d.PlayingClipTime:0.0000},{cont:0.0000},{d.PlayingClipTime - cont:0.0000},{gap:0.0000},{Time.frameCount},{(player ? game.TossAccuracy : 1f):0.00},{serveMode},{gap0:0.0000},{gapMin:0.0000}");
                if (player) serveContactsPlayer++; else serveContactsRival++;
            }
        }

        /// Strings-to-ball distance at the instant the game's time-to-contact reaches 0 (gap0), and the closest approach over the frame before and after it (gapMin).
        /// The frame the log fires on can come after the game's contact step, when the ball is already off the strings; the ball is then taken back along its own pre-launch
        /// path (a line through the last two pre-launch frames) and the strings along their own path (between the last two frames).
        static void ContactGap(HeroTennisDriver d, out float gap0, out float gapMin)
        {
            gap0 = gapMin = -1f;
            if (!hist.TryGetValue(d, out var h) || h.Count < 3) return;
            var n = h[h.Count - 1]; var p = h[h.Count - 2]; var pp = h[h.Count - 3];
            bool launched = Vector3.Distance(n.ball, p.ball) > .25f && Vector3.Distance(p.ball, pp.ball) < .25f;
            Hist b1 = launched ? p : n, b0 = launched ? pp : p;        // the last two pre-launch ball samples
            float t0;                                                  // the contact instant, from the ttc series
            if (p.ttc > 0 && n.ttc <= 0) t0 = p.t + (n.t - p.t) * p.ttc / (p.ttc - n.ttc);
            else if (n.ttc > 0) { float slope = (p.ttc - n.ttc) / Mathf.Max(1e-4f, n.t - p.t); t0 = n.t + n.ttc / Mathf.Max(.05f, slope); }
            else t0 = p.t;
            Vector3 Ball(float t) => b1.ball + (b1.ball - b0.ball) / Mathf.Max(1e-4f, b1.t - b0.t) * (t - b1.t);
            Vector3 Str(float t) => p.strings + (n.strings - p.strings) * ((t - p.t) / Mathf.Max(1e-4f, n.t - p.t));
            gap0 = Vector3.Distance(Str(t0), Ball(t0));
            float lo = p.t, hi = Mathf.Max(n.t, t0); gapMin = float.MaxValue;
            for (int i = 0; i <= 40; i++) { float t = Mathf.Lerp(lo, hi, i / 40f); gapMin = Mathf.Min(gapMin, Vector3.Distance(Str(t), Ball(t))); }
        }

        static Camera Cam(TennisGame game) => game.GameplayCamera ? game.GameplayCamera : Camera.main;

        static void SaveGame(string name, TennisGame game, string scenario, string kind, string note, HeroTennisDriver pd)
        {
            string file = $"raw/{name}.png";
            GameCapture.Save(Out + "/" + file, 1280, 720);
            shots.Add(new Shot { file = file, scenario = scenario, kind = kind, note = note, frame = Time.frameCount, sex = pd && pd.matchLook ? (pd.matchLook.female ? "female" : "male") : "", role = "PLAYER" });
        }

        /// The game camera, moved to look at one hero (no HUD).
        static void CloseUp(TennisGame game, HeroTennisDriver d, string name, Vector3 from, Vector3 look, float fov, string scenario, string kind, string role, int size)
        {
            var cam = Cam(game);
            var p = cam.transform.position; var r = cam.transform.rotation; var f = cam.fieldOfView;
            cam.transform.SetPositionAndRotation(from, Quaternion.LookRotation(look - from)); cam.fieldOfView = fov;
            string file = $"raw/{name}.jpg"; Save(cam, Out + "/" + file, size, size);
            cam.transform.SetPositionAndRotation(p, r); cam.fieldOfView = f;
            var yaw = d.BodyYaw();
            shots.Add(new Shot { file = file, scenario = scenario, kind = kind, role = role, sex = d.matchLook.female ? "female" : "male", clip = d.PlayingClip, state = d.State, clipTime = d.PlayingClipTime, contact = d.ContactOf(d.CurrentAction),
                hipsYaw = yaw.x, chestYaw = yaw.y, frame = Time.frameCount, note = "game camera moved to the hero | " + d.WeightReport() });
        }

        static void Save(Camera cam, string path, int w, int h)
        {
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, 4);
            var prev = cam.targetTexture; cam.targetTexture = rt; cam.Render(); cam.targetTexture = prev;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false); var active = RenderTexture.active;
            RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply(); RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(rt);
            File.WriteAllBytes(path, path.EndsWith(".jpg") ? tex.EncodeToJPG(90) : tex.EncodeToPNG()); Object.DestroyImmediate(tex);
        }
    }
}
