"""SWING FIX: re-author the golfer's swing takes in the SHIPPED golfer FBX, leaving everything else in it as it is.

blender/matchhero (the sources matchhero_golf.py rebuilds the whole golfer from) is not on this machine, and a full rebuild would also redo the body,
kit, hair and the borrowed emotes. So this loads Unity/Assets/Resources/Hero/golfer_<m|f>.fbx, poses the authored swings with golf_swing.py on its own
rig and clubs (the same solver matchhero_golf.py calls, with the same arguments), replaces only those takes, and writes the FBX back with
matchhero_golf.py's export settings.

    Blender -b --factory-startup --python splice_swings.py -- --sex Male --in <golfer_m.fbx> [--gs <dir with golf_swing.py>]
        [--only Drive,IronSwing] [--verify] [--measure] [--out <dir>]

--verify  regenerates the takes with the given golf_swing.py and compares them with the FBX's own takes (bone heads + club, every frame):
          with the shipped golf_swing.py this must come out ~0, which proves the rig, clubs and solver set-up here are the ones the FBX was built with.
--measure prints the swing measurements (elbows, hinge, head, spine, knees, slide) at address / top / P5 / P6 / impact / finish.
--out     writes <out>/golfer_<m|f>.fbx and <out>/golfer_<m|f>_clips.json (the clips json updated for the re-authored takes only).
"""
import bpy, sys, os, math, json, importlib.util
from mathutils import Vector, Matrix, Quaternion

ARGV = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
def arg(n, d=None): return ARGV[ARGV.index(n) + 1] if n in ARGV else d
SEX = arg("--sex", "Male"); FEMALE = SEX == "Female"; TAG = "f" if FEMALE else "m"
IN = arg("--in"); OUT = arg("--out"); GS_DIR = arg("--gs")
VERIFY = "--verify" in ARGV; MEASURE = "--measure" in ARGV
REPO = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
GS_DIR = GS_DIR or os.path.join(REPO, "blender", "scripts")

ORDER = ["Drive", "IronSwing", "HalfSwing", "Chip", "Putt", "Idle", "Wave", "Cheer", "FistPump",
         "Walk", "RunForward", "Emote_Scuba", "Emote_Spike", "Emote_Thrust", "Intro_BringIt", "Intro_Pushups", "Intro_Wave"]
HELD = {"Drive": "Driver", "IronSwing": "Iron", "HalfSwing": "Iron", "Chip": "Wedge", "Putt": "Putter"}
AUTHORED_NAMES = ["Drive", "IronSwing", "HalfSwing", "Chip", "Putt"]
ONLY = [c for c in (arg("--only", "Drive,IronSwing,HalfSwing,Chip").split(","))]
# (matchhero_golf.py's constants)
CLUB_LENGTH = {"Driver": 1.14, "Iron": 0.97, "Wedge": 0.92, "Putter": 0.86}
V4_CLUB_LENGTH = {"Driver": 1.156, "Iron": 1.018, "Wedge": 0.955, "Putter": 0.906}
SIDES = (("L", "Left"), ("R", "Right"))
FINGERS = ("Index", "Middle", "Ring", "Little")
GRIP = {"Index": (50, 70, 40), "Middle": (55, 75, 45), "Ring": (62, 80, 45), "Little": (68, 82, 45)}

bpy.ops.wm.read_factory_settings(use_empty=True)
scn = bpy.context.scene
bpy.ops.import_scene.fbx(filepath=IN)
rig = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
meshes = [o for o in bpy.data.objects if o.type == 'MESH']
club_meshes = [o for o in meshes if o.name.startswith("CLUB_")]
bpy.context.view_layer.update()
def near_identity(M, tol=1e-5): return all(abs(M[i][j] - (1.0 if i == j else 0.0)) < tol for i in range(4) for j in range(4))
assert near_identity(rig.matrix_world), f"the armature is expected at the origin: {rig.matrix_world}"
assert near_identity(rig.data.bones["Club"].matrix_local), "the Club bone is expected to rest at the origin"

