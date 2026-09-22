using GolfArcade.Game;
using UnityEngine;
using UnityEngine.UI;

namespace GolfArcade.Tennis
{
    /// Singles match. Ball simulation runs at a fixed 120 Hz; players are posed once per
    /// rendered frame, or every step while a swing could meet the ball.
    public sealed class TennisGame : MonoBehaviour
    {
        public bool FemalePlayer;
        public bool ManualSimulation;
        public bool NativeControlled;
        /// Benchmark mode: the player swings for themselves so a device run can be timed
        /// without anyone holding the phone.
        public bool AutoPlay;
        public bool Initialized { get; private set; }
        public Camera GameplayCamera { get; private set; }
        public void Refeed() { resetTimer=0; BeginPoint(); }
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
        /// How far the assist wants to shift the player, in court metres, and where the ball
        /// is predicted to arrive. Exposed for the motion servo, the HUD and tests.
        public float AssistOffset { get; private set; }
        public float PredictedInterceptX { get; private set; } = -100;
        public bool Sprint;
        public bool ReplayPlaying => replay && replay.Playing;
        Transform ball, ballSpinner;
        Renderer ballRenderer;
        LineRenderer landingRing, aimRing;
        Vector3 previousBall, renderedBall;
        Text title, status, help, hitMarker, banner;
        Image staminaFill;
        float accumulator, resetTimer, charge;
        float hitMarkerAt = -99;
        TennisHitMap hitMap;
        TennisFx fx;
        TennisSounds sounds;
        TennisReplay replay;
        TennisResortCrowd crowd;
        TennisCoach coach;
        int bounces;
        float predictedOpponentX;
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
        public float OpponentDifficulty = TennisOpponent.DefaultDifficulty;
        readonly System.Random random = new(2701);
        Vector3 previousRacket;
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
            crowd = gameObject.AddComponent<TennisResortCrowd>();
            Player = new GameObject("Player — permanent standard").AddComponent<TennisActor>();
            Player.transform.position = new Vector3(0, .035f, -11.2f);
            Player.Build(FemalePlayer, GolferStyle.SkinColor);
            Opponent = new GameObject("Opponent — permanent standard").AddComponent<TennisActor>();
            Opponent.transform.SetPositionAndRotation(new Vector3(0, .035f, 11.2f), Quaternion.Euler(0,180,0));
            Opponent.Build(!FemalePlayer, new Color(.52f,.31f,.18f), false);
            Opponent.Motion = TennisActor.Style.Rival;
            BuildBall();
            landingRing=MakeLine("Predicted landing",new Color(.1f,.9f,1),.05f);
            aimRing=MakeLine("Shot aim target",new Color(1,.85f,.1f),.06f);
            var camera = Camera.main;
            if (!camera) { camera = new GameObject("Tennis gameplay camera").AddComponent<Camera>(); camera.tag = "MainCamera"; camera.gameObject.AddComponent<AudioListener>(); }
            GameplayCamera=camera;
            camera.fieldOfView = 56; camera.nearClipPlane = .08f; camera.farClipPlane = 600;
            camera.backgroundColor = new Color(.49f,.73f,.88f); camera.clearFlags = CameraClearFlags.SolidColor;
            camera.allowMSAA = true;
            camera.allowHDR = TennisQuality.Current == TennisQuality.Tier.High;
            if (!camera.GetComponent<TennisGrade>()) camera.gameObject.AddComponent<TennisGrade>();
            var light = new GameObject("Resort sun").AddComponent<Light>(); light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(48,-35,0); light.shadows = LightShadows.Soft;
            TennisLook.LightScene(light);
            TennisLook.AddContactShadow(Player.transform, .55f, .45f).HeightOverride = 0;
            TennisLook.AddContactShadow(Opponent.transform, .55f, .45f).HeightOverride = 0;
            TennisLook.AddContactShadow(ball, .16f, .55f).FadeHeight = 4f;
            fx = new GameObject("Tennis effects").AddComponent<TennisFx>(); fx.transform.SetParent(transform); fx.Build();
            sounds = TennisSounds.Create(transform);
            replay = gameObject.AddComponent<TennisReplay>();
            replay.Build(new[] { Player.transform, Opponent.transform }, ball, camera, OnReplayPose);
            BuildHud(); gameObject.AddComponent<TennisPhoneInput>();
            gameObject.AddComponent<TennisFrameGovernor>();
            TennisWarmup.Run(camera, fx);
            BeginPoint(); UpdateCamera(true);
            Initialized=true;
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
            ballRenderer.sharedMaterial = new Material(Shader.Find("Standard")) { name = "Tennis ball felt", mainTexture = BallTexture(), color = Color.white };
            ballRenderer.sharedMaterial.SetFloat("_Glossiness", .12f);
            var trail = ball.gameObject.AddComponent<TrailRenderer>(); trail.time = .22f; trail.startWidth = .1f; trail.endWidth = .01f;
            trail.minVertexDistance = .05f;
            trail.sharedMaterial = new Material(Shader.Find("Sprites/Default")) { name = "Ball trail" };
            trail.startColor = new Color(.9f,1,.4f,.55f); trail.endColor = new Color(.9f,1,.4f,0);
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
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
        public void BeginSwing(float handSide, float lift, float strokeFacing)
        {
            if (!Player || Player.Swinging || resetTimer > 0 || ReplayPlaying) return;
            if (Flow == Phase.PlayerServeToss && !serveLaunchPending)
            {
                Player.Serve(.65f, true);
                serveSwingAt = phaseTimer; serveSwingLatency = TennisRules.ServeOnsetLatency;
                sounds.Whoosh(.5f);
                return;
            }
            if (Flow != Phase.Rally || faultDelay > 0) return;
            StartRallySwing(.65f, handSide, lift, strokeFacing, true);
        }

