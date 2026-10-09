using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using GolfArcade.Tennis;
using Object=UnityEngine.Object;
namespace GolfArcade.EditorTools
{
    /// Static evaluated actual meshes: isolate authored patch placement from face shader,
    /// depth prepass, AO, skin timing and culling. Does not modify production prefabs.
    public static class HeroLidProbe
    {
        public static void Run(){
            string output=Path.GetFullPath(Environment.GetEnvironmentVariable("HLP_OUT")??"../proof/full-visual-overhaul/lid-probe-1");Directory.CreateDirectory(output);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            foreach(string sex in new[]{"Male","Female"}){
                bool golf=Environment.GetEnvironmentVariable("HLP_GOLF")=="1";GameObject root;
                if(golf){var built=GolfArcade.Game.HeroGolfer.Build(null,new GolfArcade.Game.HeroLook{Female=sex=="Female",Skin=new Color(.72f,.48f,.30f),HairColor=Color.black});root=built.Root;}
                else{var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Tennis/Customization/Player"+sex+".prefab");root=Object.Instantiate(prefab);}
                var look=root.GetComponent<MatchHeroLook>();typeof(MatchHeroLook).GetMethod("OwnMaterials",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(look,null);
                var driver=root.GetComponent<HeroTennisDriver>();if(driver){driver.ResolvePerformanceClips();driver.slots.First(s=>s.id==HeroTennisDriver.Clip.Ready).clip.SampleAnimation(root,.6f);}
                var source=look.face.GetComponent<MeshFilter>().sharedMesh;int sub=source.subMeshCount-1;
                var patch=new Mesh();patch.vertices=source.vertices.Select(v=>look.face.transform.TransformPoint(v)).ToArray();patch.normals=source.normals.Select(n=>look.face.transform.TransformDirection(n)).ToArray();patch.triangles=source.GetTriangles(sub);patch.RecalculateBounds();
                var body=new Mesh();look.body.BakeMesh(body,true);body.vertices=body.vertices.Select(v=>look.body.transform.TransformPoint(v)).ToArray();body.RecalculateBounds();
                var eyes=source.GetTriangles(Array.FindIndex(look.face.sharedMaterials,m=>m&&m.name.Contains("Sclera"))).Select(i=>look.face.transform.TransformPoint(source.vertices[i])).ToArray();
                var aperture=new Bounds(eyes[0],Vector3.zero);foreach(var v in eyes)aperture.Encapsulate(v);
                var capBounds=new Bounds(patch.vertices[patch.triangles[0]],Vector3.zero);foreach(int i in patch.triangles)capBounds.Encapsulate(patch.vertices[i]);
                File.WriteAllText(Path.Combine(output,sex+"-placement.txt"),"Face matrix="+look.face.transform.localToWorldMatrix+"\nFace raw bounds="+source.bounds+"\nSclera world="+aperture+"\nPatch world="+capBounds+"\n"+"FaceUp="+look.face.sharedMaterials[0].GetVector("_FaceUp")+"\n");
                foreach(var r in root.GetComponentsInChildren<Renderer>())r.enabled=false;
                GameObject Node(string name,Mesh mesh,Color c){var o=new GameObject(name);o.AddComponent<MeshFilter>().sharedMesh=mesh;var r=o.AddComponent<MeshRenderer>();var m=new Material(Shader.Find("Universal Render Pipeline/Unlit"));m.SetColor("_BaseColor",c);m.SetFloat("_Cull",0);r.sharedMaterial=m;return o;}
                var skin=Node("Actual evaluated body",body,new Color(.8f,.65f,.5f));var lids=Node("Actual appended cap",patch,Color.magenta);
                var camera=new GameObject("Probe camera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.2f,.5f,.6f);camera.orthographic=true;camera.orthographicSize=.18f;camera.nearClipPlane=.001f;
                var target=aperture.center;
                for(int view=0;view<2;view++)for(int mode=0;mode<2;mode++){
                    skin.SetActive(mode==0);var offset=view==0 ? new Vector3(0,0,1):new Vector3(.5f,0,.866f);
                    camera.transform.SetPositionAndRotation(target+offset,Quaternion.LookRotation(-offset));
                    var rt=RenderTexture.GetTemporary(1000,1000,24);var active=RenderTexture.active;camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
                    var tex=new Texture2D(1000,1000,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1000,1000),0,0);tex.Apply();
                    File.WriteAllBytes(Path.Combine(output,sex+"-"+(view==0 ? "front":"threequarter")+"-"+(mode==0 ? "with-body":"cap-only")+".png"),tex.EncodeToPNG());
                    camera.targetTexture=null;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(tex);
                }
                Object.DestroyImmediate(camera.gameObject);Object.DestroyImmediate(skin);Object.DestroyImmediate(lids);Object.DestroyImmediate(root);
                Debug.Log("[HeroLidProbe] "+sex+" ScleraWorld="+aperture+" CapWorld="+capBounds);
            }
            if(Application.isBatchMode)EditorApplication.Exit(0);
        }
    }
}
