using System;
using UnityEngine;

namespace GolfArcade.Tennis
{
    // Keeps renderer-owned meshes and managed solver buffers bounded to a hero's lifetime.
    [DisallowMultipleComponent]
    public sealed class HeroGarmentGussetLifetime : MonoBehaviour
    {
        [Serializable]
        public sealed class CaptureStats
        {
            public bool enabled;
            public long calls, solves, skippedCalls, managedApplyBytes, managedBuildBytes;
            public int fullCalls, lodCalls, builds, meshActivations, maximumFreeVertices;
            public int retainedRendererCaches, retainedMeshes;
            public double meanApplyMs, maximumApplyMs, totalBuildMs, maximumBuildMs;
        }
        internal Transform owner;
        void OnDestroy() => HeroGarmentGusset.Release(owner ? owner : transform);

        // Editor capture entry points can bracket one complete capture, including both LODs.
        // These are silent unless VISUAL_UNDERARM_GUSSET_STATS=1 at process startup.
        public static void BeginCaptureStats() => HeroGarmentGusset.BeginCaptureStats();
        public static CaptureStats GetCaptureStats() => HeroGarmentGusset.CaptureStats();
        public static void EndCaptureStats() => HeroGarmentGusset.EndCaptureStats();
    }
}
