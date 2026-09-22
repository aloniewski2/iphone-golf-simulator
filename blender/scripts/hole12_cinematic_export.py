"""Export Codex's cinematic swing for the game: the camera, the ball, the trail's look, the thresholds.

    Blender -b <Hole_12_Cinematic.blend> --python blender/scripts/hole12_cinematic_export.py

Codex's third pass on Hole 12 (Documents/Codex/2026-09-20/i-j/outputs/Hole_12_Cinematic.blend, with
CINEMATIC_README.md and Swing_Trail_Config.json beside it) added, on top of the animated scene:

  * CAM_Cinematic_Flight — a close chase that banks around the ball in flight and orbits the
    landing, with the lens breathing between 38 and 52 mm and focus on the ball;
  * CAM_Course_Cinematic — a wide establishing drift over both islands and the bridge, 38 mm;
  * Trail_Green / Trail_Yellow / Trail_Red — one tapered, translucent tube following the ball
    (fat and bright at the ball, a hair at the tail, fading out over half a second after the
    landing), one per swing rating band: 80–100 green, 50–79 yellow, under 50 red.

The game plays the two cameras as the hole's intro (SignatureShot) and draws every shot's trail
to the tube's recipe (ShotEffects), so this writes both out of the scene itself:

  Unity/Assets/Resources/Course/hole_12_shot.json   ball + both cameras, every frame, scene metres
  Unity/Assets/Resources/Course/swing_trail.json    the trail's colours, taper and fade; the rating bands
"""
import bpy, json, os, sys
from mathutils import Vector

REPO = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
COURSE = os.path.join(REPO, "Unity", "Assets", "Resources", "Course")
SHOT_JSON = os.path.join(COURSE, "hole_12_shot.json")
TRAIL_JSON = os.path.join(COURSE, "swing_trail.json")
CONFIG = os.path.join(os.path.dirname(bpy.data.filepath), "Swing_Trail_Config.json")

sc = bpy.data.scenes.get("Hole 12 • Island Carry") or bpy.context.scene
bpy.context.window.scene = sc
O = bpy.data.objects
FPS, FRAMES = sc.render.fps, sc.frame_end
START = 24                      # the launch; the roll onto the tee before it is Blender-only
ball = O["Golf ball • animated"]
flight, wide = O["CAM_Cinematic_Flight"], O["CAM_Course_Cinematic"]
BALL_R = 0.45                   # Codex's presentation ball


def camera_track(cam):
    """Per frame: position, a point 20 m down the view, the up vector, the lens (mm)."""
    flat = []
    for f in range(1, FRAMES + 1):
        sc.frame_set(f)
        m = cam.matrix_world
        fwd = (m.to_3x3() @ Vector((0, 0, -1))).normalized()
        up = (m.to_3x3() @ Vector((0, 1, 0))).normalized()
        flat.extend(round(c, 4) for c in (*m.translation, *(m.translation + fwd * 20), *up))
        flat.append(round(cam.data.lens, 3))
    return flat


ball_flat = []
for f in range(1, FRAMES + 1):
    sc.frame_set(f)
    ball_flat.extend(round(c, 4) for c in ball.matrix_world.translation)
sc.frame_set(1)

# The reference empties the game solves the scene→course mapping from.
REFS = ("TEE_WHITE", "CUP", "MARKER_UP", "LANDING_SAFE")
ref_points = []
for name in REFS:
    o = O.get(name)
    p = o.matrix_world.translation if o else O["TEE_WHITE"].matrix_world.translation + Vector((0, 0, 50))
    ref_points.extend(round(c, 4) for c in p)

# Touchdowns, read off the ball itself: every frame it comes back to rest height after being up.
landings = []
airborne = False
for f in range(START, FRAMES + 1):
    z = ball_flat[(f - 1) * 3 + 2]
    if z > 21.62: airborne = True
    elif airborne: airborne = False; landings.append(f)

with open(SHOT_JSON, "w") as fh:
    json.dump({"fps": FPS, "frames": FRAMES, "start": START, "ballRadius": BALL_R,
               "sensorWidth": flight.data.sensor_width, "lens": flight.data.lens,
               "landings": landings, "rest": 204,
               "refNames": list(REFS), "refPoints": ref_points,
               "ball": ball_flat, "cam": camera_track(flight), "overview": camera_track(wide)}, fh, separators=(",", ":"))
print("SHOT", SHOT_JSON, os.path.getsize(SHOT_JSON) // 1024, "KB; landings at frames", landings)

# ---------------------------------------------------------------- the trail's recipe
# Trail_Green is a 20-ring tube: ring i sits (1 − i/19) × 10 frames behind the ball, radius
# 0.025 + 0.28·t^1.2 (t = 1 at the ball), and each band's material carries alpha 0.04 + 0.38·t².
# The three colours are the Principled base colours; the tube fades out over the 16 frames after
# the landing. Read the numbers off the meshes rather than trusting this comment.
def colour(name):
    m = O[name].data.materials[-1]     # the band at the ball
    return [round(c, 4) for c in m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value[:3]]


head = O["Trail_Green"].data.materials[-1].node_tree.nodes["Principled BSDF"].inputs["Alpha"].default_value
tail = O["Trail_Green"].data.materials[0].node_tree.nodes["Principled BSDF"].inputs["Alpha"].default_value
config = json.load(open(CONFIG)) if os.path.exists(CONFIG) else {}
bands = []
for band in config.get("trail_nodes", []):
    q = band["node"].split("_")[-1].lower()
    bands.append({"quality": q, "min": band.get("min_inclusive", 0),
                  "max": band.get("max_exclusive", band.get("max_inclusive", 100) + 1)})
with open(TRAIL_JSON, "w") as fh:
    json.dump({"source": "Hole_12_Cinematic.blend (Codex) via hole12_cinematic_export.py",
               "colors": {"green": colour("Trail_Green"), "yellow": colour("Trail_Yellow"), "red": colour("Trail_Red")},
               "headAlpha": round(head, 3), "tailAlpha": round(tail, 3),
               "headRadiusBalls": round(0.305 / BALL_R, 3), "tailRadiusBalls": round(0.025 / BALL_R, 3),
               "lengthSeconds": round(10 / FPS, 3), "fadeSeconds": round(16 / FPS, 3),
               "emission": 1.1, "roughness": 0.28,
               "ratingBands": bands or [{"quality": "red", "min": 0, "max": 50}, {"quality": "yellow", "min": 50, "max": 80}, {"quality": "green", "min": 80, "max": 101}],
               "defaultRating": config.get("default_rating", 90)}, fh, indent=1)
print("TRAIL", TRAIL_JSON, json.load(open(TRAIL_JSON))["colors"], "alpha", round(head, 3), "→", round(tail, 3))
