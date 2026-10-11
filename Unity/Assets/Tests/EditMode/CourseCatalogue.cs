using System.Linq;
using GolfArcade.Course;

namespace GolfArcade.Tests
{
    /// The sixteen modelled holes by number, whichever of the three courses they are dealt to.
    static class Catalogue
    {
        public static Hole Hole(int number) => Course.Course.Containing(number).Holes.Single(h => h.Number == number);
    }
}
