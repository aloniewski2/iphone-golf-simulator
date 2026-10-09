using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace GolfArcade.Tennis
{
    /// Uses the same original sportswear meshes and fitted hair as the native editor.
    public sealed class TennisCustomization : MonoBehaviour
    {
        [Serializable] public class MeshData {
            public float[] positions,normals,uv,reference; public int[] triangles; public string texture,mask;
        }
        static readonly string[] HairColors={"211C1A","593722","A54D2B","D8B365","BCC0C5","365D99"};
        readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();
        public static Vector3 BodyScale(int height,int build) => new Vector3(1,.90f+Mathf.Clamp(height,0,4)*.05f,1);
        public static Vector3 FaceScale(int shape) => new[]{Vector3.one,new Vector3(1.12f,.94f,1),new Vector3(.92f,1.08f,1),new Vector3(1.16f,1,1)}[Mathf.Clamp(shape,0,3)];

        /// Shared mesh deformation: the skeleton, hand sockets and contact reach stay fixed.
        public static Vector3 Shape(Vector3 v,float size,int face,float headHeight=1.22f) {
            if(v.y>headHeight) {var f=FaceScale(face);v=new Vector3(v.x*f.x,headHeight+(v.y-headHeight)*f.y,v.z*f.z);}
            return v;
        }

        /// HERO_MAINSTAY: the tennis player base. Resources/Tennis/Customization/PlayerMale and PlayerFemale are the two match heroes
        /// (work/match-anim-set: the bald grey-mannequin male and female bodies with their own skeleton, the painted face, the classic
        /// racket on Hand_Racket, and their 16 clips), built by MatchHeroBuild. TennisHeroSetup puts one on every actor. The old
        /// Higgsfield tennis bases (PlayerMale.fbx / PlayerFemale.fbx) are archived under Assets/Characters/Archive and are not loaded.
        public static string HeroPath(bool female) => "Tennis/Customization/Player" + (female ? "Female" : "Male");
        public static GameObject HeroBase(bool female) => Resources.Load<GameObject>(HeroPath(female));

        /// Rebind the supplied base to the golf animation skeleton. Tennis no longer rebinds a base to its hidden gameplay rig: the body
        /// on court is the match hero (HeroBase), so for tennis this only checks that both heroes exist.
        public static bool AttachBase(Transform model,bool female,bool golf=false) {
            if(!golf) {
                if(!HeroBase(female)) throw new InvalidOperationException("Match hero base asset is missing: "+HeroPath(female));
                return true;
            }
            var prefab=Resources.Load<GameObject>("Tennis/Customization/Player"+(female?"Female":"Male")+"Golf");
            if(!prefab) throw new InvalidOperationException("Player base asset is missing");
            var bones=new Dictionary<string,Transform>();
            foreach(var t in model.GetComponentsInChildren<Transform>(true)) bones.TryAdd(t.name,t);
            var sources=prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            // Validate before hiding any current renderer.
            foreach(var source in sources) foreach(var bone in source.bones)
                if(!bone || !bones.ContainsKey(bone.name))throw new InvalidOperationException("Player base bone mismatch");
            foreach(var r in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))r.gameObject.SetActive(false);
            foreach(var source in sources) {
                var target=new GameObject(source.name).AddComponent<SkinnedMeshRenderer>();
                target.transform.SetParent(model,false);
                target.sharedMesh=source.sharedMesh;target.sharedMaterials=source.sharedMaterials;
                target.bones=Array.ConvertAll(source.bones,b=>bones[b.name]);
                target.rootBone=source.rootBone&&bones.TryGetValue(source.rootBone.name,out var root)?root:target.bones[0];
                target.updateWhenOffscreen=true;
            }
            return true;
        }

        /// Shared cosmetic mesh binding, also used by the modular Humanoid look prefab.
        public static SkinnedMeshRenderer RebindCosmetic(SkinnedMeshRenderer source, Transform sourceRoot, Transform targetRoot, Transform slot)
        {
            if (!source || !source.sharedMesh || !sourceRoot || !targetRoot || !slot)
                throw new InvalidOperationException("Cosmetic requires a mesh, skeleton and slot");
            if (source.sharedMesh.bindposes.Length != source.bones.Length)
                throw new InvalidOperationException("Cosmetic bind-pose count differs from its bone list: " + source.name);
            var lookup = new Dictionary<string, Transform>();
            foreach (var bone in targetRoot.GetComponentsInChildren<Transform>(true)) lookup.TryAdd(bone.name, bone);
            foreach (var bone in source.bones) if (bone && targetRoot.GetComponentsInChildren<Transform>(true).Count(t => t.name == bone.name) != 1)
                throw new InvalidOperationException("Cosmetic bone name must resolve once: " + bone.name);
            var rebound = Array.ConvertAll(source.bones, b => b && lookup.TryGetValue(b.name, out var match)
                ? match : throw new InvalidOperationException("Cosmetic bone mismatch: " + (b ? b.name : "null")));
            var renderer = new GameObject(source.name).AddComponent<SkinnedMeshRenderer>();
            renderer.transform.SetParent(slot, false);
            var local = sourceRoot.worldToLocalMatrix * source.transform.localToWorldMatrix;
            renderer.transform.localPosition = local.GetColumn(3);
            renderer.transform.localRotation = local.rotation;
            renderer.transform.localScale = local.lossyScale;
            renderer.sharedMesh = source.sharedMesh;
            renderer.sharedMaterials = source.sharedMaterials;
            renderer.bones = rebound;
            renderer.rootBone = source.rootBone && lookup.TryGetValue(source.rootBone.name, out var root) ? root : rebound[0];
            renderer.localBounds = source.localBounds;
            renderer.quality = SkinQuality.Bone4;
            renderer.updateWhenOffscreen = true;
            return renderer;
        }

        public void Apply(Transform model,Transform head,int skin,int hair,int hairColor,int face,int height,int build,float bodySize=-1,TennisLook.Kit? outfit=null)
        {
            if(!head)return;
            // The hidden tennis gameplay rig wears no player base (see AttachBase): nothing to dress, and no hair is ever added to it.
            if(!Array.Exists(model.GetComponentsInChildren<SkinnedMeshRenderer>(true),r=>r.name.Contains("Player")))return;
            float size=bodySize<0?Mathf.Clamp(build,0,4)/4f:Mathf.Clamp01(bodySize);
            bool female=Array.Exists(model.GetComponentsInChildren<Renderer>(),r=>r.name.Contains("PlayerFemale"));
            bool golf=Array.Exists(model.GetComponentsInChildren<Renderer>(),r=>r.name.EndsWith("Golf"));
            float headHeight=golf?1.32f:1.22f;
            string baseName="Player"+(female?"Female":"Male");
            var source=Resources.Load<Texture2D>("Tennis/Customization/"+baseName+"Color");
            var mask=Resources.Load<Texture2D>("Tennis/Customization/"+baseName+"Mask");
            var reference=JsonUtility.FromJson<SkinReference>(Resources.Load<TextAsset>("Tennis/Customization/"+baseName+"Mask").text);
            var tint=new Material(Resources.Load<Shader>("Tennis/Shaders/KitRecolor"));
            tint.SetTexture("_Mask",mask);tint.SetVector("_Ref",new Vector4(.4f,.2f,.6f,reference.skin));
            tint.SetColor("_Skin",GolfArcade.Game.GolferStyle.SkinTones[Mathf.Clamp(skin,0,5)]);
            var texture=new RenderTexture(source.width,source.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);owned.Add(texture);
            Graphics.Blit(source,texture,tint);Destroy(tint);
            var materials=new Dictionary<string,Material>();
            foreach(var r in model.GetComponentsInChildren<SkinnedMeshRenderer>()) {
                if(!r.name.Contains("Player"))continue;
                if(r.name.StartsWith("V4 Higgs body")) {
                    var mesh=Instantiate(r.sharedMesh);owned.Add(mesh);var vertices=mesh.vertices;
                    // Apply the same authored garment/body targets used by the native locker.
                    string targetName=size<.5f?"Slim":"Broad";
                    int shape=-1;
                    for(int j=0;j<mesh.blendShapeCount;j++)
                        if(mesh.GetBlendShapeName(j).EndsWith(targetName,StringComparison.Ordinal))shape=j;
                    if(shape>=0) {
                        var delta=new Vector3[vertices.Length];
                        mesh.GetBlendShapeFrameVertices(shape,0,delta,null,null);
                        float weight=Mathf.Abs(size-.5f)*2;
                        for(int j=0;j<vertices.Length;j++)vertices[j]+=delta[j]*weight;
                    }
                    mesh.ClearBlendShapes();
                    for(int i=0;i<vertices.Length;i++) {
                        // FBX retains Blender's Z-up mesh data; native JSON is already Y-up.
                        var v=vertices[i];v=Shape(new Vector3(v.x,v.z,-v.y),size,face,headHeight);
                        vertices[i]=new Vector3(v.x,-v.z,v.y);
                    }
                    mesh.vertices=vertices;mesh.RecalculateNormals();
                    // FBX duplicates vertices at UV seams. Average the normals at coincident
                    // positions so the supplied faces and body retain a smooth surface.
                    var normals=mesh.normals;var sums=new Dictionary<Vector3Int,Vector3>();
                    Vector3Int Key(Vector3 v)=>new Vector3Int(Mathf.RoundToInt(v.x*100000),Mathf.RoundToInt(v.y*100000),Mathf.RoundToInt(v.z*100000));
                    for(int i=0;i<vertices.Length;i++){var key=Key(vertices[i]);sums.TryGetValue(key,out var sum);sums[key]=sum+normals[i];}
                    for(int i=0;i<normals.Length;i++)normals[i]=sums[Key(vertices[i])].normalized;
                    mesh.normals=normals;mesh.RecalculateBounds();r.sharedMesh=mesh;
                }
                var mats=r.sharedMaterials;
                for(int i=0;i<mats.Length;i++) {
                    string name=mats[i].name;
                    if(!materials.TryGetValue(name,out var mat)) {
                        mat=new Material(Resources.Load<Shader>("Tennis/Shaders/TennisCharacter")){name=name};owned.Add(mat);materials[name]=mat;
                        mat.color=name.Contains("top")?new Color(.12f,.48f,.68f):name.Contains("bottom")?new Color(.035f,.08f,.17f):new Color(.9f,.93f,.95f);
                        mat.SetFloat("_Smoothness",.035f);mat.SetFloat("_RimStrength",.08f);
                        if(name.Contains("skin")){mat.color=Color.white;mat.mainTexture=texture;}
                        else if(name.Contains("limb"))mat.color=GolfArcade.Game.GolferStyle.SkinTones[Mathf.Clamp(skin,0,5)];
                        else if(name.Contains("trim"))mat.color=new Color(.94f,.95f,.91f);
                        else if(outfit.HasValue) {
                            var kit=outfit.Value;Color c=name.Contains("top")?kit.Shirt:name.Contains("bottom")?kit.Shorts:kit.Accent;
                            if(c.a>0)mat.color=c;
                        }
                    }
                    mats[i]=mat;
                }
                r.sharedMaterials=mats;
            }
            var socket=new GameObject("Customized hair").transform;
            socket.SetParent(head,false);
            var renderer=Array.Find(model.GetComponentsInChildren<SkinnedMeshRenderer>(),r=>r.name.StartsWith("V4 Higgs body")&&r.name.Contains("Player"));
            int headIndex=Array.IndexOf(renderer.bones,head);
            var bind=renderer.sharedMesh.bindposes[headIndex];
            socket.localPosition=bind.MultiplyPoint3x4(new Vector3(0,0,headHeight));
            socket.localRotation=bind.rotation*Quaternion.Euler(90,0,0);
            socket.localScale=FaceScale(face);
            if(hair>0)AddMesh(baseName+new[]{"","Swept","Curls","Bob"}[Mathf.Clamp(hair,1,3)],socket,skin,Mathf.Clamp(hairColor,0,5));
            // Size changes the mesh only; the gameplay root and contact reach stay fixed.
        }
        [Serializable] class SkinReference { public float skin; }
        void AddMesh(string name,Transform parent,int skin,int hairColor) {
            var asset=Resources.Load<TextAsset>("Tennis/Customization/"+name);if(!asset)return;
            var d=JsonUtility.FromJson<MeshData>(asset.text);int count=d.positions.Length/3;
            var v=new Vector3[count];var n=new Vector3[count];var uv=new Vector2[count];
            for(int i=0;i<count;i++){v[i]=new Vector3(d.positions[i*3],d.positions[i*3+1],d.positions[i*3+2]);n[i]=new Vector3(d.normals[i*3],d.normals[i*3+1],d.normals[i*3+2]);uv[i]=new Vector2(d.uv[i*2],d.uv[i*2+1]);}
            var triangles=d.triangles;
            var mesh=new Mesh {name=name,indexFormat=IndexFormat.UInt32,vertices=v,normals=n,uv=uv,triangles=triangles};mesh.RecalculateBounds();mesh.RecalculateTangents();owned.Add(mesh);
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var mat=new Material(Resources.Load<Shader>("Tennis/Shaders/TennisCharacter"));owned.Add(mat);mat.SetFloat("_Smoothness",.05f);mat.SetFloat("_RimStrength",.08f);
            if(hairColor>=0){ColorUtility.TryParseHtmlString("#"+HairColors[hairColor],out var c);mat.color=c;}
            else {
                var source=Resources.Load<Texture2D>("Tennis/Customization/"+d.texture);
                var mask=Resources.Load<Texture2D>("Tennis/Customization/"+d.mask);
                if(source && mask) {
                    var tint=new Material(Resources.Load<Shader>("Tennis/Shaders/KitRecolor"));
                    tint.SetTexture("_Mask",mask);tint.SetVector("_Ref",new Vector4(d.reference[0],d.reference[1],d.reference[2],d.reference[3]));
                    tint.SetColor("_Skin",GolfArcade.Game.GolferStyle.SkinTones[Mathf.Clamp(skin,0,5)]);
                    var target=new RenderTexture(source.width,source.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);owned.Add(target);
                    Graphics.Blit(source,target,tint);Destroy(tint);mat.mainTexture=target;
                } else mat.color=GolfArcade.Game.GolferStyle.SkinTones[Mathf.Clamp(skin,0,5)];
            }
            go.AddComponent<MeshRenderer>().sharedMaterial=mat;
        }
        void OnDestroy(){foreach(var obj in owned)if(obj)Destroy(obj);}
    }

}
