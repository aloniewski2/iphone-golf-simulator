using System;
using System.Linq;
using UnityEngine;
namespace GolfArcade.Tennis {
 /// Garment-only fit corrections, blended by pose on the unchanged rig.
 /// Pairwise segment directions are invariant to FBX axis conversion and scale.
 public sealed class GolfGarmentCorrectives : MonoBehaviour {
  [Serializable] sealed class Anchor { public string shape; public float[] feature; }
  [Serializable] sealed class Profile { public Anchor[] anchors; }
  static readonly string[,] Segments={{"Spine","Chest"},{"Chest","Neck"},{"LeftShoulder","LeftUpperArm"},{"LeftUpperArm","LeftLowerArm"},{"LeftLowerArm","LeftHand"},{"RightShoulder","RightUpperArm"},{"RightUpperArm","RightLowerArm"},{"RightLowerArm","RightHand"}};
  public MatchHeroLook look;
  Profile profile; SkinnedMeshRenderer[] targets; Transform[,] joints; int[][] shapes;
  readonly Vector3[] directions=new Vector3[8];readonly float[] features=new float[28];float[] weights;
  bool Init() {
   if(profile!=null)return true;if(!look)look=GetComponent<MatchHeroLook>();if(!look||!look.golfKit)return false;
   var text=Resources.Load<TextAsset>("Golf/Correctives/"+(look.female?"Female":"Male"));if(!text)return false;
   profile=JsonUtility.FromJson<Profile>(text.text);targets=look.kit?.Where(r=>r&&r.sharedMesh&&r.sharedMesh.blendShapeCount>0).ToArray();if(targets==null||targets.Length==0||profile?.anchors==null){profile=null;return false;}
   joints=new Transform[8,2];var rig=transform.Find("Rig_"+(look.female?"Female":"Male"));var all=rig.GetComponentsInChildren<Transform>(true);
   for(int i=0;i<8;i++)for(int j=0;j<2;j++)joints[i,j]=all.First(t=>t.name==Segments[i,j]);
   shapes=new int[targets.Length][];weights=new float[profile.anchors.Length];
   for(int t=0;t<targets.Length;t++){
    var mesh=targets[t].sharedMesh;shapes[t]=new int[weights.Length];
    for(int a=0;a<weights.Length;a++){shapes[t][a]=-1;for(int k=0;k<mesh.blendShapeCount;k++)if(mesh.GetBlendShapeName(k).EndsWith(profile.anchors[a].shape,StringComparison.Ordinal))shapes[t][a]=k;}
   }
   return true;
  }
  public void Apply() {
   if(!Init())return;
   for(int i=0;i<8;i++)directions[i]=(joints[i,1].position-joints[i,0].position).normalized;
   int f=0;for(int i=0;i<8;i++)for(int j=i+1;j<8;j++)features[f++]=Vector3.Dot(directions[i],directions[j]);
   double sum=0;for(int a=0;a<weights.Length;a++){double d=0;for(int k=0;k<features.Length;k++){double e=features[k]-profile.anchors[a].feature[k];d+=e*e;}d/=features.Length;weights[a]=(float)(1/Math.Pow(Math.Max(d,1e-10),2));sum+=weights[a];}
   for(int t=0;t<targets.Length;t++)for(int a=1;a<weights.Length;a++)if(shapes[t][a]>=0)targets[t].SetBlendShapeWeight(shapes[t][a],(float)(100*weights[a]/sum));
  }
  void LateUpdate()=>Apply();
 }
}
