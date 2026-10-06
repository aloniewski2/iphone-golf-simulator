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
        public void TheHaircutIsTheClassicCutUntilOneIsChosen()
        {
            var boy = new CharacterLook { Body = CharacterLook.Boy };
            var girl = new CharacterLook { Body = CharacterLook.Girl };
            Assert.AreEqual(0, boy.HaircutId, "the classic cut for a boy");
            Assert.AreEqual(0, girl.HaircutId, "and for a girl");
            girl.Haircut = 1;
            Assert.AreEqual(1, girl.HaircutId, "bald, once chosen");
        }

        [Test]
        public void ASaveFromTheIconAvatarKitKeepsWhatStillExists()
        {
            // version 1 was the icon avatar kit: 15 haircuts (5 was bald), hats, glasses, beards and clothes that are no longer on the golfer
            var curly = new CharacterLook { Version = 1, Haircut = 4, Headwear = 9, Glasses = 3, Facial = 2, Top = 2, Bottom = 1, Shirt = "112233" };
            curly.Repair();
            Assert.AreEqual(CharacterLook.CurrentVersion, curly.Version);
            Assert.AreEqual(0, curly.Haircut, "a cut the match hero has not got is the classic cut");
            Assert.AreEqual(0, curly.Headwear + curly.Glasses + curly.Facial + curly.Top + curly.Bottom, "the old hats, glasses, beards and clothes are gone");
            Assert.AreEqual("112233", curly.Shirt, "the colours stay");
            var bald = new CharacterLook { Version = 1, Haircut = 5 };
            bald.Repair();
            Assert.AreEqual(1, bald.Haircut, "bald stays bald");
            var followed = new CharacterLook { Version = 1, Haircut = -1 };
            followed.Repair();
            Assert.AreEqual(-1, followed.Haircut);
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
            two.Look.Haircut = 1;
            Assert.AreEqual(-1, one.Look.Haircut, "changing one's look leaves the other's");
        }
    }
}
