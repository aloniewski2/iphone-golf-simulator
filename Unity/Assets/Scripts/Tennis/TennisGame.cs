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
        TennisReplay replay;
        TennisResortCrowd crowd;
        TennisUmpire umpire;
        TennisStandsCrowd stands;
        TennisCoach coach;
        UnityEngine.Rendering.Volume post;
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
            TennisLook.StyleArena(environment);
            // The island the resort sits on (Higgsfield model fitted by prepare_island.py), in
            // the arena's own frame.
            var islandPrefab = Resources.Load<GameObject>("Tennis/Island/TennisIsland");
            if (islandPrefab) { var island = Instantiate(islandPrefab); island.name = "Tropical island"; island.transform.rotation = environment.transform.rotation * islandPrefab.transform.rotation; TennisLook.StyleIsland(island); }
            crowd = gameObject.AddComponent<TennisResortCrowd>();
            umpire = TennisUmpire.Spawn(environment.transform.parent);
            stands = gameObject.AddComponent<TennisStandsCrowd>(); stands.Build(null);
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
            camera.backgroundColor = new Color(.49f,.73f,.88f); camera.clearFlags = CameraClearFlags.Skybox;
            camera.allowMSAA = true;
            camera.allowHDR = TennisQuality.Current != TennisQuality.Tier.Low;
            post = TennisLook.SetupPost(camera);
            var light = new GameObject("Resort sun").AddComponent<Light>(); light.type = LightType.Directional;
            TennisLook.LightScene(light);
            TennisLook.AddContactShadow(Player.transform, .55f, .45f).HeightOverride = 0;
            TennisLook.AddContactShadow(Opponent.transform, .55f, .45f).HeightOverride = 0;
            TennisLook.AddContactShadow(ball, .16f, .55f).FadeHeight = 4f;
            fx = new GameObject("Tennis effects").AddComponent<TennisFx>(); fx.transform.SetParent(transform); fx.Build();
            HookActorFx(Player); HookActorFx(Opponent);
            sounds = TennisSounds.Create(transform);
            replay = gameObject.AddComponent<TennisReplay>();
            replay.Build(new[] { Player.transform, Opponent.transform }, ball, camera, OnReplayPose);
            BuildHud(); gameObject.AddComponent<TennisPhoneInput>();
            presentation = gameObject.AddComponent<TennisPresentation>();
            presentation.Build(this, hud, umpire);
            gameObject.AddComponent<TennisFrameGovernor>();
            TennisWarmup.Run(camera, fx);
            BeginPoint(); UpdateCamera(true);
            Initialized=true;
        }

        void HookActorFx(TennisActor actor)
        {
            actor.FootPlanted = (at, speed) => fx.Footstep(at, Mathf.Abs(speed));
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
        public void BeginSwing(float handSide, float lift, float strokeFacing)
        {
            if (IntroPlaying) { presentation.Skip(); return; }
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
            if (IntroPlaying) { presentation.Skip(); return; }
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
            // Without a clear wing from the phone, play the side the ball will be on at contact.
            bool leftSide=Mathf.Abs(handSide)>.14f ? handSide<0 : PredictBall(Mathf.Min(.2f, Mathf.Max(0, (Player.transform.position.z - BallPosition.z) / Mathf.Min(-1f, BallVelocity.z)))).x<Player.transform.position.x;
            bool backhand=TennisRules.UseBackhand(strokeFacing,leftSide,NativeSportsSession.Left);
            float gap=Mathf.Abs(BallPosition.x-Player.transform.position.x);
            string kind=TennisRules.StrokeFor(BallPosition.y,BallPosition.z,Player.transform.position.z,gap,lift>.18f);
            // A low, flat swing slices; a hard one with lift is topspin. Both have their own
            // authored clip, and now their own ball flight too.
            if (kind=="Drive") kind = lift < -.04f ? "Slice" : power > .72f ? "Topspin" : "Drive";
            // The pick-up and slice are authored forehand-only: on the backhand side they put
            // the racket on the wrong side of the body from the ball.
            if (backhand && (kind=="LowPickup" || kind=="Slice")) kind = "Drive";
            // Aim is the racket face (phone), the arrow keys, or the touch aim control; it is
            // read when the ball is struck, so it is not set here.
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
            float offset = swungAt - latency * SpeedScale - TennisRules.ServeIdealContact;
            var verdict = TennisRules.JudgeServe(offset, power, overhead, box, true);
            if (!verdict.Struck) { Feedback = verdict.Label; if (Player.Provisional || Player.Swinging) Player.CancelSwing(); return; }
            if (!Player.Swinging) Player.Serve(Mathf.Max(.45f, power));
            Feedback = verdict.Label;
            pendingServe = verdict; pendingServePower = power; serveLaunchPending = true;
            // The racket goes up to the toss: the swing is paced to meet the ball where it
            // passes closest to the serve's contact point, and steered the rest of the way.
            float natural = Player.TimeToContact, best = float.MaxValue, lead = natural;
            Vector3 aim = Player.AuthoredContact;
            for (float t = natural * .6f; t <= natural * 1.6f; t += TennisBall.Step)
            {
                float d = (PlayerToss(phaseTimer + t) - aim).sqrMagnitude;
                if (d < best) { best = d; lead = t; }
            }
            Player.PaceToContact(lead);
            Player.GuideContact(PlayerToss(phaseTimer + lead), lead);
        }

        Vector3 tossDrift;
        /// The player's toss, `t` seconds after release: straight ballistics, drifting across
        /// to the racket side.
        Vector3 PlayerToss(float t) => tossOrigin + tossDrift * t + new Vector3(0, 9.81f * TennisRules.ServeApex * t - 4.905f * t * t, 0);

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
            var verdict = pendingServe;
            Vector3 start = Player.transform.position + Vector3.up * TennisRules.ServeContactHeight + Player.transform.forward * .28f;
            // Off the strings, which were steered to the toss.
            Vector3 strings = Player.SweetSpot.position + Vector3.forward * (TennisRules.BallRadius * 1.2f);
            RecordGap(true, Vector3.Distance(strings, BallPosition));
            if (Vector3.Distance(strings, start) < 1.2f) start = strings;
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
            incoming = false; serveInFlight = true; serveFromNearSide = true; opponentShot = default;
            Flow = Phase.Rally; phaseTimer = 0; RallyShots = 1;
            Opponent.SplitStep();
            fx.Contact(start, Timing.Great, false); sounds.Hit(Timing.Great, pendingServePower); contactHitter = Player;
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
            bool wasMatchPoint = matchPoint;
            int gamesBefore = match.PlayerGames + match.OpponentGames;
            match.AwardPoint(toPlayer);
            bool gameWon = match.PlayerGames + match.OpponentGames > gamesBefore;
            Streak = 0; faultDelay = 0;
            LongestRally = Mathf.Max(LongestRally, RallyShots);
            if (umpire) umpire.Call();
            if (Player) Player.React(toPlayer);
            if (Opponent) Opponent.React(!toPlayer);
            if (!toPlayer) Misses++;
            Flow = Match.Complete ? Phase.MatchOver : Phase.PointOver;
            resetTimer = Match.Complete ? (AutoPlay ? 5f : ResultsHold) : 1.5f;
            if (Match.Complete && coach) coach.ShowResults(Match, Hits, LongestRally);
            string call = Feedback;
            Feedback = (toPlayer ? "POINT YOU — " : "POINT OPPONENT — ") + Feedback;
            float excitement = Mathf.Clamp01(RallyShots / 10f + (winner ? .3f : 0) + (Match.Complete ? .5f : 0));
            crowd.Cheer(toPlayer ? .45f + excitement * .55f : .25f + excitement * .4f);
            if (stands && (winner || RallyShots >= 5 || Match.Complete || gameWon)) stands.Cheer(toPlayer ? .5f + excitement * .5f : .3f + excitement * .4f);
            sounds.Applaud(toPlayer ? .5f + excitement * .5f : .3f + excitement * .3f);
            if (hud && !Match.Complete)
            {
                hud.ShowCall(CallFor(call, toPlayer, winner), PointContext(toPlayer), toPlayer);
                if (gameWon) hud.ShowCall("GAME", $"GAME {(toPlayer ? hud.PlayerName : hud.OpponentName)}  ·  {Match.PlayerGames}-{Match.OpponentGames}", toPlayer);
            }
            if (NativeControlled) { if (toPlayer) Haptics.Success(); else Haptics.Warning(); }
            // Replays are for moments, not every point: winners, long rallies and the match.
            pointsSinceReplay++;
            bool special = toPlayer && (winner || RallyShots >= 8 || (wasMatchPoint && Match.Complete));
            if (special && (pointsSinceReplay >= 3 || Match.Complete) && !ManualSimulation)
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

        string PointContext(bool toPlayer)
        {
            string who = toPlayer ? "POINT " + (hud ? hud.PlayerName : "YOU") : "POINT " + (hud ? hud.OpponentName : "KAI");
            return RallyShots >= 4 ? $"{who}  ·  {RallyShots}-SHOT RALLY" : who;
        }

        public void SelectCharacter(bool female)
        {
            FemalePlayer = female;
            if (!Player) return;
            Vector3 position = Player.transform.position;
            Destroy(Player.gameObject);
            Player = new GameObject("Player — permanent standard").AddComponent<TennisActor>();
            Player.transform.position = position; Player.Build(female,GolferStyle.SkinColor);
            if (fx) HookActorFx(Player);
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
                // Hit-stop: a few frames of stillness on contact, then play resumes. The phone's
                // input is still read, so nothing the player does is lost.
                if (hitStop > 0) hitStop -= Time.deltaTime;
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
                    accumulator += Mathf.Min(Time.deltaTime, .1f) * GameSpeed;
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
            UpdateCamera(false); UpdateHud(); UpdateTrajectory(); UpdateBallVisual(); UpdateCue();
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

        void MovePlayer(float dt, Vector3 playerPosition)
        {
            Vector2 me = new Vector2(playerPosition.x, playerPosition.z);
            bool live = incoming && Flow == Phase.Rally && resetTimer <= 0 && BallVelocity.z < 0;
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
                        Mathf.Max(0, reactionLeft), GoodJump ? TennisRules.SprintSpeed : TennisRules.RunSpeed, atNet);
                }
                // Stand beside the ball, on the side it is already on, not in its path.
                float side = plan.Found && plan.Point.x < me.x ? -1 : 1;
                goal = plan.Found ? new Vector2(plan.Point.x - side * .65f, plan.Point.z - .65f) : me;
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
                // Recover to the middle: the baseline, or the net if they came in.
                goal = new Vector2(0, atNet && Flow == Phase.Rally ? TennisRules.NetZ : TennisRules.BaselineZ);
                top = TennisRules.RunSpeed * .75f;
                PredictedInterceptX = -100;
            }
            goal.x = Mathf.Clamp(goal.x, -TennisRules.CourtHalfWidth - 2.4f, TennisRules.CourtHalfWidth + 2.4f);
            goal.y = Mathf.Clamp(goal.y, -14.3f, -2.6f);
            MoveGoal = goal;
            // Tired legs are slower; a dive leaves them on the floor for a beat.
            top *= Mathf.Lerp(.72f, 1f, Mathf.Clamp01(Stamina / .35f));
            if (Player.GroundRecovering) top *= .08f;
            Vector2 gap = goal - me; float distance = gap.magnitude;
            float arrive = Mathf.Sqrt(2 * TennisRules.Deceleration * distance);
            Vector2 wanted = distance > .02f ? gap / distance * Mathf.Min(top, arrive) : Vector2.zero;
            moveVelocity = Vector2.MoveTowards(moveVelocity, wanted, dt * TennisRules.Acceleration);
            Vector2 next = me + moveVelocity * dt;
            if (Vector2.Dot(goal - next, gap) < 0 && distance < .3f) { next = goal; moveVelocity = Vector2.zero; }
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
            if (!Player || dt <= 0 || ReplayPlaying) return;
            contactHitter = null;
            AssistOffset = 0;
            Player.Upright = Opponent.Upright = 0;   // only while being introduced
            // The tossing hand is only driven during a serve routine.
            if (!ServeLocked) Player.ReachTossHand(Vector3.zero, 0);
            if (Flow != Phase.OpponentServe) Opponent.ReachTossHand(Vector3.zero, 0);
            Vector3 playerPosition = Player.transform.position;
            if (ServeLocked)
            {
                // Feet are planted for the serve, as in the real thing.
                float stance = TennisRules.ServerStanceX(true, Match.DeuceCourt);
                float planted = Mathf.MoveTowards(playerPosition.x, stance, dt * 7f);
                LateralSpeed = 0; moveVelocity = Vector2.zero;
                Player.transform.position = new Vector3(planted, playerPosition.y, Mathf.MoveTowards(playerPosition.z, -TennisRules.ServeDepth, dt * 4f));
            }
            else MovePlayer(dt, playerPosition);
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
                // The routine: two bounces off the court with the tossing hand, then the wind-up.
                TennisServeRoutine.Pose(Player, phaseTimer, out Vector3 held, out Vector3 palm);
                previousBall = BallPosition; BallPosition = held; BallVelocity = Vector3.zero;
                Player.ReachTossHand(palm, 1);
                Player.Prepare(0, false, true);
                if (phaseTimer >= TennisRules.ServeTossDelay)
                {
                    Flow = Phase.PlayerServeToss; phaseTimer = 0; tossOrigin = TennisServeRoutine.Release(Player);
                    // Tossed up and across to where the serve's strings will meet it.
                    Vector3 across = Player.ContactPoint(TennisActor.Stroke.Serve, false) - tossOrigin; across.y = 0;
                    tossDrift = across / (TennisRules.ServeIdealContact + TennisRules.SweetTime);
                }
                return;
            }
            if (Flow == Phase.PlayerServeToss)
            {
                // Pure ballistic toss so the apex, and therefore the timing window, is honest.
                float rise = 9.81f * TennisRules.ServeApex;
                previousBall = BallPosition;
                BallPosition = PlayerToss(phaseTimer);
                BallVelocity = tossDrift + new Vector3(0, rise - 9.81f*phaseTimer, 0);
                // The server's body follows the toss up into the trophy position; the tossing arm
                // carries on up after the ball until the racket arm swings.
                if (!Player.Swinging) Player.Prepare(Mathf.Clamp01(phaseTimer / TennisRules.ServeIdealContact), false, true);
                Player.ReachTossHand(TennisServeRoutine.TossPalm(Player, phaseTimer), Player.Swinging ? 0 : 1);
                if (phaseTimer >= TennisRules.ServeCatch && !serveLaunchPending && !Player.Swinging)
                { Flow = Phase.PlayerServeHold; phaseTimer = TennisRules.ServeTossDelay - TennisServeRoutine.WindUp; Feedback = "Caught it — tossing again"; Player.Prepare(0, false, true); }
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
                    // The racket meets the toss where it will be at the strike.
                    Opponent.GuideContact(OpponentToss(1), .85f-tossAge);
                }
                if(tossAge>=.85f) { BallPosition=Opponent.SweetSpot.position; Flow=Phase.Rally; phaseTimer=0; Opponent.ReachTossHand(Vector3.zero, 0); ServeFromOpponent(); }
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
            if (incoming && !consumedStroke && Player.Swinging && !pending.Active && TennisRules.CrossStringBed(oldBall, BallPosition, previousRacket,
                Player.SweetSpot.position, Player.StringNormal, Player.StringRight, Player.StringUp, out var faceOffset))
            {
                float reach = Vector3.Distance(Player.transform.position + Vector3.up * 1.1f, BallPosition);
                float reachQuality = Mathf.Clamp01(1 - Mathf.Abs(reach - .85f) / .8f);
                var hit = TennisRules.Evaluate(Player.ContactAge, faceOffset, 1 - Mathf.Abs(LateralSpeed) / 10, reachQuality, Player.Power, Stamina);
                if (hit.Contact) Strike(hit, faceOffset, true);
            }
            // Arcade reach assist: still requires a deliberate, timed swing near the ball.
            // Exact string contact above retains the best quality reward. A dive stretches it.
            if(incoming && !consumedStroke && Player.Swinging && !pending.Active && TennisRules.AssistedContact(oldBall,BallPosition,Player.transform.position,Player.ContactAge,Player.Overhead,out float assist,Player.Power,Player.Kind==TennisActor.Stroke.Dive)) {
                var hit=TennisRules.Evaluate(TennisRules.SweetTime,Vector2.zero,1-Mathf.Abs(LateralSpeed)/10,assist,Player.Power,Stamina);
                hit.Quality=assist; hit.Center=assist; hit.Timing=Mathf.Clamp01(1-Mathf.Abs(Player.ContactAge-TennisRules.SweetTime)/.2f);
                hit.Speed*=Mathf.Lerp(.9f,.97f,assist); hit.ErrorDegrees=Mathf.Lerp(7,3,assist); hit.Label="ASSISTED RETURN";
                Vector2 where=TennisRules.OnStringBed(TennisRules.FaceOffset(BallPosition,Player.SweetSpot.position,Player.StringRight,Player.StringUp));
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
                squash = 1; squashAxis = Vector3.up;
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
            // The opponent reads the ball as it arrives and starts its swing so the racket
            // meets it: the return leaves from its strings at the contact frame, instead of the
            // old instant reply sent from wherever the ball happened to be.
            if (live && !incoming && !opponentShot.Decided && BallVelocity.z > .5f)
            {
                float contactZ = Opponent.transform.position.z - .65f;
                float toArrive = (contactZ - BallPosition.z) / BallVelocity.z;
                if (toArrive <= OpponentSwingLead || BallPosition.z > contactZ)
                {
                    var decision = TennisOpponent.Decide(Opponent.transform.position.x, BallPosition.x + BallVelocity.x * Mathf.Max(0, toArrive),
                        Player.transform.position.x, OpponentDifficulty,
                        (float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble(),
                        Mathf.InverseLerp(15, 30, new Vector2(BallVelocity.x, BallVelocity.z).magnitude));
                    Feedback = decision.Label;
                    opponentShot = new OpponentShot { Decided = true, Decision = decision,
                        Spin = decision.Error ? 0 : Mathf.Lerp(-.6f, .9f, (float)random.NextDouble()) };
                    // Forehand or backhand by where the ball will be when it is met, not where it is.
                    bool ballRight = PredictBall(Mathf.Max(0, toArrive)).x > Opponent.transform.position.x;
                    if (!decision.Reached)
                    {
                        // Just out of reach: it throws itself at the ball anyway, which sells
                        // the winner far better than standing and watching it go by.
                        if (Mathf.Abs(BallPosition.x - Opponent.transform.position.x) < TennisOpponent.Reach + 1.8f)
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
                RecordGap(false, Vector3.Distance(strings, BallPosition));
                if (Vector3.Distance(strings, BallPosition) < 2f) BallPosition = previousBall = strings;
                if (decision.Error && decision.Landing.z > 0)
                {
                    // Netted: struck into the tape, where it drops dead on its own side.
                    InjectBall(BallPosition, TennisRules.ShotVelocity(BallPosition, new Vector3(decision.Landing.x, TennisRules.NetHeight * .55f, 0), 18));
                    bounceRestitution = .75f;
                }
                else SendFromOpponent(decision.Landing, decision.Speed, opponentShot.Spin);
                fx.OpponentContact(BallPosition); sounds.Hit(Timing.Good, .6f); contactHitter = Opponent;
                Player.SplitStep();
                RallyShots++;
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
                // Beside the ball, racket-side, not with it at the belly button.
                if (ballComing) predictedOpponentX = arrival.x + (arrival.x >= Opponent.transform.position.x ? -.65f : .65f);
                bool reading = ballComing && phaseTimer > TennisOpponent.Reaction;
                opponentX = TennisOpponent.Reposition(Opponent.transform.position.x, predictedOpponentX, reading, dt);
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
            if (AutoPlayLean && incoming && Flow == Phase.Rally && plan.Found)
                MoveInput = Mathf.Sign(plan.Point.x - Player.transform.position.x);
            if (Player.Swinging) return;
            if (Flow == Phase.PlayerServeToss && phaseTimer >= TennisRules.ServeIdealContact - .02f)
            { BeginSwing(0, .3f, 1); RequestSwing(.8f, 0, .3f, 1); return; }
            if (Flow != Phase.Rally || !incoming || BallVelocity.z > -1) return;
            float toContact = (BallPosition.z - (Player.transform.position.z + .65f)) / -BallVelocity.z;
            if (toContact < .20f && toContact > .1f)
            {
                float facing = BallPosition.x < Player.transform.position.x ? -1 : 1;
                AimInput = (float)random.NextDouble() * 1.6f - .8f;
                BeginSwing(0, 0, facing); RequestSwing(.55f + (float)random.NextDouble() * .35f, 0, 0, facing);
            }
        }

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
        /// Brief freeze on contact that sells the impact; scaled by how clean the hit was.
        float hitStop;

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
                TennisBall.Integrate(ref p, ref v, spin, TennisBall.Step);
                if (p.y < TennisRules.BallRadius && v.y < 0) { p.y = TennisRules.BallRadius; TennisBall.Bounce(ref v, ref spin, bounceRestitution); }
            }
            return when;
        }

        /// Where the ball will be `seconds` from now, bounces included.
        Vector3 PredictBall(float seconds)
        {
            Vector3 p = BallPosition, v = BallVelocity; float spin = BallSpin;
            for (float t = 0; t < seconds; t += TennisBall.Step)
            {
                TennisBall.Integrate(ref p, ref v, spin, TennisBall.Step);
                if (p.y < TennisRules.BallRadius && v.y < 0) { p.y = TennisRules.BallRadius; TennisBall.Bounce(ref v, ref spin, bounceRestitution); }
            }
            return p;
        }

        void ResolvePending(float dt)
        {
            if (!pending.Active) return;
            pending.Age += dt;
            if (!Player.Swinging || (Player.Provisional && pending.Age > ConfirmGrace * SpeedScale))
            { pending.Active = false; Player.ReleaseContact(); return; }
            // Wait for confirmation, and for the ball to reach the racket.
            // The racket's last pose was a step ago, so the ball leaves one step after the lead:
            // on the frame the strings were posed on it.
            float due = pending.Lead > 0 ? pending.Lead + TennisBall.Step - 1e-4f : 0;
            if (Player.Provisional || pending.Age < due) return;
            pending.Active = false;
            BallPosition = pending.At;
            ReturnBall(pending.Hit, pending.Face);
            // Fly the ball on for the time the confirmation took, so the shot is exactly where
            // it would have been had the phone confirmed instantly.
            Vector3 p = BallPosition, v = BallVelocity;
            for (float t = 0; t < pending.Age - due; t += TennisBall.Step) TennisBall.Integrate(ref p, ref v, BallSpin, TennisBall.Step);
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

        void ReturnBall(TennisHit hit, Vector2 faceOffset)
        {
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
                Stamina=Mathf.Max(0,Stamina-TennisRules.DiveStamina);
            }
            // Grade the contact, then reward three well-timed balls in a row.
            LastGrade = TennisRules.Grade(hit.Timing);
            LastFaceOffset = faceOffset;
            Streak = TennisRules.Extends(LastGrade) ? Streak + 1 : 0;
            LastWasSupercharged = Streak >= TennisRules.SuperchargeStreak;
            if (LastWasSupercharged) { hit = TennisRules.Supercharge(hit); Streak = 0; }
            if (hud) hud.ShowGrade(LastGrade, LastWasSupercharged, $"{Player.StrokeLabel.ToUpperInvariant()}  ·  {hit.Speed * 3.6f:0} KM/H");
            if (hitMap) hitMap.Record(faceOffset, LastGrade, LastWasSupercharged);
            if (coach) { coach.Record(LastGrade); if (Hits >= 3 && NativeControlled) coach.Offer(TennisCoach.Tip.Timing); }
            LastHit = hit; Hits++; Returns++; consumedStroke = true; incoming = false; bounces = 0; opponentShot = default;
            serveInFlight = false; bounceRestitution=.75f;
            RallyShots++;
            float error = ((float)random.NextDouble()*2-1) * hit.ErrorDegrees;
            // Aimed by the racket face; how much of the court is available, and how deep, is
            // decided by the contact. The error cone then applies on top.
            Vector3 target=TennisRules.AimedTarget(AimInput,hit.Quality,Player.Kind==TennisActor.Stroke.Lob ? .4f : 0);
            target.x+=Mathf.Tan(error*Mathf.Deg2Rad)*(target.z-BallPosition.z);
            BallSpin = SpinFor(Player.Kind, Player.Power);
            BallVelocity = TennisRules.RallyVelocity(BallPosition,target,hit.Speed,BallSpin,hit.Quality);
            Feedback = $"{TennisRules.GradeLabel(LastGrade)} · {Player.StrokeLabel} · {hit.Label} · {hit.Speed*3.6f:0} km/h"
                + (Streak > 0 ? $"\n{Streak} clean in a row — {TennisRules.SuperchargeStreak - Streak} to supercharge" : "");
            ballRenderer.material.color = LastWasSupercharged ? new Color(.7f,1,1) : Color.Lerp(new Color(1,.8f,.7f), Color.white, hit.Quality);
            fx.Contact(BallPosition, LastGrade, LastWasSupercharged);
            if (LastWasSupercharged) Player.SetExpression(TennisActor.Expression.Surprised, .7f);
            else if (LastGrade >= Timing.Excellent) Player.SetExpression(TennisActor.Expression.Happy, .8f);
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
            serveLaunchPending = false; pending.Active = false;
            StartReading();
            if (ball) ball.position = renderedBall = position;
        }

        /// Start the next point from whichever end is serving.
        void BeginPoint() {
            Stamina=1; SecondServe=false; consumedStroke=false;
            bounces=0; bounceRestitution=.75f; incoming=true; serveInFlight=false; faultDelay=0; serveLaunchPending=false;
            serveTimer=0; phaseTimer=0; BallVelocity=Vector3.zero; BallSpin=0; RallyShots=0; replayDue=-1;
            resetTimer=0; LastWasSupercharged=false;
            ballRenderer.material.color=Color.white;
            ballTrail.Clear();
            matchPoint = IsMatchPoint(Match);
            crowd.Hush(matchPoint);
            if (matchPoint) { banner.text = "MATCH POINT"; bannerUntil = HudClock.Now + 2.2f; }
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
                    Player.transform.position.y, TennisRules.BaselineZ);
                moveVelocity=Vector2.zero;
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
            Vector3 strings=Opponent.SweetSpot.position+Vector3.back*(TennisRules.BallRadius*1.2f);
            if(Vector3.Distance(strings,start)<1.2f) start=strings;
            bounceRestitution=.60f;
            InjectBall(start,TennisRules.ServeVelocity(start,plan.Landing,plan.Speed,plan.Spin));
            BallSpin=plan.Spin;
            serveInFlight=true; serveFromNearSide=false;
            RallyShots = 1;
            fx.OpponentContact(start); sounds.Hit(Timing.Great, .8f); contactHitter = Opponent;
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
        public const float CueLead = .24f, CueSpan = .7f;
        void UpdateCue()
        {
            if (!fx) return;
            var cam = GameplayCamera;
            if (Flow == Phase.PlayerServeToss && !Player.Swinging && !serveLaunchPending)
            {
                float ideal = TennisRules.ServeIdealContact + TennisRules.ServeOnsetLatency;
                fx.Cue(phaseTimer < ideal + .12f, renderedBall, cam, Mathf.Clamp01((ideal - phaseTimer) / .6f));
                return;
            }
            bool show = Flow == Phase.Rally && incoming && !Player.Swinging && faultDelay <= 0 && BallVelocity.z < -1;
            float toContact = show ? (BallPosition.z - (Player.transform.position.z + .65f)) / -BallVelocity.z : 0;
            show &= toContact > .05f && toContact < CueLead + CueSpan;
            fx.Cue(show, renderedBall, cam, Mathf.Clamp01((toContact - CueLead) / CueSpan));
        }

        float cameraShakeSeed;
        /// Seconds of opening flyover left: the camera sweeps in over the resort to the
        /// player before the first serve. Game time, so it waits while the session is paused.
        TennisPresentation presentation;
        public const float IntroSeconds = TennisPresentation.Length;
        /// Play runs this much faster than real time: balls, feet and strokes alike. The
        /// phone's latencies are real seconds and are converted wherever they meet game time.
        public const float GameSpeed = 1.2f;
        float SpeedScale => ManualSimulation ? 1 : GameSpeed;
        public bool IntroPlaying => presentation && presentation.Playing && !ManualSimulation && !AutoPlay;
        void UpdateCamera(bool immediate)
        {
            var camera = GameplayCamera ? GameplayCamera : Camera.main; if (!camera || !Player) return;
            // Wii Sports keeps the character large and close; the animation is the feedback.
            float lead = Mathf.Clamp(Player.transform.position.x, -5f, 5f);
            Vector3 target = new Vector3(lead*.55f, 3.5f, Player.transform.position.z - 4.4f);
            Vector3 look = new Vector3(lead*.35f, 1.15f, Player.transform.position.z + 8f);
            float fov = 56;
            if (ServeLocked)
            {
                // Serve camera: in tight behind the server's shoulder, looking down the
                // diagonal the serve has to travel.
                Vector3 box = TennisRules.ServeTargetCentre(true, Match.DeuceCourt);
                // Wide enough, and off the tossing shoulder, to see the whole routine: the
                // bounces at waist height and the toss going up.
                float tossSide = Player.LeftHanded ? 1 : -1;     // the camera sits on the tossing side
                target = Player.transform.position + new Vector3(tossSide * 2.1f, 2.5f, -4.4f);
                look = Vector3.Lerp(Player.transform.position + Vector3.up * 1.2f, box, .3f);
                fov = 52;
            }
            if (matchPoint && Serving) fov -= 4;
            if (IntroPlaying && presentation.Drive(camera, target, look, fov, Time.deltaTime)) return;
            if (presentation && presentation.Playing) presentation.Finish();
            float rate = 1 - Mathf.Exp(-Time.deltaTime * GameSpeed * (ServeLocked ? 4 : 7));
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
            string pause = NativeControlled ? NativeSportsSession.PauseReason : null;
            string warning = NativeControlled ? NativeSportsSession.TrackingWarning : null;
            if (!ReferenceEquals(pause, shownPause) || !ReferenceEquals(warning, shownWarning))
            {
                shownPause = pause; shownWarning = warning;
                status.text = !string.IsNullOrEmpty(pause) ? pause : warning ?? "";
            }
            if (banner.text.Length > 0 && HudClock.Now > bannerUntil && !ReplayPlaying) banner.text = "";
            float f = fx ? fx.Flash * .35f : 0;
            if (Mathf.Abs(f - shownFlash) > .002f) { shownFlash = f; flash.canvasRenderer.SetAlpha(f); }
        }
    }
}