spec = importlib.util.spec_from_file_location("golf_swing", os.path.join(GS_DIR, "golf_swing.py"))
gs = importlib.util.module_from_spec(spec); spec.loader.exec_module(gs)
print("golf_swing from", os.path.join(GS_DIR, "golf_swing.py"))

def rest_head(n): return rig.data.bones[n].head_local.copy()
def rest_rot(n): return rig.data.bones[n].matrix_local.to_3x3()
LEN = {}
for s, side in SIDES:
    for key, (a, b, c) in ((f"arm{s}", (f"{side}UpperArm", f"{side}LowerArm", f"{side}Hand")), (f"leg{s}", (f"{side}UpperLeg", f"{side}LowerLeg", f"{side}Foot"))):
        LEN[key] = ((rest_head(b) - rest_head(a)).length, (rest_head(c) - rest_head(b)).length)
GEO = {}
for m in club_meshes:
    assert near_identity(m.matrix_world), m.name
    nm = m.name[5:].title()
    GEO[nm] = gs.ClubGeo(m, nm, CLUB_LENGTH[nm], V4_CLUB_LENGTH[nm])      # (the FBX's club meshes are already prepared: right-handed, sole flat at the lie)
GOLFER = gs.Golfer(gs.Rig({b.name: rest_head(b.name) for b in rig.data.bones}, {b.name: rest_rot(b.name) for b in rig.data.bones},
                          {b.name: (b.parent.name if b.parent else None) for b in rig.data.bones}, LEN), FEMALE, GEO)
AUTHORED = {"Drive": lambda: gs.full_swing(GOLFER, "Driver", top=56, impact=69, finish=90, end=104),
            "IronSwing": lambda: gs.full_swing(GOLFER, "Iron"),
            "HalfSwing": lambda: gs.half_swing(GOLFER),
            "Chip": lambda: gs.chip(GOLFER),
            "Putt": lambda: gs.putt(GOLFER)}


def finger_pose(gamma):
    out = {}
    for s_, side_ in SIDES:
        fan = -gamma[s_] if s_ == "L" else gamma[s_]
        for fin in FINGERS:
            for i, part in enumerate(("Proximal", "Intermediate", "Distal")):
                q = Quaternion((1, 0, 0), math.radians(GRIP[fin][i]))
                if i == 0: q = Quaternion((0, 0, 1), fan) @ q
                out[f"{side_}{fin}{part}"] = q
    return out


def authored_clip(clip):
    sw = AUTHORED[clip]()
    frames = []
    for i in range(sw.end + 1):
        P, info = sw.pose(i)
        # the lead hand and lead shoulder in the chest's turning frame, from the mid-shoulder point, in units of his reach (the club keys' `hands` coordinates)
        Rch = GOLFER.heading(info["p"]); S = info["S"]
        kc = lambda w: GOLFER.to_rfu(Rch.inverted() @ (w - S)) / GOLFER.reach
        GL = info["origin"] + info["R"] @ Vector((0, 0, GOLFER.geos[sw.club].top - GOLFER.GRIP_LEAD))
        frames.append((i + 1, {"basis": P.basis, "club": info["M"], "heads": dict(P.heads), "head": info["head"], "R": info["R"], "fingers": finger_pose(info["gamma"]),
                              "keyhand": kc(GL), "keyshoulder": kc(P.child_head("LeftUpperArm", "LeftShoulder")), "p": info["p"],
                              "miss": info["miss"], "leg_miss": info["leg_miss"]}))
    misses = sorted(((max(r["miss"].values()), f) for f, r in frames), reverse=True)[:4]
    print(f"{clip}: worst hand misses (cm @ frame): " + ", ".join(f"{m * 100:.2f}@{f - 1}" for m, f in misses))
    worst_arm = max(max(r["miss"].values()) for _f, r in frames); worst_leg = max(max(r["leg_miss"].values()) for _f, r in frames)
    print(f"{clip}: authored {len(frames)} frames, top {sw.top}, impact {sw.impact}, finish {sw.finish}; hands miss the grip by {worst_arm * 100:.2f} cm at most, a foot its ground by {worst_leg * 100:.2f} cm")
    return frames, sw


