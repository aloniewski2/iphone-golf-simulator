using System.Collections;
using System.Linq;
using GolfArcade.Tennis;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GolfArcade.PlayTests
{
    /// HERO_MAINSTAY: every stroke the game can play maps to its own clip of the hero's own sex (Male_* on the male, Female_* on the female), with the contact
    /// times of work/match-anim-set and the replacement work/serve-and-feet Serve (male = female), and the old Hero_* clips are not on the prefab.
    public class MatchHeroClipMapTests
    {
        static readonly (HeroTennisDriver.Clip slot, string clip, float contact)[] Table =
        {
            (HeroTennisDriver.Clip.Forehand, "Forehand", .667f), (HeroTennisDriver.Clip.Backhand, "Backhand", .633f), (HeroTennisDriver.Clip.Serve, "Serve", 1.700f),
            (HeroTennisDriver.Clip.Volley, "VolleyForehand", .300f), (HeroTennisDriver.Clip.VolleyBackhand, "VolleyBackhand", .300f), (HeroTennisDriver.Clip.Smash, "Overhead", .733f),
            (HeroTennisDriver.Clip.ForehandWide, "ForehandWide", .867f), (HeroTennisDriver.Clip.BackhandWide, "BackhandWide", .767f), (HeroTennisDriver.Clip.Return, "Return", .433f),
            (HeroTennisDriver.Clip.ForehandShort, "ForehandShort", .467f), (HeroTennisDriver.Clip.ForehandOpen, "ForehandOpen", .533f), (HeroTennisDriver.Clip.SliceApproach, "SliceApproach", .500f),
        };

        [UnityTest, Timeout(300000)]
        public IEnumerator EveryStrokePlaysItsOwnMatchClipOnBothSexes()
        {
            yield return SceneManager.LoadSceneAsync("Tennis"); yield return null;
            var game = Object.FindFirstObjectByType<TennisGame>();
            while (!game.Initialized) yield return null;
            foreach (bool female in new[] { false, true })
            {
                game.SelectCharacter(female); yield return null; yield return null;
                var d = game.Player.GetComponentInChildren<HeroTennisDriver>(true);
                Assert.IsNotNull(d); Assert.IsNotNull(d.matchLook, "a match hero"); Assert.AreEqual(female, d.matchLook.female);
                string sex = female ? "Female" : "Male";
                // the clips on the prefab: this sex's own FBX clips, at the brief's contact times
                foreach (var (slot, clip, contact) in Table)
                {
                    var s = d.slots.First(x => x.id == slot);
                    Assert.IsNotNull(s.clip, $"{sex} {slot} has a clip");
                    Assert.AreEqual($"{sex}_{clip}"+((slot==HeroTennisDriver.Clip.Forehand||slot==HeroTennisDriver.Clip.Backhand)?"ArmCorrected":""), s.clip.name, $"{slot} plays the {sex} {clip} clip");
                    Assert.AreEqual(contact, s.contact, .001f, $"{sex}_{clip} contact time");
                }
                Assert.IsFalse(d.slots.Any(x => x.clip && x.clip.name.StartsWith("Hero_")), "no old Hero_* clip on the prefab");
                Assert.AreEqual($"{sex}_ReadyIdle", d.slots.First(x => x.id == HeroTennisDriver.Clip.Ready).clip.name, "ready / idle is ReadyIdle");
                // the stroke -> clip map (outside a returned serve; the game is at the serve hold)
                void Expect(TennisActor.Stroke k, bool bh, bool overhead, HeroTennisDriver.Clip want) => Assert.AreEqual(want, d.ClipFor(k, bh, overhead), $"{sex}: {k} backhand={bh} overhead={overhead}");
                foreach (var k in new[] { TennisActor.Stroke.Drive, TennisActor.Stroke.Topspin, TennisActor.Stroke.Slice, TennisActor.Stroke.Lob, TennisActor.Stroke.LowPickup, TennisActor.Stroke.Dive })
                {
                    Expect(k, false, false, HeroTennisDriver.Clip.Forehand);
                    Expect(k, true, false, HeroTennisDriver.Clip.Backhand);
                    Expect(k, false, true, HeroTennisDriver.Clip.Smash);
                }
                Expect(TennisActor.Stroke.Running, false, false, HeroTennisDriver.Clip.ForehandWide);
                Expect(TennisActor.Stroke.Running, true, false, HeroTennisDriver.Clip.BackhandWide);
                Expect(TennisActor.Stroke.Volley, false, false, HeroTennisDriver.Clip.Volley);
                Expect(TennisActor.Stroke.Volley, true, false, HeroTennisDriver.Clip.VolleyBackhand);
                Expect(TennisActor.Stroke.Smash, false, false, HeroTennisDriver.Clip.Smash);
                Expect(TennisActor.Stroke.Serve, false, false, HeroTennisDriver.Clip.Serve);
            }
        }
    }
}
