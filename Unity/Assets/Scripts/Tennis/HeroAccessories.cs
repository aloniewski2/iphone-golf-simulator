using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GolfArcade.Tennis
{
    /// The three accessory slots. Each holds at most one item; slots never touch each other or the body, head, clothes, rig or clips.
    public enum AccessorySlot { Headwear, Glove, BackCarry }

    /// ACCESSORIES (work/accessories): basic hero accessories as separate swappable items. An item is its own skinned piece (renderer `Acc_*`), bound BY NAME to
    /// the hero's own bones or to a socket this component creates under a bone (`Headwear` under Head, `Back_Bag` under UpperChest; `HairRoot` is reserved
    /// empty under Head for the hair that comes later), drawn with the kit V2 cloth look (TennisCloth + the piece's own N / M maps). Nothing in a prefab changes: the
    /// component is added by MatchHeroLook.OwnMaterials (the one place the game and every tool build a hero's look) and loads the per-sex mesh and data from
    /// Resources/Accessories/<Sex>/<item>/ (installed by Editor/AccessoriesInstall.cs from work/accessories/out).
    ///
    /// Slots and items:  Headwear = tennis_cap | golf_cap,  Glove = golf_glove (left hand; the mirrored right one for left-handed golfers),  BackCarry = golf_bag.
    /// Defaults (by kit): tennis kit -> tennis_cap; golf kit -> golf_cap + golf_glove. The bag is carried only where the game asks: SetCarry(true) in the plaza and
    /// between golf shots. ACC_DEFAULTS=0 (environment) turns the default items off for A/B captures; ACC_ITEMS=a,b,... replaces the default list.
    [DefaultExecutionOrder(100)]
    public sealed class HeroAccessories : MonoBehaviour
    {
        public const string SocketHeadwear = "Headwear", SocketHair = "HairRoot", SocketBack = "Back_Bag";

        public sealed class ItemDef
        {
            public readonly string id, name; public readonly AccessorySlot slot; public readonly string baseHex, trimHex;
            public ItemDef(string id, AccessorySlot slot, string name, string baseHex, string trimHex) { this.id = id; this.slot = slot; this.name = name; this.baseHex = baseHex; this.trimHex = trimHex; }
        }
        public static readonly ItemDef[] Catalog =
        {
            new ItemDef("tennis_cap", AccessorySlot.Headwear, "Tennis cap", "F3F2EE", "1F2B57"),
            new ItemDef("golf_cap", AccessorySlot.Headwear, "Golf cap", "1F2B57", "F3F2EE"),
            new ItemDef("golf_glove", AccessorySlot.Glove, "Golf glove", "F1F0EC", "1F2B57"),
            new ItemDef("golf_bag", AccessorySlot.BackCarry, "Golf carry bag", "F1F0EC", "1F2B57"),
        };
        public static ItemDef Def(string id) => Catalog.FirstOrDefault(d => d.id == id);

        [Serializable] sealed class PieceData { public string name, renderer, mesh, maps; public float metresPerUv; public string[] bones, roles; }
        [Serializable] sealed class ItemData { public string item, sex, slot, parentBone, socket; public float[] socketPosition, socketRotation; public PieceData[] pieces; }

        sealed class Equipped
        {
            public ItemDef def; public ItemData data; public List<SkinnedMeshRenderer> renderers = new List<SkinnedMeshRenderer>();
            public bool visible = true; public Color? baseColour, trimColour; public List<(Material m, bool metal)> materials = new List<(Material, bool)>();
        }

        public MatchHeroLook look;
        readonly Dictionary<AccessorySlot, Equipped> worn = new Dictionary<AccessorySlot, Equipped>();
        Dictionary<string, Transform> byName;
        bool leftHandedGolfer, carry;

        /// The component of a hero (added when missing).
        public static HeroAccessories For(MatchHeroLook look)
        {
            if (!look) return null;
            var a = look.GetComponent<HeroAccessories>();
            if (!a) a = look.gameObject.AddComponent<HeroAccessories>();
            a.look = look;
            return a;
        }

        /// Called by MatchHeroLook.OwnMaterials: the hero's default accessories for its kit.
        public static void Setup(MatchHeroLook look)
        {
            if (!look || !look.body) return;
            var a = For(look);
            if (Environment.GetEnvironmentVariable("ACC_DEFAULTS") == "0") return;
            string list = Environment.GetEnvironmentVariable("ACC_ITEMS");
            var items = !string.IsNullOrEmpty(list) ? list.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray()
                : look.golfKit ? new[] { "golf_cap", "golf_glove" } : new[] { "tennis_cap" };
            foreach (var id in items) a.Equip(id);
        }

        // ---------------------------------------------------------------- queries
        public string Item(AccessorySlot slot) => worn.TryGetValue(slot, out var e) ? e.def.id : null;
        public bool IsVisible(AccessorySlot slot) => worn.TryGetValue(slot, out var e) && e.visible;
        public bool Carrying => carry;
        public IEnumerable<SkinnedMeshRenderer> Renderers(AccessorySlot slot) => worn.TryGetValue(slot, out var e) ? e.renderers.Where(r => r) : Enumerable.Empty<SkinnedMeshRenderer>();
        public IEnumerable<SkinnedMeshRenderer> AllRenderers() => worn.Values.SelectMany(e => e.renderers).Where(r => r);

        // ---------------------------------------------------------------- slot operations
        public bool Equip(string itemId) { var d = Def(itemId); return d != null && Equip(d.slot, itemId); }

        /// Puts `itemId` in `slot` (replacing what is there). False when the item has no data for this hero's body.
        public bool Equip(AccessorySlot slot, string itemId)
        {
            var def = Def(itemId);
            if (def == null || def.slot != slot || !look || !look.body) return false;
            if (worn.TryGetValue(slot, out var cur) && cur.def.id == itemId && cur.renderers.Count > 0 && cur.renderers.All(r => r)) return true;
            Unequip(slot);
            string sex = look.female ? "Female" : "Male";
            var json = Resources.Load<TextAsset>($"Accessories/{sex}/{itemId}/item");
            if (!json) return false;
            var data = JsonUtility.FromJson<ItemData>(json.text);
            var e = new Equipped { def = def, data = data };
            if (!string.IsNullOrEmpty(data.socket)) PlaceSocket(data);
            foreach (var p in data.pieces)
            {
                var r = BuildPiece(e, p, sex);
                if (r) e.renderers.Add(r);
            }
            if (e.renderers.Count != data.pieces.Length) { foreach (var r in e.renderers) if (r) Kill(r.gameObject); return false; }
            worn[slot] = e;
            if (slot == AccessorySlot.Glove) RefreshGlove();
            Apply(e);
            return true;
        }

        public void Unequip(AccessorySlot slot)
        {
            if (!worn.TryGetValue(slot, out var e)) return;
            foreach (var r in e.renderers) if (r) { r.enabled = false; Kill(r.gameObject); }
            foreach (var (m, _) in e.materials) Kill(m);
            worn.Remove(slot);
            if (slot == AccessorySlot.Glove) RefreshGlove();
            ApplyCover();
        }

        public void SetVisible(AccessorySlot slot, bool on)
        {
            if (!worn.TryGetValue(slot, out var e)) return;
            e.visible = on; Apply(e);
        }

        /// Base and trim colour of the item in `slot` (sRGB picks, like the kit colours; null keeps the item's own).
        public void SetColours(AccessorySlot slot, Color? baseColour, Color? trimColour)
        {
            if (!worn.TryGetValue(slot, out var e)) return;
            e.baseColour = baseColour; e.trimColour = trimColour; Apply(e);
        }

        /// The golf bag on the back: true in the plaza and between golf shots, false while swinging / in matches.
        public void SetCarry(bool carried)
        {
            carry = carried;
            if (carried && !worn.ContainsKey(AccessorySlot.BackCarry)) Equip(AccessorySlot.BackCarry, "golf_bag");
            SetVisible(AccessorySlot.BackCarry, carried);
        }

        /// Golfers: the glove goes on the lead hand (left hand for a right-hander, so the mirrored piece for a left-hander).
        public void SetGolfHand(bool leftHanded)
        {
            leftHandedGolfer = leftHanded;
            if (worn.TryGetValue(AccessorySlot.Glove, out var e)) Apply(e);
            RefreshGlove();
        }

        /// The old kit gloves (Kit_Glove_L/R) stay off while an accessory glove is worn.
        public void RefreshGlove()
        {
            if (!look || look.kit == null) return;
            bool mine = worn.ContainsKey(AccessorySlot.Glove);
            foreach (var r in look.kit)
                if (r && (r.name == "Kit_Glove_L" || r.name == "Kit_Glove_R"))
                    r.enabled = !mine && r.name == (leftHandedGolfer ? "Kit_Glove_R" : "Kit_Glove_L");
        }

        // ---------------------------------------------------------------- building
        Transform Bone(string name)
        {
            if (byName == null || !byName.TryGetValue(name, out var t) || !t)
            {
                byName = new Dictionary<string, Transform>();
                foreach (var tr in look.GetComponentsInChildren<Transform>(true)) if (!byName.ContainsKey(tr.name)) byName[tr.name] = tr;
                byName.TryGetValue(name, out t);
            }
            return t;
        }

        void PlaceSocket(ItemData d)
        {
            var parent = Bone(d.parentBone);
            if (!parent) throw new InvalidOperationException($"accessory {d.item}: parent bone {d.parentBone} missing on {look.name}");
            var s = Bone(d.socket);
            if (!s || s.parent != parent)
            {
                var go = new GameObject(d.socket); s = go.transform; s.SetParent(parent, false);
                byName[d.socket] = s;
            }
            s.localPosition = new Vector3(d.socketPosition[0], d.socketPosition[1], d.socketPosition[2]);
            s.localRotation = new Quaternion(d.socketRotation[0], d.socketRotation[1], d.socketRotation[2], d.socketRotation[3]);
            s.localScale = Vector3.one;
            // the empty hair socket: hair that comes later is parented here and the cap squashes / hides it, never the other way round
            if (d.parentBone == "Head" && !Bone(SocketHair)) { var h = new GameObject(SocketHair); h.transform.SetParent(parent, false); byName[SocketHair] = h.transform; }
        }

        SkinnedMeshRenderer BuildPiece(Equipped e, PieceData p, string sex)
        {
            var mesh = Resources.Load<Mesh>($"Accessories/{sex}/{e.def.id}/{p.mesh}");
            if (!mesh) { Debug.LogWarning($"[Accessories] mesh Accessories/{sex}/{e.def.id}/{p.mesh} missing"); return null; }
            var body = look.body;
            var go = new GameObject(p.renderer);
            go.transform.SetParent(body.transform.parent, false);
            go.transform.localPosition = body.transform.localPosition; go.transform.localRotation = body.transform.localRotation; go.transform.localScale = body.transform.localScale;
            go.layer = body.gameObject.layer;
            var smr = go.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh;
            var bones = new Transform[p.bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                bones[i] = Bone(p.bones[i]);
                if (!bones[i]) { Debug.LogWarning($"[Accessories] {e.def.id}: bone {p.bones[i]} missing on {look.name}"); Kill(go); return null; }
            }
            smr.bones = bones; smr.rootBone = body.rootBone;
            smr.localBounds = body.localBounds; smr.updateWhenOffscreen = body.updateWhenOffscreen; smr.quality = SkinQuality.Bone4;
            smr.shadowCastingMode = body.shadowCastingMode; smr.receiveShadows = body.receiveShadows;
            smr.lightProbeUsage = body.lightProbeUsage; smr.reflectionProbeUsage = body.reflectionProbeUsage;
            smr.motionVectorGenerationMode = body.motionVectorGenerationMode; smr.skinnedMotionVectors = body.skinnedMotionVectors; smr.allowOcclusionWhenDynamic = body.allowOcclusionWhenDynamic;
            smr.renderingLayerMask = body.renderingLayerMask | HeroRimLight.Layer;
            var mats = new Material[p.roles.Length];
            string maps = $"Accessories/{sex}/{e.def.id}/Acc_{sex}_{e.def.id}_{p.maps}";
            for (int i = 0; i < mats.Length; i++)
            {
                bool metal = p.roles[i].EndsWith("Metal");
                mats[i] = metal ? look.AccessoryMetal(p.roles[i] + " (hero)", Color.white) : look.AccessoryCloth(p.roles[i] + " (hero)", maps, p.metresPerUv, Color.white, Color.white);
                e.materials.Add((mats[i], metal));
            }
            smr.sharedMaterials = mats;
            return smr;
        }

        void Apply(Equipped e)
        {
            Color b = e.baseColour ?? HeroKit.Hex(e.def.baseHex), t = e.trimColour ?? HeroKit.Hex(e.def.trimHex);
            foreach (var (m, metal) in e.materials)
            {
                if (!m) continue;
                if (metal) { if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(.78f, .79f, .81f, 1)); continue; }
                m.SetColor("_BaseColor", MatchHeroLook.KitTint(b)); m.SetColor("_TrimColor", MatchHeroLook.KitTint(t));
            }
            // hidden pieces are disabled, not deactivated: the lobby export keeps its part list stable and only draws enabled renderers
            foreach (var r in e.renderers)
            {
                if (!r) continue;
                bool on = e.visible;
                if (e.def.slot == AccessorySlot.Glove) on &= r.name.EndsWith("R") == leftHandedGolfer;
                r.enabled = on;
            }
            ApplyCover();
        }

        // ---------------------------------------------------------------- skin cover
        /// Skin an accessory covers (the golf glove: the whole hand, work/accessories/py/glove_cover.py) is not drawn, so it can never show through the wall in a swing.
        /// Same idea as the clothing V2 cover: the body ASSET is untouched; a derived copy of the mesh the body shows right now drops the listed triangles
        /// (int32 body-vertex triplets in Resources/Accessories/<Sex>/<item>/Acc_<Sex>_<item>_<Tag>_hide.bytes). Re-applied by MatchHeroLook after its own cover.
        Mesh coverSource;
        static readonly Dictionary<(Mesh, string), Mesh> covered = new Dictionary<(Mesh, string), Mesh>();
        static readonly HashSet<Mesh> coverVariants = new HashSet<Mesh>();
        static long CoverKey(int a, int b, int c)
        {
            if (a > b) (a, b) = (b, a); if (b > c) (b, c) = (c, b); if (a > b) (a, b) = (b, a);
            return ((long)a << 42) | ((long)b << 21) | (long)c;
        }

        public void ReapplyCover() => ApplyCover();

        void ApplyCover()
        {
            if (!look || !look.body) return;
            var cur = look.body.sharedMesh; if (!cur) return;
            var src = coverVariants.Contains(cur) && coverSource ? coverSource : cur;
            if (!src.isReadable) return;
            string sex = look.female ? "Female" : "Male";
            var lists = new List<(string key, TextAsset data)>();
            foreach (var e in worn.Values)
                foreach (var r in e.renderers)
                {
                    if (!r || !r.enabled) continue;
                    var p = e.data.pieces.FirstOrDefault(q => q.renderer == r.name);
                    if (p == null) continue;
                    var ta = Resources.Load<TextAsset>($"Accessories/{sex}/{e.def.id}/Acc_{sex}_{e.def.id}_{p.maps}_hide");
                    if (ta) lists.Add(($"{e.def.id}:{p.maps}", ta));
                }
            if (lists.Count == 0)
            {
                if (coverVariants.Contains(cur) && coverSource) look.body.sharedMesh = coverSource;
                return;
            }
            coverSource = src;
            string key = string.Join("|", lists.Select(l => l.key).OrderBy(k => k, StringComparer.Ordinal));
            if (!covered.TryGetValue((src, key), out var mesh) || !mesh)
            {
                var hide = new HashSet<long>();
                foreach (var (_, ta) in lists)
                {
                    var b = ta.bytes;
                    for (int i = 0; i + 12 <= b.Length; i += 12) hide.Add(CoverKey(BitConverter.ToInt32(b, i), BitConverter.ToInt32(b, i + 4), BitConverter.ToInt32(b, i + 8)));
                }
                mesh = UnityEngine.Object.Instantiate(src); mesh.name = src.name + " (accessory cover)"; mesh.hideFlags = HideFlags.DontSave;
                for (int sub = 0; sub < src.subMeshCount; sub++)
                {
                    var t = src.GetTriangles(sub); var keep = new List<int>(t.Length);
                    for (int i = 0; i < t.Length; i += 3)
                        if (!hide.Contains(CoverKey(t[i], t[i + 1], t[i + 2]))) { keep.Add(t[i]); keep.Add(t[i + 1]); keep.Add(t[i + 2]); }
                    mesh.SetTriangles(keep, sub, false);
                }
                covered[(src, key)] = mesh; coverVariants.Add(mesh);
            }
            look.body.sharedMesh = mesh;
        }

        static void Kill(UnityEngine.Object o) { if (!o) return; if (Application.isPlaying) Destroy(o); else DestroyImmediate(o); }
        void OnDestroy() { foreach (var e in worn.Values) foreach (var (m, _) in e.materials) Kill(m); }
    }
}
