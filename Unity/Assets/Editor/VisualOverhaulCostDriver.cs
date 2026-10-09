// DRAFT FOR P5. A temporary Play-mode PlayerLoop callback, no saved/runtime script.
using System;
using UnityEngine.LowLevel;
namespace GolfArcade.EditorTools
{
    public static class VisualOverhaulCostDriver
    {
        static PlayerLoopSystem original;static bool installed;
        static PlayerLoopSystem Copy(PlayerLoopSystem source)
        {
            var result=source;
            if(source.subSystemList!=null){result.subSystemList=new PlayerLoopSystem[source.subSystemList.Length];for(int i=0;i<source.subSystemList.Length;i++)result.subSystemList[i]=Copy(source.subSystemList[i]);}
            return result;
        }
        static bool Insert(ref PlayerLoopSystem loop)
        {
            if(loop.type==typeof(UnityEngine.PlayerLoop.Update))
            {
                var old=loop.subSystemList??Array.Empty<PlayerLoopSystem>();var next=new PlayerLoopSystem[old.Length+1];next[0]=new PlayerLoopSystem{type=typeof(VisualOverhaulCostDriver),updateDelegate=VisualOverhaulFrameCosts.PlayerUpdate};Array.Copy(old,0,next,1,old.Length);loop.subSystemList=next;return true;
            }
            if(loop.subSystemList!=null)for(int i=0;i<loop.subSystemList.Length;i++){var child=loop.subSystemList[i];if(Insert(ref child)){loop.subSystemList[i]=child;return true;}}
            return false;
        }
        public static void Install()
        {
            if(installed)throw new InvalidOperationException("Frame-cost PlayerLoop already installed");original=PlayerLoop.GetCurrentPlayerLoop();var changed=Copy(original);if(!Insert(ref changed))throw new InvalidOperationException("Actual Play-mode Update loop missing");PlayerLoop.SetPlayerLoop(changed);installed=true;
        }
        public static void Restore(){if(installed){PlayerLoop.SetPlayerLoop(original);installed=false;}}
    }
}