def write_action(name, frames_data, held=True):
    """(matchhero_golf.py write_action, unchanged in effect)"""
    for pb in rig.pose.bones: pb.rotation_mode = 'QUATERNION'
    rig.animation_data_create()
    act = bpy.data.actions.new(name)
    rig.animation_data.action = act
    slot = act.slots.new('OBJECT', rig.name) if not len(act.slots) else act.slots[0]
    rig.animation_data.action_slot = slot
    layer = act.layers.new("L") if not len(act.layers) else act.layers[0]
    strip = layer.strips.new(type='KEYFRAME') if not len(layer.strips) else layer.strips[0]
    cb = strip.channelbag(slot, ensure=True)
    n_f = len(frames_data)
    def curve(path, idx, values):
        fc = cb.fcurves.new(path, index=idx)
        fc.keyframe_points.add(n_f)
        co = []
        for (fr, _), v in zip(frames_data, values): co += [float(fr), float(v)]
        fc.keyframe_points.foreach_set('co', co)
        for kp in fc.keyframe_points: kp.interpolation = 'LINEAR'
        fc.update()
    def quats(getter):
        qs = [getter(rec) for _, rec in frames_data]
        for i in range(1, len(qs)):
            if qs[i].dot(qs[i - 1]) < 0: qs[i] = -qs[i]
        return qs
    bones = [b for b in rig.data.bones.keys() if b in frames_data[0][1]["basis"]]
    for n in bones:
        qs = quats(lambda rec: rec["basis"][n][0].to_quaternion())
        for k in range(4): curve(f'pose.bones["{n}"].rotation_quaternion', k, [q[k] for q in qs])
        if n in ("Root", "Hips"):
            for k in range(3): curve(f'pose.bones["{n}"].location', k, [rec["basis"][n][1][k] for _, rec in frames_data])
    for s, side in SIDES:
        for fin in FINGERS:
            for i, part in enumerate(("Proximal", "Intermediate", "Distal")):
                n = f"{side}{fin}{part}"
                if n not in rig.data.bones: continue
                qs_ = quats(lambda rec: rec["fingers"][n])
                for k in range(4): curve(f'pose.bones["{n}"].rotation_quaternion', k, [q_[k] for q_ in qs_])
    qs, ls, ss = [], [], []
    for fr, rec in frames_data:
        loc, rot, scl = rec["club"].decompose(); qs.append(rot); ls.append(loc); ss.append(scl)
    for i in range(1, len(qs)):
        if qs[i].dot(qs[i - 1]) < 0: qs[i] = -qs[i]
    for k in range(4): curve('pose.bones["Club"].rotation_quaternion', k, [q[k] for q in qs])
    for k in range(3): curve('pose.bones["Club"].location', k, [l[k] for l in ls])
    for k in range(3): curve('pose.bones["Club"].scale', k, [sc[k] for sc in ss])
    return act


def sample(act, frames, bones=None):
    """Every bone's armature-space head and tail, and the Club bone's matrix, at each frame of the action."""
    rig.animation_data_create(); rig.animation_data.action = act
    if len(act.slots): rig.animation_data.action_slot = act.slots[0]
    out = []
    for f in frames:
        scn.frame_set(f)
        out.append({pb.name: (pb.head.copy(), pb.tail.copy()) for pb in rig.pose.bones if bones is None or pb.name in bones})
    return out


def action_of(clip):
    return bpy.data.actions.get(f"{rig.name}|{clip}") or bpy.data.actions.get(clip)


# ---------------------------------------------------------------- measurements (golf_swing's frame: ball along -X, target along -Y, up +Z)
def flex(h, a, b, c):
    u, v = h[a] - h[b], h[c] - h[b]; return 180 - math.degrees(u.angle(v))


