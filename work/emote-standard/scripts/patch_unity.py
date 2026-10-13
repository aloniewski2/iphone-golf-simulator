"""EMOTE STANDARD: the code side, applied to a Unity project root (an APFS clone first, the real project only after the films pass).
  python3 patch_unity.py <UnityRoot> [--check]
Every edit is an exact, asserted string replacement (fails loudly if the file moved on); originals are kept once in work/emote-standard/rollback/code/.
New files: Scripts/Game/GolferFootwork.cs, Editor/EmoteStandardWire.cs (copied from work/emote-standard/unity/)."""
import os
import shutil
import sys

W = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
root = sys.argv[1]
check = '--check' in sys.argv
RB = W + '/rollback/code'
os.makedirs(RB, exist_ok=True)

EDITS = {
    'Assets/Scripts/Tennis/HeroTennisDriver.cs': [
        ("EmoteScuba, EmoteThrust, EmoteSpike, IntroWave, IntroBringIt, IntroPushups }   // EMOTES: three taunts and three intros (match heroes only)",
         "EmoteScuba, EmoteThrust, EmoteSpike, IntroWave, IntroBringIt, IntroPushups,   // EMOTES: three taunts and three intros (match heroes only)\n"
         "                          EmoteJumpJoy, EmoteFistPump, EmoteStomp, EmoteSlump, EmoteShock, EmoteVictory, EmoteDefeat }   // EMOTE STANDARD: joy, excited, mad, sad, shock, big win, big loss"),
        ("        const int Count = 33;", "        const int Count = 40;"),
        ("        public static bool IsEmote(Clip c) => c >= Clip.EmoteScuba && c <= Clip.IntroPushups;",
         "        public static bool IsEmote(Clip c) => c >= Clip.EmoteScuba && c <= Clip.EmoteDefeat;\n"
         "        /// EMOTE STANDARD: the emotion set (also the match's own point / match reactions, see ResolvePerformanceClips).\n"
         "        public static readonly Clip[] Standard = { Clip.EmoteJumpJoy, Clip.EmoteFistPump, Clip.EmoteStomp, Clip.EmoteSlump, Clip.EmoteShock, Clip.EmoteVictory, Clip.EmoteDefeat };"),
        ("            if (game && Array.IndexOf(Taunts, c) >= 0) game.HoldNextPoint((length[(int)c] + .4f) * TennisGame.GameSpeed);      // the next serve waits for the taunt (the game's timers run at GameSpeed x real time)",
         "            if (game && (Array.IndexOf(Taunts, c) >= 0 || Array.IndexOf(Standard, c) >= 0)) game.HoldNextPoint((length[(int)c] + .4f) * TennisGame.GameSpeed);      // the next serve waits for the taunt / emote (the game's timers run at GameSpeed x real time)"),
        ("            slots = combined.ToArray();\n        }\n\n        /// Upper-body layer of a Generic rig",
         "            // EMOTE STANDARD: the point and match reactions are the emotion set's full-body clips when the hero has them\n"
         "            // (they replace the one-arm Performance gestures): won point = jump for joy / fist pump, lost point = slump (or stomp,\n"
         "            // see OnReacted), whiff = hands on head, match won = victory spin, match lost = drop to the knees.\n"
         "            foreach (var (to, from) in new[] { (Clip.CelebratePoint, Clip.EmoteJumpJoy), (Clip.HitPerfect, Clip.EmoteFistPump), (Clip.SadPointLost, Clip.EmoteSlump),\n"
         "                                               (Clip.MissWhiff, Clip.EmoteShock), (Clip.MatchWin, Clip.EmoteVictory), (Clip.MatchLose, Clip.EmoteDefeat) })\n"
         "            {\n"
         "                ClipSlot src = default; foreach (var s in combined) if (s.id == from && s.clip) { src = s; break; }\n"
         "                if (!src.clip) continue;\n"
         "                combined.RemoveAll(s => s.id == to);\n"
         "                combined.Add(new ClipSlot { id = to, clip = src.clip });\n"
         "            }\n"
         "            slots = combined.ToArray();\n        }\n\n        /// Upper-body layer of a Generic rig"),
        ("                : won ? (emoteRoll % 2 == 0 ? Clip.CelebratePoint : Clip.HitPerfect) : Clip.SadPointLost;",
         "                : won ? (emoteRoll % 2 == 0 ? Clip.CelebratePoint : Clip.HitPerfect) : (emoteRoll % 2 == 0 || !valid[(int)Clip.EmoteStomp] ? Clip.SadPointLost : Clip.EmoteStomp);"),
    ],
    'Assets/Scripts/Tennis/TennisEmotes.cs': [
        ('        public static readonly string[] IDs = { "scuba", "thrust", "spike", "wave", "bringIt", "pushups" };',
         '        public static readonly string[] IDs = { "scuba", "thrust", "spike", "wave", "bringIt", "pushups", "jumpJoy", "fistPump", "stomp", "slump", "shock", "victory", "defeat" };'),
        ("            HeroTennisDriver.Clip.IntroWave, HeroTennisDriver.Clip.IntroBringIt, HeroTennisDriver.Clip.IntroPushups\n        };",
         "            HeroTennisDriver.Clip.IntroWave, HeroTennisDriver.Clip.IntroBringIt, HeroTennisDriver.Clip.IntroPushups,\n"
         "            HeroTennisDriver.Clip.EmoteJumpJoy, HeroTennisDriver.Clip.EmoteFistPump, HeroTennisDriver.Clip.EmoteStomp, HeroTennisDriver.Clip.EmoteSlump,\n"
         "            HeroTennisDriver.Clip.EmoteShock, HeroTennisDriver.Clip.EmoteVictory, HeroTennisDriver.Clip.EmoteDefeat\n        };"),
        ('        static readonly string[] ExportNames = { "Emote_Scuba", "Emote_Thrust", "Emote_Spike", "Intro_Wave", "Intro_BringIt", "Intro_Pushups" };',
         '        static readonly string[] ExportNames = { "Emote_Scuba", "Emote_Thrust", "Emote_Spike", "Intro_Wave", "Intro_BringIt", "Intro_Pushups",\n'
         '            "Emote_JumpJoy", "Emote_FistPump", "Emote_Stomp", "Emote_Slump", "Emote_Shock", "Emote_Victory", "Emote_Defeat" };'),
        ('        static readonly string[] Names = { "Scuba", "Thrust", "Spike", "Wave", "Bring It", "Push-ups" };',
         '        static readonly string[] Names = { "Scuba", "Thrust", "Spike", "Wave", "Bring It", "Push-ups", "Jump for Joy", "Fist Pump", "Stomp", "Slump", "Shock", "Victory", "Defeat" };'),
        ('        static readonly float[] Durations = { 5.2f, 4f, 3.7f, 2.5f, 3.5f, 3.5f };',
         '        static readonly float[] Durations = { 5.2f, 4f, 3.7f, 2.5f, 3.5f, 3.5f, 2.1f, 1.6f, 2.2f, 2.6f, 2.2f, 3.2f, 3.3f };'),
    ],
    'Assets/Scripts/Tennis/MatchHeroFacePerformance.cs': [
        ("        public Vector2 ForcedGaze;",
         "        public Vector2 ForcedGaze;\n"
         "        /// A mood asked for by a figure that has no tennis driver (the golfer playing an emote): Happy, Sad, Angry or Shock.\n"
         "        public enum Mood { None, Happy, Sad, Angry, Shock }\n"
         "        public Mood ForcedMood;"),
        ("            bool happy = c == HeroTennisDriver.Clip.CelebratePoint || c == HeroTennisDriver.Clip.MatchWin || c == HeroTennisDriver.Clip.HitPerfect;\n"
         "            bool sad = c == HeroTennisDriver.Clip.SadPointLost || c == HeroTennisDriver.Clip.MatchLose;\n"
         "            bool effort = c == HeroTennisDriver.Clip.Serve || c == HeroTennisDriver.Clip.Smash;\n"
         "            blink = Mathf.MoveTowards(blink, happy ? .16f : effort ? .12f : 0, dt * 3);\n"
         "            brow = Mathf.MoveTowards(brow, happy ? .00065f : sad ? -.0006f : effort ? -.0004f : 0, dt * .006f);",
         "            bool happy = ForcedMood == Mood.Happy || c == HeroTennisDriver.Clip.CelebratePoint || c == HeroTennisDriver.Clip.MatchWin || c == HeroTennisDriver.Clip.HitPerfect\n"
         "                         || c == HeroTennisDriver.Clip.EmoteJumpJoy || c == HeroTennisDriver.Clip.EmoteFistPump || c == HeroTennisDriver.Clip.EmoteVictory;\n"
         "            bool sad = ForcedMood == Mood.Sad || c == HeroTennisDriver.Clip.SadPointLost || c == HeroTennisDriver.Clip.MatchLose || c == HeroTennisDriver.Clip.EmoteSlump || c == HeroTennisDriver.Clip.EmoteDefeat;\n"
         "            bool angry = ForcedMood == Mood.Angry || c == HeroTennisDriver.Clip.EmoteStomp;\n"
         "            bool shock = ForcedMood == Mood.Shock || c == HeroTennisDriver.Clip.EmoteShock || c == HeroTennisDriver.Clip.MissWhiff;\n"
         "            bool effort = c == HeroTennisDriver.Clip.Serve || c == HeroTennisDriver.Clip.Smash;\n"
         "            // EMOTE STANDARD moods: angry = a hard squint under a low brow, shock = eyes wide open under a high one\n"
         "            blink = Mathf.MoveTowards(blink, happy ? .16f : angry ? .24f : effort ? .12f : sad ? .1f : 0, dt * 3);\n"
         "            brow = Mathf.MoveTowards(brow, happy ? .00065f : shock ? .0011f : angry ? -.0009f : sad ? -.0006f : effort ? -.0004f : 0, dt * .006f);"),
    ],
    'Assets/Scripts/Hub/HubHeroAnimator.cs': [
        ("            a.Build(idleClip, walkClip, runClip);\n            return a;",
         "            // EMOTE STANDARD: the plaza walks / runs on the standard empty-handed locomotion cycles and plays the empty-handed\n"
         "            // versions of the emotion set (the plaza hero carries no racket); Resources/HeroMoves/<Sex>/ (Walk, Run, Emote_*).\n"
         "            foreach (var c in Resources.LoadAll<AnimationClip>(\"HeroMoves/\" + (look.female ? \"Female\" : \"Male\")))\n"
         "            {\n"
         "                if (c.name.StartsWith(\"__preview__\")) continue;\n"
         "                if (c.name == \"Walk\") { walkClip = c; a.walkClipSpeed = StandardWalkSpeed; }\n"
         "                else if (c.name == \"Run\") { runClip = c; a.runClipSpeed = StandardRunSpeed; }\n"
         "                else if (c.name.StartsWith(\"Emote_\")) a.clips[c.name.Replace(\"_\", \"\")] = c;\n"
         "            }\n"
         "            a.Build(idleClip, walkClip, runClip);\n            return a;"),
        ("        public float walkClipSpeed = 1.25f, runClipSpeed = 3.25f;",
         "        public float walkClipSpeed = 1.25f, runClipSpeed = 3.25f;\n"
         "        /// Planted-foot speeds of the standard Walk / Run cycles at 1x (work/emote-standard/scripts/gait.py: 2 x 0.64 m per 1.067 s, 2 x 1.167 m per 0.667 s).\n"
         "        public const float StandardWalkSpeed = 1.2f, StandardRunSpeed = 3.5f;"),
    ],
    'Assets/Scripts/Game/GolferView.cs': [
        ("        HeroGolfer heroFigure;",
         "        HeroGolfer heroFigure;\n"
         "        /// Aim footwork: the shoes stay planted and shuffle round the ball as the aim turns (GolferFootwork).\n"
         "        GolferFootwork footwork;\n"
         "        public GolferFootwork Footwork => footwork;"),
        ("            heroFigure?.Dispose(); heroFigure = null;", "            heroFigure?.Dispose(); heroFigure = null; footwork = null;"),
        ("            foreach (var c in Resources.LoadAll<AnimationClip>(path))\n                if (!c.name.StartsWith(\"__preview__\")) clips[c.name] = c;\n            if (clips.Count == 0) { Debug.LogWarning($\"{path} has no swing clips\"); return false; }",
         "            foreach (var c in Resources.LoadAll<AnimationClip>(path))\n                if (!c.name.StartsWith(\"__preview__\")) clips[c.name] = c;\n            if (clips.Count == 0) { Debug.LogWarning($\"{path} has no swing clips\"); return false; }\n"
         "            // EMOTE STANDARD: the empty-handed emotion set and the standard Walk / Run (they replace the borrowed tennis ones)\n"
         "            foreach (var c in Resources.LoadAll<AnimationClip>(\"HeroMoves/\" + (look.Hero.Value.Female ? \"Female\" : \"Male\")))\n"
         "                if (!c.name.StartsWith(\"__preview__\")) clips[c.name] = c;"),
        ("            Head = heroFigure.Bones.TryGetValue(\"Head\", out var head) ? head : null;",
         "            Head = heroFigure.Bones.TryGetValue(\"Head\", out var head) ? head : null;\n"
         "            footwork = spectator ? null : GolferFootwork.Build(model.transform, heroFigure.Bones);"),
        ("        void Update()\n        {\n            if (hasModel) UpdateModel(); else UpdateFigure();\n        }",
         "        void Update()\n        {\n            if (hasModel) UpdateModel(); else UpdateFigure();\n        }\n\n"
         "        /// After every Update (GolfGame turns the stance round the ball in its own): the shoes, at address only.\n"
         "        void LateUpdate()\n        {\n"
         "            if (footwork != null && hasModel) footwork.Tick(phase == 0 && time <= Mathf.Max(.02f, TopTime * .04f), Time.deltaTime);\n"
         "        }\n\n"
         "        /// The face that goes with a move (the golfer has no tennis driver to read it from).\n"
         "        static GolfArcade.Tennis.MatchHeroFacePerformance.Mood MoodOf(string move) => move switch\n"
         "        {\n"
         "            \"Emote_JumpJoy\" or \"Emote_FistPump\" or \"Emote_Victory\" or \"Cheer\" or \"FistPump\" => GolfArcade.Tennis.MatchHeroFacePerformance.Mood.Happy,\n"
         "            \"Emote_Slump\" or \"Emote_Defeat\" => GolfArcade.Tennis.MatchHeroFacePerformance.Mood.Sad,\n"
         "            \"Emote_Stomp\" => GolfArcade.Tennis.MatchHeroFacePerformance.Mood.Angry,\n"
         "            \"Emote_Shock\" => GolfArcade.Tennis.MatchHeroFacePerformance.Mood.Shock,\n"
         "            _ => GolfArcade.Tennis.MatchHeroFacePerformance.Mood.None,\n"
         "        };\n"
         "        void SetMood(string move)\n        {\n"
         "            var face = heroFigure != null ? heroFigure.Root.GetComponentInChildren<GolfArcade.Tennis.MatchHeroFacePerformance>() : null;\n"
         "            if (face) face.ForcedMood = MoodOf(move);\n        }"),
        ("            clipName = move;\n            foreach (var kv in clubMeshes) kv.Value.SetActive(false);",
         "            clipName = move; SetMood(move);\n            foreach (var kv in clubMeshes) kv.Value.SetActive(false);"),
    ],
    'Assets/Scripts/Game/GolfGame.HoleOut.cs': [
        ("            int toPar = holeStrokes - hole.Par;\n            string move = holeStrokes == 1 || toPar <= -2 ? \"Cheer\" : toPar == -1 ? \"FistPump\" : toPar == 0 ? \"Wave\" : \"Idle\";\n            return golfer.HasMove(move) ? move : \"Idle\";",
         "            int toPar = holeStrokes - hole.Par;\n"
         "            // EMOTE STANDARD: an ace = the victory spin, eagle = jump for joy, birdie = fist pump, par = wave, bogey = slump, worse = an angry stomp\n"
         "            string move = holeStrokes == 1 ? \"Emote_Victory\" : toPar <= -2 ? \"Emote_JumpJoy\" : toPar == -1 ? \"Emote_FistPump\" : toPar == 0 ? \"Wave\" : toPar == 1 ? \"Emote_Slump\" : \"Emote_Stomp\";\n"
         "            if (!golfer.HasMove(move)) move = holeStrokes == 1 || toPar <= -2 ? \"Cheer\" : toPar == -1 ? \"FistPump\" : toPar == 0 ? \"Wave\" : \"Idle\";\n"
         "            return golfer.HasMove(move) ? move : \"Idle\";"),
    ],
}
EDITS['Assets/Editor/MatchHeroLockerExport.cs'] = [
    ('            (HeroTennisDriver.Clip.MatchLose, "matchLose", false)\n        };',
     '            (HeroTennisDriver.Clip.MatchLose, "matchLose", false),\n'
     '            // EMOTE STANDARD: the emotion set (ids = TennisEmotes.IDs / EmoteCatalog.ids)\n'
     '            (HeroTennisDriver.Clip.EmoteJumpJoy, "jumpJoy", false), (HeroTennisDriver.Clip.EmoteFistPump, "fistPump", false),\n'
     '            (HeroTennisDriver.Clip.EmoteStomp, "stomp", false), (HeroTennisDriver.Clip.EmoteSlump, "slump", false),\n'
     '            (HeroTennisDriver.Clip.EmoteShock, "shock", false), (HeroTennisDriver.Clip.EmoteVictory, "victory", false),\n'
     '            (HeroTennisDriver.Clip.EmoteDefeat, "defeat", false)\n        };'),
]
NEW = {'Assets/Scripts/Game/GolferFootwork.cs': W + '/unity/Scripts/Game/GolferFootwork.cs',
       'Assets/Editor/EmoteStandardWire.cs': W + '/unity/Editor/EmoteStandardWire.cs'}

ok = True
for rel, edits in EDITS.items():
    p = os.path.join(root, rel)
    s = open(p).read()
    keep = os.path.join(RB, rel.replace('/', '__'))
    if not os.path.exists(keep) and not check:
        shutil.copy(p, keep)
    for a, b in edits:
        if b in s:
            continue                       # already applied
        if a not in s:
            print('MISSING in', rel, ':', a[:90].replace('\n', ' | '))
            ok = False
            continue
        if s.count(a) != 1:
            print('AMBIGUOUS in', rel, ':', a[:90])
            ok = False
            continue
        s = s.replace(a, b)
    if not check:
        open(p, 'w').write(s)
for rel, src in NEW.items():
    if not check:
        shutil.copy(src, os.path.join(root, rel))
print('PATCH', 'OK' if ok else 'FAILED', root, '(check only)' if check else '')
sys.exit(0 if ok else 1)
