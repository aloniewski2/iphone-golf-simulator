using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GolfArcade.EditorTools
{
    public sealed class TropicalArenaImporter : AssetPostprocessor
    {
        const string Folder = "Assets/Resources/Tennis/TropicalV3/";
        const string Kits = "Assets/Resources/Tennis/KitsV3/";
        bool IsKit => assetPath.StartsWith(Kits);
        [Serializable] class Entry { public string name, texture; public float[] color; public float roughness; }
        [Serializable] class Manifest { public Entry[] materials; }
        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Folder) && !IsKit) return;
            var m=(ModelImporter)assetImporter;
            m.importCameras=false; m.importLights=false; m.importAnimation=false;
            m.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
            m.materialLocation=ModelImporterMaterialLocation.InPrefab;
            m.useFileScale=true; m.globalScale=1; m.addCollider=false;
            if(IsKit) {m.animationType=ModelImporterAnimationType.Generic; m.avatarSetup=ModelImporterAvatarSetup.NoAvatar;}
        }
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder) && !IsKit) return;
            var t=(TextureImporter)assetImporter; t.maxTextureSize=2048;
            t.mipmapEnabled=true; t.textureCompression=TextureImporterCompression.Compressed;
        }
        Material OnAssignMaterialModel(Material source, Renderer renderer)
        {
            if (!assetPath.StartsWith(Folder) && !IsKit) return null;
            return AssetDatabase.LoadAssetAtPath<Material>((IsKit?Kits:Folder)+"Materials/"+source.name+".mat");
        }
        public static void Prepare()
        {
            PrepareFolder(Folder); if(File.Exists(Kits+"materials.json")) {PrepareFolder(Kits);PrepareRacket();}
        }
        static void PrepareRacket()
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(Kits+"racket.fbx");
            if(!source)return;
            var instance=UnityEngine.Object.Instantiate(source);
            var filter=instance.GetComponentInChildren<MeshFilter>();
            var mesh=UnityEngine.Object.Instantiate(filter.sharedMesh);
            var vertices=mesh.vertices;
            for(int i=0;i<vertices.Length;i++) vertices[i]=filter.transform.TransformPoint(vertices[i]);
            var bounds=new Bounds(vertices[0],Vector3.zero);foreach(var v in vertices)bounds.Encapsulate(v);
            int length=0,thin=0;
            for(int i=1;i<3;i++){if(bounds.size[i]>bounds.size[length])length=i;if(bounds.size[i]<bounds.size[thin])thin=i;}
            int width=3-length-thin;
            float low=0,high=0;
            foreach(var v in vertices){float w=Mathf.Abs(v[width]-bounds.center[width]);if(v[length]<bounds.min[length]+bounds.size[length]*.2f)low=Mathf.Max(low,w);if(v[length]>bounds.max[length]-bounds.size[length]*.2f)high=Mathf.Max(high,w);}
            bool reverse=low>high;
            var map=Matrix4x4.zero;map[0,width]=1;map[1,length]=reverse?-1:1;map[2,thin]=1;map[3,3]=1;
            for(int i=0;i<vertices.Length;i++) {
                var v=vertices[i];vertices[i]=new Vector3(v[width]-bounds.center[width],reverse?bounds.max[length]-v[length]:v[length]-bounds.min[length],v[thin]-bounds.center[thin])*(.686f/bounds.size[length]);
            }
            mesh.vertices=vertices;
            if(map.determinant<0)for(int s=0;s<mesh.subMeshCount;s++){var triangles=mesh.GetTriangles(s);for(int i=0;i<triangles.Length;i+=3){int t=triangles[i];triangles[i]=triangles[i+1];triangles[i+1]=t;}mesh.SetTriangles(triangles,s);}
            mesh.RecalculateNormals();mesh.RecalculateBounds();mesh.RecalculateTangents();
            string path=Kits+"RacketGameplayMesh.asset";
            var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(saved){EditorUtility.CopySerialized(mesh,saved);UnityEngine.Object.DestroyImmediate(mesh);}else{saved=mesh;AssetDatabase.CreateAsset(saved,path);}
            var root=new GameObject("Racket gameplay metres");root.AddComponent<MeshFilter>().sharedMesh=saved;
            var mats=filter.GetComponent<Renderer>().sharedMaterials;
            for(int i=0;i<mats.Length;i++)mats[i]=AssetDatabase.LoadAssetAtPath<Material>(Kits+"Materials/"+mats[i].name+".mat")??mats[i];
            root.AddComponent<MeshRenderer>().sharedMaterials=mats;
            PrefabUtility.SaveAsPrefabAsset(root,Kits+"RacketGameplay.prefab");
            UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(instance);AssetDatabase.SaveAssets();
        }
        static void PrepareFolder(string folder)
        {
            var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(folder+"materials.json"));
            Directory.CreateDirectory(folder+"Materials");
            foreach(var e in manifest.materials) {
            string path=folder+"Materials/"+e.name+".mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!mat) { mat=new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard")); AssetDatabase.CreateAsset(mat,path); }
            mat.shader=Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Color color=new Color(e.color[0],e.color[1],e.color[2],e.color[3]);
            Texture texture=string.IsNullOrEmpty(e.texture)?null:AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"Textures/"+e.texture);
            mat.SetColor("_BaseColor",texture?Color.white:color); mat.SetColor("_Color",texture?Color.white:color);
            mat.SetTexture("_BaseMap",texture); mat.SetTexture("_MainTex",texture);
            mat.SetFloat("_Smoothness",1-e.roughness); mat.enableInstancing=true;
            mat.SetFloat("_Glossiness",folder==Kits?1-e.roughness:.5f); mat.SetFloat("_Metallic",0);
            EditorUtility.SetDirty(mat);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
