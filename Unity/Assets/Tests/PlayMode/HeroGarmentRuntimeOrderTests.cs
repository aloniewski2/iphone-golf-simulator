using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using GolfArcade.Tennis;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GolfArcade.PlayTests
{
    // Observe the actual end-of-frame rig, without sampling clips or applying a
    // corrective in the test. This catches a corrective evaluated before the driver.
    [DefaultExecutionOrder(2000)]
    public sealed class HeroGarmentFinalPoseObserver : MonoBehaviour
    {
        static readonly FieldInfo AppliedDirections = typeof(HeroGarmentPoseCorrectives)
            .GetField("directions", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly string[,] Segments = {
            {"Spine","Chest"}, {"Chest","Neck"},
            {"LeftShoulder","LeftUpperArm"}, {"LeftUpperArm","LeftLowerArm"}, {"LeftLowerArm","LeftHand"},
            {"RightShoulder","RightUpperArm"}, {"RightUpperArm","RightLowerArm"}, {"RightLowerArm","RightHand"}
        };
        HeroTennisDriver driver;
        HeroGarmentPoseCorrectives corrective;
        Transform[,] joints;
        readonly Vector3[] previous = new Vector3[8];
        public int Frames, MovingFrames, StaleFrames;
        public float MaximumError;
        public bool SawWave, SawServe, SawForehand;

        public void Observe(HeroTennisDriver live)
        {
            driver = live; corrective = live.matchLook.GetComponent<HeroGarmentPoseCorrectives>();
            Assert.IsNotNull(corrective); Assert.IsNotNull(AppliedDirections);
            var byName = live.matchLook.GetComponentsInChildren<Transform>(true)
                .GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
            joints = new Transform[8, 2];
            for (int i = 0; i < 8; i++) for (int j = 0; j < 2; j++) joints[i,j] = byName[Segments[i,j]];
        }

        void LateUpdate()
        {
            if (!driver || !driver.isActiveAndEnabled || joints == null) return;
            var applied = (Vector3[])AppliedDirections.GetValue(corrective);
            float error = 0, movement = 0;
            for (int i = 0; i < 8; i++)
            {
                var final = (joints[i,1].position - joints[i,0].position).normalized;
                error = Mathf.Max(error, Vector3.Distance(final, applied[i]));
                if (Frames > 0) movement = Mathf.Max(movement, Vector3.Distance(final, previous[i]));
                previous[i] = final;
            }
            Frames++; if (movement > .003f) MovingFrames++;
            if (error > .0001f) StaleFrames++;
            MaximumError = Mathf.Max(MaximumError, error);
            SawWave |= driver.CurrentAction == HeroTennisDriver.Clip.IntroWave;
            SawServe |= driver.CurrentAction == HeroTennisDriver.Clip.Serve;
            SawForehand |= driver.CurrentAction == HeroTennisDriver.Clip.Forehand;
        }
    }

    public sealed class HeroGarmentRuntimeOrderTests
    {
        [UnityTest, Timeout(180000)]
        public IEnumerator LiveDriverGarmentsUseThisFramesFinalPose()
        {
            int oldRate = Time.captureFramerate;
            bool oldDetail = HeroGarmentLOD.ForceFullDetail;
            Time.captureFramerate = 60; HeroGarmentLOD.ForceFullDetail = true;
            try
            {
                yield return SceneManager.LoadSceneAsync("Tennis", LoadSceneMode.Single);
                var game = Object.FindFirstObjectByType<TennisGame>();
                Assert.IsNotNull(game);
                for (int n = 0; n < 300 && !game.Initialized; n++) yield return null;
                Assert.IsTrue(game.Initialized);
                game.ManualSimulation = true; game.enabled = false;
                foreach (var presentation in Object.FindObjectsByType<TennisPresentation>(FindObjectsSortMode.None))
                { presentation.Skip(); presentation.enabled = false; }

                foreach (bool female in new[] { false, true })
                {
                    game.SelectCharacter(female); yield return null; yield return null;
                    var actor = game.Player;
                    var driver = actor.GetComponentInChildren<HeroTennisDriver>(true);
                    Assert.IsNotNull(driver); Assert.IsTrue(driver.enabled); Assert.IsNotNull(driver.matchLook);
                    // Freeze match decisions, while the production driver/actor paths
                    // still evaluate normally. No Sample or corrective.Apply calls.
                    driver.game = null; actor.CancelSwing(); actor.Prepare(0, false);
                    for (int n = 0; n < 20; n++) { actor.Advance(1f / 60, 0); actor.Pose(); yield return null; }
                    var observer = driver.gameObject.AddComponent<HeroGarmentFinalPoseObserver>();
                    observer.Observe(driver);
                    for (int frame = 0; frame < 240; frame++)
                    {
                        if (frame == 10) Assert.IsTrue(driver.PlayEmote(HeroTennisDriver.Clip.IntroWave));
                        if (frame == 80) actor.Serve(.8f);
                        if (frame == 150) actor.Swing(.8f, false, TennisActor.Stroke.Drive);
                        actor.Advance(1f / 60, 0); actor.Pose(); yield return null;
                    }
                    // The coroutine resumes after the preceding frame's observer.
                    Assert.That(observer.Frames, Is.GreaterThanOrEqualTo(235));
                    Assert.That(observer.MovingFrames, Is.GreaterThan(30), "Exercise changed live poses, not a stationary ready snapshot.");
                    Assert.IsTrue(observer.SawWave && observer.SawServe && observer.SawForehand, "All requested live actions must actually play.");
                    Debug.Log($"[GarmentRuntimeOrder] {(female ? "Female" : "Male")} frames={observer.Frames} moving={observer.MovingFrames} stale={observer.StaleFrames} maxDirectionError={observer.MaximumError:R}");
                    Assert.AreEqual(0, observer.StaleFrames, "Garment inputs must match the final rendered pose in the same frame.");
                    Object.Destroy(observer); driver.game = game;
                }
            }
            finally { Time.captureFramerate = oldRate; HeroGarmentLOD.ForceFullDetail = oldDetail; }
        }
    }
}
