using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using GolfArcade.Game;
using GolfArcade.Tennis;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GolfArcade.PlayTests
{
    public sealed class ReceivingRegressionTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static readonly FieldInfo Active = typeof(NativeSportsSession).GetField("<Active>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        static readonly FieldInfo Touch = typeof(NativeSportsSession).GetField("<Touch>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        static readonly MethodInfo NativeUpdate = typeof(NativeSportsSession).GetMethod("Update", Private);
        static readonly MethodInfo HeroUpdate = typeof(HeroTennisDriver).GetMethod("LateUpdate", Private);
        readonly List<GameObject> adapters = new();
        bool activeBefore, touchBefore;
        TennisGame game;

        [UnitySetUp] public IEnumerator Load()
        {
            activeBefore = (bool)Active.GetValue(null); touchBefore = (bool)Touch.GetValue(null);
            Touch.SetValue(null, false);
            yield return SceneManager.LoadSceneAsync("Tennis", LoadSceneMode.Single);
            yield return null;
            game = Object.FindFirstObjectByType<TennisGame>();
            game.enabled = false; game.NativeControlled = true; game.ManualSimulation = true;
            game.ManualRallyMovement = true;
            game.ConfigureMatch(TennisGame.Mode.Exhibition, null, null, null);
            game.GetComponent<TennisPresentation>().Finish();
            game.SetControllerSetup(true); game.SetControllerSetup(false);
        }
        [TearDown] public void Restore()
        {
            foreach (var root in adapters) if (root) Object.DestroyImmediate(root);
            adapters.Clear(); Active.SetValue(null, activeBefore); Touch.SetValue(null, touchBefore);
        }
        void Receive()
        {
            typeof(TennisGame).GetField("match", Private).SetValue(game, TennisMatch.New(false));
            typeof(TennisGame).GetMethod("BeginPoint", Private).Invoke(game, null);
            Assert.That(game.Flow, Is.EqualTo(TennisGame.Phase.OpponentServe));
        }
        NativeSportsSession Adapter(float target)
        {
            var root = new GameObject("Receiving controller test"); adapters.Add(root);
            var adapter = root.AddComponent<NativeSportsSession>(); adapter.enabled = false;
            typeof(NativeSportsSession).GetField("tennis", Private).SetValue(adapter, game);
            typeof(NativeSportsSession).GetField("paused", Private).SetValue(adapter, false);
            typeof(NativeSportsSession).GetField("resumedAt", Private).SetValue(adapter, Time.realtimeSinceStartupAsDouble);
            typeof(NativeSportsSession).GetField("target", Private).SetValue(adapter, target);
            Active.SetValue(null, true);
            return adapter;
        }
        void Frame(NativeSportsSession adapter = null)
        {
            if (adapter) NativeUpdate.Invoke(adapter, null);
            for (int i = 0; i < 4; i++) game.Step(TennisBall.Step);
            HeroUpdate.Invoke(game.Player.GetComponentInChildren<HeroTennisDriver>(), null);
        }
        void CaptureReady(bool female)
        {
            var root = new GameObject("Receiving pose proof camera");
            var camera = root.AddComponent<Camera>(); camera.CopyFrom(game.GameplayCamera); camera.enabled = false;
            camera.rect = new Rect(0, 0, 1, 1); camera.aspect = 1; camera.cullingMask = ~(1 << 5);
            camera.transform.position = game.Player.transform.position + new Vector3(2.5f, 1.9f, 4);
            camera.transform.LookAt(game.Player.transform.position + Vector3.up * .9f); camera.fieldOfView = 35;
            string folder = System.Environment.GetEnvironmentVariable("GAMEPLAY_PROOF_DIR") ?? "Library/Captures/receiving-fix";
            Directory.CreateDirectory(folder);
            var rt = RenderTexture.GetTemporary(960, 960, 24);
            var previous = RenderTexture.active;
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            var image = new Texture2D(960, 960, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 960, 960), 0, 0); image.Apply();
            File.WriteAllBytes(folder + (female ? "/ready-female.png" : "/ready-male.png"), image.EncodeToPNG());
            camera.targetTexture = null; RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt); Object.DestroyImmediate(image); Object.DestroyImmediate(root);
        }

        [UnityTest] public IEnumerator ReceivingButtonsWinOverOpposingPhoneMotion()
        {
            Receive(); var adapter = Adapter(-1);
            float before = game.Player.transform.position.x;
            game.ServeNudge = 1;
            for (int i = 0; i < 15; i++) Frame(adapter);
            Assert.That(game.PlayerUsesTrackedMovement, Is.False, "The receiving D-pad owns the court before the opponent hits");
            Assert.That(game.Player.transform.position.x, Is.GreaterThan(before + .3f), "Holding right must move right even if the phone reports a leftward position");
            Receive(); before = game.Player.transform.position.x;
            typeof(NativeSportsSession).GetField("target", Private).SetValue(adapter, 1f);
            game.ServeNudge = -1;
            for (int i = 0; i < 15; i++) Frame(adapter);
            Assert.That(game.Player.transform.position.x, Is.LessThan(before - .3f));
            yield return null;
        }

        [UnityTest] public IEnumerator ServingStateCannotLeakIntoTheReceivingReadyPose()
        {
            foreach (bool female in new[] { false, true })
            {
                game.SelectCharacter(female); yield return null;
                game.Player.Prepare(.9f, false, true); game.Player.Tick(.5f, 0);
                var hero = game.Player.GetComponentInChildren<HeroTennisDriver>(); HeroUpdate.Invoke(hero, null);
                Receive();
                for (int i = 0; i < 20; i++) { Frame(); yield return null; }
                var look = hero.matchLook;
                float leftBend = 180 - Vector3.Angle(look.Bone(HumanBodyBones.LeftUpperLeg).position - look.Bone(HumanBodyBones.LeftLowerLeg).position,
                    look.Bone(HumanBodyBones.LeftFoot).position - look.Bone(HumanBodyBones.LeftLowerLeg).position);
                float rightBend = 180 - Vector3.Angle(look.Bone(HumanBodyBones.RightUpperLeg).position - look.Bone(HumanBodyBones.RightLowerLeg).position,
                    look.Bone(HumanBodyBones.RightFoot).position - look.Bone(HumanBodyBones.RightLowerLeg).position);
                float hands = Vector3.Distance(look.Bone(HumanBodyBones.LeftHand).position, look.Bone(HumanBodyBones.RightHand).position);
                Debug.Log($"[ReceivingRegression] female={female} action={hero.CurrentAction} serve={game.Player.PrepareServe} knees={leftBend:F1}/{rightBend:F1} hands={hands:F3}");
                CaptureReady(female);
                Assert.That(game.Player.PrepareServe, Is.False, "A receiver must never retain the last serve's trophy preparation");
                Assert.That(hero.CurrentAction, Is.EqualTo(HeroTennisDriver.Clip.Ready));
                Assert.That(Mathf.Min(leftBend, rightBend), Is.GreaterThan(10), "ReadyIdle keeps both knees bent");
                Assert.That(hands, Is.LessThan(.35f), "Both hands stay together on the ready racket");
            }
        }

        [UnityTest] public IEnumerator StartingTheReturnKeepsThePositionChosenWithTheDPad()
        {
            Receive(); var adapter = Adapter(0);
            NativeUpdate.Invoke(adapter, null);
            float before = game.Player.transform.position.x;
            game.InjectBall(new Vector3(before + .6f, 1.3f, 6), new Vector3(0, 2.2f, -16.5f));
            NativeUpdate.Invoke(adapter, null);
            Assert.That(game.MoveInput, Is.EqualTo(0).Within(.001f), "A stationary phone must preserve the receiver's chosen position when control changes to a rally");
            Assert.That(game.PlayerUsesTrackedMovement, Is.True);
            yield return null;
        }

        [UnityTest] public IEnumerator AutomaticMovementIgnoresOppositePhoneSteering()
        {
            game.ManualRallyMovement = false;
            game.Player.transform.position = new Vector3(0, .035f, -11.2f);
            game.InjectBall(new Vector3(2.4f, 1.3f, 6), new Vector3(0, 2.2f, -16.5f));
            game.SetLateralInput(-1, true);
            Assert.That(game.PlayerUsesTrackedMovement, Is.False);
            for (int i = 0; i < 24; i++) Frame();
            Assert.That(game.Player.transform.position.x, Is.GreaterThan(.4f), "Computer intercepts rightward ball despite leftward phone input");
            yield return null;
        }

        [UnityTest] public IEnumerator QuietPhoneLetsTheCharacterTrackTheIncomingBall()
        {
            game.Player.transform.position = new Vector3(0, .035f, -11.2f);
            game.InjectBall(new Vector3(2.4f, 1.3f, 6), new Vector3(0, 2.2f, -16.5f));
            var adapter = Adapter(0);
            for (int i = 0; i < 24; i++) Frame(adapter);
            NativeUpdate.Invoke(adapter, null);
            Debug.Log($"[ReceivingRegression] tracking x={game.Player.transform.position.x:F3} offset={game.AssistOffset:F3} input={game.MoveInput:F3} goal={game.MoveGoal}");
            Assert.That(game.Player.transform.position.x, Is.GreaterThan(.4f), "A quiet phone still gets the normal ball interception assist");
            Assert.That(game.AssistOffset, Is.EqualTo(game.Player.transform.position.x).Within(.03f));
            Assert.That(game.MoveInput, Is.EqualTo(0).Within(.03f), "The controller must not fight automatic tracking by steering back to the old centre");
            game.SetLateralInput(-1, true); game.Step(TennisBall.Step);
            Assert.That(game.LateralSpeed, Is.LessThan(0), "A deliberate opposite step takes priority on the first input frame");
            yield return null;
        }
    }
}
