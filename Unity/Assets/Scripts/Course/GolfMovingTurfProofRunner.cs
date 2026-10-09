#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using GolfArcade.Course;
using GolfArcade.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace GolfArcade.EditorTools
{
    [InitializeOnLoad]
    public static class GolfMovingTurfProof
    {
        const string Key="GolfMovingTurfProof";
        [Serializable] public sealed class Config
        {
            public string output;
            public int[] holes={12,14};
            public int width=900,height=1600,warmFrames=30;
            public float holeTimeoutSeconds=240;
            public bool requireMoving=true,exerciseFlight=true;
        }
        static GolfMovingTurfProof(){EditorApplication.update+=Boot;}
        public static void Run()
        {
            string path=Environment.GetEnvironmentVariable("GOLF_MOVING_TURF_CONFIG");
            if(string.IsNullOrWhiteSpace(path))throw new ArgumentException("GOLF_MOVING_TURF_CONFIG required");
            var config=JsonUtility.FromJson<Config>(File.ReadAllText(path));
            if(string.IsNullOrWhiteSpace(config.output)||config.holes==null||config.holes.Length==0)throw new ArgumentException("Config requires output and holes");
            if(config.width<1||config.height<1)throw new ArgumentException("Invalid capture dimensions");
            Directory.CreateDirectory(config.output);
            SessionState.SetString(Key+"config",JsonUtility.ToJson(config));
            SessionState.SetBool(Key+"active",true);SessionState.SetBool(Key+"started",false);
            EditorSceneManager.OpenScene("Assets/Scenes/Golf.unity");EditorApplication.isPlaying=true;
        }
        static void Boot()
        {
            if(!SessionState.GetBool(Key+"active",false)||SessionState.GetBool(Key+"started",false)||!EditorApplication.isPlaying)return;
            SessionState.SetBool(Key+"started",true);
            var runner=new GameObject("Moving turf desktop proof").AddComponent<GolfMovingTurfProofRunner>();
            runner.Begin(JsonUtility.FromJson<Config>(SessionState.GetString(Key+"config","")));
        }
        internal static void Finish(int code){SessionState.SetBool(Key+"active",false);EditorApplication.Exit(code);}
    }

    // Runs after the production turf's default-order LateUpdate. Rendering stays
    // in Unity's normal camera loop; no manual LateUpdate or Camera.Render calls.
    [DefaultExecutionOrder(32000)]
    public sealed class GolfMovingTurfProofRunner:MonoBehaviour
    {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        [Serializable] public sealed class PoseReport
        {
            public string label,image,physicsGate,physicsHash,poolHash,selectedCollider,selectedSurface,courseLie;
            public Vector3 requestedPoint;public float selectionDistanceMeters,clearanceMeters;
            public int hole,placedFrame,renderedFrame,lateUpdateFrame,rebuildBefore,rebuildAfter;
            public int cells,near,mid,far,retainedBlocks,retainedCells,regeneratedComparedCells;
            public int groundQueries,colliderRaycasts,ownedMaterialCount;
            public double rebuildMilliseconds,lastRebuildMilliseconds;
            public Vector3 ball,patchCenter;
            public bool matricesExact,materialsExact,physicsExact,sameStationNoRebuild,rebuildOccurred;
        }
        [Serializable] public sealed class HoleReport
        {
            public int hole,flightFrames,flightCoarseStations;
            public bool moving,ownerExact,dressingPhysicsExact,flightRebuildsAbsent,flightStaticPhysicsExact,windmillStaticExact,windmillLaunchExact,windmillPhaseExact=true;
            public double windmillLaunchExpected,windmillLaunchActual,windmillMaximumPhaseError,windmillCurrentAngleBefore,windmillCurrentAngleAfter,windmillInitialFlightTime;
            public string flightBeforeAudit,flightAfterAudit;
            public int renderedWaterTriangles;public bool knownWaterStationRejected;
            public List<WindFieldChange> windmillChangedFields=new();
            public string gate,dressingAudit,baselinePhysicsHash;
            public List<PoseReport> poses=new();
        }
        [Serializable] public sealed class Report
        {
            public string gate="PENDING",unity,graphics,movingFlag,error;
            public string timingScope="Stopwatch timestamps inside production RefreshForAddress only; excludes capture/audit overhead. Desktop latency, no phone-FPS claim.";
            public List<HoleReport> holes=new();
        }
        [Serializable] public sealed class WindFieldChange {public string field,before,after;}
        sealed class WindSnapshot {public SpinningSails instance;public Delegate angle,drive;public readonly Dictionary<string,string> fields=new();}
        readonly struct WaterTriangle
        {
            public readonly Vector3 a,b,c;public readonly Vector2 lo,hi;
            public WaterTriangle(Vector3 x,Vector3 y,Vector3 z){a=x;b=y;c=z;lo=Vector2.Min(new(x.x,x.z),Vector2.Min(new(y.x,y.z),new(z.x,z.z)));hi=Vector2.Max(new(x.x,x.z),Vector2.Max(new(y.x,y.z),new(z.x,z.z)));}
        }
        readonly List<WaterTriangle> renderedWater=new();
        sealed class BlockSnapshot {public object block;public object[] cells;public string[] hashes;}
        sealed class Pose {public string label;public CoursePoint point,requested;public bool sameStation,recordForReturn,returnStation;public float clearance;}
        sealed class SelectionGround {public MeshCollider collider;public Bounds bounds;public Material[] materials;public int[] triangleEnds;}
        readonly List<SelectionGround> selectionGround=new();
        static readonly Vector2[] ClearanceDirections={new(1,0),new(-1,0),new(0,1),new(0,-1),new(.7071068f,.7071068f),new(-.7071068f,.7071068f),new(.7071068f,-.7071068f),new(-.7071068f,-.7071068f)};
        GolfMovingTurfProof.Config config;
        GolfGame game;CameraRig rig;Camera cam;RenderTexture target;
        GolfCoastalTurf turf;HoleView view;Hole hole;HoleReport holeReport;
        Transform ball;GolferView golfer;
        Report report=new();
        bool pending,flightWatching;int pendingPlacedFrame,lastReadFrame=-1,lastLateUpdateFrame=-1;
        int lastLateRebuild,flightRebuildCount;readonly HashSet<Vector2Int> flightStations=new();
        string pendingImage,callbackError;
        double deadline;
        static object Field(object owner,string name)=>owner.GetType().GetField(name,Private|BindingFlags.Public).GetValue(owner);
        static string Hash(Action<BinaryWriter> write)
        {
            using var stream=new MemoryStream();using(var writer=new BinaryWriter(stream,System.Text.Encoding.UTF8,true))write(writer);
            using var sha=SHA256.Create();return BitConverter.ToString(sha.ComputeHash(stream.ToArray())).Replace("-","").ToLowerInvariant();
        }
        static string TextHash(string text)=>Hash(w=>w.Write(text));
        static void Check(bool pass,string message){if(!pass)throw new InvalidOperationException(message);}
        public void Begin(GolfMovingTurfProof.Config value)
        {
            config=value;report.unity=Application.unityVersion;report.graphics=SystemInfo.graphicsDeviceType+" / "+SystemInfo.graphicsDeviceName;
            report.movingFlag=Environment.GetEnvironmentVariable("VISUAL_MOVING_TURF")??"unset: pilots";
            RenderPipelineManager.endContextRendering+=AfterRender;
            StartCoroutine(Guard(Work()));
        }
        IEnumerator Guard(IEnumerator work)
        {
            while(true){
                object next;bool more;
                try{more=work.MoveNext();next=more?work.Current:null;}
                catch(Exception e){Fail(e);yield break;}
                if(!more)yield break;
                yield return next;
            }
        }
        void CheckDeadline()
        {
            if(callbackError!=null)throw new InvalidOperationException(callbackError);
            if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("Moving turf proof frame/flight timeout");
        }
        IEnumerator Work()
        {
            deadline=EditorApplication.timeSinceStartup+config.holeTimeoutSeconds;
            for(int i=0;i<config.warmFrames;i++){CheckDeadline();yield return null;}
            while(!(game=Object.FindFirstObjectByType<GolfGame>())||!HoleView.Current){CheckDeadline();yield return null;}
            rig=Object.FindFirstObjectByType<CameraRig>();Check(rig,"Missing production CameraRig");
            cam=rig.GetComponentInChildren<Camera>();Check(cam,"Missing production golf camera");
            ball=(Transform)Field(game,"ball");golfer=(GolferView)Field(game,"golfer");
            GolfVisualPhysicsGate.Enabled=true;GolfVisualPhysicsGate.Reports.Clear();
            GolfWindSway.FreezeTime=0;GolfWindSway.ForcedWind=Wind.Calm;
            game.ChooseHoles(0);game.Play();
            target=new RenderTexture(config.width,config.height,24){antiAliasing=4,name="Moving turf actual camera proof"};target.Create();
            cam.targetTexture=target;cam.aspect=(float)config.width/config.height;
            foreach(int number in config.holes){
                deadline=EditorApplication.timeSinceStartup+config.holeTimeoutSeconds;
                game.enabled=true;rig.enabled=true;game.JumpToHole(number);game.DropBall(game.CurrentHole.Tee);
                yield return null;
                view=HoleView.Current;hole=game.CurrentHole;turf=view.GetComponentInChildren<GolfCoastalTurf>();
                Check(turf,"Missing accepted turf component");
                holeReport=new HoleReport{hole=number,moving=turf.MovingEnabled};report.holes.Add(holeReport);
                holeReport.ownerExact=turf.GetComponentInParent<GolfGame>()==game&&ReferenceEquals(Field(turf,"game"),game);
                Check(holeReport.ownerExact,"Turf did not cache the actual ancestor GolfGame");
                Check(!config.requireMoving||turf.MovingEnabled,"Moving pilot disabled by flag/scope");
                if(GolfVisualPhysicsGate.Reports.TryGetValue(number,out var dressing)){
                    holeReport.dressingAudit=JsonUtility.ToJson(dressing,true);holeReport.dressingPhysicsExact=dressing.gate=="PASS";
                    File.WriteAllText(Path.Combine(config.output,$"hole{number:00}_dressing_physics.json"),holeReport.dressingAudit);
                }
                Check(holeReport.dressingPhysicsExact,"Existing before/after visual dressing physics gate failed or absent");
                var baselinePhysics=PhysicsState();holeReport.baselinePhysicsHash=TextHash(baselinePhysics);
                File.WriteAllText(Path.Combine(config.output,$"hole{number:00}_physics_before_addresses.json"),baselinePhysics);
                var initialMaterials=MaterialIds();
                game.enabled=false;rig.enabled=false;
                foreach(var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))canvas.enabled=false;
                HideAim();golfer.SetVisible(true);
                CacheSelectionGround();
                holeReport.renderedWaterTriangles=renderedWater.Count;
                if(number==19){
                    var priorWater=new CoursePoint(-.004415035247802734,179.12255859375);
                    holeReport.knownWaterStationRejected=!GrassAt(priorWater,out var unusedPoint,out var unusedName,out var unusedRole);
                    Check(holeReport.knownWaterStationRejected&&renderedWater.Count>0,"Known underwater station was accepted or actual water mask is absent");
                }
                var earlier=new Dictionary<Vector2Int,BlockSnapshot>();
                foreach(var pose in Poses(hole)){
                    // Next-frame placement prevents multiple addresses sharing a
                    // render queue. DropBall drives the production state and ball.
                    yield return null;CheckDeadline();
                    var before=Snapshot();int rebuildBefore=turf.RebuildCount;int queriesBefore=turf.GroundQueriesThisRebuild;
                    game.DropBall(pose.point);game.enabled=false;rig.enabled=false;HideAim();golfer.SetVisible(true);
                    var aim=HoleView.ToWorld(hole.Pin)-ball.position;aim.y=0;
                    if(aim.sqrMagnitude<.00001f)aim=Vector3.forward;aim.Normalize();
                    cam.fieldOfView=60;rig.FrameAddress(ball.position,aim,false);rig.SnapNext();rig.ApplyFrame();
                    Check(DryPlayable(pose.point)&&!UnderRenderedWater(ball.position-Vector3.up*.06f),"Selected address is water/nonplayable or below rendered water: "+pose.label);
                    Check(!UnderRenderedWater(cam.transform.position),"Address camera is below a rendered water sheet: "+pose.label);
                    Check(game.Current==GolfGame.State.Aim,"DropBall did not enter real Aim state");
                    pendingPlacedFrame=Time.frameCount;pendingImage=Path.Combine(config.output,$"hole{number:00}_{pose.label}.png");pending=true;
                    while(pending){CheckDeadline();yield return null;}
                    Check(lastLateUpdateFrame==lastReadFrame,"ReadPixels did not follow the actual production LateUpdate frame");
                    var after=Snapshot();
                    GrassAt(pose.point,out var selectedPoint,out var selectedCollider,out var selectedSurface);
                    var p=new PoseReport{hole=number,label=pose.label,image=pendingImage,placedFrame=pendingPlacedFrame,
                        renderedFrame=lastReadFrame,lateUpdateFrame=lastLateUpdateFrame,rebuildBefore=rebuildBefore,rebuildAfter=lastLateRebuild,
                        cells=turf.CellCount,near=turf.LastNearCells,mid=turf.LastMidCells,far=turf.LastFarCells,
                        selectedCollider=selectedCollider,selectedSurface=SurfaceName(selectedSurface),courseLie=hole.LieAt(pose.point).ToString(),
                        requestedPoint=new Vector3((float)pose.requested.X,0,(float)pose.requested.D),
                        selectionDistanceMeters=(float)pose.point.DistanceTo(pose.requested)*.9144f,clearanceMeters=pose.clearance,
                        groundQueries=turf.GroundQueriesThisRebuild,colliderRaycasts=turf.ColliderRaycastsThisRebuild,
                        rebuildOccurred=lastLateRebuild!=rebuildBefore,rebuildMilliseconds=lastLateRebuild!=rebuildBefore?turf.RebuildMilliseconds:0,
                        lastRebuildMilliseconds=turf.RebuildMilliseconds,ball=ball.position,patchCenter=turf.PatchCenter,
                        ownedMaterialCount=MaterialIds().Length,materialsExact=initialMaterials.SequenceEqual(MaterialIds()),
                        matricesExact=true,sameStationNoRebuild=!pose.sameStation||lastLateRebuild==rebuildBefore};
                    Check(p.renderedFrame>=p.placedFrame,"Rendered stale frame");
                    Check(p.cells<=640&&p.near<=48&&p.mid<=96&&p.near+p.mid+p.far<=640,"Resident/LOD caps violated");
                    Check(p.materialsExact,"Owned material IDs/count changed while moving");
                    Check(p.sameStationNoRebuild,"Movement inside one coarse station rebuilt the pool");
                    if(turf.MovingEnabled){
                        float step=4/.9144f;
                        var expected=new Vector3((float)hole.Tee.X+Mathf.FloorToInt((ball.position.x-(float)hole.Tee.X)/step+.5f)*step,0,
                            (float)hole.Tee.D+Mathf.FloorToInt((ball.position.z-(float)hole.Tee.D)/step+.5f)*step);
                        Check((expected-turf.PatchCenter).sqrMagnitude<.000001f,"Production LateUpdate did not follow the actual addressed ball");
                        Check((game.BallPosition-ball.position).sqrMagnitude<.000001f,"Production BallPosition differs from displayed addressed ball");
                        if(pose.sameStation)Check(p.groundQueries==queriesBefore,"Same station changed ground-query diagnostics");
                        if(pose.label=="tee_coarse_cross")Check(p.rebuildAfter==rebuildBefore+1,"One coarse crossing did not produce exactly one rebuild");
                        Check(p.cells>0&&p.near+p.mid+p.far>0,"Actual grassy address produced no resident or submitted turf cells: "+pose.label);
                        CompareRevisited(earlier,after,p,pose.returnStation);
                    }
                    VerifyRetained(before,after,p);
                    p.poolHash=PoolHash(after);p.physicsHash=TextHash(PhysicsState());p.physicsExact=p.physicsHash==holeReport.baselinePhysicsHash;
                    p.physicsGate=p.physicsExact?"PASS":"FAIL";Check(p.physicsExact,"Original physics changed after address movement");
                    File.WriteAllText(Path.Combine(config.output,$"hole{number:00}_{pose.label}_physics.json"),PhysicsState());
                    holeReport.poses.Add(p);
                    if(pose.recordForReturn)earlier=after;
                    Flush();
                }
                if(config.exerciseFlight){
                    // Real shot; normal game Update and turf LateUpdate stay live.
                    yield return null;game.DropBall(hole.Tee);game.enabled=true;rig.enabled=true;
                    yield return null;
                    flightRebuildCount=turf.RebuildCount;flightStations.Clear();flightWatching=true;
                    var beforeFlight=PhysicsState();var windBefore=WindState();
                    holeReport.flightBeforeAudit=Path.Combine(config.output,$"hole{number:00}_flight_physics_before.json");
                    File.WriteAllText(holeReport.flightBeforeAudit,beforeFlight);
                    double launchBase=hole.Windmill?.CurrentAngle()??0;holeReport.windmillCurrentAngleBefore=launchBase;
                    try{
                        game.StrikeToward(hole.RecommendedTarget(hole.Tee));
                        Check(game.Current==GolfGame.State.Flight,"Real StrikeToward did not enter Flight");
                        if(hole.Windmill!=null){
                            double initialTime=Convert.ToDouble(Field(game,"flightTime"));holeReport.windmillInitialFlightTime=initialTime;
                            holeReport.windmillLaunchExpected=launchBase-hole.Windmill.DegreesPerSecond*initialTime;
                            holeReport.windmillLaunchActual=hole.Windmill.AngleAtLaunch;
                            holeReport.windmillLaunchExact=holeReport.windmillLaunchExpected==holeReport.windmillLaunchActual;
                        }else holeReport.windmillLaunchExact=true;
                        while(game.Current==GolfGame.State.Flight){CheckDeadline();yield return null;}
                    }finally{
                        flightWatching=false;game.enabled=false;rig.enabled=false;
                        // Preserve full source audits even when timeout/phase/rebuild
                        // checks throw during flight, before reporting any failure.
                        var afterFlight=PhysicsState();var windAfter=WindState();holeReport.windmillCurrentAngleAfter=hole.Windmill?.CurrentAngle()??0;
                        holeReport.flightAfterAudit=Path.Combine(config.output,$"hole{number:00}_flight_physics_after.json");
                        File.WriteAllText(holeReport.flightAfterAudit,afterFlight);
                        holeReport.windmillStaticExact=CompareWind(windBefore,windAfter,holeReport.windmillChangedFields);
                        if(hole.Windmill!=null)holeReport.windmillLaunchExact&=hole.Windmill.AngleAtLaunch==holeReport.windmillLaunchActual;
                        var beforeState=JsonUtility.FromJson<GolfVisualPhysicsGate.State>(beforeFlight);
                        var afterState=JsonUtility.FromJson<GolfVisualPhysicsGate.State>(afterFlight);
                        // This hash includes the allowed mutable launch phase; its
                        // geometry/rate/bindings are checked separately and exactly.
                        if(hole.Windmill!=null)afterState.windmillHash=beforeState.windmillHash;
                        holeReport.flightStaticPhysicsExact=JsonUtility.ToJson(beforeState)==JsonUtility.ToJson(afterState);
                        holeReport.flightCoarseStations=flightStations.Count;
                        holeReport.flightRebuildsAbsent=callbackError==null;
                        Flush();
                    }
                    Check(holeReport.flightFrames>0&&holeReport.flightCoarseStations>1,"Real shot did not cross multiple flight stations");
                    Check(holeReport.flightRebuildsAbsent,"Production turf rebuilt during actual flight");
                    Check(holeReport.flightStaticPhysicsExact,"Static physics changed during actual shot; full before/after audits saved");
                    Check(holeReport.windmillStaticExact&&holeReport.windmillLaunchExact&&holeReport.windmillPhaseExact,"Windmill changed outside its exact launch/flight contract; full audits and changed fields saved");
                    Check(initialMaterials.SequenceEqual(MaterialIds()),"Owned turf materials changed during actual shot");
                }
                else holeReport.flightRebuildsAbsent=true;
                holeReport.gate="PASS";Flush();
            }
            report.gate="PASS";Flush();Cleanup();GolfMovingTurfProof.Finish(0);
        }
        List<Pose> Poses(Hole h)
        {
            var middleWanted=PointAlong(h,.45);var approachWanted=PointAlong(h,Math.Max(.65,1-18/Math.Max(h.Length,1)));
            // A centerline fraction can cross open water on the authored par3.
            // Choose real supported grass, with an actual nearest-face clearance
            // test, and enough separation to retire/regenerate the return pool.
            var middle=ChooseGrass(middleWanted,h.Tee,30);
            var approach=ChooseGrass(approachWanted,middle,30);
            GrassAt(middle,out var unusedMiddle,out var unusedCollider,out var middleSurface);
            GrassAt(approach,out var unusedApproach,out var unusedApproachCollider,out var approachSurface);
            string middleLabel=middleSurface==1?"fairway":"mid_"+SurfaceName(middleSurface).ToLowerInvariant();
            string approachLabel="approach_"+SurfaceName(approachSurface).ToLowerInvariant();
            return new List<Pose>{new(){label="tee",point=h.Tee,requested=h.Tee},
                new(){label="tee_same_station",point=new CoursePoint(h.Tee.X+.5/.9144,h.Tee.D),requested=h.Tee,sameStation=true},
                new(){label="tee_coarse_cross",point=new CoursePoint(h.Tee.X+4.1/.9144,h.Tee.D),requested=h.Tee},
                new(){label=middleLabel,point=middle,requested=middleWanted,clearance=2,recordForReturn=true},
                new(){label=approachLabel,point=approach,requested=approachWanted,clearance=2},
                new(){label="return_"+middleLabel,point=middle,requested=middleWanted,clearance=2,returnStation=true},
                new(){label="return_tee",point=h.Tee,requested=h.Tee}};
        }
        void CacheSelectionGround()
        {
            selectionGround.Clear();CacheRenderedWater();
            foreach(var collider in turf.gameObject.GetComponentsInChildren<MeshCollider>()){
                if(!collider.sharedMesh)continue;
                var ends=new int[collider.sharedMesh.subMeshCount];int total=0;
                for(int i=0;i<ends.Length;i++){total+=(int)collider.sharedMesh.GetIndexCount(i)/3;ends[i]=total;}
                selectionGround.Add(new SelectionGround{collider=collider,bounds=collider.bounds,materials=collider.GetComponent<Renderer>()?.sharedMaterials,triangleEnds=ends});
            }
        }
        bool GrassAt(CoursePoint at,out Vector3 point,out string colliderName,out float surface)
        {
            point=default;colliderName="none";surface=-1;
            if(!DryPlayable(at))return false;
            SelectionGround land=null;RaycastHit best=default;float nearest=float.MaxValue;
            var ray=new Ray(new Vector3((float)at.X,1000,(float)at.D),Vector3.down);
            foreach(var source in selectionGround){
                if(!source.collider||!source.collider.enabled||!source.collider.gameObject.activeInHierarchy
                    ||at.X<source.bounds.min.x||at.X>source.bounds.max.x||at.D<source.bounds.min.z||at.D>source.bounds.max.z)continue;
                if(source.collider.Raycast(ray,out var hit,2000)&&hit.distance<nearest){land=source;best=hit;nearest=hit.distance;}
            }
            if(land==null||best.normal.y<.92f||UnderRenderedWater(best.point))return false;
            colliderName=land.collider.name;
            if(!GolfCoastalTurf.EligibleCollider(colliderName,hole.Number))return false;
            int sub=0;while(sub<land.triangleEnds.Length&&best.triangleIndex>=land.triangleEnds[sub])sub++;
            var mats=land.materials;if(mats==null||sub>=mats.Length||!mats[sub])return false;
            if(colliderName.StartsWith("TERRAIN")){if(!mats[sub].name.Contains(" Rough"))return false;surface=0;}
            else surface=mats[sub].HasProperty("_Surface")?mats[sub].GetFloat("_Surface"):colliderName.StartsWith("TEE")?3:1;
            if(colliderName.StartsWith("GREEN_APRON")&&surface!=1&&surface!=4)return false;
            if(surface!=0&&surface!=1&&surface!=3&&surface!=4)return false;
            // Reuse the actual cached visual-path mask without invoking Ground,
            // changing runtime query counters or moving/submitting the turf pool.
            var onPath=typeof(GolfCoastalTurf).GetMethod("OnVisualPath",Private);
            Check(onPath!=null,"Accepted visual-path predicate missing");
            if((bool)onPath.Invoke(turf,new object[]{best.point}))return false;
            point=best.point;return true;
        }
        CoursePoint ChooseGrass(CoursePoint desired,CoursePoint awayFrom,float separationMeters)
        {
            var candidates=new List<Vector3>();
            foreach(var source in selectionGround){
                var mesh=source.collider.sharedMesh;var mats=source.materials;string name=source.collider.name;
                if(!mesh.isReadable||mats==null||!GolfCoastalTurf.EligibleCollider(name,hole.Number))continue;
                var vertices=mesh.vertices;
                for(int sub=0;sub<Math.Min(mesh.subMeshCount,mats.Length);sub++){
                    var material=mats[sub];if(!material)continue;
                    float role=material.HasProperty("_Surface")?material.GetFloat("_Surface"):-1;
                    if(name.StartsWith("GREEN_APRON")&&role!=1&&role!=4)continue;
                    if(name.StartsWith("TERRAIN")?!material.name.Contains(" Rough"):(role!=0&&role!=1&&role!=3&&role!=4))continue;
                    var indices=mesh.GetTriangles(sub);
                    for(int i=0;i+2<indices.Length;i+=3){
                        var a=source.collider.transform.TransformPoint(vertices[indices[i]]);var b=source.collider.transform.TransformPoint(vertices[indices[i+1]]);var c=source.collider.transform.TransformPoint(vertices[indices[i+2]]);
                        if(Vector3.Cross(b-a,c-a).normalized.y<.92f)continue;
                        var candidate=(a+b+c)/3;
                        if(new CoursePoint(candidate.x,candidate.z).DistanceTo(awayFrom)*.9144>=separationMeters)candidates.Add(candidate);
                    }
                }
            }
            float Distance(Vector3 p)=>((float)desired.X-p.x)*((float)desired.X-p.x)+((float)desired.D-p.z)*((float)desired.D-p.z);
            candidates.Sort((a,b)=>{int order=Distance(a).CompareTo(Distance(b));if(order!=0)return order;order=a.z.CompareTo(b.z);return order!=0?order:a.x.CompareTo(b.x);});
            foreach(var candidate in candidates){
                var at=new CoursePoint(candidate.x,candidate.z);
                if(!GrassAt(at,out var root,out var unused,out var role))continue;
                bool clear=true;
                foreach(var direction in ClearanceDirections){
                    var edge=new CoursePoint(at.X+direction.x*2/.9144,at.D+direction.y*2/.9144);
                    if(!GrassAt(edge,out var edgeRoot,out var unusedCollider,out var unusedSurface)||Mathf.Abs(edgeRoot.y-root.y)>1f){clear=false;break;}
                }
                if(clear)return at;
            }
            throw new InvalidOperationException($"No actual supported grass with 2 m clearance and {separationMeters} m separation near ({desired.X:R},{desired.D:R}) on hole {hole.Number}");
        }
        static string SurfaceName(float surface)=>surface==0?"Rough":surface==1?"Fairway":surface==3?"Tee":surface==4?"Fringe":"Excluded";
        static CoursePoint PointAlong(Hole h,double fraction)
        {
            double wanted=h.Length*fraction;
            for(int i=1;i<h.Centerline.Length;i++){
                var a=h.Centerline[i-1];var b=h.Centerline[i];double length=a.DistanceTo(b);
                if(wanted<=length||i==h.Centerline.Length-1){double t=Math.Min(1,wanted/Math.Max(length,.00001));return new CoursePoint(a.X+(b.X-a.X)*t,a.D+(b.D-a.D)*t);}
                wanted-=length;
            }
            return h.Tee;
        }
        void HideAim()
        {
            var line=(LineRenderer)Field(game,"aimLine");if(line)line.enabled=false;
            var marker=(Transform)Field(game,"landingMarker");if(marker)marker.gameObject.SetActive(false);
            var dots=(AimDots)Field(game,"aimDots");if(dots)dots.Hide();
            var landing=(LandingZone)Field(game,"landingZone");if(landing)landing.gameObject.SetActive(false);
            var grid=(GreenRead)Field(game,"greenRead");if(grid)grid.Hide();
        }
        void LateUpdate()
        {
            if(!turf||!game)return;
            lastLateUpdateFrame=Time.frameCount;lastLateRebuild=turf.RebuildCount;
            if(flightWatching&&game.Current==GolfGame.State.Flight){
                holeReport.flightFrames++;
                var p=game.BallPosition;float step=4/.9144f;
                flightStations.Add(new Vector2Int(Mathf.FloorToInt((p.x-(float)hole.Tee.X)/step+.5f),Mathf.FloorToInt((p.z-(float)hole.Tee.D)/step+.5f)));
                if(turf.RebuildCount!=flightRebuildCount)callbackError="Production turf rebuilt while actual game state was Flight";
                if(hole.Windmill!=null){
                    double time=Convert.ToDouble(Field(game,"flightTime"));
                    double error=Math.Abs(hole.Windmill.CurrentAngle()-hole.Windmill.AngleAt(time));
                    holeReport.windmillMaximumPhaseError=Math.Max(holeReport.windmillMaximumPhaseError,error);
                    if(error>1e-9)holeReport.windmillPhaseExact=false;
                }
            }
        }
        void AfterRender(ScriptableRenderContext context,List<Camera> cameras)
        {
            if(!pending||!cam||!cameras.Contains(cam)||lastLateUpdateFrame!=Time.frameCount||Time.frameCount<pendingPlacedFrame)return;
            try{
                Check(lastReadFrame!=Time.frameCount,"More than one address capture in actual Time.frameCount");
                var previous=RenderTexture.active;var texture=new Texture2D(config.width,config.height,TextureFormat.RGB24,false);
                try{RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,config.width,config.height),0,0);texture.Apply();File.WriteAllBytes(pendingImage,texture.EncodeToPNG());}
                finally{RenderTexture.active=previous;Object.DestroyImmediate(texture);}
                lastReadFrame=Time.frameCount;pending=false;
            }catch(Exception e){callbackError=e.ToString();pending=false;}
        }
        bool DryPlayable(CoursePoint at)=>hole.LieAt(at) is CourseLie.Tee or CourseLie.Fairway or CourseLie.Rough or CourseLie.Fringe;
        void CacheRenderedWater()
        {
            renderedWater.Clear();
            foreach(var filter in view.GetComponentsInChildren<MeshFilter>()){
                var mesh=filter.sharedMesh;if(!mesh||!mesh.isReadable||!filter.TryGetComponent<Renderer>(out var renderer)||!renderer.enabled||!renderer.gameObject.activeInHierarchy)continue;
                var mats=renderer.sharedMaterials;var vertices=mesh.vertices;
                for(int sub=0;sub<Math.Min(mesh.subMeshCount,mats.Length);sub++){
                    var mat=mats[sub];if(!mat)continue;string name=mat.name;
                    if(!(name.Contains(" Water")||name.Contains(" Shallow")||name.Contains(" Surf")||name.Contains(" Fall")||name.StartsWith("LK_WATER")||name.StartsWith("LK_SURF")||name.StartsWith("LK_FALL")||name.Contains(" Lava")||name.StartsWith("LK_LAVA")))continue;
                    var indices=mesh.GetTriangles(sub);
                    for(int i=0;i+2<indices.Length;i+=3){
                        var a=filter.transform.TransformPoint(vertices[indices[i]]);var b=filter.transform.TransformPoint(vertices[indices[i+1]]);var c=filter.transform.TransformPoint(vertices[indices[i+2]]);
                        if(Math.Abs((b.x-a.x)*(c.z-a.z)-(b.z-a.z)*(c.x-a.x))>.000001f)renderedWater.Add(new(a,b,c));
                    }
                }
            }
        }
        bool UnderRenderedWater(Vector3 point)
        {
            foreach(var triangle in renderedWater){
                if(point.x<triangle.lo.x||point.x>triangle.hi.x||point.z<triangle.lo.y||point.z>triangle.hi.y)continue;
                float bx=triangle.b.x-triangle.a.x,bz=triangle.b.z-triangle.a.z,cx=triangle.c.x-triangle.a.x,cz=triangle.c.z-triangle.a.z;
                float px=point.x-triangle.a.x,pz=point.z-triangle.a.z,den=bx*cz-bz*cx;
                float u=(px*cz-pz*cx)/den,v=(bx*pz-bz*px)/den;
                if(u<0||v<0||u+v>1)continue;
                float height=triangle.a.y+u*(triangle.b.y-triangle.a.y)+v*(triangle.c.y-triangle.a.y);
                if(point.y<=height+.05f)return true;
            }
            return false;
        }
        WindSnapshot WindState()
        {
            var result=new WindSnapshot{instance=hole.Windmill};
            if(result.instance==null)return result;
            result.angle=result.instance.CurrentAngle;result.drive=result.instance.Drive;
            foreach(var field in typeof(SpinningSails).GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)){
                var value=field.GetValue(result.instance);
                if(value is double number)result.fields[field.Name]=number.ToString("R",System.Globalization.CultureInfo.InvariantCulture);
                else if(value is double[] numbers)result.fields[field.Name]=Hash(w=>{w.Write(numbers.Length);foreach(var number in numbers)w.Write(number);});
            }
            return result;
        }
        static bool CompareWind(WindSnapshot before,WindSnapshot after,List<WindFieldChange> changes)
        {
            bool exact=ReferenceEquals(before.instance,after.instance)&&ReferenceEquals(before.angle,after.angle)&&ReferenceEquals(before.drive,after.drive)&&before.fields.Count==after.fields.Count;
            foreach(var entry in before.fields){
                bool exists=after.fields.TryGetValue(entry.Key,out var value);
                if(!exists||value!=entry.Value){changes.Add(new(){field=entry.Key,before=entry.Value,after=exists?value:"missing"});if(entry.Key!="AngleAtLaunch")exact=false;}
            }
            return exact;
        }
        string PhysicsState()
        {
            var method=typeof(GolfVisualPhysicsGate).GetMethod("Capture",BindingFlags.Static|BindingFlags.NonPublic);
            Check(method!=null,"Existing physics audit Capture method missing");
            return JsonUtility.ToJson(method.Invoke(null,new object[]{turf.gameObject,hole}),true);
        }
        int[] MaterialIds()
        {
            var materials=(IEnumerable)Field(turf,"groundMaterials");var ids=new List<int>();
            var grass=(Material)Field(turf,"grass");if(grass)ids.Add(grass.GetInstanceID());
            foreach(Material m in materials){Check(m,"Owned turf material was destroyed");ids.Add(m.GetInstanceID());}
            ids.Sort();return ids.ToArray();
        }
        Dictionary<Vector2Int,BlockSnapshot> Snapshot()
        {
            var result=new Dictionary<Vector2Int,BlockSnapshot>();var blocks=(IDictionary)Field(turf,"blocks");
            foreach(DictionaryEntry entry in blocks){
                var cells=((IEnumerable)Field(entry.Value,"cells")).Cast<object>().ToArray();
                result.Add((Vector2Int)entry.Key,new BlockSnapshot{block=entry.Value,cells=cells,hashes=cells.Select(CellHash).ToArray()});
            }
            return result;
        }
        static string CellHash(object cell)=>Hash(w=>{
            var matrix=(Matrix4x4)Field(cell,"matrix");for(int i=0;i<16;i++)w.Write(matrix[i]);
            var p=(Vector3)Field(cell,"center");w.Write(p.x);w.Write(p.y);w.Write(p.z);
            var b=(Bounds)Field(cell,"bounds");w.Write(b.center.x);w.Write(b.center.y);w.Write(b.center.z);w.Write(b.size.x);w.Write(b.size.y);w.Write(b.size.z);
            w.Write((bool)Field(cell,"rough"));w.Write((float)Field(cell,"surface"));
        });
        static string PoolHash(Dictionary<Vector2Int,BlockSnapshot> blocks)=>Hash(w=>{
            foreach(var entry in blocks.OrderBy(x=>x.Key.y).ThenBy(x=>x.Key.x)){
                w.Write(entry.Key.x);w.Write(entry.Key.y);w.Write(entry.Value.hashes.Length);foreach(var hash in entry.Value.hashes)w.Write(hash);
            }
        });
        static void VerifyRetained(Dictionary<Vector2Int,BlockSnapshot> before,Dictionary<Vector2Int,BlockSnapshot> after,PoseReport pose)
        {
            foreach(var entry in before){
                if(!after.TryGetValue(entry.Key,out var retained))continue;
                pose.retainedBlocks++;pose.retainedCells+=entry.Value.cells.Length;
                bool exact=ReferenceEquals(entry.Value.block,retained.block)&&entry.Value.cells.Length==retained.cells.Length&&entry.Value.hashes.SequenceEqual(retained.hashes);
                for(int i=0;exact&&i<retained.cells.Length;i++)exact=ReferenceEquals(entry.Value.cells[i],retained.cells[i]);
                pose.matricesExact&=exact;Check(exact,"Retained block/cell/matrix changed at "+entry.Key);
            }
        }
        static void CompareRevisited(Dictionary<Vector2Int,BlockSnapshot> earlier,Dictionary<Vector2Int,BlockSnapshot> after,PoseReport pose,bool requirePositiveComparison)
        {
            if(!pose.label.StartsWith("return_"))return;
            foreach(var entry in earlier){
                if(!after.TryGetValue(entry.Key,out var current))continue;
                if(requirePositiveComparison)Check(!ReferenceEquals(entry.Value.block,current.block),"Return comparison did not regenerate retired block: "+entry.Key);
                pose.regeneratedComparedCells+=current.hashes.Length;
                Check(entry.Value.hashes.SequenceEqual(current.hashes),"Revisited tile changed authored roots/rotations or admission count: "+entry.Key);
            }
            if(requirePositiveComparison)Check(pose.regeneratedComparedCells>0,"Return station had no comparable regenerated cells");
        }
        void Flush(){File.WriteAllText(Path.Combine(config.output,"moving-turf-proof.json"),JsonUtility.ToJson(report,true));}
        void Fail(Exception error){report.gate="FAIL";report.error=error.ToString();Flush();Debug.LogException(error);Cleanup();GolfMovingTurfProof.Finish(1);}
        void Cleanup()
        {
            RenderPipelineManager.endContextRendering-=AfterRender;
            GolfWindSway.FreezeTime=null;GolfWindSway.ForcedWind=null;
            if(cam)cam.targetTexture=null;if(target){target.Release();Object.DestroyImmediate(target);}
        }
        void OnDestroy(){RenderPipelineManager.endContextRendering-=AfterRender;}
    }
}
#endif
