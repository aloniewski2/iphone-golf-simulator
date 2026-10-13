#!/usr/bin/env python3
"""Install the Plaza (PLAN_MenuHub_WalkableWorld) into a Unity project from a working clone.

  python3 work/menu-hub/install_unity.py <clone Unity dir> <target Unity dir>

New files (Hub scripts, scene, tests, with their .meta GUIDs) are copied as they are. Files other chats also edit are PATCHED in
place, never overwritten: NativeSportsSession.cs gets four small hooks, EditorBuildSettings.asset gets Hub.unity appended.
Idempotent: running it twice changes nothing the second time.
"""
import os, re, shutil, sys

src, dst = sys.argv[1], sys.argv[2]
NEW = ["Assets/Scripts/Hub", "Assets/Scripts/Hub.meta",
       "Assets/Scripts/Game/NativeSportsSession.Hub.cs", "Assets/Scripts/Game/NativeSportsSession.Hub.cs.meta",
       "Assets/Scenes/Hub.unity", "Assets/Scenes/Hub.unity.meta",
       "Assets/Tests/PlayMode/HubPlazaTests.cs", "Assets/Tests/PlayMode/HubPlazaTests.cs.meta",
       "Assets/Tests/EditMode/HubLayoutTests.cs", "Assets/Tests/EditMode/HubLayoutTests.cs.meta",
       "Assets/Tests/PlayMode/HubBayTests.cs", "Assets/Tests/PlayMode/HubBayTests.cs.meta",
       "Assets/Tests/PlayMode/HubPreviewCapture.cs", "Assets/Tests/PlayMode/HubPreviewCapture.cs.meta",
       "Assets/Tests/PlayMode/HubPartyTests.cs", "Assets/Tests/PlayMode/HubPartyTests.cs.meta",
       "Assets/Shaders/HubGlitchScreen.shader", "Assets/Shaders/HubGlitchScreen.shader.meta",
       "Assets/Resources/Hub", "Assets/Resources/Hub.meta"]
changed = []
for rel in NEW:
    a, b = os.path.join(src, rel), os.path.join(dst, rel)
    if os.path.isdir(a):
        for folder, _, files in os.walk(a):
            out = os.path.join(b, os.path.relpath(folder, a)); os.makedirs(out, exist_ok=True)
            for name in sorted(files):
                if name == ".DS_Store": continue
                fa, fb = os.path.join(folder, name), os.path.join(out, name)
                if not os.path.exists(fb) or open(fa, "rb").read() != open(fb, "rb").read():
                    shutil.copy2(fa, fb); changed.append(os.path.relpath(fb, dst))
    elif os.path.exists(a):
        if not os.path.exists(b) or open(a, "rb").read() != open(b, "rb").read():
            os.makedirs(os.path.dirname(b), exist_ok=True); shutil.copy2(a, b); changed.append(rel)

def patch(rel, edits):
    p = os.path.join(dst, rel); s = open(p).read(); o = s
    for marker, old, new in edits:
        if marker in s: continue
        if s.count(old) != 1: sys.exit(f"{rel}: cannot place hook ({old[:60]!r} found {s.count(old)}x) - patch by hand")
        s = s.replace(old, new)
    if s != o: open(p, "w").write(s); changed.append(rel + " (patched)")

patch("Assets/Scripts/Game/NativeSportsSession.cs", [
    ("sealed partial class NativeSportsSession", "public sealed class NativeSportsSession : MonoBehaviour", "public sealed partial class NativeSportsSession : MonoBehaviour"),
    ('if(m.sport=="hub") { StartHub(m); return; }',
     '                    if(loading || (Active && m.session==session)) return;\n                    if(m.sport!="golf" && m.sport!="tennis") return;\n                    StartCoroutine(Load(m)); return;',
     '                    if(m.sport=="hub") { StartHub(m); return; }\n                    if(loading || (Active && m.session==session)) return;\n                    if(m.sport!="golf" && m.sport!="tennis") return;\n                    LeaveHub(); StartCoroutine(Load(m)); return;'),
    ("if(HubIntercept(m)) return;", "                if(hubMode && HubCommand(m)) return;\n", "                if(HubIntercept(m)) return;\n"),
    ("if(HubIntercept(m)) return;", "                if(m.session!=session || !Active) return;\n", "                if(m.session!=session || !Active) return;\n                if(HubIntercept(m)) return;\n"),
    ("public partial class Message {", "[Serializable] public class Message {", "[Serializable] public partial class Message {"),
    ("var op=BeginSceneLoad(m);", 'var op=SceneManager.LoadSceneAsync(m.sport=="tennis"?"Tennis":"Golf");', "var op=BeginSceneLoad(m);"),
    ("SceneLoadTick(op);", "            while(!op.isDone) {\n                if(Time.realtimeSinceStartup>=nextProgress) {", "            while(!op.isDone) {\n                SceneLoadTick(op);\n                if(Time.realtimeSinceStartup>=nextProgress) {"),
    ("HandOff(m) : Present", '            yield return Present(m.external,"ready");\n            loading=false;\n        }', '            yield return behindPlaza ? HandOff(m) : Present(m.external,"ready");\n            loading=false;\n        }'),
    ("if(hubMode) { HubUpdate(); return; }", "            if(!Active || loading) return;\n", "            if(!Active || loading) return;\n            if(hubMode) { HubUpdate(); return; }\n"),
])
# the match builds over several frames when it loads behind the plaza, and the resort's plant batching stops re-copying meshes
import subprocess
here = os.path.dirname(os.path.abspath(__file__))
for script, rel in [("slice_tennis.py", "Assets/Scripts/Tennis/TennisGame.cs"), ("perf_tennis_art.py", "Assets/Scripts/Tennis/TennisVenueArt.cs"),
                    ("netlook_tennis.py", "Assets/Scripts/Tennis/TennisGame.Network.cs")]:
    out = subprocess.run([sys.executable, os.path.join(here, script), os.path.join(dst, rel)], capture_output=True, text=True)
    if out.returncode: sys.exit(out.stdout + out.stderr)
    if "already" not in out.stdout: changed.append(rel + " (patched)")
scene_guid = re.search(r"guid: (\w+)", open(os.path.join(src, "Assets/Scenes/Hub.unity.meta")).read()).group(1)
patch("ProjectSettings/EditorBuildSettings.asset", [
    ("Assets/Scenes/Hub.unity", "  m_configObjects: {}", f"  - enabled: 1\n    path: Assets/Scenes/Hub.unity\n    guid: {scene_guid}\n  m_configObjects: {{}}"),
])
print("\n".join(changed) if changed else "already installed")
