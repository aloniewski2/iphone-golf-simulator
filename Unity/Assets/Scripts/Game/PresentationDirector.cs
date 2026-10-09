using System;

namespace GolfArcade.Game
{
    public enum PresentationCut { Full, Short, Off }
    public enum PresentationBeat { None, Loading, Venue, WalkOn, Start, Point, Game, Set, HoleOut, MatchEnd, Exit }

    /// Shared timing/input policy. Sport adapters own camera and actor choreography;
    /// this clock never awards a point, advances a hole, or changes simulation speed.
    public sealed class PresentationDirector
    {
        public PresentationBeat Beat { get; private set; }
        public PresentationCut Cut { get; private set; }
        public float Elapsed { get; private set; }
        public float Duration { get; private set; }
        public float SkipFrom { get; private set; }
        public int Sequence { get; private set; }
        public bool Playing { get; private set; }
        public bool Shared { get; private set; }
        public float Fraction => Duration <= 0 ? 1 : Math.Min(1, Elapsed / Duration);
        Action cleanup, ready, markSeen;

        public static PresentationCut Select(PresentationCut preference, bool seen, bool multiplayer, bool constrained) =>
            preference == PresentationCut.Off ? PresentationCut.Off : multiplayer || constrained || seen ? PresentationCut.Short : preference;

        public static float Budget(PresentationBeat beat, bool golf, PresentationCut cut)
        {
            bool full = cut == PresentationCut.Full;
            if (cut == PresentationCut.Off && (beat == PresentationBeat.Venue || beat == PresentationBeat.WalkOn)) return 0;
            return beat switch {
                PresentationBeat.Venue => golf ? (full ? 4 : 1.5f) : (full ? 3 : 1.2f),
                PresentationBeat.WalkOn => golf ? (full ? 1.5f : .8f) : (full ? 3 : 1.5f),
                PresentationBeat.Start => golf ? (full ? 1 : .6f) : (full ? 1.2f : .8f),
                PresentationBeat.Point => full ? 1.2f : .8f,
                PresentationBeat.Game => full ? 2.5f : 1.5f,
                PresentationBeat.Set => full ? 4 : 2.5f,
                PresentationBeat.HoleOut => full ? 2.5f : 1.5f,
                PresentationBeat.MatchEnd => full ? 6 : 3,
                PresentationBeat.Exit => full ? 1 : .8f,
                _ => 0,
            };
        }
        public static float GolfReaction(int strokesToPar, bool holeInOne, PresentationCut cut)
        {
            bool full = cut == PresentationCut.Full;
            if (holeInOne) return full ? 4 : 3;
            if (strokesToPar <= -2) return full ? 2 : 1.2f;
            if (strokesToPar == -1) return full ? 1.5f : 1;
            return strokesToPar == 0 ? (full ? .8f : .5f) : (full ? .6f : .5f);
        }
        public void Begin(PresentationBeat beat, PresentationCut cut, float duration, float skipFrom,
            Action restore, Action readyState, Action seen = null, bool shared = false)
        {
            Cancel(); Sequence++; Beat = beat; Cut = cut; Duration = Math.Max(0, duration);
            Elapsed = 0; SkipFrom = Math.Max(0, skipFrom); Shared = shared;
            cleanup = restore; ready = readyState; markSeen = seen; Playing = true;
            if (Duration == 0) Finish(false);
        }
        public void Tick(float elapsedSeconds, bool paused = false)
        {
            if (!Playing || paused || float.IsNaN(elapsedSeconds) || float.IsInfinity(elapsedSeconds) || elapsedSeconds < 0) return;
            Elapsed = Math.Min(Duration, Elapsed + elapsedSeconds);
            if (Elapsed >= Duration) Finish(false);
        }
        public bool Skip()
        {
            if (!Playing || Shared || Beat == PresentationBeat.Loading || Beat == PresentationBeat.Exit || Elapsed < SkipFrom) return false;
            Finish(true); return true;
        }
        public void Cancel()
        {
            if (!Playing) return;
            Playing = false; var restore = cleanup; cleanup = ready = markSeen = null; restore?.Invoke();
        }
        void Finish(bool skipped)
        {
            if (!Playing) return;
            Playing = false;
            var restore = cleanup; var go = ready; var seen = markSeen;
            cleanup = ready = markSeen = null;
            restore?.Invoke();
            if (!skipped && Cut == PresentationCut.Full) seen?.Invoke();
            go?.Invoke();
        }
    }
}
