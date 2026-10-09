using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GolfArcade.Course
{
    /// Render-only botanical replacement and secondary landscape detail. Attach after
    /// ObstacleScan and Ground sampling: original gameplay meshes/markers remain intact.
    public sealed class GolfResortDress : MonoBehaviour
    {
        sealed class Template { public Mesh mesh; public Matrix4x4 toRoot; public int[] paletteSlots; public bool preserveLeafRim; }
        sealed class Batch { public Material material; public string lodKind; public bool instanced; public readonly List<CombineInstance> pieces = new(), farPieces = new(); }
        readonly List<Mesh> meshes = new();
        readonly List<Material> materials = new();
        readonly Dictionary<string, Batch> batches = new();
        readonly Dictionary<string, Template> templates = new();
        readonly Dictionary<string,Mesh> proportioned = new();
        readonly Dictionary<Mesh,Mesh> painted = new();
        Material[] palette;
        Material botanical;
        Color[] paletteColors;
        Hole hole;
        int plants, triangles;
        public int PlantCount => plants;
        public int TriangleCount => triangles;
        public int CliffModules { get; private set; }
        public bool ReplacedAuthoredPlants { get; private set; }

        public static GolfResortDress Apply(GameObject model,Hole hole)
        {
            var existing=model.GetComponent<GolfResortDress>(); if(existing)return existing;
            if(hole.Number==12){
                // Coastal pilot uses one authored island mass and a dedicated
                // botanical composition; legacy scatter/erosion stays on other holes.
                var coastal=model.AddComponent<GolfResortDress>();coastal.hole=hole;
                coastal.ReplacedAuthoredPlants=true;
                GolfCoastalCliff12.Apply(model,hole);
                GolfCoastalBotany.Apply(model,hole);
                GolfCoastalTurf.Apply(model,hole);coastal.CoastalWater(model);return coastal;
            }
            var kit=Resources.Load<GameObject>("Course/Resort/BotanicalKit");
            if(!kit){Debug.LogError("Missing golf botanical kit");return null;}
            var owner=model.AddComponent<GolfResortDress>();owner.hole=hole;
            foreach(var mf in kit.GetComponentsInChildren<MeshFilter>())
                if(mf.sharedMesh&&mf.sharedMesh.isReadable) {
                    var renderer=mf.GetComponent<Renderer>();var sourceMaterials=renderer?renderer.sharedMaterials:Array.Empty<Material>();
                    var slots=new int[mf.sharedMesh.subMeshCount];
                    for(int i=0;i<slots.Length;i++)slots[i]=i<sourceMaterials.Length&&sourceMaterials[i]?PaletteIndex(sourceMaterials[i].name):Mathf.Min(i,6);
                    owner.templates[mf.name]=new Template{mesh=mf.sharedMesh,toRoot=kit.transform.worldToLocalMatrix*mf.transform.localToWorldMatrix,paletteSlots=slots};
                }
            if(owner.templates.Count==0){Debug.LogError("Golf botanical kit requires readable meshes");return owner;}
            owner.InstallTempleCanopyCandidate();owner.CreateMaterials();owner.ReplaceMarkers();owner.ScatterRough();owner.DressShore();owner.BuildCliffForms();GolfCliffSculpt.Apply(model,hole);if(hole.Number is >=1 and <=3)owner.DressMeadow();owner.BuildBatches();if(hole.Number==9)owner.CoastalWater(model);
            Debug.Log($"[GolfResortDress] hole {hole.Number}: {owner.plants} botanical assets, {owner.triangles} triangles in {owner.batches.Count} bounded batches; original physics retained");
            return owner;
        }

        // Accepted temple pilot; other tropical courses remain explicit review candidates.
        // Exact existing marker stream; no new gameplay obstacles.
        void InstallTempleCanopyCandidate()
        {
            string review=System.Environment.GetEnvironmentVariable("VISUAL_GOLF_CANOPY");
            bool curved=System.Environment.GetEnvironmentVariable("VISUAL_GOLF_CANOPY22")=="1";
            if(hole.Number is not (16 or 19 or 21 or 22 or 23)||(!curved&&(review=="0"||(hole.Number!=19&&review!="1"))))return;
            var kit=Resources.Load<GameObject>(curved?"Course/Resort/GolfTropicalCanopy22":"Course/Resort/GolfTempleCanopy20");
            if(!kit){Debug.LogError("[GolfResortDress] Missing temple canopy candidate");return;}
            int replaced=0;
            foreach(var mf in kit.GetComponentsInChildren<MeshFilter>())
            {
                if((mf.name!="PALM"&&mf.name!="PALM_FAR")||!mf.sharedMesh||!mf.sharedMesh.isReadable)continue;
                var mats=mf.GetComponent<Renderer>().sharedMaterials;var slots=new int[mf.sharedMesh.subMeshCount];
                for(int i=0;i<slots.Length;i++)slots[i]=i<mats.Length&&mats[i]?PaletteIndex(mats[i].name):Mathf.Min(i,6);
                templates[mf.name]=new Template{mesh=mf.sharedMesh,toRoot=kit.transform.worldToLocalMatrix*mf.transform.localToWorldMatrix,paletteSlots=slots,preserveLeafRim=true};
                replaced++;
            }
            if(replaced!=2)throw new System.InvalidOperationException("Temple canopy candidate requires exact PALM/PALM_FAR");
            Debug.Log("[GolfResortDress] Temple canopy pilot: two render prototypes replaced; original PLANT marker placement and obstacle data retained");
        }

        void CoastalWater(GameObject model)
        {
            var copies=new Dictionary<Material,Material>();
            foreach(var renderer in model.GetComponentsInChildren<Renderer>()){
                var slots=renderer.sharedMaterials;bool changed=false;
                for(int i=0;i<slots.Length;i++){
                    var source=slots[i];if(!source||source.shader.name!="GolfArcade/GolfOcean")continue;
                    if(!copies.TryGetValue(source,out var water)){
                        water=new Material(source){name=source.name+" coastal daylight"};
                        water.SetColor("_Shallow",new Color(.06f,.63f,.70f));
                        water.SetColor("_Deep",new Color(.025f,.32f,.59f));
                        water.SetColor("_Sky",new Color(.33f,.68f,.91f));
                        water.SetFloat("_WaveScale",.42f);water.SetFloat("_Sparkle",2);
                        water.SetFloat("_ResortPop",1);materials.Add(water);copies[source]=water;
                    }
                    slots[i]=water;changed=true;
                }
                if(changed)renderer.sharedMaterials=slots;
            }
        }

        static int PaletteIndex(string name)
        {
            if(name.Contains("LEAF_DARK"))return 1;if(name.Contains("LEAF_MID"))return 2;if(name.Contains("LEAF_LIGHT"))return 3;
            if(name.Contains("CORAL"))return 4;if(name.Contains("GOLD"))return 5;if(name.Contains("STONE"))return 6;return 0;
        }

        void CreateMaterials()
        {
            var shader=Resources.Load<Shader>("Course/Shaders/GolfBotanical");
            if(!shader)throw new InvalidOperationException("Missing golf botanical shader");
            Color[] colors={new(.46f,.32f,.18f),new(.12f,.33f,.16f),new(.25f,.46f,.18f),new(.47f,.62f,.23f),new(.95f,.40f,.26f),new(.97f,.73f,.22f),new(.54f,.55f,.46f)};
            bool volcanic=hole.Number==16||hole.Number>=21;
            if(volcanic){colors[0]=new(.28f,.24f,.20f);colors[1]=new(.15f,.28f,.15f);colors[2]=new(.32f,.42f,.19f);colors[3]=new(.52f,.55f,.24f);colors[6]=new(.21f,.24f,.27f);}
            if(hole.Number==17){colors[1]=new(.13f,.29f,.24f);colors[2]=new(.21f,.38f,.29f);colors[3]=new(.32f,.47f,.34f);colors[6]=new(.57f,.65f,.68f);}
            palette=new Material[colors.Length];
            paletteColors=colors;
            for(int i=0;i<colors.Length;i++){
                var m=new Material(shader){name=$"Golf resort {hole.Number} palette {i}",enableInstancing=true};
                m.SetColor("_BaseColor",colors[i]);m.SetFloat("_Gloss",i is >=1 and <=3?.26f:.08f);m.SetFloat("_Leaf",i==0||i==6?0:1);
                m.SetFloat("_Cull",i==0||i==6?(float)CullMode.Back:(float)CullMode.Off);palette[i]=m;materials.Add(m);
            }
            botanical=new Material(shader){name=$"Golf resort {hole.Number} vertex palette",enableInstancing=true};
            botanical.SetColor("_BaseColor",Color.white);botanical.SetFloat("_Gloss",.19f);botanical.SetFloat("_Leaf",1);botanical.SetFloat("_Cull",(float)CullMode.Off);botanical.SetFloat("_VertexPalette",1);materials.Add(botanical);
        }

        Mesh Paint(Mesh source,Template template)
        {
            if(painted.TryGetValue(source,out var result))return result;
            // Material colours become per-vertex colour so a whole bounded planting
            // cell renders in one pass. Wind amplitude moves from red to alpha.
            var positions=source.vertices;var normals=source.normals;var sourceColors=source.colors;
            var outPositions=new List<Vector3>();var outNormals=new List<Vector3>();var outColors=new List<Color>();var outIndices=new List<int>();
            for(int sub=0;sub<source.subMeshCount;sub++){
                var map=new Dictionary<int,int>();int slot=template.paletteSlots[sub];var color=paletteColors[slot];
                // Evergreen branches share a quieter hue hierarchy than broad
                // resort foliage; strong yellow alternation made tiered pillars.
                if(template.mesh.name.Contains("PINE")&&slot is >=1 and <=3){
                    color=slot==1?new Color(.14f,.33f,.20f):slot==2?new Color(.22f,.40f,.21f):new Color(.28f,.46f,.22f);
                    if(hole.Number==17)color=Color.Lerp(color,new Color(.24f,.41f,.35f),.28f);
                }
                if(template.mesh.name.StartsWith("PALM")&&slot is >=1 and <=3){
                    color=slot==1?new Color(.17f,.36f,.21f):slot==2?new Color(.26f,.46f,.23f):new Color(.34f,.54f,.28f);
                    if(hole.Number==16||hole.Number>=21)color=Color.Lerp(color,new Color(.32f,.37f,.22f),.22f);
                }
                if((template.mesh.name.StartsWith("SHRUB")||template.mesh.name=="BUSH_BROAD")&&slot is >=1 and <=3){
                    color=slot==1?new Color(.14f,.30f,.17f):slot==2?new Color(.23f,.41f,.21f):new Color(.33f,.50f,.25f);
                    if(hole.Number==17)color=Color.Lerp(color,new Color(.24f,.41f,.35f),.25f);
                    if(hole.Number==16||hole.Number>=21)color=Color.Lerp(color,new Color(.29f,.35f,.20f),.20f);
                }
                color=color.linear;
                foreach(int old in source.GetTriangles(sub)){
                    if(!map.TryGetValue(old,out int next)){
                        next=outPositions.Count;map.Add(old,next);outPositions.Add(positions[old]);outNormals.Add(old<normals.Length?normals[old]:Vector3.up);
                        color.a=old<sourceColors.Length?sourceColors[old].r:0;outColors.Add(color);
                    }
                    outIndices.Add(next);
                }
            }
            result=new Mesh{name=source.name+" vertex palette",indexFormat=IndexFormat.UInt32};result.SetVertices(outPositions);result.SetNormals(outNormals);result.SetColors(outColors);result.SetTriangles(outIndices,0);result.RecalculateBounds();
            painted.Add(source,result);meshes.Add(result);return result;
        }

        static void SmoothConnectedNormals(Mesh mesh)
        {
            // Adjacent frond sections have distinct UV vertices. Average only
            // coincident source positions so the continuous leaf catches a
            // smooth light response while all silhouettes remain unchanged.
            var vertices=mesh.vertices;var normals=mesh.normals;var sums=new Dictionary<Vector3Int,Vector3>();
            Vector3Int key(Vector3 p)=>new(Mathf.RoundToInt(p.x*100000),Mathf.RoundToInt(p.y*100000),Mathf.RoundToInt(p.z*100000));
            for(int i=0;i<vertices.Length;i++){var k=key(vertices[i]);sums.TryGetValue(k,out var sum);sums[k]=sum+normals[i];}
            for(int i=0;i<vertices.Length;i++)if(sums[key(vertices[i])].sqrMagnitude>.0001f)normals[i]=sums[key(vertices[i])].normalized;
            mesh.normals=normals;
        }

        Mesh PrepareAsset(string kind,Template template,Vector3 scale,bool paint)
        {
            Mesh asset=template.mesh;
            if((kind.StartsWith("PINE")||kind.StartsWith("PALM")||kind.StartsWith("OAK"))&&asset.uv2.Length==asset.vertexCount){
                string shapeKey=kind+"/"+Mathf.RoundToInt(scale.x/Mathf.Max(scale.y,.01f)*1000);
                if(!proportioned.TryGetValue(shapeKey,out asset)){
                    asset=Instantiate(template.mesh);asset.name=kind+" crown proportions";meshes.Add(asset);
                    var vertices=asset.vertices;var pivots=asset.uv2;var inverse=template.toRoot.inverse;
                    var preservedNormals=template.preserveLeafRim?asset.normals:null;
                    var crownNormalMatrix=(inverse*Matrix4x4.Scale(new Vector3(1,scale.x/Mathf.Max(scale.y,.01f),1))*template.toRoot).inverse.transpose;
                    for(int i=0;i<vertices.Length;i++)if(pivots[i].x>=0){
                        var position=template.toRoot.MultiplyPoint3x4(vertices[i]);float centre=pivots[i].x;
                        float proportion=scale.x/Mathf.Max(scale.y,.01f);
                        if(kind.StartsWith("PINE"))proportion=Mathf.Lerp(1,proportion,.55f);
                        position.y=centre+(position.y-centre)*proportion;
                        if(preservedNormals!=null&&preservedNormals.Length==vertices.Length){
                            preservedNormals[i]=crownNormalMatrix.MultiplyVector(preservedNormals[i]).normalized;
                        }
                        vertices[i]=inverse.MultiplyPoint3x4(position);
                    }
                    asset.vertices=vertices;
                    if(preservedNormals!=null&&preservedNormals.Length==vertices.Length)asset.normals=preservedNormals;
                    else {
                        asset.RecalculateNormals();
                        if(kind.StartsWith("PALM")||kind.StartsWith("PINE")||kind.StartsWith("OAK"))SmoothConnectedNormals(asset);
                    }
                    asset.RecalculateBounds();proportioned.Add(shapeKey,asset);
                }
            }
            return paint?Paint(asset,template):asset;
        }

        void Add(string kind,Vector3 point,Vector3 scale,float angle,Material overrideMaterial=null)
        {
            if(!templates.TryGetValue(kind,out var template))return;
            var asset=PrepareAsset(kind,template,scale,!overrideMaterial);
            var world=transform.worldToLocalMatrix*Matrix4x4.TRS(point,Quaternion.Euler(0,angle,0),scale);
            var matrix=world*template.toRoot;
            bool lod=(kind=="PALM"||kind=="PINE"||kind=="OAK")&&!overrideMaterial&&templates.ContainsKey(kind+"_FAR");
            Mesh far=null;Matrix4x4 farMatrix=matrix;
            if(lod){var low=templates[kind+"_FAR"];far=PrepareAsset(kind+"_FAR",low,scale,true);farMatrix=world*low.toRoot;}
            // Small tree cells get genuine runtime LOD: foreground articulated
            // crowns, cheaper distant crowns, one palette/material per cell.
            bool instanced=!overrideMaterial&&(kind.StartsWith("SHRUB_")||kind=="FERN"||kind.StartsWith("FLOWER_"));
            // Windmill's rigid broadleaf canopy shares one palette and no local
            // light selection; large batches retain its exact authored geometry.
            float cell=instanced||hole.Number==20&&kind=="CANOPY"?480:lod?48:120;int x=Mathf.FloorToInt(point.x/cell),z=Mathf.FloorToInt(point.z/cell);
            for(int sub=0;sub<asset.subMeshCount;sub++){
                int cost=(int)asset.GetIndexCount(sub)/3;if(cost==0)continue;
                var material=overrideMaterial?overrideMaterial:botanical;string key=$"{material.GetInstanceID()}/{x}/{z}/{(lod||instanced?kind:string.Empty)}";
                if(!batches.TryGetValue(key,out var batch)){batch=new Batch{material=material,lodKind=lod?kind:null,instanced=instanced};batches.Add(key,batch);}
                batch.pieces.Add(new CombineInstance{mesh=asset,subMeshIndex=sub,transform=matrix});triangles+=cost;
                if(lod)batch.farPieces.Add(new CombineInstance{mesh=far,subMeshIndex=0,transform=farMatrix});
            }
            plants++;
        }

        void ReplaceMarkers()
        {
            // All source tree/bush visual meshes are replaced only when the source includes
            // complete plant foot markers. No collider, source MeshFilter or active flag changes.
            var markers=new List<Transform>();
            foreach(var t in GetComponentsInChildren<Transform>(true))
                if(t.name.StartsWith("PLANT_")&&t.name.Split('_').Length>=5)markers.Add(t);
            if(markers.Count==0){ReplaceUnmarkedTrees();return;}
            bool tropical=hole.Number==19||hole.Number==16||hole.Number>=21;
            var rng=new System.Random(7700+hole.Number);
            int trees=0;
            foreach(var t in markers){
                var bits=t.name.Split('_');if(!int.TryParse(bits[3],out int cm)||!int.TryParse(bits[4],out int reach))continue;
                float sourceScale=(Mathf.Abs(t.lossyScale.x)+Mathf.Abs(t.lossyScale.y)+Mathf.Abs(t.lossyScale.z))/3;
                float height=cm*.01f*sourceScale,radius=reach*.01f*sourceScale;
                float angle=(float)rng.NextDouble()*360;
                if(bits[2]=="T"){
                    string asset=tropical?"PALM":hole.Number==20?"CANOPY":"PINE";
                    Add(asset,t.position,new Vector3(radius,height,radius),angle);trees++;
                } else if(bits[2]=="B"){
                    Add("SHRUB_"+(trees%2),t.position,new Vector3(radius,height*1.25f,radius),angle);
                    if(hole.Number!=17&&hole.Number!=18&&rng.NextDouble()<.38)
                        Add("FLOWER_CORAL",t.position+new Vector3(radius*.3f,0,radius*.2f),Vector3.one*Mathf.Min(radius,.9f),angle);
                } else if(bits[2]=="R"){
                    Add("CACTUS",t.position,new Vector3(radius,height,radius),angle);
                } else if(bits[2]=="S"){
                    Add("FERN",t.position,new Vector3(radius,height,radius),angle);
                }
            }
            // Some source trees are not marker-backed (e.g. landmark deadwood/cacti): retain them.
            if(trees>0||hole.Number==18){
                foreach(var r in GetComponentsInChildren<MeshRenderer>(true))
                    if(r.name=="TREES"||r.name.StartsWith("TREES.")||r.name=="SHRUBS")r.enabled=false;
                ReplacedAuthoredPlants=true;
            }
        }

        void ReplaceUnmarkedTrees()
        {
            // Legacy islands lack marker empties. Reuse the exact already-extracted obstacle
            // footprint and height so replacement foliage reads where the ball collides.
            bool hasTrees=false;
            foreach(var mf in GetComponentsInChildren<MeshFilter>(true))
                if(mf.name.StartsWith("TREE")||mf.name.StartsWith("SHRUB")){hasTrees=true;break;}
            if(!hasTrees||hole.Obstacles==null)return;
            int count=0;var rng=new System.Random(3051+hole.Number);
            foreach(var o in hole.Obstacles){
                if(o.Kind!=ObstacleKind.Tree&&o.Kind!=ObstacleKind.Bush)continue;
                float height=(float)(o.Top-o.Base),radius=(float)o.Radius;
                if(height<.15f||radius<.2f)continue;
                string kind=o.Kind==ObstacleKind.Bush?"SHRUB_"+(count%2):hole.Number==14?"CANOPY":"PINE";
                Add(kind,new Vector3((float)o.X,(float)o.Base,(float)o.D),new Vector3(radius,height,radius),(float)rng.NextDouble()*360);count++;
            }
            if(count==0)return;
            foreach(var r in GetComponentsInChildren<MeshRenderer>(true))
                if(r.name.StartsWith("TREE")||r.name.StartsWith("SHRUB"))r.enabled=false;
            ReplacedAuthoredPlants=true;
        }

        sealed class CliffNode
        {
            public Vector3 point,normal;public float low=float.MaxValue,high=float.MinValue;public int count;
        }
        void BuildCliffForms()
        {
            var nodes=new Dictionary<Vector2Int,CliffNode>();
            foreach(var c in GetComponentsInChildren<MeshCollider>()){
                if(!c.name.StartsWith("TERRAIN")||!c.sharedMesh||!c.sharedMesh.isReadable)continue;
                var vertices=c.sharedMesh.vertices;var indices=c.sharedMesh.triangles;
                for(int t=0;t<indices.Length;t+=3){
                    var a=c.transform.TransformPoint(vertices[indices[t]]);var b=c.transform.TransformPoint(vertices[indices[t+1]]);var d=c.transform.TransformPoint(vertices[indices[t+2]]);
                    var normal=Vector3.Cross(b-a,d-a).normalized;if(Mathf.Abs(normal.y)>.55f)continue;
                    Edge(a,b,normal);Edge(b,d,normal);Edge(d,a,normal);
                }
            }
            void Edge(Vector3 a,Vector3 b,Vector3 normal){
                if(Mathf.Abs(a.y-b.y)<2.5f||new Vector2(a.x-b.x,a.z-b.z).magnitude>6) return;
                var midpoint=(a+b)*.5f;var key=new Vector2Int(Mathf.RoundToInt(midpoint.x/5),Mathf.RoundToInt(midpoint.z/5));
                if(!nodes.TryGetValue(key,out var node)){node=new CliffNode();nodes.Add(key,node);}
                node.point+=midpoint;node.normal+=normal;node.low=Mathf.Min(node.low,Mathf.Min(a.y,b.y));node.high=Mathf.Max(node.high,Mathf.Max(a.y,b.y));node.count++;
            }
            var look=GolfCourseLook.Current;if(!look)return;
            bool volcanic=hole.Number==16||hole.Number>=21;
            var rock=look.Get(volcanic?GolfCourseLook.Surface.Basalt:hole.Number==18?GolfCourseLook.Surface.Sandstone:GolfCourseLook.Surface.Cliff);
            var rng=new System.Random(4111+hole.Number*53);int count=0,rimClusters=0;
            foreach(var node in nodes.Values){
                var point=node.point/node.count;var normal=node.normal.normalized;normal.y=0;normal.Normalize();
                float span=node.high-node.low;if(span<3)continue;
                // Outcrops occupy a few crevices in the continuous erosion shell. No
                // repeated brick-wall courses; shape/spacing/height are independent.
                if(CliffModules<95&&rng.NextDouble()<.34){
                    // Broad fractured slabs start at the authored cliff foot. The
                    // shell and offset shoulders overlap, rather than float in rows.
                    float height=Mathf.Min(span*(.62f+(float)rng.NextDouble()*.31f),30.0f);
                    var center=point+normal*(.40f+(float)rng.NextDouble()*.60f);
                    center.y=node.low-.30f;
                    float width=8.0f+(float)rng.NextDouble()*9.0f,depth=4.8f+(float)rng.NextDouble()*2.4f;
                    float heading=Mathf.Atan2(normal.x,normal.z)*Mathf.Rad2Deg-22+(float)rng.NextDouble()*44;
                    Add("BLUFF_"+(count%3),center,new Vector3(width,height,depth),heading,rock);CliffModules++;
                }
                count++;
                // Dense botanical edge mass grows inward from the actual authored cliff rim.
                if(hole.Number!=17&&hole.Number!=18&&rimClusters<60&&count%3==0){
                    var at=point-normal*1.5f;at.y=node.high;
                    var ray=new Ray(at+Vector3.up*5,Vector3.down);RaycastHit hit=default;bool found=false,overlay=false;
                    foreach(var c in GetComponentsInChildren<MeshCollider>()){
                        if(c.Raycast(ray,out var h,15)){
                            if(!c.name.StartsWith("TERRAIN")){overlay=true;break;}
                            if(h.normal.y>.7f){hit=h;found=true;}
                        }
                    }
                    if(found&&!overlay){
                        rimClusters++;
                        float radius=1.9f+(float)rng.NextDouble()*1.3f;
                        Add("SHRUB_"+(rimClusters%2),hit.point,new Vector3(radius,1.2f+(float)rng.NextDouble()*.65f,radius),(float)rng.NextDouble()*360);
                        Add("FERN",hit.point-normal*.65f,new Vector3(2.0f,1.1f,2.0f),(float)rng.NextDouble()*360);
                        if(count%4==0)Add("FLOWER_CORAL",hit.point+normal*.35f,Vector3.one*1.2f,(float)rng.NextDouble()*360);
                    }
                }
            }
            Debug.Log($"[GolfResortDress] hole {hole.Number}: {CliffModules} sparse irregular limestone/basalt outcrops");
        }

        void DressShore()
        {
            // Small stone forms break a straight seawall silhouette on its submerged lip.
            // Sampling the original cliff faces retains the authored island shape and height.
            var rng=new System.Random(11211+hole.Number*41);int placed=0;
            foreach(var c in GetComponentsInChildren<MeshCollider>()){
                if(!c.name.StartsWith("TERRAIN")||!c.sharedMesh||!c.sharedMesh.isReadable)continue;
                var vertices=c.sharedMesh.vertices;var indices=c.sharedMesh.triangles;
                int goal=hole.Number==19?75:hole.Number==12?55:30;
                for(int attempt=0;attempt<indices.Length/3&&placed<goal;attempt++){
                    int t=rng.Next(indices.Length/3)*3;
                    var a=c.transform.TransformPoint(vertices[indices[t]]);var b=c.transform.TransformPoint(vertices[indices[t+1]]);var d=c.transform.TransformPoint(vertices[indices[t+2]]);
                    var normal=Vector3.Cross(b-a,d-a).normalized;
                    if(Mathf.Abs(normal.y)>.40f||Mathf.Min(a.y,Mathf.Min(b.y,d.y))>3)continue;
                    var center=(a+b+d)/3;if(center.y<-.5f||center.y>7)continue;
                    float radius=1.3f+(float)rng.NextDouble()*2.7f;
                    var point=center+normal*.15f;point.y=Mathf.Max(-.2f,center.y-radius*.5f);
                    var look=GolfCourseLook.Current;
                    var stone=look?look.Get(hole.Number==18?GolfCourseLook.Surface.Sandstone:hole.Number==16||hole.Number>=21?GolfCourseLook.Surface.Basalt:GolfCourseLook.Surface.Cliff):null;
                    Add("BOULDER",point,new Vector3(radius,radius*.45f,radius*.70f),(float)rng.NextDouble()*360,stone);placed++;
                }
            }
        }

        void ScatterRough()
        {
            var colliders=GetComponentsInChildren<MeshCollider>();var land=new List<MeshCollider>();
            foreach(var c in colliders)if(c.name.StartsWith("TERRAIN"))land.Add(c);
            if(land.Count==0)return;
            var rng=new System.Random(51091+hole.Number*331);
            int goal=hole.Number==19?240:hole.Number==20?150:hole.Number==18?36:hole.Number==17?64:160;
            int placed=0;
            for(int attempt=0;attempt<4200&&placed<goal;attempt++){
                var c=land[rng.Next(land.Count)];var bounds=c.bounds;
                float x=Mathf.Lerp(bounds.min.x,bounds.max.x,(float)rng.NextDouble()),z=Mathf.Lerp(bounds.min.z,bounds.max.z,(float)rng.NextDouble());
                var point=new CoursePoint(x,z);
                if(point.DistanceTo(hole.Tee)<10||point.DistanceTo(hole.Pin)<12)continue;
                var ray=new Ray(new Vector3(x,bounds.max.y+4,z),Vector3.down);
                if(!c.Raycast(ray,out var hit,bounds.size.y+8)||hit.normal.y<.85f)continue;
                // Reject every playable overlay above terrain, regardless of collision layer.
                bool covered=false;
                foreach(var other in colliders)if(other!=c&&other.Raycast(ray,out var overlay,bounds.size.y+8)&&overlay.distance<=hit.distance+.12f){covered=true;break;}
                if(covered)continue;
                if(!c.TryGetComponent<Renderer>(out var r))continue;
                int index=hit.triangleIndex,sub=0;
                for(;sub<c.sharedMesh.subMeshCount;sub++){int n=(int)c.sharedMesh.GetIndexCount(sub)/3;if(index<n)break;index-=n;}
                var mats=r.sharedMaterials;if(sub>=mats.Length||!mats[sub])continue;
                string name=mats[sub].name;
                if(!(name.EndsWith(" Rough")||name.EndsWith(" Ash")||name.StartsWith("LK_ROUGH")))continue;
                // Secondary botanical clusters stay below knee height; physics remains readable.
                float size=1.35f+(float)rng.NextDouble()*1.65f;
                string kind=hole.Number==18?"BOULDER":placed%3==0?"FERN":"SHRUB_"+(placed%2);
                float height=kind=="BOULDER"?.45f:hole.Number==19?1.65f:1.30f;
                Add(kind,hit.point-Vector3.up*.04f,new Vector3(size,height,size),(float)rng.NextDouble()*360);
                if(hole.Number!=17&&hole.Number!=18&&placed%4==0)Add("FLOWER_CORAL",hit.point,new Vector3(.65f,.65f,.65f),(float)rng.NextDouble()*360);
                placed++;
            }
        }

        void DressMeadow()
        {
            var rng=new System.Random(9171+hole.Number);
            foreach(var r in GetComponentsInChildren<MeshRenderer>()){
                if(r.name!="Trunk")continue;
                var b=r.bounds;float h=b.size.y*2;
                Add("OAK",new Vector3(b.center.x,b.min.y,b.center.z),new Vector3(h*.35f,h*.98f,h*.35f),(float)rng.NextDouble()*360);
                r.enabled=false;
            }
            foreach(var r in GetComponentsInChildren<MeshRenderer>())if(r.name=="Crown")r.enabled=false;
            double edge=hole.FairwayWidth/2+hole.RoughWidth;
            int cluster=0;
            for(int i=1;i<hole.Centerline.Length;i++){
                var a=hole.Centerline[i-1];var b=hole.Centerline[i];double dx=b.X-a.X,dz=b.D-a.D,len=a.DistanceTo(b);
                if(len<1)continue;
                for(double s=2;s<len;s+=8)foreach(int side in new[]{-1,1}){
                    float x=(float)(a.X+dx*s/len-dz/len*side*(edge-6+rng.NextDouble()*3));
                    float z=(float)(a.D+dz*s/len+dx/len*side*(edge-6+rng.NextDouble()*3));
                    float width=1.6f+(float)rng.NextDouble()*1.1f;
                    Add("SHRUB_"+(cluster++%2),new Vector3(x,0,z),new Vector3(width,1.4f,width),(float)rng.NextDouble()*360);
                    if(cluster%2==0)Add("FLOWER_CORAL",new Vector3(x+.7f,0,z+.3f),Vector3.one*1.15f,(float)rng.NextDouble()*360);
                    if(cluster%3==0)Add("FLOWER_GOLD",new Vector3(x-.6f,0,z-.4f),Vector3.one*.90f,(float)rng.NextDouble()*360);
                }
                // Independent orchard/woodland groups occupy the countryside
                // beyond the exact rough boundary, rather than a repeated hedge.
                for(double s=10;s<len;s+=38)foreach(int side in new[]{-1,1}){
                    for(int j=0;j<4;j++){
                        double orchardAlong=s+(rng.NextDouble()-.5)*19,lateral=edge+22+rng.NextDouble()*44;
                        float x=(float)(a.X+dx*orchardAlong/len-dz/len*side*lateral),z=(float)(a.D+dz*orchardAlong/len+dx/len*side*lateral);
                        if(hole.DistanceFromCenterline(new CoursePoint(x,z))<edge+18)continue;
                        float height=9.5f+(float)rng.NextDouble()*4.5f,radius=3.9f+(float)rng.NextDouble()*1.8f;
                        Add("OAK",new Vector3(x,GolfMeadowWorld.Height(hole,x,z)-.12f,z),new Vector3(radius,height,radius),(float)rng.NextDouble()*360);
                    }
                }
            }
            // A rear grove frames short par3 vistas inside portrait FOV. Every
            // root lies beyond the complete playable rough and the green.
            var last=hole.Centerline[hole.Centerline.Length-2];var pin=hole.Pin;
            var along=new Vector3((float)(pin.X-last.X),0,(float)(pin.D-last.D)).normalized;
            var right=Vector3.Cross(Vector3.up,along);
            foreach(int side in new[]{-1,1})for(int j=0;j<5;j++){
                var at=new Vector3((float)pin.X,0,(float)pin.D)+along*(62+(float)rng.NextDouble()*43)+right*side*(37+(float)rng.NextDouble()*22);
                if(hole.DistanceFromCenterline(new CoursePoint(at.x,at.z))<edge+18)continue;
                at.y=GolfMeadowWorld.Height(hole,at.x,at.z)-.12f;
                float height=10+(float)rng.NextDouble()*4,radius=4.2f+(float)rng.NextDouble()*1.6f;
                Add("OAK",at,new Vector3(radius,height,radius),(float)rng.NextDouble()*360);
            }
            GolfMeadowWorld.Apply(gameObject,hole);
        }

        MeshRenderer MakeBatch(Transform parent,string name,List<CombineInstance> pieces,Material material)
        {
            var mesh=new Mesh{name="Golf resort botanical batch",indexFormat=IndexFormat.UInt32};
            mesh.CombineMeshes(pieces.ToArray(),true,true,false);meshes.Add(mesh);
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;return renderer;
        }
        void BuildBatches()
        {
            int count=0;
            foreach(var batch in batches.Values){
                string name="RESORT_BOTANICAL_"+count++;
                if(batch.instanced){
                    var instances=new GameObject(name+"_CLUSTERS");instances.transform.SetParent(transform,false);
                    instances.AddComponent<GolfBotanicalInstances>().Initialize(batch.material,batch.pieces);continue;
                }
                if(string.IsNullOrEmpty(batch.lodKind)){MakeBatch(transform,name,batch.pieces,batch.material);continue;}
                var go=new GameObject(name+"_"+batch.lodKind+"_LOD");go.transform.SetParent(transform,false);
                var high=MakeBatch(go.transform,"RESORT_"+batch.lodKind+"_NEAR",batch.pieces,batch.material);
                var low=MakeBatch(go.transform,"RESORT_"+batch.lodKind+"_FAR",batch.farPieces,batch.material);
                var lod=go.AddComponent<LODGroup>();lod.fadeMode=LODFadeMode.None;
                lod.SetLODs(new[]{new LOD(.30f,new Renderer[]{high}),new LOD(.015f,new Renderer[]{low})});lod.RecalculateBounds();
            }
        }
        void OnDestroy(){foreach(var mesh in meshes)if(mesh)Destroy(mesh);foreach(var m in materials)if(m)Destroy(m);}
    }
}
