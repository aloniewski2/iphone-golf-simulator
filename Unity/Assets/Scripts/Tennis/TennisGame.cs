using GolfArcade.Game;
using UnityEngine;
using UnityEngine.UI;

namespace GolfArcade.Tennis
{
    /// First playable singles rally lab. Ball simulation runs at a fixed 120 Hz.
    public sealed class TennisGame : MonoBehaviour
    {
        public bool FemalePlayer;
        public bool ManualSimulation;
        public bool NativeControlled;
        public bool Initialized { get; private set; }
        public Camera GameplayCamera { get; private set; }
        public void Refeed() { resetTimer=0; BeginServe(); }
        public bool Serving { get; private set; }
        float serveTimer;
        float bounceRestitution=.75f;
        public TennisActor Player { get; private set; }
        public TennisActor Opponent { get; private set; }
        public Vector3 BallPosition { get; private set; }
        public Vector3 BallVelocity { get; private set; }
        public float Stamina { get; private set; } = 1;
        public float LateralSpeed { get; private set; }
        public TennisHit LastHit { get; private set; }
        public int Hits { get; private set; }
        public int Misses { get; private set; }
        public string Feedback { get; private set; } = "Find your position. Time the swing.";
        public float MoveInput, AimInput;
        /// How far the assist wants to shift the player, in court metres, and where the ball
        /// is predicted to arrive. Exposed for the motion servo, the HUD and tests.
        public float AssistOffset { get; private set; }
        public float PredictedInterceptX { get; private set; }
        public bool Sprint;
        Transform ball;
        Renderer ballRenderer;
        LineRenderer trajectory, aimRing;
        Text title, status, help;
        Image staminaFill;
        float accumulator, resetTimer, charge;
        int bounces, feedIndex;
        bool incoming = true, consumedStroke;
        readonly System.Random random = new(2701);
        Vector3 previousRacket;
        public string InputStatus = "Keyboard · A/D move · Shift sprint · hold/release Space · arrows aim";

        void Start()
        {
            var arena = Resources.Load<GameObject>("Tennis/CoastalTennisResort");
            if (!arena) throw new System.InvalidOperationException("Approved coastal tennis arena export is missing");
            var environment = Instantiate(arena); environment.name = "Approved coastal tennis resort";
            TennisActor.PrepareMaterials(environment);
            Player = new GameObject("Player — permanent standard").AddComponent<TennisActor>();
            Player.transform.position = new Vector3(0, .035f, -11.2f);
            Player.Build(FemalePlayer, GolferStyle.SkinColor);
            Opponent = new GameObject("Opponent — permanent standard").AddComponent<TennisActor>();
            Opponent.transform.SetPositionAndRotation(new Vector3(0, .035f, 11.2f), Quaternion.Euler(0,180,0));
            Opponent.Build(!FemalePlayer, new Color(.52f,.31f,.18f));
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere); sphere.name = "Tennis ball";
            Destroy(sphere.GetComponent<Collider>()); ball = sphere.transform; ball.localScale = Vector3.one * (TennisRules.BallRadius*2);
            ballRenderer = sphere.GetComponent<Renderer>(); ballRenderer.sharedMaterial = GolfArcade.Course.HoleView.Mat(new Color(.82f,1,.12f));
            var trail = sphere.AddComponent<TrailRenderer>(); trail.time = .24f; trail.startWidth = .09f; trail.endWidth = .015f;
            trail.sharedMaterial = GolfArcade.Course.HoleView.Mat(new Color(.83f,1,.2f));
            trajectory=MakeLine("Predicted ball flight",new Color(.1f,.9f,1),.035f);
            aimRing=MakeLine("Shot aim target",new Color(1,.85f,.1f),.06f);
            var camera = Camera.main;
            if (!camera) { camera = new GameObject("Tennis gameplay camera").AddComponent<Camera>(); camera.tag = "MainCamera"; camera.gameObject.AddComponent<AudioListener>(); }
            GameplayCamera=camera;
            camera.fieldOfView = 56; camera.nearClipPlane = .08f; camera.farClipPlane = 600;
            camera.backgroundColor = new Color(.49f,.73f,.88f); camera.clearFlags = CameraClearFlags.SolidColor;
            var light = new GameObject("Resort sun").AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.15f;
            light.transform.rotation = Quaternion.Euler(48,-35,0); light.shadows = LightShadows.Soft;
            RenderSettings.ambientLight = new Color(.70f,.76f,.80f);
            BuildHud(); gameObject.AddComponent<TennisPhoneInput>(); BeginServe(); UpdateCamera(true);
            Initialized=true;
        }

