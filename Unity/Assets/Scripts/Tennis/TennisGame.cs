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
        public void Refeed() { resetTimer=0; Feed(); }
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
        public bool Sprint;
        Transform ball;
        Renderer ballRenderer;
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
            Destroy(sphere.GetComponent<Collider>()); ball = sphere.transform; ball.localScale = Vector3.one * .067f;
            ballRenderer = sphere.GetComponent<Renderer>(); ballRenderer.sharedMaterial = GolfArcade.Course.HoleView.Mat(new Color(.82f,1,.12f));
            var trail = sphere.AddComponent<TrailRenderer>(); trail.time = .16f; trail.startWidth = .035f; trail.endWidth = .006f;
            trail.sharedMaterial = GolfArcade.Course.HoleView.Mat(new Color(.83f,1,.2f));
            var camera = Camera.main;
            if (!camera) { camera = new GameObject("Tennis gameplay camera").AddComponent<Camera>(); camera.tag = "MainCamera"; camera.gameObject.AddComponent<AudioListener>(); }
            camera.fieldOfView = 56; camera.nearClipPlane = .08f; camera.farClipPlane = 600;
            camera.backgroundColor = new Color(.49f,.73f,.88f); camera.clearFlags = CameraClearFlags.SolidColor;
            var light = new GameObject("Resort sun").AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.15f;
            light.transform.rotation = Quaternion.Euler(48,-35,0); light.shadows = LightShadows.Soft;
            RenderSettings.ambientLight = new Color(.70f,.76f,.80f);
            BuildHud(); gameObject.AddComponent<TennisPhoneInput>(); Feed(); UpdateCamera(true);
        }

        public void RequestSwing(float power)
        {
            if (!Player || Player.Swinging || resetTimer > 0) return;
            Player.Swing(power, (BallPosition.x < Player.transform.position.x) != NativeSportsSession.Left);
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
            Feed();
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
                if (Input.GetKeyDown(KeyCode.R)) { resetTimer = 0; Feed(); }
                if (Input.GetKeyDown(KeyCode.F1)) SelectCharacter(!FemalePlayer);
                if (Input.GetKeyDown(KeyCode.G)) UnityEngine.SceneManagement.SceneManager.LoadScene("Golf");
                accumulator += Mathf.Min(Time.deltaTime, .1f);
                while (accumulator >= 1f/120) { Step(1f/120); accumulator -= 1f/120; }
            }
            UpdateCamera(false); UpdateHud();
        }

        public void Step(float dt)
        {
            if (!Player || dt <= 0) return;
            float maximum = Mathf.Lerp(3.2f, Sprint ? TennisRules.SprintSpeed : TennisRules.RunSpeed, Mathf.Clamp01(Stamina / .3f));
            LateralSpeed = Mathf.MoveTowards(LateralSpeed, Mathf.Clamp(MoveInput,-1,1) * maximum, dt * TennisRules.Acceleration);
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
            if (resetTimer > 0) { resetTimer -= dt; if (resetTimer <= 0) Feed(); return; }
            Vector3 oldBall = BallPosition;
            BallPosition += BallVelocity * dt + Vector3.down * (4.905f * dt * dt);
            BallVelocity += Vector3.down * (9.81f * dt);
            if (incoming && !consumedStroke && Player.Swinging && TennisRules.CrossStringBed(oldBall, BallPosition, previousRacket,
                Player.SweetSpot.position, Player.StringNormal, Player.StringRight, Player.StringUp, out var faceOffset))
            {
                float reach = Vector3.Distance(Player.transform.position + Vector3.up * 1.1f, BallPosition);
                float reachQuality = Mathf.Clamp01(1 - Mathf.Abs(reach - .85f) / .8f);
                var hit = TennisRules.Evaluate(Player.SwingAge, faceOffset, 1 - Mathf.Abs(LateralSpeed) / 10, reachQuality, Player.Power, Stamina);
                if (hit.Contact) ReturnBall(hit);
            }
            if (oldBall.z * BallPosition.z < 0)
            {
                float fraction = Mathf.Abs(oldBall.z / (BallPosition.z - oldBall.z));
                float height = Mathf.Lerp(oldBall.y, BallPosition.y, fraction);
                if (height < .97f && Mathf.Abs(BallPosition.x) < 6.4f) EndFeed("NET — more lift or cleaner contact");
            }
            if (BallPosition.y < .034f && BallVelocity.y < 0)
            {
                bounces++;
                BallPosition = new Vector3(BallPosition.x,.034f,BallPosition.z);
                BallVelocity = new Vector3(BallVelocity.x*.94f,-BallVelocity.y*.75f,BallVelocity.z*.94f);
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
            LastHit = hit; Hits++; consumedStroke = true; incoming = false; bounces = 0;
            float error = ((float)random.NextDouble()*2-1) * hit.ErrorDegrees;
            Vector3 direction = Quaternion.Euler(0,error,0) * new Vector3(Mathf.Clamp(AimInput,-1,1)*.22f,0,1).normalized;
            float distance = Mathf.Max(8, 9 - BallPosition.z), flightTime = distance / hit.Speed;
            float vertical = (.034f - BallPosition.y + 4.905f * flightTime * flightTime) / flightTime;
            BallVelocity = direction * hit.Speed + Vector3.up * vertical;
            Feedback = $"{hit.Label} · {hit.Speed*3.6f:0} km/h\nTiming {hit.Timing:P0}  Center {hit.Center:P0}  Position {hit.Positioning:P0}";
            ballRenderer.material.color = Color.Lerp(new Color(1,.34f,.15f), new Color(.4f,1,.6f), hit.Quality);
        }

        public void InjectBall(Vector3 position, Vector3 velocity)
        { BallPosition = position; BallVelocity = velocity; incoming = true; bounces = 0; resetTimer = 0; consumedStroke = false; if (ball) ball.position = position; }

        void Feed()
        {
            float target = Mathf.Sin(feedIndex++ * 1.7f) * 2.7f;
            InjectBall(new Vector3(Opponent.transform.position.x,2.4f,9), new Vector3((target-Opponent.transform.position.x)/1.1f,.9f,-19.5f));
        }

        void EndFeed(string reason)
        { if (resetTimer > 0) return; Feedback = reason; Misses++; resetTimer = .8f; }

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
            status.text = Feedback; help.text = InputStatus + "\nR refeed · C calibrate phone tilt · F1 character · G golf";
            staminaFill.rectTransform.sizeDelta = new Vector2(220*Stamina,12);
            staminaFill.color = Color.Lerp(new Color(1,.3f,.2f), new Color(.25f,.9f,.7f), Stamina);
        }
    }
}
