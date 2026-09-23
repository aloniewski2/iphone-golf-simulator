using System;

namespace GolfArcade.Swing
{
    /// How well the ball was struck, Wii Sports style: the word that pops up at impact.
    public enum StrikeGrade { Perfect, Great, Good, Thin }

    /// Which way the ball bends, for a right-handed swing: a little either way is a draw or a
    /// fade, a lot is a hook or a slice.
    public enum ShotShape { Straight, Draw, Fade, Hook, Slice }

    /// The verdict on one strike, read off what the detector measured.
    public readonly struct StrikeReport
    {
        public readonly StrikeGrade Grade;
        public readonly ShotShape Shape;
        /// Backswing time over downswing time; tour players swing close to 3:1.
        public readonly double TempoRatio;

        public StrikeReport(StrikeGrade grade, ShotShape shape, double tempo) { Grade = grade; Shape = shape; TempoRatio = tempo; }

        public string GradeWord => Grade switch
        {
            StrikeGrade.Perfect => "PERFECT!",
            StrikeGrade.Great => "GREAT!",
            StrikeGrade.Good => "GOOD",
            _ => "THIN",
        };

        public string ShapeWord => Shape switch
        {
            ShotShape.Draw => "DRAW",
            ShotShape.Fade => "FADE",
            ShotShape.Hook => "HOOK",
            ShotShape.Slice => "SLICE",
            _ => "STRAIGHT",
        };
    }

    public static class Strikes
    {
        /// Curve (spin-axis tilt, degrees) past which a draw or fade becomes a hook or slice.
        public const double BigCurve = 7;
        /// Tempo, backswing over downswing, that counts as smooth.
        public const double SmoothTempoLow = 2, SmoothTempoHigh = 8;   // (the detector sees the downswing late and short, so it reads long)

        public static ShotShape ShapeOf(double curveDegrees)
        {
            if (!double.IsFinite(curveDegrees) || Math.Abs(curveDegrees) < 0.5) return ShotShape.Straight;
            bool big = Math.Abs(curveDegrees) >= BigCurve;
            return curveDegrees > 0 ? (big ? ShotShape.Slice : ShotShape.Fade) : (big ? ShotShape.Hook : ShotShape.Draw);
        }

        /// Committed (the downswing had the speed), square (no curve to speak of) and smooth
        /// (a real tempo, not a snatch) is PERFECT; lose one and it is GREAT, more and it is GOOD.
        /// A downswing pushed rather than swung catches the ball THIN whatever else it did.
        public static StrikeReport Judge(SwingImpact impact)
        {
            var shape = ShapeOf(impact.CurveDegrees);
            double back = impact.TempoSeconds - impact.DownswingSeconds;
            double tempo = impact.DownswingSeconds > 1e-3 && back > 0 ? back / impact.DownswingSeconds : 0;
            if (impact.Thin > 0.25) return new StrikeReport(StrikeGrade.Thin, shape, tempo);
            bool committed = impact.Commit >= 1;
            bool square = shape == ShotShape.Straight;
            bool shaped = shape is ShotShape.Draw or ShotShape.Fade;
            bool smooth = tempo >= SmoothTempoLow && tempo <= SmoothTempoHigh;
            int faults = (committed ? 0 : 1) + (square ? 0 : shaped ? 1 : 2) + (smooth ? 0 : 1);
            var grade = faults == 0 ? StrikeGrade.Perfect : faults == 1 ? StrikeGrade.Great : StrikeGrade.Good;
            return new StrikeReport(grade, shape, tempo);
        }
    }
}
