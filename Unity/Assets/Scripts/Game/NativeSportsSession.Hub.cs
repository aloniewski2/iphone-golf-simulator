using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using GolfArcade.Hub;
using GolfArcade.Tennis;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace GolfArcade.Game
{
    /// The `hub` session kind (PLAN_MenuHub_WalkableWorld §5 step 1): with a TV connected the menus are the walkable Plaza
    /// (Hub.unity) instead of the native TV menu. Same bridge as a match: the phone sends `start` with sport "hub", then the
    /// stick on the binary sample channel (target = x, aim = y, power = deflection, at the phone's 30–60 Hz) and buttons as
    /// commands. Unity reports where the player is.
    ///
    ///   phone -> Unity   start{sport:"hub"}  hubA  hubB  hubEmote{mode:id}  hubGo{mode:place|spot}  hubUse{mode:spot} (go there and use it)  hubLook{female,skin,skinHex,shirt,shorts,accent,racket}  end
    ///   Unity -> phone   ready  hubPlace(place)  hubZone(kind|id|label|detail)  hubAction(spot id)  hubState(place|zone|speed|inputAge ms p50|p95)
    public sealed partial class NativeSportsSession
    {
        /// A match start from a bay: load it behind the plaza (additive, hidden) and hand off with a dive into the bay screen.
        public partial class Message { public bool seamless; public string bay; public string remote; }
        bool hubMode; HubWorld hub; float nextHubBeat, nextHubPose;
        readonly float[] hubAges = new float[64]; int hubAgeCount, hubAgeNext;
        /// True while the phone is driving the Plaza.
        public static bool HubActive { get; private set; }
        public HubWorld Hub => hub;

        void StartHub(Message m)
        {
            if (Active && hubMode && m.session == session) return;
            // a bay load was cancelled (standing up): the plaza never left, so take it over instead of reloading it
            var existing = adoptable; adoptable = null;
            if (existing && existing.Initialized && existing.gameObject.activeInHierarchy) { StopAllCoroutines(); loading = false; StartCoroutine(AdoptHub(m, existing)); return; }
            if (loading) return;
            StartCoroutine(LoadHub(m));
        }
        /// A match start leaves the plaza (the match scene replaces Hub.unity; PLAN §5 makes this seamless later).
        void LeaveHub() { hubMode = false; HubActive = false; hub = null; }

        IEnumerator LoadHub(Message m)
        {
            loading = true; Ready = false; Active = true; hubMode = true; HubActive = true; multiplayerSession = false;
            session = m.session; token = m.token; lastSample = -1; target = 0; tennis = null; golf = null; hub = null;
            Left = m.left; Touch = true; touch = true;
            AudioListener.volume = m.sound && !Application.isBatchMode ? 1 : 0; Haptics.Enabled = m.haptics;
            Time.timeScale = 1;
            Application.targetFrameRate = m.external ? 60 : FrameRate.Target(m.fps, Screen.currentResolution.refreshRateRatio.value);
            QualitySettings.vSyncCount = 0;
            Screen.orientation = m.external ? ScreenOrientation.Portrait : ScreenOrientation.LandscapeLeft;
            HubWorld.StartFemale = m.female;
            var op = SceneManager.LoadSceneAsync("Hub");
            float nextProgress = 0;
            while (op != null && !op.isDone)
            {
                if (Time.realtimeSinceStartup >= nextProgress)
                {
                    nextProgress = Time.realtimeSinceStartup + .1f;
                    Emit("loadProgress", Mathf.Clamp01(op.progress / .9f).ToString("0.00", CultureInfo.InvariantCulture));
                }
                yield return null;
            }
            Emit("loadProgress", "1"); Emit("sceneReady", "Plaza loaded");
            hub = HubWorld.Find();
            if (!hub || !hub.Initialized || !hub.Camera) { loading = false; Emit("error", "The plaza did not load."); yield break; }
            hub.PlaceChanged += p => { if (hubMode) Emit("hubPlace", p); };
            hub.ZoneChanged += _ => { if (hubMode) Emit("hubZone", ZoneMessage()); };
            hub.Interacted += id => { if (hubMode) Emit("hubAction", id); };
            hub.ApplyLook(m.female, m.skinHex, m.skin, m.shirt, m.shorts, m.accent, m.racket);
            if (!string.IsNullOrEmpty(m.bay)) hub.SitAt(m.bay);   // back from a match: on the same bench (RETURN)
            gameplayCamera = hub.Camera;
            paused = true;   // the point recorder stays off; the plaza itself never pauses (timeScale stays 1)
            yield return Present(m.external, "ready");
            loading = false;
            if (!Ready) yield break;
            Emit("hubPlace", hub.Place); Emit("hubZone", ZoneMessage());
        }

        /// The plaza is still loaded (a bay load was cancelled): take it over again without reloading it.
        IEnumerator AdoptHub(Message m, HubWorld world)
        {
            loading = true; Ready = false; Active = true; hubMode = true; HubActive = true; multiplayerSession = false;
            session = m.session; token = m.token; lastSample = -1; tennis = null; golf = null; hub = world; Time.timeScale = 1;
            if (world.gameObject.scene.IsValid()) SceneManager.SetActiveScene(world.gameObject.scene);
            if (!string.IsNullOrEmpty(m.bay)) world.SitAt(m.bay); else world.LeaveStation();
            world.BayLoading(-1);
            world.ApplyLook(m.female, m.skinHex, m.skin, m.shirt, m.shorts, m.accent, m.racket);
            gameplayCamera = world.Camera; paused = true;
            yield return Present(m.external, "ready");
            loading = false;
            Emit("hubPlace", world.Place); Emit("hubZone", ZoneMessage());
        }

        // ================================================================== a match loading behind the plaza (PLAN §5)
        bool behindPlaza; AsyncOperation behindOp; HiddenMatch hidden; bool cancelBehind; HubWorld adoptable;
        readonly List<ResourceRequest> preloads = new List<ResourceRequest>();
        /// Proof: the plaza's frame times while the match loaded behind it (ms), and the memory peak.
        public static readonly List<float> BehindFrameMs = new List<float>();
        public static long BehindPeakBytes;
        public static readonly List<string> BehindSlowFrames = new List<string>();

        /// Load's scene request: the normal single-scene load, or, from a bay with the plaza up, an additive load held behind it.
        AsyncOperation BeginSceneLoad(Message m)
        {
            string scene = m.sport == "tennis" ? "Tennis" : "Golf";
            var world = HubWorld.Find();
            behindPlaza = m.seamless && world && world.Initialized && world.gameObject.activeInHierarchy;
            if (!behindPlaza) return SceneManager.LoadSceneAsync(scene);
            hub = world; cancelBehind = false; BehindFrameMs.Clear(); BehindPeakBytes = 0; BehindSlowFrames.Clear();
            TennisGame.SliceInit = true;
            Application.backgroundLoadingPriority = ThreadPriority.Low;   // asset integration stays a few ms per frame
            // the big assets the match's build asks for synchronously: fetch them now, off the main thread
            preloads.Clear();
            foreach (var path in m.sport == "tennis"
                ? new[] { "Tennis/TropicalV3/TropicalTennisResort", "Tennis/Island/TennisIsland", "Tennis/Customization/PlayerMale", "Tennis/Customization/PlayerFemale", "Tennis/Rendering/TennisPost" }
                : new string[0])
                preloads.Add(Resources.LoadAsync(path));
            hidden = gameObject.AddComponent<HiddenMatch>(); hidden.Begin(scene, world);
            var op = SceneManager.LoadSceneAsync(scene, LoadSceneMode.Additive);
            op.allowSceneActivation = false; behindOp = op;
            world.BayLoading(0);
            return op;
        }
        void SceneLoadTick(AsyncOperation op)
        {
            if (!behindPlaza || op != behindOp) return;
            float assets = 0; foreach (var r in preloads) assets += r.isDone ? 1 : r.progress; assets = preloads.Count > 0 ? assets / preloads.Count : 1;
            if (hub) hub.BayLoading(.6f * Mathf.Min(Mathf.Clamp01(op.progress / .9f), assets));
            // activate once the scene's data and the preloads are in (the build itself is sliced across frames)
            if (op.progress >= .9f && assets >= 1 && !cancelBehind) op.allowSceneActivation = true;
        }

        /// The match is built and hidden: (solo) go now — the camera dives into the bay screen, the match takes the TV, the plaza unloads.
        IEnumerator HandOff(Message m)
        {
            Emit("matchReady", m.bay ?? "");
            if (hub) { hub.BayLoading(1); yield return hub.Dive(.55f); }
            TennisGame.SliceInit = false; Application.backgroundLoadingPriority = ThreadPriority.Normal;
            var plaza = hub ? hub.gameObject.scene : default;
            if (hub) hub.gameObject.SetActive(false);
            if (hidden) hidden.Reveal();
            preloads.Clear();
            yield return Present(m.external, "ready");
            behindPlaza = false; hub = null; HubActive = false;
            if (plaza.IsValid() && plaza.isLoaded) SceneManager.UnloadSceneAsync(plaza);
        }

        /// Hub commands, and "end" for a match that is still loading behind the plaza (standing up from the bench: CANCEL).
        bool HubIntercept(Message m)
        {
            if (hubMode && HubCommand(m)) return true;
            if (behindPlaza && loading && m.action == "end") { CancelBehind(); return true; }
            return false;
        }
        void CancelBehind()
        {
            StopAllCoroutines(); loading = false; Ready = false; Active = false; Left = false; Time.timeScale = 1;
            cancelBehind = true; StartCoroutine(UnloadBehind());
        }
        IEnumerator UnloadBehind()
        {
            var op = behindOp; if (op != null) { op.allowSceneActivation = true; while (!op.isDone) yield return null; }
            var plaza = HubWorld.Find(); adoptable = plaza;
            if (plaza && plaza.gameObject.scene.IsValid()) SceneManager.SetActiveScene(plaza.gameObject.scene);
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.isLoaded && (!plaza || s != plaza.gameObject.scene) && (s.name == "Tennis" || s.name == "Golf")) SceneManager.UnloadSceneAsync(s);
            }
            if (hidden) hidden.Abandon();
            TennisGame.SliceInit = false; Application.backgroundLoadingPriority = ThreadPriority.Normal;
            preloads.Clear(); behindOp = null; behindPlaza = false; tennis = null; golf = null;
            if (plaza) plaza.BayLoading(-1);
        }

        string ZoneMessage()
        {
            if (!hub) return "";
            if (hub.ZoneSpot != null) { var s = hub.ZoneSpot; return $"{(s.kind == HubLayout.Kind.Bay ? "bay" : "station")}|{s.id}|{s.label}|{s.detail}"; }
            if (hub.ZoneDoor != null) { var d = hub.ZoneDoor; return $"door|{d.id}|{d.label}|{(d.locked ? "Coming soon" : "Walk in")}"; }
            return "";
        }

        /// Hub commands; false lets the shared handler take it (sound, haptics, display).
        bool HubCommand(Message m)
        {
            switch (m.action)
            {
                case "end":
                    if (hub && hub.Player) hub.Player.SetStick(0, 0, 0);
                    StopAllCoroutines(); loading = false; Ready = false; LeaveHub(); Active = false; Left = false;
                    return true;
                case "pause": case "resume": return true;   // the plaza has no pause
                case "hubA": if (hub) hub.Interact(); return true;
                case "hubB": if (hub) hub.LeaveStation(); return true;
                case "hubUse": if (hub && !string.IsNullOrEmpty(m.mode)) hub.GoTo(m.mode, true); return true;
                case "hubGo": if (hub && !string.IsNullOrEmpty(m.mode)) hub.GoTo(m.mode); return true;
                case "hubEmote":
                    if (hub && hub.Player && TennisEmotes.TryClip(m.mode, out var clip))
                        Emit("emoteResult", hub.Player.PlayEmote(clip.ToString()) ? TennisEmotes.Name(m.mode) : "Stand still to emote");
                    return true;
                case "hubLook": if (hub) hub.ApplyLook(m.female, m.skinHex, m.skin, m.shirt, m.shorts, m.accent, m.racket); return true;
                // party (PLAN §4): a friend's state, the member list, Call party
                case "hubRemote": if (hub && !string.IsNullOrEmpty(m.remote)) HubRemotes.For(hub).Receive(JsonUtility.FromJson<RemoteState>(m.remote)); return true;
                case "hubRoster": if (hub) HubRemotes.For(hub).Roster((m.mode ?? "").Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries)); return true;
                case "hubCall": if (hub) HubRemotes.For(hub).Call(m.mode); return true;
            }
            return false;
        }

        void HubUpdate()
        {
            if (!hub || !hub.Player) return;
            Sample latest = default; bool have = false; double now = SportsClock();
            for (int i = 0; i < 64; i++)
            {
                if (SportsPollSample(out var sample) == 0) break;
                if (!AcceptSample(sample, token, lastSample, now)) continue;
                lastSample = sample.time; latest = sample; have = true;
            }
            if (have)
            {
                hub.Player.SetStick(latest.target, latest.aim, latest.power, latest.time);
                hubAges[hubAgeNext] = (float)(now - latest.time); hubAgeNext = (hubAgeNext + 1) % hubAges.Length; hubAgeCount = Mathf.Min(hubAgeCount + 1, hubAges.Length);
            }
            // a phone that stops talking (backgrounded, out of range) must not leave the hero running
            else if (lastSample > 0 && now - lastSample > .4 && hub.Player.StickMagnitude > 0) hub.Player.SetStick(0, 0, 0);
            // my pose for the party (the phone relays it to friends at ~10 Hz): place|x|y|z|yaw|speed|seated bay|emote
            if (Time.unscaledTime >= nextHubPose)
            {
                nextHubPose = Time.unscaledTime + .1f;
                var pp = hub.Player.transform.position;
                string seat = hub.SeatedInBay ? hub.Station.id : "";
                string emote = hub.Player.hero && hub.Player.hero.Playing != null ? hub.Player.hero.Playing : "";
                Emit("hubPose", string.Format(CultureInfo.InvariantCulture, "{0}|{1:0.000}|{2:0.000}|{3:0.000}|{4:0.0}|{5:0.00}|{6}|{7}", hub.Place, pp.x, pp.y, pp.z, hub.Player.Yaw, hub.Player.Speed, seat, emote));
            }
            if (Time.unscaledTime >= nextHubBeat)
            {
                nextHubBeat = Time.unscaledTime + .25f;
                Emit("hubState", string.Format(CultureInfo.InvariantCulture, "{0}|{1}|{2:0.00}|{3:0}|{4:0}", hub.Place, hub.ZoneId, hub.Player.Speed, AgePercentile(.5f) * 1000, AgePercentile(.95f) * 1000));
            }
        }

        readonly float[] ageScratch = new float[64];
        float AgePercentile(float p)
        {
            if (hubAgeCount == 0) return 0;
            System.Array.Copy(hubAges, ageScratch, hubAgeCount); System.Array.Sort(ageScratch, 0, hubAgeCount);
            return ageScratch[Mathf.Clamp(Mathf.RoundToInt(p * (hubAgeCount - 1)), 0, hubAgeCount - 1)];
        }
    }
}

