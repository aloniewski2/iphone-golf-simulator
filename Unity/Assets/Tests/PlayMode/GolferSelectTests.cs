using System.Collections;
using GolfArcade.Game;
using GolfArcade.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GolfArcade.PlayTests
{
    /// The golfer select screen: both golfers square on with their faces in the middle of the
    /// head, the arrows switching between them, the kit and shirt swatches recolouring the golfer
    /// and being remembered, LET'S GO back to the menu. Frames go to Library/Captures/review/select-*.png.
    public class GolferSelectTests
    {
        const string Dir = "Library/Captures/review";

        static void Press(string name)
        {
            var go = GameObject.Find(name);
            Assert.IsTrue(go, $"no {name} on the screen");
            go.GetComponent<HoldButton>().Pressed();
        }

        [UnityTest]
        public IEnumerator ChoosingAGolfer()
        {
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync("Golf", LoadSceneMode.Single);
            var cam = Camera.main;
            cam.aspect = GameCapture.PhoneWidth / (float)GameCapture.PhoneHeight;
            var game = Object.FindFirstObjectByType<GolfGame>();
            game.InstantReplays = false;
            yield return new WaitForSecondsRealtime(0.5f);
            var body0 = GolferStyle.Body; int kit0 = GolferStyle.Kit, shirt0 = GolferStyle.Shirt;
            try
            {
                GolferStyle.Body = GolferStyle.BodyKind.Male; GolferStyle.Kit = 0; GolferStyle.Shirt = 0;
                game.OpenGolferPicker();
                yield return new WaitForSecondsRealtime(1.5f);
                Assert.IsTrue(GameObject.Find("Golfer select"), "the select screen is up");
                Assert.AreEqual(GolfGame.State.Golfer, game.Current);
                Assert.IsNotNull(GameCapture.Save($"{Dir}/select-a-male.png"));
                Feet($"{Dir}/select-a-feet.png");
                // the shoes as they were modelled, standing and at address (the clips turned the
                // foot bones half a turn from how the skin was bound, folding the shoes flat)
                AssertShoesAsBound("standing");
                {
                    var view = GameObject.Find("Golfer").GetComponent<GolferView>();
                    view.SetClub(GolfArcade.Shot.GolfClub.Driver, false);
                    yield return null;
                    Feet($"{Dir}/select-a-feet-address.png");
                    view.Perform("Idle");
                    yield return null;
                }

                // the face straight on: the decal in the middle of the picture, left to right
                var decal = FaceDecal();
                var v = cam.WorldToViewportPoint(decal.bounds.center);
                Assert.AreEqual(0.5f, v.x, 0.03f, "the face is in the middle of the screen");

                Press("Next golfer");
                yield return new WaitForSecondsRealtime(1.2f);
                Assert.AreEqual(GolferStyle.BodyKind.Female, GolferStyle.Body, "the arrow switches golfer");
                Assert.IsNotNull(GameCapture.Save($"{Dir}/select-b-female.png"));

                Press("KIT Teal");
                Press("SHIRT Sky");
                yield return new WaitForSecondsRealtime(0.4f);
                Assert.AreEqual(1, GolferStyle.Kit); Assert.AreEqual(3, GolferStyle.Shirt);
                Assert.IsNotNull(GameCapture.Save($"{Dir}/select-c-female-teal.png"));

                Press("Previous golfer");
                Press("KIT Crimson");
                Press("SHIRT White");
                yield return new WaitForSecondsRealtime(1.2f);
                Assert.AreEqual(GolferStyle.BodyKind.Male, GolferStyle.Body);
                Assert.IsNotNull(GameCapture.Save($"{Dir}/select-d-male-crimson.png"));

                Press("Lets go");
                yield return null;
                Assert.AreEqual(GolfGame.State.Menu, game.Current, "LET'S GO goes back to the menu");
                Assert.IsFalse(GameObject.Find("Golfer select"), "the screen is put away");
            }
            finally
            {
                GolferStyle.Body = body0; GolferStyle.Kit = kit0; GolferStyle.Shirt = shirt0;
                if (game.Current == GolfGame.State.Golfer) game.CloseGolferPicker();
            }
        }

        /// Each foot and toe bone against the skin's bind pose, in its parent's frame: the shoes
        /// keep their shape when the feet turn with the leg the way they were bound.
        static void AssertShoesAsBound(string when)
        {
            SkinnedMeshRenderer skin = null;
            foreach (var smr in GameObject.Find("Golfer").GetComponentsInChildren<SkinnedMeshRenderer>())
                if (!skin || smr.sharedMesh.vertexCount > skin.sharedMesh.vertexCount) skin = smr;
            var bones = skin.bones; var binds = skin.sharedMesh.bindposes;
            foreach (var name in new[] { "Foot.L", "Toes.L", "Foot.R", "Toes.R" })
            {
                int i = System.Array.FindIndex(bones, b => b && b.name == name);
                int p = System.Array.IndexOf(bones, bones[i].parent);
                var bound = (binds[p] * binds[i].inverse).rotation;
                Assert.Less(Quaternion.Angle(bound, bones[i].localRotation), 15f, $"{name} {when}: turned from how the shoe was bound");
            }
        }

        /// A close-up of the golfer's shoes from the front and from the side, beside each other.
        static void Feet(string path)
        {
            Transform l = null, r = null;
            foreach (var t in GameObject.Find("Golfer").GetComponentsInChildren<Transform>())
            {
                if (t.name == "Foot.L") l = t;
                if (t.name == "Foot.R") r = t;
            }
            Assert.IsTrue(l && r, "no foot bones");
            var mid = (l.position + r.position) / 2;
            var golfer = GameObject.Find("Golfer").transform;
            var go = new GameObject("Feet camera");
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 30; cam.nearClipPlane = 0.05f; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.gray;
            var rt = new RenderTexture(1200, 600, 24);
            var shot = new Texture2D(1200, 600, TextureFormat.RGB24, false);
            try
            {
                cam.targetTexture = rt;
                var faces = Camera.main.transform.position - mid; faces.y = 0; faces.Normalize();
                var views = new[] { faces, Quaternion.Euler(0, 70, 0) * faces };
                for (int i = 0; i < views.Length; i++)
                {
                    cam.rect = new Rect(i * 0.5f, 0, 0.5f, 1);
                    go.transform.position = mid + views[i] * 1.6f + Vector3.up * 0.5f;
                    go.transform.LookAt(mid + Vector3.up * 0.1f);
                    cam.Render();
                }
                RenderTexture.active = rt;
                shot.ReadPixels(new Rect(0, 0, 1200, 600), 0, 0);
                RenderTexture.active = null;
                System.IO.File.WriteAllBytes(path, shot.EncodeToPNG());
            }
            finally { Object.Destroy(go); Object.Destroy(rt); Object.Destroy(shot); }
        }

        static Renderer FaceDecal()
        {
            foreach (var r in GameObject.Find("Golfer").GetComponentsInChildren<Renderer>())
                foreach (var m in r.sharedMaterials)
                    if (m && m.mainTexture && m.mainTexture.name == "FaceAtlas") return r;
            Assert.Fail("no face decal");
            return null;
        }
    }
}
