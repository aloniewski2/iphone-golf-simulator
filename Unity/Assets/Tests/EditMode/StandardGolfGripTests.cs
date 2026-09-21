using System.Linq;
using GolfArcade.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GolfArcade.Tests
{
    public class StandardGolfGripTests
    {
        [TestCase("male")]
        [TestCase("female")]
        public void GolfGripStaysConnectedAt120HzAndPreservesContact(string gender)
        {
            string path = $"Assets/Resources/StandardCharacters/standard_{gender}_golf.fbx";
            var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            try
            {
                var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c => c.name == "StandardGolfDrive");
                var grip = instance.AddComponent<StandardGolfGrip>(); grip.Initialize();
                var arms = instance.GetComponent<StandardCharacterArms>() ?? instance.AddComponent<StandardCharacterArms>(); arms.Initialize();
                var contact = instance.GetComponentsInChildren<Transform>().First(t => t.name == "StandardClubContact");
                Vector3 previous = Vector3.zero;
                Vector3 previousAuthored = Vector3.zero;
                for (int frame = 0; frame <= 360; frame++)
                {
                    float phase = frame / 360f;
                    clip.SampleAnimation(instance, phase * clip.length);
                    Vector3 authoredContact = contact.position;
                    grip.Apply(phase);
                    Assert.IsTrue(grip.IsReady);
                    Assert.Less(grip.MaximumHandleError, .0001f);
                    if (frame == 0 || frame == 288) Assert.Less(Vector3.Distance(contact.position, authoredContact), .0001f, "Address/impact moved off the ball");
                    if (frame > 0) Assert.Less(Vector3.Distance(contact.position, previous), .3f, $"Club jumped at 120 Hz frame {frame}; authored delta {Vector3.Distance(authoredContact, previousAuthored)}");
                    previous = contact.position;
                    previousAuthored = authoredContact;
                    arms.ApplyAfterAnimation();
                    Assert.Less(arms.MaximumSegmentError, .005f, $"Overextended arms at {phase}");
                    Assert.Less(arms.MaximumGripError, .0001f);
                }
            }
            finally { Object.DestroyImmediate(instance); }
        }
    }
}
