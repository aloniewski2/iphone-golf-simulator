using System;
using System.Collections.Generic;
using System.Linq;
using GolfArcade.Tennis;
using UnityEngine;

namespace GolfArcade.Hub
{
    /// One friend's state in your plaza (PLAN_MenuHub_WalkableWorld §4), relayed by the phones at ~10 Hz (hubPresence) with their
    /// lobby look merged in. `t` is the sender's clock; interpolation uses the arrival time here.
    [Serializable] public sealed class RemoteState
    {
        public string id, name, place, bay, emote, skinHex, shirt, shorts, accent;
        public bool female; public int colour;
        public float x, y, z, yaw, speed;
    }

    /// Friends in your plaza: each is the match hero in their own look, a name tag in their party colour, moved between the
    /// last two snapshots 120 ms in the past (smooth at 10 Hz with jitter), seated beside you on a shared bench, emotes mirrored.
    /// Also Call party: a beacon on the bay and an arrow at your feet that points the way there (next door, then the bay).
    public sealed class HubRemotes : MonoBehaviour
    {
        public const float Delay = .12f;
        public static readonly Color[] PartyColours = { new Color(.36f, .55f, 1f), new Color(.48f, .85f, .29f), new Color(1f, .39f, .72f), new Color(.66f, .44f, 1f) };

        sealed class Snap { public double at; public RemoteState s; }
        sealed class Remote
        {
            public string id; public GameObject root; public HubHeroAnimator hero; public MatchHeroLook look; public Transform tag;
            public readonly List<Snap> snaps = new List<Snap>(); public string lookKey, emote; public bool female; public string place;
            public Vector3 shown; public float yaw;
        }
        readonly Dictionary<string, Remote> remotes = new Dictionary<string, Remote>();
        HubWorld world;
        public int Count => remotes.Count;
        /// Diagnostics for SYNC: where a friend is drawn now.
        public bool TryGetShown(string id, out Vector3 at) { at = default; if (!remotes.TryGetValue(id, out var r)) return false; at = r.shown; return true; }
        /// Where a friend last said they are (their place and position), for joining them.
        public bool TryGetLast(string id, out string place, out Vector3 at)
        {
            place = null; at = default;
            if (!remotes.TryGetValue(id, out var r) || r.snaps.Count == 0) return false;
            var s = r.snaps[r.snaps.Count - 1].s; place = s.place; at = new Vector3(s.x, s.y, s.z); return true;
        }

        public static HubRemotes For(HubWorld w) { var r = w.GetComponent<HubRemotes>(); if (!r) { r = w.gameObject.AddComponent<HubRemotes>(); r.world = w; } return r; }

        public void Receive(RemoteState s)
        {
            if (s == null || string.IsNullOrEmpty(s.id) || float.IsNaN(s.x) || float.IsNaN(s.z)) return;
            if (!remotes.TryGetValue(s.id, out var r)) remotes[s.id] = r = new Remote { id = s.id };
            string key = $"{s.female}|{s.skinHex}|{s.shirt}|{s.shorts}|{s.accent}|{s.name}|{s.colour}";
            if (!r.root || r.female != s.female) Spawn(r, s);
            if (r.lookKey != key) { Dress(r, s); r.lookKey = key; }
            double now = Time.unscaledTimeAsDouble;
            if (r.snaps.Count > 0 && r.snaps[r.snaps.Count - 1].s.place != s.place) r.snaps.Clear();   // a door: no sliding across the map
            r.snaps.Add(new Snap { at = now, s = s });
            while (r.snaps.Count > 12) r.snaps.RemoveAt(0);
            if (!string.IsNullOrEmpty(s.emote) && s.emote != r.emote && r.hero) r.hero.Play(s.emote);
            r.emote = s.emote;
        }

        /// The party's member list: anyone not in it walks off (is removed).
        public void Roster(IEnumerable<string> ids)
        {
            var keep = new HashSet<string>(ids);
            foreach (var id in remotes.Keys.Where(k => !keep.Contains(k)).ToList()) { if (remotes[id].root) Destroy(remotes[id].root); remotes.Remove(id); }
        }