def metrics(frames, sw):
    H = [rec["heads"] for _f, rec in frames]
    shaft = [(rec["R"] @ Vector((0, 0, -1))).normalized() for _f, rec in frames]      # up the shaft is +Z of the club frame: the hands -> head direction is -Z
    top, imp, fin = sw.top, sw.impact, sw.finish
    def lead_arm_elev(i):
        v = H[i]["LeftHand"] - H[i]["LeftUpperArm"]; return math.degrees(math.atan2(v.z, math.hypot(v.x, v.y)))
    def hinge(i): return math.degrees((H[i]["LeftHand"] - H[i]["LeftLowerArm"]).angle(shaft[i]))
    def shaft_elev(i): return math.degrees(math.asin(max(-1, min(1, shaft[i].z))))
    def spine(i): v = H[i]["Neck"] - H[i]["Hips"]; return math.degrees(math.atan2(-v.x, v.z))
    # P5: lead arm closest to horizontal on the way down; P6: shaft closest to horizontal after P5 (pointing back, head behind the hands)
    p5 = min(range(top, imp), key=lambda i: abs(lead_arm_elev(i)))
    p6 = min(range(p5, imp), key=lambda i: abs(shaft_elev(i)))
    keys = [("address", 0), ("top", top), ("P5", p5), ("P6", p6), ("impact", imp), ("impact+3", min(imp + 3, len(H) - 1)), ("finish", fin)]
    a = 0
    rows = [("lead elbow flex", lambda i: flex(H[i], "LeftUpperArm", "LeftLowerArm", "LeftHand")),
            ("trail elbow flex", lambda i: flex(H[i], "RightUpperArm", "RightLowerArm", "RightHand")),
            ("lead arm elevation", lead_arm_elev),
            ("hinge forearm-shaft", hinge),
            ("shaft elevation", shaft_elev),
            ("spine bend", spine),
            ("head drop cm", lambda i: (H[i]["Head"].z - H[a]["Head"].z) * 100),
            ("head to ball cm", lambda i: -(H[i]["Head"].x - H[a]["Head"].x) * 100),
            ("head to target cm", lambda i: -(H[i]["Head"].y - H[a]["Head"].y) * 100),
            ("pelvis to target cm", lambda i: -(H[i]["Hips"].y - H[a]["Hips"].y) * 100),
            ("pelvis height cm", lambda i: (H[i]["Hips"].z - H[a]["Hips"].z) * 100),
            ("lead knee flex", lambda i: flex(H[i], "LeftUpperLeg", "LeftLowerLeg", "LeftFoot")),
            ("trail knee flex", lambda i: flex(H[i], "RightUpperLeg", "RightLowerLeg", "RightFoot")),
            ("lead hand above head cm", lambda i: (H[i]["LeftHand"].z - H[i]["Head"].z) * 100),
            ("lead hand from head cm", lambda i: (H[i]["LeftHand"] - H[i]["Head"]).length * 100)]
    out = {"frames": {k: i for k, i in keys}}
    lines = [f"  {'':<24}" + "".join(f"{k + ' f' + str(i):>14}" for k, i in keys)]
    for name, fn in rows:
        vals = [fn(i) for _k, i in keys]; out[name] = vals
        lines.append(f"  {name:<24}" + "".join(f"{v:14.1f}" for v in vals))
    for k, i in keys:
        rec = frames[i][1]
        lines.append(f"  {k:<9} key hands (r,f,u) {tuple(round(x, 2) for x in rec['keyhand'])}  lead shoulder {tuple(round(x, 2) for x in rec['keyshoulder'])}"
                     f"  lead stretch {(H[i]['LeftHand'] - H[i]['LeftUpperArm']).length / GOLFER.reach:.3f}  trail {(H[i]['RightHand'] - H[i]['RightUpperArm']).length / GOLFER.reach:.3f}"
                     f"  hips {rec['p']['hip_turn']:.0f} shoulders {rec['p']['shoulder_turn']:.0f}")
    # the hinge through the downswing (the cast shows here)
    lines.append("  downswing hinge by frame: " + " ".join(f"f{i}:{hinge(i):.0f}/{lead_arm_elev(i):+.0f}" for i in range(top, imp + 1)))
    print("\n".join(lines))
    return out


