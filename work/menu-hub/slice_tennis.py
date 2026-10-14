# Applies the plaza's time-sliced build to TennisGame.Start (idempotent). usage: slice_tennis.py <TennisGame.cs>
import sys
p = sys.argv[1]; s = open(p).read()
if "public static bool SliceInit" in s: print("already sliced"); sys.exit(0)
Y = "if (SliceInit) { InitProgress = %s; yield return null; }"
edits = [
 ("        void Start()\n        {\n            var arena = Resources.Load<GameObject>(\"Tennis/TropicalV3/TropicalTennisResort\");",
  "        /// PLAN_MenuHub_WalkableWorld §5: a match loading behind the plaza builds over several frames (each step one frame) so the\n"
  "        /// plaza on the TV never hitches. Off for every other launch: Start then runs to the end in its first frame, as before.\n"
  "        public static bool SliceInit;\n"
  "        /// 0..1 through the build (only advanced while SliceInit).\n"
  "        public float InitProgress { get; private set; }\n"
  "        IEnumerator Start()\n        {\n            var arena = Resources.Load<GameObject>(\"Tennis/TropicalV3/TropicalTennisResort\");"),
 ("            var environment = Instantiate(arena); environment.name",
  "            GameObject environment;\n"
  "            if (SliceInit) { var made = InstantiateAsync(arena); while (!made.isDone) yield return null; environment = made.Result[0]; }\n"
  "            else environment = Instantiate(arena);\n"
  "            environment.name"),
 ("            TennisLook.StyleArena(environment);\n", "            TennisLook.StyleArena(environment);\n            " + (Y % ".2f") + "\n"),
 ("            else TennisVenueBuilder.Build(venue, environment);\n", "            else TennisVenueBuilder.Build(venue, environment);\n            " + (Y % ".35f") + "\n"),
 ("            if (venue == TennisVenueKind.Resort) { stands = gameObject.AddComponent<TennisStandsCrowd>(); stands.Build(null); }\n",
  "            if (venue == TennisVenueKind.Resort) { stands = gameObject.AddComponent<TennisStandsCrowd>(); stands.Build(null); }\n            " + (Y % ".45f") + "\n"),
 ("            Player.Build(FemalePlayer, PlayerSkinFor(FemalePlayer), NativeSportsSession.Left, PlayerBody(FemalePlayer));\n",
  "            Player.Build(FemalePlayer, PlayerSkinFor(FemalePlayer), NativeSportsSession.Left, PlayerBody(FemalePlayer));\n            " + (Y % ".5f") + "\n"),
 ("            Opponent.Build(!FemalePlayer, new Color(.52f,.31f,.18f), false);\n            Opponent.Motion = TennisActor.Style.Rival;\n",
  "            Opponent.Build(!FemalePlayer, new Color(.52f,.31f,.18f), false);\n            Opponent.Motion = TennisActor.Style.Rival;\n            " + (Y % ".58f") + "\n"),
 ("            TennisHeroSetup.Attach(this);\n            BuildBall();\n",
  "            if (SliceInit) { TennisHeroSetup.AttachPlayer(this); InitProgress = .64f; yield return null; TennisHeroSetup.AttachRival(this); }   // = Attach, one hero a frame\n"
  "            else TennisHeroSetup.Attach(this);\n            " + (Y % ".7f") + "\n            BuildBall();\n"),
 ("            fx = new GameObject(\"Tennis effects\").AddComponent<TennisFx>(); fx.transform.SetParent(transform); fx.Build();\n",
  "            fx = new GameObject(\"Tennis effects\").AddComponent<TennisFx>(); fx.transform.SetParent(transform); fx.Build();\n            " + (Y % ".8f") + "\n"),
 ("            replay.Build(new[] { Player.transform, Opponent.transform }, ball, camera, OnReplayPose);\n",
  "            replay.Build(new[] { Player.transform, Opponent.transform }, ball, camera, OnReplayPose);\n            " + (Y % ".88f") + "\n"),
 ("            presentation.Build(this, hud, umpire);\n", "            presentation.Build(this, hud, umpire);\n            " + (Y % ".94f") + "\n"),
 ("            BeginPoint(); UpdateCamera(true);\n            Initialized=true;\n", "            BeginPoint(); UpdateCamera(true);\n            InitProgress = 1;\n            Initialized=true;\n"),
 ("        void Update()\n        {\n            if (NetworkFrame()) return;\n", "        void Update()\n        {\n            if (!Initialized) return;   // a sliced build (SliceInit) is still in progress\n            if (NetworkFrame()) return;\n"),
]
for a, b in edits:
    if s.count(a) != 1: sys.exit(f"TennisGame.cs: cannot place slice ({a[:60]!r} found {s.count(a)}x)")
    s = s.replace(a, b)
if "using System.Collections;" not in s: s = "using System.Collections;\n" + s
open(p, "w").write(s); print("sliced")