        void Spawn(Remote r, RemoteState s)
        {
            if (r.root) Destroy(r.root);
            r.female = s.female;
            r.root = new GameObject("Friend " + s.name); r.root.transform.SetParent(world.transform, false);
            var prefab = TennisCustomization.HeroBase(s.female); if (!prefab) return;
            var go = Instantiate(prefab, r.root.transform, false); go.name = "Friend hero";
            r.look = go.GetComponent<MatchHeroLook>();
            if (r.look) HeroAccessories.For(r.look).SetCarry(true);   // ACCESSORIES: friends in the plaza carry the golf bag too
            if (r.look && r.look.racketGrip) foreach (var rend in r.look.racketGrip.GetComponentsInChildren<Renderer>(true)) rend.enabled = false;
            r.hero = HubHeroAnimator.Attach(go);
            r.root.transform.position = new Vector3(s.x, s.y, s.z); r.shown = r.root.transform.position;
            HubWorld.SetLayer(r.root.transform);
        }

        void Dress(Remote r, RemoteState s)
        {
            if (r.look)
            {
                r.look.SetSkin(!string.IsNullOrEmpty(s.skinHex) ? HeroKit.Hex(s.skinHex) : HeroKit.Hex(HeroKit.SkinHex[2]));
                r.look.SetKit(s.shirt, s.shorts, s.accent);
            }
            if (r.tag) Destroy(r.tag.gameObject);
            r.tag = HubArt.NameTag(r.root.transform, string.IsNullOrEmpty(s.name) ? "Friend" : s.name, PartyColours[Mathf.Abs(s.colour) % PartyColours.Length] * .85f);
            r.tag.localPosition = Vector3.up * 2.2f; HubWorld.SetLayer(r.tag);
        }

        void LateUpdate()
        {
            if (!world || !world.Camera) return;
            double renderAt = Time.unscaledTimeAsDouble - Delay; float dt = Time.unscaledDeltaTime;
            // who sits in which bay (seats after yours, by id)
            var seated = remotes.Values.Where(r => r.snaps.Count > 0 && !string.IsNullOrEmpty(r.snaps[r.snaps.Count - 1].s.bay))
                                       .GroupBy(r => r.snaps[r.snaps.Count - 1].s.bay).ToDictionary(g => g.Key, g => g.OrderBy(r => r.id).ToList());
            foreach (var r in remotes.Values)
            {
                if (!r.root || r.snaps.Count == 0) continue;
                var last = r.snaps[r.snaps.Count - 1].s;
                Vector3 pos; float yaw, speed;
                if (!string.IsNullOrEmpty(last.bay) && HubLayout.SpotById(last.bay) is HubLayout.Spot bay)
                {
                    int index = seated.TryGetValue(last.bay, out var list) ? list.IndexOf(r) + 1 : 1;
                    pos = HubWorld.SeatOf(bay, index); yaw = Mathf.Atan2(bay.facing.x, bay.facing.z) * Mathf.Rad2Deg; speed = 0;
                    if (r.hero) r.hero.Seated = true;
                }
                else
                {
                    if (r.hero) r.hero.Seated = false;
                    // the two snapshots around renderAt; past the newest, hold it (never guess ahead by more than a frame or two)
                    int i = r.snaps.FindLastIndex(n => n.at <= renderAt);
                    if (i < 0) { var f = r.snaps[0].s; pos = new Vector3(f.x, f.y, f.z); yaw = f.yaw; speed = f.speed; }
                    else if (i >= r.snaps.Count - 1) { pos = new Vector3(last.x, last.y, last.z); yaw = last.yaw; speed = last.speed; }
                    else
                    {
                        var a = r.snaps[i]; var b = r.snaps[i + 1];
                        float k = (float)((renderAt - a.at) / Math.Max(1e-4, b.at - a.at));
                        pos = Vector3.Lerp(new Vector3(a.s.x, a.s.y, a.s.z), new Vector3(b.s.x, b.s.y, b.s.z), k);
                        yaw = Mathf.LerpAngle(a.s.yaw, b.s.yaw, k); speed = Mathf.Lerp(a.s.speed, b.s.speed, k);
                    }
                }
                if (r.place != last.place) { r.place = last.place; r.shown = pos; }
                r.shown = Vector3.Distance(r.shown, pos) > 3 ? pos : Vector3.Lerp(r.shown, pos, 1 - Mathf.Exp(-dt / .05f));
                r.root.transform.position = r.shown;
                r.yaw = Mathf.MoveTowardsAngle(r.yaw, yaw, 900 * dt);
                if (r.hero) { r.hero.transform.localRotation = Quaternion.Euler(0, r.yaw, 0); r.hero.Tick(speed, dt); }
                if (r.tag) { r.tag.rotation = world.Camera.transform.rotation; r.tag.localPosition = Vector3.up * (r.hero && r.hero.Seated ? 1.62f : 2.2f); r.tag.gameObject.SetActive(last.place == world.Place); }
                // friends in another room are not drawn here (their room is off-stage; keep the pose costs down)
                r.root.SetActive(last.place == world.Place || Vector3.Distance(r.shown, world.Camera.transform.position) < 80);
            }
            UpdateCall(dt);
        }

