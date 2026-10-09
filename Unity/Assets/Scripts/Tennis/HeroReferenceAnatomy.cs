using System;
using System.Linq;
using UnityEngine;

namespace GolfArcade.Tennis
{
    /// Pilot import of authored continuous facial anatomy onto the existing sports rig.
    /// The canonical body and feature FBX retain the original rest skeleton.
    public static class HeroReferenceAnatomy
    {
        public static bool Apply(MatchHeroLook hero)
        {
            if (Environment.GetEnvironmentVariable("VISUAL_REFERENCE_HEAD") != "1" || !hero.body) return false;
            var source = Resources.Load<GameObject>("Tennis/HeroReference/" + (hero.female ? "Female" : "Male"));
            if (!source) return false;
            var renderers = source.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var skin = renderers.FirstOrDefault(r => r.name == "Body");
            var features = renderers.FirstOrDefault(r => r.name == "ReferenceFeatures");
            if (!skin || !features) { Debug.LogError("[HeroReferenceAnatomy] missing Body or ReferenceFeatures"); return false; }
            float sourceHeight=skin.sharedMesh.bounds.size.magnitude;
            float originalHeight=hero.body.sharedMesh.bounds.size.magnitude;
            if(sourceHeight<originalHeight*.5f || sourceHeight>originalHeight*2f)
            {
                Debug.LogError("[HeroReferenceAnatomy] FBX mesh units differ from the canonical rig; original retained");
                return false;
            }
            var map = hero.GetComponentsInChildren<Transform>(true).GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
            bool CanMap(SkinnedMeshRenderer r) => r.bones.All(b => b && map.ContainsKey(b.name));
            if (!CanMap(skin) || !CanMap(features)) { Debug.LogError("[HeroReferenceAnatomy] source skeleton does not match sports rig"); return false; }
            var oldFace = hero.face;
            hero.body.sharedMesh = skin.sharedMesh;
            hero.body.sharedMaterials = skin.sharedMaterials;
            hero.body.bones = skin.bones.Select(b => map[b.name]).ToArray();
            if (skin.rootBone && map.TryGetValue(skin.rootBone.name, out var skinRoot)) hero.body.rootBone = skinRoot;
            hero.body.localBounds = skin.localBounds;
            var node = new GameObject("ReferenceFeatures");
            node.transform.SetParent(hero.body.transform, false);
            var relative = skin.transform.worldToLocalMatrix * features.transform.localToWorldMatrix;
            node.transform.localPosition = relative.GetColumn(3);
            node.transform.localRotation = relative.rotation;
            node.transform.localScale = relative.lossyScale;
            var live = node.AddComponent<SkinnedMeshRenderer>();
            live.sharedMesh = features.sharedMesh; live.sharedMaterials = features.sharedMaterials;
            live.bones = features.bones.Select(b => map[b.name]).ToArray();
            if (features.rootBone && map.TryGetValue(features.rootBone.name, out var featureRoot)) live.rootBone = featureRoot;
            live.localBounds = features.localBounds;
            live.updateWhenOffscreen = true;
            if (oldFace) oldFace.enabled = false;
            hero.face = live;
            Debug.Log("[HeroReferenceAnatomy] installed " + (hero.female ? "Female" : "Male") +
                      " body=" + skin.sharedMesh.vertexCount + " features=" + features.sharedMesh.vertexCount +
                      "; original animation bones retained; legacy face geometry disabled");
            return true;
        }
    }
}
