"""Finish the Codex "animated Hole 12" hand-off: the shot the camera follows, the water, the exports.

    Blender -b <Hole_12_Animated.blend> --python blender/scripts/hole12_animate_finish.py -- [options]

    --out <dir>     where the deliverables go (default: the .blend's own folder)
    --sheet         render a quick EEVEE contact sheet of the shot instead of the full set
    --no-video      skip the 8-second preview video       --no-stills   skip the Cycles stills
    --no-glb        skip the animated GLB export

Codex's second session (chatgpt.com/s/cx_6ab19270a8708191ab1a6fcc490ba737) took Hole_12_Island_Carry
.blend and asked for animated water, cleaner terrain and a ball that rolls, flies, lands and bounces.
It got as far as the mesh work — one solid tee-and-approach landmass, real bunker bowls cut into the
green, bevelled cliff edges, a wave grid with four morph targets — and a first pass at the ball and
its camera, then stopped mid-render: the preview video was never made, the GLB and the stills still
show the tee island missing (they predate the terrain repair), and the camera lost the ball for most
of the flight. This script is the repeatable last mile, run on the saved Hole_12_Animated.blend:

  * drops the golfer preview scene that came along from golfer.blend;
  * re-lays the water: the same four travelling-wave morph targets, but calm within a few metres of
    every shore (the foam rings and glints sit on still water) and low enough that the glints never
    dip under a crest;
  * re-bakes the ball on Codex's designed path (roll to the tee, launch, carry, three diminishing
    bounces, roll to rest a metre short of the cup) with an honest spin: backspin in the air, and on
    the ground a roll of exactly distance / radius so the stripe reads;
  * gives the world a sky gradient that only camera rays see (the lighting is unchanged);
  * rebuilds CAM_Ball_Action as three damped shots — behind the ball on the tee, a chase up the
    carry, and from touchdown a hole cam behind the cup for the bounces and the roll — baked every
    frame so the GLB carries the move;
  * saves the .blend, re-exports Exports/Hole_12_Animated.glb as one combined clip (ball, camera,
    wave weights, glints), renders the preview MP4 and the two Cycles stills, and rewrites
    ANIMATION_README.md and Ball_Animation.json to match;
  * writes the shot for the game — Unity/Assets/Resources/Course/hole_12_shot.json: the ball and
    the camera every frame in scene metres, plus the reference empties the game solves the
    scene→course mapping from (Game/SignatureShot.cs plays it as the hole's intro).
"""
import bpy, json, math, os, struct, sys
from mathutils import Vector

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []


def option(flag, default=None):
    if flag in ARGS:
        i = ARGS.index(flag)
        return ARGS[i + 1] if default is not None and i + 1 < len(ARGS) else True
    return default if default is not None else False


OUT = option("--out", os.path.dirname(bpy.data.filepath) or os.getcwd())
REPO = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
SHOT_JSON = os.path.join(REPO, "Unity", "Assets", "Resources", "Course", "hole_12_shot.json")
SHEET = option("--sheet")
FPS, FRAMES = 30, 240
BALL_R = 0.45        # Codex's presentation scale — a real ball would be a pixel at course scale
LENS = 34.0

sc = bpy.data.scenes.get("Hole 12 • Island Carry") or bpy.context.scene
bpy.context.window.scene = sc
O = bpy.data.objects


def obj(name):
    o = O.get(name)
    if o is None: raise KeyError(f"scene has no object {name!r}")
    return o


# ------------------------------------------------------------------ 1. leftovers
# The golfer studio's preview scene rides along in every file descended from golfer.blend.
for other in [s for s in bpy.data.scenes if s is not sc]:
    for o in list(other.objects): bpy.data.objects.remove(o, do_unlink=True)
    bpy.data.scenes.remove(other)
for a in [a for a in bpy.data.actions if a.users == 0 or a.name == "Swing"]:
    bpy.data.actions.remove(a)
sc.frame_start, sc.frame_end, sc.render.fps = 1, FRAMES, FPS

