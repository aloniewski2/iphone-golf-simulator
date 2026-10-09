using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using GolfArcade.Game;

namespace GolfArcade.Course
{
    /// Bounded cut-turf detail. Original ground/colliders remain unchanged; only
    /// three grass draw submissions are made, with fixed near/mid allocations.
    public sealed class GolfCoastalTurf : MonoBehaviour
    {
        const float YardsPerMeter=1f/.9144f, RadiusMeters=12f, TileMeters=1f, RoughHeightScale=1.8f, MoveStepMeters=4f;
        const int NearLimit=48, MidLimit=96, CellLimit=640;
        sealed class Cell { public Matrix4x4 matrix; public Vector3 center; public Bounds bounds; public float distance; public bool rough; public float surface; }
        readonly struct PathTriangle
        {
            public readonly Vector2 a,b,c,lo,hi;
            public readonly float bottom,top;
            public readonly bool exposedSolid;
            public readonly Vector3 heightPlane;
            public PathTriangle(Vector3 x,Vector3 y,Vector3 z,bool solid=false)
            {
                a=new(x.x,x.z);b=new(y.x,y.z);c=new(z.x,z.z);
                lo=Vector2.Min(a,Vector2.Min(b,c));hi=Vector2.Max(a,Vector2.Max(b,c));
                bottom=Mathf.Min(x.y,Mathf.Min(y.y,z.y));top=Mathf.Max(x.y,Mathf.Max(y.y,z.y));
                exposedSolid=solid;
                var n=Vector3.Cross(y-x,z-x);
                heightPlane=solid?new Vector3(-n.x/n.y,-n.z/n.y,Vector3.Dot(n,x)/n.y):default;
            }
        }
        sealed class Block { public readonly List<Cell> cells=new(4); }
        sealed class GroundSource
        {
            public MeshCollider collider; public Bounds bounds; public int[] triangleEnds; public Material[] materials;
        }
        static readonly Vector3[] Corners={new(-.44f,0,-.44f),new(.44f,0,-.44f),new(-.44f,0,.44f),new(.44f,0,.44f)};
        static readonly Vector3[] Children={new(-.25f,0,-.25f),new(.25f,0,-.25f),new(-.25f,0,.25f),new(.25f,0,.25f)};
        // Accepted live-address courses. Opt in remaining Supports() holes
        // only after their actual address/path/physics captures pass.
        public static readonly HashSet<int> MovingHoles=new(){7,8,9,10,12,13,14,15,16,17,18,19,20,21,22,23};
        readonly Dictionary<Vector2Int,Block> blocks=new();
        readonly List<Vector2Int> desired=new(441),retired=new(441);
        readonly HashSet<Vector2Int> desiredSet=new();
        readonly List<GroundSource> groundSources=new();
        Hole activeHole; GolfGame game; Vector3 teeAnchor; Vector2Int patchCell;
        bool patchBuilt;
        public bool MovingEnabled {get;private set;}
        public int RebuildCount {get;private set;}
        public double RebuildMilliseconds {get;private set;}
        public int GroundQueriesThisRebuild {get;private set;}
        public int ColliderRaycastsThisRebuild {get;private set;}
        public int RetainedCellsThisRebuild {get;private set;}
        public Vector3 PatchCenter {get;private set;}
        readonly List<PathTriangle> paths=new();
        readonly List<Cell> cells=new();
        readonly List<Matrix4x4> near=new(),mid=new(),far=new();
        readonly List<float> nearTypes=new(),midTypes=new(),farTypes=new();
        MaterialPropertyBlock nearProperties,midProperties,farProperties;
        readonly List<Material> groundMaterials=new();
        Mesh nearMesh,midMesh,farMesh;Matrix4x4 nearBasis,midBasis,farBasis;Material grass;Bounds tileBounds;
        Plane[] planes=new Plane[6];
        public int CellCount=>cells.Count;
        public int LastNearCells {get;private set;}
        public int LastMidCells {get;private set;}
        public int LastFarCells {get;private set;}
        public int MaximumTriangles {get;private set;}

