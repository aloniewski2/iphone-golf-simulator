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
        public enum State { Menu, Golfer, Intro, Aim, Flight, Result, HoleDone, RoundDone }

        [Tooltip("Degrees per second the aim sweeps while a button is held.")]
        public float AimSweepDegreesPerSecond = 28f;
        [Tooltip("Degrees a tap nudges the aim.")]
        public float AimTapDegrees = 1.5f;
        [Tooltip("Use the keyboard/button swing even when a gyro is present.")]
        public bool ForceSyntheticSwing;

        public State Current { get; private set; } = State.Menu;
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
        GreenRead greenRead;
        BigScreen bigScreen;
        Hud.MenuView menu;
        /// 7, 12, or 0 for the whole round; remembered on the device.
        int chosenHoles;
        CameraRig rig;
        bool holeCam;
        GolferView golfer;
        Hud hud;
        Transform ball;
        ShotEffects effects;
        /// Times the ball has met the ground this shot (bounces and the landing), for reviews.
        public int Bounces { get; private set; }
        /// The hole's designed shot (Blender, Resources/Course/hole_NN_shot.json), played after the
        /// intro's aerial; null when the hole has none.
        SignatureShot signature;
        /// Codex's animated ball and trail tubes for that shot, played as one clip.
        CinematicRig cinematic;
        int signatureLanding;
        bool signaturePlaying;
        /// True while the intro is on the designed shot rather than the aerial.
        public bool SignaturePlaying => Current == State.Intro && signaturePlaying;
        /// The ball, for the tests' eyes — Codex's during his shot, the game's otherwise.
        public Vector3 BallPosition => signaturePlaying && cinematic != null ? cinematic.Ball.position : ball.position;
        /// The ball's spin, shown by Codex's stripe: backspin in the air, a roll on the ground.
        Quaternion ballSpin = Quaternion.identity;
        Vector3 lastSpinPos;
        /// The ball grows for the camera while it flies — Codex's presentation ball is a twentieth
        /// of the frame — and is back to its true 0.12 yd by the time it stops.
        const float BallSize = 0.12f;
        float ballScale = 1f, ballScaleVelocity;
        double lastHeight;
        bool dropped, trailing;
        LineRenderer aimLine;
        Transform landingMarker;
        LandingZone landingZone;
        readonly System.Collections.Generic.List<Vector3> aimPath = new();
        Camera minimapCamera;
        RenderTexture minimapTexture;

        CoursePoint ballAt;
        double heading;
        GolfClub club;
        int holeStrokes;
        float stateTime;
        double flightTime;
        double launchGround, landingGround;
        /// Seconds the ball spends dropping off an edge when it comes down lower than it left
        /// (into the sea, off a cliff): a free fall drawn between the carry and the roll.
        double dropSeconds;
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
            // Past the sea's edge the default skybox shows its grey ground; make that the fog's
            // colour so the far distance dissolves instead of banding (on a copy, not the asset).
            if (RenderSettings.skybox && RenderSettings.skybox.HasProperty("_GroundColor"))
            {
                RenderSettings.skybox = new Material(RenderSettings.skybox);
                RenderSettings.skybox.SetColor("_GroundColor", RenderSettings.fogColor);
            }

            rig = CameraRig.Create();
            hud = Hud.Create();
            greenRead = GreenRead.Create(transform);
            golfer = GolferView.Create(transform);
            sounds = GolfSounds.Create(transform);

            ball = HoleView.Primitive(PrimitiveType.Sphere, "Ball", Color.white, transform).transform;
            ball.localScale = Vector3.one * 0.12f;
            DressBall();
            effects = ShotEffects.Create(transform, ball, rig.Camera);

            aimLine = new GameObject("Aim line").AddComponent<LineRenderer>();
            aimLine.transform.SetParent(transform, false);
            aimLine.material = HoleView.UnlitMat(Color.white);
            aimLine.startWidth = aimLine.endWidth = 0.18f;
            aimLine.positionCount = AimLineSamples;
            aimLine.useWorldSpace = true;

            landingZone = LandingZone.Create(transform);
            landingMarker = landingZone.transform;

            BuildMinimap();

            Swing = new SwingController(ForceSyntheticSwing);
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
            bigScreen = BigScreen.Create(transform, rig.Camera);
            bigScreen.OnChanged = on =>
            {
                if (on) { hud.EnterControllerLayout(); hud.Controller.OnClub = i => SelectClub(GolfClubs.All[i]); hud.SetHole(hole.Number, hole.Par, hole.Length, hole.Picture); }
                else hud.LeaveControllerLayout();
                RefreshControls();
                if (Current == State.Aim) UpdateAimVisuals();
            };
            chosenHoles = PlayerPrefs.GetInt("holes", 0);

            ShowMenu();
        }

        // ----- The menu -----

        /// The holes to play: one of them, or the round.
        Course.Course CourseFor(int holes)
        {
            var all = Course.Course.Cliffside();
            if (holes == 0) return all;
            return new Course.Course { Name = all.Name, Holes = System.Array.FindAll(all.Holes, h => h.Number == holes) };
        }

        /// Before a round: pick the golfer, the holes and the screen, over a slow flyover of the
        /// first hole to be played.
        public void ShowMenu()
        {
            course = CourseFor(chosenHoles);
            hole = course.Holes[0];
            if (holeView) DestroyImmediate(holeView.gameObject);
            holeView = HoleView.Build(hole, transform);
            ball.gameObject.SetActive(false);
            golfer.SetVisible(false);
            greenRead.Hide();
            aimLine.positionCount = 0;
            landingMarker.gameObject.SetActive(false);
            hud.HideScorecard();
            hud.ShowPlayHud(false);
            menu = hud.ShowMenu();
            var stamp = Resources.Load<TextAsset>("build_id");
            menu.Build.text = stamp ? $"build {stamp.text.Trim()}" : "";
            menu.Golfer.Pressed = OpenGolferPicker;
            menu.HoleSeven.Pressed = () => ChooseHoles(7);
            menu.HoleTwelve.Pressed = () => ChooseHoles(12);
            menu.BothHoles.Pressed = () => ChooseHoles(0);
            menu.AirPlay.Pressed = () => { bigScreen.SetWanted(true); bigScreen.OpenAirPlayPicker(); RefreshMenu(); };
            menu.Play.Pressed = Play;
            RefreshMenu();
            rig.SnapNext();
            Enter(State.Menu);
        }

        void ChooseHoles(int holes)
        {
            chosenHoles = holes;
            PlayerPrefs.SetInt("holes", holes); PlayerPrefs.Save();
            ShowMenu(); // the flyover moves to the chosen hole
        }

        void RefreshMenu() => menu?.Refresh(GolferStyle.Summary, chosenHoles, bigScreen.Status);

        // ----- The golfer picker -----

        Hud.GolferPicker picker;

        /// Its own screen: the golfer stands at the tee, club in hand, turning slowly, and
        /// every choice on the sheet below changes them on the spot.
        public void OpenGolferPicker()
        {
            if (Current != State.Menu) return;
            hud.HideMenu(); menu = null;
            picker = hud.ShowGolferPicker(GolferStyle.SkinTones, GolferStyle.HairNames, GolferStyle.HairColors);
            picker.Male.Pressed = () => { Tick(); GolferStyle.Body = GolferStyle.BodyKind.Male; RestyleForPicker(); };
            picker.Female.Pressed = () => { Tick(); GolferStyle.Body = GolferStyle.BodyKind.Female; RestyleForPicker(); };
            for (int i = 0; i < picker.Skins.Length; i++) { int tone = i; picker.Skins[i].Pressed = () => { Tick(); GolferStyle.SkinTone = tone; RestyleForPicker(); }; }
            for (int i = 0; i < picker.Hairs.Length; i++) { int hair = i; picker.Hairs[i].Pressed = () => { Tick(); GolferStyle.Hair = (GolferStyle.HairKind)hair; RestyleForPicker(); }; }
            for (int i = 0; i < picker.HairColors.Length; i++) { int tone = i; picker.HairColors[i].Pressed = () => { Tick(); GolferStyle.HairTone = tone; RestyleForPicker(); }; }
            picker.Done.Pressed = CloseGolferPicker;
            ballAt = hole.Tee; heading = hole.Tee.HeadingTo(hole.Pin);
            PlaceBall(ballAt, 0);
            ball.gameObject.SetActive(true);
            Enter(State.Golfer);
            RestyleForPicker();
            rig.FramePortrait(golfer.transform.position, golfer.transform.forward, 0);
            rig.SnapNext();
        }

        void RestyleForPicker() => RestyleGolfer();

        public void CloseGolferPicker()
        {
            hud.HideGolferPicker(); picker = null;
            ShowMenu();
        }

        /// From the menu into the round.
        public void Play()
        {
            if (Current != State.Menu) return;
            hud.HideMenu();
            hud.ShowPlayHud(true);
            StartRound();
        }

        void OnDestroy() { Haptics.Release(); Swing?.Stop(); }

        void Tick() { if (Current == State.Aim) { sounds.PlayTick(); Haptics.Tick(); } }

        /// Swap the golfer for the chosen body/skin, back at address on the ball.
        public void RestyleGolfer()
        {
            Tick();
            golfer.ApplyStyle();
            if (Current == State.Aim) { golfer.Stand(ball.position, AimDirection()); hud.SetMeter(0); }
            else if (Current == State.Golfer)
            {
                golfer.SetClub(GolfClub.Driver, false);
                golfer.Stand(ball.position, AimDirection());
                golfer.SetVisible(true);
                picker?.Refresh(GolferStyle.Body == GolferStyle.BodyKind.Female, GolferStyle.SkinTone, (int)GolferStyle.Hair, GolferStyle.HairTone);
            }
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

        void StartRound()
        {
            course = CourseFor(chosenHoles);
            Card = new Scorecard(course);
            hud.HideScorecard();
            StartHole(0);
        }

        /// Straight to a hole of the round, for tests and reviews; the card keeps what was played.
        public void JumpToHole(int number)
        {
            int index = System.Array.FindIndex(course.Holes, h => h.Number == number);
            if (index < 0)
            {
                course = CourseFor(0); Card = new Scorecard(course); // not in the chosen holes: play the whole round
                index = System.Array.FindIndex(course.Holes, h => h.Number == number);
                if (index < 0) throw new System.ArgumentException($"no hole {number} on {course.Name}");
            }
            StartHole(index);
        }

        void StartHole(int index)
        {
            holeIndex = index;
            hole = course.Holes[index];
            // Gone now, not at the end of the frame: the new hole's ground is read by raycast
            // while it is built (pin height, the green's slope grid), and the old one is in the way.
            if (holeView) DestroyImmediate(holeView.gameObject);
            holeView = HoleView.Build(hole, transform);
            ballAt = hole.Tee;
            holeStrokes = 0;
            Wind = Wind.Random(rng);
            holeView.ShowFlag(true);
            if (!Wind.IsCalm) holeView.SetFlagWind(Wind.DirectionDegrees);
            hud.SetHole(hole.Number, hole.Par, hole.Length, hole.Picture);
            double downTheHole = hole.Tee.HeadingTo(hole.Pin);
            hud.SetWind((float)Wind.RelativeTo(downTheHole), Wind.Describe(downTheHole), Wind.IsCalm, Wind.SpeedMPH);
            hud.SetScore(Card.Total, Card.ToPar, holeStrokes);
            FrameMinimap();
            PlaceBall(ballAt, 0);
            ball.gameObject.SetActive(true);
            golfer.SetVisible(false);
            hud.SetStatus("");
            hud.SetTempo("");
            // The showcase: HUD away, letterbox and the hole's card over the flyover.
            hud.ShowPlayHud(false);
            hud.ShowHoleIntro(hole.Number, hole.Name, hole.Par, hole.Length, hole.Picture, hole.Blurb,
                Wind.IsCalm ? "Calm today" : $"Wind   ·   {Wind.Describe(downTheHole)}");
            signature = SignatureShot.Load(hole.Number, holeView);
            cinematic?.Destroy();
            cinematic = signature != null ? CinematicRig.Load(hole.Number, holeView) : null;
            signatureLanding = 0; signaturePlaying = overviewPlaying = false;
            rig.SnapNext();
            Enter(State.Intro);
            RefreshControls();
        }

        void BeginAim(bool keepHeading)
        {
            if (Current == State.Intro)
            {
                hud.HideHoleIntro(); hud.ShowPlayHud(true);
                if (signaturePlaying || overviewPlaying)
                {
                    rig.RestoreFov(); signaturePlaying = overviewPlaying = false;
                    cinematic?.Show(false);
                    ball.gameObject.SetActive(true); PlaceBall(ballAt, 0);
                }
            }
            var lie = hole.LieAt(ballAt);
            bool putting = lie == CourseLie.Green;
            if (!keepHeading || !aimedByPlayer)
            {
                heading = ballAt.HeadingTo(putting ? hole.Pin : hole.RecommendedTarget(ballAt));
                aimedByPlayer = false;
            }
            club = AutoClub(lie, ballAt.DistanceTo(hole.Pin));
            Swing.SetClub(club);
            golfer.SetClub(club, ballAt.DistanceTo(hole.Pin) < 40);
            Swing.Armed = true;
            golfer.SetVisible(true);
            golfer.Settle();
            effects.EndFlight();
            RefreshControls();
            hud.SetMeter(0);
            UpdateAimVisuals();
            if (Current == State.Intro) rig.SnapNext(); // cut from the flyover, don't glide the length of the hole
            rig.ResetZoom();
            if (putting) rig.FrameGreen(ball.position, AimDirection(), (float)ballAt.DistanceTo(hole.Pin));
            else rig.FrameAddress(ball.position, AimDirection(), putting);
            // On the green the flag comes out and the read goes down.
            holeView.ShowFlag(!putting);
            if (putting) greenRead.Show(hole); else greenRead.Hide();
            Enter(State.Aim);
        }

        /// Straight to a spot on the hole, for tests and reviews: the ball is dropped there and
        /// the next stroke set up as if it had just rolled to a stop.
        public void DropBall(CoursePoint at)
        {
            ballAt = at;
            aimedByPlayer = false;
            PlaceBall(ballAt, 0);
            ball.gameObject.SetActive(true);
            rig.SnapNext();
            BeginAim(false);
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
        /// Yards from the cup at which a putt's camera cuts to the hole cam.
        const float HoleCamReach = 3.5f;
        /// The hole's showcase before the first shot, seconds: the aerial and the walkthrough — or,
        /// on a hole with a signature shot, the aerial and then that shot.
        const float ShowcaseSeconds = 10f;
        const float AerialSeconds = ShowcaseSeconds * CameraRig.AerialShare;
        /// Codex's establishing drift is eight seconds; the intro gives it five and runs it faster.
        const float OverviewSeconds = 5f;
        /// What runs before the signature shot: the establishing drift where the file has one, else the aerial.
        float LeadSeconds => signature != null && signature.HasOverview ? OverviewSeconds : AerialSeconds;
        float IntroSeconds => signature != null ? LeadSeconds + signature.Duration : ShowcaseSeconds;
        bool overviewPlaying;

        /// The establishing camera over the course, `s` seconds in.
        void PlayOverview(float s)
        {
            if (!overviewPlaying) { overviewPlaying = true; rig.SnapNext(); }
            signature.OverviewAt(s / OverviewSeconds * signature.OverviewDuration, out var at, out var look, out var up, out var hfov);
            rig.Cue(at, look, up);
            rig.SetHorizontalFov(hfov);
        }

        /// The designed shot, `s` seconds in: the ball on its path, the camera on its, the trail
        /// and the turf puffs of a pure strike along the way.
        void PlaySignature(float s)
        {
            if (!signaturePlaying)
            {
                signaturePlaying = true;
                rig.SnapNext();                                   // a cut from the establishing shot
                sounds.PlayStrike(GolfClub.Iron, 0.9);
                if (cinematic != null)
                {
                    // Codex's ball and trail tube take over from the game's ball for the shot
                    cinematic.SetQuality(ShotEffects.Quality.Pure);
                    cinematic.Show(true);
                    ball.gameObject.SetActive(false);
                }
                else { effects.SetQuality(ShotEffects.Quality.Pure); effects.BeginFlight(); }
            }
            if (cinematic != null) cinematic.Sample(signature.ClipTime(s));
            else ball.position = signature.BallAt(s, 0.06f);
            signature.CameraAt(s, out var at, out var look, out var up, out var hfov);
            rig.Cue(at, look, up);
            rig.SetHorizontalFov(hfov);                           // the lens breathes as Codex keyed it
            var ballNow = cinematic != null ? cinematic.Ball.position : ball.position;
            while (signatureLanding < signature.Landings.Length && s >= signature.Landings[signatureLanding])
            {
                float strength = signatureLanding == 0 ? 1.2f : 0.6f;
                effects.Touchdown(ballNow - Vector3.up * (cinematic != null ? 0.5f : 0.04f), CourseLie.Green, strength);
                sounds.PlayThud(strength);
                signatureLanding++;
            }
        }
        void LayAimLine(double length)
        {
            if (club == GolfClub.Putter) { LayPuttRibbon(); return; }
            // A full, square swing's flight in today's wind: the arc the games draw to the
            // target ring, level with the ground it leaves, so it shows in the course view and on
            // the minimap alike.
            var lie = hole.LieAt(ballAt);
            var launch = club.Launch(1, 0, 0, lie.PowerFactor());
            launch.WindMPH = Wind.SpeedMPH;
            launch.WindDegrees = Wind.RelativeTo(heading);
            var flight = BallFlight.Simulate(launch);
            double sinH = Math.Sin(heading * Math.PI / 180), cosH = Math.Cos(heading * Math.PI / 180);
            double ground = HoleView.GroundHeight(ballAt);
            const double step = 0.12;
            int samples = Math.Max(2, (int)(flight.RollStartTime / step) + 2);
            aimLine.material = HoleView.UnlitMat(Color.white);
            aimLine.positionCount = samples;
            aimPath.Clear();
            for (int i = 0; i < samples; i++)
            {
                var p = flight.PositionAt(Math.Min(flight.RollStartTime, i * step));
                var at = new CoursePoint(ballAt.X + p.LateralYards * cosH + p.DistanceYards * sinH, ballAt.D - p.LateralYards * sinH + p.DistanceYards * cosH);
                double under = HoleView.GroundHeight(at);
                var world = new Vector3((float)at.X, (float)(Math.Max(ground, under) + p.HeightYards + 0.1), (float)at.D);
                aimLine.SetPosition(i, world);
                aimPath.Add(world);
            }
        }

        /// The putt's predicted roll as one smooth ribbon, break and all: the line a putt hit
        /// with just enough pace to reach the hole would take from here on the current aim, so
        /// the player turns the aim until the ribbon finds the cup and then judges the pace.
        static readonly Color RibbonColor = new(0.55f, 1f, 0.8f);
        void LayPuttRibbon()
        {
            double toPin = ballAt.DistanceTo(hole.Pin) + 0.5;
            double meter = Math.Pow(Math.Min(1, toPin / GolfClub.Putter.ReferenceDistanceYards()), 1 / GolfClub.Putter.MeterExponent());
            var preview = new CourseShot(GolfClub.Putter, new SwingImpact { Power = meter }, heading, ballAt, hole, 1, Wind);
            int samples = Math.Max(2, (int)(preview.Duration / CourseShot.SampleInterval) + 1);
            aimLine.material = HoleView.UnlitMat(RibbonColor);
            aimLine.positionCount = samples;
            aimPath.Clear();
            for (int i = 0; i < samples; i++)
            {
                var p = preview.PositionAt(Math.Min(preview.Duration, i * CourseShot.SampleInterval));
                var world = HoleView.ToWorld(new CoursePoint(p.x, p.d), 0.05);
                aimLine.SetPosition(i, world);
                aimPath.Add(world);
            }
        }

        void PlaceBall(CoursePoint p, double height)
        {
            ball.position = HoleView.ToWorld(p, height + 0.06);
            ballScale = 1f; ballScaleVelocity = 0f; ball.localScale = Vector3.one * BallSize;
        }

        /// The game's ball wears Codex's: his dimpled white sphere and the dark alignment stripe
        /// that makes the spin readable, out of the cinematic file, scaled from his 0.45 m
        /// presentation ball to the game's 0.12 yd one. Without the file it stays a plain sphere.
        void DressBall()
        {
            var prefab = Resources.Load<GameObject>("Course/hole_12_cinematic");
            Transform source = null;
            if (prefab) foreach (var t in prefab.GetComponentsInChildren<Transform>(true)) if (t.name == "BALL") { source = t; break; }
            if (!source) return;
            var model = Instantiate(source.gameObject, ball, false);
            model.name = "Codex ball";
            model.transform.localPosition = Vector3.zero; model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one / (2f * CinematicRig.BallRadius);   // his diameter → the unit sphere's
            foreach (var r in model.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            foreach (var c in model.GetComponentsInChildren<Collider>(true)) Destroy(c);
            if (ball.TryGetComponent(out MeshRenderer plain)) plain.enabled = false;
        }

        /// Spin the ball the way Codex's stripe shows it: a slow, readable backspin about the
        /// flight's axis in the air; on the ground a roll of distance over radius, capped so the
        /// stripe does not strobe.
        void SpinBall(Vector3 pos, Vector3 velocity, bool airborne)
        {
            // size for the lens: a twentieth of the view's height at the ball while airborne,
            // easing back to life size along the ground
            float d = Vector3.Distance(rig.transform.position, pos);
            float viewHeight = 2f * d * Mathf.Tan(rig.Camera.fieldOfView * Mathf.Deg2Rad / 2f);
            float wanted = airborne ? Mathf.Max(1f, 0.05f * viewHeight / BallSize) : 1f;
            ballScale = Mathf.SmoothDamp(ballScale, wanted, ref ballScaleVelocity, airborne ? 0.25f : 0.6f);
            ball.localScale = Vector3.one * (BallSize * ballScale);
            var flat = velocity; flat.y = 0;
            if (flat.sqrMagnitude > 1e-4f)
            {
                var right = Vector3.Cross(Vector3.up, flat.normalized);
                float angle = airborne ? -3f * Mathf.Rad2Deg * Time.deltaTime
                                       : Mathf.Min((pos - lastSpinPos).magnitude / 0.06f, 12f * Time.deltaTime) * Mathf.Rad2Deg;
                ballSpin = Quaternion.AngleAxis(angle, right) * ballSpin;
            }
            lastSpinPos = pos;
            ball.rotation = ballSpin;
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
            landingZone.SetRadius(Mathf.Max(3f, (float)rated * 0.045f));
            landingMarker.position += Vector3.up * 0.04f;
            landingMarker.gameObject.SetActive(!putting);
            golfer.Stand(ball.position, dir);
            hud.SetDistance(putting ? $"{toPin * 3:F0} ft to the hole" : $"{toPin:F0} yd to the pin");
            if (putting)
            {
                // The read in words: rise along the line and which way the ground tips across it.
                var g = hole.Surface.Gradient(ballAt);
                double along = g.dx * dir.x + g.dd * dir.z, across = g.dx * dir.z - g.dd * dir.x;
                string read = Math.Abs(along) < 0.004 && Math.Abs(across) < 0.004 ? "Flat"
                    : $"{(along >= 0 ? "Uphill" : "Downhill")} {Math.Abs(along) * 100:F1}%  ·  {(Math.Abs(across) < 0.004 ? "straight" : across > 0 ? "breaks left" : "breaks right")}";
                hud.SetClub($"Putter  ·  full stroke {rated * 3:F0} ft");
                hud.SetRead(read);
            }
            else
            {
                hud.SetClub($"{club.DisplayName()}  ·  {rated:F0} yd{(lie.PowerFactor() < 1 ? $"  ({lie.Label()})" : "")}");
                hud.SetWind((float)Wind.RelativeTo(heading), Wind.Describe(heading), Wind.IsCalm, Wind.SpeedMPH);
            }
            if (hud.Controller != null)
            {
                var yards = new string[GolfClubs.All.Length];
                for (int i = 0; i < yards.Length; i++)
                {
                    double d = GolfClubs.All[i].ReferenceDistanceYards() * lie.PowerFactor();
                    yards[i] = GolfClubs.All[i] == GolfClub.Putter ? $"{d * 3:F0} ft" : $"{d:F0} yd";
                }
                hud.Controller.SetClubs(Array.IndexOf(GolfClubs.All, club), yards);
                hud.Controller.SetHeading((float)(heading - hole.Tee.HeadingTo(hole.Pin)));
            }
            targetYards = putting ? 0 : ballAt.DistanceTo(FullShotCarry(lie));
            hud.SetScore(Card.Total, Card.ToPar, holeStrokes);
        }

        double targetYards;

        /// The strike, in one colour: pure when it starts on line, flies straight and finds the
        /// short grass; off line when it is pushed or pulled hard, curves away, or ends in the
        /// water or out of bounds; fair in between.
        /// The swing rated 0–100 the way Codex's trail config expects it — the line it started
        /// on and the curve it took, and where it ended up — then banded: 80 and up green, 50–79
        /// yellow, under 50 red.
        static ShotEffects.Quality Judge(CourseShot shot) => ShotEffects.QualityOf(Rate(shot));
        static float Rate(CourseShot shot)
        {
            double off = Math.Abs(shot.StartLine) / 6.0 + Math.Abs(shot.Curve) / 10.0;
            bool lost = shot.Lie == CourseLie.Water || shot.Lie == CourseLie.OutOfBounds;
            bool found = shot.Lie == CourseLie.Fairway || shot.Lie == CourseLie.Green || shot.Lie == CourseLie.Tee || shot.IsHoled;
            double line = 1 - 0.55 * Math.Min(off, 1.4);
            double lie = lost ? 0.3 : found ? 1.0 : 0.75;
            return (float)(100 * Math.Max(0, line) * lie);
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
            SelectClub(GolfClubs.All[(i + step + GolfClubs.All.Length) % GolfClubs.All.Length]);
        }

        void SelectClub(GolfClub chosen)
        {
            if (Current != State.Aim) return;
            Tick();
            club = chosen;
            Swing.SetClub(club);
            golfer.SetClub(club, ballAt.DistanceTo(hole.Pin) < 40);
            UpdateAimVisuals();
        }

        /// The phone-as-controller layout without a big screen, for reviews (and a look at it).
        public void PreviewBigScreen(bool on) => bigScreen.SetPreview(on);

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
            launchGround = HoleView.GroundHeight(ballAt);
            landingGround = HoleView.GroundHeight(LastShot.Touchdown);
            dropSeconds = landingGround < launchGround - 0.3 && LastShot.CarryTime > 0
                ? Math.Sqrt(2 * (launchGround - landingGround) / CourseShot.GravityYards) : 0;
            greenRead.Hide();
            hud.Controller?.SetTarget(null, Vector3.zero, "", false);
            holeCam = false;
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
            Bounces = 0; lastHeight = 0; dropped = false; trailing = false;
            effects.SetQuality(Judge(LastShot));
            flightCamHome = rig.transform.position;
            Enter(State.Flight);
        }

        /// 1 in flight, easing to 0.45 over the last quarter second before the ball lands and
        /// back to 1 half a second after: the first bounce in slow motion.
        static float BallClock(double t, double carry)
        {
            double into = t - carry;
            if (into < -0.35 || into > 0.75) return 1f;
            float edge = into < 0 ? Mathf.SmoothStep(1f, 0.45f, (float)((into + 0.35) / 0.35))
                                  : Mathf.SmoothStep(0.45f, 1f, (float)((into - 0.45) / 0.3));
            return into < 0 ? edge : (into < 0.45 ? 0.45f : edge);
        }

        /// The flight camera for a full shot is Codex's cinematic chase, driven by where the ball
        /// is in its own story: 0→1 through the air, 1→2 along the ground. Putts keep their read.
        Vector3 flightCamHome;
        void FlightCamera(Vector3 pos, double shotTime, double carry)
        {
            double duration = Math.Max(LastShot.Duration, carry + 0.01);
            float p = shotTime <= carry ? (float)(shotTime / Math.Max(carry, 0.01)) : 1f + (float)((shotTime - carry) / (duration - carry));
            var rest = HoleView.ToWorld(LastShot.Rest);
            var dir = rest - flightCamHome; dir.y = 0;
            if (dir.sqrMagnitude < 1f) dir = new Vector3(Mathf.Sin((float)LastShot.Heading * Mathf.Deg2Rad), 0, Mathf.Cos((float)LastShot.Heading * Mathf.Deg2Rad));
            rig.CinematicChase(pos, dir, Mathf.Clamp(p, 0, 2), at => (float)HoleView.GroundHeight(HoleView.ToCourse(at)));
        }

        // ----- Frame loop -----

        void Update()
        {
            stateTime += Time.deltaTime;
            Swing.Update();
            PollControllerButtons();
            UpdateControllerHint();

            // The map's marks: the ball, and while aiming the path and where it comes down.
            bool aimingShot = Current == State.Aim;
            hud.SetMinimapMarks(minimapCamera, ball.position, landingMarker.position, aimingShot ? aimPath : null,
                aimingShot && landingMarker.gameObject.activeSelf, ball.gameObject.activeSelf && Current != State.Menu && Current != State.Golfer);

            switch (Current)
            {
                case State.Menu:
                    // A slow aerial of the hole, round and back, behind the menu.
                    rig.Showcase(hole, Mathf.PingPong(stateTime / 30f, CameraRig.AerialShare * 0.999f));
                    if (menu != null && Time.frameCount % 30 == 0) menu.BigScreenStatus.text = bigScreen.Status;
                    break;

                case State.Golfer:
                    rig.FramePortrait(golfer.transform.position, golfer.transform.forward, stateTime);
                    break;

                case State.Intro:
                    // The showcase: the whole hole from the air, then the walk up to the green —
                    // or the hole's signature shot, where it has one. A tap skips it.
                    if (signature == null) rig.Showcase(hole, Mathf.Clamp01(stateTime / ShowcaseSeconds));
                    else if (stateTime < LeadSeconds) { if (signature.HasOverview) PlayOverview(stateTime); else rig.Showcase(hole, Mathf.Clamp01(stateTime / ShowcaseSeconds)); }
                    else PlaySignature(stateTime - LeadSeconds);
                    // The card rises with the aerial and is gone before the walk reaches the green.
                    hud.SetHoleIntroAlpha(Mathf.Min(Mathf.SmoothStep(0, 1, (stateTime - 0.4f) / 0.8f), Mathf.SmoothStep(0, 1, (IntroSeconds - 1.2f - stateTime) / 0.9f)));
                    // A tap skips — but not the one that pressed Play, which is still down this frame.
                    if (stateTime > IntroSeconds || (stateTime > 0.75f && Input.GetMouseButtonDown(0))) BeginAim(false);
                    break;

                case State.Aim:
                    hud.Controller?.SetTarget(bigScreen.PhoneView, landingMarker.position, $"{targetYards:F0} YDS", landingMarker.gameObject.activeSelf);
                    // Arrows and keys sweep at full rate; the joystick sweeps with how far it is pushed.
                    float sweep = (hud.AimLeftHeld ? -1 : 0) + (hud.AimRightHeld ? 1 : 0)
                                + (Input.GetKey(KeyCode.LeftArrow) ? -1 : 0) + (Input.GetKey(KeyCode.RightArrow) ? 1 : 0)
                                + Mathf.Clamp(hud.AimStick, -1f, 1f) * 1.5f;
                    if (sweep != 0) Nudge(sweep * AimSweepDegreesPerSecond * Time.deltaTime);
                    if (Input.GetKeyDown(KeyCode.UpArrow)) CycleClub(-1);
                    if (Input.GetKeyDown(KeyCode.DownArrow)) CycleClub(1);
                    if (Input.GetKeyDown(KeyCode.G)) { GolferStyle.CycleBody(); RestyleGolfer(); }
                    if (Input.GetKeyDown(KeyCode.T)) { GolferStyle.CycleSkin(); RestyleGolfer(); }
                    if (Swing.Phase == SwingPhase.Downswing && lastPhase != SwingPhase.Downswing) sounds.PlayWhoosh(Swing.Detector.Load);
                    if (Swing.Phase == SwingPhase.Address && lastPhase != SwingPhase.Address) { sounds.PlayReady(); Haptics.Tick(); }
                    if (Swing.Phase == SwingPhase.Backswing || Swing.Phase == SwingPhase.Downswing) { }
                    else if (Swing.Phase == SwingPhase.Address) hud.SetStatus(Swing.UsingPhone ? "Ready — swing!" : "Ready — hold SPACE or the button, release to swing");
                    else if (Swing.UsingPhone && Swing.Detector.WrongEndDown) hud.SetStatus("Flip the phone: top edge toward the ground, like a club");
                    else if (Swing.UsingPhone && !Swing.Detector.PointedDown) hud.SetStatus($"Point the phone down at the ball, like a club  ({Swing.Detector.LeanDegrees:F0}° off)");
                    else hud.SetStatus("Hold the phone still…");
                    break;

                case State.Flight:
                    // The ball's own clock: it runs slow for a beat around the first touchdown
                    // of a full shot — the landing seen the way a replay shows it — and the
                    // cameras keep real time, so the move stays smooth through it.
                    flightTime += Time.deltaTime * (club == GolfClub.Putter || LastShot.CarryTime < 1.6 ? 1f : BallClock(flightTime, LastShot.CarryTime));
                    if (flightTime < 0) break;
                    if (!strikePlayed) { strikePlayed = true; sounds.PlayStrike(club, LastShot.Power); Haptics.Impact(LastShot.Power); }
                    // The flight model's arc is level with the ground it left. Drawn: in the air it
                    // rises to a landing that is higher (the far bank), and stays level over one that
                    // is lower — then the ball drops off the edge to the sea or the low ground at
                    // the carry point, and only then rolls, following the ground it rolls over.
                    double carry = LastShot.CarryTime;
                    double shotTime = flightTime <= carry ? flightTime : Math.Max(carry, flightTime - dropSeconds);
                    var p = LastShot.PositionAt(shotTime);
                    double ground;
                    if (shotTime < carry)
                        ground = landingGround > launchGround ? launchGround + (landingGround - launchGround) * (shotTime / carry) : launchGround;
                    else if (flightTime < carry + dropSeconds)
                    {
                        double falling = flightTime - carry;
                        ground = Math.Max(landingGround, launchGround - 0.5 * CourseShot.GravityYards * falling * falling);
                    }
                    else ground = HoleView.GroundHeight(new CoursePoint(p.x, p.d));
                    var pos = new Vector3((float)p.x, (float)(ground + p.h + 0.06), (float)p.d);
                    if (club != GolfClub.Putter && !trailing) { effects.BeginFlight(); trailing = true; }
                    // Every time the ball meets the ground — the landing and each bounce after
                    // — the turf it hit puffs up; a fall into the sea splashes.
                    bool landed = lastHeight > 0.08 && p.h <= 0.08 && shotTime <= carry + 1e-6 && flightTime > 0.1f;
                    bool fell = dropSeconds > 0 && !dropped && flightTime >= carry + dropSeconds;
                    if (landed || fell)
                    {
                        var lie = hole.LieAt(new CoursePoint(p.x, p.d));
                        float strength = fell ? 1.2f : Mathf.Clamp((float)((lastHeight - p.h) / Mathf.Max(Time.deltaTime, 1e-3f)) / 18f, 0.3f, 1.5f);
                        effects.Touchdown(new Vector3(pos.x, (float)ground + 0.02f, pos.z), lie, strength);
                        if (lie != CourseLie.Water) sounds.PlayThud(strength);
                        Bounces++; if (fell) dropped = true;
                    }
                    lastHeight = p.h;
                    ball.position = pos;
                    var velocity = (pos - lastBallPos) / Mathf.Max(Time.deltaTime, 1e-4f);
                    lastBallPos = pos;
                    SpinBall(pos, velocity, p.h > 0.08);
                    if (club == GolfClub.Putter)
                    {
                        // Watch the putt from where it was read; as it closes on the cup, cut to
                        // behind the hole to watch it arrive (the games' hole cam).
                        var cup = HoleView.ToWorld(hole.Pin);
                        bool closing = Vector3.Dot(cup - pos, velocity) > 0;
                        if (!holeCam && (cup - pos).magnitude <= HoleCamReach && (closing || LastShot.IsHoled)) { holeCam = true; rig.SnapNext(); }
                        if (holeCam) rig.HoleCam(pos, cup);
                    }
                    else FlightCamera(pos, shotTime, carry);
                    if (flightTime >= LastShot.Duration + dropSeconds) FinishShot();
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
                            hud.PlayAgain.Pressed = ShowMenu;
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
            effects.EndFlight();
            ballScale = 1f; ballScaleVelocity = 0f; ball.localScale = Vector3.one * BallSize;
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
            if (!holeCam) rig.HoldOn(ball.position, HoleView.ToWorld(hole.Pin) - ball.position, club == GolfClub.Putter);
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
