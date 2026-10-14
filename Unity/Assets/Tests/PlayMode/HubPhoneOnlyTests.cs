using System;
using System.Collections;
using System.IO;
using GolfArcade.Game;
using GolfArcade.Hub;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GolfArcade.Tests
{
    /// Friends + controller lock, Unity half (2026-10-11): with no TV the Plaza runs on the phone itself, drawn above the phone's
    /// controls, and the PLAY hall is shut (PHONE_ONLY); with a TV it is open as before; joining a friend's party takes you to stand
    /// beside them, in their room when this phone may enter it (MEET). Stills: $HUB_PROOF_DIR/phone_only_*.png.
    public class HubPhoneOnlyTests
    {
        static string ProofDir => Environment.GetEnvironmentVariable("HUB_PROOF_DIR");
        GameObject host; NativeSportsSession bridge;

        void Send(string action, Action<NativeSportsSession.Message> more = null)
        {
            var m = new NativeSportsSession.Message { version = 1, session = "hub-phone", action = action, touch = true }; more?.Invoke(m); bridge.Receive(JsonUtility.ToJson(m));
        }
        IEnumerator BootHub(bool phoneOnly)
        {
            HubWorld.EditorDriven = true;
            host = new GameObject("NativeSportsSession"); Object.DontDestroyOnLoad(host);
            bridge = host.AddComponent<NativeSportsSession>();
            Send("start", m => { m.sport = "hub"; m.phoneOnly = phoneOnly; m.shirt = "E8505B"; m.shorts = "24345E"; m.accent = "FFFFFF"; });
            for (int i = 0; i < 900 && !bridge.Ready; i++) yield return null;
            var hub = HubWorld.Find(); Assert.IsNotNull(hub, "the plaza booted");
            hub.HurryArrival(); yield return new WaitForSeconds(.3f);
        }
        [TearDown] public void Clean()
        {
            HubWorld.EditorDriven = false; HubWorld.PhoneOnly = false;
            if (host) Object.DestroyImmediate(host);
            Time.timeScale = 1;
        }

        /// Push the stick into a door for up to 3 s: true if it took you through.
        static IEnumerator WalkInto(HubWorld hub, HubLayout.Door door, Action<bool> through)
        {
            var p = hub.Player; int before = hub.DoorLog.Count;
            p.Teleport(door.position - door.inward * 2.2f, Mathf.Atan2(door.inward.x, door.inward.z) * Mathf.Rad2Deg);
            yield return null;
            p.SetStick(door.inward.x, door.inward.z, .8f);
            float t = 0; while (hub.DoorLog.Count == before && t < 3) { yield return null; t += Time.deltaTime; }
            p.SetStick(0, 0, 0);
            while (hub.Transitioning) yield return null;
            through(hub.DoorLog.Count > before);
        }

        [UnityTest, Timeout(300000)] public IEnumerator PhoneOnlyShutsThePlayHallAndDrawsAboveTheControls()
        {
            yield return BootHub(true);
            var hub = HubWorld.Find();
            Assert.IsTrue(HubWorld.PhoneOnly);
            // the plaza draws in the top part of the phone's screen; the controls are under it
            Assert.AreEqual(new Rect(0, 1 - HubWorld.PhoneViewShare, 1, HubWorld.PhoneViewShare), hub.Camera.rect);
            // the PLAY door: its prompt says a TV is needed, and walking into it does nothing (a toast says why)
            var play = HubLayout.DoorById("door-plaza-play"); Assert.IsNotNull(play);
            Assert.IsTrue(HubWorld.DoorClosed(play), "the PLAY door is shut on a phone with no TV");
            bool through = true; yield return WalkInto(hub, play, r => through = r);
            Assert.IsFalse(through, "walking into PLAY does not go through");
            Assert.AreEqual(HubLayout.Plaza, hub.Place);
            Assert.AreEqual(HubWorld.PlayClosedText, hub.ToastShown, "the plaza says why");
            if (!string.IsNullOrEmpty(ProofDir)) { yield return new WaitForEndOfFrame(); Save(ScreenCapture.CaptureScreenshotAsTexture(), "phone_only_play_shut_screen"); }
            // ☰ travel to PLAY, a sport room or a bench is refused too; sitting on a bench is refused
            foreach (var target in new[] { "play", "tennis", "golf", "bay-tennis-online", "bay-golf-round" })
                Assert.IsFalse(hub.GoTo(target), target + " is shut without a TV");
            Assert.IsFalse(hub.SitAt("bay-golf-online"));
            // the rest of the plaza is open: the locker and the clubhouse
            foreach (var room in new[] { "locker", "clubhouse" })
            {
                var door = HubLayout.DoorById("door-plaza-" + room); Assert.IsNotNull(door, room);
                Assert.IsFalse(HubWorld.DoorClosed(door));
                hub.GoTo(HubLayout.Plaza); while (hub.Transitioning) yield return null;
                through = false; yield return WalkInto(hub, door, r => through = r);
                Assert.IsTrue(through, "the " + room + " opens on the phone"); Assert.AreEqual(room, hub.Place);
            }
            hub.GoTo(HubLayout.Plaza); while (hub.Transitioning) yield return null;
            yield return new WaitForSeconds(.3f);
            // proof: the phone's screen (1179x2556): the plaza in its top 56 %, the controls' area under it
            Still(hub.Camera, "phone_only_plaza", 590, 1278);
        }

        [UnityTest, Timeout(300000)] public IEnumerator WithATvThePlayHallIsOpen()
        {
            yield return BootHub(false);
            var hub = HubWorld.Find();
            Assert.IsFalse(HubWorld.PhoneOnly);
            Assert.AreEqual(new Rect(0, 0, 1, 1), hub.Camera.rect, "the whole TV");
            var play = HubLayout.DoorById("door-plaza-play");
            Assert.IsFalse(HubWorld.DoorClosed(play));
            bool through = false; yield return WalkInto(hub, play, r => through = r);
            Assert.IsTrue(through); Assert.AreEqual(HubLayout.PlayHall, hub.Place);
            Assert.IsTrue(hub.GoTo("bay-golf-online")); while (hub.Transitioning) yield return null;
            Assert.AreEqual(HubLayout.GolfRoom, hub.Place);
        }

        /// MEET: one tap on Join and your hero stands beside your friend's, in their room (a quick fade). On a phone with no TV a
        /// friend in a sport room is met in the plaza instead.
        [UnityTest, Timeout(300000)] public IEnumerator JoiningAFriendTakesYouToThem()
        {
            yield return BootHub(false);
            var hub = HubWorld.Find();
            var locker = HubLayout.PlaceOf(HubLayout.Locker);
            var at = locker.origin + new Vector3(1.5f, 0, 1f);
            var friend = new RemoteState { id = "sam", name = "Sam", place = HubLayout.Locker, x = at.x, y = at.y, z = at.z, skinHex = "C47A4C", shirt = "3F62CC", shorts = "24345E", colour = 1 };
            Send("hubRemote", m => m.remote = JsonUtility.ToJson(friend));
            yield return null;
            Send("hubMeet", m => m.mode = "sam");
            for (int i = 0; i < 120 && (hub.Transitioning || hub.Place != HubLayout.Locker); i++) yield return null;
            Assert.AreEqual(HubLayout.Locker, hub.Place, "you are in Sam's room");
            Assert.Less(Vector3.Distance(hub.Player.transform.position, at), 2.5f, "beside Sam");
            yield return new WaitForSeconds(.4f);
            Still(hub.Camera, "meet_friend_locker", 1280, 720);
            Send("hubMeet", m => m.mode = "nobody"); yield return null;
            Assert.IsFalse(hub.Transitioning, "an unknown friend does nothing");
        }

        [UnityTest, Timeout(300000)] public IEnumerator OnAPhoneAFriendInASportRoomIsMetInThePlaza()
        {
            yield return BootHub(true);
            var hub = HubWorld.Find();
            hub.GoTo("locker"); while (hub.Transitioning) yield return null;
            var golf = HubLayout.PlaceOf(HubLayout.GolfRoom);
            var friend = new RemoteState { id = "sam", name = "Sam", place = HubLayout.GolfRoom, x = golf.origin.x, y = golf.origin.y, z = golf.origin.z, skinHex = "C47A4C", shirt = "3F62CC", shorts = "24345E", colour = 1 };
            Send("hubRemote", m => m.remote = JsonUtility.ToJson(friend)); yield return null;
            Send("hubMeet", m => m.mode = "sam");
            for (int i = 0; i < 120 && (hub.Transitioning || hub.Place != HubLayout.Plaza); i++) yield return null;
            Assert.AreEqual(HubLayout.Plaza, hub.Place, "the golf room is shut on this phone: you wait in the plaza");
        }

        static void Still(Camera cam, string name, int w, int h)
        {
            if (string.IsNullOrEmpty(ProofDir) || !cam) return;
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32); rt.antiAliasing = 4;
            var prev = RenderTexture.active; RenderTexture.active = rt; GL.Clear(true, true, Color.black); RenderTexture.active = prev;
            var keep = cam.targetTexture; cam.targetTexture = rt; cam.Render(); cam.targetTexture = keep;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
            RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
            Save(tex, name);
        }
        static void Save(Texture2D tex, string name)
        {
            if (string.IsNullOrEmpty(ProofDir)) { Object.Destroy(tex); return; }
            Directory.CreateDirectory(ProofDir);
            File.WriteAllBytes(Path.Combine(ProofDir, name + ".png"), tex.EncodeToPNG()); Object.Destroy(tex);
        }
    }
}