        // ---- Call party
        string callBay; float callUntil; Transform beacon, beaconGem, beaconRing, arrow;
        public string CallBay => callUntil > Time.unscaledTime ? callBay : null;
        public void Call(string bayId, float seconds = 12f)
        {
            var bay = HubLayout.SpotById(bayId); if (bay == null) { callBay = null; callUntil = 0; return; }
            callBay = bayId; callUntil = Time.unscaledTime + seconds;
            if (!beacon)
            {
                // a gem floating over the next place to go (the door out, then the bay) with a ring on the floor under it
                beacon = new GameObject("Party beacon").transform; beacon.SetParent(world.transform, false);
                // aqua: it has to stand out against the warm doorways and the evening sky
                beaconGem = Part("Party beacon gem", beacon, GemMesh(), new Color(.35f, 2.0f, 2.5f));
                beaconRing = Part("Party beacon ring", beacon, HubKit.Ring(.62f, .8f, 48), new Color(.3f, 1.7f, 2.1f));
                arrow = Part("Party arrow", world.transform, ArrowMesh(), new Color(.35f, 2.0f, 2.5f));
                HubWorld.SetLayer(beacon); HubWorld.SetLayer(arrow);
            }
        }
        static Transform Part(string name, Transform parent, Mesh mesh, Color glow)
        {
            var t = new GameObject(name).transform; t.SetParent(parent, false);
            t.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = t.gameObject.AddComponent<MeshRenderer>(); mr.sharedMaterial = HubArt.Glow(glow); mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return t;
        }
        /// Where to walk next toward a place: the door out of here, or the spot itself once you are in its room.
        public static Vector3 Waypoint(string fromPlace, HubLayout.Spot target)
        {
            if (fromPlace == target.place) return target.position;
            string via = target.place == HubLayout.TennisRoom || target.place == HubLayout.GolfRoom ? HubLayout.PlayHall : HubLayout.Plaza;
            if (fromPlace == HubLayout.PlayHall && via == HubLayout.PlayHall) return HubLayout.Doors.First(d => d.place == HubLayout.PlayHall && d.to == "door-" + target.place + "-exit").position;
            if (fromPlace == HubLayout.Plaza) return HubLayout.Doors.First(d => d.place == HubLayout.Plaza && d.to == "door-" + (via == HubLayout.PlayHall ? HubLayout.PlayHall : target.place) + "-exit").position;
            return HubLayout.Doors.First(d => d.place == fromPlace && d.id.EndsWith("-exit")).position;
        }
        void UpdateCall(float dt)
        {
            // arrived: you are at the called bay, so the call is answered
            if (callUntil > Time.unscaledTime && world.Station != null && world.Station.id == callBay) callUntil = 0;
            bool on = callUntil > Time.unscaledTime && world.Player;
            if (beacon) beacon.gameObject.SetActive(on);
            if (arrow) arrow.gameObject.SetActive(on);
            if (!on || !arrow) return;
            var target = HubLayout.SpotById(callBay);
            var next = Waypoint(world.Place, target); bool atBay = world.Place == target.place;
            float t = Time.unscaledTime;
            beacon.position = new Vector3(next.x, world.Player.transform.position.y, next.z);
            beaconRing.localPosition = Vector3.up * .03f; beaconRing.gameObject.SetActive(atBay);
            // at a door the gem floats out in front of it (inside the frame it would be hidden by the lintel)
            var outward = world.Player.transform.position - beacon.position; outward.y = 0;
            var lift = atBay ? Vector3.zero : (outward.sqrMagnitude > .01f ? outward.normalized * 1.2f : Vector3.zero);
            beaconGem.localPosition = lift + Vector3.up * ((atBay ? 2.5f : 2.9f) + .14f * Mathf.Sin(t * 2.6f));
            beaconGem.localRotation = Quaternion.Euler(0, t * 90f, 0);
            var p = world.Player.transform.position; var to = next - p; to.y = 0;
            if (to.sqrMagnitude < .01f) to = Vector3.forward;
            // over the hero's head: on the ground ahead it is hidden by the hero's own body from the follow camera
            arrow.position = p + Vector3.up * 2.35f + to.normalized * .25f;
            arrow.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);
            arrow.localScale = Vector3.one * (1.1f + .12f * Mathf.Sin(t * 7));
        }
        static Mesh gemMesh;
        /// An elongated octahedron with flat faces (each face its own vertices).
        static Mesh GemMesh()
        {
            if (gemMesh) return gemMesh;
            var top = new Vector3(0, .5f, 0); var bottom = new Vector3(0, -.5f, 0);
            var ring = Enumerable.Range(0, 4).Select(i => Quaternion.Euler(0, i * 90, 0) * new Vector3(.32f, 0, 0)).ToArray();
            var v = new List<Vector3>(); var tri = new List<int>();
            for (int i = 0; i < 4; i++)
            {
                var a = ring[i]; var b = ring[(i + 1) % 4];
                foreach (var face in new[] { new[] { top, b, a }, new[] { bottom, a, b } }) { tri.Add(v.Count); tri.Add(v.Count + 1); tri.Add(v.Count + 2); v.AddRange(face); }
            }
            gemMesh = new Mesh { name = "Party beacon gem" }; gemMesh.SetVertices(v); gemMesh.SetTriangles(tri, 0);
            // outward (convex, centred) normals first so FixWinding can turn every face out, then the true flat normals
            gemMesh.SetNormals(Enumerable.Range(0, v.Count).Select(i => (v[i / 3 * 3] + v[i / 3 * 3 + 1] + v[i / 3 * 3 + 2]).normalized).ToList());
            HubShapes.FixWinding(gemMesh); gemMesh.RecalculateNormals(); gemMesh.RecalculateBounds();
            return gemMesh;
        }
        static Mesh arrowMesh;
        static Mesh ArrowMesh()
        {
            if (arrowMesh) return arrowMesh;
            var v = new[] { new Vector3(0, 0, .55f), new Vector3(.42f, 0, 0), new Vector3(.16f, 0, 0), new Vector3(.16f, 0, -.45f), new Vector3(-.16f, 0, -.45f), new Vector3(-.16f, 0, 0), new Vector3(-.42f, 0, 0) };
            arrowMesh = new Mesh { name = "Party arrow" }; arrowMesh.vertices = v;
            arrowMesh.triangles = new[] { 0, 1, 6, 2, 3, 4, 2, 4, 5 };
            arrowMesh.normals = v.Select(_ => Vector3.up).ToArray(); HubShapes.FixWinding(arrowMesh); arrowMesh.RecalculateBounds();
            return arrowMesh;
        }
    }
}