        float serveSwingAt, serveSwingLatency;

        public void RequestSwing(float power, float handSide=0, float lift=0, float strokeFacing=0)
        {
            // A finished match waits on the results card: swing to play again.
            if (Flow == Phase.MatchOver && coach && coach.ShowingResults && resetTimer < ResultsHold - 1.5f) { NewMatch(); return; }
            if (!Player || resetTimer > 0 || ReplayPlaying) return;
            if (Player.Swinging && Player.Provisional)
            {
                Player.Confirm(power);
                if (Player.Kind == TennisActor.Stroke.Serve) StrikeServe(power, lift, serveSwingAt, serveSwingLatency);
                else Feedback = Player.StrokeLabel;
                return;
            }
            if (Player.Swinging) return;
            if (Flow == Phase.PlayerServeToss && !serveLaunchPending)
            { StrikeServe(power, lift, phaseTimer, TennisRules.ServeLatency); return; }
            if (Flow != Phase.Rally || faultDelay > 0) return;
            StartRallySwing(power, handSide, lift, strokeFacing, false);
        }

        /// The phone wrote the stroke off -- it was a step, not a swing.
        public void AbortSwing()
        {
            if (Player && Player.Provisional) Player.CancelSwing();
        }

        void StartRallySwing(float power, float handSide, float lift, float strokeFacing, bool provisional)
        {
            bool leftSide=Mathf.Abs(handSide)>.14f ? handSide<0 : BallPosition.x<Player.transform.position.x;
            bool backhand=TennisRules.UseBackhand(strokeFacing,leftSide,NativeSportsSession.Left);
            float gap=Mathf.Abs(BallPosition.x-Player.transform.position.x);
            string kind=TennisRules.StrokeFor(BallPosition.y,BallPosition.z,Player.transform.position.z,gap,lift>.18f);
            // A low, flat swing slices; a hard one with lift is topspin. Both have their own
            // authored clip, and now their own ball flight too.
            if (kind=="Drive") kind = lift < -.04f ? "Slice" : power > .72f ? "Topspin" : "Drive";
            // Timing is the skill: an early swing opens the cross-court corner, a late one
            // goes down the line.
            float reach=Vector3.Distance(Player.transform.position+Vector3.up*1.1f,BallPosition);
            float closing=Mathf.Max(1f,new Vector2(BallVelocity.x,BallVelocity.z).magnitude);
            AimInput=TennisRules.AimFromTiming(-reach/closing,backhand);
            Player.Swing(power,backhand,StrokeKind(kind),provisional);
            if (!provisional) Feedback=Player.StrokeLabel;
            consumedStroke = false;
            sounds.Whoosh(power);
        }

        /// The player's own serve: the toss is automatic, the strike is not. A mistimed swing
        /// nets the ball and costs a fault; two faults lose the point.
        void StrikeServe(float power, float lift, float swungAt, float latency)
        {
            var box = TennisRules.ServeTargetCentre(true, Match.DeuceCourt);
            // Only phone motion can prove a genuine overhead action. Touch buttons and the
            // keyboard never report lift, so demanding it there would make serving impossible.
            bool canProveOverhead = NativeControlled && !NativeSportsSession.Touch;
            float overhead = canProveOverhead ? lift : TennisRules.ServeMinLift;
            // Correct for detection latency: by the time a swing is reported, the player
            // began it some time earlier.
            float offset = swungAt - latency - TennisRules.ServeIdealContact;
            var verdict = TennisRules.JudgeServe(offset, power, overhead, box, true);
            if (!verdict.Struck) { Feedback = verdict.Label; if (Player.Provisional || Player.Swinging) Player.CancelSwing(); return; }
            if (!Player.Swinging) Player.Serve(Mathf.Max(.45f, power));
            Feedback = verdict.Label;
            pendingServe = verdict; pendingServePower = power; serveLaunchPending = true;
        }

        /// Release a struck serve at the moment the animated racket meets the ball.
        void LaunchServe()
        {
            serveLaunchPending = false;
            var verdict = pendingServe;
            Vector3 start = Player.transform.position + Vector3.up * TennisRules.ServeContactHeight + Player.transform.forward * .28f;
            BallPosition = previousBall = start;
            // A second serve is hit with safer kick; a first serve is flatter and faster.
            float spin = SecondServe ? .75f : .12f;
            if (verdict.Legal)
                BallVelocity = TennisRules.ServeVelocity(start, verdict.Landing, SecondServe ? verdict.Speed * .8f : verdict.Speed, spin);
            else
            {
                // Mistimed: struck into the tape, visibly, instead of vanishing.
                BallVelocity = TennisRules.ShotVelocity(start, new Vector3(verdict.Landing.x, TennisRules.NetHeight * .55f, 0), 18);
                spin = 0;
            }
            BallSpin = spin;
            bounceRestitution = .60f; bounces = 0; consumedStroke = true;
            incoming = false; serveInFlight = true; serveFromNearSide = true;
            Flow = Phase.Rally; phaseTimer = 0; RallyShots = 1;
            Opponent.SplitStep();
            fx.Contact(start, Timing.Great, false); sounds.Hit(Timing.Great, pendingServePower);
            if (NativeControlled) Haptics.Impact(pendingServePower);
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
            if (!nearServer)
            {
                // The opponent's fault: their second serve, or the point on a double.
                if (SecondServe) { Feedback = "DOUBLE FAULT — opponent"; AwardPoint(true); return; }
                SecondServe = true; faultDelay = 1.1f; faultForOpponent = true;
                Feedback = "FAULT — opponent's second serve";
                return;
            }
            if (SecondServe) { Feedback = "DOUBLE FAULT"; AwardPoint(false); return; }
            SecondServe = true; faultDelay = 1.1f; faultForOpponent = false;
            Feedback = "FAULT — second serve";
        }
        bool faultForOpponent;

