using GolfArcade.Course;
using NUnit.Framework;

namespace GolfArcade.Tests
{
    public class GolfCourseStandardTests
    {
        [TestCase("MAT_SNOW", "TERRAIN", GolfCourseLook.Surface.Snow)]
        [TestCase("MAT_ICE_DEEP", "WATER_ICE", GolfCourseLook.Surface.Ice)]
        [TestCase("MAT_ROUGH_ASH", "TERRAIN", GolfCourseLook.Surface.Ash)]
        [TestCase("MAT_REDROCK_CREAM", "TERRAIN", GolfCourseLook.Surface.Sandstone)]
        [TestCase("MAT_GLASS", "WATERFALL", GolfCourseLook.Surface.Fall)]
        [TestCase("MAT_FOAM", "WATER_FALL_1", GolfCourseLook.Surface.Fall)]
        [TestCase("MAT_FOAM", "WATER_FOAM", GolfCourseLook.Surface.Surf)]
        [TestCase("MAT_GREEN.001", "TEE_BOX", GolfCourseLook.Surface.Tee)]
        [TestCase("MAT_FIRSTCUT", "FAIRWAY_FIRSTCUT", GolfCourseLook.Surface.Fringe)]
        public void AuthoredSurfacesKeepTheirIdentity(string material, string renderer, GolfCourseLook.Surface expected)
            => Assert.AreEqual(expected, GolfCourseLook.Role(material,renderer,false));

        [TestCase("MAT_WATER")]
        [TestCase("MAT_WATER_SHALLOW")]
        [TestCase("MAT_FOAM")]
        public void MagmaSeaRemainsLava(string material)
            => Assert.AreEqual(GolfCourseLook.Surface.Lava,GolfCourseLook.Role(material,"WATER_OCEAN",true));

        [TestCase("MAT_FLAG")]
        [TestCase("MAT_POLE")]
        [TestCase("MAT_CUP")]
        [TestCase("MAT_BALL")]
        public void GameplayMaterialsKeepTheirOwnRoute(string material)
            => Assert.IsNull(GolfCourseLook.Role(material,"Pin",false));

        [Test]
        public void CompleteSelectableCatalogHasExactlyThirteenUpgradeTargets()
        {
            int count=0;
            foreach(var course in Course.Course.All()) foreach(var h in course.Holes)
            {
                Assert.AreEqual(!GolfLook.IsPostcard(h.Number),GolfCourseLook.Handles(h.Number));
                if(GolfCourseLook.Handles(h.Number)) { count++; Assert.IsNotNull(GolfCourseAtmosphere.For(h.Number)); }
            }
            Assert.AreEqual(13,count);
        }

        [Test]
        public void CourseProfilesAreIndependentOfThePostcardReference()
        {
            var reference=GolfAtmosphere.Holes[9];
            var alpine=GolfCourseAtmosphere.For(17); var jungle=GolfCourseAtmosphere.For(19);
            Assert.AreNotSame(reference,alpine); Assert.AreNotSame(alpine,jungle);
            Assert.AreNotEqual(alpine.FogColor,jungle.FogColor);
            Assert.AreEqual("Split",reference.Name);
        }
    }
}
