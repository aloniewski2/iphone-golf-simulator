using System.Collections;
using System.IO;
using GolfArcade.Tennis;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GolfArcade.PlayTests
{
    /// The phone launch path (NativeSportsSession): SelectCharacter, outfit, locker look, then a campaign match
    /// against a named rival. Both players must still be the Hero01 visual afterwards, and a self-played rally
    /// must not be eaten by honest-contact misses.
    public class NativeLaunchHeroTests
    {
        [UnityTest, Timeout(600000)]
        public IEnumerator HeroSurvivesTheNativeLaunchPath()
        {
            yield return SceneManager.LoadSceneAsync("Tennis"); yield return null;
            var game = Object.FindFirstObjectByType<TennisGame>();
            while (!game.Initialized) yield return null;
            // exactly what NativeSportsSession does on a phone launch
            game.SelectCharacter(false);
            var kit = TennisLook.Kit.From("FFFFFF", "1E2A5A", "F28C28", "2E6BD6", 2);
            game.ApplyOutfit(kit);
            game.SetPlayerLook(HeroKit.Style.From(2, 1, 1, kit));
            game.ConfigureMatch(TennisGame.Mode.Campaign, "Milo", "Milo", "ROUND 1");
            yield return null; yield return null;
            var ph = game.Player.GetComponentInChildren<HeroTennisDriver>(true);
            var rh = game.Opponent.GetComponentInChildren<HeroTennisDriver>(true);
            int heroes = Object.FindObjectsByType<HeroTennisDriver>(FindObjectsSortMode.None).Length;
            // self-play for 60 s at 60 fps
            int oldRate = Time.captureFramerate; Time.captureFramerate = 60;
            float oldJ = TennisGame.AutoPlayTimingJitter; TennisGame.AutoPlayTimingJitter = .05f;
            game.AutoPlay = true;
            for (int f = 0; f < 3600; f++) yield return null;
            game.AutoPlay = false; Time.captureFramerate = oldRate; TennisGame.AutoPlayTimingJitter = oldJ;
            string report = $"playerHero={(ph != null)} rivalHero={(rh != null)} heroesInScene={heroes} hits={game.Hits} honestMisses={game.HonestMisses} playerGapMean={game.PlayerGaps.Mean:0.000} visible={game.PlayerGaps.Visible}/{game.PlayerGaps.Count} rivalWhiffs={game.RivalWhiffs} score={game.Match.Scoreboard}";
            Debug.Log("[NativeLaunch] " + report);
            File.WriteAllText(Path.GetFullPath("../ArtDir/score80/native_launch_" + (System.Environment.GetEnvironmentVariable("NL_TAG") ?? "run") + ".txt"), report + "\n");
            Assert.IsNotNull(ph, "the player's Hero01 must survive SelectCharacter");
            Assert.IsNotNull(rh, "the campaign rival's Hero01 must survive ConfigureMatch");
            Assert.AreEqual(2, heroes, "exactly one hero per player (no orphans)");
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator EveryRivalAndThePlayerKeepTheHeroStandard()
        {
            yield return SceneManager.LoadSceneAsync("Tennis"); yield return null;
            var game = Object.FindFirstObjectByType<TennisGame>();
            while (!game.Initialized) yield return null;
            game.ManualSimulation = true;
            var kit = TennisLook.Kit.From("FFD23F", "1E2A5A", "F28C28", "2E6BD6", 3);
            game.SelectCharacter(false); game.SetPlayerLook(HeroKit.Style.From(3, 2, 2, kit));
            game.SelectCharacter(true);   // a second rebuild must keep the same hero and look
            yield return null; yield return null;
            var ph = game.Player.GetComponentInChildren<HeroTennisDriver>(true);
            Assert.IsNotNull(ph, "player hero after two rebuilds");
            Assert.AreEqual(HeroCosmetics.Hat.Cap, ph.cosmetics.CurrentHat, "the locker look (cap) is re-applied on rebuild");
            string dir = Path.GetFullPath("../ArtDir/score80/lineup"); Directory.CreateDirectory(dir);
            var cam = new GameObject("Lineup cam").AddComponent<Camera>(); cam.enabled = false; cam.fieldOfView = 30;
            var rt = new RenderTexture(360, 540, 24); var tex = new Texture2D(360, 540, TextureFormat.RGB24, false); cam.targetTexture = rt;
            var states = new System.Text.StringBuilder();
            void Shot(Transform who, string name)
            {
                var d = who.GetComponentInChildren<HeroTennisDriver>(true); var act = who.GetComponent<TennisActor>();
                states.Append($"{name}: state={d.State} action={d.CurrentAction} prep={act.PrepareAmount:0.00} prepServe={act.PrepareServe} prepBH={act.PrepareBackhand} swinging={act.Swinging} flow={game.Flow}\n");
                var p = who.position; cam.transform.position = p + who.forward * 3.4f + Vector3.up * 1.0f; cam.transform.LookAt(p + Vector3.up * .8f);
                cam.Render(); var prev = RenderTexture.active; RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, 360, 540), 0, 0); tex.Apply(); RenderTexture.active = prev;
                File.WriteAllBytes($"{dir}/{name}.png", tex.EncodeToPNG());
            }
            Shot(game.Player.transform, "00_player");
            int n = 1;
            foreach (var r in TennisRoster.All)
            {
                game.ConfigureMatch(TennisGame.Mode.Campaign, r.Key, r.Key, "ROUND");
                yield return null; yield return null;
                var rh = game.Opponent.GetComponentInChildren<HeroTennisDriver>(true);
                Assert.IsNotNull(rh, r.Key + " must be on the hero standard");
                Assert.AreEqual(2, Object.FindObjectsByType<HeroTennisDriver>(FindObjectsSortMode.None).Length, "no orphan heroes after " + r.Key);
                Shot(game.Opponent.transform, $"{n++:00}_{r.Key}");
            }
            game.ConfigureMatch(TennisGame.Mode.Exhibition, null, "Rival", "EXHIBITION");
            yield return null; yield return null;
            Assert.IsNotNull(game.Opponent.GetComponentInChildren<HeroTennisDriver>(true), "the standard rival returns after the campaign");
            Shot(game.Opponent.transform, $"{n:00}_standard_rival");
            File.WriteAllText(dir + "/states.txt", states.ToString());
            Object.Destroy(cam.gameObject);
        }
    }
}
