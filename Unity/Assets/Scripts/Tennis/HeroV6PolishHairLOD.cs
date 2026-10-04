using UnityEngine;
namespace GolfArcade.Tennis {
 /// V6-only opaque mesh LOD selection. No dither/cross-fade; shares the renderer's existing Head binding.
 [DisallowMultipleComponent] public sealed class HeroV6PolishHairLOD:MonoBehaviour {
  public Mesh lod0,lod1,lod2; public int forceLOD=-1; SkinnedMeshRenderer skin;
  void LateUpdate(){if(!skin)skin=GetComponent<SkinnedMeshRenderer>();if(!skin||!skin.enabled)return;var camera=Camera.main;int level=forceLOD;
   if(level<0){float distance=camera?Vector3.Distance(camera.transform.position,skin.bounds.center):0;level=distance>12?2:distance>6?1:0;}
   Mesh mesh=level==2?lod2:level==1?lod1:lod0;if(mesh&&skin.sharedMesh!=mesh)skin.sharedMesh=mesh;
  }
 }
}