        /// Close the point, bank it on the scoreboard, and let the ball keep rolling.
        void AwardPoint(bool toPlayer, bool winner = false)
        {
            if (Flow == Phase.PointOver || Flow == Phase.MatchOver) return;
            bool wasMatchPoint = matchPoint;
            match.AwardPoint(toPlayer);
            Streak = 0; faultDelay = 0;
            LongestRally = Mathf.Max(LongestRally, RallyShots);
            if (Player) Player.React(toPlayer);
            if (Opponent) Opponent.React(!toPlayer);
            if (!toPlayer) Misses++;
            Flow = Match.Complete ? Phase.MatchOver : Phase.PointOver;
            resetTimer = Match.Complete ? (AutoPlay ? 5f : ResultsHold) : 2.2f;
            if (Match.Complete && coach) coach.ShowResults(Match, Hits, LongestRally);
            Feedback = (toPlayer ? "POINT YOU — " : "POINT OPPONENT — ") + Feedback;
            float excitement = Mathf.Clamp01(RallyShots / 10f + (winner ? .3f : 0) + (Match.Complete ? .5f : 0));
            crowd.Cheer(toPlayer ? .45f + excitement * .55f : .25f + excitement * .4f);
            sounds.Applaud(toPlayer ? .5f + excitement * .5f : .3f + excitement * .3f);
            if (NativeControlled) { if (toPlayer) Haptics.Success(); else Haptics.Warning(); }
            // Replays are for moments, not every point: winners, long rallies and the match.
            pointsSinceReplay++;
            bool special = toPlayer && (winner || RallyShots >= 8 || (wasMatchPoint && Match.Complete));
            if (special && (pointsSinceReplay >= 3 || Match.Complete) && !ManualSimulation)
            { replayDue = .7f; pointsSinceReplay = 0; }
        }

        public void SelectCharacter(bool female)
        {
            FemalePlayer = female;
            if (!Player) return;
            Vector3 position = Player.transform.position;
            Destroy(Player.gameObject);
            Player = new GameObject("Player — permanent standard").AddComponent<TennisActor>();
            Player.transform.position = position; Player.Build(female,GolferStyle.SkinColor);
            TennisLook.AddContactShadow(Player.transform, .55f, .45f).HeightOverride = 0;
            if (replay) replay.Build(new[] { Player.transform, Opponent.transform }, ball, GameplayCamera, OnReplayPose);
            BeginPoint();
        }

        // Public adapter boundary: tracked phone position can supply this without changing physics.
        public void SetLateralInput(float normalizedSpeed, bool sprint)
        { MoveInput = Mathf.Clamp(normalizedSpeed,-1,1); Sprint = sprint; }

        void Update()
        {
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
                if (Input.GetKeyDown(KeyCode.F1)) SelectCharacter(!FemalePlayer);
                if (Input.GetKeyDown(KeyCode.G)) UnityEngine.SceneManagement.SceneManager.LoadScene("Golf");
                if (AutoPlay) DriveAutoPlay();
                accumulator += Mathf.Min(Time.deltaTime, .1f);
                while (accumulator >= 1f/120) { Step(1f/120); accumulator -= 1f/120; }
            }
            if (replayDue > 0)
            {
                replayDue -= Time.deltaTime;
                if (replayDue <= 0 && replay.Play(2.6f, .5f)) { banner.text = "REPLAY"; bannerUntil = Time.unscaledTime + 99; }
            }
            PoseActors();
            UpdateCamera(false); UpdateHud(); UpdateTrajectory(); UpdateBallVisual();
            replay.Record(Time.time);
        }

        /// Build both skeletons once for everything simulated this frame.
        void PoseActors()
        {
            if (!posePending) return;
            posePending = false;
            Player.Pose(); Opponent.Pose();
        }

        void OnReplayPose(bool playing)
        {
            if (!playing) { banner.text = ""; resetTimer = Mathf.Max(resetTimer, .4f); return; }
            Player.RefreshArms(); Opponent.RefreshArms();
        }

