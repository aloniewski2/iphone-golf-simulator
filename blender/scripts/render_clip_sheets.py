"""Render review frames of the tennis clips, for contact sheets (see montage in
make_clip_sheets.py): evenly spaced frames of each clip from a fixed three-quarter camera,
so a whole motion is judged in one look instead of by scrubbing.

Optionally fits a Higgsfield body first (fit_higgs_characters.fit_character), so the clips
are reviewed on the character that will actually wear them. The studio file is not saved.

Run:  Blender --background --python render_clip_sheets.py -- OUT_DIR [--fit NAME GLB] [--clips A,B] [--frames 8]
"""
import json
import sys
from pathlib import Path

import bpy
from mathutils import Vector

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
ROOT = HERE.parents[1]
STUDIO = ROOT / "SportsLibrary/Blender/sports-animation-studio-v4.blend"
DEFAULT = ["Ready", "SplitStep", "RunLeft", "RunRight", "BrakeLeft", "Forehand", "Backhand", "RunningForehand",
           "RunningBackhand", "VolleyForehand", "VolleyBackhand", "LowPickup", "Lob", "Serve", "Smash",
           "DiveForehand", "GroundRecovery", "Celebrate", "Intro"]


def main():
    argv = sys.argv[sys.argv.index("--") + 1:]
    out = Path(argv[0]); out.mkdir(parents=True, exist_ok=True)
    wanted = argv[argv.index("--clips") + 1].split(",") if "--clips" in argv else DEFAULT
    per = int(argv[argv.index("--frames") + 1]) if "--frames" in argv else 8
    views = argv[argv.index("--views") + 1].split(",") if "--views" in argv else ["three"]
    bpy.ops.wm.open_mainfile(filepath=str(STUDIO))
    scene = bpy.data.scenes["03 TENNIS"]; bpy.context.window.scene = scene
    gender = "Male"
    if "--avatar" in argv:
        # The base avatar, fitted exactly as fit_avatar.py ships it.
        import fit_avatar
        head = float(argv[argv.index("--avatar") + 1])
        fit_avatar.fit.HEIGHT = fit_avatar.RIG_SHOULDER / fit_avatar.LANDMARKS["shoulder"]
        fit_avatar.fit.build_face = fit_avatar.avatar_face; fit_avatar.fit.remove_fragments = fit_avatar.small_fragments; fit_avatar.fit.remove_fists = fit_avatar.cut_hands
        fit_avatar.fit.REAL_ARMS = False
        report = []
        rig, body = fit_avatar.fit.fit_character(gender, report, fit_avatar.GLB, fit_avatar.NAME, fit_avatar.LANDMARKS)
        from coherent_avatar_body import rebuild
        rebuild(gender, rig, body, fit_avatar.fit.texture_pixels(body), report)
        fit_avatar.big_fists(gender)
        fit_avatar.scale_head(body, rig, head)
        print("\n".join("FIT " + r for r in report), flush=True)
    rig = bpy.data.objects[f"{gender}_Tennis_Rig"]
    body = bpy.data.collections[f"V4 {gender} Tennis | BODY"]
    kit = bpy.data.collections[f"V4 {gender} Tennis | KIT 1"]
    prop = next(c for c in scene.collection.children if c.name.startswith(f"V4 PROP {gender} V4 racket"))
    # The face decal has no texture in Blender (Unity draws the expression atlas on it).
    keep = {o for o in body.objects if "face decal" not in o.name} | set(kit.objects) | set(prop.all_objects)
    for o in scene.objects:
        if o.type in {"MESH", "CURVE"} and o not in keep: o.hide_render = True
    clips = json.loads((ROOT / "blender/tennis-clips.json").read_text())["clips"]
    cam_data = bpy.data.cameras.new("Review"); cam_data.lens = float(argv[argv.index("--lens") + 1]) if "--lens" in argv else 38
    cam = bpy.data.objects.new("Review camera", cam_data); scene.collection.objects.link(cam); scene.camera = cam
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"; scene.display.shading.color_type = "TEXTURE"
    scene.display.shading.show_shadows = True
    scene.render.resolution_x = 360; scene.render.resolution_y = 440
    scene.render.film_transparent = False
    for clip in clips:
        if clip["hand"] != "RH" or clip["clip"] not in wanted: continue
        first, last = clip["firstFrame"] + 1, clip["lastFrame"] + 1
        for i in range(per):
            f = round(first + (last - first) * i / (per - 1))
            scene.frame_set(f)
            # Follow the character (runs and dives travel) at waist height.
            hips = rig.matrix_world @ rig.pose.bones["Hips"].head
            target = Vector((hips.x, hips.y, 1.05))
            for view in views:
                offset = {"three": Vector((3.2, -4.6, .9)), "front": Vector((0, -5.6, .6)), "side": Vector((5.6, 0, .6))}[view]
                cam.location = target + offset
                cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
                scene.render.filepath = str(out / f"{clip['clip']}-{view}-{i:02d}.png")
                bpy.ops.render.render(write_still=True)
        print("SHEET", clip["clip"], first, last, flush=True)


main()
