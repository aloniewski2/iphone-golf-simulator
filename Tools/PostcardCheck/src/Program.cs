using System;
using System.Globalization;
using System.Linq;
using GolfCourse = GolfArcade.Course.Course;

namespace GolfArcade.PostcardCheck
{
    /// Standalone harness around the REAL game sources (Hole.cs, CourseShot.cs, Wind.cs, Scorecard.cs, BallFlight.cs, GolfClub.cs, MotionSwingDetector.cs).
    /// Build and run with Tools/PostcardCheck/run.sh; see the usage text below.
    public static class Program
    {
        const string Usage = @"PostcardCheck (real Hole.cs / CourseShot.cs compiled with Unity's Roslyn, run on Unity's dotnet)
  report --course X [--hole N]                 per hole: number, par, centerline length, shore points, hazards, fairway/green/rough numbers, water carries
  lies   --course X [--hole N] [--step 2]      LieAt of every grid cell as letters (T F R B G W O), one row per d; --points FILE|- lists letters for 'x d' lines
  probe  --course X --hole N --at x,d [--at ..]  every sub-expression of LieAt at a point
  gates  --course X [--hole N] [--map] [--fast] [--set key=value ..]   independent design gates, GATE: NAME PASS|FAIL - detail
  plan   --course X [--hole N] [--budget 160] [--max-strokes S] [--wind mph,deg] [--ridge|--no-ridge] [--ridge-margin 4]   shot planner (real CourseShot), PAR_REACHABLE
  plan-selfcheck --course X [--samples 120]   brute-force proof that the planner's two prunings lose no finish
  tests  FILE.cs [more.cs] [--filter text]    compile NUnit tests against the real sources + shim, run every [Test]
X is a static factory on GolfArcade.Course.Course (Cliffside, Meadow, Postcards ...), matched by method name or Course.Name.
Exit codes: 0 pass, 1 a gate/test failed, 2 usage, 3 course does not exist yet, 4 test compile error.";

        public static int Main(string[] argv)
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            Console.OutputEncoding = new System.Text.UTF8Encoding(false);
            if (argv.Length == 0 || argv[0] is "help" or "-h" or "--help") { Console.WriteLine(Usage); return argv.Length == 0 ? 2 : 0; }
            var a = new Args(argv);
            string cmd = a.Positional.FirstOrDefault() ?? "";
            try
            {
                if (cmd == "tests") return TestRunner.Command(a);
                if (cmd is "report" or "lies" or "probe" or "gates" or "plan" or "plan-selfcheck")
                {
                    var course = CourseSelect.Find(a.Get("course"), out string err);
                    if (course == null)
                    {
                        Console.WriteLine($"COURSE_MISSING: {err}");
                        if (cmd == "gates") Console.WriteLine("GATE: COURSE_EXISTS FAIL - " + err);
                        return 3;
                    }
                    if (a.Has("hole") && !CourseSelect.Holes(course, a).Any()) { Console.WriteLine($"no hole {a.Get("hole")} in course {course.Name} (holes: {string.Join(", ", course.Holes.Select(h => h.Number))})"); return 2; }
                    switch (cmd)
                    {
                        case "report": return Report.Run(course, a);
                        case "lies": return Report.Lies(course, a);
                        case "probe": return Report.Probe(course, a);
                        case "gates": return Gates.Command(course, a);
                        case "plan-selfcheck": return Planner.SelfCheck(course, a);
                        default: return Planner.Command(course, a);
                    }
                }
                Console.Error.WriteLine("unknown command '" + cmd + "'\n" + Usage);
                return 2;
            }
            catch (Exception e) when (e is ArgumentException or FormatException)
            {
                Console.Error.WriteLine("error: " + e.Message);
                return 2;
            }
        }
    }
}