namespace GolfArcade.Game
{
    /// Keeps a match scene invisible and silent while it loads and builds behind the plaza: its cameras, lights, canvases,
    /// post-processing volumes and audio listeners are held off (state recorded), the plaza's lighting settings stay in force (the
    /// match's own are remembered for the hand-off), and the plaza's frame times and memory are recorded for the gates.
    sealed class HiddenMatch : MonoBehaviour
    {
        string sceneName; Scene scene; bool loaded; HubWorld plaza;
        Snapshot hubLight, matchLight; bool sawMatchLight;
        readonly List<(Behaviour b, bool was)> held = new List<(Behaviour, bool)>();
        readonly HashSet<Behaviour> seen = new HashSet<Behaviour>();

        public void Begin(string name, HubWorld world)
        {
            sceneName = name; plaza = world; hubLight = Snapshot.Take();
            AudioListener.pause = true;
            SceneManager.sceneLoaded += Loaded;
        }
        void OnDestroy() { SceneManager.sceneLoaded -= Loaded; }
        void Loaded(Scene s, LoadSceneMode mode)
        {
            if (s.name != sceneName || mode != LoadSceneMode.Additive) return;
            scene = s; loaded = true;
            // objects the match creates in Start must land in its own scene, not the plaza's (which unloads later)
            SceneManager.SetActiveScene(s);
            Hold();
        }
        void LateUpdate()
        {
            if (plaza && plaza.gameObject.activeInHierarchy)
            {
                NativeSportsSession.BehindFrameMs.Add(Time.unscaledDeltaTime * 1000);
                if (Time.unscaledDeltaTime > .05f)
                {
                    var g = FindFirstObjectByType<GolfArcade.Tennis.TennisGame>();
                    NativeSportsSession.BehindSlowFrames.Add($"{Time.unscaledDeltaTime * 1000:0}ms loaded={loaded} initProgress={(g ? g.InitProgress : -1):0.00} initialized={(g && g.Initialized)} frame={Time.frameCount}");
                }
                NativeSportsSession.BehindPeakBytes = System.Math.Max(NativeSportsSession.BehindPeakBytes, UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong());
            }
            if (!loaded) return;
            Hold();
            // the match sets its sky / ambient / reflections in its build: remember them, keep the plaza's on screen
            var now = Snapshot.Take();
            if (!now.Same(hubLight)) { matchLight = now; sawMatchLight = true; hubLight.Apply(); }
            var game = FindFirstObjectByType<GolfArcade.Tennis.TennisGame>();
            if (plaza && game && !game.Initialized) plaza.BayLoading(.6f + .4f * game.InitProgress);
        }
        void Hold()
        {
            if (!scene.IsValid()) return;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var c in root.GetComponentsInChildren<Camera>(true)) Keep(c);
                foreach (var c in root.GetComponentsInChildren<Light>(true)) Keep(c);
                foreach (var c in root.GetComponentsInChildren<Canvas>(true)) Keep(c);
                foreach (var c in root.GetComponentsInChildren<Volume>(true)) Keep(c);
                foreach (var c in root.GetComponentsInChildren<AudioListener>(true)) Keep(c);
            }
        }
        void Keep(Behaviour b)
        {
            if (!b || !seen.Add(b)) { if (b && b.enabled && held.Exists(h => h.b == b)) b.enabled = false; return; }
            held.Add((b, b.enabled)); b.enabled = false;
        }
        /// Hand-off: the match's components and lighting come back exactly as it set them.
        public void Reveal()
        {
            foreach (var (b, was) in held) if (b) b.enabled = was;
            if (sawMatchLight) matchLight.Apply();
            AudioListener.pause = false; Destroy(this);
        }
        /// Cancelled: the match scene is being unloaded; the plaza keeps its own settings.
        public void Abandon() { hubLight.Apply(); AudioListener.pause = false; Destroy(this); }

        struct Snapshot
        {
            AmbientMode mode; Color sky, equator, ground, flat; float ambientIntensity; SphericalHarmonicsL2 probe; Material skybox; Light sun;
            bool fog; Color fogColor; FogMode fogMode; float fogDensity, fogStart, fogEnd;
            DefaultReflectionMode reflectionMode; Texture reflection; float reflectionIntensity; int bounces; Color subtractive;
            public static Snapshot Take() => new Snapshot
            {
                mode = RenderSettings.ambientMode, sky = RenderSettings.ambientSkyColor, equator = RenderSettings.ambientEquatorColor, ground = RenderSettings.ambientGroundColor,
                flat = RenderSettings.ambientLight, ambientIntensity = RenderSettings.ambientIntensity, probe = RenderSettings.ambientProbe, skybox = RenderSettings.skybox, sun = RenderSettings.sun,
                fog = RenderSettings.fog, fogColor = RenderSettings.fogColor, fogMode = RenderSettings.fogMode, fogDensity = RenderSettings.fogDensity,
                fogStart = RenderSettings.fogStartDistance, fogEnd = RenderSettings.fogEndDistance, reflectionMode = RenderSettings.defaultReflectionMode,
                reflection = RenderSettings.customReflectionTexture, reflectionIntensity = RenderSettings.reflectionIntensity, bounces = RenderSettings.reflectionBounces,
                subtractive = RenderSettings.subtractiveShadowColor,
            };
            public bool Same(Snapshot o) => mode == o.mode && sky == o.sky && equator == o.equator && ground == o.ground && flat == o.flat && skybox == o.skybox && sun == o.sun
                && fog == o.fog && fogColor == o.fogColor && reflectionMode == o.reflectionMode && reflection == o.reflection && Mathf.Approximately(ambientIntensity, o.ambientIntensity)
                && Mathf.Approximately(reflectionIntensity, o.reflectionIntensity);
            public void Apply()
            {
                RenderSettings.ambientMode = mode; RenderSettings.ambientSkyColor = sky; RenderSettings.ambientEquatorColor = equator; RenderSettings.ambientGroundColor = ground;
                RenderSettings.ambientLight = flat; RenderSettings.ambientIntensity = ambientIntensity; RenderSettings.ambientProbe = probe; RenderSettings.skybox = skybox; RenderSettings.sun = sun;
                RenderSettings.fog = fog; RenderSettings.fogColor = fogColor; RenderSettings.fogMode = fogMode; RenderSettings.fogDensity = fogDensity;
                RenderSettings.fogStartDistance = fogStart; RenderSettings.fogEndDistance = fogEnd; RenderSettings.defaultReflectionMode = reflectionMode;
                RenderSettings.customReflectionTexture = reflection; RenderSettings.reflectionIntensity = reflectionIntensity; RenderSettings.reflectionBounces = bounces;
                RenderSettings.subtractiveShadowColor = subtractive;
            }
        }
    }
}
