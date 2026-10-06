using System.Collections.Generic;
using UnityEngine;

namespace GolfArcade.Course
{
    /// Compatibility adapter for existing GolfAtmosphere callers. Lighting and
    /// highlight handling now live in the shared hero profile; no colour MPBs.
    /// The legacy V4 swing remains intact. Its flat Lit surfaces receive the
    /// same shared character shader, retaining their authored colours/maps.
    public sealed class GolfFigureExposure : MonoBehaviour
    {
        public static Color Exposure { get; private set; } = Color.white;
        int signature;
        readonly List<Material> owned = new();
        public static bool IsWhite(Color c) => Mathf.Approximately(c.r,1)&&Mathf.Approximately(c.g,1)&&Mathf.Approximately(c.b,1);
        public static void Set(Color legacyExposure)
        {
            Exposure=Color.white;
            var golfer=Object.FindFirstObjectByType<GolfArcade.Game.GolferView>(FindObjectsInactive.Include);
            if(!golfer)return;
            var adapter=golfer.GetComponent<GolfFigureExposure>()??golfer.gameObject.AddComponent<GolfFigureExposure>();
            adapter.Refresh(true);
        }
        void LateUpdate()=>Refresh(false);
        void Refresh(bool force)
        {
            int next=transform.childCount*7919+(transform.childCount>0?transform.GetChild(transform.childCount-1).GetInstanceID():0);
            if(!force&&next==signature)return;signature=next;
            var shader=Resources.Load<Shader>("Tennis/Shaders/TennisCharacter");
            if(!shader)throw new System.InvalidOperationException("Shared hero shader missing");
            foreach(var r in GetComponentsInChildren<Renderer>(true))
            {
                var mats=r.sharedMaterials;bool changed=false;
                for(int i=0;i<mats.Length;i++)
                {
                    r.SetPropertyBlock(null,i);
                    var source=mats[i];if(!source||source.shader.name!="Universal Render Pipeline/Lit")continue;
                    var m=new Material(shader){name=source.name+" (shared golf profile)",enableInstancing=source.enableInstancing};
                    foreach(string p in new[]{"_BaseColor","_RimColor"})if(source.HasProperty(p)&&m.HasProperty(p))m.SetColor(p,source.GetColor(p));
                    foreach(string p in new[]{"_BaseMap","_BumpMap"})if(source.HasProperty(p)&&m.HasProperty(p)&&source.GetTexture(p))m.SetTexture(p,source.GetTexture(p));
                    foreach(string p in new[]{"_Smoothness","_BumpScale"})if(source.HasProperty(p)&&m.HasProperty(p))m.SetFloat(p,source.GetFloat(p));
                    mats[i]=m;owned.Add(m);changed=true;
                }
                if(changed)r.sharedMaterials=mats;
            }
        }
        void OnDestroy(){foreach(var m in owned)if(m)Destroy(m);}
    }
}
