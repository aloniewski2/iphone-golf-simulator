using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace GolfArcade.Tennis
{
    /// Plan 2D "court postcard" pass, world art only. The v3 resort export keeps its court, runoff,
    /// stands, walls, palms and props; what read badly on the cinematic / reaction cameras is swapped
    /// for the EnvV4 kit (ArtDir/env_v4/tools/build_env_v4.py):
    ///   * lumpy grey-green "garden shrubs" (41 x 6k tris) -> chunky toy shrubs (320-500 faces) with flowers;
    ///   * stone pines / cypress blobs -> toy broadleaf + flowering trees;
    ///   * the grey 5k-tri clubhouse -> the beach-house landmark on the same footprint, facing the court;
    ///   * a band of toy trees on the island just outside the resort terrace, where react / orbit cams look,
    ///     and a sloped grass skirt so the terrace no longer reads as a bare slab from the air.
    /// Nothing is placed inside the court + runoff corridor (|x| < 13, |z| < 21) or above it.
    /// Everything uses ONE shared palette-atlas material (GPU instancing on) so the kit batches.
    public static class TennisEnvironmentV4
    {
        public static bool Enabled = true;
        const string Root = "Tennis/EnvV4/";
        static Material kit;

        public static Material Kit
        {
            get
            {
                if (kit) return kit;
                var tex = Resources.Load<Texture2D>(Root + "EnvV4_Palette");
                kit = TennisVenueArt.Surface("EnvV4 palette kit", new Color(.94f, .94f, .94f), .18f, 0, .025f, .004f, tex);
                return kit;
            }
        }

        /// (internal: the volcano venue plants the same toy trees on its grassland)
        internal static GameObject Spawn(string asset, Transform parent, Vector3 at, float yaw, float height, float tint = 1)
        {
            // One coherent opaque kit replaces the mixture of flat V4 canopies,
            // photogrammetry foliage and hand-built shrubs. Batched by seven shared roles.
            if (asset.Contains("TreeBroadleaf") || asset.Contains("TreeFlowering"))
            { TennisVenueArt.QueuePlant("CANOPY", at, yaw, height, height * .55f); return null; }
            if (asset.Contains("Shrub"))
            {
                TennisVenueArt.QueuePlant("BUSH_BROAD", at, yaw, height, height * .8f);
                TennisVenueArt.QueuePlant("FLOWER_CORAL", at + Vector3.up * (height * .18f), yaw + 37, height * .8f, height * .55f);
                return null;
            }
            // Env V5: the sculpted (Tripo) toy broadleaf replaces the V4 blob broadleafs when it ships
            if (asset.StartsWith("EnvV4_TreeBroadleaf") && Resources.Load<GameObject>(Root + "EnvV5_TreeA")) asset = "EnvV5_TreeA";
            bool clubhouse = asset == "EnvV4_BeachHouse";
            var prefab = clubhouse ? Resources.Load<GameObject>("Tennis/Premium/TennisClubhouse") : null;
            if (!prefab) prefab = Resources.Load<GameObject>(Root + asset);
            if (!prefab) return null;
            var go = Object.Instantiate(prefab, parent); go.name = asset;
            var mat = asset.StartsWith("EnvV5_") ? V5Material(asset) : Kit;
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (clubhouse)
                {
                    var roles = r.sharedMaterials;
                    for (int i = 0; i < roles.Length; i++)
                    {
                        string n = roles[i] ? roles[i].name : "";
                        int cell = n.StartsWith("CLUBHOUSE_") && int.TryParse(n.Substring(10, 2), out int c) ? c : -1;
                        float gloss = cell == 7 ? .73f : cell == 6 ? .27f : cell == 4 ? .36f : cell == 12 ? .33f : .18f;
                        roles[i] = TennisVenueArt.Surface("Clubhouse role " + cell, new Color(.94f,.94f,.94f), gloss, 0, cell==6?.065f:.025f, cell==6?.013f:.004f,
                            Resources.Load<Texture2D>(Root + "EnvV4_Palette"));
                        if(cell==7)
                        {
                            roles[i].SetTexture("_ReflectionMap",Resources.Load<Texture2D>("Course/Resort/SkyCoastalSmall"));
                            roles[i].SetFloat("_ReflectionWeight",.46f);roles[i].SetFloat("_ReflectionHeading",-45);
                        }
                        TennisResortMaterials.Surface(roles[i], "Clubhouse role " + cell);
                    }
                    r.sharedMaterials = roles;
                }
                else r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; r.receiveShadows = true;
                if (tint != 1) { var mpb = new MaterialPropertyBlock(); mpb.SetColor("_BaseColor", new Color(.94f * tint, .94f * tint, .94f * tint)); r.SetPropertyBlock(mpb); }
            }
            var lod1 = asset.StartsWith("EnvV5_") ? Resources.Load<GameObject>(Root + asset + "_LOD1") : null;
            if (lod1)
            {
                // sculpted props: full mesh near the cams, a ~900-tri stand-in beyond ~20% screen height
                var l1 = Object.Instantiate(lod1, go.transform); l1.name = asset + "_LOD1";
                l1.transform.localPosition = Vector3.zero; l1.transform.localRotation = Quaternion.identity; l1.transform.localScale = Vector3.one;
                foreach (var r in l1.GetComponentsInChildren<MeshRenderer>(true)) { r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; }
                var r0 = go.GetComponentsInChildren<MeshRenderer>(true).Where(r => !r.transform.IsChildOf(l1.transform)).ToArray();
                var lg = go.AddComponent<LODGroup>();
                lg.SetLODs(new[] { new LOD(.2f, r0), new LOD(.01f, l1.GetComponentsInChildren<MeshRenderer>(true)) });
            }
            go.transform.rotation = Quaternion.Euler(0, yaw, 0) * prefab.transform.rotation;   // keep the FBX up-axis fix
            // fit by height, feet on `at`
            var b = Bounds(go); float s = height / Mathf.Max(.01f, b.size.y);
            go.transform.localScale *= s; b = Bounds(go);
            go.transform.position += at - new Vector3(b.center.x, b.min.y, b.center.z);
            return go;
        }

        static readonly Dictionary<string, Material> v5Mats = new Dictionary<string, Material>();
        /// Env V5 props carry their own sculpted base-colour texture (<asset>.png beside the FBX), on the kit's shader.
        static Material V5Material(string asset)
        {
            if (v5Mats.TryGetValue(asset, out var m) && m) return m;
            var tex = Resources.Load<Texture2D>(Root + asset);
            m = new Material(Kit) { name = asset + " (V5)", enableInstancing = true };
            if (tex) { m.mainTexture = tex; if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex); }
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(.94f, .94f, .94f));   // world a notch under the characters
            return v5Mats[asset] = m;
        }

        static Bounds Bounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(); var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds); return b;
        }

        static bool InPlay(Vector3 p) => Mathf.Abs(p.x) < 13f && Mathf.Abs(p.z) < 21f;

        public static void Apply(GameObject arena, GameObject island)
        {
            if (!Enabled || !arena || System.Environment.GetEnvironmentVariable("HERO_ENV_OFF") == "1") return;   // env var: editor before/after proof only
            var root = new GameObject("EnvV4 postcard kit").transform;
            root.SetParent(arena.transform, true);
            var rng = new System.Random(2040);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var shrubs = new[] { "EnvV4_ShrubA", "EnvV4_ShrubB", "EnvV4_ShrubC" };
            int si = 0;
            foreach (var r in arena.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.enabled || !r.sharedMaterial) continue;
                string m = r.sharedMaterial.name.Replace(" (Instance)", "");
                int styled = m.IndexOf(" (styled)", System.StringComparison.Ordinal);
                if (styled >= 0) m = m.Substring(0, styled);
                var b = r.bounds; var foot = new Vector3(b.center.x, b.min.y, b.center.z);
                switch (m)
                {
                    case "TropicalV3_026":   // lumpy garden shrubs -> toy shrubs (same spots, same size)
                    case "TropicalV3_014":   // photoscan hydrangeas -> coherent leaf / flower volumes
                        r.enabled = false;
                        Spawn(shrubs[si++ % 3], root, foot, R(0, 360), Mathf.Max(.9f, b.size.y * 1.15f), R(.92f, 1.04f));
                        break;
                    case "TropicalV3_028":   // stone pine blobs -> big toy broadleaf
                        r.enabled = false;
                        Spawn(si++ % 2 == 0 ? "EnvV4_TreeBroadleafA" : "EnvV4_TreeBroadleafB", root, foot, R(0, 360), R(6.8f, 7.8f));
                        break;
                    case "TropicalV3_034":   // cypress -> flowering tree
                        r.enabled = false;
                        Spawn("EnvV4_TreeFlowering", root, foot, R(0, 360), 6.2f);
                        break;
                    case "TropicalV3_018":   // hanging vines that belonged to the old clubhouse facade
                        r.enabled = false; break;
                    case "TropicalV3_021": case "TropicalV3_029":   // far headlands + their village: sit them in the sea, not on it
                        r.transform.position += Vector3.down * 5f;
                        break;
                    case "TropicalV3_015":   // grey clubhouse -> beach house landmark, facing the court
                    {
                        r.enabled = false;
                        var house = Spawn("EnvV4_BeachHouse", root, foot, 0, 9.2f);
                        if (house)
                        {
                            // long side along the court (world z), front (-Y in Blender) toward the court (-x)
                            house.transform.rotation = Quaternion.Euler(0, HouseYaw, 0) * Resources.Load<GameObject>(Root + "EnvV4_BeachHouse").transform.rotation;
                            var hb = Bounds(house); float s = Mathf.Min(b.size.z / hb.size.z, 9.2f / hb.size.y) * 1.02f;
                            house.transform.localScale *= s; hb = Bounds(house);
                            house.transform.position += foot - new Vector3(hb.center.x, hb.min.y, hb.center.z);
                        }
                        break;
                    }
                }
            }
            bool authoredCoast = TennisVenueArt.BuildCoast(root, arena, island);
            if (!authoredCoast)
            {
                if (island) PlantIsland(root, island, arena, R);
                BuildSkirt(root, arena, island);
            }
            TennisVenueArt.Build(TennisVenueKind.Resort, arena);
        }

        /// Yaw that turns the Blender-authored house (front = -Y) to face -x in the arena.
        public static float HouseYaw = 270f;

        // ---- island tree band + terrace skirt
        static void PlantIsland(Transform root, GameObject island, GameObject arena, System.Func<float, float, float> R)
        {
            var mc = island.GetComponentInChildren<MeshFilter>(); if (!mc) return;
            var col = mc.gameObject.AddComponent<MeshCollider>(); col.sharedMesh = mc.sharedMesh;
            var trees = new[] { "EnvV4_TreeBroadleafA", "EnvV4_TreeBroadleafB", "EnvV4_TreeFlowering", "EnvV4_TreeBroadleafA" };
            int n = 0;
            // terrace (foundation) rectangle in world space: x -37..45, z -32.5..34.5
            float x0 = -37, x1 = 45, z0 = -32.5f, z1 = 34.5f;
            var spots = new List<Vector3>();
            // Two staggered rows, dense enough to screen the island's flat canopies from the react / orbit cams.
            for (int row = 0; row < 2; row++)
            {
                for (float x = x0 - 6 + row * 2.4f; x <= x1 + 6; x += 4.8f) spots.Add(new Vector3(x + R(-1, 1), 0, z0 - 4.5f - row * 5f - R(0, 1.5f)));   // south, behind the player
                for (float z = z0 - 4 + row * 2.4f; z <= z1 + 4; z += 4.8f) spots.Add(new Vector3(x0 - 4.5f - row * 5f - R(0, 1.5f), 0, z + R(-1, 1)));   // west, left-side cams
            }
            for (float z = z0; z <= z1 - 10; z += 9f) { spots.Add(new Vector3(x1 + R(5, 12), 0, z + R(-2, 2))); }         // east, behind the house
            foreach (var s in spots)
            {
                if (InPlay(s)) continue;
                if (!Physics.Raycast(new Vector3(s.x, 80, s.z), Vector3.down, out var hit, 200) || hit.collider != col) continue;
                if (hit.point.y < -2.8f) continue;   // on the sand / in the sea: skip
                Spawn(trees[n++ % trees.Length], root, hit.point - Vector3.up * .15f, R(0, 360), R(5.8f, 8.2f), R(.88f, 1.04f));
                if (n % 2 == 0) Spawn(n % 4 == 0 ? "EnvV4_ShrubA" : "EnvV4_ShrubC", root, hit.point + new Vector3(R(-3, 3), -.1f, R(-3, 3)), R(0, 360), R(1.2f, 1.8f));
            }
            Object.Destroy(col);
        }

        /// A sloped grass apron around the terrace so its vertical edges disappear into the island.
        static void BuildSkirt(Transform root, GameObject arena, GameObject island)
        {
            if (!island) return;
            var mf = island.GetComponentInChildren<MeshFilter>(); if (!mf) return;
            var col = mf.GetComponent<MeshCollider>(); bool created = !col;
            if (!col) { col = mf.gameObject.AddComponent<MeshCollider>(); col.sharedMesh = mf.sharedMesh; }
            float x0 = -37, x1 = 45, z0 = -32.5f, z1 = 34.5f, top = -.12f, run = 5f;
            var outer = new[] { new Vector3(x0 - run, top, z0 - run), new Vector3(x1 + run, top, z0 - run), new Vector3(x1 + run, top, z1 + run), new Vector3(x0 - run, top, z1 + run) };
            var inner = new[] { new Vector3(x0, top, z0), new Vector3(x1, top, z0), new Vector3(x1, top, z1), new Vector3(x0, top, z1) };
            var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
            const float lawnU = (14 + .5f) / 16f;
            for (int i = 0; i < 4; i++)
            {
                int j = (i + 1) % 4;
                for (int s = 0; s < 24; s++)
                {
                    float a = s / 24f, b = (s + 1) / 24f;
                    var o0 = Vector3.Lerp(outer[i], outer[j], a); var o1 = Vector3.Lerp(outer[i], outer[j], b);
                    bool h0 = col.Raycast(new Ray(o0 + Vector3.up * 80, Vector3.down), out var hit0, 200);
                    bool h1 = col.Raycast(new Ray(o1 + Vector3.up * 80, Vector3.down), out var hit1, 200);
                    // Blend only into dry ground. An ocean-facing edge has its original
                    // limestone seawall; it must never grow a rectangular grass platform.
                    if (!h0 || !h1 || hit0.point.y < -2.2f || hit1.point.y < -2.2f) continue;
                    o0.y = Mathf.Min(top, hit0.point.y + .025f); o1.y = Mathf.Min(top, hit1.point.y + .025f);
                    int k = verts.Count;
                    verts.Add(Vector3.Lerp(inner[i], inner[j], a)); verts.Add(Vector3.Lerp(inner[i], inner[j], b)); verts.Add(o1); verts.Add(o0);
                    for (int v = 0; v < 4; v++) uvs.Add(new Vector2(lawnU, .65f));
                    tris.AddRange(new[] { k, k + 2, k + 1, k, k + 3, k + 2 });
                }
            }
            if (created) Object.Destroy(col);
            if (verts.Count == 0) return;
            var mesh = new Mesh { name = "Terrace grass skirt" }; mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            // face up
            var n = mesh.normals; if (n.Length > 0 && n[0].y < 0) { tris.Reverse(); mesh.SetTriangles(tris, 0); mesh.RecalculateNormals(); }
            var go = new GameObject("Terrace grass skirt"); go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh; var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = Kit;
            var mpb = new MaterialPropertyBlock(); mpb.SetColor("_BaseColor", new Color(.9f, .94f, .88f)); mr.SetPropertyBlock(mpb);
        }
    }
}