# ------------------------------------------------------------------ 1b. a sky worth pointing a camera at
# The world is a flat dim blue-grey that lights the scene well but reads as overcast wherever the
# camera sees it (the chase looks up a lot). Camera rays get a gradient — pale at the horizon,
# deeper overhead — and every other ray keeps the old colour, so the lighting does not change.
world = sc.world
if world and world.use_nodes and not world.node_tree.nodes.get("Sky gradient"):
    nt = world.node_tree
    bg = next(n for n in nt.nodes if n.type == 'BACKGROUND')
    out = next(n for n in nt.nodes if n.type == 'OUTPUT_WORLD')
    coord = nt.nodes.new("ShaderNodeTexCoord")
    xyz = nt.nodes.new("ShaderNodeSeparateXYZ"); nt.links.new(coord.outputs["Generated"], xyz.inputs["Vector"])
    ramp = nt.nodes.new("ShaderNodeMapRange"); ramp.name = "Sky gradient"
    ramp.inputs["From Min"].default_value, ramp.inputs["From Max"].default_value = -0.02, 0.45
    nt.links.new(xyz.outputs["Z"], ramp.inputs["Value"])
    mix = nt.nodes.new("ShaderNodeMix"); mix.data_type = 'RGBA'
    socket = {i.identifier: i for i in mix.inputs}
    socket["A_Color"].default_value = (0.42, 0.66, 0.86, 1)     # horizon haze (linear; AgX lifts it)
    socket["B_Color"].default_value = (0.06, 0.24, 0.66, 1)     # overhead
    nt.links.new(ramp.outputs["Result"], mix.inputs["Factor"])
    sky = nt.nodes.new("ShaderNodeBackground"); sky.inputs["Strength"].default_value = 1.0
    nt.links.new(mix.outputs["Result"], sky.inputs["Color"])
    path = nt.nodes.new("ShaderNodeLightPath")
    choose = nt.nodes.new("ShaderNodeMixShader")
    nt.links.new(path.outputs["Is Camera Ray"], choose.inputs["Fac"])
    nt.links.new(bg.outputs["Background"], choose.inputs[1])
    nt.links.new(sky.outputs["Background"], choose.inputs[2])
    nt.links.new(choose.outputs["Shader"], out.inputs["Surface"])

# ------------------------------------------------------------------ 2. what is land
# The ground the ball and the water have to agree with: the closed terrain solids Codex built.
LAND = [obj("Terrain • continuous tee and approach"), obj("Green island • cliff"), obj("Eastern promontory • cliff")]
dg = bpy.context.evaluated_depsgraph_get()


def ground_at(x, y, objects=None):
    """World z of the highest render-visible mesh under (x, y), or None over open water."""
    best = None
    for o in objects or [o for o in sc.objects if o.type == 'MESH' and not o.hide_render
                         and o.name not in ("Water • looping waves", "Ocean", "Water • moving glints")]:
        oe = o.evaluated_get(dg)
        inv = oe.matrix_world.inverted()
        hit, loc, _, _ = oe.ray_cast(inv @ Vector((x, y, 500)), (inv.to_3x3() @ Vector((0, 0, -1))).normalized())
        if hit:
            z = (oe.matrix_world @ loc).z
            if best is None or z > best: best = z
    return best


# ------------------------------------------------------------------ 3. the water
# Codex's grid: 64 × 80 quads over the course, four morph targets a quarter wave apart, cross-faded
# one after another so the swell travels. Re-laid here with a shoreline: any vertex under land or
# within CALM metres of it stays flat, easing to full swell by SWELL metres out, so the foam rings
# (z 0.16, bevel 0.22) and the glint cards (z 0.30) ride still water and never get swallowed.
waves = obj("Water • looping waves")
CALM, SWELL = 6.0, 22.0
NX, NY = 64, 80
cell = Vector((360 / NX, 460 / NY))
land = [[ground_at(-180 + cell.x * i, -120 + cell.y * j, LAND) is not None for i in range(NX + 1)] for j in range(NY + 1)]
# distance to land in cells, by dilation (enough passes to cover SWELL)
dist = [[0 if land[j][i] else 99 for i in range(NX + 1)] for j in range(NY + 1)]
for _ in range(int(SWELL / min(cell) + 2)):
    for j in range(NY + 1):
        for i in range(NX + 1):
            if dist[j][i] == 0: continue
            best = dist[j][i]
            for dj, di in ((0, 1), (0, -1), (1, 0), (-1, 0)):
                jj, ii = j + dj, i + di
                if 0 <= jj <= NY and 0 <= ii <= NX: best = min(best, dist[jj][ii] + 1)
            dist[j][i] = best