        // Candidate extension: authored cut-turf slots only. Native Ash/Snow/
        // Desert/Basalt slots keep their exact materials and receive no blades.
        public static readonly HashSet<int> CoverageHoles=new(){10,16,17,18,21,22,23};
        public static bool CoverageCandidate(int number)=>CoverageHoles.Contains(number)
            &&Environment.GetEnvironmentVariable("VISUAL_GRASS_COVERAGE24")=="1";
        public static bool SupportsApron(int number)=>CoverageHoles.Contains(number);
        public static bool Supports(int number) => number is 7 or 8 or 9 or 10 or 12 or 13 or 14 or 15 or 16 or 17 or 18 or 19 or 20 or 21 or 22 or 23 || CoverageCandidate(number);
        public static bool EligibleCollider(string name,int number)=>name.StartsWith("TERRAIN")||name.StartsWith("TEE_BOX")||name.StartsWith("FAIRWAY")
            ||SupportsApron(number)&&name.StartsWith("GREEN_APRON");
        static float CutHeightScale(Hole hole)=>hole.Number==18?.55f:hole.Number==17?.65f:
            hole.Number==16||hole.Theme=="magma"?.75f:1f;
        public static void Apply(GameObject model,Hole hole)
        {
            if(!Supports(hole.Number))return;
            ApplyAroundTee(model,hole);
        }
        // Authored grass roles share one bounded patch. Candidate cold/dry/
        // volcanic holes require explicit opt-in; procedural Meadow is separate.
        public static void ApplyAroundTee(GameObject model,Hole hole)
        {
            if(model.GetComponent<GolfCoastalTurf>())return;
            var prefab=Resources.Load<GameObject>("Course/Resort/CoastalTurf");
            var shader=Resources.Load<Shader>("Course/Shaders/GolfCoastalTurf");
            if(!prefab||!shader){Debug.LogError("[GolfCoastalTurf] missing authored turf tile/shader");return;}
            var owner=model.AddComponent<GolfCoastalTurf>();owner.Build(model,hole,prefab,shader);
        }
        static MeshFilter Find(GameObject prefab,string name)=>Array.Find(prefab.GetComponentsInChildren<MeshFilter>(),x=>x.name==name);
        void Build(GameObject model,Hole hole,GameObject prefab,Shader shader)
        {
            var n=Find(prefab,"TURF_NEAR");var m=Find(prefab,"TURF_MID");var f=Find(prefab,"TURF_FAR");
            if(!n||!m||!f||!n.sharedMesh||!m.sharedMesh||!f.sharedMesh){Debug.LogError("[GolfCoastalTurf] invalid turf schema");return;}
            nearMesh=n.sharedMesh;midMesh=m.sharedMesh;farMesh=f.sharedMesh;
            nearProperties=new MaterialPropertyBlock();midProperties=new MaterialPropertyBlock();farProperties=new MaterialPropertyBlock();
            nearProperties.SetFloatArray("_TurfSurface",new float[NearLimit]);midProperties.SetFloatArray("_TurfSurface",new float[MidLimit]);farProperties.SetFloatArray("_TurfSurface",new float[CellLimit]);
            nearBasis=prefab.transform.worldToLocalMatrix*n.transform.localToWorldMatrix;
            midBasis=prefab.transform.worldToLocalMatrix*m.transform.localToWorldMatrix;
            farBasis=prefab.transform.worldToLocalMatrix*f.transform.localToWorldMatrix;
            tileBounds=TransformBounds(nearMesh.bounds,nearBasis);
            tileBounds.Encapsulate(TransformBounds(midMesh.bounds,midBasis));
            tileBounds.Encapsulate(TransformBounds(farMesh.bounds,farBasis));
            grass=new Material(shader){name=$"Course {hole.Number} short cut grass",enableInstancing=true};
            GolfTurfPalette.ApplyBlades(grass,hole);grass.SetFloat("_Smoothness",.08f);
            var mowing=new Vector3((float)(hole.Pin.X-hole.Tee.X),0,(float)(hole.Pin.D-hole.Tee.D)).normalized;
            grass.SetVector("_StripeDirection",new Vector4(mowing.z,0,-mowing.x,0));
            
            activeHole=hole; game=model.GetComponentInParent<GolfGame>();
            teeAnchor=new Vector3((float)hole.Tee.X,0,(float)hole.Tee.D);
            string movingMode=Environment.GetEnvironmentVariable("VISUAL_MOVING_TURF");
            MovingEnabled=Supports(hole.Number)&&movingMode!="0"&&(MovingHoles.Contains(hole.Number)||movingMode=="all");
            BuildPathMask(model,hole);
            FineGround(model,hole,teeAnchor);
            Physics.SyncTransforms(); CacheGround(model);
            if(MovingEnabled) RefreshForAddress(teeAnchor);
            else {
                // Preserve the accepted static-tee population and RNG on non-pilots.
                var rng=new System.Random(121252);float size=TileMeters*YardsPerMeter;
                for(int z=-12;z<=12;z++)for(int x=-12;x<=12;x++){
                    if(cells.Count>=CellLimit)continue;
                    var p=teeAnchor+new Vector3(x*size,0,z*size);
                    if(new Vector2(p.x-teeAnchor.x,p.z-teeAnchor.z).magnitude>(RadiusMeters-TileMeters*.7f)*YardsPerMeter)continue;
                    AddCell(p,1,rng,true,null);
                }
                UpdateTriangleBound();
            }
            int roughCells=0;foreach(var cell in cells)if(cell.rough)roughCells++;
            Debug.Log($"[GolfCoastalTurf] {cells.Count} 1m clustered cells within12m patch; moving={MovingEnabled} ({cells.Count-roughCells} cut / {roughCells} rough); near/mid caps {NearLimit}/{MidLimit}, max {MaximumTriangles} triangles, 3 instanced submissions, no grass shadows/colliders; hole {hole.Number}, {paths.Count} visual path-mask triangles");
        }
        // Explicit capture hook: call after placing the ball at a settled address.
        // This performs no draw submission; allow the next LateUpdate to draw once.
        public void RefreshForAddress(Vector3 settledBall)
        {
            if(!MovingEnabled||!grass||!nearMesh||float.IsNaN(settledBall.x)||float.IsNaN(settledBall.z)
                ||float.IsInfinity(settledBall.x)||float.IsInfinity(settledBall.z))return;
            float step=MoveStepMeters*YardsPerMeter;
            var key=new Vector2Int(Mathf.FloorToInt((settledBall.x-teeAnchor.x)/step+.5f),Mathf.FloorToInt((settledBall.z-teeAnchor.z)/step+.5f));
            if(patchBuilt&&key==patchCell)return;
            long rebuildStart=System.Diagnostics.Stopwatch.GetTimestamp();
            patchBuilt=true;patchCell=key;RebuildCount++;GroundQueriesThisRebuild=0;ColliderRaycastsThisRebuild=0;
            PatchCenter=teeAnchor+new Vector3(key.x*step,0,key.y*step);
            bool fadeEdge=Environment.GetEnvironmentVariable("VISUAL_TURF_EDGE")!="0";
            grass.SetVector("_PatchCenter",new Vector4(PatchCenter.x,0,PatchCenter.z,fadeEdge?1:0));
            grass.SetVector("_PatchFade",new Vector4(8f*YardsPerMeter,11.1f*YardsPerMeter,0,0));
            desired.Clear();desiredSet.Clear();retired.Clear();
            int cx=key.x*4,cz=key.y*4;
            for(int z=-12;z<=12;z++)for(int x=-12;x<=12;x++) {
                if(x*x+z*z>(RadiusMeters-TileMeters*.7f)*(RadiusMeters-TileMeters*.7f))continue;
                var tile=new Vector2Int(cx+x,cz+z);desired.Add(tile);desiredSet.Add(tile);
            }
            foreach(var entry in blocks)if(!desiredSet.Contains(entry.Key))retired.Add(entry.Key);
            foreach(var tile in retired)blocks.Remove(tile);
            // Keep the same Cell instances/roots/rotations for every overlapping
            // block. Empty blocks are retained too: failed ground is not queried again.
            cells.Clear();
            foreach(var entry in blocks)cells.AddRange(entry.Value.cells);
            RetainedCellsThisRebuild=cells.Count;
            desired.Sort((a,b)=>{
                int da=(a.x-cx)*(a.x-cx)+(a.y-cz)*(a.y-cz),db=(b.x-cx)*(b.x-cx)+(b.y-cz)*(b.y-cz);
                int order=da.CompareTo(db);if(order!=0)return order;
                order=a.y.CompareTo(b.y);return order!=0?order:a.x.CompareTo(b.x);
            });
            foreach(var tile in desired) {
                if(blocks.ContainsKey(tile))continue;
                var block=new Block();blocks.Add(tile,block);
                if(cells.Count>=CellLimit)continue;
                uint seed=2166136261u;
                unchecked { seed=(seed^(uint)activeHole.Number)*16777619u;seed=(seed^(uint)tile.x)*16777619u;seed=(seed^(uint)tile.y)*16777619u; }
                var rng=new System.Random((int)(seed&0x7fffffffu));
                var p=teeAnchor+new Vector3(tile.x*TileMeters*YardsPerMeter,0,tile.y*TileMeters*YardsPerMeter);
                AddCell(p,1,rng,true,block);
            }
            UpdateTriangleBound();
            RebuildMilliseconds=(System.Diagnostics.Stopwatch.GetTimestamp()-rebuildStart)*1000.0/System.Diagnostics.Stopwatch.Frequency;
        }
        void UpdateTriangleBound()
        {
            MaximumTriangles=Mathf.Min(NearLimit,cells.Count)*(int)nearMesh.GetIndexCount(0)/3+Mathf.Min(MidLimit,Mathf.Max(0,cells.Count-NearLimit))*(int)midMesh.GetIndexCount(0)/3+Mathf.Max(0,cells.Count-NearLimit-MidLimit)*(int)farMesh.GetIndexCount(0)/3;
        }
        void CacheGround(GameObject model)
        {
            foreach(var collider in model.GetComponentsInChildren<MeshCollider>()) {
                if(!collider.sharedMesh)continue;
                var ends=new int[collider.sharedMesh.subMeshCount];int total=0;
                for(int i=0;i<ends.Length;i++){total+=(int)collider.sharedMesh.GetIndexCount(i)/3;ends[i]=total;}
                groundSources.Add(new GroundSource{collider=collider,bounds=collider.bounds,triangleEnds=ends,materials=collider.GetComponent<Renderer>()?.sharedMaterials});
            }
        }
        void AddCell(Vector3 p,float span,System.Random rng,bool subdivide,Block block)
        {
            if(cells.Count>=CellLimit||!Ground(p,out var point,out var normal,out var rough,out var surface))return;
            bool continuous=true;
            foreach(var d in Corners)
                if(!Ground(p+d*span*YardsPerMeter,out var corner,out var unused,out var cornerRough,out var cornerSurface)||cornerRough!=rough
                    ||(MovingEnabled&&cornerSurface!=surface)||Mathf.Abs(Vector3.Dot(corner-point,normal))>.025f){continuous=false;break;}
            if(!continuous){
                // Smaller queries follow actual cut/rough and excluded role edges.
                if(subdivide)foreach(var d in Children)AddCell(p+d*YardsPerMeter,.5f,rng,false,block);
                return;
            }
            var rotation=Quaternion.FromToRotation(Vector3.up,normal)*Quaternion.AngleAxis(rng.Next(4)*90,Vector3.up);
            var matrix=Matrix4x4.TRS(point-Vector3.up*.0015f,rotation,new Vector3(span,(rough?RoughHeightScale:1)*CutHeightScale(activeHole),span)*YardsPerMeter);
            var bounds=TransformBounds(tileBounds,matrix);bounds.Expand(.12f);
            var cell=new Cell{center=point,rough=rough,surface=surface,matrix=matrix,bounds=bounds};
            cells.Add(cell);block?.cells.Add(cell);
        }
        static Bounds TransformBounds(Bounds bounds,Matrix4x4 matrix)
        {
            var e=bounds.extents;
            var x=matrix.MultiplyVector(new Vector3(e.x,0,0));
            var y=matrix.MultiplyVector(new Vector3(0,e.y,0));
            var z=matrix.MultiplyVector(new Vector3(0,0,e.z));
            var extents=new Vector3(Mathf.Abs(x.x)+Mathf.Abs(y.x)+Mathf.Abs(z.x),Mathf.Abs(x.y)+Mathf.Abs(y.y)+Mathf.Abs(z.y),Mathf.Abs(x.z)+Mathf.Abs(y.z)+Mathf.Abs(z.z));
            return new Bounds(matrix.MultiplyPoint3x4(bounds.center),extents*2);
        }
        bool Ground(Vector3 p,out Vector3 point,out Vector3 normal,out bool rough,out float surface)
        {
            GroundQueriesThisRebuild++;
            point=normal=default;rough=false;surface=0;
            // A hidden collision fairway can continue below an authored lagoon.
            if(MovingEnabled||CoverageHoles.Contains(activeHole.Number)){
                var lie=activeHole.LieAt(new CoursePoint(p.x,p.z));
                if(lie==CourseLie.Water||CoverageHoles.Contains(activeHole.Number)&&lie==CourseLie.Ice)return false;
            }
            float closest=float.MaxValue;GroundSource land=null;RaycastHit best=default;
            var ray=new Ray(new Vector3(p.x,1000,p.z),Vector3.down);
            foreach(var source in groundSources) {
                if(!source.collider||!source.collider.enabled||!source.collider.gameObject.activeInHierarchy
                    ||p.x<source.bounds.min.x||p.x>source.bounds.max.x||p.z<source.bounds.min.z||p.z>source.bounds.max.z)continue;
                ColliderRaycastsThisRebuild++;
                if(source.collider.Raycast(ray,out var hit,2000)&&hit.distance<closest){land=source;best=hit;closest=hit.distance;}
            }
            if(land==null||best.normal.y<.92f)return false;
            string name=land.collider.name;
            if(!EligibleCollider(name,activeHole.Number))return false;
            if(OnVisualPath(best.point))return false;
            int sub=0;while(sub<land.triangleEnds.Length&&best.triangleIndex>=land.triangleEnds[sub])sub++;
            var mats=land.materials;
            if(mats==null||sub>=mats.Length||!mats[sub])return false;
            var material=mats[sub];
            if(name.StartsWith("TERRAIN")) {
                if(!material.name.Contains(" Rough"))return false;
                rough=true;
            }
            else surface=material.HasProperty("_Surface")?material.GetFloat("_Surface"):name.StartsWith("TEE")?3:1;
            if(name.StartsWith("GREEN_APRON")&&surface!=1&&surface!=4)return false;
            // Never put the accepted cut/rough geometry on green or hazard roles.
            if((MovingEnabled||CoverageHoles.Contains(activeHole.Number))&&(surface!=0&&surface!=1&&surface!=3&&surface!=4))return false;
            point=best.point;normal=best.normal;return true;
        }
        void BuildPathMask(GameObject model,Hole hole)
        {
            // Some authored paths (Postcard8 DRESS_PATH_01/02) are render-only
            // ribbons above grassy collision ground. Query their actual triangles
            // without creating physics objects or changing the underlying lie.
            var tee=new Vector3((float)hole.Tee.X,0,(float)hole.Tee.D);
            foreach(var filter in model.GetComponentsInChildren<MeshFilter>())
            {
                var mesh=filter.sharedMesh;
                if(!mesh||!mesh.isReadable||!filter.TryGetComponent<Renderer>(out var renderer)||!renderer.enabled)continue;
                var closest=renderer.bounds.ClosestPoint(new Vector3(tee.x,renderer.bounds.center.y,tee.z));
                if(!MovingEnabled&&new Vector2(closest.x-tee.x,closest.z-tee.z).sqrMagnitude>196*YardsPerMeter*YardsPerMeter)continue;
                var materials=renderer.sharedMaterials;var vertices=mesh.vertices;
                for(int sub=0;sub<Mathf.Min(mesh.subMeshCount,materials.Length);sub++)
                {
                    var material=materials[sub];if(!material)continue;
                    // Hole10's authored render-only basalt tee border sits 1.15cm
                    // above the unchanged tee collider. Its interior support piers
                    // are hidden below turf and must not punch holes in that turf.
                    bool exposedBasalt=hole.Number==10&&filter.name=="DRESS_PADRIM_V3"
                        &&(material.name.StartsWith("LK_BASALT")||material.name.Contains(" Basalt"));
                    if(!(material.name.StartsWith("LK_PATH")||material.name.Contains(" Path")||exposedBasalt))continue;
                    var triangles=mesh.GetTriangles(sub);
                    for(int i=0;i+2<triangles.Length;i+=3)
                    {
                        var a=filter.transform.TransformPoint(vertices[triangles[i]]);
                        var b=filter.transform.TransformPoint(vertices[triangles[i+1]]);
                        var c=filter.transform.TransformPoint(vertices[triangles[i+2]]);
                        if(Mathf.Abs(Vector3.Cross(b-a,c-a).y)<.00001f)continue;
                        paths.Add(new PathTriangle(a,b,c,exposedBasalt));
                    }
                }
            }
        }
        static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
        static float EdgeDistance(Vector2 point,Vector2 a,Vector2 b)
        {
            var edge=b-a;float t=Mathf.Clamp01(Vector2.Dot(point-a,edge)/Mathf.Max(edge.sqrMagnitude,.000001f));
            return (point-a-edge*t).sqrMagnitude;
        }
        bool OnVisualPath(Vector3 point)
        {
            const float margin=.16f*YardsPerMeter;
            var p=new Vector2(point.x,point.z);
            foreach(var path in paths)
            {
                if(point.y<path.bottom-.35f||point.y>path.top+.35f||p.x<path.lo.x-margin||p.x>path.hi.x+margin||p.y<path.lo.y-margin||p.y>path.hi.y+margin)continue;
                // Test the actual triangle plane at the queried root, not its
                // maximum height: buried pier caps remain covered by tee turf.
                if(path.exposedSolid&&point.y>path.heightPlane.x*p.x+path.heightPlane.y*p.y+path.heightPlane.z+.005f*YardsPerMeter)continue;
                float a=Cross(path.b-path.a,p-path.a),b=Cross(path.c-path.b,p-path.b),c=Cross(path.a-path.c,p-path.c);
                if((a>=0&&b>=0&&c>=0)||(a<=0&&b<=0&&c<=0))return true;
                // The tile's authored fan tips overhang its root square. Retain
                // a narrow margin while subdividing boundary cells as before.
                if(EdgeDistance(p,path.a,path.b)<margin*margin||EdgeDistance(p,path.b,path.c)<margin*margin||EdgeDistance(p,path.c,path.a)<margin*margin)return true;
            }
            return false;
        }
        void FineGround(GameObject model,Hole hole,Vector3 tee)
        {
            var colour=Resources.Load<Texture2D>("Course/Resort/CoastalTurf_C");var normal=Resources.Load<Texture2D>("Course/Resort/CoastalTurf_N");
            if(!colour||!normal){Debug.LogError("[GolfCoastalTurf] missing micro-turf surface maps");return;}
            colour.anisoLevel=16;normal.anisoLevel=16;
            var replacements=new Dictionary<Material,Material>();
            foreach(var renderer in model.GetComponentsInChildren<MeshRenderer>()){
                if(!renderer.enabled)continue;
                var closest=renderer.bounds.ClosestPoint(new Vector3(tee.x,renderer.bounds.center.y,tee.z));
                // A moving patch shares one atlas material per eligible role over
                // the whole authored hole; no cloning/restoration during movement.
                if(!MovingEnabled&&new Vector2(closest.x-tee.x,closest.z-tee.z).sqrMagnitude>RadiusMeters*RadiusMeters*YardsPerMeter*YardsPerMeter)continue;
                var mats=renderer.sharedMaterials;bool changed=false;
                for(int i=0;i<mats.Length;i++){
                    var source=mats[i];if(!source||source.shader.name!="GolfArcade/GolfGround"||!(source.name.Contains(" Rough")||source.name.Contains(" Tee")||source.name.Contains(" Fairway")||source.name.Contains(" Fringe")))continue;
                    if(!replacements.TryGetValue(source,out var detail)){
                        detail=new Material(source){name=source.name,enableInstancing=true};
                        detail.SetTexture("_BaseMap",colour);detail.SetTexture("_BumpMap",normal);
                        detail.SetFloat("_TileYards",1f*YardsPerMeter);detail.SetFloat("_NormalEnabled",1);detail.SetFloat("_BumpScale",.65f);
                        // The source atlas uses curved folded lamina and root AO. Its
                        // measured linear luminance preserves the accepted palette midpoint.
                        detail.SetFloat("_PaletteMode",1);detail.SetFloat("_PaletteDetail",1);detail.SetFloat("_TurfMidpoint",0.0907707f);
                        detail.SetFloat("_DetailContrast",6.5f);detail.SetColor("_BaseColor",Color.white);
                        var role=source.name.Contains(" Rough")?GolfCourseLook.Surface.Rough:source.name.Contains(" Fringe")?GolfCourseLook.Surface.Fringe:source.name.Contains(" Tee")?GolfCourseLook.Surface.Tee:GolfCourseLook.Surface.Fairway;
                        GolfTurfPalette.ApplyGround(detail,hole,role,true);
                        // Keep the course-coordinate map for longitudinal fairway
                        // bands; the tee uses its own much quieter mowing treatment.
                        detail.SetFloat("_Wrap",.32f);
                        detail.SetFloat("_SheenFromAlpha",0);groundMaterials.Add(detail);replacements[source]=detail;
                    }
                    mats[i]=detail;changed=true;
                }
                if(changed)renderer.sharedMaterials=mats;
            }
        }
        void LateUpdate()
        {
            // GolfGame positions the next addressed ball only after flight settles.
            // Replays and signature/flyover flight retain the previous address pool.
            if(MovingEnabled&&game&&ReferenceEquals(game.CurrentHole,activeHole)
                &&(game.Current is GolfGame.State.Aim or GolfGame.State.Result or GolfGame.State.HoleDone))
                RefreshForAddress(game.BallPosition);
            var camera=Camera.main;if(!camera||!grass||!nearMesh||!midMesh||!farMesh||!SystemInfo.supportsInstancing)return;
            GeometryUtility.CalculateFrustumPlanes(camera,planes);near.Clear();mid.Clear();far.Clear();nearTypes.Clear();midTypes.Clear();farTypes.Clear();
            foreach(var c in cells)c.distance=(c.center-camera.transform.position).sqrMagnitude;
            cells.Sort((a,b)=>a.distance.CompareTo(b.distance));
            foreach(var c in cells){
                if(c.distance>35f*35f*YardsPerMeter*YardsPerMeter||!GeometryUtility.TestPlanesAABB(planes,c.bounds))continue;
                if(near.Count<NearLimit&&c.distance<10f*10f*YardsPerMeter*YardsPerMeter){near.Add(c.matrix*nearBasis);nearTypes.Add(c.surface);}
                else if(mid.Count<MidLimit&&c.distance<17f*17f*YardsPerMeter*YardsPerMeter){mid.Add(c.matrix*midBasis);midTypes.Add(c.surface);}
                else{far.Add(c.matrix*farBasis);farTypes.Add(c.surface);}
            }
            LastNearCells=near.Count;LastMidCells=mid.Count;LastFarCells=far.Count;
            if(near.Count>0){nearProperties.SetFloatArray("_TurfSurface",nearTypes);Graphics.DrawMeshInstanced(nearMesh,0,grass,near,nearProperties,ShadowCastingMode.Off,true,gameObject.layer,null,LightProbeUsage.Off);}
            if(mid.Count>0){midProperties.SetFloatArray("_TurfSurface",midTypes);Graphics.DrawMeshInstanced(midMesh,0,grass,mid,midProperties,ShadowCastingMode.Off,true,gameObject.layer,null,LightProbeUsage.Off);}
            if(far.Count>0){farProperties.SetFloatArray("_TurfSurface",farTypes);Graphics.DrawMeshInstanced(farMesh,0,grass,far,farProperties,ShadowCastingMode.Off,true,gameObject.layer,null,LightProbeUsage.Off);}
        }
        void OnDestroy(){if(grass)Destroy(grass);foreach(var material in groundMaterials)if(material)Destroy(material);}
    }
}
