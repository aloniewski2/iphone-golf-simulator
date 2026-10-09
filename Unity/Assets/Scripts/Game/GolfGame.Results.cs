using GolfArcade.Course;
using GolfArcade.Shot;
using GolfArcade.UI;
using GolfArcade.Tennis;
using System.Linq;
using UnityEngine;

namespace GolfArcade.Game
{
    public sealed partial class GolfGame
    {
        ShotResultPanel shotResultPanel;
        string[] resultEmotes = TennisEmotes.Normalize(null);
        public void EquipResultEmotes(string[] ids) => resultEmotes = TennisEmotes.Normalize(ids);
        public bool ShowingShotResult => Current == State.Result && shotResultPanel && shotResultPanel.gameObject.activeSelf;
        public string ShotStatistics { get; private set; }
        public string ShotResultTitle { get; private set; }
        public string ResultEmote => golfer ? golfer.Performing : null;

        string GolfReactionKey => "golf.reaction."+(holeStrokes==1?"holeinone":holeStrokes-hole.Par<=-2?"eagle":holeStrokes-hole.Par==-1?"birdie":holeStrokes==hole.Par?"par":"bogey");
        string GolfBoundaryKey => holeIndex+1>=course.Holes.Length?"golf.matchend":"golf.holeout."+course.Key+"."+hole.Number;
        float ResultReactionSeconds => LastShot!=null && LastShot.IsHoled ? Mathf.Max(PresentationDirector.GolfReaction(holeStrokes-hole.Par,holeStrokes==1,PresentationPolicy.Cut(GolfReactionKey)), PresentationDirector.Budget(holeIndex+1>=course.Holes.Length?PresentationBeat.MatchEnd:PresentationBeat.HoleOut,true,PresentationPolicy.Cut(GolfBoundaryKey))) : 3f;
        void MarkNaturalGolfResultSeen() {
            if(LastShot==null || !LastShot.IsHoled || stateTime<ResultReactionSeconds)return;
            if(PresentationPolicy.Cut(GolfReactionKey)==PresentationCut.Full)PresentationPolicy.Seen(GolfReactionKey);
            if(PresentationPolicy.Cut(GolfBoundaryKey)==PresentationCut.Full)PresentationPolicy.Seen(GolfBoundaryKey);
        }
        bool boundaryPresented;
        float ResultSkipAfter => LastShot!=null && LastShot.IsHoled ? (holeIndex+1>=course.Holes.Length?1.5f:holeStrokes==1?1f:.5f) : 0f;
        bool resultHeroCamera;
        void NextPresentationHole() { Enter(State.RoundDone); NextHole(); }
        void ShowShotResult()
        {
            var shot = LastShot;
            bool putt = club == GolfClub.Putter;
            ShotResultTitle = shot.IsHoled ? "IN THE HOLE!" : shot.PenaltyStrokes > 0 ? "PENALTY +1" : shot.Lie.Label().ToUpperInvariant();
            string quality = putt ? "PUTT" : lastReport.GradeWord;
            string remaining = shot.IsHoled ? "HOLED" : shot.Rest.DistanceTo(hole.Pin) < 10
                ? $"{shot.Rest.DistanceTo(hole.Pin) * 3:F1} ft to pin" : $"{shot.Rest.DistanceTo(hole.Pin):F0} yd to pin";
            string carry = putt ? "—" : $"{shot.Carry:F0} yd";
            string total = putt ? $"{shot.Total * 3:F0} ft" : $"{shot.Total:F0} yd";
            string detail = shot.PenaltyStrokes > 0 ? (InLava(shot) ? "Lava" : shot.Lie.Label()) + " · penalty stroke" : remaining;
            ShotStatistics = $"{quality} · Carry {carry} · Total {total} · {detail}";
            hud.HideLanding(); hud.HideShotStats(); hud.FlightMode(false); hud.ShowPlayHud(false);
            flightHud = false;
            aimLine.positionCount = 0;
            effects.ClearTracer();
            if (!shotResultPanel) shotResultPanel = ShotResultPanel.Create(transform, rig.Camera);
            BuildSwingCurve();
            string outcome = shot.IsHoled ? "HOLED!" : shot.PenaltyStrokes > 0 ? "PENALTY +1" : shot.Lie.Label();
            shotResultPanel.Show(quality, putt ? UiKit.ArcadeYellow : GradeColor(lastReport.Grade), outcome, SwingTiles(true), detail,
                curvePoints, curveReach, curveTarget,
                shot.IsHoled || Match?.IsContest == true ? "SCORECARD" : "NEXT SHOT", resultEmotes.Select(TennisEmotes.Name).ToArray(), ContinueShotResult, PlayResultEmote);
            shotResultPanel.AnimateStats(shot.Total,shot.Roll,shot.Apex,putt);
            // The result waits on a front view so equipped emotes read clearly.
            golfer.Perform("Idle");
            resultHeroCamera=PresentationPolicy.BigMoments && !bigMomentUsed && shot.IsHoled && holeStrokes<=hole.Par;
            if(resultHeroCamera) {bigMomentUsed=true;rig.FrameCharacterResult(golfer.transform);} else rig.FrameShotResult();
            rig.SnapNext(); rig.ApplyFrame();
        }

        public bool PlayResultEmote(int selection)
        {
            if (!ShowingShotResult || Time.timeScale <= 0 || selection < 0 || selection > 2) return false;
            string move = TennisEmotes.ExportName(resultEmotes[selection]);
            return move != null && golfer.Perform(move, loop: false);
        }

        public void ContinueShotResult()
        {
            if (Current != State.Result || Time.timeScale <= 0 || stateTime<ResultSkipAfter) return;
            boundaryPresented = LastShot.IsHoled;
            AfterResult();
        }
    }
}