        public void RequestSwing(float power, float handSide=0, float lift=0, float strokeFacing=0)
        {
            if (!Player || Player.Swinging || resetTimer > 0) return;
            bool leftSide=Mathf.Abs(handSide)>.14f ? handSide<0 : BallPosition.x<Player.transform.position.x;
            bool smash=BallPosition.y>1.8f && (lift>.12f || power>.65f);
            Player.Swing(power,TennisRules.UseBackhand(strokeFacing,leftSide,NativeSportsSession.Left),smash);
            Feedback=Player.StrokeLabel;
            consumedStroke = false;
        }

        public void SelectCharacter(bool female)
        {
            FemalePlayer = female;
            if (!Player) return;
            Vector3 position = Player.transform.position;
            Destroy(Player.gameObject);
            Player = new GameObject("Player — permanent standard").AddComponent<TennisActor>();
            Player.transform.position = position; Player.Build(female,GolferStyle.SkinColor);
            BeginServe();
        }

        // Public adapter boundary: tracked phone position can supply this without changing physics.
        public void SetLateralInput(float normalizedSpeed, bool sprint)
        { MoveInput = Mathf.Clamp(normalizedSpeed,-1,1); Sprint = sprint; }

        void Update()
        {
            if (!Player) return;
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
                accumulator += Mathf.Min(Time.deltaTime, .1f);
                while (accumulator >= 1f/120) { Step(1f/120); accumulator -= 1f/120; }
            }
            UpdateCamera(false); UpdateHud(); UpdateTrajectory();
        }

        public void Step(float dt)
        {
            if (!Player || dt <= 0) return;
            float maximum = Mathf.Lerp(3.2f, Sprint ? TennisRules.SprintSpeed : TennisRules.RunSpeed, Mathf.Clamp01(Stamina / .3f));
            // Assisted movement: lean toward where the ball is actually heading, but stop
            // short of it so the player still has to cover the last stretch themselves.
            //
            // This is published as a court-space offset rather than folded into MoveInput,
            // because motion control is a position servo on `target`: an assist added to the
            // velocity command would simply be cancelled by the servo on the next frame.
            // Callers add AssistOffset to the position they are steering toward.
            AssistOffset = 0;
            if (incoming && !Serving && resetTimer <= 0 &&
                TennisRules.PredictInterceptX(BallPosition, BallVelocity, Player.transform.position.z, bounceRestitution, out float interceptX))
            {
                PredictedInterceptX = interceptX;
                AssistOffset = TennisRules.MovementAssist(Player.transform.position.x, interceptX);
            }
            float command = Mathf.Clamp(MoveInput,-1,1);
            if (!NativeControlled) command = Mathf.Clamp(command + AssistOffset / TennisRules.AssistDeadBand, -1, 1);
            LateralSpeed = Mathf.MoveTowards(LateralSpeed, command * maximum, dt * TennisRules.Acceleration);
            Vector3 playerPosition = Player.transform.position;
            float nextX = Mathf.Clamp(playerPosition.x + LateralSpeed * dt, -6.5f, 6.5f);
            LateralSpeed = (nextX - playerPosition.x) / dt;
            Player.transform.position = new Vector3(nextX, playerPosition.y, playerPosition.z);
            Stamina = TennisRules.StaminaStep(Stamina, LateralSpeed, dt);
            previousRacket = Player.SweetSpot.position;
            bool wasSwinging = Player.Swinging;
            Player.Tick(dt, LateralSpeed);
            if (wasSwinging && !Player.Swinging && !consumedStroke) Feedback = "WHIFF — swing earlier/later or reposition";
            float opponentX = Mathf.MoveTowards(Opponent.transform.position.x, Mathf.Clamp(BallPosition.x, -4,4), dt * 6.2f);
            float opponentSpeed = (opponentX - Opponent.transform.position.x) / dt;
            Opponent.transform.position = new Vector3(opponentX,.035f,11.2f); Opponent.Tick(dt, opponentSpeed);
            if (resetTimer > 0) { resetTimer -= dt; if (resetTimer <= 0) BeginServe(); return; }
            if(Serving) {
                serveTimer+=dt;
                float toss=Mathf.Clamp01(serveTimer/.85f);
                BallPosition=Opponent.transform.TransformPoint(new Vector3(.35f,1.2f+Mathf.Sin(toss*Mathf.PI*.65f)*1.45f,.3f));
                ball.position=BallPosition;
                if(serveTimer>=.67f && !Opponent.Swinging) Opponent.Serve(.6f);
                if(serveTimer>=.85f) { BallPosition=Opponent.SweetSpot.position; Serving=false; Feed(true); }
                return;
            }
            Vector3 oldBall = BallPosition;
            BallPosition += BallVelocity * dt + Vector3.down * (4.905f * dt * dt);
            BallVelocity += Vector3.down * (9.81f * dt);
            if (incoming && !consumedStroke && Player.Swinging && TennisRules.CrossStringBed(oldBall, BallPosition, previousRacket,
                Player.SweetSpot.position, Player.StringNormal, Player.StringRight, Player.StringUp, out var faceOffset))
            {
                float reach = Vector3.Distance(Player.transform.position + Vector3.up * 1.1f, BallPosition);
                float reachQuality = Mathf.Clamp01(1 - Mathf.Abs(reach - .85f) / .8f);
                var hit = TennisRules.Evaluate(Player.ContactAge, faceOffset, 1 - Mathf.Abs(LateralSpeed) / 10, reachQuality, Player.Power, Stamina);
                if (hit.Contact) ReturnBall(hit);
            }
            // Arcade reach assist: still requires a deliberate, timed swing near the ball.
            // Exact string contact above retains the best quality reward.
            if(incoming && !consumedStroke && Player.Swinging && TennisRules.AssistedContact(oldBall,BallPosition,Player.transform.position,Player.ContactAge,Player.Overhead,out float assist,Player.Power)) {
                var hit=TennisRules.Evaluate(TennisRules.SweetTime,Vector2.zero,1-Mathf.Abs(LateralSpeed)/10,assist,Player.Power,Stamina);
                hit.Quality=assist; hit.Center=assist; hit.Timing=Mathf.Clamp01(1-Mathf.Abs(Player.ContactAge-TennisRules.SweetTime)/.2f);
                hit.Speed*=Mathf.Lerp(.9f,.97f,assist); hit.ErrorDegrees=Mathf.Lerp(7,3,assist); hit.Label="ASSISTED RETURN";
                ReturnBall(hit);
            }
            if (oldBall.z * BallPosition.z < 0)
            {
                float fraction = Mathf.Abs(oldBall.z / (BallPosition.z - oldBall.z));
                float height = Mathf.Lerp(oldBall.y, BallPosition.y, fraction);
                if (height < .97f && Mathf.Abs(BallPosition.x) < 6.4f) EndFeed("NET — more lift or cleaner contact");
            }
            if (BallPosition.y < TennisRules.BallRadius && BallVelocity.y < 0)
            {
                bounces++;
                BallPosition = new Vector3(BallPosition.x,TennisRules.BallRadius,BallPosition.z);
                BallVelocity = new Vector3(BallVelocity.x*.94f,-BallVelocity.y*bounceRestitution,BallVelocity.z*.94f);
                if (Mathf.Abs(BallPosition.x) > TennisRules.CourtHalfWidth || Mathf.Abs(BallPosition.z) > TennisRules.CourtHalfLength)
                    EndFeed("OUT — control power and positioning");
                else if (bounces > 1) EndFeed("DOUBLE BOUNCE — get there sooner");
            }
            // Training partner catches and feeds at the far end; not a competitive AI.
            if (!incoming && BallPosition.z > 9 && resetTimer <= 0)
            { Opponent.Swing(.6f, BallPosition.x > Opponent.transform.position.x); Feed(); }
            if (BallPosition.z < -14 || BallPosition.y < -1) EndFeed("MISSED BALL — move into range and time your swing");
            ball.position = BallPosition;
        }

        void ReturnBall(TennisHit hit)
        {
            if(Player.Overhead) hit.Speed=Mathf.Min(24,hit.Speed*1.1f);
            LastHit = hit; Hits++; consumedStroke = true; incoming = false; bounces = 0; bounceRestitution=.75f;
            float error = ((float)random.NextDouble()*2-1) * hit.ErrorDegrees;
            Vector3 target=TennisRules.ShotTarget(AimInput,Player.Power);
            target.x+=Mathf.Tan(error*Mathf.Deg2Rad)*(target.z-BallPosition.z);
            BallVelocity = TennisRules.ShotVelocity(BallPosition,target,hit.Speed);
            Feedback = $"{Player.StrokeLabel} · {hit.Label} · {hit.Speed*3.6f:0} km/h\nTiming {hit.Timing:P0}  Center {hit.Center:P0}  Position {hit.Positioning:P0}";
            ballRenderer.material.color = Color.Lerp(new Color(1,.34f,.15f), new Color(.4f,1,.6f), hit.Quality);
        }

        public void InjectBall(Vector3 position, Vector3 velocity)
        { Serving=false; BallPosition = position; BallVelocity = velocity; incoming = true; bounces = 0; resetTimer = 0; consumedStroke = false; if (ball) ball.position = position; }

        void BeginServe() {
            Stamina=1;
            Serving=true; serveTimer=0; BallVelocity=Vector3.zero;
            BallPosition=Opponent.transform.TransformPoint(new Vector3(.35f,1.2f,.3f)); ball.position=BallPosition;
            Feedback="Opponent serving — move into position, then swing";
            ballRenderer.material.color=new Color(.82f,1,.12f);
            ball.GetComponent<TrailRenderer>().Clear();
        }

        void Feed(bool serve=false)
        {
            float target = Mathf.Sin(feedIndex++ * 1.7f) * 2.7f;
            Vector3 start=BallPosition;
            float flight=serve ? 1.2f : 1.45f;
            // Opening serve lands inside the service box, then reaches the receiver at racket height.
            Vector3 landing=new Vector3(serve ? 1.2f : target,TennisRules.BallRadius,serve ? -5.7f : -7.5f);
            Vector3 velocity=(landing-start)/flight+Vector3.up*(4.905f*flight);
            bounceRestitution=serve ? .60f : .75f;
            InjectBall(start,velocity);
        }

        void EndFeed(string reason)
        { if (resetTimer > 0) return; Feedback = reason; Misses++; Stamina=1; resetTimer = .8f; }

        LineRenderer MakeLine(string label,Color color,float width) {
            var line=new GameObject(label).AddComponent<LineRenderer>();
            line.transform.SetParent(transform); line.useWorldSpace=true;
            line.sharedMaterial=GolfArcade.Course.HoleView.Mat(color);
            line.startWidth=line.endWidth=width;
            line.numCapVertices=3; return line;
        }

        void UpdateTrajectory() {
            trajectory.enabled=!Serving && resetTimer<=0;
            Vector3 p=BallPosition,v=BallVelocity;
            trajectory.positionCount=41;
            for(int i=0;i<41;i++) {
                trajectory.SetPosition(i,p);
                if(p.y<=TennisRules.BallRadius && v.y<0) { trajectory.positionCount=i+1; break; }
                Vector3 next=p+v*.05f+Vector3.down*(4.905f*.05f*.05f);
                if(next.y<TennisRules.BallRadius) next=Vector3.Lerp(p,next,Mathf.Clamp01((p.y-TennisRules.BallRadius)/(p.y-next.y)));
                p=next; v+=Vector3.down*(9.81f*.05f);
            }
            Vector3 target=TennisRules.ShotTarget(AimInput,.5f); target.y=.06f;
            aimRing.positionCount=33;
            for(int i=0;i<33;i++) {
                float angle=i*Mathf.PI*2/32;
                aimRing.SetPosition(i,target+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*.4f);
            }
        }

        void UpdateCamera(bool immediate)
        {
            var camera = Camera.main; if (!camera || !Player) return;
            Vector3 target = new Vector3(Player.transform.position.x*.20f,6.4f,-18.8f);
            camera.transform.position = immediate ? target : Vector3.Lerp(camera.transform.position,target,1-Mathf.Exp(-Time.deltaTime*7));
            camera.transform.LookAt(new Vector3(Player.transform.position.x*.10f,.55f,-.5f));
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
                text.alignment = TextAnchor.MiddleLeft;
                var rect = text.rectTransform; rect.anchorMin = rect.anchorMax = anchor; rect.pivot = anchor; rect.sizeDelta = size; rect.anchoredPosition = offset;
                go.AddComponent<Shadow>().effectDistance = new Vector2(1,-1); return text;
            }
            title = Label("Rally score",new Vector2(0,1),new Vector2(1050,90),new Vector2(28,-15),24);
            status = Label("Live hit feedback",new Vector2(0,0),new Vector2(1200,100),new Vector2(28,75),24);
            help = Label("Controls",Vector2.zero,new Vector2(1230,55),new Vector2(28,10),17);
            var bar = new GameObject("Stamina").AddComponent<Image>(); bar.transform.SetParent(canvas.transform,false); bar.color = new Color(.25f,.9f,.7f);
            staminaFill = bar; bar.rectTransform.anchorMin = bar.rectTransform.anchorMax = new Vector2(0,1); bar.rectTransform.pivot = new Vector2(0,1); bar.rectTransform.anchoredPosition = new Vector2(28,-110); bar.rectTransform.sizeDelta = new Vector2(220,12);
        }

        void UpdateHud()
        {
            title.text = $"COASTAL TENNIS · RALLY LAB\nHits {Hits}  |  Resets {Misses}  |  Stamina {Stamina:P0}  |  Swing power {charge:P0}";
            status.text = NativeControlled && !string.IsNullOrEmpty(NativeSportsSession.PauseReason) ? NativeSportsSession.PauseReason : Feedback;
            bool degraded = NativeControlled && !string.IsNullOrEmpty(NativeSportsSession.TrackingWarning);
            if (degraded) status.text = NativeSportsSession.TrackingWarning + "\n" + status.text;
            status.color = degraded ? new Color(1,.72f,.25f) : Color.white;
            help.text = NativeControlled ? "Move sideways · Swing phone to hit · Yellow: aim / Cyan: flight · Quit on phone" : InputStatus + "\nR serve · C calibrate phone tilt · F1 character · G golf";
            staminaFill.rectTransform.sizeDelta = new Vector2(220*Stamina,12);
            staminaFill.color = Color.Lerp(new Color(1,.3f,.2f), new Color(.25f,.9f,.7f), Stamina);
        }
    }
}
