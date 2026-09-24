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
        /// (Replay is last so the older states keep their numbers.)
        public enum State { Menu, Golfer, Intro, Aim, Flight, Result, HoleDone, RoundDone, Replay }

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
        /// How the last full swing was struck, and whether its word has popped yet.
        StrikeReport lastReport;
        bool gradeShown;
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
        /// The spectators at the tee, behind the rope.
        Gallery gallery;
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
        /// The introductions on the tee: you, then the gallery cheering (for tests and reviews).
        public bool MeetingThePlayer => Current == State.Intro && meeting && !cheered;
        public bool GalleryCheering => Current == State.Intro && cheered;
        public int GallerySize => gallery ? gallery.Count : 0;
        /// True while the intro is on the designed shot rather than the aerial.
        public bool SignaturePlaying => Current == State.Intro && signaturePlaying;
        /// The ball, for the tests' eyes — Codex's during his shot, the game's otherwise.
        public Vector3 BallPosition => signaturePlaying && cinematic != null ? cinematic.Ball.position : ball.position;
        /// The ball's spin, shown by Codex's stripe: backspin in the air, a roll on the ground.
        Quaternion ballSpin = Quaternion.identity;
        Vector3 lastSpinPos;
        const float BallSize = 0.12f;
        /// The ball's visible part, under `ball`: it spins, and swells in flight (BallLook).
        Transform ballBody;
        BallLook ballLook;
        double lastHeight;
        bool trailing;
        LineRenderer aimLine;
        Transform landingMarker;
        LandingZone landingZone;
        readonly System.Collections.Generic.List<Vector3> aimPath = new();
        AimDots aimDots;
        Camera minimapCamera;
        RenderTexture minimapTexture;

        CoursePoint ballAt;
        double heading;
        GolfClub club;
        int holeStrokes;
        float stateTime;
        double flightTime;
        double launchGround, landingGround;
        /// The shot's line over the ground, launch to landing, and the landing spot: the ball
        /// cam rides the one and settles behind the other.
        Vector3 shotLine, landingSpot;
        /// The drawn ball's height above the flight model's and its rate of climb, so it never
        /// falls faster than gravity off a ledge or a rise.
        double drawnY, drawnVy;
        bool touchedDown;
        /// Flight time at which the ball went into the water, or below 0.
        double splashedAt;
        /// An approach from further out than `PovFromYards` that comes down within
        /// `PovWithinYards` of the pin is watched through Codex's ball POV (CameraRig.BallPov):
        /// just above the ball, the big ball low in the frame and the flag ahead.
        const double PovFromYards = 30, PovWithinYards = 10;
        bool povShot;
        /// The shot from the air: this long after the strike (the ball just off the face) the
        /// camera starts its crane up and back into the aerial (CameraRig.Drone); at DroneHold
        /// the HUD clears to the swing-stat tiles and the yardage riding the ball.
        const float CraneStart = 0.12f, DroneHold = 0.8f;
        bool flightHud;
        SwingImpact lastImpact;
        Vector3 originWorld;
        /// True while the shot in the air (or just finished) is being watched from the ball POV.
        public bool PovShot => povShot && (Current == State.Flight || Current == State.Result);
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

            // Clean edges and clean shadows, whatever quality level the platform defaults to: 4×
            // MSAA for the low-poly silhouettes (cliffs, trees, the green's rim) and soft,
            // high-resolution sun shadows over the distance the camera actually works in, instead
            // of one coarse map stretched over 150 yd that stair-steps across the fairway.
            QualitySettings.antiAliasing = 4;
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
            QualitySettings.shadowProjection = ShadowProjection.StableFit;
            QualitySettings.shadowCascades = 2;
            QualitySettings.shadowCascade2Split = 0.25f;
            QualitySettings.shadowDistance = 90f;

            var light = new GameObject("Sun").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50, -30, 0);
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.75f;
            light.shadowBias = 0.04f; light.shadowNormalBias = 0.3f;
            RenderSettings.ambientLight = new Color(0.55f, 0.6f, 0.65f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.76f, 0.91f, 0.99f);   // the sky just under the horizon
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 320; RenderSettings.fogEndDistance = 1100;   // the islands on the horizon stay in view
            // A clear resort sky, the way Golf Dreams paints it (GolfArcade/SkyGradient): cyan
            // overhead, bright at the horizon, the haze colour below it so the sea's far edge
            // dissolves into the sky instead of banding.
            var sky = Shader.Find("GolfArcade/SkyGradient");
            if (sky)
            {
                RenderSettings.skybox = new Material(sky);
                RenderSettings.skybox.SetColor("_Below", RenderSettings.fogColor);
                var clouds = Resources.Load<Texture2D>("Sky/clouds");
                if (clouds)
                {
                    clouds.wrapModeU = TextureWrapMode.Repeat; clouds.wrapModeV = TextureWrapMode.Clamp;
                    clouds.filterMode = FilterMode.Bilinear;
                    RenderSettings.skybox.SetTexture("_Clouds", clouds);
                }
            }
            else if (RenderSettings.skybox && RenderSettings.skybox.HasProperty("_GroundColor"))
            {
                RenderSettings.skybox = new Material(RenderSettings.skybox);
                RenderSettings.skybox.SetColor("_GroundColor", RenderSettings.fogColor);
            }

            rig = CameraRig.Create();
            hud = Hud.Create();
            greenRead = GreenRead.Create(transform);
            golfer = GolferView.Create(transform);
            gallery = Gallery.Create(transform);
            sounds = GolfSounds.Create(transform);

            ball = new GameObject("Ball").transform;
            ball.SetParent(transform, false);
            ball.localScale = Vector3.one * BallSize;
            ballBody = new GameObject("Ball body").transform;
            ballBody.SetParent(ball, false);
            HoleView.Primitive(PrimitiveType.Sphere, "Plain ball", Color.white, ballBody);
            DressBall();
            ballLook = BallLook.Create(transform, ball, ballBody, rig.Camera, BallSize);
            effects = ShotEffects.Create(transform, ball, rig.Camera, ballLook);

            aimLine = new GameObject("Aim line").AddComponent<LineRenderer>();
            aimLine.transform.SetParent(transform, false);
            aimLine.material = HoleView.UnlitMat(Color.white);
            aimLine.startWidth = aimLine.endWidth = 0.18f;
            aimLine.positionCount = AimLineSamples;
            aimLine.useWorldSpace = true;
            aimDots = AimDots.Create(transform, rig.Camera);

            landingZone = LandingZone.Create(transform);
            landingMarker = landingZone.transform;

            BuildMinimap();

            Swing = new SwingController(ForceSyntheticSwing);
            Swing.OnLoad = OnLoad;
            Swing.OnCancel = OnCancel;
            Swing.OnImpact = OnImpact;
            Swing.OnSourceChanged = _ => { RefreshControls(); Haptics.Release(); sounds.Release(); hud.SetMeter(0); golfer.Settle(); };
            Swing.Start();

            hud.AimLeft.Pressed = () => { Tick(); Nudge(-AimTapDegrees * (club == GolfClub.Putter ? 0.33f : 1f)); };
            hud.AimRight.Pressed = () => { Tick(); Nudge(AimTapDegrees * (club == GolfClub.Putter ? 0.33f : 1f)); };
            hud.ClubUp.Pressed = () => { Tick(); CycleClub(-1); };
            hud.ClubDown.Pressed = () => { Tick(); CycleClub(1); };
            hud.SwingHold.Pressed = () => Swing.Synthetic?.Backswing(true);
            hud.SwingHold.Released = () => Swing.Synthetic?.Backswing(false);
            bigScreen = BigScreen.Create(transform, rig.Camera);
            bigScreen.OnChanged = on =>
            {
                if (on)
                {
                    // live on a screen: the HUD goes with the course; a preview keeps it on the phone
                    hud.EnterControllerLayout(bigScreen.Live ? rig.Camera : null);
                    hud.Controller.OnClub = i => SelectClub(GolfClubs.All[i]);
                    hud.Controller.SetScreen(bigScreen.Live);
                    hud.Controller.Skip.Pressed = () => { if (Current == State.Intro && stateTime > 0.3f) BeginAim(false); };
                    // (from the menu there's no hole yet: StartHole fills it in)
                    if (hole != null && Card != null) { hud.SetHole(hole.Number, hole.Par, hole.Length, hole.Picture, hole.Name); ShowScore(); }
                }
                else hud.LeaveControllerLayout();
                ShowControllerForState();
                SizeMinimap(on ? Hud.ControllerMapSize : Hud.MinimapSize);
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
            var holes = Course.Course.Cliffside().Holes;
            menu = hud.ShowMenu(holes);
            var stamp = Resources.Load<TextAsset>("build_id");
            menu.Build.text = stamp ? $"build {stamp.text.Trim()}" : "";
            menu.Golfer.Pressed = OpenGolferPicker;
            for (int i = 0; i < menu.Holes.Length; i++) { int number = menu.HoleNumbers[i]; menu.Holes[i].Pressed = () => ChooseHoles(number); }
            menu.AllHoles.Pressed = () => ChooseHoles(0);
            menu.AirPlay.Pressed = () => { bigScreen.SetWanted(true); bigScreen.OpenAirPlayPicker(); RefreshMenu(); };
            menu.Play.Pressed = Play;
            RefreshMenu();
            rig.SnapNext();
            Enter(State.Menu);
        }

        /// The round: 0 for both holes, or one hole's number. Remembered on the device.
        public int ChosenHoles => chosenHoles;
        public void ChooseHoles(int holes)
        {
            chosenHoles = holes;
            PlayerPrefs.SetInt("holes", holes); PlayerPrefs.Save();
            ShowMenu(); // the flyover moves to the chosen hole
        }

        void RefreshMenu() => menu?.Refresh(GolferStyle.Summary, chosenHoles, bigScreen.Status);

        // ----- The golfer select screen -----

        GolferSelect select;
        /// The way the golfer faces on the select screen, and how far a drag has turned them.
        Vector3 selectFacing = Vector3.forward;
        float selectSpin;
        bool spinning;

        /// Its own screen (UI/GolferSelect.cs): the golfer stands easy at the tee, face to the
        /// camera, and every choice changes them on the spot.
        public void OpenGolferPicker()
        {
            if (Current != State.Menu) return;
            hud.HideMenu(); menu = null;
            select = hud.ShowGolferSelect(GolferStyle.KitNames, GolferStyle.KitColors, GolferStyle.ShirtNames, GolferStyle.ShirtColors);
            select.Previous.Pressed = () => SwitchGolfer();
            select.Next.Pressed = () => SwitchGolfer();
            for (int i = 0; i < select.Kits.Length; i++) { int kit = i; select.Kits[i].Pressed = () => { Click(); GolferStyle.Kit = kit; golfer.Redress(); RefreshSelect(); }; }
            for (int i = 0; i < select.Shirts.Length; i++) { int shirt = i; select.Shirts[i].Pressed = () => { Click(); GolferStyle.Shirt = shirt; golfer.Redress(); RefreshSelect(); }; }
            select.Spin = dx => { spinning = true; selectSpin -= dx * 0.35f; };
            select.SpinDone = () => spinning = false;
            select.Go.Pressed = CloseGolferPicker;
            selectSpin = 0; spinning = false;
            ballAt = hole.Tee; heading = hole.Tee.HeadingTo(hole.Pin);
            PlaceBall(ballAt, 0);
            ball.gameObject.SetActive(false);   // (no ball to address: the golfer stands easy)
            Enter(State.Golfer);
            RestyleForPicker();
            selectFacing = golfer.transform.forward;
            rig.FramePortrait(golfer.transform.position, selectFacing, 0);
            rig.SnapNext();
        }

        /// Two golfers: either arrow is the other one.
        void SwitchGolfer()
        {
            Click();
            GolferStyle.CycleBody();
            selectSpin = 0;
            RestyleForPicker();
        }

        void RefreshSelect() => select?.Refresh(GolferStyle.Body == GolferStyle.BodyKind.Female, GolferStyle.Kit, GolferStyle.Shirt);

        void Click() { sounds.PlayTick(); Haptics.Tick(); }

        void RestyleForPicker() => RestyleGolfer();

        /// One frame of the select screen: the camera square on, and the golfer turned by the
        /// drag, easing back to face it once let go.
        void UpdateSelect()
        {
            if (!spinning) selectSpin = Mathf.LerpAngle(selectSpin, 0, 1f - Mathf.Exp(-Time.deltaTime * 3f));
            golfer.transform.rotation = Quaternion.LookRotation(selectFacing, Vector3.up) * Quaternion.Euler(0, selectSpin, 0);
            rig.FramePortrait(golfer.transform.position, selectFacing, stateTime);
        }

        public void CloseGolferPicker()
        {
            hud.HideGolferSelect(); select = null;
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

        void OnDestroy() { Haptics.Release(); Swing?.Stop(); Time.timeScale = 1f; }

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
                // standing easy, head up to the camera: at address the face looks down at the ball
                golfer.Perform("Idle");
                RefreshSelect();
            }
        }

        void BuildMinimap()
        {
            var go = new GameObject("Minimap camera");
            go.transform.SetParent(transform, false);
            minimapCamera = go.AddComponent<Camera>();
            minimapCamera.orthographic = true;
            minimapCamera.clearFlags = CameraClearFlags.SolidColor;
            minimapCamera.backgroundColor = new Color(0.2f, 0.4f, 0.15f);
            minimapCamera.cullingMask = ~(1 << BallLook.OverlayLayer);   // the map draws its own marks
            SizeMinimap(Hud.MinimapSize);
        }

        /// The map's picture at the size it's shown: the HUD's tall corner map, or the
        /// controller's wide card (the same hole, with more of the sea either side).
        void SizeMinimap(Vector2 size)
        {
            if (minimapTexture && minimapTexture.width == (int)size.x && minimapTexture.height == (int)size.y) return;
            var old = minimapTexture;
            minimapTexture = new RenderTexture((int)size.x, (int)size.y, 16);
            minimapCamera.targetTexture = minimapTexture;
            hud.Minimap.texture = minimapTexture;
            if (old) { old.Release(); Destroy(old); }
            if (hole != null) FrameMinimap();
        }

        /// The map frames the whole hole, whatever its shape — the fairway's every turn, the
        /// green, the islands it is played over — with the tee at the bottom and the hole running
        /// up it, fitted to the picture's shape (the HUD's tall corner, the controller's wide card).
        void FrameMinimap()
        {
            var tee = HoleView.ToWorld(hole.Tee); var pin = HoleView.ToWorld(hole.Pin);
            var up = pin - tee; up.y = 0; up.Normalize();
            var across = new Vector3(up.z, 0, -up.x);
            var points = new System.Collections.Generic.List<CoursePoint>(hole.Centerline);
            if (hole.Shore != null) points.AddRange(hole.Shore);
            foreach (var islet in hole.Islets) points.AddRange(islet);
            float minA = float.MaxValue, maxA = float.MinValue, minC = float.MaxValue, maxC = float.MinValue;
            foreach (var p in points)
            {
                var v = new Vector3((float)p.X, 0, (float)p.D) - new Vector3(tee.x, 0, tee.z);
                float a = Vector3.Dot(v, up), c = Vector3.Dot(v, across);
                minA = Mathf.Min(minA, a); maxA = Mathf.Max(maxA, a); minC = Mathf.Min(minC, c); maxC = Mathf.Max(maxC, c);
            }
            float pad = (float)hole.GreenRadius;
            minA -= pad; maxA += pad; minC -= pad; maxC += pad;
            float aspect = (float)minimapTexture.width / minimapTexture.height;
            var mid = new Vector3(tee.x, 0, tee.z) + up * ((minA + maxA) / 2) + across * ((minC + maxC) / 2);
            minimapCamera.transform.position = mid + Vector3.up * 300;
            minimapCamera.transform.rotation = Quaternion.LookRotation(Vector3.down, up);
            minimapCamera.farClipPlane = 600;
            minimapCamera.orthographicSize = Mathf.Max((maxA - minA) / 2, (maxC - minC) / 2 / aspect) * 1.04f;
            minimapCamera.aspect = aspect;
        }

        // ----- Hole flow -----

        void StartRound()
        {
            course = CourseFor(chosenHoles);
            Card = new Scorecard(course);
            longestDrive = longestShot = 0; perfectStrikes = putts = 0;
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
            hud.SetHole(hole.Number, hole.Par, hole.Length, hole.Picture, hole.Name);
            double downTheHole = hole.Tee.HeadingTo(hole.Pin);
            hud.SetWind((float)Wind.RelativeTo(downTheHole), Wind.Describe(downTheHole), Wind.IsCalm, Wind.SpeedMPH);
            ShowScore();
            FrameMinimap();
            PlaceBall(ballAt, 0);
            ball.gameObject.SetActive(true);
            golfer.SetVisible(false);
            hud.SetStatus("");
            hud.SetTempo("");
            // The showcase: HUD away, the tournament's title over the flyover; then the
            // introductions on the tee, you and the gallery behind the rope.
            hud.ShowPlayHud(false);
            hud.ShowHoleIntro(Tournament, hole.Number, hole.Name, hole.Par, hole.Length,
                Wind.IsCalm ? "Calm today" : $"Wind   ·   {Wind.Describe(downTheHole)}");
            var teeLine = TeeAim();
            golfer.Stand(ball.position, teeLine);
            gallery.gameObject.SetActive(true);
            gallery.Place(hole, golfer.transform.position, teeLine, hole.Number * 31 + holeIndex);
            meeting = cheered = false;
            signature = SignatureShot.Load(hole.Number, holeView);
            cinematic?.Destroy();
            cinematic = signature != null ? CinematicRig.Load(hole.Number, holeView) : null;
            signatureLanding = 0; signaturePlaying = overviewPlaying = false;
            rig.RestoreFov();
            rig.SnapNext();
            Enter(State.Intro);
            RefreshControls();
        }

        void BeginAim(bool keepHeading)
        {
            if (Current == State.Intro)
            {
                EndShowcase();
                hud.HideNameplate(); hud.ShowPlayHud(true);
                holeView.ShowTeeMarkers(true);
            }
            var lie = hole.LieAt(ballAt);
            bool putting = lie.IsPuttingSurface();
            // the gallery is at the tee: once you've left it, they've nothing to draw
            gallery.gameObject.SetActive(ballAt.DistanceTo(hole.Tee) < 20);
            if (!keepHeading || !aimedByPlayer)
            {
                heading = ballAt.HeadingTo(putting ? hole.Pin : hole.RecommendedTarget(ballAt));
                aimedByPlayer = false;
            }
            // the club for where this shot is going: the pin, or on a long hole the next landing
            club = AutoClub(lie, putting ? ballAt.DistanceTo(hole.Pin) : PlaysLike(ballAt, hole.RecommendedTarget(ballAt)));
            Swing.SetClub(club);
            golfer.SetClub(club, ballAt.DistanceTo(hole.Pin) < 40);
            Swing.Armed = true;
            golfer.SetVisible(true);
            golfer.Settle();
            effects.ClearTracer();
            rig.RestoreFov();
            hud.FlightMode(false); hud.HideShotStats(); hud.SetBallTag(null, default, null); flightHud = false;
            hud.Map.Trace.Clear(); hud.Map.Changed();
            hud.SetMeter(0);
            UpdateAimVisuals();
            if (Current == State.Intro) rig.SnapNext(); // cut from the flyover, don't glide the length of the hole
            rig.ResetZoom();
            if (putting) rig.FrameGreen(ball.position, AimDirection(), (float)ballAt.DistanceTo(hole.Pin));
            else rig.FrameAddress(ball.position, AimDirection(), putting);
            // On the green the read goes down; the flag stays in on a long putt, so the hole can be
            // found from across the green, and comes out inside six yards.
            holeView.ShowFlag(!putting || ballAt.DistanceTo(hole.Pin) > 6);
            if (putting) greenRead.Show(hole); else greenRead.Hide();
            Enter(State.Aim);
            RefreshControls();   // after Enter: the aim buttons and the joystick show only while aiming
            hud.HideHoleIntro();  // a skip from the flyover leaves the title up otherwise
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

        /// For tests and reviews: aim at `target` and strike a square swing with just the power
        /// that brings the ball down nearest it, as if the player had swung it.
        public void StrikeToward(CoursePoint target)
        {
            if (Current != State.Aim) return;
            PlanStrike(target, out heading, out double best);
            aimedByPlayer = true;
            UpdateAimVisuals();
            OnImpact(new SwingImpact { Power = best, Backswing = 0.8, Commit = 1.2, TempoSeconds = 1.0, DownswingSeconds = 0.25 });
        }

        /// The aim and the power that bring a full shot down on `target` in today's wind; the
        /// shot that makes.
        CourseShot PlanStrike(CoursePoint target, out double aim, out double power)
        {
            aim = ballAt.HeadingTo(target);
            var lie = hole.LieAt(ballAt);
            power = 1;
            CourseShot shot = null;
            for (int pass = 0; pass < 2; pass++)
            {
                double miss = double.MaxValue;
                CoursePoint landed = target;
                for (double p = 0.2; p <= 1.0001; p += 0.01)
                {
                    var trial = new CourseShot(club, new SwingImpact { Power = p }, aim, ballAt, hole, 1, Wind);
                    double m = trial.Landing.DistanceTo(target);
                    if (m < miss) { miss = m; power = p; landed = trial.Landing; shot = trial; }
                }
                // aim off the wind's drift and go again
                if (pass == 0) aim += ballAt.HeadingTo(target) - ballAt.HeadingTo(landed);
            }
            return shot;
        }

        // ----- The demo reel (DemoVideoTests): a hole played by script, for a recording -----

        /// Phone prompts ("Ready — swing!") and no desktop pairing line along the bottom.
        public bool Demo { get; set; }

        /// The backswing as the phone reports it: the meter, its yardage, the golfer winding up.
        public void ShowBackswing(double load) => OnLoad(load);

        /// Somewhere to put the tee shot so it finishes on the green between `nearest` and
        /// `farthest` yards from the pin, coming down at least `landsFrom` yards from it (so the
        /// shot is watched from the air rather than from the ball); null if nowhere will do.
        public CoursePoint? GreenTarget(double nearest, double farthest, double landsFrom)
        {
            if (Current != State.Aim) return null;
            double toPin = ballAt.DistanceTo(hole.Pin), line = ballAt.HeadingTo(hole.Pin) * Math.PI / 180;
            CoursePoint? best = null; double bestScore = double.MaxValue;
            for (double back = 4; back <= 26; back += 2)
                for (double side = -6; side <= 6; side += 3)
                {
                    double d = toPin - back;
                    var target = new CoursePoint(ballAt.X + Math.Sin(line) * d + Math.Cos(line) * side, ballAt.D + Math.Cos(line) * d - Math.Sin(line) * side);
                    var shot = PlanStrike(target, out _, out _);
                    if (shot == null || shot.Lie != CourseLie.Green || shot.IsHoled) continue;
                    double rest = shot.Rest.DistanceTo(hole.Pin);
                    if (rest < nearest || rest > farthest || shot.Landing.DistanceTo(hole.Pin) < landsFrom) continue;
                    double score = Math.Abs(rest - (nearest + farthest) / 2);
                    if (score < bestScore) { bestScore = score; best = target; }
                }
            return best;
        }

        /// On the green: the putt that drops — the line (read off the break) and the pace that dies
        /// into the cup — struck. If none drops from here, the one that finishes nearest. True if
        /// it goes in.
        public bool StrikeHolingPutt()
        {
            if (Current != State.Aim || club != GolfClub.Putter) return false;
            double straight = ballAt.HeadingTo(hole.Pin);
            double bestAim = straight, bestPower = 0.5, nearest = double.MaxValue;
            bool holed = false;
            for (double off = -12; off <= 12.001 && !holed; off += 0.25)
            {
                // the powers that drop on this line, softest first; the middle of that run dies in
                double first = -1, last = -1;
                for (double p = 0.02; p <= 1.0001; p += 0.005)
                {
                    var trial = new CourseShot(GolfClub.Putter, new SwingImpact { Power = p }, straight + off, ballAt, hole, 1, Wind);
                    if (trial.IsHoled) { if (first < 0) first = p; last = p; }
                    else if (first >= 0) break;
                    else
                    {
                        double miss = trial.Rest.DistanceTo(hole.Pin);
                        if (miss < nearest) { nearest = miss; bestAim = straight + off; bestPower = p; }
                    }
                }
                if (first >= 0) { holed = true; bestAim = straight + off; bestPower = first + (last - first) * 0.3; }
            }
            heading = bestAim; aimedByPlayer = true;
            UpdateAimVisuals();
            OnImpact(new SwingImpact { Power = bestPower, Backswing = Math.Min(1, bestPower * 1.4), Commit = 1.2, TempoSeconds = 1.0, DownswingSeconds = 0.25 });
            return holed;
        }

        /// The shortest club in the bag that gets there from this lie (a putter on the green);
        /// the player can still step up or down the bag.
        /// The yards a shot plays: the distance, and a yard more for every yard the ground climbs
        /// (the flight comes down on a rise early; falling ground gives nothing back).
        static double PlaysLike(CoursePoint from, CoursePoint to)
            => from.DistanceTo(to) + Math.Max(0, HoleView.GroundHeight(to) - HoleView.GroundHeight(from));

        static GolfClub AutoClub(CourseLie lie, double yards)
        {
            if (lie.IsPuttingSurface()) return GolfClub.Putter;
            return GolfClubs.ForDistance(yards, c => lie.PowerFactor(c));
        }

        void Enter(State s)
        {
            Current = s; stateTime = 0;
            ShowControllerForState();
            // the ball is drawn to be seen for a full shot and the settle after it; true size otherwise
            ballLook.Readable((s == State.Flight || s == State.Result || s == State.Replay) && club != GolfClub.Putter);
            if (s != State.Aim) hud.SetFace(null);
        }

        /// The controller sheet is up only while a round is on — the menu, the golfer picker and
        /// the round's scorecard stay on the phone — and while the opening plays on the TV it
        /// offers to skip it; the round itself starts at the tee when the opening ends.
        void ShowControllerForState()
        {
            if (hud.Controller == null) return;
            bool round = Current is not (State.Menu or State.Golfer or State.RoundDone);
            hud.Controller.SetShown(round);
            hud.Controller.SetIntro(Current == State.Intro);
            // live on a big screen, the HUD goes there for the round; the menu, the picker and the
            // round's card stay on the phone, where they can be touched
            hud.HudOnTv(round);
        }

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
            if (Demo) { hud.SetControllerHint(""); return; }
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
                case State.Result: case State.HoleDone: case State.Replay: phase = 4; load = 0; break;
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
        /// The tournament title is up over the opening aerial only.
        float TitleSeconds => Mathf.Min(IntroSeconds - 1.2f, Mathf.Max(3.5f, LeadSeconds - 0.2f));
        bool overviewPlaying;

        /// The introductions on the tee after the flyover, the way the tennis broadcast opens:
        /// first you, close up on the tee, waving to the camera with your nameplate up and the
        /// gallery behind the rope; then the gallery, cheering you on as the camera turns to them.
        const float YouSeconds = 2.8f, GallerySeconds = 1.9f;
        float MeetSeconds => YouSeconds + (gallery.Count > 0 ? GallerySeconds : 0f);
        bool meeting, cheered;

        void MeetThePlayer(float t)
        {
            if (!meeting)
            {
                meeting = true;
                EndShowcase();
                hud.HideHoleIntro();
                var teeLine = TeeAim();
                golfer.SetClub(GolfClub.Driver, false);
                golfer.Stand(ball.position, teeLine);
                golfer.SetVisible(true);
                golfer.Perform("Wave");
                hud.ShowNameplate("YOU", $"On the tee   ·   Hole {hole.Number}");
                rig.SnapNext();
            }
            var feet = golfer.transform.position;
            var facing = golfer.transform.forward; facing.y = 0; facing.Normalize();
            if (t < YouSeconds || gallery.Count == 0)
            {
                // full length, from in front and a little to the side, drifting slowly round;
                // the gallery behind the rope over the golfer's shoulders
                float turn = Mathf.Lerp(-16f, 6f, Mathf.SmoothStep(0, 1, t / YouSeconds));
                var from = Quaternion.AngleAxis(turn, Vector3.up) * facing;
                rig.Cue(feet + from * 4.6f + Vector3.up * 1.3f, feet + Vector3.up * 0.95f);
                hud.SetNameplateAlpha(Mathf.Min(Mathf.Clamp01((t - 0.25f) / 0.4f), Mathf.Clamp01((YouSeconds - 0.15f - t) / 0.3f)));
                return;
            }
            if (!cheered)
            {
                cheered = true;
                hud.HideNameplate();
                holeView.ShowTeeMarkers(false);                     // they'd stand in front of the lens
                gallery.Cheer();
                sounds.PlayApplause();
                golfer.Perform("FistPump");
                rig.SnapNext();
            }
            // the broadcast's crowd shot: from the tee in front of them, a little down the line,
            // at head height, easing in as they cheer
            float u = Mathf.SmoothStep(0, 1, (t - YouSeconds) / GallerySeconds);
            var toFans = gallery.Centre - feet; toFans.y = 0; toFans.Normalize();
            var along = Vector3.Cross(Vector3.up, toFans);
            if (Vector3.Dot(along, TeeAim()) < 0) along = -along;          // down the hole's side
            var eye = feet + toFans * Mathf.Lerp(1.2f, 2.0f, u) + along * Mathf.Lerp(2.2f, 1.6f, u) + Vector3.up * 1.75f;
            rig.Cue(eye, gallery.Centre + Vector3.up * 1.05f);
        }

        /// The aim off the tee before the player has touched it: at the hole's own target.
        Vector3 TeeAim()
        {
            double h = hole.Tee.HeadingTo(hole.RecommendedTarget(hole.Tee)) * Math.PI / 180;
            return new Vector3((float)Math.Sin(h), 0, (float)Math.Cos(h));
        }

        /// The flyover's things put away: the lens, Codex's cinematic ball, and the game's ball
        /// back on the tee.
        void EndShowcase()
        {
            if (!signaturePlaying && !overviewPlaying) return;
            rig.RestoreFov(); signaturePlaying = overviewPlaying = false;
            cinematic?.Show(false);
            effects.ClearTracer();
            ball.gameObject.SetActive(true); PlaceBall(ballAt, 0);
        }

        /// The tournament: the course's name, as an Open.
        string Tournament => $"{course.Name} Open";

        /// The scoreboard: every hole of the round against its par, the one being played live.
        void ShowScore()
        {
            var pars = new int[course.Holes.Length];
            var strokes = new int?[pars.Length];
            for (int i = 0; i < pars.Length; i++) { pars[i] = course.Holes[i].Par; strokes[i] = Card.StrokesOn(i); }
            strokes[holeIndex] ??= holeStrokes;
            hud.SetScoreboard($"{Tournament}   ·   Hole {hole.Number}", pars, strokes, holeIndex);
            hud.SetScore(Card.Total, Card.ToPar, holeStrokes);
        }

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
            var launch = lie.LaunchFrom(club, 1, 0, 0);
            launch.WindMPH = Wind.SpeedMPH;
            launch.WindDegrees = Wind.RelativeTo(heading);
            var flight = BallFlight.Simulate(launch);
            double sinH = Math.Sin(heading * Math.PI / 180), cosH = Math.Cos(heading * Math.PI / 180);
            double ground = HoleView.GroundHeight(ballAt);
            const double step = 0.12;
            int samples = Math.Max(2, (int)(flight.RollStartTime / step) + 2);
            aimLine.positionCount = 0;   // full shots are the Wii's dots, not a line
            aimPath.Clear();
            for (int i = 0; i < samples; i++)
            {
                var p = flight.PositionAt(Math.Min(flight.RollStartTime, i * step));
                var at = new CoursePoint(ballAt.X + p.LateralYards * cosH + p.DistanceYards * sinH, ballAt.D - p.LateralYards * sinH + p.DistanceYards * cosH);
                double under = HoleView.GroundHeight(at);
                var world = new Vector3((float)at.X, (float)(Math.Max(ground, under) + p.HeightYards + 0.1), (float)at.D);
                aimPath.Add(world);
            }
            aimDots.Lay(aimPath);
            PlanShotOnMap(lie);
        }

        /// The minimap's picture of the shot before it is hit, Wii Golf style: the dots to the
        /// landing, the zone around it that a slightly-off swing still finds (the same ellipse as
        /// the ring on the course), and the club's reach — an arc as far as a full swing with it
        /// carries, whichever way you turn.
        void PlanShotOnMap(CourseLie lie)
        {
            var plan = hud.Map;
            plan.Path.Clear(); plan.Path.AddRange(aimPath);
            plan.Reach.Clear();
            ShotZone(lie, out var centre, out float across, out float along);
            plan.ShowZone = true;
            plan.ZoneCentre = new Vector3((float)centre.X, 0, (float)centre.D);
            plan.ZoneAcross = across; plan.ZoneAlong = along; plan.ZoneHeading = (float)heading;
            double reach = ballAt.DistanceTo(centre);
            for (int i = 0; i <= 16; i++)
            {
                double a = (heading + (i / 16.0 - 0.5) * 70) * Math.PI / 180;
                plan.Reach.Add(new Vector3((float)(ballAt.X + Math.Sin(a) * reach), 0, (float)(ballAt.D + Math.Cos(a) * reach)));
            }
            // the meter's checkpoints and their targets on the course and the map: short of the
            // pin, on it, past it
            checkpointSpots.Clear(); plan.Targets.Clear();
            int onPin = PinTargets(lie, checkpointPowers, out var yards);
            var labels = new string[checkpointPowers.Length];
            for (int i = 0; i < checkpointPowers.Length; i++)
            {
                var spot = LandingFor(checkpointPowers[i], lie);
                checkpointSpots.Add(HoleView.ToWorld(spot, 0.05));
                plan.Targets.Add(new Vector3((float)spot.X, 0, (float)spot.D));
                labels[i] = i == onPin ? $"PIN {yards[i]:F0}" : $"{yards[i]:F0} yd";
            }
            hud.SetCheckpoints(checkpointPowers, labels, onPin);
            plan.Changed();
            landingZone.SetZone(across, along, (float)heading);
        }

        /// Where a full swing with this club comes down in today's wind, and how far round that
        /// a slightly-off one still lands: pushed or pulled a couple of degrees with the curve
        /// that goes with it (across the line), an eighth less speed (short of it).
        void ShotZone(CourseLie lie, out CoursePoint centre, out float across, out float along)
        {
            BallFlight.Launch L(double power, double start, double curve)
            {
                var l = lie.LaunchFrom(club, power, start, curve);
                l.WindMPH = Wind.SpeedMPH; l.WindDegrees = Wind.RelativeTo(heading);
                return l;
            }
            var square = BallFlight.Simulate(L(1, 0, 0)).CarryPoint;
            var push = BallFlight.Simulate(L(1, 3, 5)).CarryPoint;
            var pull = BallFlight.Simulate(L(1, -3, -5)).CarryPoint;
            var soft = BallFlight.Simulate(L(0.88, 0, 0)).CarryPoint;
            across = Mathf.Max(3f, (float)Math.Max(Math.Abs(push.LateralYards - square.LateralYards), Math.Abs(pull.LateralYards - square.LateralYards)));
            along = Mathf.Max(3f, (float)Math.Abs(square.DistanceYards - soft.DistanceYards));
            centre = FullShotCarry(lie);
        }

        /// The power meter's three checkpoints, as powers (the meter reads power: empty to the
        /// club's full distance), each with a numbered target where that power comes down: one
        /// short of the pin, one on it, one past it (PinTargets).
        readonly float[] checkpointPowers = new float[3];
        readonly System.Collections.Generic.List<Vector3> checkpointSpots = new();

        /// The three targets around the pin, in `powers` (and their carries in `yards`): the pin's
        /// own distance with one a tenth of it (5 to 20 yd) short and one as far past, each at the
        /// power that carries there on this aim in today's wind. Past the club's reach the long
        /// one stops at a full swing; with the pin out of reach altogether all three stand short
        /// of it, the last at a full swing. Returns which one is the pin's, or -1.
        int PinTargets(CourseLie lie, float[] powers, out double[] yards)
        {
            double pin = ballAt.DistanceTo(hole.Pin);
            double gap = Math.Clamp(pin * 0.1, 5, 20);
            double reach = CarryFor(1, lie);
            int onPin = 1;
            yards = new[] { pin - gap, pin, pin + gap };
            if (pin > reach - 2) { yards = new[] { reach - 2 * gap, reach - gap, reach }; onPin = -1; }
            else if (pin + gap > reach) yards[2] = reach;
            for (int i = 0; i < powers.Length; i++)
            {
                yards[i] = Math.Max(yards[i], 3);
                powers[i] = (float)PowerFor(yards[i], lie);
            }
            return onPin;
        }

        /// How far this power carries on the aim line in today's wind, yards.
        double CarryFor(double power, CourseLie lie) => BallFlight.Simulate(TodaysLaunch(power, lie)).CarryPoint.DistanceYards;

        /// The power that carries `yards` on the aim line in today's wind (carry only grows with
        /// power, so a halving search finds it).
        double PowerFor(double yards, CourseLie lie)
        {
            double lo = 0, hi = 1;
            if (CarryFor(hi, lie) <= yards) return 1;
            for (int i = 0; i < 16; i++)
            {
                double mid = (lo + hi) / 2;
                if (CarryFor(mid, lie) < yards) lo = mid; else hi = mid;
            }
            return (lo + hi) / 2;
        }

        BallFlight.Launch TodaysLaunch(double power, CourseLie lie)
        {
            var launch = lie.LaunchFrom(club, Math.Clamp(power, 0, 1), 0, 0);
            launch.WindMPH = Wind.SpeedMPH;
            launch.WindDegrees = Wind.RelativeTo(heading);
            return launch;
        }

        /// Where a shot of this power (the meter's reading) comes down in today's wind, square on
        /// the aim.
        CoursePoint LandingFor(double power, CourseLie lie)
        {
            var carry = BallFlight.Simulate(TodaysLaunch(power, lie)).CarryPoint;
            double cosH = Math.Cos(heading * Math.PI / 180), sinH = Math.Sin(heading * Math.PI / 180);
            return new CoursePoint(ballAt.X + carry.LateralYards * cosH + carry.DistanceYards * sinH,
                                   ballAt.D - carry.LateralYards * sinH + carry.DistanceYards * cosH);
        }

        /// The putt's predicted roll as one smooth ribbon, break and all: the line a putt hit
        /// with just enough pace to reach the hole would take from here on the current aim, so
        /// the player turns the aim until the ribbon finds the cup and then judges the pace.
        static readonly Color RibbonColor = new(0.55f, 1f, 0.8f);
        bool puttRibbonPending;
        float nextPuttRibbon;
        /// The ribbon is a whole putt simulated over the green's slopes: while the aim sweeps it is
        /// redrawn at most 15 times a second, and once more where the sweep stops.
        void LayPuttRibbon()
        {
            if (Time.unscaledTime < nextPuttRibbon) { puttRibbonPending = true; return; }
            puttRibbonPending = false;
            nextPuttRibbon = Time.unscaledTime + 1f / 15f;
            double toPin = ballAt.DistanceTo(hole.Pin) + 0.5;
            double meter = Math.Pow(Math.Min(1, toPin / GolfClub.Putter.ReferenceDistanceYards()), 1 / GolfClub.Putter.MeterExponent());
            var preview = new CourseShot(GolfClub.Putter, new SwingImpact { Power = meter }, heading, ballAt, hole, 1, Wind);
            int samples = Math.Max(2, (int)(preview.Duration / CourseShot.SampleInterval) + 1);
            aimDots.Hide();
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
            var plan = hud.Map;
            plan.Path.Clear(); plan.Path.AddRange(aimPath);
            plan.Reach.Clear(); plan.ShowZone = false; plan.Targets.Clear();
            checkpointSpots.Clear();
            hud.SetCheckpoints(null, null);
            plan.Changed();
        }

        void PlaceBall(CoursePoint p, double height)
        {
            ball.position = HoleView.ToWorld(p, height + 0.06);
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
            var model = Instantiate(source.gameObject, ballBody, false);
            model.name = "Codex ball";
            model.transform.localPosition = Vector3.zero; model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one / (2f * CinematicRig.BallRadius);   // his diameter → the unit sphere's
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                // the white cover gets its dimples (GolfArcade/GolfBall); the stripe stays as it is
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i] && mats[i].name.Contains("dimpled")) mats[i] = DimpledBall() ?? mats[i];
                r.sharedMaterials = mats;
            }
            foreach (var c in model.GetComponentsInChildren<Collider>(true)) Destroy(c);
            var plain = ballBody.Find("Plain ball");
            if (plain) plain.GetComponent<MeshRenderer>().enabled = false;
        }

        static Material dimpled;
        /// The ball's cover: white, glossy, dimpled.
        static Material DimpledBall()
        {
            if (dimpled) return dimpled;
            var shader = Shader.Find("GolfArcade/GolfBall");
            var map = Resources.Load<Texture2D>("Effects/ball_dimples_normal");
            if (!shader || !map) return null;
            map.wrapMode = TextureWrapMode.Repeat;
            dimpled = new Material(shader) { color = new Color(0.97f, 0.97f, 0.96f), name = "Ball (dimpled)" };
            dimpled.SetTexture("_Dimples", map);
            dimpled.SetTextureScale("_Dimples", new Vector2(8, 4));   // round the sphere's UVs: squarish dimples
            return dimpled;
        }

        /// Spin the ball the way Codex's stripe shows it: a slow, readable backspin about the
        /// flight's axis in the air; on the ground a roll of distance over radius, capped so the
        /// stripe does not strobe.
        void SpinBall(Vector3 pos, Vector3 velocity, bool airborne)
        {
            var flat = velocity; flat.y = 0;
            if (flat.sqrMagnitude > 1e-4f)
            {
                var right = Vector3.Cross(Vector3.up, flat.normalized);
                float angle = airborne ? -3f * Mathf.Rad2Deg * Time.deltaTime
                                       : Mathf.Min((pos - lastSpinPos).magnitude / 0.06f, 12f * Time.deltaTime) * Mathf.Rad2Deg;
                ballSpin = Quaternion.AngleAxis(angle, right) * ballSpin;
            }
            lastSpinPos = pos;
            ballBody.localRotation = ballSpin;
        }

        void UpdateAimVisuals()
        {
            var lie = hole.LieAt(ballAt);
            bool putting = lie.IsPuttingSurface();
            double toPin = ballAt.DistanceTo(hole.Pin);
            double rated = club.ReferenceDistanceYards() * lie.PowerFactor(club);
            var dir = AimDirection();
            var from = HoleView.ToWorld(ballAt, 0.03);
            LayAimLine(Math.Min(rated, putting ? toPin + 3 : rated));
            landingMarker.position = putting ? from : HoleView.ToWorld(FullShotCarry(lie), 0.01);
            landingMarker.position += Vector3.up * 0.04f;
            landingMarker.gameObject.SetActive(!putting);
            golfer.Stand(ball.position, dir);
            if (putting) hud.SetDistance(toPin * 3, "FT", "to the hole"); else hud.SetDistance(toPin, "YD", "to the pin");
            if (putting)
            {
                // The read: rise along the line and which way the ground tips across it.
                var g = hole.Surface.Gradient(ballAt);
                double along = g.dx * dir.x + g.dd * dir.z, across = g.dx * dir.z - g.dd * dir.x;
                hud.SetClub($"Putter  ·  {rated * 3:F0} ft", putter: true);
                hud.SetCardRow(0, "elevation", "Slope", Math.Abs(along) < 0.004 ? "Flat" : $"{(along >= 0 ? "Up" : "Down")} {Math.Abs(along) * 100:F1}%");
                hud.SetCardRow(1, "target", "Break", Math.Abs(across) < 0.004 ? "Straight" : across > 0 ? "Left" : "Right");
            }
            else
            {
                // what it plays: a green above the ball wants a yard more for every yard it climbs
                double rise = HoleView.GroundHeight(hole.Pin) - HoleView.GroundHeight(ballAt);
                string note = rise >= 1.5 ? $"+{rise:F0}" : null;
                hud.SetClub($"{club.DisplayName()}  ·  {rated:F0}");
                hud.SetCardRow(0, "elevation", "Plays", $"{toPin + Math.Max(0, rise):F0}", note);
                hud.SetWind((float)Wind.RelativeTo(heading), Wind.Describe(heading), Wind.IsCalm, Wind.SpeedMPH);
            }
            hud.SetCardRow(2, "grass", "Lie", lie == CourseLie.Rough ? "Rough (flyer)" : lie.Label());
            if (hud.Controller != null)
            {
                var yards = new string[GolfClubs.All.Length];
                for (int i = 0; i < yards.Length; i++)
                {
                    double d = GolfClubs.All[i].ReferenceDistanceYards() * lie.PowerFactor(GolfClubs.All[i]);
                    yards[i] = GolfClubs.All[i] == GolfClub.Putter ? $"{d * 3:F0} ft" : $"{d:F0} yd";
                }
                hud.Controller.SetClubs(Array.IndexOf(GolfClubs.All, club), yards);
                // straight is along this hole's line from here: round a bend, not across it
                hud.Controller.SetHeading((float)(heading - ballAt.HeadingTo(putting ? hole.Pin : hole.RecommendedTarget(ballAt))));
            }
            targetYards = putting ? 0 : ballAt.DistanceTo(FullShotCarry(lie));
            ShowScore();
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
        CoursePoint FullShotCarry(CourseLie lie) => LandingFor(1, lie);

        /// Where the line points, degrees (for tests).
        public double AimHeading => heading;
        /// The hole being played (for tests).
        public Hole CurrentHole => hole;

        void Nudge(double degrees)
        {
            if (Current != State.Aim) return;
            heading += degrees;
            aimedByPlayer = true;
            UpdateAimVisuals();
            // on the green the camera keeps the putt's view (the green behind the ball, the cup ahead)
            if (club == GolfClub.Putter) rig.FrameGreen(ball.position, AimDirection(), (float)ballAt.DistanceTo(hole.Pin));
            else rig.FrameAddress(ball.position, AimDirection(), false);
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

        /// This hole's wind, set instead of drawn at random (for reviews and tests that need a
        /// scripted shot to land where it's meant to).
        public void SetWind(Wind wind)
        {
            Wind = wind;
            if (!Wind.IsCalm) holeView.SetFlagWind(Wind.DirectionDegrees);
            double downTheHole = hole.Tee.HeadingTo(hole.Pin);
            hud.SetWind((float)Wind.RelativeTo(downTheHole), Wind.Describe(downTheHole), Wind.IsCalm, Wind.SpeedMPH);
        }

        /// The phone-as-controller layout without a big screen, for reviews (and a look at it).
        public void PreviewBigScreen(bool on) => bigScreen.SetPreview(on);

        /// For reviews: lay the HUD out as it is on a live big screen (the course camera standing in
        /// for the TV's), with the controller on the phone's own canvas; false puts it back.
        public void ReviewTvHud(bool on)
        {
            hud.LeaveControllerLayout();
            if (on)
            {
                hud.EnterControllerLayout(rig.Camera);
                hud.Controller.OnClub = i => SelectClub(GolfClubs.All[i]);
                if (hole != null) hud.SetHole(hole.Number, hole.Par, hole.Length, hole.Picture, hole.Name);
                ShowScore();
                ShowControllerForState();
            }
            SizeMinimap(on ? Hud.ControllerMapSize : Hud.MinimapSize);
            RefreshControls();
            if (Current == State.Aim) UpdateAimVisuals();
        }

        // ----- Swing events -----

        void OnLoad(double load)
        {
            if (Current != State.Aim) return;
            if (club != GolfClub.Putter)
            {
                // the amber dot slides out over the ground to where the meter's reading comes
                // down, the meter says how far, and the checkpoints it has passed light up
                var spot = LandingFor(load, hole.LieAt(ballAt));
                aimDots.MarkAt(HoleView.ToWorld(spot, 0.05));
                hud.Map.ShowLoad = aimDots.MarkShown; hud.Map.Load = aimDots.MarkPosition;
                hud.SetMeter((float)load, null, $"{ballAt.DistanceTo(spot):F0} yd");
            }
            else
            {
                // the meter says how far this stroke rolls on the flat
                double feet = GolfClub.Putter.DistanceYards(load) * 3;
                hud.SetMeter((float)load, null, $"{feet:F0} ft");
            }
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
            aimDots.MarkAt(null);
            hud.Map.ShowLoad = false;
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
            LastShot = new CourseShot(club, impact, heading, ballAt, hole, 1, Wind);
            launchGround = HoleView.GroundHeight(ballAt);
            landingGround = HoleView.GroundHeight(LastShot.Landing);
            landingSpot = HoleView.ToWorld(LastShot.Landing);
            shotLine = landingSpot - HoleView.ToWorld(ballAt); shotLine.y = 0;
            if (shotLine.sqrMagnitude < 1f) shotLine = AimDirection();
            drawnY = launchGround; drawnVy = 0;
            touchedDown = false; splashedAt = -1;
            lastImpact = impact; originWorld = HoleView.ToWorld(ballAt); flightHud = false;
            lastReport = Strikes.Judge(impact); gradeShown = false;
            CountForTheCard();
            replayPath.Clear();
            hud.SetFace(null);
            povShot = club != GolfClub.Putter && ballAt.DistanceTo(hole.Pin) > PovFromYards
                      && LastShot.LandingTime > 0 && LastShot.Landing.DistanceTo(hole.Pin) <= PovWithinYards;
            greenRead.Hide();
            holeCam = false;
            holeStrokes++;
            strikePlayed = false;
            ShowScore();
            hud.SetMeter((float)impact.Power, (float)impact.Backswing);
            hud.SetTempo($"Swing {club.ClubSpeedMPH(impact.Power):F0} mph  ·  Load {impact.Backswing:P0}  ·  Face {impact.FaceDegrees:+0;-0}°");
            float toBall = golfer.Strike();
            hud.SetStatus("");
            RefreshControls();
            landingMarker.gameObject.SetActive(false);
            aimLine.positionCount = 0;
            aimDots.Hide();
            flightTime = -toBall; // the ball leaves when the club gets to it
            lastBallPos = ball.position;
            Bounces = 0; lastHeight = 0; trailing = false;
            var quality = Judge(LastShot);
            effects.SetQuality(quality);
            // the map: the plan goes, the shot's own path is drawn as it flies
            var plan = hud.Map;
            plan.Path.Clear(); plan.Reach.Clear(); plan.Trace.Clear(); plan.ShowZone = false; plan.Targets.Clear();
            hud.SetCheckpoints(null, null);
            plan.TraceColor = club == GolfClub.Putter ? Color.white : ShotEffects.ColorOf(quality);
            plan.Trace.Add(ball.position);
            plan.ShowLoad = false;
            plan.Changed();
            Enter(State.Flight);
            RefreshControls();   // now it is in the air: the aim and club buttons go
            ballLook.Pov(povShot);
        }

        // ----- Frame loop -----

        void Update()
        {
            stateTime += Time.deltaTime;
            Swing.Update();
            PollControllerButtons();
            UpdateControllerHint();

            // The map: the ball wherever it is; while aiming, the plan (PlanShotOnMap); in flight, the trace.
            var plan = hud.Map;
            bool aimingShot = Current == State.Aim;
            plan.Ball = ball.position;
            plan.ShowBall = ball.gameObject.activeSelf && Current != State.Menu && Current != State.Golfer;
            plan.Landing = landingMarker.position;
            plan.ShowLanding = aimingShot && landingMarker.gameObject.activeSelf;
            if (!aimingShot) plan.ShowLoad = false;
            hud.DrawMinimap(minimapCamera);
            bool hudShowsCourse = hud.Controller == null || hud.OnTv;   // (not under the controller in a preview)
            // the pin: on the maps, and on the picture — over it, or at the edge pointing the way
            plan.Pin = HoleView.ToWorld(hole.Pin);
            bool pinWanted = aimingShot && club != GolfClub.Putter && hudShowsCourse && Current != State.Menu;
            hud.SetPinMarker(pinWanted ? rig.Camera : null, plan.Pin + Vector3.up * 3.2f, $"PIN  {ballAt.DistanceTo(hole.Pin):F0} YD");
            hud.SetCourseTargets(rig.Camera, checkpointSpots, aimingShot && club != GolfClub.Putter && hudShowsCourse);
            // the yardage riding the ball, counting through the flight and the run
            if (flightHud && (Current == State.Flight || Current == State.Result) && ball.gameObject.activeSelf && hudShowsCourse)
            {
                var gone = ball.position - originWorld; gone.y = 0;
                hud.SetBallTag(rig.Camera, ballLook.Centre, $"{gone.magnitude:F0} yds");
            }
            else hud.SetBallTag(null, default, null);

            switch (Current)
            {
                case State.Menu:
                    // A slow aerial of the hole, round and back, behind the menu.
                    rig.Showcase(hole, Mathf.PingPong(stateTime / 30f, CameraRig.AerialShare * 0.999f));
                    if (menu != null && Time.frameCount % 30 == 0) menu.BigScreenStatus.text = bigScreen.Status;
                    break;

                case State.Golfer:
                    UpdateSelect();
                    break;

                case State.Intro:
                    // The showcase: the whole hole from the air, then the walk up to the green —
                    // or the hole's signature shot, where it has one — and then the introductions
                    // on the tee. A tap skips it all.
                    if (stateTime < IntroSeconds)
                    {
                        if (signature == null) rig.Showcase(hole, Mathf.Clamp01(stateTime / ShowcaseSeconds));
                        else if (stateTime < LeadSeconds) { if (signature.HasOverview) PlayOverview(stateTime); else rig.Showcase(hole, Mathf.Clamp01(stateTime / ShowcaseSeconds)); }
                        else PlaySignature(stateTime - LeadSeconds);
                        // The title pops in with the aerial and goes with it, as the broadcast's does.
                        hud.SetHoleIntroAlpha(Mathf.Min(Mathf.Clamp01((stateTime - 0.3f) / 0.45f), Mathf.SmoothStep(0, 1, (TitleSeconds - stateTime) / 0.9f)));
                    }
                    else MeetThePlayer(stateTime - IntroSeconds);
                    // A tap skips — but not the one that pressed Play, which is still down this frame.
                    if (stateTime > IntroSeconds + MeetSeconds || (stateTime > 0.75f && Input.GetMouseButtonDown(0))) BeginAim(false);
                    break;

                case State.Aim:
                    // Arrows and keys sweep at full rate; the joystick sweeps with how far it is pushed.
                    float sweep = (hud.AimLeftHeld ? -1 : 0) + (hud.AimRightHeld ? 1 : 0)
                                + (Input.GetKey(KeyCode.LeftArrow) ? -1 : 0) + (Input.GetKey(KeyCode.RightArrow) ? 1 : 0)
                                + Mathf.Clamp(hud.AimStick, -1f, 1f) * 1.5f;
                    // a putt's line is a matter of a degree or two: the sweep is a fifth as fast
                    if (sweep != 0) Nudge(sweep * AimSweepDegreesPerSecond * (club == GolfClub.Putter ? 0.2f : 1f) * Time.deltaTime);
                    if (puttRibbonPending && Time.unscaledTime >= nextPuttRibbon) LayPuttRibbon();
                    if (Input.GetKeyDown(KeyCode.UpArrow)) CycleClub(-1);
                    if (Input.GetKeyDown(KeyCode.DownArrow)) CycleClub(1);
                    if (Input.GetKeyDown(KeyCode.G)) { GolferStyle.CycleBody(); RestyleGolfer(); }
                    if (Input.GetKeyDown(KeyCode.T)) { GolferStyle.CycleSkin(); RestyleGolfer(); }
                    if (Swing.Phase == SwingPhase.Downswing && lastPhase != SwingPhase.Downswing) { sounds.PlayWhoosh(Swing.Detector.Load); if (club != GolfClub.Putter) Haptics.Top(Swing.Detector.Load); }
                    // the face dial, while the club is at address and on the way
                    bool swinging = Swing.Phase is SwingPhase.Address or SwingPhase.Backswing or SwingPhase.Downswing;
                    hud.SetFace(club != GolfClub.Putter && swinging && !Demo ? Swing.Detector.FaceNow : null);
                    if (Swing.Phase == SwingPhase.Address && lastPhase != SwingPhase.Address) { sounds.PlayReady(); Haptics.Tick(); }
                    if (Swing.Phase == SwingPhase.Backswing || Swing.Phase == SwingPhase.Downswing) { }
                    else if (Swing.Phase == SwingPhase.Address || Demo) hud.SetStatus(Swing.UsingPhone || Demo ? "Ready — swing!" : "Ready — hold SPACE or the button, release to swing");
                    else if (Swing.UsingPhone && Swing.Detector.WrongEndDown) hud.SetStatus("Flip the phone: top edge toward the ground, like a club");
                    else if (Swing.UsingPhone && !Swing.Detector.PointedDown) hud.SetStatus($"Point the phone down at the ball, like a club  ({Swing.Detector.LeanDegrees:F0}° off)");
                    else hud.SetStatus("Hold the phone still…");
                    break;

                case State.Flight:
                    flightTime += Time.deltaTime;
                    if (flightTime < 0) break;
                    if (!strikePlayed)
                    {
                        strikePlayed = true; sounds.PlayStrike(club, LastShot.Power);
                        if (club == GolfClub.Putter) Haptics.Impact(LastShot.Power); else { Haptics.Strike(lastReport.Grade, LastShot.Power); AnnounceStrike(); }
                        effects.Strike(originWorld, AimDirection(), hole.LieAt(HoleView.ToCourse(originWorld)), (float)LastShot.Power, club == GolfClub.Putter);
                    }
                    FlyBall();
                    break;

                case State.Result:
                    if (stateTime > 2.4f) { if (ReplayWorthy()) StartReplay(); else AfterResult(); }
                    break;

                case State.Replay:
                    PlayReplay();
                    break;

                case State.HoleDone:
                    if (stateTime > 3f)
                    {
                        if (holeIndex + 1 < course.Holes.Length) StartHole(holeIndex + 1);
                        else
                        {
                            RefreshControls();
                            ShowRoundCard();
                            Enter(State.RoundDone);
                        }
                    }
                    break;
            }
            lastPhase = Swing.Phase;
            SendAck();
        }

        /// One frame of the shot. The flight model flies level with the ground it left; drawn,
        /// the arc comes down on the ground where it actually lands — up on the far bank or down
        /// at the sea — the difference spread over the carry so the curve stays one smooth arc,
        /// and it never passes through anything on the way. From the first touchdown on, the
        /// bounces and the roll ride the ground they are on. Nothing is drawn falling faster than
        /// gravity, so a ball that runs off a ledge or clears a rise drops off it in an arc
        /// instead of snapping down. A ball that finds the water splashes and is gone.
        void FlyBall()
        {
            double land = LastShot.LandingTime, end = LastShot.Duration;
            var p = LastShot.PositionAt(Math.Min(flightTime, end));
            var at = new CoursePoint(p.x, p.d);
            double under = HoleView.GroundHeight(at);
            double y;
            if (flightTime < land)
            {
                double level = launchGround + (landingGround - launchGround) * (flightTime / land);
                y = Math.Max(level + p.h, under);
            }
            else y = under + p.h;
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            double lowest = drawnY + (drawnVy - CourseShot.GravityYards * dt) * dt;
            if (y < lowest) y = lowest;
            drawnVy = (y - drawnY) / dt; drawnY = y;
            var pos = new Vector3((float)p.x, (float)(y + 0.06), (float)p.d);
            if (club != GolfClub.Putter && !trailing) { effects.BeginFlight(povShot); trailing = true; }

            // The landing and every bounce after it: the turf it hit puffs up and thuds.
            bool firstDown = !touchedDown && land > 0 && flightTime >= land;
            bool bounce = touchedDown && lastHeight > 0.08 && p.h <= 0.08 && flightTime <= LastShot.CarryTime + 1e-6;
            ball.position = pos;
            replayPath.Add(((float)flightTime, pos));
            if (firstDown || bounce)
            {
                var lie = hole.LieAt(at);
                float strength = firstDown ? Mathf.Clamp((float)-drawnVy / 18f, 0.5f, 1.5f)
                                           : Mathf.Clamp((float)((lastHeight - p.h) / dt) / 18f, 0.3f, 1.2f);
                if (lie != CourseLie.Water)
                {
                    effects.Touchdown(new Vector3(pos.x, (float)under + 0.02f, pos.z), lie, strength);
                    sounds.PlayThud(strength);
                    if (firstDown) Haptics.Tick();
                }
                if (firstDown) { touchedDown = true; effects.Land(); }
                Bounces++;
            }
            lastHeight = p.h;
            var velocity = (pos - lastBallPos) / dt;
            lastBallPos = pos;
            SpinBall(pos, velocity, p.h > 0.08);
            var trace = hud.Map.Trace;
            if (splashedAt < 0 && (pos - trace[trace.Count - 1]).sqrMagnitude > 4f) { trace.Add(pos); hud.Map.Changed(); }

            var cupAt = HoleView.ToWorld(hole.Pin);
            // a chip or a full shot that is going in gets the hole cam too, once it is rolling
            bool holingOut = club != GolfClub.Putter && LastShot.IsHoled && flightTime >= LastShot.CarryTime && (cupAt - pos).magnitude <= HoleCamReach;
            if (holingOut && !holeCam) { ballLook.Pov(false); rig.RestoreFov(); rig.ResetZoom(); }
            if (club == GolfClub.Putter || holingOut || holeCam)
            {
                // Watch the putt from where it was read; as it closes on the cup, cut to
                // behind the hole to watch it arrive (the games' hole cam).
                var cup = cupAt;
                bool closing = Vector3.Dot(cup - pos, velocity) > 0;
                if (!holeCam && (cup - pos).magnitude <= HoleCamReach && (closing || LastShot.IsHoled)) { holeCam = true; rig.SnapNext(); }
                if (holeCam) rig.HoleCam(pos, cup);
            }
            else if (povShot)
            {
                // big in the air; back to its usual size as it comes down by the flag
                ballLook.Pov(flightTime < land - 0.5);
                rig.BallPov(pos, shotLine, (float)(land - flightTime), (float)flightTime);
                rig.EaseHorizontalFov(CameraRig.PovLensHorizontal, 72f, 0.6f);
            }
            else if (flightTime >= CraneStart)
            {
                var flat = pos - originWorld; flat.y = 0;
                float along = Vector3.Dot(flat, shotLine.normalized);
                float carryYards = new Vector2(landingSpot.x - originWorld.x, landingSpot.z - originWorld.z).magnitude;
                rig.Drone(pos, originWorld, new Vector3(landingSpot.x, (float)landingGround, landingSpot.z), shotLine, along, carryYards, touchedDown, (float)(flightTime - CraneStart));
            }
            // the shot's HUD, once the ball is away and the camera is climbing
            if (club != GolfClub.Putter && !flightHud && flightTime >= DroneHold)
            {
                flightHud = true;
                hud.FlightMode(true);
                ShowSwingCard();
            }
            if (swingCard && flightHud) UpdateSwingCard();

            // Done when the shot has run its course and the drawn ball has caught up with it
            // (not still dropping off an edge); the water takes a moment to swallow it first.
            bool caughtUp = Math.Abs(y - (under + p.h)) < 0.02 || flightTime > end + 3;
            if (flightTime < end || !caughtUp) return;
            if (LastShot.Lie == CourseLie.Water && splashedAt < 0)
            {
                splashedAt = flightTime;
                effects.Splash(new Vector3(pos.x, (float)under, pos.z));
                sounds.PlaySplash();
                sounds.PlayGasp(0.55f);
                if (trailing) effects.Land();
                trace.Add(pos); hud.Map.Changed();
                ball.gameObject.SetActive(false);
            }
            if (splashedAt < 0 || flightTime >= splashedAt + 1.1) FinishShot();
        }

        // ----- The round's card -----

        double longestDrive, longestShot;
        int perfectStrikes, putts;

        /// The round's highlights, counted as each ball is struck (a ball in the water or out of
        /// bounds is no one's longest drive).
        void CountForTheCard()
        {
            if (club == GolfClub.Putter) { putts++; return; }
            if (lastReport.Grade == StrikeGrade.Perfect) perfectStrikes++;
            if (LastShot.Lie is CourseLie.Water or CourseLie.OutOfBounds) return;
            longestShot = Math.Max(longestShot, LastShot.Total);
            if (club == GolfClub.Driver) longestDrive = Math.Max(longestDrive, LastShot.Total);
        }

        /// The round is done: the card, and the way on — the same holes again, or the menu.
        void ShowRoundCard()
        {
            bool drove = longestDrive > 0;
            hud.ShowPlayHud(false);
            hud.ShowScorecard(Card, new[]
            {
                new RoundCard.Highlight { Icon = "ball", Title = drove ? "Longest drive" : "Longest shot", Value = $"{(drove ? longestDrive : longestShot):F0} yd" },
                new RoundCard.Highlight { Icon = "star", Title = "Perfect strikes", Value = perfectStrikes.ToString() },
                new RoundCard.Highlight { Icon = "putter", Title = "Putts", Value = putts.ToString() },
            });
            hud.PlayAgain.Pressed = PlayAgain;
            hud.RoundMenu.Pressed = ShowMenu;
        }

        /// From the round's card straight into the same holes again.
        public void PlayAgain()
        {
            if (Current != State.RoundDone) return;
            hud.ShowPlayHud(true);
            StartRound();
        }

        // ----- The swing card -----

        SwingCard swingCard;
        /// The flight from above in the aim's frame (along it, right of it; yards) and when.
        readonly System.Collections.Generic.List<Vector2> curvePoints = new();
        readonly System.Collections.Generic.List<double> curveTimes = new();
        float curveReach, curveTarget;

        /// Up while the ball flies: the grade and the shape, what the swing measured, and the
        /// line drawing itself as the ball goes; carry and total fill in as they happen.
        void ShowSwingCard()
        {
            var r = lastReport;
            double deadZone = Swing?.Detector.FaceDeadZoneDegrees ?? 10, f = lastImpact.FaceDegrees;
            double face = Math.Abs(f) <= deadZone ? 0 : Math.Abs(f) - deadZone;
            swingCard = hud.ShowSwingCard(r.GradeWord, GradeColor(r.Grade), r.ShapeWord, new[]
            {
                ("speed", "Swing speed", $"{club.ClubSpeedMPH(lastImpact.Power):F0} mph"),
                ("swing", "Backswing", $"{Math.Min(1, lastImpact.Backswing) * 100:F0}%"),
                ("stopwatch", "Tempo", r.TempoRatio > 0 ? $"{r.TempoRatio:F1} : 1" : "—"),
                ("face", "Face", face < 0.5 ? "Square" : $"{face:F0}° {(f > 0 ? "open" : "closed")}"),
                ("ball", "Carry", "—"),
                ("target", "Total", "—"),
            });
            // the whole line now, drawn as far as the ball has got each frame
            curvePoints.Clear(); curveTimes.Clear();
            double h = LastShot.Heading * Math.PI / 180, sin = Math.Sin(h), cos = Math.Cos(h);
            var o = LastShot.PositionAt(0);
            Vector2 Aim(double x, double d) => new((float)((x - o.x) * sin + (d - o.d) * cos), (float)((x - o.x) * cos - (d - o.d) * sin));
            for (double t = 0; ; t += 0.1)
            {
                var p = LastShot.PositionAt(Math.Min(t, LastShot.Duration));
                curvePoints.Add(Aim(p.x, p.d)); curveTimes.Add(t);
                if (t >= LastShot.Duration) break;
            }
            float end = curvePoints[curvePoints.Count - 1].x;
            // the flag where the pin is if the ball went near it, else at the end of the straight line
            var pin = Aim(hole.Pin.X, hole.Pin.D);
            curveTarget = pin.x > 0 && pin.x < end * 1.3f ? pin.x : Mathf.Max(end, 1);
            curveReach = Mathf.Max(end, curveTarget) * 1.06f;
        }

        void UpdateSwingCard()
        {
            int count = curveTimes.FindLastIndex(t => t <= flightTime) + 1;
            swingCard.SetCurve(curvePoints, count, curveReach, curveTarget);
            if (flightTime >= LastShot.CarryTime) swingCard.SetTile(4, $"{LastShot.Carry:F0} yd");
            var p = LastShot.PositionAt(flightTime);
            double gone = Math.Sqrt((p.x - LastShot.Origin.X) * (p.x - LastShot.Origin.X) + (p.d - LastShot.Origin.D) * (p.d - LastShot.Origin.D));
            swingCard.SetTile(5, $"{(flightTime >= LastShot.Duration ? LastShot.Total : gone):F0} yd");
        }

        static Color GradeColor(StrikeGrade g) => g switch { StrikeGrade.Perfect => UiKit.ArcadeYellow, StrikeGrade.Great => GreatGreen, StrikeGrade.Good => Color.white, _ => ThinOrange };

        // ----- The strike's verdict and the gallery -----

        static readonly Color GreatGreen = new(0.45f, 0.95f, 0.45f), ThinOrange = new(1f, 0.55f, 0.25f);

        /// The word at impact, seen and felt: PERFECT! in gold and a crack in the hand; the gallery
        /// applauds a pure drive off the tee.
        void AnnounceStrike()
        {
            if (gradeShown) return;
            gradeShown = true;
            var r = lastReport;
            hud.ShowStrike(r.GradeWord, GradeColor(r.Grade), r.Shape == ShotShape.Straight ? null : r.ShapeWord);
            if (r.Grade == StrikeGrade.Perfect && LastShot.Power > 0.8 && gallery.gameObject.activeSelf) sounds.PlayApplause(0.3f);
        }

        /// Where it finished, and what the gallery makes of it (the water and a holed ball have
        /// their own moments).
        void React(CourseShot shot)
        {
            double toPin = shot.Rest.DistanceTo(hole.Pin);
            if (club == GolfClub.Putter)
            {
                if (shot.LippedOut) sounds.PlayGroan(0.6f);
                else if (toPin < 2.5) sounds.PlayGroan(0.3f);
                return;
            }
            switch (shot.Lie)
            {
                case CourseLie.OutOfBounds: sounds.PlayGroan(0.4f); return;
                case CourseLie.Bunker: sounds.PlayGroan(0.35f); return;
            }
            if (shot.Lie.IsPuttingSurface() && toPin <= 4) { sounds.PlayGasp(0.45f); sounds.PlayApplause(0.6f); }
            else if (shot.Lie == CourseLie.Green) sounds.PlayApplause(0.3f);
        }

        // ----- Instant replay -----

        [Tooltip("Replay the good ones: a holed ball, an approach to a few yards, a perfect drive.")]
        public bool InstantReplays = true;
        /// The drawn ball through the last shot, flight time → where it was.
        readonly System.Collections.Generic.List<(float t, Vector3 pos)> replayPath = new();
        float replayClock;
        int replayCursor;
        bool replayStruck, replayLaunched, replayLanded, replayCut, replayDone;
        /// True while a replay is on (for tests and reviews).
        public bool Replaying => Current == State.Replay;
        /// True once the replay has cut to the camera out by the landing.
        public bool ReplayCut => Replaying && replayCut;

        bool ReplayWorthy()
        {
            var s = LastShot;
            if (!InstantReplays || Demo || s == null || replayPath.Count < 10) return false;
            if (s.IsHoled) return club != GolfClub.Putter || s.Origin.DistanceTo(hole.Pin) > 3;
            if (club == GolfClub.Putter) return false;
            if (s.Lie.IsPuttingSurface() && s.Rest.DistanceTo(hole.Pin) <= 4 && s.Origin.DistanceTo(hole.Pin) > 30) return true;
            return lastReport.Grade == StrikeGrade.Perfect && s.Carry >= 200 && s.Lie is CourseLie.Fairway or CourseLie.Green;
        }

        /// The shot again: the golfer back at the top, the strike in slow motion, then the ball
        /// followed from a low camera behind the golfer and caught coming down by one out by the
        /// landing — or, for a putt, from just behind the ball, low along the green.
        void StartReplay()
        {
            Enter(State.Replay);
            hud.ShowReplay(true);
            hud.HideShotStats();
            effects.ClearTracer();
            ball.gameObject.SetActive(true);
            ball.position = replayPath[0].pos;
            ballLook.Pov(false);
            rig.RestoreFov(); rig.ResetZoom();
            golfer.ShowLoad((float)Math.Max(0.3, lastImpact.Backswing));
            replayClock = -0.8f; replayCursor = 0;
            replayStruck = replayLaunched = replayLanded = replayCut = replayDone = false;
            rig.SnapNext();
            ReplayCamera(ball.position);
        }

        void PlayReplay()
        {
            if (stateTime > 0.4f && Input.GetMouseButtonDown(0)) { EndReplay(); return; }
            // slow motion through the strike
            Time.timeScale = replayClock > -0.3f && replayClock < 0.35f ? 0.4f : 1f;
            replayClock += Time.deltaTime;
            if (!replayStruck && replayClock >= -0.4f) { replayStruck = true; replayClock = -golfer.Strike(); }
            if (replayStruck && !replayLaunched && replayClock >= 0)
            {
                replayLaunched = true;
                sounds.PlayStrike(club, LastShot.Power);
                effects.Strike(originWorld, shotLine.normalized, hole.LieAt(LastShot.Origin), (float)LastShot.Power, club == GolfClub.Putter);
                if (club != GolfClub.Putter) effects.BeginFlight(false);
            }
            float last = replayPath[replayPath.Count - 1].t;
            if (replayLaunched)
            {
                while (replayCursor < replayPath.Count - 2 && replayPath[replayCursor + 1].t <= replayClock) replayCursor++;
                var a = replayPath[replayCursor]; var b = replayPath[Math.Min(replayCursor + 1, replayPath.Count - 1)];
                float u = b.t > a.t ? Mathf.Clamp01((replayClock - a.t) / (b.t - a.t)) : 1f;
                ball.position = Vector3.Lerp(a.pos, b.pos, u);
            }
            if (replayLaunched && !replayLanded && club != GolfClub.Putter && replayClock >= LastShot.LandingTime)
            {
                replayLanded = true;
                sounds.PlayThud(1f);
                effects.Land();
            }
            if (!replayDone && replayClock >= last)
            {
                replayDone = true;
                if (LastShot.IsHoled) { sounds.PlayCup(); sounds.PlayApplause(0.5f); ball.gameObject.SetActive(false); }
                else if (LastShot.Lie == CourseLie.Water) { effects.Splash(ball.position); sounds.PlaySplash(); ball.gameObject.SetActive(false); }
                effects.EndFlight();
            }
            ReplayCamera(ball.position);
            if (replayClock > last + 1.4f) EndReplay();
        }

        void ReplayCamera(Vector3 at)
        {
            var line = shotLine; line.y = 0;
            if (line.sqrMagnitude < 1e-4f) line = AimDirection();
            line.Normalize();
            var side = Vector3.Cross(Vector3.up, line);
            Vector3 Grounded(Vector3 p, float up) => new(p.x, (float)HoleView.GroundHeight(HoleView.ToCourse(p)) + up, p.z);
            if (club == GolfClub.Putter)
            {
                // low behind the ball, trailing it along its line
                rig.Follow(Grounded(at - line * 1.6f, 0.35f), at + line * 2.5f + Vector3.up * 0.05f, 0.1f, 0.06f);
                return;
            }
            if (replayClock < LastShot.LandingTime * 0.6f)
            {
                // low behind the golfer, off the side away from them, the lens following the
                // ball away up the line
                var cam = Grounded(originWorld - line * 8f + side * 2.2f, 1.2f);
                var before = originWorld + line * 3f + Vector3.up * 0.6f;
                var look = replayLaunched ? Vector3.Lerp(before, at, Mathf.SmoothStep(0, 1, replayClock / 0.35f)) : before;
                rig.Follow(cam, look, 0.05f, 0.04f);
                rig.Zoom(Mathf.Clamp((at - cam).magnitude / 26f, 1f, 5f));
                return;
            }
            // the green camera: past the landing and a little off the line, looking back at the
            // ball as it drops in toward it and runs out
            var land = new Vector3(landingSpot.x, (float)landingGround, landingSpot.z);
            var spot = land + line * 20f + side * 5f;
            // (never below the landing: beside an island green the ground there is the sea)
            spot.y = Mathf.Max(Grounded(spot, 0f).y, land.y) + 5f;
            if (!replayCut) { replayCut = true; rig.SnapNext(); }
            rig.Follow(spot, Vector3.Lerp(land, at, 0.45f), 0.25f, 0.12f);
            rig.Zoom(1.3f);
        }

        void EndReplay()
        {
            Time.timeScale = 1f;
            hud.ShowReplay(false);
            rig.ResetZoom(); rig.RestoreFov();
            effects.EndFlight();
            if (replayPath.Count > 0) ball.position = replayPath[replayPath.Count - 1].pos;
            if (LastShot.IsHoled || LastShot.Lie == CourseLie.Water) ball.gameObject.SetActive(false);
            AfterResult();
        }

        void FinishShot()
        {
            var shot = LastShot;
            effects.EndFlight();
            aimLine.positionCount = AimLineSamples;
            string result;
            if (shot.IsHoled)
            {
                ball.gameObject.SetActive(false);
                sounds.PlayCup();
                sounds.PlayFanfare();
                sounds.PlayRoar(club == GolfClub.Putter && shot.Total < 2 ? 0.45f : 0.85f);
                Haptics.Roar();
                result = "In the hole!";
            }
            else if (shot.Lie == CourseLie.Water) { Haptics.Failure(); result = "Water  ·  +1 stroke"; }
            else if (shot.Lie == CourseLie.OutOfBounds) { Haptics.Failure(); result = "Out of bounds  ·  +1 stroke"; }
            else if (club == GolfClub.Putter) result = $"{shot.Total * 3:F0} ft  ·  {shot.Rest.DistanceTo(hole.Pin) * 3:F1} ft left";
            else result = $"Carry {shot.Carry:F0}  ·  Total {shot.Total:F0} yd  ·  {shot.Lie.Label()}";
            if (!shot.IsHoled) React(shot);
            string shape = club != GolfClub.Putter && !shot.IsHoled && lastReport.Shape != ShotShape.Straight && shot.Lie != CourseLie.Water
                ? $"  ·  {lastReport.ShapeWord[0]}{lastReport.ShapeWord.Substring(1).ToLowerInvariant()}" : "";
            result += shape;
            holeStrokes += shot.PenaltyStrokes;
            hud.ShowBanner(result, 2.2f);
            ShowScore();
            // the POV stays where it is, on the ball by the flag, as Codex's does
            if (!holeCam && club == GolfClub.Putter) rig.HoldOn(ball.position, HoleView.ToWorld(hole.Pin) - ball.position, club == GolfClub.Putter);
            Enter(State.Result);
        }

        void AfterResult()
        {
            var shot = LastShot;
            if (shot.IsHoled)
            {
                Card.Record(holeIndex, holeStrokes);
                ShowScore();
                hud.ShowBanner(Scorecard.ScoreName(holeStrokes, hole.Par), 3f);
                Enter(State.HoleDone);
                return;
            }
            ballAt = shot.NextPosition;
            PlaceBall(ballAt, 0);
            ball.gameObject.SetActive(true);   // back from the water
            rig.SnapNext();
            BeginAim(false);
        }
    }
}