cell_m = min(cell)
keys = waves.data.shape_keys.key_blocks
for n, key in enumerate(k for k in keys if k.name.startswith("Wave phase")):
    phase = n * math.pi / 2
    for idx, v in enumerate(key.data):
        j, i = divmod(idx, NX + 1)
        x, y = v.co.x, v.co.y
        edge = min(1.0, max(0.0, min(x + 180, 180 - x, y + 120, 340 - y) / 22))
        shore = min(1.0, max(0.0, (dist[j][i] * cell_m - CALM) / (SWELL - CALM)))
        v.co.z = edge * shore * (0.16 * math.sin(x * 0.13 + y * 0.17 + phase) + 0.06 * math.sin(x * 0.31 - y * 0.09 + phase))
# the same cross-fade Codex keyed, restated so the clip loops exactly at 240 frames
for n, key in enumerate(k for k in keys if k.name.startswith("Wave phase")):
    for f in (1, 61, 121, 181, 241):
        key.value = 1.0 if ((f - 1) // 60) % 4 == n else 0.0
        key.keyframe_insert("value", frame=f)

# ------------------------------------------------------------------ 4. the ball
# Codex's path, kept: frames 1–24 roll to the tee, 24–94 the carry, three bounces 94–112–126–136,
# then a decelerating roll to rest at (0, 179.9), a metre short of the cup at (0, 181). Metres,
# scene units. One change: the carry's apex comes down from 53 m (a 53° launch) to a lofted iron's
# 34 m, so the flight reads as a golf shot and a camera can stay with it.
TEE_Z, GREEN_Z = 15.56, 21.60           # ball centres: tee deck 15.1 + r, green 21.14 + r
APEX = 34.0


def ball_at(f):
    if f <= 24:
        t = (f - 1) / 23; return Vector((0, -5 + 5 * t, TEE_Z))
    if f <= 94:
        t = (f - 24) / 70; return Vector((4 * t, 158 * t, TEE_Z + (GREEN_Z - TEE_Z) * t + 4 * APEX * t * (1 - t)))
    if f <= 112:
        t = (f - 94) / 18; return Vector((4 - 0.8 * t, 158 + 7 * t, GREEN_Z + 4 * 4.2 * t * (1 - t)))
    if f <= 126:
        t = (f - 112) / 14; return Vector((3.2 - 0.6 * t, 165 + 4 * t, GREEN_Z + 4 * 1.65 * t * (1 - t)))
    if f <= 136:
        t = (f - 126) / 10; return Vector((2.6 - 0.3 * t, 169 + 2 * t, GREEN_Z + 4 * 0.52 * t * (1 - t)))
    if f <= 204:
        t = (f - 136) / 68; q = 1 - (1 - t) ** 2; return Vector((2.3 * (1 - q), 171 + 8.9 * q, GREEN_Z))
    return Vector((0, 179.9, GREEN_Z))


def airborne(f): return 24 < f < 136 and ball_at(f).z > (TEE_Z if f < 60 else GREEN_Z) + 0.02


ball = obj("Golf ball • animated")
ball.animation_data_clear()
ball.rotation_mode = 'XYZ'
BACKSPIN = 0.10      # rad/frame in the air — a slow, readable spin, not the 50 rev/s of a real drive
roll = 0.0
last = ball_at(1)
events = []
for f in range(1, FRAMES + 1):
    p = ball_at(f)
    step = (p - last).xy.length
    roll += BACKSPIN if airborne(f) else -step / BALL_R    # forward roll is negative about +X
    ball.location = p
    ball.rotation_euler = (roll, 0, 0.15)
    ball.keyframe_insert("location", frame=f)
    ball.keyframe_insert("rotation_euler", frame=f)
    last = p
    if f in (1, 24, 94, 103, 112, 119, 126, 136, 170, 204): events.append({"frame": f, "position": [round(c, 3) for c in p]})

# ------------------------------------------------------------------ 5. the camera
# Three shots, the way a broadcast covers a par 3: a tee camera low behind the ball, a tracer that
# chases the carry from behind and under the arc, then a cut at touchdown to a hole camera behind
# the cup that watches the bounces come at it and the roll run up to the hole. Inside a shot the
# camera is damped — it glides to where it wants to be rather than snapping — and it is baked every
# frame, so the GLB carries the whole move.
cam = obj("CAM_Ball_Action")
cam.animation_data_clear()
cam.constraints.clear()
cam.data.lens, cam.data.clip_end = LENS, 3000
CUP = Vector((0, 181, 21.14))
CUT = 95


def wants(f):
    """(camera position, look-at point, position time constant s, look time constant s)."""
    p = ball_at(f)
    if f <= 24:                       # on the tee: low, behind and to the right, the hole ahead
        return p + Vector((4.5, -9, 2.4)), p + Vector((0, 6, 0.4)), 0.6, 0.4
    if f < CUT:                       # the chase: behind and under the arc, the lead easing off
        t = (f - 24) / 70
        v = (ball_at(f + 1) - ball_at(f - 1)).xy.normalized()
        d = Vector((v.x, v.y, 0))
        back = 12 + 6 * math.sin(math.pi * t)
        up = 3.5 + 0.2 * (p.z - TEE_Z)
        return p - d * back + Vector((0, 0, up)), p + d * 9 * (1 - t), 0.30, 0.12
    u = (f - CUT) / (FRAMES - CUT)    # the hole cam: behind and left of the cup, a slow push in
    pos = Vector((-9.5, 190.5, 23.8)).lerp(Vector((-6.5, 187.5, 23.0)), u)
    focus = p.lerp(CUP, 0.4)
    focus.z = GREEN_Z + 0.3 * (p.z - GREEN_Z)
    return pos, focus, 3.0, 0.35


dt = 1 / FPS
pos, look, _, _ = wants(1)
pos, look = pos.copy(), look.copy()
for f in range(1, FRAMES + 1):
    tp, tl, tau_p, tau_l = wants(f)
    if f == CUT: pos, look = tp.copy(), tl.copy()           # the cut
    pos += (tp - pos) * (1 - math.exp(-dt / tau_p))
    look += (tl - look) * (1 - math.exp(-dt / tau_l))
    cam.location = pos
    cam.rotation_euler = (look - pos).to_track_quat('-Z', 'Y').to_euler()
    cam.keyframe_insert("location", frame=f)
    cam.keyframe_insert("rotation_euler", frame=f)


def fcurves(action):
    found = []
    for layer in action.layers:
        for strip in layer.strips:
            for slot in action.slots:
                bag = strip.channelbag(slot)
                if bag: found.extend(bag.fcurves)
    return found


for o in (ball, cam):
    for fc in fcurves(o.animation_data.action):
        for k in fc.keyframe_points: k.interpolation = 'LINEAR'
# Euler rotation baked per frame can flip by 2π between keys; keep the curves continuous.
for fc in fcurves(cam.animation_data.action):
    if fc.data_path == "rotation_euler":
        prev = None
        for k in fc.keyframe_points:
            if prev is not None:
                while k.co.y - prev > math.pi: k.co.y -= 2 * math.pi
                while k.co.y - prev < -math.pi: k.co.y += 2 * math.pi
            prev = k.co.y

sc.timeline_markers.clear()
for name, f in (("TEE ROLL", 1), ("LAUNCH", 24), ("LANDING", 94), ("BOUNCE 1", 103), ("BOUNCE 2", 119), ("ROLL OUT", 136), ("AT REST", 204)):
    sc.timeline_markers.new(name, frame=f)
sc.camera = cam
sc.frame_set(103)

# ------------------------------------------------------------------ 6. sanity: the ball never sinks
worst = 0.0
for f in range(1, FRAMES + 1):
    p = ball_at(f)
    g = ground_at(p.x, p.y, LAND + [O[n] for n in ("Putting green", "Putting fringe", "Approach fairway", "Approach fringe", "Tee deck", "Tee fringe")])
    if g is not None: worst = max(worst, g + BALL_R - p.z)
    if f in (1, 94, 136, 204): print(f"BALL f{f} {tuple(round(c, 2) for c in p)} ground {None if g is None else round(g, 2)}")
assert worst < 0.05, f"ball sinks {worst:.2f} m into the ground"
print("BALL clearance ok (max sink %.3f m)" % worst)

# ------------------------------------------------------------------ 7. save, then the deliverables
sc.render.resolution_x, sc.render.resolution_y, sc.render.resolution_percentage = 1280, 720, 100
sc.render.engine = 'CYCLES'; sc.cycles.samples = 16
bpy.ops.wm.save_as_mainfile(filepath=bpy.data.filepath or os.path.join(OUT, "Hole_12_Animated.blend"), check_existing=False, compress=True)
print("SAVED", bpy.data.filepath)

with open(os.path.join(OUT, "Ball_Animation.json"), "w") as fh:
    json.dump({"fps": FPS, "frames": FRAMES, "ball_radius_metres": BALL_R,
               "style": "Exaggerated ball scale for animation visibility; a designed shot, not a simulation",
               "camera": "CAM_Ball_Action, baked every frame", "key_events": events}, fh, indent=2)

# The same shot for the game, flat arrays (Unity's JsonUtility reads nothing nested): ball
# centres and, for the camera, its position and a point 20 m down its view, per frame. The
# game maps scene metres onto its course through the reference empties listed here, whose
# world positions it knows once the model is placed.
REFS = ("TEE_WHITE", "CUP", "MARKER_UP", "LANDING_SAFE")
ref_points = {}
for name in REFS:
    o = O.get(name)
    if o is None and name == "MARKER_UP":       # the prep script adds it 50 m over the tee
        ref_points[name] = tuple(O["TEE_WHITE"].location + Vector((0, 0, 50)))
    else:
        ref_points[name] = tuple(o.matrix_world.translation)
ball_flat, cam_flat = [], []
for f in range(1, FRAMES + 1):
    sc.frame_set(f)
    ball_flat.extend(round(c, 4) for c in ball_at(f))
    m = cam.matrix_world
    look = m.translation + (m.to_3x3() @ Vector((0, 0, -1))).normalized() * 20
    cam_flat.extend(round(c, 4) for c in (*m.translation, *look))
sc.frame_set(1)
with open(SHOT_JSON, "w") as fh:
    json.dump({"fps": FPS, "frames": FRAMES, "start": 24, "ballRadius": BALL_R, "sensorWidth": cam.data.sensor_width, "lens": LENS,
               "landings": [94, 112, 126, 136], "rest": 204,
               "refNames": list(REFS), "refPoints": [round(c, 4) for n in REFS for c in ref_points[n]],
               "ball": ball_flat, "cam": cam_flat}, fh, separators=(",", ":"))
print("SHOT", SHOT_JSON, os.path.getsize(SHOT_JSON) // 1024, "KB")


def eevee(samples=12):
    sc.render.engine = 'BLENDER_EEVEE'
    sc.eevee.taa_render_samples = samples


if SHEET:
    # a quick look at the shot: 8 frames tiled by the caller
    eevee(8)
    sc.render.resolution_x, sc.render.resolution_y = 640, 360
    sc.render.image_settings.media_type = 'IMAGE'; sc.render.image_settings.file_format = 'PNG'
    for f in (1, 30, 55, 94, 96, 103, 119, 136, 170, 204, 220, 240):
        sc.frame_set(f)
        sc.render.filepath = os.path.join(OUT, "sheet_%03d.png" % f)
        bpy.ops.render.render(write_still=True)
    sys.exit(0)

# the GLB wants real meshes: curves (foam rings, railings, waterfall) become meshes — after the save
bpy.ops.object.select_all(action='DESELECT')
curves = [o for o in sc.objects if o.type == 'CURVE' and not o.hide_render]
for o in curves: o.select_set(True)
if curves:
    bpy.context.view_layer.objects.active = curves[0]
    bpy.ops.object.convert(target='MESH')

if not option("--no-glb"):
    os.makedirs(os.path.join(OUT, "Exports"), exist_ok=True)
    glb = os.path.join(OUT, "Exports", "Hole_12_Animated.glb")
    bpy.ops.object.select_all(action='DESELECT')
    for o in sc.objects:
        if o.type in ('MESH', 'CAMERA') and not o.hide_render:
            o.hide_set(False); o.select_set(True)
    for o in bpy.data.collections["Gameplay_Refs"].objects: o.select_set(True)
    sc.frame_set(1)
    bpy.ops.export_scene.gltf(filepath=glb, export_format='GLB', use_selection=True, export_extras=True,
                              export_cameras=True, export_animations=True, export_animation_mode='SCENE',
                              export_frame_range=True, export_force_sampling=True, export_morph=True)
    # one clip for everything (the exporter writes one per object), so a viewer's Play runs the lot
    b = open(glb, "rb").read()
    ln, _ = struct.unpack_from("<II", b, 12)
    d = json.loads(b[20:20 + ln]); tail = b[20 + ln:]
    merged = {"name": "Island Carry • water and ball • 8 seconds", "samplers": [], "channels": []}
    for a in d.get("animations", []):
        off = len(merged["samplers"]); merged["samplers"].extend(a["samplers"])
        for c in a["channels"]:
            c["sampler"] += off; merged["channels"].append(c)
    d["animations"] = [merged]
    animated = sorted({d["nodes"][c["target"]["node"]].get("name", "") for c in merged["channels"]})
    assert any(c["target"]["path"] == "weights" for c in merged["channels"]), "wave morphs missing from the GLB"
    assert any("Golf ball" in n for n in animated), "ball animation missing from the GLB"
    r = json.dumps(d, separators=(",", ":")).encode(); r += b" " * ((-len(r)) % 4)
    open(glb, "wb").write(struct.pack("<III", 0x46546C67, 2, 12 + 8 + len(r) + len(tail)) + struct.pack("<II", len(r), 0x4E4F534A) + r + tail)
    print("GLB", glb, round(os.path.getsize(glb) / 1e6, 2), "MB; animated:", animated)

if not option("--no-stills"):
    sc.render.engine = 'CYCLES'; sc.cycles.samples = 12; sc.cycles.use_denoising = True
    sc.render.image_settings.media_type = 'IMAGE'; sc.render.image_settings.file_format = 'PNG'
    sc.camera = cam; sc.render.resolution_x, sc.render.resolution_y = 1100, 700
    sc.frame_set(103); sc.render.filepath = os.path.join(OUT, "Hole_12_Ball_Bounce.png"); bpy.ops.render.render(write_still=True)
    sc.frame_set(60); sc.render.filepath = os.path.join(OUT, "Hole_12_Carry.png"); bpy.ops.render.render(write_still=True)
    sc.camera = obj("CAM_Presentation"); sc.render.resolution_x, sc.render.resolution_y = 1100, 1269
    sc.frame_set(60); sc.render.filepath = os.path.join(OUT, "Hole_12_Clean_Terrain.png"); bpy.ops.render.render(write_still=True)
    print("STILLS done")

if not option("--no-video"):
    eevee(12)
    sc.camera = cam
    sc.render.resolution_x, sc.render.resolution_y = 1280, 720
    sc.render.image_settings.media_type = 'VIDEO'          # Blender 5: a movie is a media type first
    sc.render.image_settings.file_format = 'FFMPEG'
    sc.render.ffmpeg.format, sc.render.ffmpeg.codec = 'MPEG4', 'H264'
    sc.render.ffmpeg.constant_rate_factor, sc.render.ffmpeg.ffmpeg_preset = 'MEDIUM', 'GOOD'
    sc.render.filepath = os.path.join(OUT, "Hole_12_Ball_Flight_")
    sc.frame_set(1)
    bpy.ops.render.render(animation=True)
    made = [n for n in os.listdir(OUT) if n.startswith("Hole_12_Ball_Flight_") and n.endswith(".mp4")]
    if made:
        final = os.path.join(OUT, "Hole_12_Ball_Flight.mp4")
        if os.path.exists(final): os.remove(final)
        os.rename(os.path.join(OUT, made[0]), final)
        print("VIDEO", final, round(os.path.getsize(final) / 1e6, 2), "MB")
