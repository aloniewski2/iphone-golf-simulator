using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GolfArcade.Course
{
    /// Shared postcard surface library adapted to the authored legacy material names.
    /// Owns only visual materials and masks; source meshes and collision data are untouched.
    public sealed class GolfCourseLook : MonoBehaviour
    {
        public static bool Enabled = true;
        public static readonly HashSet<int> DisabledHoles = new();
        public static bool Handles(int number) => Enabled && !DisabledHoles.Contains(number)
            && (number is >= 1 and <= 3 || number is >= 7 and <= 10 || number is >= 12 and <= 23);
        public static GolfCourseLook Current { get; private set; }
        public enum Surface { Fairway, Green, Tee, Fringe, Rough, Sand, Cliff, Rock, Masonry, Path, Water, Shallow, Surf, Fall, Lava, Basalt, Ash, Snow, Ice, Desert, Sandstone, Wood, Bark, Leaves, Paint, Smoke, Glass }
        readonly Dictionary<string, Material> materials = new();
        readonly List<Mesh> visualMeshes = new();
        Texture2D mowing;
        Vector4 mowingBounds;
        Hole hole;
        public Hole Hole => hole;
        bool Magma => hole.Theme == "magma";
        bool Volcanic => Magma || hole.Number is 10 or 16;

        public static GolfCourseLook Attach(Hole hole, GameObject owner)
        {
            if (!Handles(hole.Number)) return null;
            var look = owner.GetComponent<GolfCourseLook>() ?? owner.AddComponent<GolfCourseLook>();
            look.hole = hole; Current = look;
            look.BuildMowingMap();
            return look;
        }

        public static Surface? Role(string material, string renderer, bool magma)
        {
            string n = material.Replace(" (Instance)", "").Split('.')[0];
            // Postcard8/9/10 use LK_* names. Route their managed surfaces through
            // the same per-course hierarchy instead of the shared legacy tint cache.
            string legacy=n.Split('@')[0];
            if(legacy is "LK_GREEN" or "LK_FAIRWAY" or "LK_ROUGH" or "LK_SCRUB")
            {
                if(renderer.StartsWith("TEE"))return Surface.Tee;
                if(renderer.Contains("FIRSTCUT")||renderer.Contains("APRON")||renderer.EndsWith("_LIP"))return Surface.Fringe;
                return legacy=="LK_GREEN"?Surface.Green:legacy=="LK_FAIRWAY"?Surface.Fairway:Surface.Rough;
            }
            if(legacy.StartsWith("LK_CLIFF"))return Surface.Cliff;
            if(legacy=="LK_BASALT")return Surface.Basalt;
            if(legacy=="LK_SAND")return Surface.Sand;

            if (renderer.Contains("FALL") && n == "MAT_GLASS") return Surface.Fall;
            if (n=="MAT_WINDOW"||n=="MAT_GLASS"||(n=="MAT_SLATE"&&(renderer.StartsWith("WINDMILL")||renderer.StartsWith("FARMHOUSE"))))return Surface.Glass;
            if (magma && (n.StartsWith("MAT_WATER") || n == "MAT_FOAM")) return Surface.Lava;
            if (n == "MAT_FAIRWAY" || n == "MAT_FAIRWAY_STRIPE") return Surface.Fairway;
            if (n == "MAT_GREEN") return renderer.StartsWith("TEE") ? Surface.Tee : Surface.Green;
            if (n == "MAT_FIRSTCUT" || n == "MAT_BUNKER_LIP") return Surface.Fringe;
            if (n is "MAT_ROUGH" or "MAT_ROUGH_JUNGLE" or "MAT_MOSS") return Surface.Rough;
            if (n is "MAT_SAND" or "MAT_SAND_BLACK" or "MAT_PUMICE") return Surface.Sand;
            if (n.StartsWith("MAT_CLIFF")) return Surface.Cliff;
            if (n.StartsWith("MAT_BASALT")) return Surface.Basalt;
            if (n.StartsWith("MAT_REDROCK")) return Surface.Sandstone;
            if (n.StartsWith("MAT_ROCK")) return Surface.Rock;
            if (n.StartsWith("MAT_TEMPLE") || n is "MAT_STONE" or "MAT_CHALK" or "MAT_WALL") return Surface.Masonry;
            if (n.StartsWith("MAT_PATH")) return Surface.Path;
            if (n == "MAT_WATER_SHALLOW") return Surface.Shallow;
            if (n == "MAT_WATER") return Surface.Water;
            if (n == "MAT_FOAM") return renderer.Contains("FALL") ? Surface.Fall : Surface.Surf;
            if (n.StartsWith("MAT_LAVA")) return Surface.Lava;
            if (n == "MAT_ROUGH_ASH" || n == "MAT_COAL") return Surface.Ash;
            if (n == "MAT_SNOW") return Surface.Snow;
            if (n.StartsWith("MAT_ICE")) return Surface.Ice;
            if (n == "MAT_DESERT") return Surface.Desert;
            if (n is "MAT_PALM_TRUNK" or "MAT_BARK" or "MAT_LOG" or "MAT_LOG_DARK" or "MAT_DEAD_WOOD") return Surface.Bark;
            if (n.StartsWith("MAT_WOOD") || n.StartsWith("MAT_ROOF") || n == "MAT_SLATE") return Surface.Wood;
            if (n.StartsWith("MAT_TREE") || n is "MAT_PALM_FROND" or "MAT_JUNGLE_LEAF" or "MAT_CACTUS" or "MAT_CACTUS_DARK" or "MAT_TUFT" or "MAT_HEDGE" or "MAT_TULIP_LEAF" or "MAT_VINE") return Surface.Leaves;
            if (n == "MAT_SMOKE") return Surface.Smoke;
            if (n.StartsWith("MAT_TULIP") || n.StartsWith("MAT_FLOWER") || n is "MAT_BLOOM" or "MAT_CANVAS" or "MAT_COCONUT" or "MAT_RED_PAINT" or "MAT_WHITE_PAINT" or "MAT_CARROT") return Surface.Paint;
            return null;
        }

        public Material Resolve(Material source, string renderer, Color? palette = null)
        {
            var role = Role(source.name, renderer, Magma);
            // FBX diffuse colours are already linearized by the importer. Reusing them as
            // shader Color properties darkens vegetation again. Use the course's sRGB palette.
            bool vegetation = role is Surface.Leaves or Surface.Bark or Surface.Wood or Surface.Paint or Surface.Sandstone
                || source.name.StartsWith("MAT_COCONUT")
                || role == Surface.Wood && (renderer.StartsWith("TREE") || renderer.StartsWith("SHRUB"));
            return role.HasValue ? Get(role.Value, vegetation ? palette ?? source.color.gamma : source.color, source.name) : null;
        }

        public Material Get(Surface role) => Get(role, Color.white, "");
        Material Get(Surface role, Color authored, string original)
        {
            // Authored colours matter for foliage, wood, flowers and the desert's stratified slots.
            bool useColor = role is Surface.Leaves or Surface.Paint or Surface.Wood or Surface.Bark or Surface.Sandstone;
            string key = role + (useColor ? ":" + ColorUtility.ToHtmlStringRGBA(authored) : "")
                + (original is "MAT_SAND_BLACK" or "MAT_PUMICE" || original.StartsWith("MAT_TEMPLE") ? original : "");
            if (materials.TryGetValue(key, out var cached)) return cached;
            if(role==Surface.Ice){
                var ice=new Material(Resources.Load<Shader>("Course/Shaders/GolfIce")){name="Course "+hole.Number+" clear glacier ice",enableInstancing=true};
                ice.SetColor("_DeepColor",new Color(.11f,.39f,.48f));ice.SetColor("_ShallowColor",new Color(.42f,.73f,.77f));
                materials.Add(key,ice);return ice;
            }
            if(role==Surface.Glass){
                var glass=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Course "+hole.Number+" reflective window",enableInstancing=true};
                glass.SetColor("_BaseColor",new Color(.065f,.19f,.25f));glass.SetFloat("_Metallic",.12f);glass.SetFloat("_Smoothness",.82f);
                glass.EnableKeyword("_EMISSION");glass.SetColor("_EmissionColor",new Color(.02f,.055f,.075f));materials.Add(key,glass);return glass;
            }
            string baseName = role switch
            {
                Surface.Fairway or Surface.Fringe => "LK_FAIRWAY",
                Surface.Green or Surface.Tee => "LK_GREEN",
                Surface.Sand or Surface.Desert or Surface.Snow or Surface.Ice => "LK_SAND",
                Surface.Cliff => "LK_CLIFF", Surface.Rock or Surface.Wood or Surface.Bark or Surface.Leaves or Surface.Paint => "LK_ROCK",
                Surface.Sandstone => "LK_CLIFF", Surface.Masonry => "LK_MASONRY", Surface.Path => "LK_PATH",
                Surface.Water => "LK_WATER", Surface.Shallow => "LK_WATER_SHALLOW", Surface.Surf => "LK_SURF", Surface.Fall => "LK_FALL",
                Surface.Lava => "LK_LAVA", Surface.Basalt => "LK_BASALT", Surface.Smoke => "LK_SMOKE", _ => "LK_ROUGH"
            };
            var m = new Material(GolfLook.Get(baseName)) { name = "Course " + hole.Number + " " + key, enableInstancing = true };
            materials.Add(key, m);
            if (role is Surface.Water or Surface.Shallow)
            {
                // The shallow shelves have their own geometry. Equal gradient endpoints keep
                // them shallow even hundreds of yards away from the tee/world origin.
                if (hole.Number == 17) { m.SetColor("_Shallow", new Color(.14f,.31f,.38f)); m.SetColor("_Deep",new Color(.035f,.12f,.22f)); m.SetColor("_Sky",new Color(.15f,.31f,.43f)); }
                if (hole.Number is 19 or 20) { m.SetColor("_Shallow",new Color(.09f,.33f,.29f)); m.SetColor("_Deep",new Color(.035f,.19f,.22f)); m.SetColor("_Sky",new Color(.10f,.31f,.34f)); }
                if (role == Surface.Shallow) m.SetColor("_Deep",m.GetColor("_Shallow"));
                if(hole.Number!=17){ m.SetColor("_Shallow",new Color(.055f,.59f,.62f));m.SetColor("_Deep",new Color(.025f,.29f,.45f));m.SetColor("_Sky",new Color(.28f,.53f,.70f)); }
                if(role==Surface.Shallow)m.SetColor("_Deep",m.GetColor("_Shallow"));
                m.SetVector("_WaterOrigin",new Vector4((float)((hole.Tee.X+hole.Pin.X)*.5),0,(float)((hole.Tee.D+hole.Pin.D)*.5),0));
                m.SetFloat("_Sparkle",role == Surface.Shallow ? .7f : 1.35f);
                return m;
            }
            if (role is Surface.Surf or Surface.Fall or Surface.Smoke)
            {
                m.SetFloat("_VertexAlpha",0); m.SetFloat("_EdgeFade",0);
                if (role == Surface.Surf) { m.SetFloat("_VertexAlpha",1); m.SetFloat("_AlphaGain",.55f); m.SetFloat("_AlphaPower",1.4f); m.SetTextureScale("_MainTex",new Vector2(1,1)); }
                if (role == Surface.Smoke) { m.SetFloat("_EdgeFade",.16f); m.SetFloat("_SoftFade",8); }
                return m;
            }
            if (role == Surface.Lava) return m;
            m.DisableKeyword("_GOLF_SAND");
            if (role is Surface.Sand or Surface.Desert) m.EnableKeyword("_GOLF_SAND");
            m.SetFloat("_Surface",role switch { Surface.Fairway => 1, Surface.Green => 2, Surface.Tee => 3, Surface.Fringe => 4, Surface.Sand or Surface.Desert => 5, _ => 0 });
            m.SetFloat("_WorldUV",1); m.SetFloat("_Bands",0.004f);
            m.SetFloat("_TileYards",(role is Surface.Green or Surface.Tee ? 6 : role is Surface.Sand ? 4 : role is Surface.Rough ? 12 : 10) / .9144f);
            m.SetFloat("_FollowCourse",role == Surface.Fairway ? 1 : 0);
            m.SetTexture("_MowingMap",mowing); m.SetVector("_MowingBounds",mowingBounds);
            var d = new Vector3((float)(hole.Pin.X-hole.Tee.X),0,(float)(hole.Pin.D-hole.Tee.D)).normalized;
            m.SetVector("_StripeDirection",new Vector4(d.z,0,-d.x,0));
            m.SetColor("_BaseColor",new Color(.80f,.80f,.80f));
            if (role is Surface.Fairway or Surface.Green or Surface.Tee or Surface.Fringe or Surface.Rough)
            {
                var tint = hole.Number == 19 ? new Color(.64f,.77f,.73f) : hole.Number == 17 ? new Color(.78f,.82f,.86f) : new Color(.64f,.78f,.69f);
                if (Volcanic) tint = new Color(.62f,.85f,.62f);
                if (role == Surface.Fringe) tint *= .86f;
                m.SetColor("_BaseColor",tint);
            }
            if (role == Surface.Sand) { m.SetColor("_BaseColor",original == "MAT_SAND_BLACK" ? new Color(.27f,.29f,.32f) : new Color(.73f,.76f,.79f)); }
            if (role == Surface.Basalt)
            {
                m.SetColor("_BaseColor",Volcanic ? new Color(.72f,.82f,.94f) : new Color(.95f,.98f,1));
                m.SetFloat("_Cap",0); m.SetFloat("_RockMipBias",.25f);
                m.SetFloat("_EmissionEnabled",Volcanic ? 1 : 0);
                m.SetVector("_EmissionColor",new Vector4(.09f,.028f,.003f,1));
            }
            m.SetFloat("_TriplanarNormals",1);
            if (role is Surface.Rock or Surface.Cliff or Surface.Masonry) {
                m.SetFloat("_PaletteMode",1);m.SetFloat("_DetailContrast",.80f);m.SetColor("_BaseColor",Color.white);
                bool temple=hole.Number==19&&role==Surface.Masonry;
                m.SetColor("_LowColor",temple?new Color(.49f,.43f,.29f):new Color(.38f,.41f,.38f));
                m.SetColor("_HighColor",temple?new Color(.78f,.70f,.48f):new Color(.70f,.70f,.60f));
                m.SetFloat("_RockScale",.09f);m.SetFloat("_StrataStrength",.035f);m.SetFloat("_RockMipBias",.5f);m.SetFloat("_BumpScale",.75f);
            }
            if(role==Surface.Basalt&&!Volcanic){
                m.SetFloat("_PaletteMode",1);m.SetFloat("_DetailContrast",.7f);m.SetColor("_BaseColor",Color.white);
                m.SetColor("_LowColor",new Color(.37f,.40f,.39f));m.SetColor("_HighColor",new Color(.70f,.70f,.60f));m.SetFloat("_Basalt",.15f);
            }
            string texture = role switch { Surface.Snow => "Snow", Surface.Ice => "Ice", Surface.Ash => "Ash", Surface.Sandstone => "Sandstone", Surface.Desert => "Desert", Surface.Wood => "Wood", Surface.Bark => "Bark", Surface.Leaves => "Leaves", _ => null };
            if (texture != null)
            {
                var color = Texture(texture + "_C"); var normal = Texture(texture + "_N");
                m.SetTexture("_BaseMap",color); m.SetTexture("_BumpMap",normal); m.SetFloat("_NormalEnabled",normal ? 1 : 0);
                m.SetFloat("_EmissionEnabled",0); m.SetFloat("_SheenFromAlpha",0); m.SetFloat("_Cap",0);
                m.SetFloat("_Smoothness",role == Surface.Ice ? .55f : .10f);
                m.SetFloat("_Bands",0); m.SetFloat("_BumpScale",role == Surface.Ice ? .35f : .65f);
                m.SetFloat("_RockScale",role is Surface.Leaves or Surface.Bark or Surface.Wood ? .45f : .12f);
                m.SetFloat("_StrataStrength",role == Surface.Sandstone ? .075f : 0);
                m.SetFloat("_RockMipBias",.5f);
                if(role == Surface.Ice) m.SetFloat("_TileYards",32f);
                m.SetColor("_BaseColor",useColor ? authored * .85f : new Color(.82f,.84f,.86f));
            }
            if (role == Surface.Paint)
            {
                m.SetTexture("_BaseMap",Texture2D.whiteTexture); m.SetFloat("_NormalEnabled",0); m.SetFloat("_Cap",0); m.SetFloat("_StrataStrength",0);
                m.SetColor("_BaseColor",authored*.82f);
            }
            if (role == Surface.Wood && original.Contains("ROOF")) m.SetFloat("_RockScale",.20f);
            if (role == Surface.Leaves)
            {
                m.SetFloat("_Cull",(float)CullMode.Off);
                m.SetFloat("_Foliage",1);
                m.SetFloat("_BumpScale",.25f);
                m.SetFloat("_Wrap",.6f);
            }
            if(role is Surface.Cliff or Surface.Rock or Surface.Masonry or Surface.Basalt){
                string geology=Volcanic?"ResortBasalt":"Limestone";
                var color=Resources.Load<Texture2D>("Course/Resort/"+geology+"_C");var normal=Resources.Load<Texture2D>("Course/Resort/"+geology+"_N");
                if(color)m.SetTexture("_BaseMap",color);if(normal)m.SetTexture("_BumpMap",normal);
                m.SetFloat("_NormalEnabled",normal?1:0);m.SetFloat("_BumpScale",.7f);m.SetFloat("_RockScale",.12f);m.SetFloat("_RockMipBias",.1f);m.SetFloat("_StrataStrength",.02f);
                if(Volcanic){
                    m.SetFloat("_PaletteMode",1);m.SetFloat("_DetailContrast",.65f);m.SetColor("_BaseColor",Color.white);
                    m.SetColor("_LowColor",new Color(.16f,.19f,.23f));m.SetColor("_HighColor",new Color(.33f,.35f,.37f));m.SetFloat("_Basalt",0);
                    m.SetFloat("_EmissionEnabled",1);m.SetVector("_EmissionColor",new Vector4(3.2f,.52f,.018f,1));
                }
            }
            if(!Volcanic&&role is Surface.Cliff or Surface.Basalt or Surface.Rock){
                var carved=Resources.Load<Texture2D>("Course/Resort/GeologyLimestone_C");
                if(carved)m.SetTexture("_BaseMap",carved);
                m.SetFloat("_EmissionEnabled",0);m.SetFloat("_PaletteMode",0);m.SetColor("_BaseColor",new Color(.83f,.86f,.90f));m.SetFloat("_RockScale",.13f);m.SetFloat("_BumpScale",.5f);m.SetFloat("_StrataStrength",0);m.SetFloat("_Basalt",0);m.SetFloat("_HeightStrength",.70f);
                if(hole.Number!=18){
                    m.SetFloat("_GeologicalMacro",.12f);m.SetFloat("_RockScale",.065f);m.SetFloat("_RockMipBias",.65f);
                    m.SetFloat("_PaletteMode",1);m.SetColor("_BaseColor",Color.white);m.SetFloat("_DetailContrast",.75f);m.SetFloat("_PaletteDetail",.24f);
                    m.SetColor("_LowColor",new Color(.34f,.33f,.28f));m.SetColor("_HighColor",new Color(.77f,.73f,.62f));
                    m.SetFloat("_HeightStrength",.27f);m.SetFloat("_BumpScale",.56f);
                }
                if(role==Surface.Cliff&&hole.Number!=18)m.SetFloat("_WetFoot",1);
            }
            if(role==Surface.Snow){
                // The inherited sand material carries a beige palette. Snow has
                // its own cold-white response while retaining the source texture.
                m.SetFloat("_PaletteMode",1);m.SetColor("_BaseColor",Color.white);
                m.SetColor("_LowColor",new Color(.70f,.82f,.88f));m.SetColor("_HighColor",new Color(.94f,.98f,1));
                m.SetFloat("_DetailContrast",.35f);m.SetFloat("_Smoothness",.07f);m.SetFloat("_Bands",0);
            }
            if(role==Surface.Ash){
                m.SetFloat("_PaletteMode",1);m.SetColor("_BaseColor",Color.white);
                m.SetColor("_LowColor",new Color(.13f,.15f,.17f));m.SetColor("_HighColor",new Color(.26f,.26f,.23f));
                m.SetFloat("_DetailContrast",.40f);m.SetFloat("_Bands",0);
            }
            if(hole.Number==18&&role is Surface.Cliff or Surface.Rock or Surface.Sandstone){
                var warmth=role==Surface.Sandstone&&original.StartsWith("MAT_REDROCK")?authored:new Color(.78f,.42f,.22f);
                m.SetFloat("_PaletteMode",1);m.SetColor("_BaseColor",Color.white);
                m.SetColor("_LowColor",Color.Lerp(new Color(.32f,.16f,.10f),warmth,.50f));
                m.SetColor("_HighColor",Color.Lerp(warmth,new Color(.91f,.74f,.50f),.25f));
                m.SetFloat("_DetailContrast",.45f);m.SetFloat("_StrataStrength",.03f);m.SetFloat("_EmissionEnabled",0);
            }
            if(role==Surface.Masonry&&original.StartsWith("MAT_TEMPLE")){
                Color low=new(.61f,.44f,.28f),high=new(.92f,.74f,.51f);
                if(original.Contains("DARK")){low=new(.34f,.25f,.17f);high=new(.54f,.40f,.26f);}
                if(original.Contains("TRIM")){low=new(.72f,.57f,.39f);high=new(.98f,.85f,.64f);}
                if(original.Contains("BLOCK_B")){low=new(.65f,.49f,.33f);high=new(.94f,.79f,.58f);}
                if(original.Contains("RELIEF")){low=new(.43f,.31f,.19f);high=new(.68f,.51f,.33f);}
                if(original.Contains("RECESS")){low=new(.045f,.039f,.028f);high=new(.09f,.074f,.05f);}
                m.SetColor("_LowColor",low);m.SetColor("_HighColor",high);m.SetFloat("_DetailContrast",.45f);m.SetFloat("_RockScale",.35f);m.SetFloat("_BumpScale",.45f);m.SetFloat("_Wrap",.18f);m.SetFloat("_Smoothness",.18f);m.SetFloat("_HeightStrength",.15f);
            }
            GolfLook.ResortPalette(m,role,Volcanic,hole.Number==19);
            GolfTurfPalette.ApplyGround(m,hole,role);
            if(role==Surface.Sand&&original=="MAT_SAND_BLACK") {m.SetFloat("_PaletteMode",0);m.SetColor("_BaseColor",new Color(.25f,.27f,.30f));}
            return m;
        }

        static Texture2D Texture(string name)
        {
            var t = Resources.Load<Texture2D>("Course/Standard/" + name);
            if (!t) throw new InvalidOperationException("Missing standardized golf texture " + name);
            t.anisoLevel = 16; return t;
        }

        void BuildMowingMap()
        {
            if (mowing) return;
            const int size = 128;
            Vector2 lo = new(float.MaxValue,float.MaxValue), hi = new(float.MinValue,float.MinValue);
            foreach (var p in hole.Centerline) { var v = new Vector2((float)p.X,(float)p.D); lo = Vector2.Min(lo,v); hi = Vector2.Max(hi,v); }
            float margin = (float)Math.Max(hole.FairwayWidth, hole.GreenRadius * 2) + 24;
            lo -= Vector2.one * margin; hi += Vector2.one * margin;
            var extent = hi-lo; mowingBounds = new Vector4(lo.x,lo.y,1/extent.x,1/extent.y);
            var pixels = new Color[size*size];
            for (int y=0;y<size;y++) for (int x=0;x<size;x++)
            {
                var p = lo + new Vector2((x+.5f)*extent.x/size,(y+.5f)*extent.y/size);
                float best = float.MaxValue, walked = 0; Color result = default;
                for (int i=1;i<hole.Centerline.Length;i++)
                {
                    var a = new Vector2((float)hole.Centerline[i-1].X,(float)hole.Centerline[i-1].D);
                    var b = new Vector2((float)hole.Centerline[i].X,(float)hole.Centerline[i].D);
                    var edge = b-a; float length = edge.magnitude;
                    if (length < .001f) continue;
                    var along = edge/length; float t = Mathf.Clamp(Vector2.Dot(p-a,along),0,length);
                    var delta = p-(a+along*t); float distance = delta.sqrMagnitude;
                    if (distance < best) { best = distance; var right = new Vector2(along.y,-along.x); result = new Color(Vector2.Dot(p-a,right),(walked+Vector2.Dot(p-a,along)),right.x,right.y); }
                    walked += length;
                }
                pixels[y*size+x] = result;
            }
            mowing = new Texture2D(size,size,TextureFormat.RGBAFloat,false,true) { name = "Course mowing " + hole.Number, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            mowing.SetPixels(pixels); mowing.Apply(false,true);
        }

        /// Called after ground/obstacle extraction. Render-only UV repairs cannot change the collision source.
        public void FinishModel(GameObject model)
        {
#if UNITY_EDITOR
            var physicsToken=GolfVisualPhysicsGate.Begin(model,hole);
#endif
            GolfPostcardBasalt.Apply(model,hole);
            GolfLandmarkFinish.Apply(model,hole,this);
            var resort = GolfResortDress.Apply(model,hole);
            GolfStoneFinish.Apply(model);
            var palms = model.GetComponent<GolfCoursePalms>();
            if (palms && (!resort || !resort.ReplacedAuthoredPlants)) palms.Rebuild();
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!mf.sharedMesh || !mf.sharedMesh.isReadable || !mf.TryGetComponent(out Renderer r)) continue;
                bool sheets = false;
                foreach (var m in r.sharedMaterials) if (m && m.shader.name == "GolfArcade/GolfSurf") sheets = true;
                if (!sheets) continue;
                var mesh = Instantiate(mf.sharedMesh); mesh.name += " visual UV"; visualMeshes.Add(mesh);
                var uv = new Vector2[mesh.vertexCount]; var vertices = mesh.vertices;
                bool fall = mf.name.Contains("FALL"), smoke = mf.name.Contains("SMOKE");
                var bounds = mesh.bounds;
                for (int i=0;i<uv.Length;i++)
                {
                    var p = mf.transform.TransformPoint(vertices[i]);
                    uv[i] = fall ? new Vector2(p.x*.3f+p.z*.3f,p.y*.2f) : new Vector2(p.x*.20f,p.z*.20f);
                    if (smoke) uv[i] = new Vector2((vertices[i].x-bounds.min.x)/Mathf.Max(bounds.size.x,.001f),(vertices[i].z-bounds.min.z)/Mathf.Max(bounds.size.z,.001f));
                }
                mesh.uv = uv;
                if (!fall && !smoke) GolfCourseFoam.Feather(mesh,mf.transform);
                mf.sharedMesh = mesh; r.shadowCastingMode = ShadowCastingMode.Off;
            }
            GolfCourseFringe.Dress(model,hole);
            GolfCoastalTurf.Apply(model,hole);
            GolfSurfaceEdges.Apply(model);
            GolfSurfaceBatching.Apply(model);
            GolfPlantInstances.Apply(model);
#if UNITY_EDITOR
            GolfVisualPhysicsGate.End(physicsToken,model,hole);
#endif
        }

        public void DressProcedural(GameObject root)
        {
#if UNITY_EDITOR
            var physicsToken=GolfVisualPhysicsGate.Begin(root,hole);
#endif
            GolfSurfaceEdges.Reset();
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>())
            {
                string n = r.name.ToLowerInvariant();
                Surface? role = n.StartsWith("rough") ? Surface.Rough : n.StartsWith("fairway fringe") ? Surface.Fringe : n.StartsWith("fairway") ? Surface.Fairway : n.StartsWith("green") ? Surface.Green : n.StartsWith("tee") ? Surface.Tee : n.StartsWith("bunker") ? Surface.Sand : n.StartsWith("water") ? Surface.Water : null;
                if (role.HasValue) r.sharedMaterial = Get(role.Value);
            }
            GolfResortDress.Apply(root,hole);
#if UNITY_EDITOR
            GolfVisualPhysicsGate.End(physicsToken,root,hole);
#endif
        }

        void OnDestroy()
        {
            foreach (var m in materials.Values) Release(m);
            foreach (var m in visualMeshes) Release(m);
            Release(mowing);
            if (Current == this) Current = null;
        }
        static void Release(UnityEngine.Object obj)
        {
            if(!obj)return;
            if(Application.isPlaying)Destroy(obj);else DestroyImmediate(obj);
        }
    }
}
