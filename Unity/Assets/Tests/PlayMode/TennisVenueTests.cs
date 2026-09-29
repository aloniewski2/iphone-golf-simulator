using System.Collections;
using GolfArcade.Tennis;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GolfArcade.PlayTests
{
    /// The sky and crater courts have an open edge: a ball that comes down past it is called and
    /// falls away; on the deck (and at the resort, everywhere) it still bounces.
    public class TennisVenueTests
    {
        [TearDown] public void Reset() { TennisVenue.Selected = TennisVenueKind.Resort; TennisVenue.CourtColorOverride = null; }

        static IEnumerator Load(TennisVenueKind venue, System.Action<TennisGame> ready)
        {
            TennisVenue.Selected = venue;
            yield return SceneManager.LoadSceneAsync("Tennis", LoadSceneMode.Single); yield return null;
            var game = Object.FindFirstObjectByType<TennisGame>(); game.ManualSimulation = true;
            ready(game);
        }

        /// Drops a ball at (x, z) from a metre up and steps until it has fallen for a while.
        /// Returns whether it ever moved upward again (bounced) and its final height.
        static (bool bounced, float finalY, string call) Drop(TennisGame game, float x, float z)
        {
            game.InjectBall(new Vector3(x, 1f, z), new Vector3(0, -3f, 0));
            bool bounced = false;
            for (int i = 0; i < 120; i++)
            {
                game.Step(1f / 60);
                if (game.BallVelocity.y > .5f) bounced = true;
            }
            return (bounced, game.BallPosition.y, game.Feedback);
        }

        [UnityTest, Timeout(300000)] public IEnumerator SkyscraperBallPastTheEdgeFallsAndIsCalledOut()
        {
            TennisGame game = null;
            yield return Load(TennisVenueKind.Skyscraper, g => game = g);
            var wide = Drop(game, 14.5f, -5f);
            Assert.IsFalse(wide.bounced, "a ball past the deck edge must not bounce");
            Assert.Less(wide.finalY, -5f, "it should keep falling");
            StringAssert.Contains("OUT", wide.call, "and be called");
            var onDeck = Drop(game, 2f, -3f);
            Assert.IsTrue(onDeck.bounced, "a ball on the deck still bounces");
        }

        [UnityTest, Timeout(300000)] public IEnumerator VolcanoBallPastTheEdgeFalls()
        {
            TennisGame game = null;
            yield return Load(TennisVenueKind.Volcano, g => game = g);
            var long_ = Drop(game, 1f, -23f);
            Assert.IsFalse(long_.bounced, "a ball past the far edge must not bounce");
            Assert.Less(long_.finalY, -5f);
            Assert.IsTrue(Drop(game, -2f, -3f).bounced);
        }

        [UnityTest, Timeout(300000)] public IEnumerator ResortStillBouncesEverywhere()
        {
            TennisGame game = null;
            yield return Load(TennisVenueKind.Resort, g => game = g);
            Assert.IsTrue(Drop(game, 14.5f, -5f).bounced);
        }

        [Test] public void OverDeckIsBoundedOnlyOffTheResort()
        {
            TennisVenue.Selected = TennisVenueKind.Skyscraper;
            Assert.IsTrue(TennisVenue.OverDeck(new Vector3(12, 0, 21)));
            Assert.IsFalse(TennisVenue.OverDeck(new Vector3(13, 0, 0)));
            Assert.IsFalse(TennisVenue.OverDeck(new Vector3(0, 0, -22)));
            TennisVenue.Selected = TennisVenueKind.Resort;
            Assert.IsTrue(TennisVenue.OverDeck(new Vector3(100, 0, 100)));
        }

        [Test] public void CourtColourIsPerVenueAndOverridable()
        {
            TennisVenue.Selected = TennisVenueKind.Skyscraper;
            var teal = TennisVenue.CourtColor;
            TennisVenue.Selected = TennisVenueKind.Volcano;
            Assert.AreNotEqual(teal, TennisVenue.CourtColor);
            TennisVenue.CourtColorOverride = Color.red;
            Assert.AreEqual(Color.red, TennisVenue.CourtColor);
        }
    }
}
