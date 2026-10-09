using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GolfArcade.Tennis
{
    /// The premium resort construction layer. Actual shaped geometry, shared with the golf
    /// botanical kit, sits outside the play corridor; it has no colliders or gameplay tags.
    public static class TennisVenueArt
    {
        static readonly Dictionary<string, Material> surfaces = new Dictionary<string, Material>();
        struct Plant { public string asset; public Vector3 position; public float yaw, height, radius; }
        static readonly List<Plant> plants = new List<Plant>();
        internal static void QueuePlant(string asset, Vector3 at, float yaw, float height, float radius)
            => plants.Add(new Plant { asset = asset, position = at, yaw = yaw, height = height, radius = radius });

        public static Material Surface(string key, Color colour, float smoothness, float metallic = 0,
                                       float variation = .04f, float grain = .01f, Texture map = null)
        {
            if (surfaces.TryGetValue(key, out var old) && old) { TennisResortMaterials.Surface(old, key); return old; }
            var shader = Resources.Load<Shader>("Tennis/Shaders/TennisWorldSurface") ?? Shader.Find("Universal Render Pipeline/Lit");
            var m = new Material(shader) { name = key, enableInstancing = true };
            m.SetColor("_BaseColor", colour); m.SetFloat("_Smoothness", smoothness); m.SetFloat("_Metallic", metallic);
            if (m.HasProperty("_Variation")) m.SetFloat("_Variation", variation);
            if (m.HasProperty("_Grain")) m.SetFloat("_Grain", grain);
            if (map) m.SetTexture("_BaseMap", map);
            TennisResortMaterials.Surface(m, key);
            return surfaces[key] = m;
        }

        /// Render-only coast authored on the original island and headland footprint.
        /// Empty markers place the same opaque foliage used around the live court.
        public static bool BuildCoast(Transform parent, GameObject arena, GameObject island)
        {
            var prefab = Resources.Load<GameObject>("Tennis/Premium/TennisCoast");
            if (!prefab || !island) return false;
            var coast = Object.Instantiate(prefab, parent);
            coast.name = "Authored resort coast";
            coast.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0,180,0) * prefab.transform.rotation);
            Transform shoreOrigin=null,shoreU=null,shoreV=null;
            foreach(var t in coast.GetComponentsInChildren<Transform>(true))
            {if(t.name=="COAST_SHORE_ORIGIN")shoreOrigin=t;else if(t.name=="COAST_SHORE_U")shoreU=t;else if(t.name=="COAST_SHORE_V")shoreV=t;}
            if(shoreOrigin&&shoreU&&shoreV)
            {
                var u=shoreU.position-shoreOrigin.position;var v=shoreV.position-shoreOrigin.position;
                u/=u.sqrMagnitude;v/=v.sqrMagnitude;
                var toUV=Matrix4x4.identity;
                toUV.SetRow(0,new Vector4(u.x,u.y,u.z,-Vector3.Dot(u,shoreOrigin.position)));
                toUV.SetRow(1,new Vector4(v.x,v.y,v.z,-Vector3.Dot(v,shoreOrigin.position)));
                Shader.SetGlobalMatrix("_TennisShoreWorldToUV",toUV);
                Debug.Log("[TennisCoast] shoreline UV anchors "+shoreOrigin.position+" U="+shoreU.position+" V="+shoreV.position);
            }
            foreach (var renderer in coast.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    string role = materials[i] ? materials[i].name : "";
                    if (role.Contains("TERRAIN"))
                    {
                        materials[i] = Surface("Coast blended meadow and beach", new Color(.94f,.94f,.88f), .10f, 0, .105f, .008f);
                        materials[i].SetFloat("_CoastBlend",1);
                        materials[i].SetColor("_SandColor",new Color(.78f,.75f,.63f));
                        materials[i].SetColor("_GrassColor",new Color(.13f,.30f,.09f));
                        materials[i].SetTexture("_WorldMap",Resources.Load<Texture2D>("Course/Resort/Limestone_C"));
                        materials[i].SetFloat("_WorldMapWeight",1); materials[i].SetFloat("_WorldMapScale",.12f);
                    }
                    else if (role.Contains("GRASS")) materials[i] = Surface("Coast meadow", new Color(.13f,.30f,.09f), .1f, 0, .075f, .009f);
                    else if (role.Contains("SAND")) materials[i] = Surface("Coast warm sand", new Color(.83f,.77f,.63f), .10f, 0, .045f, .018f);
                    else if (role.Contains("LIMESTONE"))
                    {
                        materials[i] = Surface("Coast eroded limestone", new Color(.94f,.94f,.88f), .14f, 0, .06f, .015f);
                        materials[i].SetTexture("_WorldMap", Resources.Load<Texture2D>("Course/Resort/Limestone_C"));
                        materials[i].SetFloat("_WorldMapWeight", 1); materials[i].SetFloat("_WorldMapScale", .12f);
                    }
                    else materials[i] = Surface("Coast wet stone", new Color(.36f,.48f,.43f), .25f, 0, .04f, .01f);
                }
                renderer.sharedMaterials = materials; renderer.receiveShadows = true;
            }
            // Two slender pedestrian links explain the resort terrace's construction
            // and connect it to real authored ground, instead of a grass rectangle in water.
            var causeway=new TennisVenueBuilder.MB();var rails=new TennisVenueBuilder.MB();
            foreach(int side in new[]{-1,1})
            {
                Vector3 start=new Vector3(side<0?-36.5f:44.5f,.035f,side<0?-8f:12f);
                Vector3 destination=Vector3.zero;float nearest=float.MaxValue;
                foreach(var filter in coast.GetComponentsInChildren<MeshFilter>(true))
                {
                    if(!filter.name.Contains("coast") || !filter.sharedMesh)continue;
                    foreach(var local in filter.sharedMesh.vertices)
                    {
                        var v=filter.transform.TransformPoint(local);
                        if(v.y<-.35f || Mathf.Abs(v.z)>28 || (side<0?v.x>-39:v.x<47))continue;
                        float cost=(v-start).sqrMagnitude+Mathf.Abs(v.z-start.z)*2;
                        if(cost<nearest){nearest=cost;destination=v;}
                    }
                }
                if(nearest>55*55)continue;
                destination.y+=.05f;
                var direction=destination-start;int segments=Mathf.Max(3,Mathf.CeilToInt(direction.magnitude/3));
                var right=Vector3.Cross(Vector3.up,direction).normalized*2.15f;
                for(int n=0;n<segments;n++)
                {
                    var a=Vector3.Lerp(start,destination,n/(float)segments);var b=Vector3.Lerp(start,destination,(n+1)/(float)segments);
                    causeway.Face4(a-right,a+right,b+right,b-right,Vector2.zero,Vector2.right,Vector2.one,Vector2.up);
                    if(n%2==0)foreach(int edge in new[]{-1,1})
                    {
                        var at=a+right*edge;
                        causeway.Cyl(new Vector3(at.x,-4.3f,at.z),.34f,.28f,at.y+4.3f,12,Vector2.zero,Vector2.zero);
                        rails.Cyl(at,.055f,.055f,.95f,8,Vector2.zero,Vector2.zero);
                    }
                }
            }
            Make(parent,"Resort shore causeways",causeway,Surface("Resort walkway cut limestone",new Color(.74f,.72f,.63f),.2f));
            Make(parent,"Resort causeway bronze posts",rails,Surface("Resort walkway bronze",new Color(.24f,.27f,.25f),.34f,.35f));
            foreach (var marker in coast.GetComponentsInChildren<Transform>(true))
            {
                if (!marker.name.StartsWith("COAST_")) continue;
                var parts = marker.name.Split('_');
                if (parts.Length < 5 || !float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var height)
                    || !float.TryParse(parts[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var radius)
                    || !float.TryParse(parts[4], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var yaw)) continue;
                QueuePlant(parts[1] == "BUSH" ? "BUSH_BROAD" : parts[1] == "SHRUB0" ? "SHRUB_0" : parts[1] == "SHRUB1" ? "SHRUB_1" : parts[1], marker.position, yaw, height*.01f, radius*.01f);
            }
            foreach (var renderer in island.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
            foreach (var renderer in arena.GetComponentsInChildren<Renderer>(true))
                foreach (var material in renderer.sharedMaterials)
                    if (material && material.name.StartsWith("TropicalV3_021")) { renderer.enabled = false; break; }
            return true;
        }

        public static void Build(TennisVenueKind venue, GameObject arena)
        {
            if (!arena || arena.transform.Find("Premium tennis craft")) return;
            var root = new GameObject("Premium tennis craft").transform;
            root.SetParent(arena.transform, true);
            root.gameObject.AddComponent<TennisVenueBreeze>();
            var stone = new TennisVenueBuilder.MB(); var timber = new TennisVenueBuilder.MB();
            var trim = new TennisVenueBuilder.MB(); var botany = new BotanicalBatch(venue == TennisVenueKind.Resort);
            if (venue == TennisVenueKind.Resort)
            { TennisResortFixtures.Build(root,arena); TennisResortVista.Build(root); }
            foreach (var p in plants) botany.Add(p.asset, p.position, p.radius, p.height, p.yaw);
            plants.Clear();
            var ivory = new Color(.78f, .73f, .62f); var teak = new Color(.43f, .25f, .12f);
            if (venue == TennisVenueKind.Resort)
            {
                // A limestone colonnade / timber pergola gives the left flank an authored
                // sports-resort landmark instead of another box building.
                TennisResortVista.Pergola(stone,timber);
                int palm = 0;
                foreach (var r in arena.GetComponentsInChildren<Renderer>(true))
                {
                    if (!r.enabled || !r.sharedMaterial) continue;
                    string n = r.sharedMaterial.name;
                    if (n.StartsWith("TropicalV3_030"))
                    {
                        var b = r.bounds; r.enabled = false;
                        botany.Add("PALM", new Vector3(b.center.x, b.min.y, b.center.z), 4.65f,
                            Mathf.Clamp(b.size.y, 10, 13.5f), palm++ * 71);
                    }
                    else if(n.StartsWith("TropicalV3_028"))
                    {
                        var b=r.bounds;r.enabled=false;
                        float radius=Mathf.Clamp(Mathf.Max(b.size.x,b.size.z)/1.52f,5,12);
                        botany.Add("CANOPY",new Vector3(b.center.x,b.min.y,b.center.z),radius,Mathf.Clamp(b.size.y/1.02f,7,13.5f),palm++*73);
                    }
                    else if (n.StartsWith("TropicalV3_016") && r.bounds.center.z > 20)
                        r.enabled = false; // The low limestone seawall leaves a calm ocean window.
                }
                for (int side = -1; side <= 1; side += 2)
                    for (int i = 0; i < 6; i++)
                    {
                        var p = new Vector3(side * 14.2f, 0, -17.5f + i * 7);
                        Planter(stone, botany, p, 1.45f, i + side * 7);
                    }
                for (int i = 0; i < 9; i++)
                {
                    float x = -30 + i * 7.5f;
                    botany.Add("BUSH_BROAD", new Vector3(x, .1f, 30), 2.0f, 1.6f, i * 47);
                    botany.Add("FLOWER_CORAL", new Vector3(x + 1.1f, .4f, 29), .9f, 1.25f, i * 63);
                }
            }
            else if (venue == TennisVenueKind.Skyscraper)
            {
                ivory = new Color(.68f, .70f, .72f);
                for (int side = -1; side <= 1; side += 2)
                    for (int i = 0; i < 4; i++)
                    {
                        var p = new Vector3(side * 11.55f, 0, -14 + i * 9.3f);
                        Planter(stone, botany, p, .72f, i + 11);
                        if (i == 1 || i == 2) BevelBox(timber, p + new Vector3(0, .48f, 2.0f), new Vector3(.68f, .18f, 1.6f), .07f);
                    }
                // Warm metallic / ceramic details anchor the court against the distant city.
                for (int side = -1; side <= 1; side += 2)
                    BevelBox(trim, new Vector3(side * 12.15f, -.13f, 0), new Vector3(.08f, .08f, 41.0f), .02f);
            }
            else
            {
                ivory = new Color(.085f, .095f, .125f); teak = new Color(.19f, .095f, .055f);
                // Faceted obsidian corner anchors give the levitating platform believable
                // construction. Narrow bronze seams catch the lava, without bright ball-like dots.
                for (int side = -1; side <= 1; side += 2)
                    for (int end = -1; end <= 1; end += 2)
                    {
                        var p = new Vector3(side * 11.85f, -.1f, end * 20.5f);
                        stone.Cyl(p, .62f, .43f, 1.0f, 8, Vector2.zero, Vector2.zero);
                        stone.Cyl(p + Vector3.up * .98f, .52f, .52f, .13f, 8, Vector2.zero, Vector2.zero);
                        trim.Cyl(p + Vector3.up * .92f, .47f, .47f, .07f, 8, Vector2.zero, Vector2.zero);
                    }
                for (int side = -1; side <= 1; side += 2)
                    BevelBox(trim, new Vector3(side * 10.2f, .025f, 0), new Vector3(.045f, .025f, 38.8f), .008f);
            }
            Make(root, "Cut stone and ceramic", stone, Surface(venue + " cut stone", ivory, venue == TennisVenueKind.Volcano ? .38f : .22f));
            Make(root, "Warm timber joinery", timber, Surface(venue + " timber", teak, .26f, 0, .065f, .015f));
            Make(root, "Fine bronze hardware", trim, Surface(venue + " bronze", new Color(.58f, .36f, .16f), .48f, .65f, .02f, .003f));
            botany.Build(root);
            NetTape(root, arena);
        }

        static void Planter(TennisVenueBuilder.MB stone, BotanicalBatch leaves, Vector3 p, float radius, int seed)
        {
            BevelBox(stone, p + Vector3.up * .34f, new Vector3(radius * 2, .68f, radius * 2), .10f);
            BevelBox(stone, p + Vector3.up * .68f, new Vector3(radius * 2.08f, .14f, radius * 2.08f), .08f);
            leaves.Add("BUSH_BROAD", p + Vector3.up * .72f, radius * 1.08f, radius * .85f, seed * 31);
            leaves.Add("FERN", p + new Vector3(radius * .42f, .7f, -radius * .2f), radius * .64f, radius * .68f, seed * 17);
            leaves.Add("FLOWER_CORAL", p + new Vector3(-radius * .35f, .72f, radius * .23f), radius * .62f, radius * .86f, seed * 57);
        }

        static void NetTape(Transform root, GameObject arena)
        {
            foreach (var r in arena.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.sharedMaterial || !r.sharedMaterial.name.StartsWith("TropicalV3_012")) continue;
                var b = r.bounds;
                if (b.size.x < 5 || b.size.y < .3f) continue;
                var m = new TennisVenueBuilder.MB();
                // Follow the original imported net's exact extents / court height.
                BevelBox(m, new Vector3(b.center.x, b.max.y - .018f, b.center.z), new Vector3(b.size.x, .065f, .045f), .012f);
                BevelBox(m, new Vector3(b.center.x, b.center.y, b.center.z), new Vector3(.043f, b.size.y, .035f), .006f);
                Make(root, "Woven ivory net tape", m, Surface("Tennis net tape", new Color(.86f, .86f, .80f), .14f, 0, .015f, .005f));
                break;
            }
        }

        static void Make(Transform root, string name, TennisVenueBuilder.MB mb, Material m)
        {
            if (mb.Count == 0) return;
            TennisVenueBuilder.MeshObject(root, name, mb.ToMesh(name), m, true);
        }

        /// Eight-sided footprint and chamfered shoulders: a visible edge catches the key light.
        internal static void BevelBox(TennisVenueBuilder.MB m, Vector3 c, Vector3 size, float bevel)
        {
            float x = size.x * .5f, z = size.z * .5f, y = size.y * .5f;
            float b = Mathf.Min(bevel, Mathf.Min(x, Mathf.Min(y, z)) * .8f);
            Vector2[] ring = { new Vector2(-x+b,-z), new Vector2(x-b,-z), new Vector2(x,-z+b), new Vector2(x,z-b), new Vector2(x-b,z), new Vector2(-x+b,z), new Vector2(-x,z-b), new Vector2(-x,-z+b) };
            for (int i = 0; i < ring.Length; i++)
            {
                int j = (i + 1) % ring.Length;
                var a = c + new Vector3(ring[i].x, -y+b, ring[i].y);
                var d = c + new Vector3(ring[j].x, -y+b, ring[j].y);
                var at = c + new Vector3(ring[i].x, y-b, ring[i].y);
                var dt = c + new Vector3(ring[j].x, y-b, ring[j].y);
                m.Face(a, d, dt, at, 0, 1, 0, 1);
                var p = c + new Vector3(ring[i].x * (x-b) / x, y, ring[i].y * (z-b) / z);
                var q = c + new Vector3(ring[j].x * (x-b) / x, y, ring[j].y * (z-b) / z);
                m.Face(at, dt, q, p, 0, 1, 0, 1);
                // Top fan is wound upward.
                m.Tri(m.Add(c + Vector3.up * y, Vector2.one * .5f), m.Add(p, Vector2.zero), m.Add(q, Vector2.one));
            }
        }

        sealed class BotanicalBatch
        {
            static readonly Color[] Palette={new Color(.39f,.24f,.12f),new Color(.075f,.26f,.10f),new Color(.18f,.40f,.13f),new Color(.37f,.52f,.14f),new Color(.87f,.28f,.23f),new Color(.90f,.62f,.18f),new Color(.43f,.44f,.36f)};
            sealed class Cell
            {
                public readonly List<Vector3> vertices=new(),normals=new();
                public readonly List<Color> colours=new();
                public readonly List<Vector2> pivots=new();
                public readonly List<int> triangles=new();
            }
            readonly Dictionary<Vector2Int,Cell> cells=new(), treesNear=new(), treesFar=new(), foliageNear=new(), foliageFar=new();
            readonly Dictionary<string,(MeshFilter filter,Matrix4x4 matrix)> prototypes=new();
            public BotanicalBatch(bool resort)
            {
                void Load(string path)
                {
                    var kit=Resources.Load<GameObject>(path);if(!kit)return;
                    foreach(var mf in kit.GetComponentsInChildren<MeshFilter>(true))
                        prototypes[mf.name]=(mf,kit.transform.worldToLocalMatrix*mf.transform.localToWorldMatrix);
                }
                Load("Tennis/Premium/TennisBotanicalKit");Load("Course/Resort/BotanicalKit");
                // Shared PALM/PINE override old tennis cards. Broadleaf crowns
                // are their own authored species, not the shared first-pass blob.
                Load("Tennis/Premium/TennisCanopy");Load("Tennis/Premium/TennisUnderstoryFar");
                Load("Tennis/Premium/TennisCoconutPalm"); // fuller arched crown with the same shared pivot/role schema
                if (resort && System.Environment.GetEnvironmentVariable("TENNIS_RESORT_PALM_FINISH1") != "0")
                    Load("Tennis/Premium/TennisResortPalmFinish1");
            }
            static int Role(string name)
            {
                if(name.Contains("BARK"))return 0;if(name.Contains("LEAF_DARK"))return 1;
                if(name.Contains("LEAF_MID"))return 2;if(name.Contains("LEAF_LIGHT"))return 3;
                if(name.Contains("CORAL"))return 4;if(name.Contains("GOLD"))return 5;return 6;
            }
            public void Add(string name,Vector3 p,float radius,float height,float yaw)
            {
                bool tree=name=="PALM"||name=="PINE"||name=="CANOPY";
                bool foliage=!tree&&prototypes.ContainsKey(name+"_FAR");
                Append(name,p,radius,height,yaw,tree?treesNear:foliage?foliageNear:cells,tree?20:foliage?24:40);
                if(tree)Append(prototypes.ContainsKey(name+"_FAR")?name+"_FAR":name,p,radius,height,yaw,treesFar,20);
                else if(foliage)Append(name+"_FAR",p,radius,height,yaw,foliageFar,24);
            }
            void Append(string name,Vector3 p,float radius,float height,float yaw,Dictionary<Vector2Int,Cell> pockets,float cellSize)
            {
                if(!prototypes.TryGetValue(name,out var proto)||!proto.filter.sharedMesh)return;
                // Shared-material spatial cells carry the expensive sculpted crowns
                // only nearby; their actual far meshes preserve the same silhouette.
                var key=new Vector2Int(Mathf.FloorToInt(p.x/cellSize),Mathf.FloorToInt(p.z/cellSize));
                if(!pockets.TryGetValue(key,out var cell))pockets[key]=cell=new Cell();
                var mf=proto.filter;var source=mf.sharedMesh;var vertices=source.vertices;var normals=source.normals;var weight=source.colors;var leafPivots=source.uv2;
                var sourceColours=new Color[vertices.Length];var renderer=mf.GetComponent<Renderer>();
                var placement=Matrix4x4.TRS(p,Quaternion.Euler(0,yaw,0),new Vector3(radius,height,radius));
                var matrix=placement*proto.matrix;var normalMatrix=matrix.inverse.transpose;
                var leafNormalMatrix=Matrix4x4.Rotate(Quaternion.Euler(0,yaw,0))*proto.matrix.inverse.transpose;
                int start=cell.vertices.Count;
                for(int sub=0;sub<source.subMeshCount;sub++)
                {
                    int role=renderer&&sub<renderer.sharedMaterials.Length&&renderer.sharedMaterials[sub]?Role(renderer.sharedMaterials[sub].name):Mathf.Min(sub,6);
                    foreach(int index in source.GetTriangles(sub))
                    {
                        var colour=TennisResortMaterials.BotanicalColour(Palette[role],role);colour.a=weight.Length==vertices.Length?weight[index].r:(role>0&&role<6?1:0);
                        sourceColours[index]=colour;cell.triangles.Add(start+index);
                    }
                }
                for(int i=0;i<vertices.Length;i++)
                {
                    var point=proto.matrix.MultiplyPoint3x4(vertices[i]);
                    bool leaf=leafPivots.Length==vertices.Length&&leafPivots[i].x>=0;
                    if(leaf)point.y=leafPivots[i].x+(point.y-leafPivots[i].x)*radius/Mathf.Max(height,.001f);
                    cell.vertices.Add(placement.MultiplyPoint3x4(point));
                    cell.normals.Add(normals.Length==vertices.Length?(leaf?leafNormalMatrix:normalMatrix).MultiplyVector(normals[i]).normalized:Vector3.up);
                    cell.colours.Add(sourceColours[i]);cell.pivots.Add(new Vector2(p.x,p.z));
                }
            }
            public void Build(Transform root)
            {
                const string key="Tennis spatial botanical vertex palette";
                if(!surfaces.TryGetValue(key,out var material)||!material)
                {
                    var shader=Resources.Load<Shader>("Tennis/Shaders/TennisBotanical");
                    material=shader?new Material(shader){name=key,enableInstancing=true}:Surface(key,Color.white,.16f);
                    material.SetColor("_BaseColor",Color.white);if(material.HasProperty("_Gloss"))material.SetFloat("_Gloss",.18f);
                    surfaces[key]=material;
                }
                TennisResortMaterials.Botanical(material);
                Renderer BuildCell(Transform parent,string name,Vector2Int key,Cell cell,bool shadows=true)
                {
                    var mesh=new Mesh{name=name+key,indexFormat=IndexFormat.UInt32};
                    mesh.SetVertices(cell.vertices);mesh.SetNormals(cell.normals);mesh.SetColors(cell.colours);mesh.SetUVs(2,cell.pivots);mesh.SetTriangles(cell.triangles,0);mesh.RecalculateBounds();
                    var renderer=TennisVenueBuilder.MeshObject(parent,mesh.name,mesh,material,shadows).GetComponent<Renderer>();renderer.receiveShadows=true;return renderer;
                }
                foreach(var pair in cells)BuildCell(root,"Tennis foliage pocket ",pair.Key,pair.Value);
                foreach(var pair in treesNear)
                {
                    var group=new GameObject("Tennis tree LOD pocket "+pair.Key).transform;group.SetParent(root,false);
                    var near=BuildCell(group,"Tennis trees near ",pair.Key,pair.Value);
                    var far=BuildCell(group,"Tennis trees far ",pair.Key,treesFar[pair.Key],false);
                    var lod=group.gameObject.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.65f,new[]{near}),new LOD(.006f,new[]{far})});lod.RecalculateBounds();
                }
                foreach(var pair in foliageNear)
                {
                    var group=new GameObject("Tennis understory LOD pocket "+pair.Key).transform;group.SetParent(root,false);
                    var near=BuildCell(group,"Tennis foliage near ",pair.Key,pair.Value);
                    var far=BuildCell(group,"Tennis foliage far ",pair.Key,foliageFar[pair.Key],false);
                    var lod=group.gameObject.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.68f,new[]{near}),new LOD(.006f,new[]{far})});lod.RecalculateBounds();
                }
                Debug.Log("[TennisArt] static pockets="+cells.Count+"; 20m tree LOD pockets="+treesNear.Count+"; 24m understory LOD pockets="+foliageNear.Count+"; actual far meshes omit distant shadow passes");
            }
        }
    }

    /// One coherent breeze across the shared golf/tennis leaf kit. Leaf vertex weights
    /// leave the trunks / roots rigid and move only the tips by a few centimetres.
    public sealed class TennisVenueBreeze : MonoBehaviour
    {
        void LateUpdate()
        {
            float strength = TennisVenue.Current == TennisVenueKind.Skyscraper ? .065f : .038f;
            Shader.SetGlobalVector("_GolfWind", new Vector4(strength, 0, strength * .65f, Time.time));
        }
        void OnDisable() => Shader.SetGlobalVector("_GolfWind", Vector4.zero);
    }
}
