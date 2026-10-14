using System;
using System.Collections;
using System.Collections.Generic;
using GolfArcade.Tennis;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace GolfArcade.Hub
{
    /// The Plaza (PLAN_MenuHub_WalkableWorld): one scene, Hub.unity, holding the outdoor plaza and every room. This component is the
    /// scene's only authored object; it builds the greybox from HubLayout, spawns the player's match hero, runs the follow camera,
    /// the walk-through doors, the floating prompts and the fade, and reports where the player is to the phone (NativeSportsSession
    /// relays PlaceChanged / ZoneChanged / Interacted).
    [DefaultExecutionOrder(-100)]
    public sealed class HubWorld : MonoBehaviour
    {
        public static HubWorld Instance { get; private set; }
        /// The plaza draws only this layer, so a match scene loading at the same place (both sit at the origin) stays out of its shots.
        public const int Layer = 29;
        public static void SetLayer(Transform t) { foreach (var c in t.GetComponentsInChildren<Transform>(true)) c.gameObject.layer = Layer; }
        public const float DoorSeconds = .40f;

        public Camera Camera { get; private set; }
        public HubPlayer Player { get; private set; }
        public string Place { get; private set; } = HubLayout.Plaza;
        /// The nearest thing you can use: a door (walk into it) or a station / bay (press A). Null when nothing is close.
        public HubLayout.Door ZoneDoor { get; private set; }
        public HubLayout.Spot ZoneSpot { get; private set; }
        public string ZoneId => ZoneSpot != null ? ZoneSpot.id : ZoneDoor?.id ?? "";
        public bool Transitioning { get; private set; }
        public bool Initialized { get; private set; }
        /// Proof / gate data: every completed door walk-through (door id, seconds from trigger to control back, worst frame inside it).
        public readonly List<(string door, float seconds, float worstFrame)> DoorLog = new List<(string, float, float)>();

        public event Action<string> PlaceChanged;
        public event Action<string> ZoneChanged;
        /// A (select) at a station or bay: its spot id.
        public event Action<string> Interacted;

        Transform world, youTag; Image fade; Canvas overlay; Text prompt; Transform promptRoot;
        readonly Dictionary<string, Transform> placeRoots = new Dictionary<string, Transform>();
        float doorCooldown; string lastZone = "";
        Vector3 camVelocity; bool cameraSnapped;
        /// 0..1: the camera pushes into the bay screen (the hand-off to a match).
        float dive;
        public IEnumerator Dive(float seconds = .6f)
        {
            for (float t = 0; t < seconds; t += Time.unscaledDeltaTime) { dive = t / seconds; SetFade(Mathf.InverseLerp(.55f, 1f, dive) * .85f); yield return null; }
            dive = 1; SetFade(.85f);
        }

        public static HubWorld Find() => Instance ? Instance : FindFirstObjectByType<HubWorld>();

        /// Set by the session before the scene loads, so the right hero is built the first time.
        public static bool StartFemale;

        void Awake()
        {
            Instance = this;
            Build(StartFemale);
        }
        void OnDestroy() { if (Instance == this) Instance = null; }

        /// Builds everything. `female` picks the hero; the look is applied afterwards (ApplyLook) by the session.
        public void Build(bool female)
        {
            if (Initialized) return;
            world = new GameObject("Hub world").transform; world.SetParent(transform, false);
            foreach (var place in HubLayout.Places)
            {
                var root = new GameObject(place.title).transform; root.SetParent(world, false); placeRoots[place.id] = root;
                if (place.outdoor) HubArt.BuildPlaza(root); else HubRooms.Room(root, place);
            }
            // the plaza's doorways are part of its buildings and the PLAY hall's arches part of the hall; each room's way out is a lit mat
            foreach (var d in HubLayout.Doors) if (d.place != HubLayout.Plaza && Vector3.Dot(d.inward, Vector3.back) > .7f) HubRooms.Exit(placeRoots[d.place], d);
            foreach (var s in HubLayout.Spots) HubRooms.Spot(placeRoots[s.place], s);
            // the static greybox draws in a handful of batches
            StaticBatchingUtility.Combine(world.gameObject);
            BuildCamera();
            HubArt.Lighting(transform, Camera);
            BuildOverlay();
            Player = HubPlayer.Spawn(transform, female, HubLayout.Spawn, 0);
            youTag = HubArt.NameTag(Player.transform, "You", HubArt.RoyalDeep); youTag.localPosition = Vector3.up * 2.2f;
            SetLayer(transform);
            Prewarm();
            PrewarmHeroes(female);
            SnapCamera();
            Initialized = true;
        }

        // ------------------------------------------------------------------ look and body
        /// The locker's look on the plaza hero (PLAN §2 Locker: changes show live). Hex strings without '#', "" = the kit's own colour.
        public void ApplyLook(bool female, string skinHex, int skinIndex, string shirt, string shorts, string shoes, string racket)
        {
            if (!Player) return;
            Player.SetHero(female);
            var look = Player.look; if (!look) return;
            var skin = !string.IsNullOrEmpty(skinHex) ? HeroKit.Hex(skinHex) : HeroKit.Hex(HeroKit.SkinHex[Mathf.Clamp(skinIndex, 0, 5)]);
            look.SetSkin(skin);
            look.SetKit(shirt, shorts, shoes);
            look.SetRacketColour(!string.IsNullOrEmpty(racket) ? HeroKit.Hex(racket) : new Color(0, 0, 0, 0));
        }

        // ------------------------------------------------------------------ per frame
        void LateUpdate()
        {
            if (!Initialized) return;
            doorCooldown -= Time.unscaledDeltaTime;
            if (!Transitioning && Station == null) { CheckDoors(); UpdateZone(); }
            FollowCamera(Time.unscaledDeltaTime);
            if (overlay && Camera) overlay.targetDisplay = Camera.targetDisplay;
            if (promptRoot && Camera) promptRoot.rotation = Quaternion.LookRotation(promptRoot.position - Camera.transform.position, Vector3.up);
            if (youTag && Camera) { youTag.rotation = Camera.transform.rotation; youTag.gameObject.SetActive(Station == null && Place == HubLayout.Plaza); }
            PulseZoneRing();
        }

        void CheckDoors()
        {
            if (doorCooldown > 0 || !Player) return;
            var p = Player.transform.position; var v = Player.Velocity; v.y = 0;
            foreach (var d in HubLayout.Doors)
            {
                if (d.place != Place) continue;
                var local = p - d.position; local.y = 0;
                float along = Vector3.Dot(local, d.inward);
                float lateral = Mathf.Abs(Vector3.Dot(local, Vector3.Cross(Vector3.up, d.inward)));
                if (along < -.3f || along > 1f || lateral > d.width / 2) continue;
                if (Vector3.Dot(v, d.inward) < .35f) continue;
                if (d.locked) continue;
                if (HubLayout.Destination(d, out var to)) { StartCoroutine(WalkThrough(d, to)); return; }
            }
        }

        IEnumerator WalkThrough(HubLayout.Door from, HubLayout.Door to)
        {
            Transitioning = true; float started = Time.realtimeSinceStartup, worst = 0;
            Player.locked = true; Player.Autopilot = from.inward; Player.AutopilotSpeed = Mathf.Max(2.4f, Player.Speed);
            float t = 0; bool moved = false;
            while (t < DoorSeconds)
            {
                yield return null;
                float dt = Time.unscaledDeltaTime; t += dt; if (t > Time.unscaledDeltaTime) worst = Mathf.Max(worst, dt);
                // into the doorway (0 .. .14), black (.14 .. .22), out the other side (.22 .. .40)
                float a = t < .14f ? Mathf.InverseLerp(.06f, .14f, t) : t < .22f ? 1 : 1 - Mathf.InverseLerp(.22f, DoorSeconds, t);
                SetFade(a);
                if (!moved && t >= .18f)
                {
                    moved = true;
                    Player.Autopilot = -to.inward; Player.AutopilotSpeed = 1.2f;
                    Player.Teleport(to.Arrival, Mathf.Atan2(-to.inward.x, -to.inward.z) * Mathf.Rad2Deg);
                    SetPlace(to.place); SnapCamera();
                }
            }
            SetFade(0); Player.Autopilot = null; Player.locked = false; Transitioning = false; doorCooldown = .6f;
            DoorLog.Add((from.id, Time.realtimeSinceStartup - started, worst));
        }

        /// Quick travel (the phone's ☰ menu): a place id or a station / bay id. Fades out, moves, fades in (same 0.4 s as a door).
        public bool GoTo(string target, bool use = false)
        {
            if (Transitioning || !HubLayout.QuickTravel(target, out var pos, out var facing, out var place)) return false;
            LeaveStation();
            StartCoroutine(Travel(pos, facing, place, use ? HubLayout.SpotById(target) : null)); return true;
        }
        IEnumerator Travel(Vector3 pos, Vector3 facing, string place, HubLayout.Spot use = null)
        {
            Transitioning = true; Player.locked = true; float t = 0; bool moved = false;
            while (t < DoorSeconds)
            {
                yield return null; t += Time.unscaledDeltaTime;
                SetFade(t < .14f ? t / .14f : t < .22f ? 1 : 1 - Mathf.InverseLerp(.22f, DoorSeconds, t));
                if (!moved && t >= .16f) { moved = true; Player.Teleport(pos, Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg); SetPlace(place); SnapCamera(); }
            }
            SetFade(0); Player.locked = false; Transitioning = false; doorCooldown = .4f;
            if (use != null) { UpdateZone(); if (ZoneSpot == use) Interact(); }
        }

        void SetPlace(string id)
        {
            if (Place == id) return;
            Place = id; PlaceChanged?.Invoke(id);
        }

        void UpdateZone()
        {
            HubLayout.Door bestDoor = null; HubLayout.Spot bestSpot = null; float best = float.MaxValue;
            var p = Player.transform.position;
            foreach (var s in HubLayout.Spots)
            {
                if (s.place != Place) continue;
                float d = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(s.position.x, s.position.z));
                if (d <= s.radius && d < best) { best = d; bestSpot = s; }
            }
            if (bestSpot == null)
                foreach (var d in HubLayout.Doors)
                {
                    if (d.place != Place) continue;
                    float dist = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(d.position.x, d.position.z));
                    if (dist <= 4f && dist < best) { best = dist; bestDoor = d; }
                }
            ZoneDoor = bestDoor; ZoneSpot = bestSpot;
            string id = ZoneId;
            if (prompt)
            {
                // floating prompts only for the plaza's doors: in the rooms the spot's floor ring lights up (and the phone says what it is),
                // and the exits are marked on the floor
                bool exit = bestDoor != null && Vector3.Dot(bestDoor.inward, Vector3.back) > .7f;
                bool show = bestSpot == null && bestDoor != null && !exit && Place == HubLayout.Plaza;
                promptRoot.gameObject.SetActive(show);
                if (show)
                {
                    var at = bestSpot != null ? bestSpot.position + Vector3.up * 2.3f : bestDoor.position + Vector3.up * 3.6f - bestDoor.inward * .6f;
                    promptRoot.position = at;
                    string text = bestSpot != null ? "A  " + bestSpot.label : bestDoor.locked ? bestDoor.label : bestDoor.label + "  ▸";
                    if (prompt.text != text)
                    {
                        prompt.text = text;
                        var rt = (RectTransform)prompt.transform.parent;
                        rt.sizeDelta = new Vector2((text.Length * PromptHeight * .55f + PromptHeight * 1.4f) * 100, rt.sizeDelta.y);
                    }
                }
            }
            if (id != lastZone) { lastZone = id; ZoneChanged?.Invoke(id); }
        }

        /// The station you are using (A pressed): the TV frames your hero, the phone shows its panel. Null while walking.
        public HubLayout.Spot Station { get; private set; }

        /// The phone's A button: use the station or bay you stand at.
        public bool Interact()
        {
            if (Transitioning || Station != null || ZoneSpot == null) return false;
            EnterStation(ZoneSpot); Interacted?.Invoke(ZoneSpot.id); return true;
        }
        void EnterStation(HubLayout.Spot spot)
        {
            Station = spot; Player.SetStick(0, 0, 0); Player.locked = true;
            if (promptRoot) promptRoot.gameObject.SetActive(false);
            if (spot.kind == HubLayout.Kind.Bay) { StartCoroutine(SitDown(spot)); return; }
            // turn to face the room (the camera), like stepping in front of a mirror
            Player.Teleport(new Vector3(spot.position.x, Player.transform.position.y, spot.position.z), Mathf.Atan2(-spot.facing.x, -spot.facing.z) * Mathf.Rad2Deg);
        }
        /// Where a bay's bench seats you (the first seat; party seats come in phase 4).
        public static Vector3 SeatOf(HubLayout.Spot bay) => bay.position - bay.facing * .9f;
        /// Seat n on a bay's bench: you in the middle, friends either side (party, phase 4).
        public static Vector3 SeatOf(HubLayout.Spot bay, int index)
        {
            float[] offsets = { 0, .72f, -.72f, 1.44f };
            return SeatOf(bay) + Vector3.Cross(Vector3.up, bay.facing) * offsets[Mathf.Clamp(index, 0, offsets.Length - 1)];
        }
        /// Seated in a bay (phase 3): the match for this bay loads while you sit here.
        public bool SeatedInBay => Station != null && Station.kind == HubLayout.Kind.Bay && Player && Player.hero && Player.hero.Seated;
        IEnumerator SitDown(HubLayout.Spot bay)
        {
            var from = Player.transform.position; var to = SeatOf(bay); to.y = from.y;
            float yaw = Mathf.Atan2(bay.facing.x, bay.facing.z) * Mathf.Rad2Deg;
            Player.Teleport(from, yaw); if (Player.hero) Player.hero.Seated = true;
            for (float t = 0; t < .45f; t += Time.unscaledDeltaTime)
            {
                Player.Teleport(Vector3.Lerp(from, to, Mathf.SmoothStep(0, 1, t / .45f)), yaw); if (Player.hero) Player.hero.Seated = true;
                yield return null;
            }
            Player.Teleport(to, yaw); if (Player.hero) Player.hero.Seated = true;
        }
        /// Sit straight down on a bay's bench (returning from a match to the same bench: RETURN).
        public bool SitAt(string bayId)
        {
            var bay = HubLayout.SpotById(bayId); if (bay == null || bay.kind != HubLayout.Kind.Bay) return false;
            Station = bay; Player.SetStick(0, 0, 0); Player.locked = true; SetPlace(bay.place);
            var seat = SeatOf(bay); Player.Teleport(seat, Mathf.Atan2(bay.facing.x, bay.facing.z) * Mathf.Rad2Deg);
            if (Player.hero) { Player.hero.Seated = true; Player.hero.SnapSeat(); }
            SnapCamera(); return true;
        }
        // ---- the bay's loading ring (PLAN §3: "a floor ring that fills while the game loads")
        Mesh fillMesh; GameObject fillRing; float fillShown = -1; string fillBay;
        public float BayProgress => fillShown;
        /// 0..1 fills the ring of the bay you sit in; -1 hides it.
        public void BayLoading(float progress)
        {
            if (progress < 0 || Station == null || Station.kind != HubLayout.Kind.Bay) { if (fillRing) fillRing.SetActive(false); fillShown = -1; return; }
            if (!fillRing)
            {
                fillMesh = new Mesh { name = "Bay loading ring" }; fillMesh.MarkDynamic();
                fillRing = new GameObject("Bay loading ring"); fillRing.layer = Layer; fillRing.transform.SetParent(transform, false);
                fillRing.AddComponent<MeshFilter>().sharedMesh = fillMesh;
                var r = fillRing.AddComponent<MeshRenderer>(); r.sharedMaterial = HubArt.Glow(new Color(2.6f, 2.2f, .9f)); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            if (fillBay != Station.id) { fillBay = Station.id; fillRing.transform.position = Station.position + Station.facing * .1f + Vector3.up * .05f; fillShown = -2; }
            fillRing.SetActive(true);
            progress = Mathf.Clamp01(progress);
            if (Mathf.Abs(progress - fillShown) < .004f) return;
            fillShown = progress;
            const int seg = 96; int n = Mathf.Max(1, Mathf.RoundToInt(seg * progress));
            var v = new Vector3[(n + 1) * 2]; var t = new int[n * 6];
            for (int i = 0; i <= n; i++)
            {
                float a = i * Mathf.PI * 2 / seg; var d = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                v[i * 2] = d * 1.24f; v[i * 2 + 1] = d * 1.46f;
            }
            for (int i = 0; i < n; i++) { int b = i * 2; t[i * 6] = b; t[i * 6 + 1] = b + 1; t[i * 6 + 2] = b + 2; t[i * 6 + 3] = b + 2; t[i * 6 + 4] = b + 1; t[i * 6 + 5] = b + 3; }
            fillMesh.Clear(); fillMesh.vertices = v; fillMesh.triangles = t; fillMesh.RecalculateNormals(); fillMesh.RecalculateBounds();
        }

        /// The phone's B (or its panel's Done): back to walking (standing up first from a bench).
        public bool LeaveStation()
        {
            if (Station == null) return false;
            var was = Station; BayLoading(-1); Station = null; Player.locked = false; dive = 0; SetFade(0);
            if (Player.hero && Player.hero.Seated) { Player.hero.Seated = false; Player.Teleport(was.position - was.facing * .2f, Mathf.Atan2(was.facing.x, was.facing.z) * Mathf.Rad2Deg); }
            return true;
        }

        /// A match always has both bodies on court (you and your rival): their one-time mesh preparation (seconds for a body the
        /// first time) and their clips are done here, while the plaza boots behind the TV menu, not while you sit in a bay.
        static readonly List<UnityEngine.Object> keepAlive = new List<UnityEngine.Object>();
        void PrewarmHeroes(bool female)
        {
            var prefab = TennisCustomization.HeroBase(!female); if (!prefab) return;
            var tmp = Instantiate(prefab, new Vector3(0, -5000, 0), Quaternion.identity); tmp.name = "Hero prewarm";
            var driver = tmp.GetComponent<HeroTennisDriver>();
            if (driver) { driver.enabled = false; driver.ResolvePerformanceClips(); foreach (var slot in driver.slots) if (slot.clip && !keepAlive.Contains(slot.clip)) keepAlive.Add(slot.clip); }
            Destroy(tmp);
        }

        /// Renders every place once, off screen, while the plaza boots: the first look at a room must not pay for uploading its meshes,
        /// textures and pipeline states (that hitch is what the DOORS gate measures).
        void Prewarm()
        {
            if (!Camera) return;
            var rt = RenderTexture.GetTemporary(320, 180, 24); var keep = Camera.targetTexture; var pos = Camera.transform.position; var rot = Camera.transform.rotation; float far = Camera.farClipPlane;
            Camera.targetTexture = rt;
            foreach (var place in HubLayout.Places)
            {
                var o = place.origin;
                foreach (var view in place.outdoor ? new[] { new Vector3(0, 5, -17), new Vector3(0, 30, -30) } : new[] { new Vector3(0, 3.4f, -place.size.y / 2 - 3), new Vector3(0, 2.5f, 0) })
                {
                    Camera.transform.SetPositionAndRotation(o + view, Quaternion.LookRotation(o + new Vector3(0, 1, place.outdoor ? 6 : 2) - (o + view)));
                    Camera.farClipPlane = place.outdoor ? 1300 : 60; Camera.Render();
                }
            }
            Camera.targetTexture = keep; Camera.transform.SetPositionAndRotation(pos, rot); Camera.farClipPlane = far;
            RenderTexture.ReleaseTemporary(rt);
        }

        // ------------------------------------------------------------------ camera
        void BuildCamera()
        {
            var go = new GameObject("Hub camera"); go.transform.SetParent(transform, false);
            Camera = go.AddComponent<Camera>(); Camera.fieldOfView = 48; Camera.nearClipPlane = .2f;
            Camera.clearFlags = CameraClearFlags.SolidColor; Camera.backgroundColor = new Color(.98f, .72f, .58f);
            go.AddComponent<AudioListener>();
            // NOT MainCamera: a match loading behind the plaza looks its own camera up with Camera.main
            Camera.cullingMask = 1 << Layer;
        }
        (float distance, float height, float lookHeight, float far) CameraFor(string place) =>
            HubLayout.PlaceOf(place).outdoor ? (9.8f, 5.0f, 1.9f, 1300f) : (6.6f, 3.4f, 1.25f, 60f);
        Vector3 CameraTarget()
        {
            var c = CameraFor(Place); var p = Player.transform.position;
            var pos = p + new Vector3(0, c.height, -c.distance);
            var place = HubLayout.PlaceOf(Place);
            if (!place.outdoor)
            {
                // rooms are cut away on the south side (low front wall): the camera may sit behind it, never beside the side walls
                float hx = place.size.x / 2 - .6f; pos.x = Mathf.Clamp(pos.x, place.origin.x - hx, place.origin.x + hx);
                pos.z = Mathf.Max(pos.z, place.origin.z - place.size.y / 2 - 4.5f);
            }
            return pos;
        }
        void FollowCamera(float dt)
        {
            if (!Camera || !Player) return;
            var c = CameraFor(Place);
            Camera.farClipPlane = c.far; Camera.fieldOfView = HubLayout.PlaceOf(Place).outdoor ? 52 : 48;
            if (Station != null && Station.kind == HubLayout.Kind.Bay)
            {
                // seated in a bay: from behind and above the bench, the bay screen ahead (the camera dives into it at go)
                var scr = Station.position + Station.facing * 1.75f + Vector3.up * 1.95f;
                var want = SeatOf(Station) - Station.facing * 3.6f + Vector3.up * 2.3f;
                if (dive > 0) want = Vector3.Lerp(want, scr - Station.facing * .55f, Mathf.SmoothStep(0, 1, dive));
                Camera.transform.position = cameraSnapped ? want : Vector3.SmoothDamp(Camera.transform.position, want, ref camVelocity, dive > 0 ? .05f : .25f, Mathf.Infinity, dt);
                Camera.transform.rotation = Quaternion.Slerp(Camera.transform.rotation, Quaternion.LookRotation(scr - Vector3.up * .25f - Camera.transform.position, Vector3.up), cameraSnapped ? 1 : 1 - Mathf.Exp(-dt / .08f));
                cameraSnapped = false; return;
            }
            if (Station != null)
            {
                // station framing: in front of the hero, chest high, the whole outfit in shot
                var hero = Player.transform.position; var toward = -Station.facing;
                var want = hero + toward * 2.7f + Vector3.up * 1.45f + Vector3.Cross(Vector3.up, toward) * .35f;
                Camera.transform.position = Vector3.SmoothDamp(Camera.transform.position, want, ref camVelocity, .22f, Mathf.Infinity, dt);
                Camera.transform.rotation = Quaternion.Slerp(Camera.transform.rotation, Quaternion.LookRotation(hero + Vector3.up * 1.0f - Camera.transform.position, Vector3.up), 1 - Mathf.Exp(-dt / .08f));
                return;
            }
            var target = CameraTarget();
            Camera.transform.position = cameraSnapped ? target : Vector3.SmoothDamp(Camera.transform.position, target, ref camVelocity, .14f, Mathf.Infinity, dt);
            cameraSnapped = false;
            var look = Player.transform.position + Vector3.up * c.lookHeight + Vector3.forward * (HubLayout.PlaceOf(Place).outdoor ? 10f : 1.6f);
            Camera.transform.rotation = Quaternion.LookRotation(look - Camera.transform.position, Vector3.up);
            Player.cameraYaw = 0;
        }
        void SnapCamera() { cameraSnapped = true; camVelocity = Vector3.zero; if (Camera && Player) FollowCamera(0); }

        // ------------------------------------------------------------------ overlay: fade + prompt
        void BuildOverlay()
        {
            var go = new GameObject("Hub overlay"); go.transform.SetParent(transform, false);
            overlay = go.AddComponent<Canvas>(); overlay.renderMode = RenderMode.ScreenSpaceOverlay; overlay.sortingOrder = 30000;
            fade = new GameObject("Fade").AddComponent<Image>(); fade.transform.SetParent(go.transform, false);
            fade.color = new Color(0, 0, 0, 0); fade.raycastTarget = false;
            var rt = fade.rectTransform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            promptRoot = new GameObject("Hub prompt").transform; promptRoot.SetParent(transform, false);
            prompt = HubKit.Sign("Prompt", promptRoot, "", Vector3.zero, Vector3.back, PromptHeight, Color.white, new Color(.16f, .22f, .42f, .92f), 2f);
            prompt.transform.parent.localRotation = Quaternion.identity;
            promptRoot.gameObject.SetActive(false);
        }
        const float PromptHeight = .3f;
        readonly Dictionary<string, Transform> spotRings = new Dictionary<string, Transform>();
        Transform litRing;
        /// The ring of the station / bay you stand at breathes, so it is obvious what A will use.
        void PulseZoneRing()
        {
            Transform want = null;
            if (ZoneSpot != null && Station == null && !spotRings.TryGetValue(ZoneSpot.id, out want))
            {
                var found = world ? world.Find(HubLayout.PlaceOf(ZoneSpot.place).title + "/Spot ring " + ZoneSpot.id) : null;
                spotRings[ZoneSpot.id] = want = found;
            }
            if (litRing && litRing != want) litRing.localScale = Vector3.one;
            litRing = want;
            if (litRing) litRing.localScale = Vector3.one * (1.12f + .08f * Mathf.Sin(Time.unscaledTime * 6f));
        }
        void SetFade(float a) { if (fade) fade.color = new Color(.02f, .02f, .05f, Mathf.Clamp01(a)); }
        public float FadeAlpha => fade ? fade.color.a : 0;

        // ------------------------------------------------------------------ greybox



#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// Editor play: WASD / arrows walk, Shift runs, hold Shift 1 s to sprint, E is A, Q is B.
        void Update()
        {
            if (GolfArcade.Game.NativeSportsSession.Active || !Player || !Application.isEditor) return;
#if ENABLE_LEGACY_INPUT_MANAGER
            var v = new Vector2((Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1 : 0) - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1 : 0),
                                (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1 : 0) - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1 : 0));
            if (EditorDriven) return;
            Player.SetStick(v.x, v.y, v == Vector2.zero ? 0 : Input.GetKey(KeyCode.LeftShift) ? 1 : .5f);
            if (Input.GetKeyDown(KeyCode.E)) Interact();
#endif
        }
#endif
        /// Tests and proof tools drive the stick themselves; the editor keyboard stays out of their way.
        public static bool EditorDriven;
    }
}
