using GolfArcade.Game;
using UnityEngine;
using UnityEngine.UI;

namespace GolfArcade.Tennis
{
    /// Singles match. Ball simulation runs at a fixed 120 Hz; players are posed once per
    /// rendered frame, or every step while a swing could meet the ball.
    public sealed partial class TennisGame : MonoBehaviour
    {
        public bool FemalePlayer;
        public bool ManualSimulation;
        public bool NativeControlled;
        /// Benchmark mode: the player swings for themselves so a device run can be timed
        /// without anyone holding the phone.
        public bool AutoPlay;
        public bool Initialized { get; private set; }
        string[] equippedEmotes = TennisEmotes.Normalize(null);
        bool playerWonPoint, pointEmoteChosen;
        HeroTennisDriver.Clip? pendingPointEmote;
        public void EquipEmotes(string[] ids) { equippedEmotes = TennisEmotes.Normalize(ids); }
        public float PlayEquippedIntro() {
            var driver = Player ? Player.GetComponentInChildren<HeroTennisDriver>() : null;
            if (driver && TennisEmotes.TryClip(equippedEmotes[0], out var clip) && driver.PlayEmote(clip)) return driver.EmoteDuration(clip);
            if (Player) Player.PlayIntro();
            return 0;
        }
        public string EmoteWindow {
            get {
                if (GolfArcade.Multiplayer.SportsMultiplayer.Active) return GolfArcade.Multiplayer.SportsMultiplayer.Instance.EmoteWindow;
                if (IntroPlaying && presentation.CanChooseEmote) return "intro";
                return Flow == Phase.PointOver && playerWonPoint && !pointEmoteChosen && !Drill ? "point" : "";
            }
        }
        public bool RequestEquippedEmote(int slot) {
            if (slot < 0 || slot >= equippedEmotes.Length || EmoteWindow.Length == 0) return false;
            if (!TennisEmotes.TryClip(equippedEmotes[slot], out var clip)) return false;
            var driver = Player ? Player.GetComponentInChildren<HeroTennisDriver>() : null;
            if (!driver || !driver.HasEmote(clip)) return false;
            if (IntroPlaying) return presentation.ChooseEmote(clip);
            pointEmoteChosen = true;
            if (!driver.PlayEmote(clip)) pendingPointEmote = clip;
            HoldNextPoint((driver.EmoteDuration(clip) + .6f) * GameSpeed);
            replayDue = -1; if (replay) replay.Stop();
            return true;
        }
        public Camera GameplayCamera { get; private set; }
        public void Refeed() { resetTimer=0; BeginPoint(); }
        /// EMOTES: a taunt longer than the between-points beat keeps the next serve waiting until it has played out. The request (game seconds) is kept until the point-over timer exists, then the timer is
        /// raised to it once (it only ever grows; a match result or a live point is never touched).
        float tauntHold;
        public void HoldNextPoint(float seconds) { tauntHold = Mathf.Max(tauntHold, seconds); ApplyTauntHold(); }
        void ApplyTauntHold() { if (tauntHold > 0 && Flow == Phase.PointOver && resetTimer > 0 && resetTimer < 1e5f) { resetTimer = Mathf.Max(resetTimer, tauntHold); tauntHold = 0; } }
        /// Point flow. A point always starts from a serve, as in real tennis.
        public enum Phase { PlayerServeHold, PlayerServeToss, OpponentServe, Rally, PointOver, MatchOver }
        public Phase Flow { get; private set; }
        public bool Serving => Flow == Phase.PlayerServeHold || Flow == Phase.PlayerServeToss || Flow == Phase.OpponentServe;
        /// A field, not an auto-property: TennisMatch is a struct, and `Match.AwardPoint()` on a
        /// property scores a temporary copy -- the scoreboard never moved off 0-0.
        TennisMatch match = TennisMatch.New();
        public TennisMatch Match => match;
        /// True once one fault has been served in this point.
        public bool SecondServe { get; private set; }
        /// The server's feet are planted until the ball is struck.
        public bool ServeLocked => Flow == Phase.PlayerServeHold || Flow == Phase.PlayerServeToss;
        float serveTimer, phaseTimer;
        Vector3 tossOrigin;
        float bounceRestitution=.75f;
        public TennisActor Player { get; private set; }
        public TennisActor Opponent { get; private set; }
        public Vector3 BallPosition { get; private set; }
        public Vector3 BallVelocity { get; private set; }
        /// -1 heavy slice ... +1 heavy topspin. Changes flight and bounce, not just the clip.
        public float BallSpin { get; private set; }
        public float Stamina { get; private set; } = 1;
        public float LateralSpeed { get; private set; }
        public TennisHit LastHit { get; private set; }
        /// Latest contact, surfaced for the hitmarker and the impact map.
        public Timing LastGrade { get; private set; }
        public Vector2 LastFaceOffset { get; private set; }
        /// How late the last hit was swung, real seconds (negative: early).
        public static float LastLateness { get; private set; }
        public bool LastWasSupercharged { get; private set; }
        /// Consecutive well-struck balls. Three in a row supercharges the third.
        public int Streak { get; private set; }
        public int Hits { get; private set; }
        public int Misses { get; private set; }
        /// Shots in the current rally, both players.
        public int RallyShots { get; private set; }
        public int LongestRally { get; private set; }
        public string Feedback { get; private set; } = "Find your position. Time the swing.";
        public float MoveInput, AimInput;
        public float AimDepth = .75f;
        public bool ScoreOnlyText => PlayMode != Mode.Tutorial && !AimPractice;
        bool lastRivalLob, serveOnsetPending;
        float serveOnsetAt;
        public void SetShotAim(float across, float depth) { AimInput = Mathf.Clamp(across, -1, 1); AimDepth = Mathf.Clamp01(depth); }
        Vector2 lockedShotAim; bool shotAimLocked;
        void LockShotAim() { lockedShotAim = new Vector2(AimInput, AimDepth); shotAimLocked = true; }
        public Vector3 ShotAimTarget => TennisRules.PlacementTarget(shotAimLocked ? lockedShotAim.x : AimInput, shotAimLocked ? lockedShotAim.y : AimDepth);
        /// How far the assist wants to shift the player, in court metres, and where the ball
        /// is predicted to arrive. Exposed for the motion servo, the HUD and tests.
        public float AssistOffset { get; private set; }
        public float PredictedInterceptX { get; private set; } = -100;
        public bool Sprint;
        public bool ReplayPlaying => replay && replay.Playing;
        Transform ball, ballSpinner;
        TrailRenderer ballTrail;
        /// Squash on a bounce or a hit, decaying over a few frames.
        float squash; Vector3 squashAxis = Vector3.up;
        Renderer ballRenderer;
        LineRenderer landingRing, aimRing;
        Vector3 previousBall, renderedBall;
        Text status, banner;
        Image flash;
        TennisHud hud;
        float shownFlash;
        float accumulator, resetTimer, charge;
        TennisHitMap hitMap;
        TennisFx fx;
        TennisSounds sounds;
        TennisAudioDirector audio;
        TennisReplay replay;
        TennisResortCrowd crowd;
        TennisUmpire umpire;
        TennisStandsCrowd stands;
        TennisCoach coach;
        UnityEngine.Rendering.Volume post;
        int bounces;
        float predictedOpponentX;
        /// Seconds since the ball was last struck: the opponent reads each shot a beat late.
        float sinceStrike;
        /// Why the opponent missed the ball that ended the point, for the call ("STRETCHED WIDE").
        string opponentMissReason;
        bool incoming = true, consumedStroke;
        /// Only a serve has to land in the diagonal service box. Once the ball has been
        /// struck in a rally it may land anywhere in the singles court, so this must be an
        /// explicit flag: inferring it from `incoming`/`bounces` judged every groundstroke
        /// by serve rules and faulted any normal deep shot.
        bool serveInFlight;
        bool serveFromNearSide;
        /// A struck serve waits for the racket to actually reach the ball in the animation,
        /// so the ball never leaves before it has been hit.
        bool serveLaunchPending; TennisRules.ServeJudgement pendingServe; float pendingServePower;
        /// A fault is shown -- the ball flies into the net or long -- before the next serve.
        float faultDelay;
        /// How hard the opponent plays: 0 club player, 1 very hard.
        public float OpponentDifficulty
        {
            get => opponentDifficulty;
            set { opponentDifficulty = value; if (!rivalProfile) Profile = OpponentProfile.FromDifficulty(value); }
        }
        float opponentDifficulty = TennisOpponent.DefaultDifficulty;
        /// How the opponent plays: movement, consistency, shot choice and serve. A campaign
        /// rival brings its own; otherwise the difficulty slider picks a rung on the ladder.
        public OpponentProfile Profile = OpponentProfile.FromDifficulty(TennisOpponent.DefaultDifficulty);
        bool rivalProfile;
        /// The match format: sets to win and games per set (3 = the short first-to-three set).
        public int MatchSets = 1, MatchGames = TennisMatch.GamesToWin;
        /// Where the player has been missing (0 forehand side, 1 backhand side), for rivals
        /// that hunt a weakness; and how fast the player is moving across the court.
        readonly int[] sideMisses = new int[2], sideBalls = new int[2];
        /// What the native menu launched. A campaign match ends on its results card and hands
        /// back to the phone; training loops without the broadcast intro.
        public enum Mode { Exhibition, Campaign, Training, Tutorial }
        public Mode PlayMode { get; private set; } = Mode.Exhibition;
        /// For the phone: a finished campaign match (won, "3–1"), the scoreline after every
        /// point, and where each of the player's hits met the strings.
        public static event System.Action<bool, string> MatchFinished;
        /// Fired once as a match completes, just before the final score: "key=value;..." stats the phone
        /// turns into XP (points, aces, winners, longest rally, timing, sets, games, duration).
        public static event System.Action<string> MatchStatsReady;
        int statAces, statWinners, statPointsWon, statPointsLost; float statStartedAt;
        void ResetMatchStats() { statAces = statWinners = statPointsWon = statPointsLost = 0; statStartedAt = Time.time; }
        string MatchStatsLine()
        {
            int perfect = coach ? coach.GradeCounts[(int)Timing.Perfect] : 0;
            int great = coach ? coach.GradeCounts[(int)Timing.Great] + coach.GradeCounts[(int)Timing.Excellent] : 0;
            return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "won={0};pointsWon={1};pointsLost={2};aces={3};winners={4};longest={5};perfect={6};great={7};setsWon={8};setsLost={9};gamesWon={10};gamesLost={11};hits={12};seconds={13:0}",
                match.PlayerWonMatch ? 1 : 0, statPointsWon, statPointsLost, statAces, statWinners, LongestRally, perfect, great,
                match.PlayerSets, match.OpponentSets, match.PlayerGames, match.OpponentGames, Hits, Time.time - statStartedAt);
        }
        /// A ball's first bounce: (hit by the player, landed in, where, was a serve).
        public static event System.Action<bool, bool, Vector3, bool> Landed;
        /// Drills (the tutorial): no score and no reply from the opponent; a point that would
        /// have ended is reported here instead (point to the player?, why), and the court waits
        /// for the next Feed or serve.
        public bool Drill;
        public static event System.Action<bool, string> DrillPoint;
        /// The longest rally so far this session, for the phone's stats.
        public static event System.Action<int> RallyEnded;
        // Recorder boundaries include both serves of a point and the resulting score call.
        public static event System.Action RecordingPointStarted, RecordingPointEnded;
        public static event System.Action<string> ScoreChanged;
        /// The player's stroke ended without touching an incoming ball (visual hook only).
        public static event System.Action Whiffed;
        /// The ball went into the tape; true when the player hit it.
        public static event System.Action<bool> Netted;
        public static event System.Action<Vector2, Timing, bool> ContactMade;
        bool matchReported;
        readonly System.Random random = new(2701);
        Vector3 previousRacket;
        bool voidJudged;
        bool posePending;
        float replayDue = -1, pointsSinceReplay = 99;
        bool matchPoint;
        public string InputStatus = "Keyboard · A/D move · Shift sprint · hold/release Space · arrows aim";
        static readonly Color CourtDust = new(.55f, .62f, .48f);

        void Start()
        {
            var arena = Resources.Load<GameObject>("Tennis/TropicalV3/TropicalTennisResort");
            if (!arena) throw new System.InvalidOperationException("Approved coastal tennis arena export is missing");
            if (!NativeSportsSession.Active) TennisQuality.Apply();
            var environment = Instantiate(arena); environment.name = "Tropical tennis resort v3 — live arena";
            environment.transform.rotation=Quaternion.Euler(0,180,0);
            TennisLook.StyleArena(environment);
            // The sky and crater courts keep the arena's court kit and swap the resort for their own world.
            var venue = TennisVenue.Current;
            if (venue == TennisVenueKind.Resort)
            {
                // The island the resort sits on (Higgsfield model fitted by prepare_island.py), in
                // the arena's own frame.
                var islandPrefab = Resources.Load<GameObject>("Tennis/Island/TennisIsland");
                GameObject islandGo = null;
                if (islandPrefab) { var island = Instantiate(islandPrefab); island.name = "Tropical island"; island.transform.rotation = environment.transform.rotation * islandPrefab.transform.rotation; TennisLook.StyleIsland(island); islandGo = island; }
                TennisEnvironmentV4.Apply(environment, islandGo);   // Plan 2D postcard pass (world art only)
                crowd = gameObject.AddComponent<TennisResortCrowd>();
            }
            else TennisVenueBuilder.Build(venue, environment);
            umpire = TennisUmpire.Spawn(environment.transform.parent);
            if (venue == TennisVenueKind.Resort) { stands = gameObject.AddComponent<TennisStandsCrowd>(); stands.Build(null); }
            Player = new GameObject("Player — permanent standard").AddComponent<TennisActor>();
            Player.transform.position = new Vector3(0, .035f, -11.2f);
            Player.Build(FemalePlayer, PlayerSkinFor(FemalePlayer), NativeSportsSession.Left, PlayerBody(FemalePlayer));
            Opponent = new GameObject("Opponent — permanent standard").AddComponent<TennisActor>();
            Opponent.transform.SetPositionAndRotation(new Vector3(0, .035f, 11.2f), Quaternion.Euler(0,180,0));
            Opponent.Build(!FemalePlayer, new Color(.52f,.31f,.18f), false);
            Opponent.Motion = TennisActor.Style.Rival;
            TennisHeroSetup.Attach(this);
            BuildBall();
            landingRing=MakeLine("Predicted landing",new Color(.1f,.9f,1),.05f);
            aimRing=MakeLine("Shot aim target",new Color(1,.85f,.1f),.06f);
            var camera = Camera.main;
            if (!camera) { camera = new GameObject("Tennis gameplay camera").AddComponent<Camera>(); camera.tag = "MainCamera"; camera.gameObject.AddComponent<AudioListener>(); }
            GameplayCamera=camera;
            camera.fieldOfView = 56; camera.nearClipPlane = .08f; camera.farClipPlane = TennisVenue.FarClip;
            camera.backgroundColor = new Color(.49f,.73f,.88f); camera.clearFlags = CameraClearFlags.Skybox;
            camera.allowMSAA = true;
            camera.allowHDR = TennisQuality.Current != TennisQuality.Tier.Low;
            post = TennisLook.SetupPost(camera);
            var light = new GameObject("Resort sun").AddComponent<Light>(); light.type = LightType.Directional;
            TennisLook.LightScene(light);
            if (venue != TennisVenueKind.Resort) TennisVenueBuilder.Light(venue, light, camera);
            { var s = TennisLook.AddContactShadow(Player.transform, .55f, .45f); s.HeightOverride = 0; s.Surface = Player.transform; }
            { var s = TennisLook.AddContactShadow(Opponent.transform, .55f, .45f); s.HeightOverride = 0; s.Surface = Opponent.transform; }
            { var s = TennisLook.AddContactShadow(ball, .16f, .55f); s.FadeHeight = 4f; s.Surface = Player.transform; s.OnlyOverDeck = true; }
            fx = new GameObject("Tennis effects").AddComponent<TennisFx>(); fx.transform.SetParent(transform); fx.Build();
            juice = gameObject.AddComponent<TennisJuice>();
            HookActorFx(Player); HookActorFx(Opponent);
            sounds = TennisSounds.Create(transform);
            audio = TennisAudioDirector.Create(this);
            replay = gameObject.AddComponent<TennisReplay>();
            replay.Build(new[] { Player.transform, Opponent.transform }, ball, camera, OnReplayPose);
            BuildHud(); gameObject.AddComponent<TennisPhoneInput>();
            tossMeter = TennisTossMeter.Create(transform);
            presentation = gameObject.AddComponent<TennisPresentation>();
            presentation.Build(this, hud, umpire);
            gameObject.AddComponent<TennisFrameGovernor>();
            TennisWarmup.Run(camera, fx);
            BeginPoint(); UpdateCamera(true);
            Initialized=true;
        }

        void HookActorFx(TennisActor actor)
        {
            bool isPlayer = actor == Player;
            actor.FootPlanted = (at, speed) => { fx.Footstep(at, Mathf.Abs(speed)); if (audio) audio.OnFootstep(speed, isPlayer); };
            actor.DiveLanded = at => fx.Slide(at);
        }

