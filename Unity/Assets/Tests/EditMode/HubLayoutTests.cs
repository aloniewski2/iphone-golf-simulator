using System.Linq;
using GolfArcade.Hub;
using NUnit.Framework;
using UnityEngine;

namespace GolfArcade.Tests
{
    /// The Plaza's map rules (PLAN_MenuHub_WalkableWorld §2, §8 phase 1).
    public class HubLayoutTests
    {
        [Test] public void EveryPlazaDoorIsWithinEightSecondsOfTheSpawnAtRunSpeed()
        {
            foreach (var (door, metres) in HubLayout.SpawnDistances())
                Assert.LessOrEqual(metres / HubPlayer.RunSpeed, 8f, door.id + " is " + metres.ToString("0.0") + " m from the spawn");
            Assert.AreEqual(3, HubLayout.SpawnDistances().Count(), "LOCKER, CLUBHOUSE and PLAY open off the plaza");
        }

        [Test] public void EveryDoorLeadsToAPartnerThatLeadsBack()
        {
            foreach (var d in HubLayout.Doors)
            {
                if (d.locked) { Assert.IsFalse(HubLayout.Destination(d, out _)); continue; }
                Assert.IsTrue(HubLayout.Destination(d, out var partner), d.id + " has a partner");
                Assert.AreNotEqual(d.place, partner.place, d.id + " changes place");
                Assert.IsTrue(HubLayout.Destination(partner, out var back) && back == d, partner.id + " leads back to " + d.id);
                Assert.AreEqual(partner.place, HubLayout.PlaceAt(partner.Arrival), "arriving through " + partner.id + " stands you in " + partner.place);
            }
        }

        [Test] public void EveryRoomIsReachableFromThePlaza()
        {
            var seen = new System.Collections.Generic.HashSet<string> { HubLayout.Plaza };
            bool grew = true;
            while (grew)
            {
                grew = false;
                foreach (var d in HubLayout.Doors)
                    if (seen.Contains(d.place) && HubLayout.Destination(d, out var p) && seen.Add(p.place)) grew = true;
            }
            CollectionAssert.AreEquivalent(HubLayout.Places.Select(p => p.id), seen);
        }

        [Test] public void StationsAndBaysSitInsideTheirRoomsAndQuickTravelReachesThem()
        {
            foreach (var s in HubLayout.Spots)
            {
                Assert.AreEqual(s.place, HubLayout.PlaceAt(s.position), s.id + " is inside " + s.place);
                Assert.IsTrue(HubLayout.QuickTravel(s.id, out var pos, out _, out var place));
                Assert.AreEqual(s.place, place);
                Assert.LessOrEqual(Vector3.Distance(pos, s.position), s.radius, "quick travel to " + s.id + " lands in its prompt radius");
            }
            foreach (var p in HubLayout.Places) Assert.IsTrue(HubLayout.QuickTravel(p.id, out _, out _, out var place) && place == p.id, p.id);
            Assert.AreEqual(4, HubLayout.Spots.Count(s => s.place == HubLayout.TennisRoom && s.kind == HubLayout.Kind.Bay), "tennis: Exhibition, Campaign, Training, Online");
            Assert.AreEqual(3, HubLayout.Spots.Count(s => s.place == HubLayout.GolfRoom && s.kind == HubLayout.Kind.Bay), "golf: Round, Online, Pass the phone");
        }

        [Test] public void InteriorsNeverShareACameraWithAnotherPlace()
        {
            var rooms = HubLayout.Places.Where(p => !p.outdoor).ToArray();
            foreach (var a in rooms)
            {
                Assert.Greater(Vector3.Distance(a.origin, Vector3.zero), 600f, a.id + " is far from the plaza (beyond its 600 m far clip)");
                foreach (var b in rooms) if (a != b) Assert.Greater(Vector3.Distance(a.origin, b.origin), 120f, a.id + " / " + b.id + " are beyond the 60 m room far clip");
            }
        }

        [Test] public void StickBandsAreOrdered()
        {
            Assert.Less(HubPlayer.Dead, HubPlayer.RunAt); Assert.Less(HubPlayer.RunAt, HubPlayer.SprintAt);
            Assert.Less(HubPlayer.WalkMax, HubHeroAnimator.WalkTop, "a steady walk never sits in the walk/run blend");
            Assert.Greater(HubPlayer.RunSpeed, HubHeroAnimator.RunFrom, "a steady run never sits in the walk/run blend");
            // stopping from a sprint at the brake rate takes under 0.25 s
            Assert.LessOrEqual(HubPlayer.SprintSpeed / HubPlayer.Brake, .25f);
        }
    }
}