        public void Step(float dt)
        {
            if (!Player || dt <= 0 || ReplayPlaying) return;
            float maximum = Mathf.Lerp(3.2f, Sprint ? TennisRules.SprintSpeed : TennisRules.RunSpeed, Mathf.Clamp01(Stamina / .3f));
            if (Player.GroundRecovering) maximum *= .25f;
            AssistOffset = 0;
            if (incoming && !Serving && resetTimer <= 0 &&
                TennisRules.PredictInterceptX(BallPosition, BallVelocity, Player.transform.position.z, bounceRestitution, out float interceptX, BallSpin))
                PredictedInterceptX = interceptX;
            Vector3 playerPosition = Player.transform.position;
            if (ServeLocked)
            {
                // Feet are planted for the serve, as in the real thing.
                float stance = TennisRules.ServerStanceX(true, Match.DeuceCourt);
                float planted = Mathf.MoveTowards(playerPosition.x, stance, dt * 7f);
                LateralSpeed = 0;
                Player.transform.position = new Vector3(planted, playerPosition.y, playerPosition.z);
            }
            else
            {
                // Wii Sports model: the character walks itself to the ball and the player only
                // swings; the skill lives in the timing of the stroke instead.
                float goal = incoming && PredictedInterceptX > -99 ? PredictedInterceptX : TennisOpponent.RestX;
                goal = Mathf.Clamp(goal, -TennisRules.CourtHalfWidth, TennisRules.CourtHalfWidth);
                // Arrive, don't stop dead: speed is capped by what can still be shed before the
                // goal, and changes at a human rate. The old instant stop snapped the body.
                float gap = goal - playerPosition.x;
                float arrive = Mathf.Sqrt(2 * TennisRules.Deceleration * Mathf.Abs(gap));
                float wanted = Mathf.Sign(gap) * Mathf.Min(maximum, arrive);
                float velocity = Mathf.MoveTowards(LateralSpeed, wanted, dt * TennisRules.Acceleration);
                float nextX = playerPosition.x + velocity * dt;
                if ((goal - nextX) * gap < 0) { nextX = goal; velocity = 0; }
                LateralSpeed = velocity;
                Player.transform.position = new Vector3(nextX, playerPosition.y, playerPosition.z);
            }
            Stamina = TennisRules.StaminaStep(Stamina, LateralSpeed, dt);
            previousRacket = Player.SweetSpot.position;
            bool wasSwinging = Player.Swinging;
            // A provisional swing that is never confirmed or written off is dropped.
            if (Player.Provisional && Player.SwingAge > .32f) Player.CancelSwing();
            Player.Advance(dt, LateralSpeed);
            // Pose every step while the racket could meet the ball, so the swept contact test
            // sees the real racket path; otherwise once per rendered frame is enough.
            bool precise = Player.Swinging && incoming && !consumedStroke;
            if (precise) Player.Pose(); else posePending = true;
            if (wasSwinging && !Player.Swinging && !consumedStroke && Flow == Phase.Rally && incoming)
            { Feedback = "WHIFF — time the swing to the bounce"; if (coach && NativeControlled) coach.Offer(TennisCoach.Tip.Whiff); }
            if (serveLaunchPending && (!Player.Swinging || Player.ContactAge >= TennisRules.SweetTime)) LaunchServe();
            UpdateOpponent(dt);
            UpdateAttention();
            phaseTimer += dt;
            if (faultDelay > 0)
            {
                faultDelay -= dt;
                if (faultDelay <= 0)
                {
                    serveInFlight = false; phaseTimer = 0; serveTimer = 0;
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
                if (resetTimer <= 0) { if (Flow == Phase.MatchOver) NewMatch(); else BeginPoint(); return; }
            }
            if (Flow == Phase.PlayerServeHold)
            {
                // Ball rests in the tossing hand, then is thrown up automatically. Failing to
                // swing is not a fault: it drops back into the hand and is tossed again.
                tossOrigin = Player.transform.TransformPoint(new Vector3(-.28f,1.35f,.22f));
                BallPosition = previousBall = tossOrigin; BallVelocity = Vector3.zero;
                Player.Prepare(0, false, true);
                if (phaseTimer >= TennisRules.ServeTossDelay) { Flow = Phase.PlayerServeToss; phaseTimer = 0; }
                return;
            }
            if (Flow == Phase.PlayerServeToss)
            {
                // Pure ballistic toss so the apex, and therefore the timing window, is honest.
                float rise = 9.81f * TennisRules.ServeApex;
                previousBall = BallPosition;
                BallPosition = tossOrigin + new Vector3(0, rise*phaseTimer - 4.905f*phaseTimer*phaseTimer, 0);
                BallVelocity = new Vector3(0, rise - 9.81f*phaseTimer, 0);
                // The server's body follows the toss up into the trophy position.
                if (!Player.Swinging) Player.Prepare(Mathf.Clamp01(phaseTimer / TennisRules.ServeIdealContact), false, true);
                if (phaseTimer >= TennisRules.ServeCatch && !serveLaunchPending && !Player.Swinging)
                { Flow = Phase.PlayerServeHold; phaseTimer = 0; Feedback = "Caught it — tossing again"; Player.Prepare(0, false, true); }
                return;
            }
            if (Flow == Phase.OpponentServe) {
                serveTimer+=dt;
                float toss=Mathf.Clamp01(serveTimer/.85f);
                previousBall = BallPosition;
                BallPosition=Opponent.transform.TransformPoint(new Vector3(.35f,1.2f+Mathf.Sin(toss*Mathf.PI*.65f)*1.45f,.3f));
                if(serveTimer<.67f) Opponent.Prepare(toss, false, true);
                if(serveTimer>=.67f && !Opponent.Swinging) Opponent.Serve(.55f);
                if(serveTimer>=.85f) { BallPosition=Opponent.SweetSpot.position; Flow=Phase.Rally; phaseTimer=0; ServeFromOpponent(); }
                return;
            }
            SimulateBall(dt);
        }

        void SimulateBall(float dt)
        {
            Vector3 oldBall = BallPosition;
            previousBall = oldBall;
            Vector3 position = BallPosition, velocity = BallVelocity;
            TennisBall.Integrate(ref position, ref velocity, BallSpin, dt);
            BallPosition = position; BallVelocity = velocity;
            if (incoming && !consumedStroke && Player.Swinging && !Player.Provisional && TennisRules.CrossStringBed(oldBall, BallPosition, previousRacket,
                Player.SweetSpot.position, Player.StringNormal, Player.StringRight, Player.StringUp, out var faceOffset))
            {
                float reach = Vector3.Distance(Player.transform.position + Vector3.up * 1.1f, BallPosition);
                float reachQuality = Mathf.Clamp01(1 - Mathf.Abs(reach - .85f) / .8f);
                var hit = TennisRules.Evaluate(Player.ContactAge, faceOffset, 1 - Mathf.Abs(LateralSpeed) / 10, reachQuality, Player.Power, Stamina);
                if (hit.Contact) ReturnBall(hit, faceOffset);
            }
            // Arcade reach assist: still requires a deliberate, timed swing near the ball.
            // Exact string contact above retains the best quality reward. A dive stretches it.
            if(incoming && !consumedStroke && Player.Swinging && !Player.Provisional && TennisRules.AssistedContact(oldBall,BallPosition,Player.transform.position,Player.ContactAge,Player.Overhead,out float assist,Player.Power,Player.Kind==TennisActor.Stroke.Dive)) {
                var hit=TennisRules.Evaluate(TennisRules.SweetTime,Vector2.zero,1-Mathf.Abs(LateralSpeed)/10,assist,Player.Power,Stamina);
                hit.Quality=assist; hit.Center=assist; hit.Timing=Mathf.Clamp01(1-Mathf.Abs(Player.ContactAge-TennisRules.SweetTime)/.2f);
                hit.Speed*=Mathf.Lerp(.9f,.97f,assist); hit.ErrorDegrees=Mathf.Lerp(7,3,assist); hit.Label="ASSISTED RETURN";
                Vector2 where=TennisRules.FaceOffset(BallPosition,Player.SweetSpot.position,Player.StringRight,Player.StringUp);
                ReturnBall(hit, where);
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
                    if (serveInFlight) { serveInFlight = false; Feedback = "FAULT — net"; Fault(serveFromNearSide); }
                    else { Feedback = "NET"; AwardPoint(incoming); }
                }
            }
            if (BallPosition.y < TennisRules.BallRadius && BallVelocity.y < 0)
            {
                bounces++;
                BallPosition = new Vector3(BallPosition.x,TennisRules.BallRadius,BallPosition.z);
                Vector3 bounced = BallVelocity; float spin = BallSpin;
                fx.Bounce(BallPosition, bounced, CourtDust);
                sounds.Bounce(Mathf.Abs(bounced.y) / 8f);
                TennisBall.Bounce(ref bounced, ref spin, bounceRestitution);
                BallVelocity = bounced; BallSpin = spin;
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
            // The opponent plays the ball when it reaches its end of the court.
            if (live && !incoming && BallPosition.z > 9)
            {
                var decision = TennisOpponent.Decide(Opponent.transform.position.x, BallPosition.x,
                    Player.transform.position.x, OpponentDifficulty,
                    (float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble(),
                    Mathf.InverseLerp(15, 30, new Vector2(BallVelocity.x, BallVelocity.z).magnitude));
                Feedback = decision.Label;
                if (!decision.Reached) { sounds.Gasp(); AwardPoint(true, true); }
                else
                {
                    Opponent.Swing(.62f, (BallPosition.x > Opponent.transform.position.x) != Opponent.LeftHanded);
                    float spin = decision.Error ? 0 : Mathf.Lerp(-.6f, .9f, (float)random.NextDouble());
                    if (decision.Error && decision.Landing.z > 0)
                    {
                        // Netted: struck into the tape, where it drops dead on its own side.
                        InjectBall(BallPosition, TennisRules.ShotVelocity(BallPosition, new Vector3(decision.Landing.x, TennisRules.NetHeight * .55f, 0), 18));
                        bounceRestitution = .75f;
                    }
                    else SendFromOpponent(decision.Landing, decision.Speed, spin);
                    fx.Contact(BallPosition, Timing.Good, false); sounds.Hit(Timing.Good, .6f);
                    Player.SplitStep();
                    RallyShots++;
                }
            }
            if (live && (BallPosition.z < -14 || BallPosition.y < -1))
            { Feedback = "MISSED IT"; AwardPoint(false); }
            if (fx) fx.Stream(BallPosition, LastWasSupercharged && !incoming && Flow == Phase.Rally);
        }

        void UpdateOpponent(float dt)
        {
            float opponentX;
            if (ServeLocked)
                opponentX = Mathf.MoveTowards(Opponent.transform.position.x,
                    TennisRules.ReceiverStanceX(true, Match.DeuceCourt), dt * TennisOpponent.Speed);
            else if (Flow == Phase.OpponentServe)
                opponentX = Mathf.MoveTowards(Opponent.transform.position.x,
                    TennisRules.ServerStanceX(false, Match.DeuceCourt), dt * TennisOpponent.Speed);
            else
            {
                // It reads the shot a beat late, then moves to where the ball will actually
                // arrive rather than chasing where it currently is.
                Vector3 arrival = BallPosition;
                bool ballComing = !incoming
                    && TennisRules.PredictLanding(BallPosition, BallVelocity, out arrival, BallSpin);
                if (ballComing) predictedOpponentX = arrival.x;
                bool reading = ballComing && phaseTimer > TennisOpponent.Reaction;
                opponentX = TennisOpponent.Reposition(Opponent.transform.position.x, predictedOpponentX, reading, dt);
            }
            float opponentSpeed = (opponentX - Opponent.transform.position.x) / dt;
            Opponent.transform.position = new Vector3(opponentX,.035f,11.2f);
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
                bool leftSide = BallPosition.x < Player.transform.position.x - .1f;
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
            if (Player.Swinging) return;
            if (Flow == Phase.PlayerServeToss && phaseTimer >= TennisRules.ServeIdealContact - .02f)
            { BeginSwing(0, .3f, 1); RequestSwing(.8f, 0, .3f, 1); return; }
            if (Flow != Phase.Rally || !incoming || BallVelocity.z > -1) return;
            float toContact = (BallPosition.z - (Player.transform.position.z + .65f)) / -BallVelocity.z;
            if (toContact < .20f && toContact > .1f)
            {
                float facing = BallPosition.x < Player.transform.position.x ? -1 : 1;
                BeginSwing(0, 0, facing); RequestSwing(.55f + (float)random.NextDouble() * .35f, 0, 0, facing);
            }
        }

        void ReturnBall(TennisHit hit, Vector2 faceOffset)
        {
            if(Player.Overhead) hit.Speed=Mathf.Min(24,hit.Speed*1.1f);
            // Grade the contact, then reward three well-timed balls in a row.
            LastGrade = TennisRules.Grade(hit.Timing);
            LastFaceOffset = faceOffset;
            Streak = TennisRules.Extends(LastGrade) ? Streak + 1 : 0;
            LastWasSupercharged = Streak >= TennisRules.SuperchargeStreak;
            if (LastWasSupercharged) { hit = TennisRules.Supercharge(hit); Streak = 0; }
            hitMarkerAt = Time.unscaledTime;
            if (hitMap) hitMap.Record(faceOffset, LastGrade, LastWasSupercharged);
            if (coach) { coach.Record(LastGrade); if (Hits >= 3 && NativeControlled) coach.Offer(TennisCoach.Tip.Timing); }
            LastHit = hit; Hits++; consumedStroke = true; incoming = false; bounces = 0;
            serveInFlight = false; bounceRestitution=.75f;
            RallyShots++;
            float error = ((float)random.NextDouble()*2-1) * hit.ErrorDegrees;
            Vector3 target=TennisRules.ShotTarget(AimInput,Player.Power);
            target.x+=Mathf.Tan(error*Mathf.Deg2Rad)*(target.z-BallPosition.z);
            BallSpin = SpinFor(Player.Kind, Player.Power);
            BallVelocity = TennisBall.Solve(BallPosition,target,hit.Speed,BallSpin);
            Feedback = $"{TennisRules.GradeLabel(LastGrade)} · {Player.StrokeLabel} · {hit.Label} · {hit.Speed*3.6f:0} km/h"
                + (Streak > 0 ? $"\n{Streak} clean in a row — {TennisRules.SuperchargeStreak - Streak} to supercharge" : "");
            ballRenderer.material.color = LastWasSupercharged ? new Color(.7f,1,1) : Color.Lerp(new Color(1,.8f,.7f), Color.white, hit.Quality);
            fx.Contact(BallPosition, LastGrade, LastWasSupercharged);
            sounds.Hit(LastGrade, Player.Power);
            if (NativeControlled) Haptics.Impact(Mathf.Lerp(.35f, 1f, hit.Quality));
            Opponent.SplitStep();
        }

        /// Put a live rally ball in play. Clears the serve flag: a ball injected here is by
        /// definition not a serve, and leaving the flag set got the next bounce judged against
        /// the service box and faulted. The serve paths set the flag themselves afterwards.
        public void InjectBall(Vector3 position, Vector3 velocity)
        {
            Flow = Phase.Rally; BallPosition = previousBall = position; BallVelocity = velocity; BallSpin = 0;
            incoming = true; bounces = 0; resetTimer = 0; consumedStroke = false; serveInFlight = false; faultDelay = 0;
            serveLaunchPending = false;
            if (ball) ball.position = renderedBall = position;
        }

        /// Start the next point from whichever end is serving.
        void BeginPoint() {
            Stamina=1; SecondServe=false; consumedStroke=false;
            bounces=0; bounceRestitution=.75f; incoming=true; serveInFlight=false; faultDelay=0; serveLaunchPending=false;
            serveTimer=0; phaseTimer=0; BallVelocity=Vector3.zero; BallSpin=0; RallyShots=0; replayDue=-1;
            resetTimer=0; LastWasSupercharged=false;
            ballRenderer.material.color=Color.white;
            ball.GetComponent<TrailRenderer>().Clear();
            matchPoint = IsMatchPoint(Match);
            crowd.Hush(matchPoint);
            if (matchPoint) { banner.text = "MATCH POINT"; bannerUntil = Time.unscaledTime + 2.2f; }
            if (Match.PlayerServes)
            {
                Flow=Phase.PlayerServeHold;
                if (coach && NativeControlled) coach.Offer(TennisCoach.Tip.Serve);
                Feedback=$"Your serve to the {(Match.DeuceCourt ? "deuce" : "ad")} court — toss is automatic, swing down hard";
            }
            else
            {
                Flow=Phase.OpponentServe;
                // Stand the receiver where the serve is legally required to arrive; they are
                // free to move from there once the ball is live.
                Player.transform.position=new Vector3(
                    TennisRules.ReceiverStanceX(false, Match.DeuceCourt),
                    Player.transform.position.y, Player.transform.position.z);
                BallPosition=previousBall=Opponent.transform.TransformPoint(new Vector3(.35f,1.2f,.3f));
                ball.position=renderedBall=BallPosition;
                Feedback="Opponent serving — move into position, then swing";
            }
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
        void NewMatch() { match = TennisMatch.New(); Hits=0; Misses=0; LongestRally=0; resetTimer=0; if (coach) coach.HideResults(); BeginPoint(); }

        /// Opponent's serve: varied between wide, body and T, sometimes faulted, and a slower
        /// kick serve second -- every serve used to land in the middle of the box.
        void ServeFromOpponent()
        {
            var plan = TennisOpponent.PlanServe(Match.DeuceCourt, SecondServe, OpponentDifficulty,
                (float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble());
            // Struck from above the head, like the player's, and lofted enough to clear.
            Vector3 start=Opponent.transform.position+Vector3.up*TennisRules.ServeContactHeight
                +Opponent.transform.forward*.28f;
            bounceRestitution=.60f;
            InjectBall(start,TennisRules.ServeVelocity(start,plan.Landing,plan.Speed,plan.Spin));
            BallSpin=plan.Spin;
            serveInFlight=true; serveFromNearSide=false;
            RallyShots = 1;
            fx.Contact(start, Timing.Good, false); sounds.Hit(Timing.Great, .8f);
            Player.SplitStep();
        }

        /// Send the opponent's chosen shot on its way, guaranteed to clear the net so its
        /// intended target is the thing that decides the point.
        void SendFromOpponent(Vector3 landing, float speed, float spin)
        {
            bounceRestitution=.75f; serveInFlight=false;
            InjectBall(BallPosition, TennisRules.ServeVelocity(BallPosition, landing, speed, spin));
            BallSpin = spin;
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
                && TennisRules.PredictLanding(BallPosition,BallVelocity,out landing,BallSpin);
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
            bool showAim = Flow == Phase.Rally || Flow == Phase.PlayerServeHold || Flow == Phase.PlayerServeToss;
            aimRing.enabled = showAim;
            if (!showAim) return;
            Vector3 target = Flow == Phase.Rally
                ? TennisRules.ShotTarget(AimInput,.5f)
                : TennisRules.ServeTargetCentre(true, Match.DeuceCourt);
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
            ball.position = renderedBall;
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

        float cameraShakeSeed;
        void UpdateCamera(bool immediate)
        {
            var camera = GameplayCamera ? GameplayCamera : Camera.main; if (!camera || !Player) return;
            // Wii Sports keeps the character large and close; the animation is the feedback.
            float lead = Mathf.Clamp(Player.transform.position.x, -5f, 5f);
            Vector3 target = new Vector3(lead*.55f, 3.5f, Player.transform.position.z - 4.4f);
            Vector3 look = new Vector3(lead*.35f, 1.15f, -3.2f);
            float fov = 56;
            if (ServeLocked)
            {
                // Serve camera: in tight behind the server's shoulder, looking down the
                // diagonal the serve has to travel.
                Vector3 box = TennisRules.ServeTargetCentre(true, Match.DeuceCourt);
                target = Player.transform.position + new Vector3(-Mathf.Sign(box.x) * .9f, 2.7f, -3.3f);
                look = Vector3.Lerp(Player.transform.position + Vector3.up * 1.8f, box, .45f);
                fov = 52;
            }
            if (matchPoint && Serving) fov -= 4;
            float rate = 1 - Mathf.Exp(-Time.deltaTime * (ServeLocked ? 4 : 7));
            camera.transform.position = immediate ? target : Vector3.Lerp(camera.transform.position, target, rate);
            camera.fieldOfView = immediate ? fov : Mathf.Lerp(camera.fieldOfView, fov, rate);
            camera.transform.LookAt(look);
            // A short punch on strong contact.
            float shake = fx ? fx.Shake : 0;
            if (shake > .01f)
            {
                cameraShakeSeed += Time.deltaTime * 38;
                camera.transform.position += camera.transform.right * ((Mathf.PerlinNoise(cameraShakeSeed, 0) - .5f) * .09f * shake)
                    + camera.transform.up * ((Mathf.PerlinNoise(0, cameraShakeSeed) - .5f) * .07f * shake);
            }
        }

        void BuildHud()
        {
            var canvas = new GameObject("Tennis HUD").AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.gameObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvas.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1280,720);
            Text Label(string name, Vector2 anchor, Vector2 size, Vector2 offset, int fontSize)
            {
                var go = new GameObject(name); go.transform.SetParent(canvas.transform,false);
                var text = go.AddComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = fontSize; text.color = Color.white;
                text.alignment = TextAnchor.MiddleLeft; text.raycastTarget = false;
                var rect = text.rectTransform; rect.anchorMin = rect.anchorMax = anchor; rect.pivot = anchor; rect.sizeDelta = size; rect.anchoredPosition = offset;
                go.AddComponent<Shadow>().effectDistance = new Vector2(1,-1); return text;
            }
            title = Label("Rally score",new Vector2(0,1),new Vector2(1050,90),new Vector2(28,-15),24);
            status = Label("Live hit feedback",new Vector2(0,0),new Vector2(1200,100),new Vector2(28,75),24);
            help = Label("Controls",Vector2.zero,new Vector2(1230,55),new Vector2(28,10),17);
            // Hitmarker: a brief centre-screen burst naming how clean the contact was.
            hitMarker = Label("Hit grade",new Vector2(.5f,.5f),new Vector2(560,90),new Vector2(0,96),44);
            hitMarker.alignment = TextAnchor.MiddleCenter;
            hitMarker.text = "";
            var outline = hitMarker.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, .85f);
            outline.effectDistance = new Vector2(2.5f, -2.5f);
            banner = Label("Moment banner",new Vector2(.5f,1),new Vector2(700,80),new Vector2(0,-70),40);
            banner.alignment = TextAnchor.MiddleCenter; banner.text = ""; banner.color = new Color(1,.9f,.45f);
            banner.gameObject.AddComponent<Outline>().effectColor = new Color(0,0,0,.8f);
            hitMap = gameObject.AddComponent<TennisHitMap>();
            hitMap.Build(canvas);
            coach = gameObject.AddComponent<TennisCoach>();
            coach.Build(canvas);
            var bar = new GameObject("Stamina").AddComponent<Image>(); bar.transform.SetParent(canvas.transform,false); bar.color = new Color(.25f,.9f,.7f);
            bar.raycastTarget = false;
            staminaFill = bar; bar.rectTransform.anchorMin = bar.rectTransform.anchorMax = new Vector2(0,1); bar.rectTransform.pivot = new Vector2(0,1); bar.rectTransform.anchoredPosition = new Vector2(28,-110); bar.rectTransform.sizeDelta = new Vector2(220,12);
        }

        // HUD text is rebuilt only when what it shows changes. Rebuilding strings every frame
        // was a steady source of garbage and forced a canvas rebuild every frame.
        TennisMatch shownMatch; Phase shownFlow = (Phase)(-1); bool shownSecond, shownDegraded;
        string shownFeedback, shownPause, shownWarning; float shownStamina = -1; bool shownHelp; float bannerUntil;
        static bool SameScore(TennisMatch a, TennisMatch b) =>
            a.PlayerPoints == b.PlayerPoints && a.OpponentPoints == b.OpponentPoints && a.PlayerGames == b.PlayerGames
            && a.OpponentGames == b.OpponentGames && a.PlayerServes == b.PlayerServes && a.Complete == b.Complete;

        void UpdateHud()
        {
            bool scoreChanged = !SameScore(shownMatch, Match) || shownFlow != Flow || shownSecond != SecondServe;
            if (scoreChanged)
            {
                shownMatch = Match; shownFlow = Flow; shownSecond = SecondServe;
                string serveNote = Flow == Phase.PlayerServeHold ? "  ·  TOSSING…"
                    : Flow == Phase.PlayerServeToss ? "  ·  SWING NOW"
                    : SecondServe ? "  ·  SECOND SERVE" : "";
                title.text = $"TROPICAL TENNIS · SET TO {TennisMatch.GamesToWin} GAMES\n{Match.Scoreboard}{serveNote}";
            }
            string pause = NativeControlled ? NativeSportsSession.PauseReason : null;
            string warning = NativeControlled ? NativeSportsSession.TrackingWarning : null;
            if (!ReferenceEquals(pause, shownPause) || !ReferenceEquals(warning, shownWarning) || !ReferenceEquals(Feedback, shownFeedback))
            {
                shownPause = pause; shownWarning = warning; shownFeedback = Feedback;
                string text = !string.IsNullOrEmpty(pause) ? pause : Feedback;
                bool degraded = !string.IsNullOrEmpty(warning);
                status.text = degraded ? warning + "\n" + text : text;
                if (degraded != shownDegraded) { status.color = degraded ? new Color(1,.72f,.25f) : Color.white; shownDegraded = degraded; }
            }
            if (!shownHelp)
            {
                shownHelp = true;
                help.text = NativeControlled ? "Swing phone to hit · Early = cross-court, late = down the line · Cyan ring: where the ball lands" : InputStatus + "\nR serve · C calibrate phone tilt · F1 character · G golf";
            }
            // The marker pops in, holds, then fades -- long enough to read mid-rally without
            // sitting on screen through the next shot.
            float since = Time.unscaledTime - hitMarkerAt;
            if (since < .9f)
            {
                string label = LastWasSupercharged ? "SUPERCHARGED!" : TennisRules.GradeLabel(LastGrade);
                if (!ReferenceEquals(hitMarker.text, label)) hitMarker.text = label;
                var tint = LastWasSupercharged ? new Color(.45f,.95f,1f) : TennisHitMap.GradeColour(LastGrade);
                hitMarker.color = tint;
                hitMarker.canvasRenderer.SetAlpha(Mathf.Clamp01(1 - since / .9f));
                float pop = 1 + Mathf.Exp(-since * 14) * .35f;
                hitMarker.rectTransform.localScale = Vector3.one * pop * (LastWasSupercharged ? 1.25f : 1f);
            }
            else if (hitMarker.text.Length > 0) hitMarker.text = "";
            if (banner.text.Length > 0 && Time.unscaledTime > bannerUntil && !ReplayPlaying) banner.text = "";
            if (Mathf.Abs(Stamina - shownStamina) > .004f)
            {
                shownStamina = Stamina;
                staminaFill.rectTransform.sizeDelta = new Vector2(220*Stamina,12);
                staminaFill.color = Color.Lerp(new Color(1,.3f,.2f), new Color(.25f,.9f,.7f), Stamina);
            }
        }
    }
}