        void BuildBall()
        {
            // A root that follows the flight and a child that spins, so topspin and slice are
            // visible on the ball itself.
            ball = new GameObject("Tennis ball").transform;
            ball.localScale = Vector3.one * (TennisRules.BallRadius*2);
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere); sphere.name = "Tennis ball surface";
            Destroy(sphere.GetComponent<Collider>());
            ballSpinner = sphere.transform; ballSpinner.SetParent(ball, false);
            ballRenderer = sphere.GetComponent<Renderer>();
            ballRenderer.sharedMaterial = TennisLook.Lit(Color.white, BallTexture(), .12f);
            ballRenderer.sharedMaterial.name = "Tennis ball felt";
            ballTrail = ball.gameObject.AddComponent<TrailRenderer>(); ballTrail.time = .2f; ballTrail.startWidth = .12f; ballTrail.endWidth = .0f;
            ballTrail.minVertexDistance = .04f;
            var glow = Resources.Load<Shader>("Tennis/Shaders/TennisFxAdditive");
            ballTrail.sharedMaterial = new Material(glow ? glow : Shader.Find("Sprites/Default")) { name = "Ball trail", mainTexture = TennisLook.Falloff };
            ballTrail.textureMode = LineTextureMode.Stretch;
            ballTrail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// Felt with the two curved seams, generated so spin reads without an imported asset.
        static Texture2D BallTexture()
        {
            const int w = 128, h = 64;
            var texture = new Texture2D(w, h, TextureFormat.RGB24, true) { name = "Tennis ball felt", wrapMode = TextureWrapMode.Repeat };
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)w * Mathf.PI * 2, v = (y / (float)h - .5f) * Mathf.PI;
                    float seam = Mathf.Abs(v - .55f * Mathf.Sin(2 * u));
                    float grain = Mathf.PerlinNoise(x * .31f, y * .31f) * .08f;
                    bool line = seam < .07f;
                    pixels[y * w + x] = line ? new Color32(245, 245, 235, 255)
                        : (Color32)new Color(.80f + grain, .95f + grain * .5f, .18f + grain, 1);
                }
            texture.SetPixels32(pixels); texture.Apply(true, true);
            return texture;
        }

        // --- Swing input ------------------------------------------------------------------

        /// The phone has just started a stroke. The character starts it NOW, before the phone
        /// has confirmed a real swing, so the racket moves with the player's arm instead of
        /// a fifth of a second later. Confirmation (`RequestSwing`) or a write-off
        /// (`AbortSwing`) follows within a few samples.
        public void BeginSwing(float handSide, float lift, float strokeFacing, float inputAge = 0)
        {
            if (ControllerSetup || calibration != null) return; // Only confirmed strokes count in the timing check.
            if (IntroPlaying) { presentation.Skip(); return; }
            if (!Player || Player.Swinging || resetTimer > 0 || ReplayPlaying) return;
            if (Flow == Phase.PlayerServeToss && !serveCommitted)
            {
                // Start the visible serve on onset, as rally strokes do. Confirmation
                // gates the ball and score, never the player's animation.
                if (!serveOnsetPending) {
                    serveOnsetAt = phaseTimer - (TennisRules.ServeOnsetLatency + Mathf.Clamp(inputAge, 0, .25f) + Lag) * SpeedScale;
                    serveOnsetPending = true;
                    Player.Serve(.65f, true);
                }
                return;
            }
            if (Flow != Phase.Rally || faultDelay > 0 || TrackingOverhead) return;
            StartRallySwing(.65f, handSide, lift, strokeFacing, true);
        }

        // --- The controller serve (see TennisRules: "The controller serve") -------------
        /// Set by the phone: toss meter reading (1 = dead centre), aim across (T -1 .. wide +1)
        /// and depth (0 short .. 1 deep) in the target box, and a left/right nudge (-1..1)
        /// that walks the player along the baseline before a serve, either end.
        public float TossAccuracy { get; private set; } = 1;
        public Vector2 ServeAim { get; private set; } = new(0, .8f);
        public float ServeNudge;
        /// Where the player stands to serve and to receive; the nudge moves these.
        float serveX, receiveX;
        /// Seconds into the slow wind-up after TOSS (-1 while still bouncing the ball).
        float windup = -1;
        bool serveCommitted;
        /// How good the player's last serve was (1 = perfect): the opponent's reach and
        /// composure on the return shrink with it.
        float lastServeQuality;
        /// What the phone should show: "serve", "toss", "receive", "rally", "point".
        public static event System.Action<string> PhaseChanged;
        string reportedPhase = "";
        /// Keyboard, tests and self-play toss by themselves; a phone presses TOSS.
        bool AutoToss => !NativeControlled || AutoPlay;

        /// The toss meter under the server's feet (see TennisTossMeter).
        TennisTossMeter tossMeter;
        /// Phone tap to Unity: a frame or two of bridge and polling.
        const float TossInputDelay = .03f;

        /// TOSS pressed. A negative `accuracy` (the phone) is read off the meter under the
        /// player's feet as the player saw it, the TV's delay taken off; keyboard, tests and
        /// self-play pass their own.
        public void Toss(float accuracy = -1)
        {
            if (ControllerSetup) return;
            if (Flow != Phase.PlayerServeHold || windup >= 0) return;
            if (tossMeter && accuracy < 0) Learn(tossMeter.Lateness(TossInputDelay));
            float read = tossMeter ? tossMeter.Press(Lag + TossInputDelay) : 1;
            TossAccuracy = Mathf.Clamp01(accuracy < 0 ? read : accuracy); windup = 0;
            if (hud) hud.ShowTossGrade(TossAccuracy);
        }

        public void SetServeAim(float across, float depth) => ServeAim = new Vector2(Mathf.Clamp(across, -1, 1), Mathf.Clamp01(depth));

        string PhoneState() => IntroPlaying ? "intro" : Flow switch
        {
            // The serve states say which court, so the phone can draw the right target box.
            Phase.PlayerServeHold => (windup < 0 ? "serve|" : "toss|") + (Match.DeuceCourt ? "deuce" : "ad"),
            Phase.PlayerServeToss => "toss|" + (Match.DeuceCourt ? "deuce" : "ad"),
            Phase.OpponentServe => "receive",
            Phase.Rally => "rally",
            Phase.MatchOver => "finished",
            _ => "point",
        };

        /// How old the picture on the TV is, measured on the phone (seconds, real time). The
        /// player swings at what they see, so without this a laggy TV makes every shot late.
        /// 0 means unmeasured. It seeds the lag the game then learns from play.
        public float DisplayLatency
        {
            get => lag.Prior;
            set
            {
                float before = lag.Prior; bool first = !lagPriorSet;
                lag.Prior = value; lagPriorSet = true;
                // A fresh camera measurement must not seed an older rhythm-derived estimate.
                if (first || Mathf.Abs(before - lag.Prior) >= .001f) lag.Reset();
            }
        }
        readonly TennisLagLearner lag = new();
        bool lagPriorSet;
        const string LagKey = "tennis.lag.learned.v1", LagPriorKey = "tennis.lag.prior.v1";
        /// Seconds (real) taken off every swing and toss press: the TV's delay as measured,
        /// corrected by how the player's timing actually runs (see TennisLagLearner).
        public float Lag => lag.Estimate;
        /// Only a person on the phone teaches it: never self-play, the keyboard or tests.
        void Learn(float rawLate) { if ((!lagPriorSet || lag.Prior <= 0) && NativeControlled && !AutoPlay && !NativeSportsSession.Touch) lag.Observe(rawLate); }
        void SaveLag()
        {
            if (lag.Count < 3) return;
            PlayerPrefs.SetFloat(LagKey, lag.Estimate); PlayerPrefs.SetFloat(LagPriorKey, lag.Prior);
        }
        // --- Timing check (see TennisBeatCalibration) ---------------------------------------
        TennisBeatCalibration calibration;
        int calibrationBeat = -1;
        /// The timing check finished: the lag it measured (seconds), or -1 if it could not.
        public static event System.Action<float> TimingChecked;
        public bool CheckingTiming => calibration != null;

        /// Run the timing check now: play holds while a ball bounces on the TV to a beat and
        /// the player swings along with it.
        public bool ControllerSetup { get; private set; }
        /// Keep the court visible but hold point play while the phone scans and calibrates.
        public void SetControllerSetup(bool active) {
            if (ControllerSetup == active) return;
            ControllerSetup = active;
            CancelTimingCheck();
            if (active) { replayDue = -1; if (replay) replay.Stop(); if (presentation) presentation.Skip(); }
            if (hud) hud.MatchVisible = !active;
            if (!active) Refeed();
        }

        public void StartTimingCheck()
        {
            if (!Player || ReplayPlaying) return;
            calibration = new TennisBeatCalibration(Time.unscaledTime, TennisBeatCalibration.Countdown, lagPriorSet ? lag.Prior : 0); calibrationBeat = -1;
            if (hud) hud.ShowTimingCheck(true);
        }

        public void CancelTimingCheck()
        {
            calibration = null;
            if (hud) hud.ShowTimingCheck(false);
        }

        void TickTimingCheck()
        {
            float now = Time.unscaledTime;
            float height = calibration.Height(now, out int beat);
            if (beat != calibrationBeat && beat >= 0) { calibrationBeat = beat; sounds.Bounce(.6f); }
            if (hud) hud.SetTimingCheck(height, beat, calibration.Scored, calibration.CountdownLeft(now));
            if (!calibration.Finished(now)) return;
            bool ok = calibration.TryResult(out float measured);
            calibration = null;
            // The flash probe measures the physical screen delay. A predictable beat can
            // be anticipated and includes detector latency: it must never overwrite that.
            if (lagPriorSet && lag.Prior > 0) { measured = lag.Prior; ok = true; }
            else if (ok) measured = Mathf.Clamp(measured - TennisRules.ServeLatency, 0, TennisLagLearner.Max);
            if (ok)
            {
                // The new baseline: learning in play refines it from here.
                lag.Prior = measured; lagPriorSet = true; lag.Reset();
                PlayerPrefs.SetFloat(LagKey, measured); PlayerPrefs.SetFloat(LagPriorKey, measured);
            }
            if (hud) hud.TimingCheckDone(ok);
            TimingChecked?.Invoke(ok ? measured : -1);
        }

        void TimingCheckSwing()
        {
            int beat = calibration.Swing(Time.unscaledTime);
            if (beat >= 0 && hud) hud.TimingCheckSwing(beat);
        }

        /// How far the current swing was skipped ahead for the delay (game seconds).
        float swingAdvance;

        /// A stroke begins where it would be had the player seen the ball on time: the swing
        /// skips ahead by the TV's delay (never past contact), so the racket reaches the ball
        /// when the player meant it to. Timing windows and reach are unchanged.
        void CompensateDisplayDelay()
        {
            swingAdvance = 0;
            if (Lag <= 0 || !Player.Swinging) return;
            swingAdvance = Mathf.Max(0, Mathf.Min(Lag * SpeedScale, Player.TimeToContact - .02f));
            Player.AdvanceSwing(swingAdvance);
        }

        public void RequestSwing(float power, float handSide=0, float lift=0, float strokeFacing=0, float inputAge=0)
        {
            if (calibration != null) { TimingCheckSwing(); return; }
            if (ControllerSetup) return;
            if (IntroPlaying) { presentation.Skip(); return; }
            if (juice && juice.UltimateActive) return;   // the world is frozen for the ultimate cinematic
            // A finished match waits on the results card: swing to play again.
            if (!NativeControlled && Flow == Phase.MatchOver && PlayMode != Mode.Campaign && coach && coach.ShowingResults && resetTimer < ResultsHold - 1.5f) { NewMatch(); return; }
            if (!Player || resetTimer > 0 || ReplayPlaying) return;
            if (Flow == Phase.PlayerServeToss && !serveCommitted)
            {
                bool touchSwing = !NativeControlled || NativeSportsSession.Touch;
                float at = serveOnsetPending ? serveOnsetAt : phaseTimer - ((touchSwing ? 0 : TennisRules.ServeLatency) + Lag + Mathf.Clamp(inputAge, 0, .25f)) * SpeedScale;
                serveOnsetPending = false;
                CommitServe(at);
                return;
            }
            if (Player.Swinging && Player.Provisional)
            {
                LockShotAim();
                Player.Confirm(power);
                Feedback = Player.StrokeLabel;
                return;
            }
            if (Player.Swinging) return;
            if (Flow != Phase.Rally || faultDelay > 0) return;
            StartRallySwing(power, handSide, lift, strokeFacing, false);
        }

        /// The phone wrote the stroke off -- it was a step, not a swing.
        public void AbortSwing()
        {
            serveOnsetPending = false;
            if (Player && Player.Provisional) Player.CancelSwing();
        }

        /// Where the incoming ball will cross the player's hitting line, relative to the player
        /// (negative = their left). Preparation, the swing and AutoPlay all pick the wing from
        /// this, so the body never coils for one side and then swings the other.
        float ContactOffset()
        {
            if (incoming && BallVelocity.z < -.5f)
            {
                float toArrive = (BallPosition.z - (Player.transform.position.z + .6f)) / -BallVelocity.z;
                return PredictBall(Mathf.Clamp(toArrive, 0, 1.2f)).x - Player.transform.position.x;
            }
            return BallPosition.x - Player.transform.position.x;
        }
        /// The wing the body is currently prepared on, kept while the ball is dead centre.
        bool preparedLeft;
        bool BallOnLeft(float offset) => Mathf.Abs(offset) > .1f ? offset < 0 : preparedLeft;

        void StartRallySwing(float power, float handSide, float lift, float strokeFacing, bool provisional)
        {
            if (!provisional) { LockShotAim(); if (TryTrackedOverhead(power)) return; }
            // The wing follows the ball, exactly as the preparation did. The phone's grip only
            // decides when the ball comes straight at the body, where either wing could play it
            // (the grip reading is sticky when the phone is edge-on, so trusting it everywhere
            // swung forehands at backhand balls).
            float offset = ContactOffset();
            bool leftSide = BallOnLeft(offset);
            bool backhand = Mathf.Abs(offset) >= .35f || Mathf.Abs(strokeFacing) <= .5f
                ? leftSide != NativeSportsSession.Left
                : TennisRules.UseBackhand(strokeFacing, leftSide, NativeSportsSession.Left);
            float gap=Mathf.Abs(BallPosition.x-Player.transform.position.x);
            string kind=TennisRules.StrokeFor(BallPosition.y,BallPosition.z,Player.transform.position.z,gap,lift>.18f);
            // A low, flat swing slices; a hard one with lift is topspin. Both have their own
            // authored clip, and now their own ball flight too.
            if (kind == "Dive") kind = "Drive"; // Dive is an explicit player action.
            if (kind=="Drive") kind = lift < -.04f ? "Slice" : power > .72f ? "Topspin" : "Drive";
            // The pick-up and slice are authored forehand-only: on the backhand side they put
            // the racket on the wrong side of the body from the ball.
            if (backhand && (kind=="LowPickup" || kind=="Slice")) kind = "Drive";
            // Aim is the racket face (phone), the arrow keys, or the touch aim control; it is
            // read when the ball is struck, so it is not set here.
            Player.Swing(power,backhand,StrokeKind(kind),provisional);
            CompensateDisplayDelay();
            if (!provisional) Feedback=Player.StrokeLabel;
            consumedStroke = false; honestRejected = false;
            // No swing sound on the button: audio fires on contact, or on the called whiff.
        }

        /// The swing began at `swungAt` (toss time): the power bar's reading then decides the
        /// serve. The character swings at once, paced so the racket meets the ball as it drops
        /// into reach from the top of the toss.
        void CommitServe(float swungAt)
        {
            serveCommitted = true;
            var verdict = TennisRules.JudgeServeStrike(swungAt - TennisRules.ServeApex, TossAccuracy, ServeAim, true, Match.DeuceCourt,
                SecondServe, (float)random.NextDouble(), (float)random.NextDouble());
            Feedback = verdict.Label;
            if (hud) hud.LockServeMeter(verdict.Power, verdict.Perfect);
            pendingServe = verdict; pendingServePower = verdict.Power; serveLaunchPending = true;
            if (Player.Provisional) Player.Confirm(Mathf.Lerp(.5f, 1f, verdict.Power));
            if (!NativeControlled && HonestContactEnabled && !Player.Swinging)
            {
                // Honest serve: the visible strings meet the toss. If the ball needs longer to drop
                // to the racket than a paced swing can wait, hold the trophy position and start
                // the swing later at its natural speed (the verdict above is unchanged).
                Vector3 aimAt = Player.ContactPoint(TennisActor.Stroke.Serve, false);
                float bestD = float.MaxValue, when = 0;
                for (float t = 0; t <= 1.6f; t += TennisBall.Step)
                { float d = (PlayerToss(phaseTimer + t) - aimAt).sqrMagnitude; if (d < bestD) { bestD = d; when = t; } }
                float naturalServe = TennisRules.SweetTime;
                if (when > naturalServe * 1.25f) { serveSwingIn = when - naturalServe; serveSwingPower = Mathf.Lerp(.5f, 1f, verdict.Power); return; }
            }
            if (!Player.Swinging) Player.Serve(Mathf.Lerp(.5f, 1f, verdict.Power));
            // Meet the ball where it passes closest to the serve's contact point, slowing the
            // swing (down to a stately 0.4) for a ball that is still high.
            float natural = Player.TimeToContact, best = float.MaxValue, lead = natural;
            Vector3 aim = Player.AuthoredContact;
            for (float t = natural * .6f; t <= natural * 2.5f; t += TennisBall.Step)
            {
                float d = (PlayerToss(phaseTimer + t) - aim).sqrMagnitude;
                if (d < best) { best = d; lead = t; }
            }
            Player.PaceToContact(lead, .4f);
            Player.GuideContact(PlayerToss(phaseTimer + lead), lead);
        }

        Vector3 tossDrift;
        float serveSwingIn = -1, serveSwingPower;
        /// The player's toss, `t` seconds after release: straight ballistics, drifting across
        /// to the racket side.
        Vector3 PlayerToss(float t) => tossOrigin + tossDrift * t + new Vector3(0, 9.81f * TennisRules.ServeApex * t - 4.905f * t * t, 0);
        /// Where the ball has to come down to be hit, reached this long after release.
        float ServeContactTime()
        {
            float h = Player.ContactPoint(TennisActor.Stroke.Serve, false).y - tossOrigin.y, v = 9.81f * TennisRules.ServeApex;
            float disc = v * v - 2 * 9.81f * h;
            return disc > 0 ? (v + Mathf.Sqrt(disc)) / 9.81f : 2 * TennisRules.ServeApex;
        }

        /// The rival's toss at fraction `s` of its routine: from the tossing hand up past the
        /// strike point and back down onto it.
        Vector3 OpponentToss(float s)
        {
            Vector3 release = TennisServeRoutine.Release(Opponent), contact = Opponent.ContactPoint(TennisActor.Stroke.Serve, false);
            float d = contact.y - release.y, h = d + .45f, k = 4 * h - 2 * d;
            float b = (k + Mathf.Sqrt(Mathf.Max(0, k * k - 4 * d * d))) / 2, a = d + b;
            Vector3 flat = Vector3.Lerp(release, contact, s);
            return new Vector3(flat.x, release.y + a * s - b * s * s, flat.z);
        }

        /// Release a struck serve at the moment the animated racket meets the ball.
        void LaunchServe()
        {
            serveLaunchPending = false;
            // Pose now so the visible strings are measured at this exact simulation instant.
            if (HonestContactEnabled) Player.Pose();
            var verdict = pendingServe;
            Vector3 start = Player.transform.position + Vector3.up * TennisRules.ServeContactHeight + Player.transform.forward * .28f;
            // Off the strings, which were steered to the toss.
            Vector3 strings = Player.SweetSpot.position + Vector3.forward * (TennisRules.BallRadius * 1.2f);
            float gap = Vector3.Distance(Player.SweetSpot.position, BallPosition);
            RecordGap(true, gap); LastContactGap = gap;
            if (System.Environment.GetEnvironmentVariable("HERO_DEBUG") == "1")
                Debug.Log($"[ServeDbg] strings={Player.SweetSpot.position:F2} ball={BallPosition:F2} planned={Player.ContactPoint(TennisActor.Stroke.Serve,false):F2} ttc={Player.SignedTimeToContact:F3} age={Player.ContactAge:F3} phase={phaseTimer:F3} tossOrigin={tossOrigin:F2} drift={tossDrift:F2} player={Player.transform.position:F2}");
            // Honest serve: the strings the player sees must actually meet the tossed ball.
            // A swing under/past the toss is a missed serve (a fault), never a teleported hit.
            if (gap > HonestContactGap && HonestContactEnabled)
            {
                HonestMisses++;
                float t = phaseTimer; BallVelocity = tossDrift + new Vector3(0, 9.81f * TennisRules.ServeApex - 9.81f * t, 0);
                BallSpin = 0; incoming = false; consumedStroke = true; serveInFlight = false; opponentShot = default;
                Flow = Phase.Rally; phaseTimer = 0; RallyShots = 0;
                Feedback = "MISSED THE TOSS"; Whiffed?.Invoke(); WhiffJuice();
                Fault(true);
                return;
            }
            if (HonestContactEnabled || Vector3.Distance(strings, start) < 1.2f) start = strings;
            BallPosition = previousBall = start;
            // A second serve is hit with safer kick; a first serve is flatter and faster.
            float spin = SecondServe ? .75f : .12f;
            if (verdict.Legal)
                {
                var flight = TennisRules.PacedServe(start, verdict.Landing, verdict.Speed, spin);
                BallVelocity = flight.Velocity; spin = flight.Spin;
                }
            else
            {
                // Mistimed: struck into the tape, visibly, instead of vanishing.
                BallVelocity = TennisRules.ShotVelocity(start, new Vector3(verdict.Landing.x, TennisRules.NetHeight * .55f, 0), 18);
                spin = 0;
            }
            BallSpin = spin;
            lastServeQuality = verdict.Legal ? verdict.Accuracy : 0;
            // Serve speed is the reward; do not delay the faster ball with post-contact hit-stop.
            if (hud && verdict.Legal)
                hud.ShowGrade(verdict.Perfect ? Timing.Perfect : verdict.Power > .75f ? Timing.Excellent : verdict.Power > .45f ? Timing.Great : Timing.Good,
                    false, verdict.Label);
            bounceRestitution = .60f; bounces = 0; consumedStroke = true;
            incoming = false; serveInFlight = true; serveFromNearSide = true; opponentShot = default;
            sinceStrike = 0; opponentMissReason = null;
            Flow = Phase.Rally; phaseTimer = 0; RallyShots = 1;
            Opponent.SplitStep();
            fx.Contact(start, verdict.Perfect ? Timing.Perfect : Timing.Great, false); sounds.Hit(verdict.Perfect ? Timing.Perfect : Timing.Great, pendingServePower); contactHitter = Player;
            if (juice) { juice.EndTossCam(); if (verdict.Perfect && verdict.Legal) juice.PerfectServe(Player.transform, ball); else juice.Punch(.5f); }
            if (NativeControlled) Haptics.Strike(pendingServePower, verdict.Perfect);
        }

        static TennisActor.Stroke StrokeKind(string kind) => kind switch
        {
            "Smash" => TennisActor.Stroke.Smash,
            "Lob" => TennisActor.Stroke.Lob,
            "Volley" => TennisActor.Stroke.Volley,
            "LowPickup" => TennisActor.Stroke.LowPickup,
            "Running" => TennisActor.Stroke.Running,
            "Dive" => TennisActor.Stroke.Dive,
            "Slice" => TennisActor.Stroke.Slice,
            "Topspin" => TennisActor.Stroke.Topspin,
            _ => TennisActor.Stroke.Drive,
        };

        /// Spin a stroke puts on the ball.
        static float SpinFor(TennisActor.Stroke kind, float power) => kind switch
        {
            TennisActor.Stroke.Topspin => Mathf.Lerp(.6f, 1f, power),
            TennisActor.Stroke.Slice => -.8f,
            TennisActor.Stroke.Lob => .5f,
            TennisActor.Stroke.Volley => -.3f,
            TennisActor.Stroke.LowPickup => -.2f,
            TennisActor.Stroke.Smash => .1f,
            TennisActor.Stroke.Dive => 0,
            _ => .35f,
        };

        /// A serve fault. The ball is left to finish its flight before the next serve.
        void Fault(bool nearServer)
        {
            sounds.Call(false);
            if (audio) audio.OnFault(SecondServe);
            if (Drill && nearServer)
                Landed?.Invoke(true, false, BallPosition, true);
            if (!nearServer)
            {
                // The opponent's fault: their second serve, or the point on a double.
                if (SecondServe) { Feedback = "DOUBLE FAULT — opponent"; AwardPoint(true); return; }
                SecondServe = true; faultDelay = .8f; faultForOpponent = true;
                Feedback = "FAULT — opponent's second serve";
                if (hud) hud.ShowCall("FAULT", "SECOND SERVE", true);
                return;
            }
            if (SecondServe) { Feedback = "DOUBLE FAULT"; AwardPoint(false); return; }
            SecondServe = true; faultDelay = .8f; faultForOpponent = false;
            Feedback = "FAULT — second serve";
            if (hud) hud.ShowCall("FAULT", "SECOND SERVE", false);
        }
        bool faultForOpponent;

        /// Close the point, bank it on the scoreboard, and let the ball keep rolling.
        void AwardPoint(bool toPlayer, bool winner = false)
        {
            if (Flow == Phase.PointOver || Flow == Phase.MatchOver) return;
            if (Drill)
            {
                Flow = Phase.PointOver; resetTimer = 1e6f; faultDelay = 0; serveInFlight = false;
                DrillPoint?.Invoke(toPlayer, Feedback);
                return;
            }
            if (RallyShots >= 2) RallyEnded?.Invoke(RallyShots);
            bool wasMatchPoint = matchPoint;
            int setsBefore = match.PlayerSets + match.OpponentSets;
            bool gameWon = match.AwardPoint(toPlayer);
            if (match.Complete) Debug.Log("[MatchFinish] score committed " + match.FinalScore);
            bool setWon = match.PlayerSets + match.OpponentSets > setsBefore;
            if (toPlayer) statPointsWon++; else statPointsLost++;
            if (toPlayer && Feedback != null && Feedback.StartsWith("ACE")) statAces++;
            else if (toPlayer && winner) statWinners++;
            if (gameWon && coach) coach.OnGameEnded(match, setWon);
            Streak = 0; faultDelay = 0;
            LongestRally = Mathf.Max(LongestRally, RallyShots);
            if (umpire) umpire.Call();
            // Big moments get a real reaction: the match, a winner or an ace, a long rally,
            // and on the losing side a long rally or a double fault.
            bool big = winner || RallyShots >= 8 || (Feedback != null && Feedback.StartsWith("DOUBLE FAULT"));
            var moment = Match.Complete ? TennisActor.Moment.Match : big ? TennisActor.Moment.Big : TennisActor.Moment.Ordinary;
            if (Player) Player.React(toPlayer, moment);
            if (Opponent) Opponent.React(!toPlayer, moment);
            if (!toPlayer) { Misses++; if (RallyShots > 1 && incoming) sideMisses[Wing(BallPosition.x)]++; }
            Flow = Match.Complete ? Phase.MatchOver : Phase.PointOver;
            playerWonPoint = toPlayer; pointEmoteChosen = false; pendingPointEmote = null;
            // A finished set gets a longer beat (the SET call below), then the next set starts by itself.
            resetTimer = Match.Complete ? (AutoPlay ? 5f : ResultsHold) : (setWon ? 7f : 6f) * GameSpeed;   // Six real seconds to see the score and select a celebration.
            ApplyTauntHold();                                                                       // EMOTES: a taunt started by the reactions above holds the serve until it is over
            if (juice) juice.PointReaction(toPlayer ? Player.transform : Opponent.transform, toPlayer ? Opponent.transform : Player.transform,
                toPlayer ? "POINT " + (hud ? hud.PlayerName : "YOU") : "POINT " + (hud ? hud.OpponentName : "RIVAL"));
            ultimateBall = false; if (ballTrail) { ballTrail.startWidth = .12f; ballTrail.startColor = Color.white; }
            if (Match.Complete) Debug.Log("[MatchFinish] reactions complete; publishing stats");
            if (Match.Complete && !matchReported) MatchStatsReady?.Invoke(MatchStatsLine());   // queued before the results, so the phone has them
            if (Match.Complete) Debug.Log("[MatchFinish] publishing result card");
            if (Match.Complete && coach) coach.ShowResults(Match, Hits, LongestRally);
            if (Match.Complete) Debug.Log("[MatchFinish] result card published");
            ScoreChanged?.Invoke($"{Match.PlayerGames},{Match.OpponentGames},{Match.Scoreboard}");
            if (Match.Complete && !matchReported)
            {
                matchReported = true;
                MatchFinished?.Invoke(Match.PlayerWonMatch, Match.FinalScore);
            }
            string call = Feedback;
            Feedback = (toPlayer ? "POINT YOU — " : "POINT OPPONENT — ") + Feedback;
            float excitement = Mathf.Clamp01(RallyShots / 10f + (winner ? .3f : 0) + (Match.Complete ? .5f : 0));
            if (crowd) crowd.Cheer(toPlayer ? .45f + excitement * .55f : .25f + excitement * .4f);
            if (stands && (winner || RallyShots >= 5 || Match.Complete || gameWon)) stands.Cheer(toPlayer ? .5f + excitement * .5f : .3f + excitement * .4f);
            if (audio) audio.OnPointOver(Match, toPlayer, CallFor(call, toPlayer, winner), RallyShots, gameWon, Player && Player.Kind == TennisActor.Stroke.Smash);
            else sounds.Applaud(toPlayer ? .5f + excitement * .5f : .3f + excitement * .3f);
            if (hud && !Match.Complete)
            {
                hud.ShowPointResult(toPlayer ? hud.PlayerName : hud.OpponentName, gameWon ? $"{(setWon ? "SET" : "GAME")} · {Match.PlayerGames}–{Match.OpponentGames}" : $"{CallFor(call, toPlayer, winner)} · {Match.PointCall}", toPlayer);
                if (gameWon) hud.ShowCall("GAME", $"GAME {(toPlayer ? hud.PlayerName : hud.OpponentName)}  ·  {Match.PlayerGames}-{Match.OpponentGames}", toPlayer);
                if (setWon && Match.SetScores != null && Match.SetScores.Count > 0)
                    hud.ShowCall("SET", $"SET {(toPlayer ? hud.PlayerName : hud.OpponentName)}  ·  {Match.SetScores[Match.SetScores.Count - 1]}  ·  SETS {Match.PlayerSets}-{Match.OpponentSets}", toPlayer);
            }
            if (NativeControlled) { if (toPlayer) Haptics.Success(); else Haptics.Warning(); }
            RecordingPointEnded?.Invoke();
            SaveLag();
            // Replays are for moments, not every point: winners, long rallies and the match.
            pointsSinceReplay++;
            bool special = toPlayer && (winner || RallyShots >= 8 || (wasMatchPoint && Match.Complete));
            // Instant replay is off until it replays the visible characters truthfully (Plan 1B).
            if (InstantReplayEnabled && special && (pointsSinceReplay >= 3 || Match.Complete) && !ManualSimulation)
            { replayDue = .7f; pointsSinceReplay = 0; }
        }

        /// The one-word umpire call for how a point ended.
        static string CallFor(string reason, bool toPlayer, bool winner)
        {
            if (reason == null) return "POINT";
            if (reason.StartsWith("ACE")) return "ACE";
            if (reason.StartsWith("DOUBLE FAULT")) return "DOUBLE FAULT";
            if (reason.StartsWith("OUT")) return "OUT";
            if (reason.StartsWith("NET") || reason.StartsWith("Opponent nets")) return "NET";
            if (reason.StartsWith("MISSED") || reason.StartsWith("WHIFF")) return "MISSED";
            if (winner && toPlayer) return "WINNER";
            return "POINT";
        }

        string PointContext(bool toPlayer, string why = null)
        {
            // An opponent's miss says why: the player sees what earned the point.
            string who = toPlayer ? "POINT " + (hud ? hud.PlayerName : "YOU") : "POINT " + (hud ? hud.OpponentName : "RIVAL");
            if (!string.IsNullOrEmpty(why)) who += " · " + why;
            return RallyShots >= 4 ? $"{who}  ·  {RallyShots}-SHOT RALLY" : who;
        }

        /// The player's own character: the customisable base avatar (blender/scripts/fit_avatar.py),
        /// male or female, in the players' default skin tone (sampled from its texture, for the
        /// separate grip hands).
        public static string PlayerBody(bool female) => female ? "AvatarF" : "Avatar";
        static Color PlayerSkinFor(bool female) => female ? new Color(.90f, .63f, .41f) : new Color(.85f, .61f, .41f);

        TennisLook.Kit outfit = TennisLook.Kit.From(null, null, null, null, 2);
        /// The character screen's outfit colours, now and whenever the player is rebuilt.
        public void ApplyOutfit(TennisLook.Kit kit)
        {
            outfit = kit;
            if (Player) Player.WearKit(PlayerBody(FemalePlayer), kit);
        }

        /// The player's locker look on the Hero V4 standard; re-applied whenever the player is rebuilt.
        public HeroKit.Style? PlayerLook { get; private set; }
        public void SetPlayerLook(HeroKit.Style look)
        {
            PlayerLook = look;
            var hero = Player ? Player.GetComponentInChildren<HeroTennisDriver>(true) : null;
            if (hero) HeroKit.Apply(hero, look);
        }
        public string OpponentKey { get; private set; }

        public void SelectCharacter(bool female)
        {
            FemalePlayer = female;
            if (!Player) return;
            Vector3 position = Player.transform.position;
            Destroy(Player.gameObject);
            Player = new GameObject("Player — permanent standard").AddComponent<TennisActor>();
            Player.transform.position = position; Player.Build(female, PlayerSkinFor(female), NativeSportsSession.Left, PlayerBody(female));
            if (outfit.Any) Player.WearKit(PlayerBody(female), outfit);
            TennisHeroSetup.AttachPlayer(this);   // the Hero01 visual died with the old actor
            if (fx) HookActorFx(Player);
            { var s = TennisLook.AddContactShadow(Player.transform, .55f, .45f); s.HeightOverride = 0; s.Surface = Player.transform; }
            if (replay) replay.Build(new[] { Player.transform, Opponent.transform }, ball, GameplayCamera, OnReplayPose);
            BeginPoint();
        }

        /// Set up what the native menu chose: a campaign round against a named opponent (their
        /// own body, name and billing on the intro cards and scoreboard), or a training session.
        public void ConfigureMatch(Mode mode, string opponentKey, string opponentName, string roundLabel,
            int sets = 1, int games = TennisMatch.GamesToWin, string[] coachLines = null)
        {
            if (mode == Mode.Tutorial) mode = Mode.Exhibition;
            PlayMode = mode;
            var rival = TennisRoster.Find(opponentKey);
            // The rival plays its own game; anyone else follows the difficulty slider.
            rivalProfile = rival != null && mode == Mode.Campaign;
            Profile = rivalProfile ? rival.Profile : OpponentProfile.FromDifficulty(OpponentDifficulty);
            MatchSets = Mathf.Clamp(sets, 1, 3); MatchGames = Mathf.Clamp(games, 1, 6);
            match = TennisMatch.New(true, MatchSets, MatchGames); ResetMatchStats();
            sideMisses[0] = sideMisses[1] = sideBalls[0] = sideBalls[1] = 0;
            if (coach) coach.SetChangeoverLines(coachLines);
            // A campaign rival replaces the opponent; anything else after a campaign match goes back to the standard rival.
            if (Opponent && rival == null && OpponentKey != null)
            {
                Vector3 p0 = Opponent.transform.position; Quaternion r0 = Opponent.transform.rotation;
                Destroy(Opponent.gameObject);
                Opponent = new GameObject("Opponent — permanent standard").AddComponent<TennisActor>();
                Opponent.transform.SetPositionAndRotation(p0, r0);
                Opponent.Build(!FemalePlayer, new Color(.52f,.31f,.18f), false);
                Opponent.Motion = TennisActor.Style.Rival;
                if (fx) HookActorFx(Opponent);
                { var s = TennisLook.AddContactShadow(Opponent.transform, .55f, .45f); s.HeightOverride = 0; s.Surface = Opponent.transform; }
                TennisHeroSetup.AttachRival(this);
                if (replay) replay.Build(new[] { Player.transform, Opponent.transform }, ball, GameplayCamera, OnReplayPose);
            }
            OpponentKey = rival != null ? rival.Key : null;
            if (rival != null && Opponent)
            {
                Vector3 position = Opponent.transform.position; Quaternion rotation = Opponent.transform.rotation;
                Destroy(Opponent.gameObject);
                Opponent = new GameObject("Opponent — " + rival.Key).AddComponent<TennisActor>();
                Opponent.transform.SetPositionAndRotation(position, rotation);
                Opponent.Build(rival.Female, rival.Skin, false, rival.Key);
                Opponent.Motion = TennisActor.Style.Rival;
                if (rival.Boss) Opponent.RestingFace = TennisActor.Expression.Focus;
                TennisHeroSetup.AttachRival(this, rival);   // the Hero01 visual died with the old actor; the rival's own look
                if (fx) HookActorFx(Opponent);
                { var s = TennisLook.AddContactShadow(Opponent.transform, .55f, .45f); s.HeightOverride = 0; s.Surface = Opponent.transform; }
                if (replay) replay.Build(new[] { Player.transform, Opponent.transform }, ball, GameplayCamera, OnReplayPose);
            }
            string label = mode == Mode.Training ? "TRAINING" : mode == Mode.Tutorial ? "PRACTICE COURT" : string.IsNullOrEmpty(roundLabel) ? TennisVenue.Title : roundLabel;
            if (hud)
            {
                hud.OpponentName = mode == Mode.Training ? "COACH" : mode == Mode.Tutorial ? "RAY" : string.IsNullOrEmpty(opponentName) ? hud.OpponentName : opponentName.ToUpperInvariant();
                hud.EventLabel = mode == Mode.Training ? "TRAINING  ·  FREE RALLY" : mode == Mode.Tutorial ? "PRACTICE COURT  ·  TUTORIAL"
                    : mode == Mode.Campaign ? label : "EXHIBITION  ·  ONE SET";
            }
            matchReported = false;
            ResetMatchAbilities();
            BeginPoint();
            // Remove a lesson component left by an older saved route.
            var tutorial = GetComponent<TennisTutorial>();
            if (tutorial) { tutorial.Stop(); Destroy(tutorial); }
            if (presentation)
            {
                if (mode == Mode.Training) presentation.Finish();
                else if (mode == Mode.Campaign) presentation.Bill(label, rival != null && rival.Boss ? "THE CHAMPION" : label);
            }
        }

        // Public adapter boundary: tracked phone position can supply this without changing physics.
        public void SetLateralInput(float normalizedSpeed, bool sprint)
        { MoveInput = Mathf.Clamp(normalizedSpeed,-1,1); Sprint = sprint; }
        /// Physical steering owns the lateral axis. Automatic depth positioning remains
        /// available, but the phone must never wait on the AI's reaction timer.
        public bool PlayerUsesTrackedMovement => NativeControlled && !AutoPlay && !NativeSportsSession.Touch && !GolfArcade.Multiplayer.SportsMultiplayer.Active;

        void Update()
        {
            if (NetworkFrame()) return;
            if (!Player) return;
            if (ReplayPlaying)
            {
                replay.Tick(Time.deltaTime);
                UpdateHud();
                return;
            }
            if (!ManualSimulation)
            {
                if (!NativeControlled && !GetComponent<TennisPhoneInput>().HasMotionControl)
                    SetLateralInput((Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0), Input.GetKey(KeyCode.LeftShift));
                if (!NativeControlled) AimInput = (Input.GetKey(KeyCode.RightArrow) ? 1 : 0) - (Input.GetKey(KeyCode.LeftArrow) ? 1 : 0);
                if (Input.GetKey(KeyCode.Space)) charge = Mathf.Min(1, charge + Time.deltaTime / .7f);
                if (Input.GetKeyUp(KeyCode.Space)) { RequestSwing(Mathf.Max(.18f, charge)); charge = 0; }
                if (Input.GetKeyDown(KeyCode.R)) Refeed();
                if (!NativeControlled && Input.GetKeyDown(KeyCode.E)) RequestDive();
                // Dive is the only special-action input.
                if (Input.GetKeyDown(KeyCode.F1)) SelectCharacter(!FemalePlayer);
                if (Input.GetKeyDown(KeyCode.G)) UnityEngine.SceneManagement.SceneManager.LoadScene("Golf");
                if (AutoPlay) DriveAutoPlay();
                // Hit-stop: a few frames of stillness on contact, then play resumes. The phone's
                // input is still read, so nothing the player does is lost.
                if (calibration != null) TickTimingCheck();
                else if (ControllerSetup) { Player.Tick(Time.deltaTime, 0); Opponent.Tick(Time.deltaTime, 0); }
                else if (hitStop > 0) hitStop -= Time.deltaTime;
                // The first point waits for the presentation; the players still live and emote.
                else if (IntroPlaying)
                {
                    // Playing to the camera: both look into the lens while they are introduced.
                    var lens = GameplayCamera ? GameplayCamera.transform.position : Vector3.up * 3;
                    Player.LookAt(lens, 1); Opponent.LookAt(lens, 1);
                    Player.Upright = Opponent.Upright = 1;
                    // Relaxed ready stance, not the serve preparation, while they are introduced.
                    Player.Prepare(0, false); Opponent.Prepare(0, false);
                    Player.Tick(Time.deltaTime, 0); Opponent.Tick(Time.deltaTime, 0);
                }
                else
                {
                    accumulator += Mathf.Min(Time.deltaTime, .1f) * Pace;
                    while (accumulator >= 1f/120)
                    {
                        Step(1f/120); accumulator -= 1f/120;
                        // A contact is always drawn: no more simulation this frame, so the ball
                        // is shown on the strings before it flies.
                        if (contactHitter) { accumulator = 0; break; }
                    }
                }
            }
            if (replayDue > 0)
            {
                replayDue -= Time.deltaTime;
                if (replayDue <= 0 && replay.Play(2.6f, .5f)) { banner.text = "REPLAY"; bannerUntil = HudClock.Now + 99; }
            }
            PoseActors();
            if (pendingPointEmote.HasValue && Flow == Phase.PointOver && Player) {
                var driver = Player.GetComponentInChildren<HeroTennisDriver>();
                if (driver) {
                    HoldNextPoint((driver.EmoteDuration(pendingPointEmote.Value) + .6f) * GameSpeed);
                    if (driver.PlayEmote(pendingPointEmote.Value)) pendingPointEmote = null;
                }
            }
            string phoneState = PhoneState();
            if (phoneState != reportedPhase) { reportedPhase = phoneState; PhaseChanged?.Invoke(phoneState); }
            if (hud && Flow != Phase.PlayerServeToss && Flow != Phase.PlayerServeHold) hud.ShowServeMeter(false);
            if (tossMeter && tossMeter.gameObject.activeSelf && Flow != Phase.PlayerServeToss && Flow != Phase.PlayerServeHold) tossMeter.Hide();
            UpdateCamera(false); UpdateHud(); UpdateTrajectory(); UpdateBallVisual(); UpdateCue();
            if (juice && Opponent)
            {
                if (!juiceAttached && GameplayCamera) { juice.AttachCamera(GameplayCamera); juiceAttached = true; }
                juice.SetMeters(PlayerUltimate, RivalUltimate, UltimateName, UltimateArmed);
                // ULTIMATE charge cinematic, a beat BEFORE the full-meter hitter's contact (world frozen);
                // it cuts back to the play camera and the real hit happens there.
                if (TennisAbilities.UltimatesEnabled && Flow == Phase.Rally && !juice.UltimateActive)
                {
                    if (UltimateArmed && EligibleUltimateContact && PlayerUltimate >= 1 && !playerUltCharged && incoming && Player.Swinging && Player.SignedTimeToContact > 0 && Player.SignedTimeToContact < UltimateChargeLead)
                    { playerUltCharged = true; juice.UltimateCharge(Player.transform, hud ? hud.PlayerName : "YOU", UltimateName); }
                    if (RivalUltimate >= 1 && !rivalUltCharged && !incoming && Opponent.Swinging && opponentShot.Decided && opponentShot.Decision.Reached && !opponentShot.Decision.Error && !opponentShot.Struck
                        && Opponent.SignedTimeToContact > 0 && Opponent.SignedTimeToContact < UltimateChargeLead)
                    { rivalUltCharged = true; juice.UltimateCharge(Opponent.transform, hud ? hud.OpponentName : "RIVAL", "Curveball"); }
                }
                bool rivalMoment = Flow == Phase.Rally && ((!incoming && BallPosition.z > 1f) || (contactHitter == Opponent && sinceStrike < .6f));
                juice.UpdateRivalCam(rivalMoment, Opponent.transform, hud ? hud.OpponentName : "RIVAL");
            }
            if (umpire) umpire.Follow(BallPosition, Flow == Phase.Rally, Time.deltaTime);
            replay.Record(Time.time);
        }

        /// Build both skeletons once for everything simulated this frame.
        void PoseActors()
        {
            if (!posePending) return;
            posePending = false;
            Player.Pose(); Opponent.Pose();
        }

        // --- Movement ---------------------------------------------------------------------
        //
        // Assisted, but human. The character still runs to the ball on its own (Wii style),
        // limited the way a real player is: it reacts a beat after the opponent strikes, it
        // accelerates and tops out at a human pace, it tires, and it can only stretch so far.
        // A well-placed ball is therefore genuinely hard to reach. Leaning or stepping toward
        // the ball while it is being read earns a good jump (first step at once, and a
        // sprint), and a ball just beyond reach can still be dived for, at a price. Depth is
        // part of it: a short ball pulls the player in, a deep one pushes them back, and a
        // player who comes forward stays at the net to volley.
        Vector2 moveVelocity;
        float reactionLeft, replanIn;
        TennisRules.InterceptPlan plan;
        /// True once the player has read the current ball early (see above).
        public bool GoodJump { get; private set; }
        /// Balance instrumentation: balls sent at the player, jumps earned, dives made.
        public int IncomingBalls { get; private set; }
        public int GoodJumps { get; private set; }
        public int Dives { get; private set; }
        /// Balls that were still reachable at the moment the player reacted.
        public int ReachablePlans { get; private set; }
        /// Every ball the player has returned, across matches (Hits resets each match).
        public int Returns { get; private set; }
        bool reactedCounted;
        /// Self-play option: lean toward every ball as it is read (a perfect reader).
        public bool AutoPlayLean;
        /// Where the movement is heading, and whether the ball can be reached from there.
        public Vector2 MoveGoal { get; private set; }
        public bool BallReachable => plan.Found && plan.Reachable;

        void StartReading()
        {
            reactionLeft = TennisRules.ReactionTime; GoodJump = false; replanIn = 0; plan = default;
            IncomingBalls++; reactedCounted = false;
        }

        /// Walking pace for positioning before a serve (m/s), and the nudge that drives it:
        /// the phone's buttons, or A/D on the keyboard.
        const float PreServeWalk = 2.4f;
        const float ServeWalkPace = 1.35f;   // m/s: a brisk chibi walk to the serve spot
        float serveWalkSpeed;
        float PreServeNudge => Mathf.Abs(ServeNudge) > .01f ? Mathf.Clamp(ServeNudge, -1, 1) : NativeControlled ? 0 : MoveInput;

        void MovePlayer(float dt, Vector3 playerPosition)
        {
            if (DiveActive) { MoveDive(dt); return; }
            Vector2 me = new Vector2(playerPosition.x, playerPosition.z);
            bool live = incoming && Flow == Phase.Rally && resetTimer <= 0 && BallVelocity.z < 0;
            TrackingOverhead = false;
            bool atNet = me.y > TennisRules.NetRushLine;
            float top = TennisRules.RunSpeed;
            Vector2 goal;
            if (live)
            {
                reactionLeft -= dt; replanIn -= dt;
                if (replanIn <= 0)
                {
                    replanIn = .08f;
                    plan = TennisRules.PlanIntercept(BallPosition, BallVelocity, BallSpin, bounceRestitution, me,
                        Mathf.Max(0, reactionLeft), GoodJump ? TennisRules.SprintSpeed : TennisRules.RunSpeed, atNet, ballCurve);
                }
                var overheadOffset = Player.ContactPoint(TennisActor.Stroke.Smash, false) - Player.transform.position;
                overheadPlan = bounces == 0 && !serveInFlight ? TennisOverheads.Plan(BallPosition, BallVelocity, BallSpin, ballCurve, me,
                    overheadOffset, TennisRules.RunSpeed * Mathf.Lerp(.72f, 1, Mathf.Clamp01(Stamina/.35f)), Mathf.Min(.08f,Mathf.Max(0,reactionLeft)),moveVelocity.magnitude) : default;
                TrackingOverhead = overheadPlan.Found && !overheadAttempted;
                if (TrackingOverhead) { plan = overheadPlan; reactionLeft = Mathf.Min(reactionLeft, .08f); }
                // Stand beside the ball, on the side it is already on, not in its path.
                float side = plan.Found && plan.Point.x < me.x ? -1 : 1;
                goal = plan.Found ? new Vector2(plan.Point.x - side * .65f, plan.Point.z - .65f) : me;
                // With a visible character attached, stand so the ball arrives at ITS strings for
                // that wing (the hero's two-handed backhand meets the ball closer to the body).
                if (plan.Found && HonestContactEnabled && Player.VisualContact != null)
                {
                    var cp = Player.ContactPoint(TrackingOverhead ? TennisActor.Stroke.Smash : TennisActor.Stroke.Drive, !TrackingOverhead && (side < 0) != Player.LeftHanded) - Player.transform.position;
                    goal = new Vector2(plan.Point.x - cp.x, plan.Point.z - cp.z);
                }
                PredictedInterceptX = plan.Found ? plan.Point.x : -100;
                // The read: a lean toward where the ball is going, before reacting, is a jump.
                if (reactionLeft > 0 && !GoodJump)
                {
                    float need = goal.x - me.x;
                    if (Mathf.Abs(need) > .6f && MoveInput * Mathf.Sign(need) > TennisRules.JumpLean)
                    { GoodJump = true; GoodJumps++; reactionLeft = Mathf.Min(reactionLeft, TennisRules.JumpReaction); replanIn = 0; }
                }
                if (GoodJump) top = TennisRules.SprintSpeed;
                if (reactionLeft <= 0 && !reactedCounted && plan.Found) { reactedCounted = true; if (plan.Reachable) ReachablePlans++; }
                if (reactionLeft > 0) goal = me + moveVelocity * .05f;    // still reading
            }
            else
            {
                // Recover to the middle: the baseline, or the net if they came in. Receiving, hold
                // the receiving position, which the player can walk to adjust before the serve.
                if (Flow == Phase.OpponentServe)
                {
                    receiveX = Mathf.Clamp(receiveX + PreServeNudge * PreServeWalk * dt, -TennisRules.CourtHalfWidth - 1.5f, TennisRules.CourtHalfWidth + 1.5f);
                    goal = new Vector2(receiveX, TennisRules.BaselineZ);
                    top = PreServeWalk;
                }
                else goal = new Vector2(0, atNet && Flow == Phase.Rally ? TennisRules.NetZ : TennisRules.BaselineZ);
                if (Flow != Phase.OpponentServe) top = TennisRules.RunSpeed * .75f;
                PredictedInterceptX = -100;
            }
            if (overheadAttempted && Player.Swinging && Player.Kind == TennisActor.Stroke.Smash) goal = overheadStand;
            goal.x = Mathf.Clamp(goal.x, -TennisRules.CourtHalfWidth - 2.4f, TennisRules.CourtHalfWidth + 2.4f);
            goal.y = Mathf.Clamp(goal.y, -14.3f, -2.6f);
            MoveGoal = goal;
            // Tired legs are slower; a dive leaves them on the floor for a beat.
            top *= Mathf.Lerp(.72f, 1f, Mathf.Clamp01(Stamina / .35f));
            if (Player.GroundRecovering) top *= .08f;
            Vector2 gap = goal - me; float distance = gap.magnitude;
            float arrive = Mathf.Sqrt(2 * TennisRules.Deceleration * distance);
            Vector2 wanted = distance > .02f ? gap / distance * Mathf.Min(top, arrive) : Vector2.zero;
            bool tracked = PlayerUsesTrackedMovement && resetTimer <= 0;
            if (tracked) {
                float lateralTop = (Sprint ? TennisRules.SprintSpeed : TennisRules.RunSpeed) * Mathf.Lerp(.72f, 1, Mathf.Clamp01(Stamina / .35f));
                if (Player.GroundRecovering) lateralTop *= .08f;
                wanted.x = Mathf.Clamp(MoveInput, -1, 1) * lateralTop;
                // A live controller reversal is input, rather than the rival's human
                // reaction animation. Keep court speed bounded, respond within 120 ms.
                moveVelocity.x = Mathf.MoveTowards(moveVelocity.x, wanted.x, dt * 48f);
                moveVelocity.y = Mathf.MoveTowards(moveVelocity.y, wanted.y, dt * TennisRules.Acceleration);
            } else moveVelocity = Vector2.MoveTowards(moveVelocity, wanted, dt * TennisRules.Acceleration);
            Vector2 next = me + moveVelocity * dt;
            if (!tracked && Vector2.Dot(goal - next, gap) < 0 && distance < .3f) { next = goal; moveVelocity = Vector2.zero; }
            float courtX = Mathf.Clamp(next.x, -TennisRules.CourtHalfWidth - 2.4f, TennisRules.CourtHalfWidth + 2.4f);
            if (courtX != next.x) { next.x = courtX; moveVelocity.x = 0; }
            LateralSpeed = moveVelocity.x;
            Player.transform.position = new Vector3(next.x, playerPosition.y, next.y);
        }

        void OnReplayPose(bool playing)
        {
            if (!playing) { banner.text = ""; resetTimer = Mathf.Max(resetTimer, .4f); return; }
            Player.RefreshArms(); Opponent.RefreshArms();
        }

        public void Step(float dt)
        {
            if (ControllerSetup) return;
            if (!Player || dt <= 0 || ReplayPlaying) return;
            TickAbilities(dt);
            contactHitter = null;
            AssistOffset = 0;
            Player.Upright = Opponent.Upright = 0;   // only while being introduced
            // The tossing hand is only driven during a serve routine.
            if (!ServeLocked) Player.ReachTossHand(Vector3.zero, 0);
            if (Flow != Phase.OpponentServe) Opponent.ReachTossHand(Vector3.zero, 0);
            Vector3 playerPosition = Player.transform.position;
            if (Flow == Phase.PlayerServeToss)
            {
                // The toss is anchored to the release position. Continuing the walk-in
                // under it moves the racket away from that flight and manufactures a miss.
                LateralSpeed = 0; moveVelocity = Vector2.zero; serveWalkSpeed = 0;
            }
            else if (ServeLocked)
            {
                // Before the toss the player may walk along the baseline, within the half they
                // must serve from; once TOSS is pressed the feet are planted.
                float side = Mathf.Sign(TennisRules.ServerStanceX(true, Match.DeuceCourt));
                float nudge = windup < 0 && Flow == Phase.PlayerServeHold ? PreServeNudge : 0;
                serveX = side * Mathf.Clamp(side * (serveX + nudge * PreServeWalk * dt), .35f, TennisRules.CourtHalfWidth - .15f);
                // A walk, not a slide: ease up to walking pace and slow into the spot.
                float toSpot = Mathf.Abs(serveX - playerPosition.x);
                serveWalkSpeed = Mathf.MoveTowards(serveWalkSpeed, Mathf.Min(ServeWalkPace, Mathf.Sqrt(2 * 3f * toSpot)), dt * 4f);
                float planted = Mathf.MoveTowards(playerPosition.x, serveX, dt * Mathf.Max(serveWalkSpeed, .15f));
                LateralSpeed = (planted - playerPosition.x) / dt; moveVelocity = Vector2.zero;
                Player.transform.position = new Vector3(planted, playerPosition.y, Mathf.MoveTowards(playerPosition.z, -TennisRules.ServeDepth, dt * 4f));
            }
            else MovePlayer(dt, playerPosition);
            // Overhead positioning is assisted; the stroke starts only on confirmed player input.
            Stamina = TennisRules.StaminaStep(Stamina, moveVelocity.magnitude, dt);
            previousRacket = Player.SweetSpot.position;
            bool wasSwinging = Player.Swinging;
            // A provisional swing that is never confirmed or written off is dropped.
            if (Player.Provisional && Player.SwingAge > .32f) Player.CancelSwing();
            ResolvePending(dt);
            Player.Advance(dt, LateralSpeed, moveVelocity.y);
            // Pose every step while the racket could meet the ball, so the swept contact test
            // sees the real racket path; otherwise once per rendered frame is enough.
            bool precise = Player.Swinging && incoming && !consumedStroke;
            if (precise) Player.Pose(); else posePending = true;
            if (wasSwinging && !Player.Swinging && !consumedStroke && Flow == Phase.Rally && incoming)
            { Feedback = "WHIFF — time the swing to the bounce"; if (coach && NativeControlled) coach.Offer(TennisCoach.Tip.Whiff); Whiffed?.Invoke(); WhiffJuice(); }
            if (serveSwingIn >= 0)
            {
                serveSwingIn -= dt;
                if (serveSwingIn < 0)
                {
                    Player.Serve(serveSwingPower);
                    float lead = Mathf.Max(.05f, Player.TimeToContact);
                    Player.GuideContact(PlayerToss(phaseTimer + lead), lead);
                }
            }
            if (serveLaunchPending && serveSwingIn < 0 && (!Player.Swinging || Player.ContactAge >= TennisRules.SweetTime - .002f)) LaunchServe();
            UpdateOpponent(dt);
            UpdateAttention();
            phaseTimer += dt;
            if (faultDelay > 0)
            {
                faultDelay -= dt;
                if (faultDelay <= 0)
                {
                    serveInFlight = false; phaseTimer = 0; serveTimer = 0; windup = -1; serveCommitted = false;
                    if (faultForOpponent) { Flow = Phase.OpponentServe; }
                    else Flow = Phase.PlayerServeHold;
                    if (fx) fx.Stream(BallPosition, false);
                    return;
                }
            }
            // A finished point keeps simulating: a ball called out should bounce away like a
            // real one instead of freezing where it landed.
            if (resetTimer > 0 && replayDue <= 0)
            {
                resetTimer -= dt;
                if (resetTimer <= 0)
                {
                    // A campaign match stays on its result until the phone moves on.
                    if (Flow == Phase.MatchOver) { if (!NativeControlled && PlayMode != Mode.Campaign) NewMatch(); }
                    else BeginPoint();
                    return;
                }
            }
            if (Flow == Phase.PlayerServeHold)
            {
                // Bounce the ball until TOSS is pressed, then raise the arm slowly and let go.
                float windStart = TennisRules.ServeTossDelay - TennisServeRoutine.WindUp;
                float routine;
                if (windup < 0)
                {
                    // The meter only appears once the server has walked in and planted at the line.
                    bool planted = Mathf.Abs(Player.transform.position.x - serveX) < .06f && Mathf.Abs(Player.transform.position.z + TennisRules.ServeDepth) < .06f;
                    if (tossMeter && planted) tossMeter.Run(Player.transform.position);
                    else if (tossMeter && !planted) { tossMeter.Hide(); phaseTimer = 0; }
                    float loop = windStart + .6f;
                    routine = Mathf.Min(Mathf.Repeat(phaseTimer, loop), windStart - .001f);
                    if (serveWalkSpeed > .2f) routine = 0;   // carry the ball while walking in; bounce once planted
                    // Self-play tosses like a person: anywhere on the meter, rarely dead centre.
                    if (AutoToss && phaseTimer >= windStart) Toss(AutoPlay ? Mathf.Lerp(.3f, 1f, (float)random.NextDouble()) : 1);
                }
                else
                {
                    windup += dt;
                    routine = windStart + TennisServeRoutine.WindUp * Mathf.Clamp01(windup / TennisRules.ServeWindUp);
                }
                TennisServeRoutine.Pose(Player, routine, out Vector3 held, out Vector3 palm);
                previousBall = BallPosition; BallPosition = held; BallVelocity = Vector3.zero;
                Player.ReachTossHand(palm, 1);
                Player.Prepare(windup < 0 ? 0 : Mathf.Clamp01(windup / TennisRules.ServeWindUp) * .4f, false, true);
                if (windup >= TennisRules.ServeWindUp)
                {
                    Flow = Phase.PlayerServeToss; phaseTimer = 0; windup = -1; serveCommitted = false;
                    tossOrigin = TennisServeRoutine.Release(Player);
                    if (juice && TossAccuracy >= .8f) juice.BeginTossCam(Player.transform, ball);
                    // Up and across to where the serve's strings will meet it as it comes down; a
                    // loose toss (the phone's meter off centre) wanders off that line.
                    Vector3 across = Player.ContactPoint(TennisActor.Stroke.Serve, false) - tossOrigin; across.y = 0;
                    float miss = TennisRules.TossError(TossAccuracy);
                    Vector3 wander = Player.transform.right * ((float)random.NextDouble() * 2 - 1) * miss
                                   + Player.transform.forward * ((float)random.NextDouble() * 2 - 1) * miss * .6f;
                    tossDrift = (across + wander) / ServeContactTime();
                    if (hud) hud.ShowServeMeter(true);
                }
                return;
            }
            if (Flow == Phase.PlayerServeToss)
            {
                // Pure ballistic toss so the top, and therefore the power bar, is honest.
                float rise = 9.81f * TennisRules.ServeApex;
                previousBall = BallPosition;
                BallPosition = PlayerToss(phaseTimer);
                BallVelocity = tossDrift + new Vector3(0, rise - 9.81f*phaseTimer, 0);
                if (hud && !serveCommitted) hud.SetServeMeter(TennisRules.ServePowerAt(phaseTimer - TennisRules.ServeApex), phaseTimer < TennisRules.ServeApex);
                // The body rises into the trophy position with the toss; the tossing arm points
                // at the ball until the racket arm swings.
                if (!Player.Swinging) Player.Prepare(Mathf.Clamp01(.4f + .6f * phaseTimer / TennisRules.ServeApex), false, true);
                Player.ReachTossHand(TennisServeRoutine.TossPalm(Player, phaseTimer), Player.Swinging ? 0 : 1);
                if (phaseTimer >= TennisRules.ServeCatch && !serveLaunchPending && !Player.Swinging)
                {
                    // Never swung: catch it and bounce again. Not a fault.
                    Flow = Phase.PlayerServeHold; phaseTimer = 0; windup = -1; serveCommitted = false;
                    Feedback = "Caught it — press TOSS when ready"; Player.Prepare(0, false, true); if (juice) juice.EndTossCam();
                    if (hud) hud.ShowServeMeter(false);
                }
                return;
            }
            if (Flow == Phase.OpponentServe) {
                serveTimer+=dt;
                previousBall = BallPosition;
                if (serveTimer < TennisRules.ServeTossDelay)
                {
                    // The same routine as the player's: bounce, bounce, wind up.
                    TennisServeRoutine.Pose(Opponent, serveTimer, out Vector3 held, out Vector3 palm);
                    BallPosition = held; Opponent.ReachTossHand(palm, 1); Opponent.Prepare(0, false, true);
                    return;
                }
                float tossAge = serveTimer - TennisRules.ServeTossDelay;
                float toss=Mathf.Clamp01(tossAge/.85f);
                BallPosition=OpponentToss(toss);
                Opponent.ReachTossHand(TennisServeRoutine.TossPalm(Opponent, tossAge), Opponent.Swinging ? 0 : 1);
                if(tossAge<.67f) Opponent.Prepare(toss, false, true);
                if(tossAge>=.67f && !Opponent.Swinging)
                {
                    Opponent.Serve(.55f);
                    // The racket meets the toss where it will be at the strike: contact frame
                    // paced to land exactly on the launch below.
                    if (HonestContactEnabled) Opponent.PaceToContact(.85f - tossAge, .4f);
                    Opponent.GuideContact(OpponentToss(1), .85f-tossAge);
                }
                if(tossAge>=.85f) { if (HonestContactEnabled) Opponent.Pose(); BallPosition=Opponent.SweetSpot.position; Flow=Phase.Rally; phaseTimer=0; Opponent.ReachTossHand(Vector3.zero, 0); ServeFromOpponent(); }
                return;
            }
            SimulateBall(dt);
        }

        void SimulateBall(float dt)
        {
            Vector3 oldBall = BallPosition;
            previousBall = oldBall;
            Vector3 position = BallPosition, velocity = BallVelocity;
            TennisBall.Integrate(ref position, ref velocity, BallSpin, dt, ballCurve);
            BallPosition = position; BallVelocity = velocity;
            if (incoming && !consumedStroke && !honestRejected && Player.Swinging && !pending.Active && TennisRules.CrossStringBed(oldBall, BallPosition, previousRacket,
                Player.SweetSpot.position, Player.StringNormal, Player.StringRight, Player.StringUp, out var faceOffset))
            {
                float reach = Vector3.Distance(Player.transform.position + Vector3.up * 1.1f, BallPosition);
                float reachQuality = Mathf.Clamp01(1 - Mathf.Abs(reach - .85f) / .8f);
                var hit = TennisRules.Evaluate(Player.ContactAge, faceOffset, 1 - Mathf.Abs(LateralSpeed) / 10, reachQuality, Player.Power, Stamina);
                hitLateness = Player.SignedTimeToContact / SpeedScale;
                if (hit.Contact) Strike(hit, faceOffset, true);
            }
            // Arcade reach assist: still requires a deliberate, timed swing near the ball.
            // Exact string contact above retains the best quality reward. Explicit dive travel supplies extra reach.
            if(incoming && !consumedStroke && !honestRejected && Player.Swinging && !pending.Active && TennisRules.AssistedContact(oldBall,BallPosition,Player.transform.position,Player.ContactAge,Player.Overhead,out float assist,Player.Power,false)) {
                // Timed against the ball arriving beside the player (where the cue closes),
                // not the frame it first came within reach: the racket should reach its
                // contact point as the ball reaches the contact point.
                float ballDue=BallVelocity.z<-.5f ? (BallPosition.z-(Player.transform.position.z+.65f))/-BallVelocity.z : 0;
                float late=(Player.SignedTimeToContact-ballDue)/SpeedScale;
                Learn(late+swingAdvance/SpeedScale);
                hitLateness=late;
                var hit = TennisRules.AssistedHit(late, assist, 1-Mathf.Abs(LateralSpeed)/10, Player.Power, Stamina);
                // On the strings where a contact this good lands: the sweet spot for a clean,
                // well-timed hit, toward the frame for a scrambled one.
                float early=-late/.14f;
                float high=(BallPosition.y-Player.transform.position.y-1.1f)/.9f;
                // Reach by how far to the side the ball passes: about 0.7 m out on the racket
                // side is ideal, jammed at the body or at full stretch is not.
                float reachFit=1-Mathf.Clamp01((Mathf.Abs(BallPosition.x-Player.transform.position.x)-.7f)/(Mathf.Abs(BallPosition.x-Player.transform.position.x)<.7f ? .7f : 1.1f));
                Vector2 where=TennisRules.AssistedFace(hit.Timing*.7f+reachFit*.3f,early,high);
                Strike(hit, where, false);
            }
            bool live = Flow == Phase.Rally && faultDelay <= 0;
            if (live && oldBall.z * BallPosition.z < 0)
            {
                float fraction = Mathf.Abs(oldBall.z / (BallPosition.z - oldBall.z));
                float height = Mathf.Lerp(oldBall.y, BallPosition.y, fraction);
                if (height < TennisRules.NetHeight && Mathf.Abs(BallPosition.x) < 6.4f)
                {
                    // Into the tape: the ball drops dead on the hitter's side.
                    BallPosition = new Vector3(BallPosition.x, height, oldBall.z > 0 ? .05f : -.05f);
                    BallVelocity = new Vector3(BallVelocity.x * .1f, 0, -BallVelocity.z * .08f);
                    sounds.Net();
                    // Score80 B: the tape is a comic beat -- a boing ring on the net, a NET! call and the hitter's slump.
                    if (fx) fx.NetHit(new Vector3(BallPosition.x, height, 0));
                    Netted?.Invoke(!incoming); if (juice) juice.Call("NET!");
                    if (serveInFlight) { serveInFlight = false; Feedback = "FAULT — net"; Fault(serveFromNearSide); }
                    else { Feedback = "NET"; AwardPoint(incoming); }
                }
            }
            if (BallPosition.y > .6f) voidJudged = false;
            bool overDeck = TennisVenue.OverDeck(BallPosition);
            // Sky and crater courts have an open edge: a ball that comes down beyond it is called
            // like any other bounce, but there is no floor to bounce off, so it falls away.
            if (BallPosition.y < TennisRules.BallRadius && BallVelocity.y < 0 && (overDeck || !voidJudged))
            {
                bounces++;
                if (overDeck)
                {
                    BallPosition = new Vector3(BallPosition.x,TennisRules.BallRadius,BallPosition.z);
                    Vector3 bounced0 = BallVelocity; float spin0 = BallSpin;
                    fx.Bounce(BallPosition, bounced0, CourtDust);
                    squash = 1; squashAxis = Vector3.up;
                    sounds.Bounce(Mathf.Abs(bounced0.y) / 8f);
                    TennisBall.Bounce(ref bounced0, ref spin0, bounceRestitution);
                    BallVelocity = bounced0; BallSpin = spin0;
                }
                else voidJudged = true;
                if (live && bounces == 1)
                {
                    bool byPlayer = serveInFlight ? serveFromNearSide : !incoming;
                    bool farSide = byPlayer ? BallPosition.z > 0 : BallPosition.z < 0;
                    bool landedIn = farSide && (serveInFlight ? TennisRules.ServeIsIn(BallPosition, serveFromNearSide, Match.DeuceCourt) : TennisRules.BounceIsIn(BallPosition));
                    Landed?.Invoke(byPlayer, landedIn, BallPosition, serveInFlight);
                }
                if (live)
                {
                    if (serveInFlight && bounces == 1)
                    {
                        // Serve: must clear the net into the diagonal service box.
                        serveInFlight = false;
                        if (!TennisRules.ServeIsIn(BallPosition, serveFromNearSide, Match.DeuceCourt))
                        {
                            Feedback = Mathf.Abs(BallPosition.z) > TennisRules.ServiceLine ? "FAULT — long" : "FAULT — wrong box";
                            Fault(serveFromNearSide);
                        }
                    }
                    else if (bounces == 1 && (incoming ? BallPosition.z > 0 : BallPosition.z < 0))
                    {
                        // First bounce on the hitter's own side: it never cleared the net.
                        Feedback = incoming ? "Opponent nets it" : "NET"; AwardPoint(incoming, incoming);
                    }
                    else if (!TennisRules.BounceIsIn(BallPosition))
                    { Feedback = "OUT"; sounds.Call(false); AwardPoint(incoming); }
                    else if (bounces > 1)
                    {
                        // Second bounce on the opponent's side is the player's winner.
                        bool playerWins = BallPosition.z > 0;
                        Feedback = playerWins && serveFromNearSide && RallyShots <= 1 ? "ACE" : "DOUBLE BOUNCE";
                        AwardPoint(playerWins, playerWins);
                    }
                }
            }
            // The opponent reads the ball as it arrives and starts its swing so the racket
            // meets it: the return leaves from its strings at the contact frame, instead of the
            // old instant reply sent from wherever the ball happened to be.
            if (live && !Drill && !incoming && !opponentShot.Decided && BallVelocity.z > .5f)
            {
                float contactZ = Opponent.transform.position.z - .65f;
                float toArrive = (contactZ - BallPosition.z) / BallVelocity.z;
                if (toArrive <= OpponentSwingLead || BallPosition.z > contactZ)
                {
                    // Returning the player's serve: the better the serve, the less of the court the
                    // opponent can cover and the more it is rushed. A perfect one can ace.
                    bool returningServe = RallyShots == 1 && serveFromNearSide;
                    float serveBite = returningServe ? lastServeQuality : 0;
                    // What makes this ball hard: its pace, the height it will be met at, and
                    // how well it was struck. The opponent misses only hard balls.
                    float ballSpeed = new Vector2(BallVelocity.x, BallVelocity.z).magnitude;
                    float pace = returningServe ? Mathf.InverseLerp(22, 52, ballSpeed) : Mathf.InverseLerp(16, 32, ballSpeed);
                    float metAt = PredictBall(Mathf.Max(0, toArrive)).y;
                    float height = metAt < .55f ? -Mathf.InverseLerp(.55f, .15f, metAt) : metAt > 1.7f ? Mathf.InverseLerp(1.7f, 2.6f, metAt) : 0;
                    // A great serve makes the return harder, but only by so much: the receiver's
                    // own skill (reach, read, hands) decides whether it comes back.
                    float struck = returningServe ? serveBite * .6f : LastWasSupercharged || ultimateBall ? 1 : LastHit.Quality;
                    if (ultimateBall) pace = 1;   // an ultimate is as hard to return for the AI as a supercharged ball, at top pace
                    float reach = Profile.Reach + (returningServe ? Profile.ReturnReach : 0);
                    var decision = TennisOpponent.Decide(Opponent.transform.position.x, BallPosition.x + BallVelocity.x * Mathf.Max(0, toArrive),
                        Player.transform.position.x, LateralSpeed, WeakSide(), Profile,
                        (float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble(),
                        pace, reach, height, struck, returningServe, Player.transform.position.z, lastRivalLob);
                    Feedback = decision.Label;
                    opponentMissReason = decision.Error ? decision.Reason : !decision.Reached ? null : opponentMissReason;
                    opponentShot = new OpponentShot { Decided = true, Decision = decision,
                        Spin = decision.Error ? 0 : decision.Spin };
                    // Forehand or backhand by where the ball will be when it is met, not where it is.
                    bool ballRight = PredictBall(Mathf.Max(0, toArrive)).x > Opponent.transform.position.x;
                    if (!decision.Reached)
                    {
                        // Just out of reach: it throws itself at the ball anyway, which sells
                        // the winner far better than standing and watching it go by.
                        if (Mathf.Abs(BallPosition.x - Opponent.transform.position.x) < Profile.Reach + 1.8f)
                            Opponent.Swing(.8f, ballRight != Opponent.LeftHanded, TennisActor.Stroke.Dive);
                        sounds.Gasp(); AwardPoint(true, true);
                    }
                    else
                    {
                        Opponent.Swing(.62f, ballRight != Opponent.LeftHanded);
                        // Met where the ball passes closest to the swing's contact point.
                        float natural = Opponent.TimeToContact;
                        float lead = PlanContact(Opponent, natural * .6f, Mathf.Min(natural * 1.6f, .45f), out Vector3 meet);
                        Opponent.PaceToContact(lead); Opponent.GuideContact(meet, lead);
                    }
                }
            }
            if (live && !incoming && opponentShot.Decided && opponentShot.Decision.Reached && !opponentShot.Struck
                && (Opponent.ContactAge >= TennisRules.SweetTime - .002f || !Opponent.Swinging))
            {
                opponentShot.Struck = true;
                var decision = opponentShot.Decision;
                Opponent.Pose();
                Vector3 strings = Opponent.SweetSpot.position + Vector3.back * (TennisRules.BallRadius * 1.2f);
                float rivalGap = Vector3.Distance(strings, BallPosition);
                RecordGap(false, rivalGap);
                var ot = Opponent.transform;
                LastRivalStrike = $"gap={rivalGap:0.00} ballL={ot.InverseTransformPoint(BallPosition):F2} strL={ot.InverseTransformPoint(strings):F2} guideL={ot.InverseTransformPoint(Opponent.GuideBall):F2} ttc={Opponent.SignedTimeToContact:0.00} swing={Opponent.Swinging} kind={Opponent.Kind} bh={Opponent.Backhand}";
                if (HonestContactEnabled && rivalGap > RivalHonestGap)
                {
                    // The rival's racket visibly missed: an honest rival whiff, not a hit from thin air.
                    RivalWhiffs++; opponentShot.Struck = true;
                    if (fx) fx.Whiff(Opponent.transform.position, Opponent.SweetSpot.position);
                    Feedback = "RIVAL WHIFFED IT"; sounds.Gasp(); AwardPoint(true, true);
                    return;
                }
                if (rivalGap < 2f) BallPosition = previousBall = strings;
                if (juice) juice.EndLiveShot();   // 2C: the ball is coming at the player — play camera
                if (decision.Error && decision.Landing.z > 0)
                {
                    // Netted: struck into the tape, where it drops dead on its own side.
                    InjectBall(BallPosition, TennisRules.ShotVelocity(BallPosition, new Vector3(decision.Landing.x, TennisRules.NetHeight * .55f, 0), 18));
                    bounceRestitution = .75f;
                }
                else if (TennisAbilities.UltimatesEnabled && RivalUltimate >= 1)
                {
                    // Same rule as the player: a full meter fires on the next real contact.
                    RivalUltimate = 0; ultimateBall = true; rivalUltCharged = false;
                    SendFromOpponent(decision.Landing, decision.Speed, 0);
                    ballCurve = decision.Landing.x < 0 ? -4 : 4;
                    BallVelocity = TennisAbilities.CurveVelocity(BallPosition, decision.Landing, ballCurve);
                    if (ballTrail) { ballTrail.startWidth = .45f; ballTrail.startColor = new Color(.4f, .9f, 1f); }
                    if (juice) juice.UltimateHit(hud ? hud.OpponentName : "RIVAL", "Curveball");
                }
                else {
                    SendFromOpponent(decision.Landing, decision.Speed, opponentShot.Spin, decision.Kind);
                    lastRivalLob = !decision.Error && decision.Kind == TennisReturnKind.Lob;
                    RivalUltimate = 0; ultimateBall = false;
                    if (ballTrail) { ballTrail.startWidth = .12f; ballTrail.startColor = Color.white; }
                }
                fx.OpponentContact(BallPosition, ultimateBall); sounds.RivalHit(ultimateBall ? .9f : .6f); contactHitter = Opponent; OpponentStruck?.Invoke();
                sinceStrike = 0;
                Player.SplitStep();
                RallyShots++;
            }
            if (live && (BallPosition.z < -14 || BallPosition.y < -1))
            { Feedback = "MISSED IT"; AwardPoint(false); }
            if (fx) fx.Stream(BallPosition, LastWasSupercharged && !incoming && Flow == Phase.Rally);
        }

        void UpdateOpponent(float dt)
        {
            sinceStrike += dt;
            float opponentX;
            if (ServeLocked)
                opponentX = Mathf.MoveTowards(Opponent.transform.position.x,
                    TennisRules.ReceiverStanceX(true, Match.DeuceCourt), dt * Profile.Speed);
            else if (Flow == Phase.OpponentServe)
                opponentX = Mathf.MoveTowards(Opponent.transform.position.x,
                    TennisRules.ServerStanceX(false, Match.DeuceCourt), dt * Profile.Speed);
            else
            {
                // It reads the shot a beat late, then moves to where the ball will actually
                // arrive rather than chasing where it currently is.
                Vector3 arrival = BallPosition;
                bool ballComing = !incoming
                    && TennisRules.PredictLanding(BallPosition, BallVelocity, out arrival, BallSpin, ballCurve);
                if (ballComing && BallVelocity.z > .5f)
                {
                    // Where the ball will be when it reaches the hitting line, not where it
                    // bounces: a wide serve keeps travelling wide after the bounce.
                    float contactZ = Opponent.transform.position.z - .65f;
                    float cross = BallPosition.x + BallVelocity.x * Mathf.Max(0, (contactZ - BallPosition.z) / BallVelocity.z);
                    arrival.x = Mathf.Lerp(arrival.x, cross, .85f);
                }
                // Beside the ball, racket-side, not with it at the belly button.
                if (ballComing) predictedOpponentX = arrival.x + (arrival.x >= Opponent.transform.position.x ? -.65f : .65f);
                // Visible character: line up so the ball arrives at its strings for that wing
                // (the rival faces -z, so a ball at +x is on its backhand side).
                if (ballComing && HonestContactEnabled && Opponent.VisualContact != null)
                {
                    bool bh = (arrival.x >= Opponent.transform.position.x) != Opponent.LeftHanded;
                    predictedOpponentX = arrival.x - (Opponent.ContactPoint(TennisActor.Stroke.Drive, bh) - Opponent.transform.position).x;
                }
                bool returningServe = serveFromNearSide && RallyShots == 1;
                bool reading = ballComing && sinceStrike > (returningServe ? Profile.ReturnReaction : Profile.Reaction);
                opponentX = TennisOpponent.Reposition(Opponent.transform.position.x, predictedOpponentX, reading, dt, Profile.Speed * TennisRules.CourtMovementScale);
            }
            float opponentSpeed = (opponentX - Opponent.transform.position.x) / dt;
            // Behind the baseline to serve, back up to the rally position afterwards.
            float opponentZ = Mathf.MoveTowards(Opponent.transform.position.z, Flow == Phase.OpponentServe ? TennisRules.ServeDepth : 11.2f, dt * 4f);
            Opponent.transform.position = new Vector3(opponentX,.035f,opponentZ);
            Opponent.Advance(dt, -opponentSpeed);
        }

        /// Where each player looks, and whether they are taking the racket back yet.
        void UpdateAttention()
        {
            bool ballLive = Flow == Phase.Rally || Serving;
            Player.LookAt(ballLive ? BallPosition : Opponent.transform.position + Vector3.up * 1.4f, ballLive ? 1 : .6f);
            Opponent.LookAt(ballLive ? BallPosition : Player.transform.position + Vector3.up * 1.4f, ballLive ? 1 : .6f);
            if (Flow != Phase.Rally || Player.Swinging) { if (Flow == Phase.Rally) Player.Prepare(0, false); return; }
            // Take the racket back as the ball comes: preparation starts about 0.6s out and is
            // complete a quarter of a second before contact, as coaching describes.
            float prepare = 0;
            bool backhand = false;
            if (incoming && BallVelocity.z < -.5f)
            {
                float toArrive = (BallPosition.z - (Player.transform.position.z + .6f)) / -BallVelocity.z;
                prepare = 1 - Mathf.Clamp01((toArrive - .25f) / .45f);
                bool leftSide = BallOnLeft(ContactOffset());
                preparedLeft = leftSide;
                backhand = leftSide != NativeSportsSession.Left;
            }
            Player.Prepare(prepare, backhand);
            if (prepare > .3f && coach && NativeControlled) coach.Offer(backhand ? TennisCoach.Tip.Backhand : TennisCoach.Tip.FirstBall);
            if (!incoming && BallVelocity.z > .5f)
            {
                float toArrive = (Opponent.transform.position.z - .6f - BallPosition.z) / BallVelocity.z;
                bool oppBackhand = BallPosition.x > Opponent.transform.position.x;
                Opponent.Prepare(1 - Mathf.Clamp01((toArrive - .25f) / .45f), oppBackhand);
            }
            else Opponent.Prepare(0, false);
        }

        /// Benchmark driver: swing at the right moment for every ball.
        void DriveAutoPlay()
        {
            if (TrackingOverhead || overheadAttempted && incoming) return;
            if (AutoPlayLean && incoming && Flow == Phase.Rally && plan.Found)
                MoveInput = Mathf.Sign(plan.Point.x - Player.transform.position.x);
            if (Player.Swinging) return;
            if (Flow == Phase.PlayerServeToss && !serveCommitted && phaseTimer >= TennisRules.ServeApex - .18f + (float)random.NextDouble() * .3f)
            { BeginSwing(0, .3f, 1); RequestSwing(.8f, 0, .3f, 1); return; }
            if (Flow != Phase.Rally || !incoming || BallVelocity.z > -1) return;
            float toContact = (BallPosition.z - (Player.transform.position.z + .65f)) / -BallVelocity.z;
            // Self-play normally swings in a narrow band; with jitter (editor proof runs) it times each
            // ball like a person would, so routine, great and perfect hits all show up.
            if (autoSwingAt < 0) autoSwingAt = AutoPlayTimingJitter > 0 ? .15f + ((float)random.NextDouble() * 2 - 1) * AutoPlayTimingJitter : .2f;
            if (toContact < autoSwingAt && toContact > (AutoPlayTimingJitter > 0 ? .02f : .1f))
            {
                autoSwingAt = -1;
                float facing = BallOnLeft(ContactOffset()) ? -1 : 1;
                AimInput = (float)random.NextDouble() * 1.6f - .8f;
                BeginSwing(0, 0, facing); RequestSwing(.55f + (float)random.NextDouble() * .35f, 0, 0, facing);
            }
        }

        public static float AutoPlayTimingJitter = 0;
        float autoSwingAt = -1;
        struct OpponentShot { public bool Decided, Struck; public TennisReturn Decision; public float Spin; }
        OpponentShot opponentShot;
        /// How long before the ball arrives the opponent commits to its swing: its stroke
        /// reaches contact this long after it starts.
        const float OpponentSwingLead = .19f;

        /// A contact the phone has not yet confirmed. The swing started on onset; if the ball
        /// meets the racket before the phone confirms it, the hit is held here and credited
        /// the moment confirmation arrives -- with the ball put back where it was struck and
        /// flown forward for the time that passed -- instead of being lost.
        struct PendingHit { public bool Active; public TennisHit Hit; public Vector2 Face; public Vector3 At; public float Age, Lead; }
        PendingHit pending;
        /// Longest a contact may wait for confirmation (onset hold plus confirmation arc).
        public const float ConfirmGrace = .14f;
        /// Furthest the visible string-bed centre may be from the ball for a contact to count:
        /// about the hoop's half-width plus the ball. Beyond it the swing visibly missed.
        public const float HonestContactGap = .22f;
        /// Contacts refused because the visible racket was not at the ball (tests/proof).
        public int HonestMisses { get; private set; }
        public float LastContactGap { get; private set; }
        bool honestRejected;
        public static bool InstantReplayEnabled = false;
        /// Same honesty as the player's contact, with a touch more reach for the AI's visible racket.
        public const float RivalHonestGap = .26f;
        public int RivalWhiffs { get; private set; }
        /// ULTIMATE meters, equal access: every real contact charges the hitter's meter (the player by
        /// the quality of the contact, the AI at the average of those rates). Players arm their selected
        /// ability at full meter; a qualifying real contact spends it. No shop or cosmetic gating.
        public float PlayerUltimate { get; private set; }
        public float RivalUltimate { get; private set; }
        public const float UltimateChargeLead = .14f;
        public const float UltimateSpeed = 1.3f, RivalUltimateGain = .075f;
        public static float UltimateGain(Timing g) => g >= Timing.Perfect ? .12f : g >= Timing.Excellent ? .09f : g >= Timing.Great ? .07f : g >= Timing.Good ? .05f : .02f;
        bool ultimateBall, playerUltCharged, rivalUltCharged;
        public string LastRivalStrike { get; private set; }
        static readonly bool HonestContactEnabled = System.Environment.GetEnvironmentVariable("HERO_BASELINE") != "1";
        /// Brief freeze on contact that sells the impact; scaled by how clean the hit was.
        float hitStop;
        TennisJuice juice;
        public TennisJuice Juice => juice;
        /// The rival struck the ball for real (strings met it): the rival's visuals key off this.
        public static event System.Action OpponentStruck;

        /// How late (real seconds; negative early) the swing that made the pending hit was.
        float hitLateness;

        /// Longest a decided hit may let the ball fly on to meet the racket.
        const float MaxContactLead = .2f, MinContactLead = .05f;

        /// A hit has been decided. A ball that crossed the strings is struck where it is. An
        /// assisted hit is judged by timing and reach, before the racket has got there, so the
        /// ball flies on to the moment it passes where the swing will bring the strings: the
        /// swing is hurried to arrive then, and steered onto the ball. The ball leaves the
        /// strings the player sees it meet, instead of from thin air.
        void Strike(TennisHit hit, Vector2 faceOffset, bool onStrings)
        {
            float lead = 0; Vector3 meet = BallPosition;
            if (!onStrings)
            {
                lead = PlanContact(Player, MinContactLead, Mathf.Clamp(Player.TimeToContact * 1.6f, MinContactLead, MaxContactLead), out meet);
                Player.PaceToContact(lead); Player.GuideContact(meet, lead);
            }
            if (!Player.Provisional && lead <= 0) { ReturnBall(hit, faceOffset); return; }
            pending = new PendingHit { Active = true, Hit = hit, Face = faceOffset, At = meet, Age = 0, Lead = lead };
        }

        /// When, within the next few frames, the ball passes closest to where `actor`'s swing
        /// will put its strings; and where the ball is then.
        float PlanContact(TennisActor actor, float soonest, float latest, out Vector3 meet)
        {
            Vector3 aim = actor.AuthoredContact;
            Vector3 p = BallPosition, v = BallVelocity; float spin = BallSpin;
            float best = float.MaxValue, when = soonest; meet = p;
            for (float t = 0; t <= latest + 1e-4f; t += TennisBall.Step)
            {
                if (t >= soonest - 1e-4f)
                {
                    float d = (p - aim).sqrMagnitude;
                    if (d < best) { best = d; when = t; meet = p; }
                }
                TennisBall.Integrate(ref p, ref v, spin, TennisBall.Step, ballCurve);
                if (p.y < TennisRules.BallRadius && v.y < 0 && TennisVenue.OverDeck(p)) { p.y = TennisRules.BallRadius; TennisBall.Bounce(ref v, ref spin, bounceRestitution); }
            }
            return when;
        }

        /// Where the ball will be `seconds` from now, bounces included.
        Vector3 PredictBall(float seconds)
        {
            Vector3 p = BallPosition, v = BallVelocity; float spin = BallSpin;
            for (float t = 0; t < seconds; t += TennisBall.Step)
            {
                TennisBall.Integrate(ref p, ref v, spin, TennisBall.Step, ballCurve);
                if (p.y < TennisRules.BallRadius && v.y < 0 && TennisVenue.OverDeck(p)) { p.y = TennisRules.BallRadius; TennisBall.Bounce(ref v, ref spin, bounceRestitution); }
            }
            return p;
        }

        void ResolvePending(float dt)
        {
            if (!pending.Active) return;
            pending.Age += dt;
            if (System.Environment.GetEnvironmentVariable("HERO_DEBUG") == "1" && pending.Age <= dt + 1e-5f)
                Debug.Log($"[RallyDbg] start at={pending.At:F2} lead={pending.Lead:F3} strings={Player.SweetSpot.position:F2} planned={Player.AuthoredContact:F2} player={Player.transform.position:F2} kind={Player.Kind}");
            if (!Player.Swinging || (Player.Provisional && pending.Age > ConfirmGrace * SpeedScale))
            { pending.Active = false; Player.ReleaseContact(); return; }
            // Wait for confirmation, and for the ball to reach the racket.
            // The racket's last pose was a step ago, so the ball leaves one step after the lead:
            // on the frame the strings were posed on it.
            float due = pending.Lead > 0 ? pending.Lead + TennisBall.Step - 1e-4f : 0;
            if (Player.Provisional || pending.Age < due) return;
            pending.Active = false;
            // Honest contact: the visible strings (posed for this step) must be at the ball.
            float gap = Vector3.Distance(Player.SweetSpot.position, pending.At);
            LastContactGap = gap;
            if (System.Environment.GetEnvironmentVariable("HERO_DEBUG") == "1") Debug.Log($"[RallyDbg] release gap={gap:F3} strings={Player.SweetSpot.position:F2} at={pending.At:F2} ttc={Player.SignedTimeToContact:F3}");
            if (gap > HonestContactGap && HonestContactEnabled) { HonestMisses++; honestRejected = true; Player.ReleaseContact(); return; }
            BallPosition = pending.At;
            ReturnBall(pending.Hit, pending.Face);
            // Fly the ball on for the time the confirmation took, so the shot is exactly where
            // it would have been had the phone confirmed instantly.
            Vector3 p = BallPosition, v = BallVelocity;
            for (float t = 0; t < pending.Age - due; t += TennisBall.Step) TennisBall.Integrate(ref p, ref v, BallSpin, TennisBall.Step, ballCurve);
            BallPosition = previousBall = p; BallVelocity = v;
        }

        /// How far the strings were from the ball at each contact, before it is placed on them
        /// (tests and tuning: a visible gap reads as a force field instead of a hit).
        public readonly ContactGaps PlayerGaps = new(), OpponentGaps = new();
        public sealed class ContactGaps
        {
            public int Count, Visible; public float Sum, Max;
            public float Mean => Count > 0 ? Sum / Count : 0;
            public void Add(float gap) { Count++; Sum += gap; Max = Mathf.Max(Max, gap); if (gap > .12f) Visible++; }
            public override string ToString() => $"n={Count} mean={Mean:0.00}m max={Max:0.00}m visible(>12cm)={Visible}";
        }
        /// Who struck the ball in the step just simulated (null otherwise); see UpdateBallVisual.
        TennisActor contactHitter;

        void RecordGap(bool player, float gap)
        {
            (player ? PlayerGaps : OpponentGaps).Add(gap);
            if (LogContactGaps) Debug.Log($"[Gap] {(player ? "P" : "O")} {gap:0.00} {(player ? Player : Opponent).Kind} ball={BallPosition} {(player ? Player : Opponent).GuideState}");
        }
        public static bool LogContactGaps;

        /// The court side (-1/+1 in x) of the player's weaker wing, from how often each side has
        /// broken down this match (with a small prior toward the backhand), 0 before any evidence.
        float WeakSide()
        {
            if (sideBalls[0] + sideBalls[1] + sideMisses[0] + sideMisses[1] < 4) return 0;
            float Rate(int i) => (sideMisses[i] + (i == 1 ? 1.2f : 1f)) / (sideBalls[i] + sideMisses[i] + 3f);
            bool backhandWeaker = Rate(1) >= Rate(0);
            float backhandSign = Player.LeftHanded ? 1 : -1;
            return backhandWeaker ? backhandSign : -backhandSign;
        }

        /// Which wing a ball at `ballX` is on for the player: 0 forehand, 1 backhand.
        int Wing(float ballX) => (ballX < Player.transform.position.x) != Player.LeftHanded ? 1 : 0;

        void ReturnBall(TennisHit hit, Vector2 faceOffset)
        {
            ballCurve = 0;
            sideBalls[Wing(BallPosition.x)]++;
            // The ball leaves from the strings. An assisted hit is judged by timing and reach,
            // so the ball can be up to a metre from the racket at that instant; launching it
            // from there read as a force field instead of a hit.
            Vector3 strings = Player.SweetSpot.position + Vector3.forward * (TennisRules.BallRadius * 1.2f);
            RecordGap(true, Vector3.Distance(strings, BallPosition));
            if (Vector3.Distance(strings, BallPosition) < 1.4f) BallPosition = previousBall = strings;
            squash = 1; squashAxis = Vector3.forward; contactHitter = Player;
            hitStop = TennisRules.HitStopFor(TennisRules.Grade(hit.Timing), Streak + 1 >= TennisRules.SuperchargeStreak && TennisRules.Extends(TennisRules.Grade(hit.Timing)));
            if(Player.Overhead) hit.Speed=Mathf.Min(26,hit.Speed*1.1f);
            // A dive only gets the ball back: weak, loose, and it costs the legs.
            if(Player.Kind==TennisActor.Stroke.Dive)
            {
                Dives++;
                hit.Quality=Mathf.Min(hit.Quality,TennisRules.DiveQualityCap);
                hit.Speed=TennisRules.ShotSpeed(hit.Quality,Player.Power,Stamina)*.85f;
                hit.ErrorDegrees+=3;
                // Stamina is paid on the dive attempt, including misses.
            }
            // Grade the contact, then reward three well-timed balls in a row.
            LastGrade = TennisRules.Grade(hit.Timing);
            LastFaceOffset = faceOffset;
            Streak = Player.Kind != TennisActor.Stroke.Dive && TennisRules.Extends(LastGrade) ? Streak + 1 : 0;
            LastWasSupercharged = Streak >= TennisRules.SuperchargeStreak;
            if (LastWasSupercharged) { hit = TennisRules.Supercharge(hit); Streak = 0; }
            // Say which way a swing was off, so timing can be learned from each ball.
            string timingWord = TennisRules.TimingWord(hitLateness);
            if (hud) hud.ShowGrade(LastGrade, LastWasSupercharged,
                (timingWord.Length > 0 ? timingWord + "  ·  " : "") + $"{Player.StrokeLabel.ToUpperInvariant()}  ·  {hit.Speed * 3.6f:0} KM/H");
            if (hitMap) hitMap.Record(faceOffset, LastGrade, LastWasSupercharged);
            LastLateness = hitLateness;
            ContactMade?.Invoke(new Vector2(faceOffset.x / TennisRules.StringHalfWidth, faceOffset.y / TennisRules.StringHalfHeight), LastGrade, LastWasSupercharged);
            if (coach) { coach.Record(LastGrade); if (Hits >= 3 && NativeControlled) coach.Offer(TennisCoach.Tip.Timing); }
            LastHit = hit; Hits++; Returns++; consumedStroke = true; incoming = false; bounces = 0; opponentShot = default;
            sinceStrike = 0; opponentMissReason = null;
            serveInFlight = false; bounceRestitution=.75f;
            RallyShots++;
            overheadAttempted = false; TrackingOverhead = false;
            float error = ((float)random.NextDouble()*2-1) * hit.ErrorDegrees;
            // The confirmed racket face chooses direction; depth is an independent control.
            // Contact quality adds bounded scatter around that target.
            Vector3 target=ShotAimTarget;
            float scatter = Mathf.Lerp(.55f, .05f, hit.Quality);
            target.x += Mathf.Clamp(error / 12f, -1, 1) * scatter;
            target.z += ((float)random.NextDouble() * 2 - 1) * scatter;
            BallSpin = Player.Kind == TennisActor.Stroke.Dive ? 0 : SpinFor(Player.Kind, Player.Power);
            BallVelocity = Player.Kind == TennisActor.Stroke.Dive
                ? TennisAbilities.LobVelocity(BallPosition, new Vector3(target.x, TennisRules.BallRadius, 6.5f), 4.2f)
                : TennisRules.RallyVelocity(BallPosition,target,hit.Speed,BallSpin,hit.Quality);
            Feedback = $"{TennisRules.GradeLabel(LastGrade)} · {Player.StrokeLabel} · {hit.Label} · {hit.Speed*3.6f:0} km/h"
                + (Streak > 0 ? $"\n{Streak} clean in a row — {TennisRules.SuperchargeStreak - Streak} to supercharge" : "");
            ballRenderer.material.color = LastWasSupercharged ? new Color(.7f,1,1) : Color.Lerp(new Color(1,.8f,.7f), Color.white, hit.Quality);
            bool smash = Player.Kind == TennisActor.Stroke.Smash;
            // ULTIMATE (equal meter): fired on a real, confirmed contact once the meter is full.
            bool ult = TennisAbilities.UltimatesEnabled && UltimateArmed && EligibleUltimateContact && PlayerUltimate >= 1;
            if (ult)
            {
                PlayerUltimate = 0; ultimateBall = true; playerUltCharged = false;
                ApplySelectedUltimate(target, hit.Speed);
                UltimateArmed = false;
                if (ballTrail) { ballTrail.startWidth = .45f; ballTrail.startColor = new Color(1, .75f, .25f); }
            }
            else { PlayerUltimate = 0; ultimateBall = false; if (ballTrail) { ballTrail.startWidth = .12f; ballTrail.startColor = Color.white; } }
            fx.Contact(BallPosition, LastGrade, LastWasSupercharged || ult, smash);
            if (smash) hitStop = Mathf.Max(hitStop, TennisRules.HeavyHitStop);
            if (juice && ult) { juice.UltimateHit(hud ? hud.PlayerName : "YOU", UltimateName); hitStop = Mathf.Max(hitStop, TennisRules.HeavyHitStop); }   // body + ball hold on the ultimate contact
            else if (juice && (smash || LastGrade >= Timing.Perfect || LastWasSupercharged)) juice.Trigger(smash ? TennisJuice.Beat.Smash : TennisJuice.Beat.Perfect, Player.transform, LastWasSupercharged);
            // Score80 C: the FOV pulse is for Perfect-class contact only (<= 2 deg), via Trigger / UltimateHit.
            if (LastWasSupercharged) Player.SetExpression(TennisActor.Expression.Surprised, .7f);
            else if (LastGrade >= Timing.Excellent) Player.SetExpression(TennisActor.Expression.Happy, .8f);
            sounds.Hit(LastGrade, Player.Power, smash);
            if (NativeControlled) Haptics.Strike(hit.Quality, LastWasSupercharged);
            Opponent.SplitStep(); shotAimLocked = false;
        }

        /// A called whiff: bigger than a routine hit (funny failure) — swish ring, dust, slide whistle,
        /// a short push-in with a touch of slow motion, and a WHIFF! banner.
        void WhiffJuice()
        {
            if (fx) fx.Whiff(Player.transform.position, Player.SweetSpot.position);
            if (sounds) sounds.Whiff();
            if (juice) juice.Trigger(TennisJuice.Beat.Whiff, Player.transform);
        }

        /// Put a live rally ball in play. Clears the serve flag: a ball injected here is by
        /// definition not a serve, and leaving the flag set got the next bounce judged against
        /// the service box and faulted. The serve paths set the flag themselves afterwards.
        public void InjectBall(Vector3 position, Vector3 velocity)
        {
            ballCurve = 0;
            Flow = Phase.Rally; BallPosition = previousBall = position; BallVelocity = velocity; BallSpin = 0;
            incoming = true; shotAimLocked = false; bounces = 0; resetTimer = 0; consumedStroke = false; honestRejected = false; serveInFlight = false; faultDelay = 0;
            serveLaunchPending = false; serveSwingIn = -1; pending.Active = false;
            StartReading();
            if (ball) ball.position = renderedBall = position;
        }

        /// Start the next point from whichever end is serving.
        void BeginPoint() { tauntHold = 0; playerWonPoint = false; pointEmoteChosen = false; pendingPointEmote = null;
            ResetPointAbilities();
            lastRivalLob = false; serveOnsetPending = false; shotAimLocked = false;
            Stamina=1; SecondServe=false; consumedStroke=false;
            bounces=0; bounceRestitution=.75f; incoming=true; honestRejected=false; serveInFlight=false; faultDelay=0; serveLaunchPending=false; serveSwingIn=-1;
            serveTimer=0; phaseTimer=0; BallVelocity=Vector3.zero; BallSpin=0; RallyShots=0; replayDue=-1;
            resetTimer=0; LastWasSupercharged=false; sinceStrike=0; opponentMissReason=null;
            if (ballRenderer) ballRenderer.material.color=Color.white;
            if (ballTrail) ballTrail.Clear();
            matchPoint = IsMatchPoint(Match);
            if (crowd) crowd.Hush(matchPoint);
            if (audio) audio.OnPointStarting(Match, matchPoint);
            if (matchPoint) { banner.text = "MATCH POINT"; bannerUntil = HudClock.Now + 2.2f; }
            windup=-1; serveCommitted=false; ServeNudge=0;
            serveX=TennisRules.ServerStanceX(true, Match.DeuceCourt);
            receiveX=TennisRules.ReceiverStanceX(false, Match.DeuceCourt);
            if (Match.PlayerServes)
            {
                Flow=Phase.PlayerServeHold;
                if (coach && NativeControlled) coach.Offer(TennisCoach.Tip.Serve);
                Feedback=$"Your serve to the {(Match.DeuceCourt ? "deuce" : "ad")} court — aim on your phone, press TOSS, swing at the top";
            }
            else
            {
                Flow=Phase.OpponentServe;
                // Stand the receiver where the serve is legally required to arrive; they are
                // free to move from there once the ball is live.
                Player.transform.position=new Vector3(
                    TennisRules.ReceiverStanceX(false, Match.DeuceCourt),
                    Player.transform.position.y, TennisRules.BaselineZ);
                moveVelocity=Vector2.zero;
                BallPosition=previousBall=Opponent.transform.TransformPoint(new Vector3(.35f,1.2f,.3f));
                ball.position=renderedBall=BallPosition;
                Feedback="Opponent serving — move into position, then swing";
            }
            RecordingPointStarted?.Invoke();
        }

        /// Either player one point from the set.
        public static bool IsMatchPoint(TennisMatch m)
        {
            if (m.Complete) return false;
            bool Wins(bool player) { var copy = m; copy.AwardPoint(player); return copy.Complete; }
            return Wins(true) || Wins(false);
        }

        /// How long the results card waits for a swing before starting the next match itself.
        const float ResultsHold = 30f;
        void NewMatch() { ResetMatchAbilities(); matchReported = false; ResetMatchStats(); match = TennisMatch.New(true, MatchSets, MatchGames); sideMisses[0] = sideMisses[1] = sideBalls[0] = sideBalls[1] = 0; Hits=0; Misses=0; LongestRally=0; resetTimer=0; if (coach) coach.HideResults(); BeginPoint(); }

        /// Opponent's serve: varied between wide, body and T, sometimes faulted, and a slower
        /// kick serve second -- every serve used to land in the middle of the box.
        void ServeFromOpponent()
        {
            var plan = TennisOpponent.PlanServe(Match.DeuceCourt, SecondServe, Profile,
                (float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble());
            // Struck from above the head, like the player's, and lofted enough to clear.
            Vector3 start=Opponent.transform.position+Vector3.up*TennisRules.ServeContactHeight
                +Opponent.transform.forward*.28f;
            Vector3 strings=Opponent.SweetSpot.position+Vector3.back*(TennisRules.BallRadius*1.2f);
            if(Vector3.Distance(strings,start)<1.2f) start=strings;
            bounceRestitution=.60f;
            // Nearly every serve from a weaker opponent is returnable from where the player chose
            // to stand: if this one is not, ease it toward them and take pace off until it is.
            // The stronger the server, the more often the unreturnable one is let through.
            if (!plan.Fault && random.NextDouble() > .05 + .3f * Profile.Skill * Profile.Skill)
            {
                Vector3 me = Player.transform.position;
                for (int i = 0; i < 14 && !TennisRules.ServeReachable(start, TennisRules.ServeVelocity(start, plan.Landing, plan.Speed, plan.Spin), plan.Spin, bounceRestitution, me.x, me.z); i++)
                {
                    plan.Landing = TennisRules.IntoServiceBox(Vector3.Lerp(plan.Landing, new Vector3(me.x, plan.Landing.y, plan.Landing.z), .2f), false, Match.DeuceCourt, .2f);
                    plan.Speed *= .95f;
                }
            }
            InjectBall(start,TennisRules.ServeVelocity(start,plan.Landing,plan.Speed,plan.Spin));
            BallSpin=plan.Spin;
            serveInFlight=true; serveFromNearSide=false;
            RallyShots = 1;
            fx.OpponentContact(start); sounds.RivalHit(.8f); contactHitter = Opponent; OpponentStruck?.Invoke();
            Player.SplitStep();
        }

        /// Send the opponent's chosen shot on its way, guaranteed to clear the net so its
        /// intended target is the thing that decides the point.
        void SendFromOpponent(Vector3 landing, float speed, float spin, TennisReturnKind kind = TennisReturnKind.Drive)
        {
            bounceRestitution=.75f; serveInFlight=false;
            // A rally shot, not a serve: a real arc over the net (the flattest net-clearing line read as a laser).
            if (kind == TennisReturnKind.Lob) {
                InjectBall(BallPosition, TennisAbilities.LobVelocity(BallPosition, landing, 4.4f));
                BallSpin = 0;
            } else {
                float clearance = kind == TennisReturnKind.Drop ? .18f : .28f;
                InjectBall(BallPosition, TennisRules.RallyArcVelocity(BallPosition, landing, speed, spin, clearance));
                BallSpin = spin;
            }
        }

        /// A ring on the court (the tutorial's targets), drawn like the aim and landing rings.
        public LineRenderer MakeMarker(string label, Color color) => MakeLine(label, color, .09f);
        public static void DrawRing(LineRenderer line, Vector3 centre, float radius) => Ring(line, centre, radius);

        /// Hold a clean court between lesson attempts, independent of serving rules.
        public void PrepareLesson()
        {
            Flow = Phase.PointOver; resetTimer = 1e6f; faultDelay = 0; replayDue = -1;
            serveLaunchPending = serveInFlight = false; serveSwingIn = -1; pending.Active = false;
            BallVelocity = Vector3.zero; BallPosition = new Vector3(0, -10, 0);
            moveVelocity = Vector2.zero; ServeNudge = 0; MoveInput = 0;
            Player.CancelSwing(); Opponent.CancelSwing();
            Player.transform.position = new Vector3(0, Player.transform.position.y, TennisRules.BaselineZ);
            Player.Prepare(0, false);
            if (ballTrail) ballTrail.Clear();
        }

        /// The coach feeds a ball to the player's forehand or backhand side (drills).
        public void Feed(bool backhandSide, float speed = 14f)
        {
            if (!Player || !Opponent) return;
            bool left = backhandSide != NativeSportsSession.Left;
            float x = Mathf.Clamp(Player.transform.position.x + (left ? -1.05f : 1.05f), -3.7f, 3.7f);
            Vector3 from = Opponent.transform.position + new Vector3(.3f, 1.15f, -.7f);
            Vector3 to = new Vector3(x, TennisRules.BallRadius, TennisRules.BaselineZ + 4.6f);
            Opponent.Swing(.45f, false, TennisActor.Stroke.Drive);
            RallyShots = 1; faultDelay = 0; serveInFlight = false; opponentShot = default;
            InjectBall(from, TennisRules.RallyVelocity(from, to, speed, .3f, .75f));
        }

        /// The player serves next (drills): a fresh serve from the deuce court.
        public void StartPlayerServe()
        {
            match.PlayerServes = true; match.PlayerPoints = match.OpponentPoints = 0;
            BeginPoint();
        }

        LineRenderer MakeLine(string label,Color color,float width) {
            var line=new GameObject(label).AddComponent<LineRenderer>();
            line.transform.SetParent(transform); line.useWorldSpace=true;
            line.sharedMaterial=GolfArcade.Course.HoleView.Mat(color);
            line.startWidth=line.endWidth=width;
            line.numCapVertices=3; line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            line.positionCount=33; return line;
        }

        /// A circle where the ball will actually land reads far better than a line through
        /// the air: it tells you where to be, not where the ball has been.
        void UpdateTrajectory() {
            Vector3 landing = BallPosition;
            bool showLanding = Flow == Phase.Rally && incoming && faultDelay <= 0
                && TennisRules.PredictLanding(BallPosition,BallVelocity,out landing,BallSpin,ballCurve);
            landingRing.enabled = showLanding;
            if (showLanding)
            {
                // Tight when the ball is close, wide when it is still far away.
                float certainty = Mathf.Clamp01(1 - Vector3.Distance(BallPosition,landing)/16);
                float radius = Mathf.Lerp(.85f,.34f,certainty);
                Ring(landingRing, landing + Vector3.up*.02f, radius);
                landingRing.startColor = landingRing.endColor =
                    TennisRules.BounceIsIn(landing) ? new Color(.1f,.9f,1) : new Color(1,.42f,.3f);
            }
            bool showAim = (Flow == Phase.Rally && incoming) || Flow == Phase.PlayerServeHold || Flow == Phase.PlayerServeToss;
            aimRing.enabled = showAim;
            if (!showAim) return;
            Vector3 target = Flow == Phase.Rally
                ? ShotAimTarget
                : TennisRules.ServeAimPoint(ServeAim, true, Match.DeuceCourt);
            target.y=.06f;
            Ring(aimRing, target, .4f);
        }

        static void Ring(LineRenderer line, Vector3 centre, float radius) {
            for(int i=0;i<33;i++) {
                float angle=i*Mathf.PI*2/32;
                line.SetPosition(i,centre+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*radius);
            }
        }

        /// Draw the ball between the last two simulation steps. Physics runs at a fixed
        /// 120Hz, the display at its own rate; interpolating by the leftover step fraction
        /// removes the judder without the lag the old exponential smoothing added.
        void UpdateBallVisual()
        {
            if (!ball) return;
            float alpha = ManualSimulation ? 1 : Mathf.Clamp01(accumulator * 120f);
            renderedBall = Vector3.LerpUnclamped(previousBall, BallPosition, alpha);
            // The frame of a contact (and any hit-stop after it) shows the ball on the face that
            // struck it, leaving the way it was hit.
            if (contactHitter && BallVelocity.sqrMagnitude > .01f)
                renderedBall = contactHitter.SweetSpot.position + BallVelocity.normalized * (TennisRules.BallRadius * 1.1f);
            ball.position = renderedBall;
            // Squash and stretch: flattened for a few frames against whatever it just hit,
            // and drawn out a little along its flight at pace. Cheap, and it is most of what
            // makes contact read as contact.
            squash = Mathf.MoveTowards(squash, 0, Time.deltaTime / .09f);
            float speed = BallVelocity.magnitude;
            float stretch = 1 + Mathf.Clamp01((speed - 12) / 30f) * .35f;
            Vector3 along = speed > .5f ? BallVelocity / speed : Vector3.up;
            Vector3 shapeAxis = squash > .01f ? squashAxis : along;
            float lengthwise = squash > .01f ? 1 - .38f * squash : stretch;
            float across = squash > .01f ? 1 + .22f * squash : 1 / Mathf.Sqrt(stretch);
            ball.rotation = Quaternion.FromToRotation(Vector3.up, shapeAxis);
            ball.localScale = new Vector3(across, lengthwise, across) * (TennisRules.BallRadius * 2);
            // Trail colour tells the shot: warm felt glow normally, hotter and longer at pace,
            // electric cyan when supercharged.
            float heat = Mathf.Clamp01((speed - 15) / 20f);
            Color head = LastWasSupercharged && !incoming ? new Color(.45f, .95f, 1f, .95f) : Color.Lerp(new Color(1f, 1f, .55f, .45f), new Color(1f, .8f, .35f, .8f), heat);
            ballTrail.startColor = head; ballTrail.endColor = new Color(head.r, head.g, head.b, 0);
            ballTrail.time = Mathf.Lerp(.14f, .26f, heat) * (LastWasSupercharged && !incoming ? 1.6f : 1f);
            // Spin on the felt: rolls forward for topspin, backward for slice, and a little
            // with flight regardless, so a flat ball is not frozen.
            Vector3 travel = new Vector3(BallVelocity.x, 0, BallVelocity.z);
            if (travel.sqrMagnitude > .25f)
            {
                Vector3 axis = Vector3.Cross(Vector3.up, travel.normalized);
                float rate = (BallSpin * 3600f + travel.magnitude * 40f) * Time.deltaTime;
                ballSpinner.rotation = Quaternion.AngleAxis(rate, axis) * ballSpinner.rotation;
            }
        }

        /// The timing cue: a ring on the ball that closes to the moment to START the swing
        /// (the stroke then reaches contact as the ball arrives). Makes timing learnable at a
        /// glance, the way a rhythm game does, without adding anything to the court.
        /// CueLead is the stroke's own time from the start of the swing to contact (with the
        /// phone's onset detection), so a swing started as the cue closes meets the ball as it
        /// arrives beside the player -- exactly what the timing grade scores.
        public static readonly float CueLead = TennisRules.SweetTime * Mathf.Lerp(.58f, .40f, .65f) / TennisRules.StrokeDuration
            + TennisRules.ServeOnsetLatency * GameSpeed;
        public const float CueSpan = 1.0f;
        void UpdateCue()
        {
            if (!fx) return;
            var cam = GameplayCamera;
            if (Flow == Phase.PlayerServeToss && !Player.Swinging && !serveLaunchPending)
            {
                float ideal = TennisRules.ServeApex + TennisRules.ServeOnsetLatency;
                fx.Cue(phaseTimer < ideal + .12f, renderedBall, cam, Mathf.Clamp01((ideal - phaseTimer) / .6f));
                if (hud) hud.SwingCue(false, 1);
                return;
            }
            bool show = Flow == Phase.Rally && incoming && !Player.Swinging && faultDelay <= 0 && BallVelocity.z < -1;
            float toContact = show ? (BallPosition.z - (Player.transform.position.z + .65f)) / -BallVelocity.z : 0;
            show &= toContact > .05f && toContact < CueLead + CueSpan;
            float closing = Mathf.Clamp01((toContact - CueLead) / CueSpan);
            fx.Cue(show, renderedBall, cam, closing);
            if (hud) hud.SwingCue(show, closing);
        }

        Vector3 cameraLook; bool cameraFramed;
        /// Seconds of opening flyover left: the camera sweeps in over the resort to the
        /// player before the first serve. Game time, so it waits while the session is paused.
        TennisPresentation presentation;
        public const float IntroSeconds = TennisPresentation.Length;
        /// Play runs this much faster than real time: balls, feet and strokes alike. The
        /// phone's latencies are real seconds and are converted wherever they meet game time.
        public const float GameSpeed = 1.2f;
        /// The serve is the slowest part of the game: dribble, the slow rise of the arm and the
        /// high toss play at ServePace, and the rally returns to full pace once the ball is struck.
        bool SlowServe => Flow == Phase.PlayerServeHold || Flow == Phase.PlayerServeToss || Flow == Phase.OpponentServe;
        float Pace => AimPractice ? .85f : SlowServe ? TennisRules.ServePace : GameSpeed;
        float SpeedScale => ManualSimulation ? 1 : Pace;
        public bool IntroPlaying => presentation && presentation.Playing && !ManualSimulation && !AutoPlay;
        bool cutHeld, juiceAttached;
        void UpdateCamera(bool immediate)
        {
            var camera = GameplayCamera ? GameplayCamera : Camera.main; if (!camera || !Player) return;
            TennisShoulderCamera.Frame(Player.transform.position, Player.LeftHanded, BallPosition,
                Flow == Phase.Rally && incoming && TrackingOverhead, camera.aspect, out Vector3 target, out Vector3 look, out float fov);
            if (IntroPlaying && presentation.Drive(camera, target, look, fov, calibration != null ? 0 : Time.deltaTime)) { cameraFramed = false; return; }
            if (presentation && presentation.Playing) presentation.Finish();
            if (juice) { juice.BallLive = Flow == Phase.Rally; juice.NoteBallVelocity(BallVelocity); juice.EndLiveShot(); }
            float rate = 1 - Mathf.Exp(-Time.deltaTime * 9);
            bool snap = immediate || !cameraFramed;
            camera.transform.position = snap ? target : Vector3.Lerp(camera.transform.position, target, rate);
            cameraLook = snap ? look : Vector3.Lerp(cameraLook, look, rate);
            camera.fieldOfView = fov;
            camera.transform.LookAt(cameraLook);
            cameraFramed = true;
        }

        void BuildHud()
        {
            var canvas = new GameObject("Tennis HUD").AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.gameObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvas.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1280,720);
            var font = Resources.Load<Font>("Tennis/UI/Fonts/Rubik-Bold") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Text Label(string name, Vector2 anchor, Vector2 size, Vector2 offset, int fontSize)
            {
                var go = new GameObject(name); go.transform.SetParent(canvas.transform,false);
                var text = go.AddComponent<Text>(); text.font = font; text.fontSize = fontSize; text.color = Color.white;
                text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
                var rect = text.rectTransform; rect.anchorMin = rect.anchorMax = anchor; rect.pivot = anchor; rect.sizeDelta = size; rect.anchoredPosition = offset;
                foreach (var d in new[] { new Vector2(2,-2), new Vector2(-2,2) }) { var o = go.AddComponent<Outline>(); o.effectColor = TennisHud.Navy; o.effectDistance = d; }
                go.AddComponent<Shadow>().effectDistance = new Vector2(0,-3.5f); return text;
            }
            hud = gameObject.AddComponent<TennisHud>();
            hud.Build(canvas);
            // Tracking and pause notices only; everything else the HUD shows graphically.
            status = Label("Tracking notice",new Vector2(.5f,1),new Vector2(900,40),new Vector2(0,-24),20);
            status.color = new Color(1,.78f,.35f); status.text = "";
            banner = Label("Moment banner",new Vector2(.5f,1),new Vector2(700,60),new Vector2(0,-64),34);
            banner.text = ""; banner.color = new Color(1,.9f,.55f);
            flash = new GameObject("Supercharge flash").AddComponent<Image>();
            flash.transform.SetParent(canvas.transform, false); flash.raycastTarget = false;
            flash.rectTransform.anchorMin = Vector2.zero; flash.rectTransform.anchorMax = Vector2.one;
            flash.rectTransform.offsetMin = flash.rectTransform.offsetMax = Vector2.zero;
            flash.color = new Color(.85f, 1, 1, 1); flash.canvasRenderer.SetAlpha(0);
            hitMap = gameObject.AddComponent<TennisHitMap>();
            hitMap.Build(canvas, hud.MatchLayer);
            coach = gameObject.AddComponent<TennisCoach>();
            coach.Build(canvas);
        }

        // HUD text is rebuilt only when what it shows changes. Rebuilding strings every frame
        // was a steady source of garbage and forced a canvas rebuild every frame.
        string shownPause, shownWarning; float bannerUntil;

        void UpdateHud()
        {
            hud.Refresh(this);
            status.enabled = !ScoreOnlyText || Time.timeScale == 0;
            banner.enabled = !ScoreOnlyText;
            string pause = NativeControlled ? NativeSportsSession.PauseReason : null;
            string warning = NativeControlled ? NativeSportsSession.TrackingWarning : null;
            if (!ReferenceEquals(pause, shownPause) || !ReferenceEquals(warning, shownWarning))
            {
                shownPause = pause; shownWarning = warning;
                status.text = !string.IsNullOrEmpty(pause) ? pause : warning ?? "";
            }
            if (banner.text.Length > 0 && HudClock.Now > bannerUntil && !ReplayPlaying) banner.text = "";
            float f = fx ? fx.Flash * .25f : 0;   // Phase 4: a lift, never a white-out of the hero
            if (Mathf.Abs(f - shownFlash) > .002f) { shownFlash = f; flash.canvasRenderer.SetAlpha(f); }
        }
    }
}
