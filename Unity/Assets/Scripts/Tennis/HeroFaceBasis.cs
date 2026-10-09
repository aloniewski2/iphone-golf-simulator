using UnityEngine;
namespace GolfArcade.Tennis {
 /// Anatomical face axes stay fixed in Face local coordinates while the head
 /// turns or bows. Root/world up cannot split tilted golf eye geometry.
 public static class HeroFaceBasis {
  public static void Get(MatchHeroLook hero,out Vector3 right,out Vector3 up,out Vector3 forward){
   var head=hero.Bone(HumanBodyBones.Head);var face=hero.face.transform;
   up=head?face.InverseTransformDirection(head.up).normalized:face.InverseTransformDirection(hero.transform.up).normalized;
   forward=Vector3.zero;var mesh=hero.face.GetComponent<MeshFilter>().sharedMesh;var materials=hero.face.sharedMaterials;var normals=mesh.normals;
   for(int s=0;s<Mathf.Min(materials.Length,mesh.subMeshCount);s++)if(materials[s]&&materials[s].name.Contains("Sclera"))foreach(int i in mesh.GetTriangles(s))forward+=normals[i];
   if(forward.sqrMagnitude<.000001f)forward=head?face.InverseTransformDirection(head.forward):face.InverseTransformDirection(hero.transform.forward);
   // Eye paint normals are not anatomical axes: the male sclera's mean normal
   // is tilted ~61 degrees upward. Keep the rig's anatomical up and remove that
   // component from forward, rather than tipping the entire facial frame upward.
   forward-=up*Vector3.Dot(forward,up);
   if(forward.sqrMagnitude<.000001f){forward=head?face.InverseTransformDirection(head.forward):face.InverseTransformDirection(hero.transform.forward);forward-=up*Vector3.Dot(forward,up);}
   forward.Normalize();right=Vector3.Cross(up,forward).normalized;up=Vector3.Cross(forward,right).normalized;
  }
 }
}
