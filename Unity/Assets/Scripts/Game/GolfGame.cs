using System;
using GolfArcade.Course;
using GolfArcade.Shot;
using GolfArcade.Swing;
using GolfArcade.UI;
using UnityEngine;

namespace GolfArcade.Game
{
    /// The round, Wii Sports style: fly over the hole, aim with the buttons, swing the phone,
    /// watch the ball, read the result, repeat until it drops. One component on one object
    /// builds everything else at runtime.
    public sealed class GolfGame : MonoBehaviour
    {
        public enum State { Intro, Aim, Flight, Result, HoleDone, RoundDone }

        [Tooltip("Degrees per second the aim sweeps while a button is held.")]
        public float AimSweepDegreesPerSecond = 28f;
        [Tooltip("Degrees a tap nudges the aim.")]
        public float AimTapDegrees = 1.5f;
        [Tooltip("Use the keyboard/button swing even when a gyro is present.")]
        public bool ForceSyntheticSwing;

        public State Current { get; private set; } = State.Intro;
        public CourseShot LastShot { get; private set; }
        public SwingController Swing { get; private set; }
        public Scorecard Card { get; private set; }
        public Wind Wind { get; private set; }

        Course.Course course;
        GolfSounds sounds;
        readonly System.Random rng = new();
        int holeIndex;
        Hole hole;
        HoleView holeView;
        CameraRig rig;
        GolferView golfer;
        Hud hud;
        Transform ball;
        TrailRenderer trail;
        LineRenderer aimLine;
        Transform landingMarker;
        Camera minimapCamera;
        RenderTexture minimapTexture;

        CoursePoint ballAt;
        double heading;
        GolfClub club;
        int holeStrokes;
        float stateTime;
        double flightTime;
        Vector3 lastBallPos;
        bool aimedByPlayer;
        bool strikePlayed;
        SwingPhase lastPhase;

        void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            course = Course.Course.Meadow();

            var light = new GameObject("Sun").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50, -30, 0);
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;
            RenderSettings.ambientLight = new Color(0.55f, 0.6f, 0.65f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.75f, 0.85f, 0.95f);
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 250; RenderSettings.fogEndDistance = 700;

            rig = CameraRig.Create();
            hud = Hud.Create();
            golfer = GolferView.Create(transform);
            sounds = GolfSounds.Create(transform);

            ball = HoleView.Primitive(PrimitiveType.Sphere, "Ball", Color.white, transform).transform;
            ball.localScale = Vector3.one * 0.12f;
            trail = ball.gameObject.AddComponent<TrailRenderer>();
            trail.time = 2.5f; trail.startWidth = 0.12f; trail.endWidth = 0.02f;
            trail.material = HoleView.Mat(new Color(1, 1, 1, 0.8f));
            trail.emitting = false;

            aimLine = new GameObject("Aim line").AddComponent<LineRenderer>();
            aimLine.transform.SetParent(transform, false);
            aimLine.material = HoleView.UnlitMat(Color.white);
            aimLine.startWidth = aimLine.endWidth = 0.18f;
            aimLine.positionCount = 2;
            aimLine.useWorldSpace = true;

            landingMarker = HoleView.Primitive(PrimitiveType.Cylinder, "Landing marker", new Color(1f, 0.9f, 0.2f), transform).transform;
            landingMarker.GetComponent<Renderer>().sharedMaterial = HoleView.UnlitMat(new Color(1f, 0.9f, 0.2f));
            landingMarker.localScale = new Vector3(3, 0.02f, 3);

            BuildMinimap();

            Swing = new SwingController(ForceSyntheticSwing);
            Swing.OnLoad = OnLoad;
            Swing.OnCancel = OnCancel;
            Swing.OnImpact = OnImpact;
            Swing.Start();

            hud.AimLeft.Pressed = () => { Tick(); Nudge(-AimTapDegrees); };
            hud.AimRight.Pressed = () => { Tick(); Nudge(AimTapDegrees); };
            hud.ClubUp.Pressed = () => { Tick(); CycleClub(-1); };
            hud.ClubDown.Pressed = () => { Tick(); CycleClub(1); };
            hud.SwingHold.Pressed = () => Swing.Synthetic?.Backswing(true);
            hud.SwingHold.Released = () => Swing.Synthetic?.Backswing(false);

