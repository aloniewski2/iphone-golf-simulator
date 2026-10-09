using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GolfArcade.Course
{
    /// Render-only countryside beyond Meadow's authoritative flat playing lane.
    public sealed class GolfMeadowWorld : MonoBehaviour
    {
        readonly List<Mesh> meshes = new();
        readonly List<Material> materials = new();
        public static float Height(Hole hole, float x, float z)
        {
            float distance = (float)hole.DistanceFromCenterline(new CoursePoint(x,z));
            float edge = (float)(hole.FairwayWidth*.5 + hole.RoughWidth);
            float fade = Mathf.SmoothStep(0,1,Mathf.InverseLerp(edge+45,edge+180,distance));
            float seed = hole.Number*17.9f;
            float broad = Mathf.PerlinNoise(x*.0028f+seed,z*.0028f+7)*.64f + Mathf.PerlinNoise(x*.007f+31,z*.007f+seed)*.36f;
            return -.035f + fade*(5 + Mathf.Max(0,broad-.17f)*65);
        }
        public static void Apply(GameObject root, Hole hole)
        {
            if(hole.Number<1||hole.Number>3||root.GetComponent<GolfMeadowWorld>())return;
            var owner=root.AddComponent<GolfMeadowWorld>();owner.Landscape(hole);owner.Clubhouse(hole);
        }
        Material Palette(string name,float gloss)
        {
            var m=new Material(Resources.Load<Shader>("Course/Shaders/GolfBotanical")){name=name};
            m.SetColor("_BaseColor",Color.white);m.SetFloat("_VertexPalette",1);m.SetFloat("_Leaf",0);m.SetFloat("_Gloss",gloss);m.SetFloat("_Cull",(float)CullMode.Back);materials.Add(m);return m;
        }
        MeshRenderer Spawn(string name,Mesh mesh,Material material,bool shadows)
        {
            var go=new GameObject(name);go.transform.SetParent(transform,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=shadows?ShadowCastingMode.On:ShadowCastingMode.Off;r.receiveShadows=shadows;meshes.Add(mesh);return r;
        }
        void Landscape(Hole hole)
        {
            float minX=0,maxX=0,maxZ=0;
            foreach(var p in hole.Centerline){minX=Mathf.Min(minX,(float)p.X);maxX=Mathf.Max(maxX,(float)p.X);maxZ=Mathf.Max(maxZ,(float)p.D);}
            minX-=700;maxX+=700;float minZ=-600;maxZ+=850;
            const float spacing=14;int columns=Mathf.CeilToInt((maxX-minX)/spacing),rows=Mathf.CeilToInt((maxZ-minZ)/spacing);
            var positions=new List<Vector3>();var colors=new List<Color>();var distance=new List<float>();var indices=new List<int>();
            float edge=(float)(hole.FairwayWidth*.5+hole.RoughWidth);
            for(int row=0;row<=rows;row++)for(int column=0;column<=columns;column++){
                float x=minX+column*spacing,z=minZ+row*spacing;
                float away=(float)hole.DistanceFromCenterline(new CoursePoint(x,z));distance.Add(away);float y=Height(hole,x,z);positions.Add(new Vector3(x,y,z));
                float patch=Mathf.PerlinNoise(x*.015f+7,z*.015f+hole.Number);
                var color=Color.Lerp(new Color(.18f,.32f,.17f),new Color(.38f,.48f,.24f),patch);
                float field=Mathf.PerlinNoise(x*.006f+93,z*.006f+31);
                color=Color.Lerp(color,new Color(.48f,.45f,.26f),Mathf.SmoothStep(.58f,.79f,field)*.52f);
                color=Color.Lerp(color,new Color(.27f,.40f,.43f),Mathf.InverseLerp(280,1300,away)*.56f);
                color=Color.Lerp(new Color(.17f,.37f,.21f),color,Mathf.InverseLerp(edge+40,edge+150,away));color=color.linear;color.a=0;colors.Add(color);
            }
            void Triangle(int a,int b,int c){if(distance[a]>edge+16&&distance[b]>edge+16&&distance[c]>edge+16)indices.AddRange(new[]{a,b,c});}
            for(int row=0;row<rows;row++)for(int column=0;column<columns;column++){
                int a=row*(columns+1)+column,b=a+1,c=a+columns+1,d=c+1;Triangle(a,c,b);Triangle(b,c,d);
            }
            var mesh=new Mesh{name="Meadow summer hills outside gameplay",indexFormat=IndexFormat.UInt32};mesh.SetVertices(positions);mesh.SetColors(colors);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            Spawn("RESORT_MEADOW_COUNTRYSIDE",mesh,Palette("Meadow distant rolling summer land",.04f),false);
            Debug.Log($"[GolfMeadowWorld] hole{hole.Number}: {indices.Count/3} landscape triangles; all triangle corners beyond playable rough +16yd, no colliders");
        }
        static Color ClubColor(string name)
        {
            if(name.Contains("ROOF"))return new Color(.43f,.18f,.12f);
            if(name.Contains("GLASS"))return new Color(.09f,.24f,.29f);
            if(name.Contains("TRIM"))return new Color(.83f,.80f,.68f);
            if(name.Contains("WOOD"))return new Color(.34f,.22f,.13f);
            if(name.Contains("STONE"))return new Color(.46f,.48f,.43f);
            return new Color(.73f,.71f,.60f);
        }
        void Clubhouse(Hole hole)
        {
            var prefab=Resources.Load<GameObject>("Course/Resort/MeadowClub");if(!prefab){Debug.LogError("Missing Meadow club source");return;}
            var pin=hole.Pin;var before=hole.Centerline[hole.Centerline.Length-2];var along=new Vector3((float)(pin.X-before.X),0,(float)(pin.D-before.D)).normalized;
            var right=Vector3.Cross(Vector3.up,along);var point=new Vector3((float)pin.X,0,(float)pin.D)+along*80-right*60;point.y=Height(hole,point.x,point.z)-.03f;
            var place=Matrix4x4.TRS(point,Quaternion.LookRotation(-along,Vector3.up),Vector3.one*1.1f);
            var positions=new List<Vector3>();var colors=new List<Color>();var indices=new List<int>();
            foreach(var mf in prefab.GetComponentsInChildren<MeshFilter>()){
                if(!mf.sharedMesh||!mf.sharedMesh.isReadable)continue;var source=mf.sharedMesh;var verts=source.vertices;var renderer=mf.GetComponent<Renderer>();var mats=renderer.sharedMaterials;
                var world=transform.worldToLocalMatrix*place*prefab.transform.worldToLocalMatrix*mf.transform.localToWorldMatrix;
                for(int sub=0;sub<source.subMeshCount;sub++){
                    var color=ClubColor(sub<mats.Length?mats[sub].name:"").linear;color.a=0;var map=new Dictionary<int,int>();
                    foreach(int index in source.GetTriangles(sub)){
                        if(!map.TryGetValue(index,out int next)){next=positions.Count;map[index]=next;positions.Add(world.MultiplyPoint3x4(verts[index]));colors.Add(color);}indices.Add(next);
                    }
                }
            }
            var mesh=new Mesh{name="Meadow country club vertex palette",indexFormat=IndexFormat.UInt32};mesh.SetVertices(positions);mesh.SetColors(colors);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            Spawn("RESORT_MEADOW_COUNTRY_CLUB",mesh,Palette("Meadow country club finish",.18f),true);
        }
        void OnDestroy(){foreach(var mesh in meshes)if(mesh)Destroy(mesh);foreach(var material in materials)if(material)Destroy(material);}
    }
}
