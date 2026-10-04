using GolfArcade.Profile;
using NUnit.Framework;

namespace GolfArcade.Tests
{
    /// The golfer's look record: made from an older save's body, kit and shirt, repaired when damaged, and a
    /// profile's copy of it that its owner changes without touching anyone else's.
    public class CharacterLookTests
    {
        [Test]
        public void AnOlderSaveKeepsItsGolferKitAndShirt()
        {
            var look = CharacterLook.Unset();
            Assert.AreEqual(0, look.Version, "not made yet");
            look.MigrateFrom(1, 3, 4);
            Assert.AreEqual(CharacterLook.CurrentVersion, look.Version);
            Assert.AreEqual(CharacterLook.Girl, look.Body);
            Assert.AreEqual(CharacterLook.KitHex[3], look.Shorts, "the kit colour it picked (crimson)");
            Assert.AreEqual(CharacterLook.ShirtHex[4], look.Shirt, "the shirt it picked (blush)");
            var asDesigned = CharacterLook.Unset(); asDesigned.MigrateFrom(0, 0, 0);
            Assert.AreEqual("", asDesigned.Shorts, "0 is the kit as it comes");
            Assert.AreEqual("", asDesigned.Shirt);
        }

        [Test]
        public void ADamagedLookIsPutRight()
        {
            var look = new CharacterLook { Body = 7, Haircut = 99, Headwear = -4, Skin = "not a colour", Hair = "#a1b2c3", Shirt = null, Shoes = "12345" };
            look.Repair();
            Assert.AreEqual(1, look.Body); Assert.AreEqual(CharacterLook.Haircuts - 1, look.Haircut); Assert.AreEqual(0, look.Headwear);
            Assert.AreEqual("", look.Skin, "a colour that isn't one is as designed");
            Assert.AreEqual("A1B2C3", look.Hair, "a real one is tidied");
            Assert.AreEqual("", look.Shirt); Assert.AreEqual("", look.Shoes);
        }

        [Test]
        public void TheHaircutFollowsTheBodyUntilOneIsChosen()
        {
            var boy = new CharacterLook { Body = CharacterLook.Boy };
            var girl = new CharacterLook { Body = CharacterLook.Girl };
            Assert.AreEqual(0, boy.HaircutId, "swept for a boy");
            Assert.AreEqual(1, girl.HaircutId, "a ponytail for a girl");
            girl.Haircut = 4;
            Assert.AreEqual(4, girl.HaircutId, "curly, once chosen");
        }

        [Test]
        public void ACopyIsItsOwn()
        {
            var a = new CharacterLook { Shirt = "112233" };
            var b = a.Clone();
            Assert.IsTrue(a.SameAs(b));
            b.Shirt = "445566"; b.Headwear = 3;
            Assert.AreEqual("112233", a.Shirt, "the original is unchanged");
            Assert.IsFalse(a.SameAs(b));
        }

        [Test]
        public void EachNewProfileHasALookOfItsOwn()
        {
            var book = new ProfileBook();
            var one = book.Add("One"); var two = book.Add("Two");
            Assert.AreEqual(CharacterLook.CurrentVersion, one.Look.Version);
            Assert.AreNotEqual(one.Look.Body, two.Look.Body, "two golfers on one phone differ");
            two.Look.Headwear = 2;
            Assert.AreEqual(CharacterLook.Visor, one.Look.Headwear, "changing one's look leaves the other's");
        }
    }
}