            StartRound();
        }

        void OnDestroy() { Haptics.Release(); Swing?.Stop(); }

        void Tick() { if (Current == State.Aim) { sounds.PlayTick(); Haptics.Tick(); } }

        void BuildMinimap()
        {
            minimapTexture = new RenderTexture(260, 420, 16);
            var go = new GameObject("Minimap camera");
            go.transform.SetParent(transform, false);
            minimapCamera = go.AddComponent<Camera>();
            minimapCamera.orthographic = true;
            minimapCamera.targetTexture = minimapTexture;
            minimapCamera.clearFlags = CameraClearFlags.SolidColor;
            minimapCamera.backgroundColor = new Color(0.2f, 0.4f, 0.15f);
            minimapCamera.cullingMask = ~0;
            hud.Minimap.texture = minimapTexture;
        }

        void FrameMinimap()
        {
            var tee = HoleView.ToWorld(hole.Tee); var pin = HoleView.ToWorld(hole.Pin);
            var mid = (tee + pin) / 2;
            var dir = (pin - tee); dir.y = 0;
            float length = dir.magnitude + 60;
            minimapCamera.transform.position = mid + Vector3.up * 200;
            minimapCamera.transform.rotation = Quaternion.LookRotation(Vector3.down, dir.normalized);
            minimapCamera.orthographicSize = length / 2;
            minimapCamera.aspect = 260f / 420f;
        }

        // ----- Hole flow -----

        void StartRound()
        {
            Card = new Scorecard(course);
            hud.HideScorecard();
            StartHole(0);
        }

        void StartHole(int index)
        {
            holeIndex = index;
            hole = course.Holes[index];
            if (holeView) Destroy(holeView.gameObject);
            holeView = HoleView.Build(hole, transform);
            ballAt = hole.Tee;
            holeStrokes = 0;
            Wind = Wind.Random(rng);
            hud.SetHole(hole.Number, hole.Par, hole.Length);
            double downTheHole = hole.Tee.HeadingTo(hole.Pin);
            hud.SetWind((float)Wind.RelativeTo(downTheHole), Wind.Describe(downTheHole), Wind.IsCalm);
            hud.SetScore(Card.Total, Card.ToPar, holeStrokes);
            FrameMinimap();
            PlaceBall(ballAt, 0);
            ball.gameObject.SetActive(true);
            golfer.SetVisible(false);
            hud.ShowSwingControls(false, false);
            hud.SetStatus("");
            hud.SetTempo("");
            hud.ShowBanner($"Hole {hole.Number}  ·  Par {hole.Par}", 3f);
            rig.SnapNext();
            Enter(State.Intro);
        }

        void BeginAim(bool keepHeading)
        {
            var lie = hole.LieAt(ballAt);
            bool putting = lie == CourseLie.Green;
            if (!keepHeading || !aimedByPlayer)
            {
                heading = ballAt.HeadingTo(putting ? hole.Pin : hole.RecommendedTarget(ballAt));
                aimedByPlayer = false;
            }
            club = AutoClub(lie, ballAt.DistanceTo(hole.Pin));
            Swing.SetClub(club);
            Swing.Armed = true;
            golfer.SetVisible(true);
            golfer.Settle();
            trail.emitting = false; trail.Clear();
            hud.ShowSwingControls(true, Swing.Synthetic != null);
            hud.SetMeter(0);
            UpdateAimVisuals();
            rig.FrameAddress(ball.position, AimDirection(), putting);
            Enter(State.Aim);
        }

        static GolfClub AutoClub(CourseLie lie, double toPin)
        {
            if (lie == CourseLie.Green) return GolfClub.Putter;
            if (lie == CourseLie.Bunker) return GolfClub.Wedge;
            if (toPin <= 100) return GolfClub.Wedge;
            if (toPin <= 185) return GolfClub.Iron;
            return GolfClub.Driver;
        }

        void Enter(State s) { Current = s; stateTime = 0; }

        Vector3 AimDirection() => new((float)Math.Sin(heading * Math.PI / 180), 0, (float)Math.Cos(heading * Math.PI / 180));

        void PlaceBall(CoursePoint p, double height)
        {
            ball.position = HoleView.ToWorld(p, height + 0.06);
        }

        void UpdateAimVisuals()
        {
            var lie = hole.LieAt(ballAt);
            bool putting = lie == CourseLie.Green;
            double toPin = ballAt.DistanceTo(hole.Pin);
            double rated = club.ReferenceDistanceYards() * lie.PowerFactor();
            var dir = AimDirection();
            var from = HoleView.ToWorld(ballAt, 0.03);
            aimLine.SetPosition(0, from);
            aimLine.SetPosition(1, from + dir * (float)Math.Min(rated, putting ? toPin + 3 : rated));
            landingMarker.position = putting ? from : HoleView.ToWorld(FullShotCarry(lie), 0.01);
            // Grows with distance so the ring stays readable from behind the ball.
            float ring = Mathf.Max(3f, (float)rated * 0.045f);
            landingMarker.localScale = new Vector3(ring, 0.02f, ring);
            landingMarker.gameObject.SetActive(!putting);
            golfer.Stand(ball.position, dir);
            hud.SetDistance(putting ? $"{toPin * 3:F0} ft to the hole" : $"{toPin:F0} yd to the pin");
            hud.SetClub($"{club.DisplayName()}  ·  {rated:F0} yd{(lie.PowerFactor() < 1 ? $"  ({lie.Label()})" : "")}");
            hud.SetWind((float)Wind.RelativeTo(heading), Wind.Describe(heading), Wind.IsCalm);
            hud.SetScore(Card.Total, Card.ToPar, holeStrokes);
        }

        /// Where a full, square swing with this club lands in today's wind: the yellow ring
        /// moves with the wind, so the player aims off it the way the Wii teaches.
        CoursePoint FullShotCarry(CourseLie lie)
        {
            var launch = club.Launch(1, 0, 0, lie.PowerFactor());
            launch.WindMPH = Wind.SpeedMPH;
            launch.WindDegrees = Wind.RelativeTo(heading);
            var carry = BallFlight.Simulate(launch).CarryPoint;
            double cosH = Math.Cos(heading * Math.PI / 180), sinH = Math.Sin(heading * Math.PI / 180);
            return new CoursePoint(ballAt.X + carry.LateralYards * cosH + carry.DistanceYards * sinH,
                                   ballAt.D - carry.LateralYards * sinH + carry.DistanceYards * cosH);
        }

        void Nudge(double degrees)
        {
            if (Current != State.Aim) return;
            heading += degrees;
            aimedByPlayer = true;
            UpdateAimVisuals();
            rig.FrameAddress(ball.position, AimDirection(), hole.LieAt(ballAt) == CourseLie.Green);
        }

        void CycleClub(int step)
        {
            if (Current != State.Aim) return;
            int i = Array.IndexOf(GolfClubs.All, club);
            club = GolfClubs.All[(i + step + GolfClubs.All.Length) % GolfClubs.All.Length];
            Swing.SetClub(club);
            UpdateAimVisuals();
        }

        // ----- Swing events -----

        void OnLoad(double load)
        {
            if (Current != State.Aim) return;
            hud.SetMeter((float)load);
            golfer.ShowLoad((float)load);
            // The wind-up: the phone buzzes harder and the creak climbs as the meter fills.
            Haptics.Tension(load);
            sounds.SetTension(load);
            hud.SetStatus("Backswing…");
        }

        void OnCancel()
        {
            if (Current != State.Aim) return;
            hud.SetMeter(0);
            golfer.Settle();
            Haptics.Release();
            sounds.Release();
            hud.SetStatus("Hold still, then swing");
        }

        void OnImpact(SwingImpact impact)
        {
            if (Current != State.Aim) return;
            Swing.Armed = false;
            Haptics.Release();
            sounds.Release();
            var lie = hole.LieAt(ballAt);
            LastShot = new CourseShot(club, impact, heading, ballAt, hole, lie.PowerFactor(), Wind);
            holeStrokes++;
            strikePlayed = false;
            hud.SetScore(Card.Total, Card.ToPar, holeStrokes);
            hud.SetMeter((float)impact.Power, (float)impact.Backswing);
            hud.SetTempo($"Speed {impact.PeakSpeed:F1} rad/s  ·  Face {impact.FaceDegrees:+0;-0}°  ·  Tempo {impact.TempoSeconds:F2}s" + (impact.Overswing > 0 ? "  ·  TOO HARD" : ""));
            golfer.Strike();
            hud.SetStatus("");
            hud.ShowSwingControls(false, false);
            landingMarker.gameObject.SetActive(false);
            aimLine.positionCount = 0;
            flightTime = -0.12; // let the club reach the ball
            lastBallPos = ball.position;
            Enter(State.Flight);
        }

        // ----- Frame loop -----

        void Update()
        {
            stateTime += Time.deltaTime;
            Swing.Update();

            switch (Current)
            {
                case State.Intro:
                    rig.Flyover(HoleView.ToWorld(hole.Pin), HoleView.ToWorld(hole.Tee), Mathf.Clamp01(stateTime / 3.5f));
                    if (stateTime > 3.5f || Input.GetMouseButtonDown(0)) BeginAim(false);
                    break;

                case State.Aim:
                    float sweep = (hud.AimLeft.IsHeld ? -1 : 0) + (hud.AimRight.IsHeld ? 1 : 0)
                                + (Input.GetKey(KeyCode.LeftArrow) ? -1 : 0) + (Input.GetKey(KeyCode.RightArrow) ? 1 : 0);
                    if (sweep != 0) Nudge(sweep * AimSweepDegreesPerSecond * Time.deltaTime);
                    if (Input.GetKeyDown(KeyCode.UpArrow)) CycleClub(-1);
                    if (Input.GetKeyDown(KeyCode.DownArrow)) CycleClub(1);
                    if (Swing.Phase == SwingPhase.Downswing && lastPhase != SwingPhase.Downswing) sounds.PlayWhoosh(Swing.Detector.Load);
                    if (Swing.Phase == SwingPhase.Address && lastPhase != SwingPhase.Address) { sounds.PlayReady(); Haptics.Tick(); }
                    if (Swing.Phase == SwingPhase.Backswing || Swing.Phase == SwingPhase.Downswing) { }
                    else if (Swing.Phase == SwingPhase.Address) hud.SetStatus(Swing.UsingPhone ? "Ready — swing!" : "Ready — hold SPACE or the button, release to swing");
                    else hud.SetStatus("Hold the phone still…");
                    break;

                case State.Flight:
                    flightTime += Time.deltaTime;
                    if (flightTime < 0) break;
                    if (!strikePlayed) { strikePlayed = true; sounds.PlayStrike(club, LastShot.Power); Haptics.Impact(LastShot.Power); }
                    var p = LastShot.PositionAt(flightTime);
                    var pos = new Vector3((float)p.x, (float)p.h + 0.06f, (float)p.d);
                    if (!trail.emitting && club != GolfClub.Putter) { trail.Clear(); trail.emitting = true; }
                    ball.position = pos;
                    var velocity = (pos - lastBallPos) / Mathf.Max(Time.deltaTime, 1e-4f);
                    lastBallPos = pos;
                    rig.Follow(pos, velocity, club == GolfClub.Putter);
                    if (flightTime >= LastShot.Duration) FinishShot();
                    break;

                case State.Result:
                    if (stateTime > 2.4f) AfterResult();
                    break;

                case State.HoleDone:
                    if (stateTime > 3f)
                    {
                        if (holeIndex + 1 < course.Holes.Length) StartHole(holeIndex + 1);
                        else
                        {
                            hud.ShowSwingControls(false, false);
                            hud.ShowScorecard(Card);
                            hud.PlayAgain.Pressed = StartRound;
                            Enter(State.RoundDone);
                        }
                    }
                    break;
            }
            lastPhase = Swing.Phase;
        }

        void FinishShot()
        {
            var shot = LastShot;
            trail.emitting = false;
            aimLine.positionCount = 2;
            string result;
            if (shot.IsHoled)
            {
                ball.gameObject.SetActive(false);
                sounds.PlayCup();
                sounds.PlayFanfare();
                Haptics.Success();
                result = "In the hole!";
            }
            else if (shot.Lie == CourseLie.Water) { sounds.PlaySplash(); Haptics.Failure(); result = "Water  ·  +1 stroke"; }
            else if (shot.Lie == CourseLie.OutOfBounds) { Haptics.Failure(); result = "Out of bounds  ·  +1 stroke"; }
            else if (club == GolfClub.Putter) result = $"{shot.Total * 3:F0} ft  ·  {shot.Rest.DistanceTo(hole.Pin) * 3:F1} ft left";
            else result = $"Carry {shot.Carry:F0}  ·  Total {shot.Total:F0} yd  ·  {shot.Lie.Label()}";
            holeStrokes += shot.PenaltyStrokes;
            hud.ShowBanner(result, 2.2f);
            hud.SetScore(Card.Total, Card.ToPar, holeStrokes);
            rig.HoldOn(ball.position, HoleView.ToWorld(hole.Pin) - ball.position, club == GolfClub.Putter);
            Enter(State.Result);
        }

        void AfterResult()
        {
            var shot = LastShot;
            if (shot.IsHoled)
            {
                Card.Record(holeIndex, holeStrokes);
                hud.SetScore(Card.Total, Card.ToPar, holeStrokes);
                hud.ShowBanner(Scorecard.ScoreName(holeStrokes, hole.Par), 3f);
                Enter(State.HoleDone);
                return;
            }
            ballAt = shot.NextPosition;
            PlaceBall(ballAt, 0);
            rig.SnapNext();
            BeginAim(false);
        }
    }
}
