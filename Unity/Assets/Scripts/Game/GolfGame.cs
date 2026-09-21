using System;
using GolfArcade.Course;
using GolfArcade.Net;
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
        public void NativeAim(float value) { if(Current==State.Aim) Nudge(Mathf.Clamp(value,-1,1)*AimTapDegrees); }
        public void NativeClub(int value) { if(Current==State.Aim) CycleClub(value); }
        public string NativeFeedback() {
            if(Current==State.RoundDone) return $"Round complete · {Card.Total} strokes · {Card.ToPar:+0;-0;0} to par";
            if(LastShot!=null && (Current==State.Result || Current==State.HoleDone)) return $"Carry {LastShot.Carry:F0} yd · Total {LastShot.Total:F0} yd · {LastShot.Lie}";
            return $"{Current} · {Swing.Phase} · Load {Swing.Detector.Load:P0}";
        }
        public void NativeSwing(float power) {
            if(Current!=State.Aim) return;
            golfer.ShowLoad(power);
            OnImpact(new SwingImpact { Power=power, Backswing=power, PeakSpeed=power*16, TempoSeconds=.7 });
        }
        bool needsReadyPose;
        public void NativeReady() { Swing.Detector.UseReadyPose=true; needsReadyPose=true; Swing.Detector.Reset(); }
        public void NativeMotion(NativeSportsSession.Sample sample) {
            var q=new System.Numerics.Quaternion(sample.qx,sample.qy,sample.qz,sample.qw);
            if(q.LengthSquared()<.5f) return;
            if(needsReadyPose) { Swing.Detector.SetReadyPose(q); needsReadyPose=false; Debug.Log("[SportsMotion] Ready pose captured"); }
            var e=Swing.Detector.Ingest(sample.time,q,new System.Numerics.Vector3(sample.rx,sample.ry,sample.rz),new System.Numerics.Vector3(sample.gx,sample.gy,sample.gz));
            if(e==null || Current!=State.Aim) return;
            switch(e.Value.Kind) {
                case SwingEventKind.Load: OnLoad(e.Value.Load); break;
                case SwingEventKind.Cancel: OnCancel(); break;
                case SwingEventKind.Impact:
                    var impact=e.Value.Impact;
                    Debug.Log($"[SportsMotion] golf impact speed={impact.PeakSpeed:F2} power={impact.Power:F2}");
                    if(NativeSportsSession.Left) { impact.FaceDegrees*=-1; impact.CurveDegrees*=-1; impact.StartLineDegrees*=-1; }
                    OnImpact(impact); break;
            }
        }

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
        ControllerButtons lastButtons;
        float nextAck, nextHint;
        string localAddresses = "";

        void Awake()
        {
            Application.targetFrameRate = 60;
            Application.runInBackground = true; // the Mac keeps playing while the phone is the club
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            course = Course.Course.Cliffside();

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
            aimLine.positionCount = AimLineSamples;
            aimLine.useWorldSpace = true;

            landingMarker = HoleView.Primitive(PrimitiveType.Cylinder, "Landing marker", new Color(1f, 0.9f, 0.2f), transform).transform;
            landingMarker.GetComponent<Renderer>().sharedMaterial = HoleView.UnlitMat(new Color(1f, 0.9f, 0.2f));
            landingMarker.localScale = new Vector3(3, 0.02f, 3);

            BuildMinimap();

            Swing = new SwingController(ForceSyntheticSwing || NativeSportsSession.Active, !NativeSportsSession.Active);
            Swing.OnLoad = OnLoad;
            Swing.OnCancel = OnCancel;
            Swing.OnImpact = OnImpact;
            Swing.OnSourceChanged = _ => { RefreshControls(); Haptics.Release(); sounds.Release(); hud.SetMeter(0); golfer.Settle(); };
            Swing.Start();

            hud.AimLeft.Pressed = () => { Tick(); Nudge(-AimTapDegrees); };
            hud.AimRight.Pressed = () => { Tick(); Nudge(AimTapDegrees); };
            hud.ClubUp.Pressed = () => { Tick(); CycleClub(-1); };
            hud.ClubDown.Pressed = () => { Tick(); CycleClub(1); };
            hud.SwingHold.Pressed = () => Swing.Synthetic?.Backswing(true);
            hud.SwingHold.Released = () => Swing.Synthetic?.Backswing(false);
            hud.GolferBody.Pressed = () => { GolferStyle.CycleBody(); RestyleGolfer(); };
            hud.GolferSkin.Pressed = () => { GolferStyle.CycleSkin(); RestyleGolfer(); };
            hud.SetGolferStyle(GolferStyle.BodyLabel, GolferStyle.SkinColor);

            StartRound();
        }

        void OnDestroy() { Haptics.Release(); Swing?.Stop(); }

        void Tick() { if (Current == State.Aim) { sounds.PlayTick(); Haptics.Tick(); } }

        /// Swap the golfer for the chosen body/skin, back at address on the ball.
        public void RestyleGolfer()
        {
            Tick();
            golfer.ApplyStyle();
            hud.SetGolferStyle(GolferStyle.BodyLabel, GolferStyle.SkinColor);
            if (Current == State.Aim) { golfer.Stand(ball.position, AimDirection()); hud.SetMeter(0); }
        }

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

        public Camera GameplayCamera => rig ? rig.Camera : null;
        public void PrepareNativeAddress() { BeginAim(false); rig.SnapNext(); rig.ApplyFrame(); }

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
            hud.SetStatus("");
            hud.SetTempo("");
            hud.ShowBanner($"Hole {hole.Number}  ·  Par {hole.Par}", 3f);
            rig.SnapNext();
            Enter(State.Intro);
            RefreshControls();
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
            RefreshControls();
            hud.SetMeter(0);
            UpdateAimVisuals();
            if (Current == State.Intro) rig.SnapNext(); // cut from the flyover, don't glide the length of the hole
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

        /// Which controls show: touch buttons and the debug swing button only while aiming,
        /// and not the touch buttons when a phone on the network is the club.
        void RefreshControls()
        {
            bool aiming = Current == State.Aim;
            hud.ShowSwingControls(aiming, Swing.Source == Swing.Synthetic, !Swing.UsingNetwork);
            UpdateControllerHint(true);
        }

        void UpdateControllerHint(bool force = false)
        {
            if (Swing.Network == null) return;
            if (!force && Time.unscaledTime < nextHint) return;
            nextHint = Time.unscaledTime + 3f;
            if (Swing.UsingNetwork) hud.SetControllerHint($"Club: iPhone at {Swing.Network.RemoteAddress}");
            else if (Application.isMobilePlatform) hud.SetControllerHint("");
            else
            {
                if (localAddresses == "" || force) localAddresses = NetworkMotionSource.LocalAddresses();
                hud.SetControllerHint($"Phone as club: open Golf Arcade on the iPhone → USE AS CLUB   ·   this Mac: {localAddresses}");
            }
        }

        /// The phone's buttons over the network behave like the on-screen ones: a press nudges
        /// (or changes club), a hold sweeps.
        void PollControllerButtons()
        {
            var held = Swing.UsingNetwork ? Swing.Network.Buttons : ControllerButtons.None;
            var pressed = held & ~lastButtons;
            lastButtons = held;
            if (Current != State.Aim) return;
            if ((pressed & ControllerButtons.AimLeft) != 0) { Tick(); Nudge(-AimTapDegrees); }
            if ((pressed & ControllerButtons.AimRight) != 0) { Tick(); Nudge(AimTapDegrees); }
            if ((pressed & ControllerButtons.ClubUp) != 0) { Tick(); CycleClub(-1); }
            if ((pressed & ControllerButtons.ClubDown) != 0) { Tick(); CycleClub(1); }
            float sweep = ((held & ControllerButtons.AimLeft) != 0 ? -1 : 0) + ((held & ControllerButtons.AimRight) != 0 ? 1 : 0);
            if (sweep != 0) Nudge(sweep * AimSweepDegreesPerSecond * Time.deltaTime);
        }

        /// Tell the phone what is happening so its screen and haptics can mirror the game.
        void SendAck()
        {
            if (Swing.Network == null || !Swing.Network.IsConnected || Time.unscaledTime < nextAck) return;
            nextAck = Time.unscaledTime + 1f / 30;
            byte phase; float load;
            switch (Current)
            {
                case State.Aim:
                    bool loading = Swing.Phase == SwingPhase.Backswing || Swing.Phase == SwingPhase.Downswing;
                    phase = loading ? (byte)2 : (byte)1; load = (float)Swing.Detector.Load; break;
                case State.Flight: phase = 3; load = (float)LastShot.Power; break;
                case State.Result: case State.HoleDone: phase = 4; load = 0; break;
                default: phase = 0; load = 0; break;
            }
            Swing.Network.SendAck(phase, load, hud.CurrentMessage);
        }

        Vector3 AimDirection() => new((float)Math.Sin(heading * Math.PI / 180), 0, (float)Math.Cos(heading * Math.PI / 180));

        /// The aim line is drawn as short segments laid on the ground, so it follows the terrain.
        const int AimLineSamples = 24;
        void LayAimLine(double length)
        {
            aimLine.positionCount = AimLineSamples;
            double sinH = Math.Sin(heading * Math.PI / 180), cosH = Math.Cos(heading * Math.PI / 180);
            for (int i = 0; i < AimLineSamples; i++)
            {
                double s = length * i / (AimLineSamples - 1);
                aimLine.SetPosition(i, HoleView.ToWorld(new CoursePoint(ballAt.X + sinH * s, ballAt.D + cosH * s), 0.05));
            }
        }

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
            LayAimLine(Math.Min(rated, putting ? toPin + 3 : rated));
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
            hud.SetStatus(NativeSportsSession.Active ? "Swing forward and follow through" : "Backswing…");
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
            float toBall = golfer.Strike();
            hud.SetStatus("");
            RefreshControls();
            landingMarker.gameObject.SetActive(false);
            aimLine.positionCount = 0;
            flightTime = -toBall; // the ball leaves when the club gets to it
            lastBallPos = ball.position;
            Enter(State.Flight);
        }

        // ----- Frame loop -----

        void Update()
        {
            stateTime += Time.deltaTime;
            Swing.Update();
            PollControllerButtons();
            UpdateControllerHint();

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
                    if (Input.GetKeyDown(KeyCode.G)) { GolferStyle.CycleBody(); RestyleGolfer(); }
                    if (Input.GetKeyDown(KeyCode.T)) { GolferStyle.CycleSkin(); RestyleGolfer(); }
                    if (Swing.Phase == SwingPhase.Downswing && lastPhase != SwingPhase.Downswing) sounds.PlayWhoosh(Swing.Detector.Load);
                    if (Swing.Phase == SwingPhase.Address && lastPhase != SwingPhase.Address) { sounds.PlayReady(); Haptics.Tick(); }
                    if (Swing.Phase == SwingPhase.Backswing || Swing.Phase == SwingPhase.Downswing) { }
                    else if (Swing.Phase == SwingPhase.Address) hud.SetStatus(Swing.UsingPhone || NativeSportsSession.Active ? "Ready — swing!" : "Ready — hold SPACE or the button, release to swing");
                    else if (NativeSportsSession.Active) hud.SetStatus("Return to your starting pose, then swing");
                    else if (Swing.UsingPhone && Swing.Detector.WrongEndDown) hud.SetStatus("Flip the phone: top edge toward the ground, like a club");
                    else if (Swing.UsingPhone && !Swing.Detector.PointedDown) hud.SetStatus($"Point the phone down at the ball, like a club  ({Swing.Detector.LeanDegrees:F0}° off)");
                    else hud.SetStatus("Hold the phone still…");
                    break;

                case State.Flight:
                    flightTime += Time.deltaTime;
                    if (flightTime < 0) break;
                    if (!strikePlayed) { strikePlayed = true; sounds.PlayStrike(club, LastShot.Power); Haptics.Impact(LastShot.Power); }
                    var p = LastShot.PositionAt(flightTime);
                    // Height is above the ground under the ball, so the arc rides the terrain.
                    var pos = HoleView.ToWorld(new CoursePoint(p.x, p.d), p.h + 0.06);
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
                            RefreshControls();
                            hud.ShowScorecard(Card);
                            hud.PlayAgain.Pressed = StartRound;
                            Enter(State.RoundDone);
                        }
                    }
                    break;
            }
            lastPhase = Swing.Phase;
            SendAck();
        }

        void FinishShot()
        {
            var shot = LastShot;
            trail.emitting = false;
            aimLine.positionCount = AimLineSamples;
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
