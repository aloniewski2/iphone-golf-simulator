#if UNITY_EDITOR
using System;using System.Linq;using System.IO;using System.Collections.Generic;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;using GolfArcade.Tennis;using Object=UnityEngine.Object;
namespace GolfArcade.EditorTools {
 public static class HeroThumbRigProbe {
  [Serializable] sealed class SourceCamera {public string source;public float[] sourceToWorld;public float[] frontPosition,sidePosition,target;public float orthographicHeight;}
  [Serializable] sealed class NormalVertex {public float[] p,pn;public float strength;}
  [Serializable] sealed class NormalStage {public string shape;public NormalVertex[] entries;}
  [Serializable] sealed class NormalGolden {public NormalVertex[] left;public NormalStage[] stages;}
  [Serializable] sealed class SourceStage {public int percent;public float[] posedPositions;}
  [Serializable] sealed class SourceGolden {public string source;public float[] basePositions,posedPositions;public SourceStage[] stages;}
  static Vector3[] Skin(SkinnedMeshRenderer body){var v=body.sharedMesh.vertices;for(int s=0;s<body.sharedMesh.blendShapeCount;s++){float amount=body.GetBlendShapeWeight(s)/100f;if(Mathf.Abs(amount)<.000001f)continue;var delta=new Vector3[v.Length];body.sharedMesh.GetBlendShapeFrameVertices(s,body.sharedMesh.GetBlendShapeFrameCount(s)-1,delta,null,null);for(int i=0;i<v.Length;i++)v[i]+=delta[i]*amount;}var w=body.sharedMesh.boneWeights;var bind=body.sharedMesh.bindposes;var matrices=body.bones.Select((b,i)=>body.transform.worldToLocalMatrix*b.localToWorldMatrix*bind[i]).ToArray();var result=new Vector3[v.Length];for(int i=0;i<v.Length;i++){var b=w[i];result[i]=matrices[b.boneIndex0].MultiplyPoint3x4(v[i])*b.weight0+matrices[b.boneIndex1].MultiplyPoint3x4(v[i])*b.weight1+matrices[b.boneIndex2].MultiplyPoint3x4(v[i])*b.weight2+matrices[b.boneIndex3].MultiplyPoint3x4(v[i])*b.weight3;}return result;}
  static Vector3[] SurfaceNormals(Mesh mesh){
   var p=mesh.vertices;var normals=new Vector3[p.Length];var triangles=mesh.triangles;
   Vector3Int Key(Vector3 v)=>new(Mathf.RoundToInt(v.x*100000),Mathf.RoundToInt(v.y*100000),Mathf.RoundToInt(v.z*100000));
   var sums=new Dictionary<Vector3Int,Vector3>();
   for(int t=0;t<triangles.Length;t+=3){int a=triangles[t],b=triangles[t+1],c=triangles[t+2];var area=Vector3.Cross(p[b]-p[a],p[c]-p[a]);foreach(int i in new[]{a,b,c}){var k=Key(p[i]);sums[k]=sums.GetValueOrDefault(k)+area;}}
   for(int i=0;i<p.Length;i++)normals[i]=sums[Key(p[i])].normalized;return normals;
  }
  public static void Run(){
   int code=0;string output=null;var report=new System.Text.StringBuilder();try {
    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);output=Environment.GetEnvironmentVariable("HTR_OUT")??Path.GetFullPath("../proof/full-visual-overhaul/thumb-rig-probe-1");Directory.CreateDirectory(output);
    var cam=new GameObject("Anatomical thumb proof").AddComponent<Camera>();cam.backgroundColor=new Color(.22f,.27f,.34f);cam.clearFlags=CameraClearFlags.SolidColor;cam.orthographic=true;cam.orthographicSize=.12f;
    var light=new GameObject("Thumb proof key").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.5f;light.transform.rotation=Quaternion.Euler(30,-30,0);RenderSettings.ambientLight=Color.white*.4f;RenderSettings.fog=false;
    foreach(string sex in new[]{"Male","Female"}){
     foreach(string folder in new[]{"Hands","HandRest","HandPose"}) {
      string path="Assets/Resources/Tennis/Premium/"+folder+"/"+sex+".fbx";var importer=AssetImporter.GetAtPath(path) as ModelImporter;
      if(importer){importer.isReadable=true;importer.importAnimation=folder=="HandPose";importer.animationType=ModelImporterAnimationType.Generic;importer.importNormals=ModelImporterNormals.Import;importer.meshCompression=ModelImporterMeshCompression.Off;importer.SaveAndReimport();}
     }
     var root=Object.Instantiate(Resources.Load<GameObject>("Tennis/Customization/Player"+sex));var hero=root.GetComponent<MatchHeroLook>();var driver=root.GetComponent<HeroTennisDriver>();driver.enabled=false;
     var original=hero.body.sharedMesh;HeroThumbRigMigration migration=null;float maximum=0;
     foreach(var slot in driver.slots.Where(s=>s.clip))foreach(float phase in new[]{0f,.25f,.5f,.75f,1f}){
      if(migration)migration.RestoreRaw();slot.clip.SampleAnimation(root,slot.clip.length*phase);
      var live=hero.body.sharedMesh;hero.body.sharedMesh=original;var before=Skin(hero.body);hero.body.sharedMesh=live;
      migration=HeroThumbRigMigration.Prepare(hero);migration.Rebase();var after=Skin(hero.body);float max=0,sum=0;for(int v=0;v<before.Length;v++){float error=Vector3.Distance(before[v],after[v]);max=Mathf.Max(max,error);sum+=error;}maximum=Mathf.Max(maximum,max);
      report.AppendLine(sex+" "+slot.id+" "+phase+" old/new skin max="+max.ToString("G8")+" mean="+(sum/before.Length).ToString("G8"));
     }
     report.AppendLine(sex+" RUNTIME_REBASE_PARITY "+maximum);if(maximum>.00002f)throw new InvalidOperationException(sex+" old-clip skinning parity failed "+maximum);
     Object.DestroyImmediate(root);
     bool partials=Environment.GetEnvironmentVariable("HTR_PARTIALS")=="1";
     foreach(string mode in partials?new[]{"baseline","fist25","fist50","fist75","fist_closed"}:new[]{"baseline","fist","fist_closed"}){
      root=Object.Instantiate(Resources.Load<GameObject>("Tennis/Customization/Player"+sex));hero=root.GetComponent<MatchHeroLook>();driver=root.GetComponent<HeroTennisDriver>();driver.enabled=false;driver.slots.First(s=>s.id==HeroTennisDriver.Clip.Ready).clip.SampleAnimation(root,.6f);TennisPerformanceBuild.Pose(root.transform,hero,HeroTennisDriver.Clip.MatchWin,1,false);
      var originalBody=hero.body.sharedMesh;var originalPalette=hero.body.bones.ToArray();var originalSkin=Skin(hero.body);var originalHandBind=originalBody.bindposes[Array.FindIndex(originalPalette,b=>b&&b.name=="LeftHand")];
      if(Environment.GetEnvironmentVariable("HTR_REBUILT")=="1"){
       HeroContinuousHandMesh.Apply(hero);hero.body.sharedMesh.SetUVs(1,hero.body.sharedMesh.vertices.ToList());
       if(Environment.GetEnvironmentVariable("HTR_NORMALS")=="1")hero.body.sharedMesh=HeroHandNormalFields.Prepare(hero.body.sharedMesh,hero.female);
      }
      migration=HeroThumbRigMigration.Prepare(hero);migration.Rebase();if(!hero.body.sharedMesh.name.Contains("(continuous hands)"))migration.ApplyAnatomicalWeights();
      var hand=hero.Bone(HumanBodyBones.LeftHand);var bones=hero.body.bones;var binds=hero.body.sharedMesh.bindposes;var rest=bones.Select((b,i)=>new{b,m=binds[i].inverse}).ToDictionary(x=>x.b,x=>x.m);var toHand=rest[hand].inverse;
      Transform B(string digit,string joint)=>hero.Bone((HumanBodyBones)Enum.Parse(typeof(HumanBodyBones),"Left"+digit+joint));Vector3 H(Transform b)=>toHand.MultiplyPoint3x4(rest[b].GetColumn(3));
      var palm=Vector3.Cross((H(B("Index","Proximal"))-H(B("Little","Proximal"))).normalized,H(B("Middle","Proximal")).normalized).normalized;
      int percent=mode=="fist25"?25:mode=="fist50"?50:mode=="fist75"?75:100;
      if(partials&&mode.StartsWith("fist")){
       HeroAuthoredHandPose.ResetDigitsToRest(hero,true);
       if(!HeroAuthoredHandPose.ApplyFist(hero,true,percent*.01f))throw new InvalidOperationException("Partial proof requires authored migrated hand surface");
      }else if(mode.StartsWith("fist"))HeroGripPolish.ApplyFist(hero,true,mode=="fist_closed"||mode=="fist60"||mode=="fist78"?1f:.85f,mode=="fist60"?.60f:mode=="fist78"?.78f:.68f);
      string[] joints={"Proximal","Intermediate","Distal"};for(int j=0;j<3;j++){
       var bone=B("Thumb",joints[j]);var next=j<2?B("Thumb",joints[j+1]):null;var frame=toHand*rest[bone];var direction=next?H(next)-H(bone):frame.MultiplyVector(Vector3.up);var hinge=frame.inverse.MultiplyVector(Vector3.Cross(direction.normalized,palm).normalized).normalized;
       float degrees=mode.StartsWith(joints[j]+"_")?(mode.EndsWith("plus")?30:-30):mode=="fist_thumb_plus"?new[]{25f,30f,15f}[j]:mode=="fist_thumb_minus"?-new[]{25f,30f,15f}[j]:0;
       if(degrees!=0)bone.localRotation*=Quaternion.AngleAxis(degrees,hinge);
       report.AppendLine(sex+" "+mode+" "+bone.name+" head="+H(bone)+" hinge="+hinge+" degrees="+degrees);
      }
      // Compare original/rebuilt wrist under the same final pose, including the
      // deliberate new-reaction wrist straightening. No old source clip is edited.
      var keepMesh=hero.body.sharedMesh;var keepPalette=hero.body.bones;hero.body.sharedMesh=originalBody;hero.body.bones=originalPalette;originalSkin=Skin(hero.body);hero.body.sharedMesh=keepMesh;hero.body.bones=keepPalette;
      var currentVertices=hero.body.sharedMesh.vertices;var currentSkin=Skin(hero.body);var currentWeights=hero.body.sharedMesh.boneWeights;var oldVertices=originalBody.vertices;var oldWeights=originalBody.boneWeights;
      Vector3Int Key(Vector3 v)=>new(Mathf.RoundToInt(v.x*1000000),Mathf.RoundToInt(v.y*1000000),Mathf.RoundToInt(v.z*1000000));
      var lookup=currentVertices.Select((v,i)=>new{v,i}).GroupBy(v=>Key(v.v)).ToDictionary(g=>g.Key,g=>g.First().i);
      var errors=new float[3];var counts=new int[3];int missing=0;float weightError=0;
      Dictionary<string,float> Weights(BoneWeight w,Transform[] palette){var map=new Dictionary<string,float>();int[] ids={w.boneIndex0,w.boneIndex1,w.boneIndex2,w.boneIndex3};float[] ws={w.weight0,w.weight1,w.weight2,w.weight3};for(int k=0;k<4;k++)if(ws[k]>0){string name=palette[ids[k]].name;if(!map.ContainsKey(name))map[name]=0;map[name]+=ws[k];}return map;}
      for(int v=0;v<oldVertices.Length;v++){
       var local=originalHandBind.MultiplyPoint3x4(oldVertices[v]);if(Mathf.Abs(local.x)>.08f||Mathf.Abs(local.z)>.07f||local.y<-.03f||local.y>.034f)continue;
       var ow=Weights(oldWeights[v],originalPalette);if(ow.Any(k=>new[]{"Index","Middle","Ring","Little","Thumb"}.Any(d=>k.Key.Contains(d))&&k.Value>.001f))continue;
       if(!lookup.TryGetValue(Key(oldVertices[v]),out int current)){missing++;continue;}
       int band=local.y<-.005f?0:local.y<.020f?1:2;counts[band]++;errors[band]=Mathf.Max(errors[band],Vector3.Distance(originalSkin[v],currentSkin[current]));var nw=Weights(currentWeights[current],hero.body.bones);foreach(string n in ow.Keys.Union(nw.Keys))weightError=Mathf.Max(weightError,Mathf.Abs(ow.GetValueOrDefault(n)-nw.GetValueOrDefault(n)));
      }
      report.AppendLine(sex+" "+mode+" WRIST_EXACT_ORIGINAL bands[-30,-5]/[-5,20]/[20,34]mm counts="+string.Join(",",counts)+" skinnedMaxMetres="+string.Join(",",errors.Select(e=>e.ToString("G8")))+" missing="+missing+" weightMax="+weightError);
      if(mode=="fist_closed"||(partials&&mode.StartsWith("fist"))){
       var origin=HeroAuthoredHandPose.ExpectedSourcePoint(hero,true,Vector3.zero);var sourceMap=Matrix4x4.identity;
       sourceMap.SetColumn(0,(Vector4)(HeroAuthoredHandPose.ExpectedSourcePoint(hero,true,Vector3.right)-origin));sourceMap.SetColumn(1,(Vector4)(HeroAuthoredHandPose.ExpectedSourcePoint(hero,true,Vector3.up)-origin));sourceMap.SetColumn(2,(Vector4)(HeroAuthoredHandPose.ExpectedSourcePoint(hero,true,Vector3.forward)-origin));sourceMap.SetColumn(3,new Vector4(origin.x,origin.y,origin.z,1));
       Vector3 centreForSource=hand.position+hand.up*.07f;float[] F(Vector3 v)=>new[]{v.x,v.y,v.z};
       var sourceCamera=new SourceCamera{source="Premium_"+sex+"_FistSurfaceV3.blend",sourceToWorld=Enumerable.Range(0,16).Select(i=>sourceMap[i]).ToArray(),frontPosition=F(sourceMap.inverse.MultiplyPoint3x4(centreForSource+new Vector3(-.35f,.04f,.65f))),sidePosition=F(sourceMap.inverse.MultiplyPoint3x4(centreForSource+new Vector3(-.65f,.08f,.12f))),target=F(sourceMap.inverse.MultiplyPoint3x4(centreForSource)),orthographicHeight=cam.orthographicSize*2};
       File.WriteAllText(Path.Combine(output,sex+"_source-camera-contract.json"),JsonUtility.ToJson(sourceCamera,true));
       string goldenPath=Path.GetFullPath("../work/full-visual-overhaul/characters/exports/HandsV3/"+sex+"_FistGolden.json");
       var golden=JsonUtility.FromJson<SourceGolden>(File.ReadAllText(goldenPath));var expected=new Dictionary<Vector3Int,Vector3>();
       Vector3Int SourceCell(Vector3 v)=>new(Mathf.RoundToInt(v.x/.000005f),Mathf.RoundToInt(v.y/.000005f),Mathf.RoundToInt(v.z/.000005f));
       var sourceCells=new Dictionary<Vector3Int,List<(Vector3 point,Vector3 wanted)>>();
       int neighbouringMatches=0,ambiguousMatches=0;float maxBaseAlignment=0;
       bool FindSource(Vector3 current,out Vector3 wanted){
        if(expected.TryGetValue(Key(current),out wanted))return true;
        var cell=SourceCell(current);float best=.000005f*.000005f;bool found=false;Vector3 bestWanted=Vector3.zero;bool ambiguous=false;
        for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++)for(int z=-1;z<=1;z++)if(sourceCells.TryGetValue(cell+new Vector3Int(x,y,z),out var candidates))foreach(var candidate in candidates){
         float d=(candidate.point-current).sqrMagnitude;if(d>best+1e-16f)continue;
         if(found&&Mathf.Abs(d-best)<1e-16f&&Vector3.Distance(bestWanted,candidate.wanted)>.0001f)ambiguous=true;
         if(d<best-1e-16f||!found){best=d;bestWanted=candidate.wanted;found=true;ambiguous=false;}
        }
        wanted=bestWanted;if(!found)return false;
        neighbouringMatches++;if(ambiguous)ambiguousMatches++;maxBaseAlignment=Mathf.Max(maxBaseAlignment,Mathf.Sqrt(best));return !ambiguous;
       }
       var posedPositions=golden.posedPositions;
       if(partials&&percent<100){var stage=golden.stages?.FirstOrDefault(s=>s.percent==percent);if(stage==null)throw new InvalidOperationException("Independent Blender partial golden missing "+sex+" "+percent);posedPositions=stage.posedPositions;}
       if(posedPositions.Length!=golden.basePositions.Length)throw new InvalidOperationException("Independent Blender hand golden length mismatch");
       for(int i=0;i<golden.basePositions.Length;i+=3){var basePoint=new Vector3(golden.basePositions[i],golden.basePositions[i+1],golden.basePositions[i+2]);var posedPoint=new Vector3(posedPositions[i],posedPositions[i+1],posedPositions[i+2]);var imported=HeroAuthoredHandPose.SourceToImportedBody(hero.female,basePoint);var wanted=HeroAuthoredHandPose.ExpectedSourcePoint(hero,true,posedPoint);expected[Key(imported)]=wanted;var cell=SourceCell(imported);if(!sourceCells.TryGetValue(cell,out var list))sourceCells[cell]=list=new List<(Vector3 point,Vector3 wanted)>();list.Add((imported,wanted));}
       int count=0,unmatched=0;float maximumError=0,meanError=0;int handIndex=Array.IndexOf(hero.body.bones,hand);
       for(int i=0;i<currentVertices.Length;i++){
        var bw=currentWeights[i];int[] indices={bw.boneIndex0,bw.boneIndex1,bw.boneIndex2,bw.boneIndex3};float[] weights={bw.weight0,bw.weight1,bw.weight2,bw.weight3};float amount=0;
        for(int k=0;k<4;k++)if(hero.body.bones[indices[k]].name=="LeftHand"||new[]{"Thumb","Index","Middle","Ring","Little"}.Any(d=>hero.body.bones[indices[k]].name.StartsWith("Left"+d)))amount+=weights[k];
        var local=hero.body.sharedMesh.bindposes[handIndex].MultiplyPoint3x4(currentVertices[i]);if(amount<.99f||local.y<.055f)continue;
        if(!FindSource(currentVertices[i],out var wanted)){unmatched++;continue;}float error=Vector3.Distance(hero.body.transform.TransformPoint(currentSkin[i]),wanted);maximumError=Mathf.Max(maximumError,error);meanError+=error;count++;
       }
       report.AppendLine(sex+" "+mode+" ACTUAL_BLENDER_FIST_GOLDEN compared="+count+" unmatched="+unmatched+" maxMetres="+maximumError.ToString("G8")+" meanMetres="+(meanError/Mathf.Max(1,count)).ToString("G8")+" source="+golden.source+" neighbouringMatches="+neighbouringMatches+" ambiguousMatches="+ambiguousMatches+" maxBaseAlignment="+maxBaseAlignment);
       if(partials&&(count<1000||unmatched!=0||maximumError>.0001f))throw new InvalidOperationException("Authored partial source/Unity position mismatch "+sex+" "+mode+" max="+maximumError+" compared="+count+" unmatched="+unmatched);
      }
      foreach(string digit in new[]{"Index","Middle","Ring","Little","Thumb"}){
       int di=Array.FindIndex(hero.body.bones,b=>b&&b.name=="Left"+digit+"Distal");var tipCandidates=new List<int>();float far=0;
       for(int v=0;v<currentVertices.Length;v++){var w=currentWeights[v];float amount=(w.boneIndex0==di?w.weight0:0)+(w.boneIndex1==di?w.weight1:0)+(w.boneIndex2==di?w.weight2:0)+(w.boneIndex3==di?w.weight3:0);if(amount<.4f)continue;tipCandidates.Add(v);far=Mathf.Max(far,hero.body.sharedMesh.bindposes[di].MultiplyPoint3x4(currentVertices[v]).magnitude);}
       var chosen=tipCandidates.Where(v=>hero.body.sharedMesh.bindposes[di].MultiplyPoint3x4(currentVertices[v]).magnitude>far*.8f).ToArray();var tip=chosen.Length>0?chosen.Aggregate(Vector3.zero,(a,v)=>a+currentSkin[v])/chosen.Length:Vector3.zero;
       var sourceTip=chosen.Length>0?chosen.Aggregate(Vector3.zero,(a,v)=>a+hero.body.sharedMesh.bindposes[di].MultiplyPoint3x4(currentVertices[v]))/chosen.Length:Vector3.zero;var rigTip=hero.body.bones[di].TransformPoint(sourceTip);var target=HeroGripPolish.PalmContactTarget(hero,true,digit);
       report.AppendLine(sex+" "+mode+" ACTUAL_WEIGHTED_TIP "+digit+" world="+hero.body.transform.TransformPoint(tip)+" handLocal="+hand.InverseTransformPoint(hero.body.transform.TransformPoint(tip))+" target="+target+" rigTip="+rigTip+" rigTipContactError="+Vector3.Distance(rigTip,target)+" actualSkinContactError="+Vector3.Distance(hero.body.transform.TransformPoint(tip),target)+" samples="+chosen.Length);
      }
      foreach(string digit in new[]{"Index","Middle","Ring","Little","Thumb"})foreach(string joint in new[]{"Proximal","Intermediate","Distal"}){
       var bone=B(digit,joint);int bi=Array.IndexOf(hero.body.bones,bone);var point=hand.InverseTransformPoint(bone.position);var parent=Array.IndexOf(hero.body.bones,bone.parent);var neutral=parent>=0 ? binds[parent]*binds[bi].inverse:Matrix4x4.identity;
       report.AppendLine(sex+" "+mode+" JOINT_FRAME "+bone.name+" handLocal="+point.ToString("F7")+" localPosition="+bone.localPosition.ToString("F7")+" rotation="+bone.localRotation.ToString("F7")+" scale="+bone.localScale.ToString("F7")+" neutralQuaternion="+neutral.rotation.ToString("F7"));
      }
      var mesh=new Mesh();hero.body.BakeMesh(mesh,false);var scaledMesh=new Mesh();hero.body.BakeMesh(scaledMesh,true);
      var bakeVertices=mesh.vertices;var scaledVertices=scaledMesh.vertices;float bakeMax=0,scaledMax=0;for(int i=0;i<currentSkin.Length;i++){bakeMax=Mathf.Max(bakeMax,Vector3.Distance(currentSkin[i],bakeVertices[i]));scaledMax=Mathf.Max(scaledMax,Vector3.Distance(currentSkin[i],scaledVertices[i]));}
      if((mode=="fist_closed"||(partials&&mode.StartsWith("fist")))&&Environment.GetEnvironmentVariable("HTR_NORMALS")=="1"){
       var normalGolden=JsonUtility.FromJson<NormalGolden>(Resources.Load<TextAsset>("Tennis/Premium/HandNormals/"+sex).text);var entries=normalGolden.left;
       if(partials&&percent<100){var stage=normalGolden.stages?.FirstOrDefault(s=>s.shape=="Hero_Fist_"+percent+"_Left");if(stage==null)throw new InvalidOperationException("Independent posed-normal stage missing "+sex+" "+mode);entries=stage.entries;}
       var normalMap=entries.Where(e=>e.strength>.999f).Select(e=>new{p=HeroAuthoredHandPose.SourceToImportedBody(hero.female,new Vector3(e.p[0],e.p[1],e.p[2])),n=(HeroAuthoredHandPose.ExpectedSourcePoint(hero,true,new Vector3(e.pn[0],e.pn[1],e.pn[2]))-HeroAuthoredHandPose.ExpectedSourcePoint(hero,true,Vector3.zero)).normalized}).GroupBy(e=>Key(e.p)).ToDictionary(g=>g.Key,g=>g.First().n);
       int matched=0;float sumAngle=0,maxAngle=0;var bakedNormals=mesh.normals;
       for(int v=0;v<currentVertices.Length;v++){if(!normalMap.TryGetValue(Key(currentVertices[v]),out var expectedNormal))continue;float angle=Vector3.Angle(hero.body.transform.TransformDirection(bakedNormals[v]).normalized,expectedNormal);sumAngle+=angle;maxAngle=Mathf.Max(maxAngle,angle);matched++;}
       report.AppendLine(sex+" "+mode+" AUTHORED_POSED_NORMAL_GOLDEN matched="+matched+" meanDegrees="+(sumAngle/Mathf.Max(1,matched))+" maxDegrees="+maxAngle);
       if(partials&&(matched<1000||maxAngle>.2f))throw new InvalidOperationException("Authored partial source/Unity normal mismatch "+sex+" "+mode+" "+maxAngle);
      }
      report.AppendLine(sex+" "+mode+" PHOTO_BAKE_AUDIT localScale="+hero.body.transform.localScale+" lossyScale="+hero.body.transform.lossyScale+" worldDeterminant="+hero.body.transform.localToWorldMatrix.determinant+" cpu_vs_BakeFalse_max="+bakeMax+" cpu_vs_BakeTrue_max="+scaledMax);Object.DestroyImmediate(scaledMesh);var node=new GameObject("Evaluated anatomical hand");node.transform.SetParent(hero.body.transform,false);node.AddComponent<MeshFilter>().sharedMesh=mesh;node.AddComponent<MeshRenderer>().sharedMaterials=hero.body.sharedMaterials;
      foreach(var r in root.GetComponentsInChildren<Renderer>(true))if(r.gameObject!=node)r.enabled=false;
      var centre=hand.position+hand.up*.07f;
      foreach(var shot in new[]{("front",new Vector3(-.35f,.04f,.65f)),("side",new Vector3(-.65f,.08f,.12f))}){
       cam.transform.SetPositionAndRotation(centre+shot.Item2,Quaternion.LookRotation(-shot.Item2));var rt=RenderTexture.GetTemporary(640,640,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Default,4);cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;var tex=new Texture2D(640,640,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,640,640),0,0);tex.Apply();File.WriteAllBytes(Path.Combine(output,sex+"_"+mode+"_"+shot.Item1+".png"),tex.EncodeToPNG());cam.targetTexture=null;RenderTexture.active=null;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(tex);
      }
      if(mode=="fist_closed"){
       var neutral=new Material(Shader.Find("GolfArcade/DiagnosticHandSurface"));neutral.SetColor("_BaseColor",new Color(.72f,.48f,.30f,1));
       var geometric=Object.Instantiate(mesh);geometric.normals=SurfaceNormals(mesh);
       var originalNormals=mesh.normals;var geometricNormals=geometric.normals;float normalMean=0,normalMax=0;int normalCount=0,normalBackwards=0;
       int hi=Array.IndexOf(hero.body.bones,hand);for(int i=0;i<currentVertices.Length;i++){var hp=binds[hi].MultiplyPoint3x4(currentVertices[i]);if(hp.y<.035f||hp.y>.2f||Mathf.Abs(hp.x)>.08f||Mathf.Abs(hp.z)>.075f)continue;float angle=Vector3.Angle(originalNormals[i],geometricNormals[i]);normalMean+=angle;normalMax=Mathf.Max(normalMax,angle);normalCount++;if(angle>90)normalBackwards++;}
       report.AppendLine(sex+" POSED_NORMAL_AUDIT count="+normalCount+" meanDegrees="+(normalMean/Mathf.Max(1,normalCount))+" maxDegrees="+normalMax+" reversedOver90="+normalBackwards);
       var skinMaterials=hero.body.sharedMaterials;
       foreach(string variant in new[]{"neutral_back","neutral_off","flat_back","flat_off","geometric_neutral","geometric_skin","live_skin"}){
        if(variant=="live_skin"){node.GetComponent<MeshRenderer>().enabled=false;hero.body.enabled=true;hero.body.updateWhenOffscreen=true;}
        else{neutral.SetFloat("_Cull",variant.EndsWith("_off")?0:2);neutral.SetFloat("_Flat",variant.StartsWith("flat_")?1:0);node.GetComponent<MeshFilter>().sharedMesh=variant.StartsWith("geometric_")?geometric:mesh;node.GetComponent<MeshRenderer>().sharedMaterials=variant=="geometric_skin"?skinMaterials:Enumerable.Repeat(neutral,mesh.subMeshCount).ToArray();}
        foreach(var shot in new[]{("front",new Vector3(-.35f,.04f,.65f)),("side",new Vector3(-.65f,.08f,.12f))}){
         cam.transform.SetPositionAndRotation(centre+shot.Item2,Quaternion.LookRotation(-shot.Item2));var rt=RenderTexture.GetTemporary(640,640,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Default,4);cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;var tex=new Texture2D(640,640,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,640,640),0,0);tex.Apply();File.WriteAllBytes(Path.Combine(output,sex+"_"+variant+"_"+shot.Item1+".png"),tex.EncodeToPNG());cam.targetTexture=null;RenderTexture.active=null;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(tex);
        }
       }
       hero.body.enabled=false;node.GetComponent<MeshRenderer>().enabled=true;node.GetComponent<MeshFilter>().sharedMesh=mesh;Object.DestroyImmediate(geometric);Object.DestroyImmediate(neutral);
      }
      if(mode=="baseline"||mode=="fist_closed"||mode=="fist60"||mode=="fist78"){
       string[] digits={"Thumb","Index","Middle","Ring","Little"};Color[] colours={new Color(.5f,.5f,.5f),Color.magenta,Color.red,Color.green,Color.blue,Color.yellow};var groups=Enumerable.Range(0,6).Select(_=>new List<int>()).ToArray();var triangles=mesh.triangles;
       for(int t=0;t<triangles.Length;t+=3){var amount=new float[6];for(int k=0;k<3;k++){var bw=currentWeights[triangles[t+k]];int[] ids={bw.boneIndex0,bw.boneIndex1,bw.boneIndex2,bw.boneIndex3};float[] ws={bw.weight0,bw.weight1,bw.weight2,bw.weight3};for(int q=0;q<4;q++)for(int d=0;d<5;d++)if(hero.body.bones[ids[q]].name.StartsWith("Left"+digits[d]))amount[d+1]+=ws[q];}int group=0;float best=.5f;for(int d=1;d<6;d++)if(amount[d]>best){best=amount[d];group=d;}groups[group].AddRange(new[]{triangles[t],triangles[t+1],triangles[t+2]});}
       var diagnostic=Object.Instantiate(mesh);diagnostic.subMeshCount=6;for(int d=0;d<6;d++)diagnostic.SetTriangles(groups[d],d);node.GetComponent<MeshFilter>().sharedMesh=diagnostic;var materials=colours.Select((c,d)=>new Material(Shader.Find("Universal Render Pipeline/Unlit")){name="Semantic digit "+d,color=c}).ToArray();node.GetComponent<MeshRenderer>().sharedMaterials=materials;
       foreach(var shot in new[]{("front",new Vector3(-.35f,.04f,.65f)),("side",new Vector3(-.65f,.08f,.12f))}){cam.transform.SetPositionAndRotation(centre+shot.Item2,Quaternion.LookRotation(-shot.Item2));var rt=RenderTexture.GetTemporary(640,640,24);cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;var tex=new Texture2D(640,640,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,640,640),0,0);tex.Apply();File.WriteAllBytes(Path.Combine(output,sex+"_"+mode+"_semantic_"+shot.Item1+".png"),tex.EncodeToPNG());cam.targetTexture=null;RenderTexture.active=null;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(tex);}
       report.AppendLine(sex+" semantic colours: gray palm/wrist, magenta thumb, red index, green middle, blue ring, yellow little");Object.DestroyImmediate(diagnostic);foreach(var material in materials)Object.DestroyImmediate(material);
      }
      Object.DestroyImmediate(root);Object.DestroyImmediate(mesh);
     }
    }
    File.WriteAllText(Path.Combine(output,"thumb-rig-v2-audit.txt"),report.ToString());Debug.Log("HERO_THUMB_RIG_PROBE_COMPLETE "+output);
   }catch(Exception e){Debug.LogException(e);code=1;if(output!=null){report.AppendLine("FAIL: "+e);File.WriteAllText(Path.Combine(output,"thumb-rig-v2-audit.txt"),report.ToString());}}if(Application.isBatchMode)EditorApplication.Exit(code);
  }
 }
}
#endif