# ---------------------------------------------------------------- run
report = {}
new_actions = {}
for clip in ONLY:
    frames, sw = authored_clip(clip)
    if MEASURE: print(f"MEASURE {SEX} {clip}"); report[clip] = metrics(frames, sw)
    if VERIFY:
        old = action_of(clip)
        fr = [f for f, _ in frames]
        a = sample(old, fr)
        act = write_action("__verify_" + clip, frames)
        b = sample(act, fr)
        dp = max((a[i][n][0] - b[i][n][0]).length for i in range(len(fr)) for n in a[i])
        dt = max((a[i][n][1] - b[i][n][1]).length for i in range(len(fr)) for n in a[i])
        worst = max(((a[i][n][1] - b[i][n][1]).length, n, fr[i]) for i in range(len(fr)) for n in a[i])
        print(f"VERIFY {SEX} {clip}: {len(fr)} frames vs the FBX's take ({int(old.frame_range[0])}-{int(old.frame_range[1])}): max head {dp * 1000:.2f} mm, max tail {dt * 1000:.2f} mm (worst {worst[1]} f{worst[2]})")
        bpy.data.actions.remove(act)
    new_actions[clip] = (frames, sw)

if OUT:
    os.makedirs(OUT, exist_ok=True)
    acts = {}
    for clip in ORDER:
        if clip in new_actions:
            old = action_of(clip)
            if old: bpy.data.actions.remove(old)
            acts[clip] = write_action(clip, new_actions[clip][0])
        else:
            acts[clip] = action_of(clip)
            assert acts[clip] is not None, clip
    rig.animation_data.action = None
    for t in list(rig.animation_data.nla_tracks): rig.animation_data.nla_tracks.remove(t)
    track = rig.animation_data.nla_tracks.new()
    at = 1
    for clip in ORDER:
        act = acts[clip]
        strip = track.strips.new(clip, at, act)
        try: strip.action_slot = act.slots[0]
        except Exception: pass
        at = int(strip.frame_end) + 10
    rig.data.pose_position = 'POSE'
    scn.render.fps = 30; scn.render.fps_base = 1.0
    sel = [rig] + meshes
    for o in scn.objects: o.select_set(False)
    for o in sel: o.select_set(True)
    bpy.context.view_layer.objects.active = rig
    path = os.path.join(OUT, f"golfer_{TAG}.fbx")
    with bpy.context.temp_override(selected_objects=sel, active_object=rig, object=rig):
        bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'ARMATURE', 'MESH'},
                                 apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', global_scale=1.0,
                                 axis_forward='-Z', axis_up='Y', bake_space_transform=False,
                                 use_mesh_modifiers=True, mesh_smooth_type='OFF', add_leaf_bones=False,
                                 use_armature_deform_only=True, armature_nodetype='NULL',
                                 bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=True,
                                 bake_anim_use_all_actions=False, bake_anim_force_startend_keying=True,
                                 bake_anim_step=1.0, bake_anim_simplify_factor=0.0, path_mode='STRIP', embed_textures=False)
    print("exported", path)
    # the clips json: the re-authored takes' frames / top / impact and where the club head is at address and at impact
    src_json = os.path.join(os.path.dirname(IN), f"golfer_{TAG}_clips.json")
    js = json.load(open(src_json))
    for c in js["clips"]:
        if c["name"] not in new_actions: continue
        frames, sw = new_actions[c["name"]]
        m = next(o for o in club_meshes if o.name == "CLUB_" + HELD[c["name"]].upper())
        head_local = min((v.co for v in m.data.vertices), key=lambda v: v.z).copy()
        path_ = [rec["club"] @ head_local for _, rec in frames]
        c["frames"], c["top"], c["impact"] = len(frames), sw.top, sw.impact
        addr = path_[0]
        best = min(range(min(c["top"], len(path_) - 1), len(path_)), key=lambda i: (path_[i] - addr).length)
        if abs(best - c["impact"]) > 3:
            print(f"{c['name']}: the club is back at the ball at frame {best}, not {c['impact']}: impact moved"); c["impact"] = best
        c["headAddress"] = list(path_[0]); c["headImpact"] = list(path_[min(c["impact"], len(path_) - 1)])
    json.dump(js, open(os.path.join(OUT, f"golfer_{TAG}_clips.json"), "w"), indent=2)
    print("clips json written")
if report:
    json.dump(report, open(os.path.join(OUT or os.path.dirname(os.path.abspath(__file__)), f"measure_{TAG}.json"), "w"), indent=1)
