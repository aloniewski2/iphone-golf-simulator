using System.Linq;
using GolfArcade.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GolfArcade.Tests
{
    public class StandardArmSportTests
    {
        [TestCase("male", "tennis")]
        [TestCase("female", "tennis")]
        [TestCase("male", "bowling")]
        [TestCase("female", "bowling")]
        [TestCase("male", "boxing")]
        [TestCase("female", "boxing")]
        public void ActualSportPoseKeepsHandsAndUsesLongerContinuousArms(string gender, string sport)
        {
            string path = $"Assets/Tests/Fixtures/StandardCharacters/{gender}_{sport}.fbx";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab);
            var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview"));
            var instance = Object.Instantiate(prefab);
            try
            {
                var arms = instance.GetComponent<StandardCharacterArms>();
                Assert.IsNotNull(arms, "Importer must install the shared correction on every standard rig.");
                arms.Initialize();
                Assert.IsTrue(arms.IsReady);
                Assert.IsTrue(arms.FloatingHandsPreview, "All standards default to hands only");
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>().Where(r => r.name.StartsWith("Standard continuous arm") || r.name.StartsWith("Shoulder fabric ") || r.name.StartsWith("Short sleeve ") || r.name.StartsWith("Sleeve piping ")))
                    Assert.IsFalse(renderer.enabled);
                // Hidden arm rig remains valid and recoverable for technical inspection.
                arms.SetFloatingHandsPreview(false);
                var hands = instance.GetComponentsInChildren<Transform>().Where(t => t.name == "Hand.L" || t.name == "Hand.R").ToArray();
                Assert.AreEqual(2, hands.Length);
                for (int i = 0; i <= 60; i++)
                {
                    clip.SampleAnimation(instance, clip.length * i / 60);
                    var positions = hands.Select(t => t.position).ToArray();
                    var rotations = hands.Select(t => t.rotation).ToArray();
                    arms.ApplyAfterAnimation();
                    Assert.Less(arms.MaximumSegmentError, .005f);
                    for (int h = 0; h < hands.Length; h++)
                    {
                        Assert.Less(Vector3.Distance(positions[h], hands[h].position), .0001f);
                        Assert.Less(Quaternion.Angle(rotations[h], hands[h].rotation), .05f);
                    }
                    foreach (var filter in instance.GetComponentsInChildren<MeshFilter>().Where(f => f.name.StartsWith("Standard continuous arm")))
                    {
                        Assert.Less(filter.sharedMesh.bounds.size.magnitude, 1f);
                        Assert.IsTrue(filter.GetComponent<Renderer>().enabled);
                        Assert.IsFalse(filter.sharedMesh.vertices.Any(v => float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z)));
                    }
                }
                Assert.AreEqual(2, instance.GetComponentsInChildren<MeshFilter>().Count(f => f.name.StartsWith("Standard continuous arm")));
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>().Where(r => r.name.StartsWith("Forearm ") || r.name.StartsWith("Elbow ") || r.name.StartsWith("Upper arm ")))
                    Assert.IsFalse(renderer.enabled, "Original disconnected arm geometry must be hidden.");
            }
            finally { Object.DestroyImmediate(instance); }
        }
    }
}
