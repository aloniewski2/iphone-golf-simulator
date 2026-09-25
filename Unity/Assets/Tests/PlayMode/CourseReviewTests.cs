using System.Collections;
using GolfArcade.Course;
using GolfArcade.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GolfArcade.PlayTests
{
    /// Every hole of the round from the same three places — high behind the tee, off to the
    /// side at the height of the island, and down by the green — with the HUD hidden, so the
    /// course and its objects can be looked over. Frames go to Library/Captures/course.
    public class CourseReviewTests
    {
        const string Dir = "Library/Captures/course";

        static IEnumerator WaitFor(System.Func<bool> done, float seconds, string what)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (!done())
            {
                if (Time.realtimeSinceStartup > until) Assert.Fail($"timed out waiting for {what}");
                yield return null;
            }
        }

        [UnityTest, Timeout(480000)]   // thirteen holes, about 15 s each
        public IEnumerator EveryHoleFromThreeSides()
        {
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync("Golf", LoadSceneMode.Single);
            var cam = Camera.main;
            var game = Object.FindFirstObjectByType<GolfGame>();
            game.InstantReplays = false;
            yield return null;
            game.ChooseHoles(0);
            game.Play();
            yield return null;
            var hudCanvas = Object.FindFirstObjectByType<GolfArcade.UI.Hud>().GetComponent<Canvas>();
            foreach (int number in new[] { 7, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23 })
            {
                game.JumpToHole(number);
                yield return WaitFor(() => game.Current == GolfGame.State.Aim, 45, $"hole {number}'s tee");
                yield return new WaitForSecondsRealtime(0.5f);
                var hole = game.CurrentHole;
                foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                    if (r.name.StartsWith("WATER") || r.name == "Open sea")
                        Debug.Log($"COURSE {number}: {r.name} enabled {r.enabled && r.gameObject.activeInHierarchy} bounds {r.bounds.center:F1} size {r.bounds.size:F0} mat {r.sharedMaterial?.shader?.name}");
                var tee = HoleView.ToWorld(hole.Tee); var pin = HoleView.ToWorld(hole.Pin);
                var along = pin - tee; along.y = 0; float length = along.magnitude; along /= length;
                var right = Vector3.Cross(Vector3.up, along);
                var mid = (tee + pin) / 2;
                var views = new (string name, Vector3 at, Vector3 look)[]
                {
                    ("a-behind", tee - along * 60 + Vector3.up * (length * 0.45f + 40), mid),
                    ("b-side", mid + right * (length * 0.9f + 80) + Vector3.up * 35, mid),
                    ("c-green", pin - along * 30 + right * 12 + Vector3.up * 6, pin + Vector3.up * 1.5f),
                    ("e-tee", tee - along * 7 + right * 1.5f + Vector3.up * 2.2f, tee + along * 25),
                };
                hudCanvas.enabled = false;
                cam.aspect = 16f / 9f;
                foreach (var v in views)
                {
                    game.enabled = false;   // (the rig holds still while the view is set)
                    cam.transform.position = v.at; cam.transform.LookAt(v.look);
                    Assert.IsNotNull(GameCapture.Save($"{Dir}/{number:00}-{v.name}.jpg", 1600, 900));
                    game.enabled = true;
                }
                // what the physics found standing on it, drawn over the side view
                var obstacles = hole.Obstacles;
                int trees = 0, bushes = 0, rocks = 0, walls = 0;
                var shapes = new GameObject("Obstacle shapes").transform;
                var red = new Material(Shader.Find("Unlit/Color")) { color = new Color(1f, 0.15f, 0.2f) };
                var blue = new Material(Shader.Find("Unlit/Color")) { color = new Color(0.2f, 0.4f, 1f) };
                foreach (var o in obstacles)
                {
                    switch (o.Kind) { case ObstacleKind.Tree: trees++; break; case ObstacleKind.Bush: bushes++; break; case ObstacleKind.Rock: rocks++; break; default: walls++; break; }
                    var body = GameObject.CreatePrimitive(o.Kind == ObstacleKind.Wall ? PrimitiveType.Cylinder : PrimitiveType.Sphere);
                    Object.Destroy(body.GetComponent<Collider>());
                    body.transform.SetParent(shapes, false);
                    double lo = o.Kind == ObstacleKind.Tree ? o.CrownBase : o.Base;
                    body.transform.position = new Vector3((float)o.X, (float)(lo + o.Top) / 2, (float)o.D);
                    body.transform.localScale = o.Kind == ObstacleKind.Wall
                        ? new Vector3((float)o.Radius * 2, (float)(o.Top - o.Base) / 2, (float)o.Radius * 2)
                        : new Vector3((float)o.Radius * 2, (float)(o.Top - lo), (float)o.Radius * 2);
                    body.GetComponent<Renderer>().sharedMaterial = o.Kind == ObstacleKind.Rock || o.Kind == ObstacleKind.Wall ? blue : red;
                }
                Debug.Log($"COURSE {number}: {obstacles.Length} obstacles — {trees} trees, {bushes} bushes, {rocks} rocks, {walls} walls");
                Assert.Greater(trees, 5, $"hole {number}'s trees were found");
                game.enabled = false;
                cam.transform.position = views[1].at; cam.transform.LookAt(views[1].look);
                Assert.IsNotNull(GameCapture.Save($"{Dir}/{number:00}-d-obstacles.jpg", 1600, 900));
                game.enabled = true;
                Object.Destroy(shapes.gameObject);
                cam.ResetAspect();
                hudCanvas.enabled = true;
            }
        }

        /// Pushing the joystick up while aiming looks up the hole: on the Spiral's tee the summit
        /// green is found, and down it comes in over the ball. Frames of each in Captures/course.
        [UnityTest]
        public IEnumerator LookingUpFindsThePin()
        {
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync("Golf", LoadSceneMode.Single);
            var cam = Camera.main;
            cam.aspect = GameCapture.PhoneWidth / (float)GameCapture.PhoneHeight;
            var game = Object.FindFirstObjectByType<GolfGame>();
            game.InstantReplays = false;
            yield return null;
            game.ChooseHoles(0);
            game.Play();
            yield return null;
            try
            {
                foreach (int number in new[] { 13, 7 })
                {
                    game.JumpToHole(number);
                    yield return WaitFor(() => game.Current == GolfGame.State.Aim, 45, "the tee");
                    var pin = HoleView.ToWorld(game.CurrentHole.Pin) + Vector3.up * 1.5f;
                    foreach (var (name, look) in new[] { ("level", 0f), ("up", 1f), ("down", -1f) })
                    {
                        game.LookHeld = look;
                        yield return new WaitForSecondsRealtime(1.6f);
                        Assert.IsNotNull(GameCapture.Save($"{Dir}/look-{number}-{name}.jpg", 1080, 2340));
                        if (look > 0)
                        {
                            var v = cam.WorldToViewportPoint(pin);
                            Assert.IsTrue(v.z > 0 && v.x > 0.1f && v.x < 0.9f && v.y > 0.15f && v.y < 0.85f, $"hole {number}: looking up, the pin is well in the picture ({v})");
                        }
                    }
                    game.LookHeld = 0;
                }
            }
            finally { game.LookHeld = 0; }
        }

        /// A shot into a real tree on the Witch's Lair: it comes down near the tree, well short
        /// of where the same shot goes with the tree out of the way.
        [UnityTest]
        public IEnumerator ATreeStopsAShot()
        {
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync("Golf", LoadSceneMode.Single);
            var game = Object.FindFirstObjectByType<GolfGame>();
            game.InstantReplays = false;
            yield return null;
            game.ChooseHoles(0);
            game.Play();
            yield return null;
            game.JumpToHole(14);
            yield return WaitFor(() => game.Current == GolfGame.State.Aim, 45, "the tee");
            var hole = game.CurrentHole;
            // a tall tree 40–90 yards out, nearest the line to the pin
            int pick = -1; double best = double.MaxValue;
            for (int i = 0; i < hole.Obstacles.Length; i++)
            {
                var o = hole.Obstacles[i];
                if (o.Kind != ObstacleKind.Tree || o.Top - o.Base < 5) continue;
                double away = hole.Tee.DistanceTo(new CoursePoint(o.X, o.D));
                if (away < 40 || away > 90) continue;
                if (away < best) { best = away; pick = i; }
            }
            Assert.GreaterOrEqual(pick, 0, "a tree to aim at");
            var tree = hole.Obstacles[pick];
            var at = new CoursePoint(tree.X, tree.D);
            // watched from beside the line, level with the tree
            var tee = HoleView.ToWorld(hole.Tee); var treeW = new Vector3((float)tree.X, (float)tree.Base, (float)tree.D);
            var line = treeW - tee; line.y = 0; line.Normalize();
            var side = Vector3.Cross(Vector3.up, line);
            game.StrikeToward(at);
            float struck = Time.realtimeSinceStartup;
            var cam = Camera.main;
            var hudCanvas = Object.FindFirstObjectByType<GolfArcade.UI.Hud>().GetComponent<Canvas>();
            for (int frame = 0; frame < 14 && game.Current != GolfGame.State.Result; frame++)
            {
                yield return new WaitForSecondsRealtime(0.18f);
                game.enabled = false; hudCanvas.enabled = false;
                cam.transform.position = treeW - line * 6 + side * 16 + Vector3.up * ((float)(tree.Top - tree.Base) + 6);
                cam.transform.LookAt(treeW + Vector3.up * (float)((tree.Top - tree.Base) * 0.5));
                GameCapture.Save($"{Dir}/tree-hit-{frame:00}.jpg", 1280, 720);
                game.enabled = true; hudCanvas.enabled = true;
            }
            yield return WaitFor(() => game.Current == GolfGame.State.Result, 25, "the shot into the tree to finish");
            var shot = game.LastShot;
            Debug.Log($"COURSE tree shot: tree at {at} (top {tree.Top - tree.Base:F1} yd over its foot), carry {shot.Carry:F1}, rest {shot.Rest}, {shot.Lie}");
            Assert.Less(shot.Rest.DistanceTo(at), 25, "it comes down by the tree");
            Assert.Less(hole.Tee.DistanceTo(shot.Rest), best + 20, "not through it");
            Assert.GreaterOrEqual(shot.Knocks.Count, 1, "it hit the tree");
        }
    }
}
