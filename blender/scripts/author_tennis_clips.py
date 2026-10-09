"""Extend the V4 tennis animation block in the animation studio blend.

Two jobs:

  * **Convert** animations that were authored for the older V3 handedness library but never
    promoted to V4, so they have no strip in the V4 track and therefore never become clips.
  * (Authoring brand-new motions was planned here too, but measuring the library first showed
    the motions in question already exist -- the serve's jump is animated and was being
    stripped at import. See AIRBORNE in tennis_clips.py.)

Both append strips to the V4 NLA track past the end of the existing block, so every clip
already shipping keeps its exact frame range. `tennis_clips.derive_from_strips` reads the
strips back, so a strip added here becomes a Unity clip automatically on the next export.

Run:  Blender --background <studio.blend> --python author_tennis_clips.py -- [--save]
"""
import sys
from pathlib import Path

import bpy

V4_TRACK = "V4 | wardrobe + expanded motions"
CLIP_FRAMES = 60          # every V4 clip except the dives is sixty frames
GAP = 9                   # padding the existing block uses between clips

# Authored for V3 and never promoted. `None` means the action has no per-hand variant, so the
# same take is used for both. SwitchHands is deliberately excluded: nothing in the game can
# trigger it, so exporting it would only add weight.
CONVERT = ["Celebrate", "ForehandTopspin", "RecoverLeft", "RecoverRight", "Slice"]


def v4_track(rig):
    for track in rig.animation_data.nla_tracks:
        if track.name == V4_TRACK:
            return track
    raise SystemExit(f"Missing NLA track {V4_TRACK!r} on {rig.name}")


def block_end(track):
    return int(max((s.frame_end for s in track.strips), default=0))


def source_action(gender, clip, hand):
    """Prefer the handed V3 take, fall back to an unhanded one."""
    for name in (f"{gender}__Tennis__{clip}__{hand}", f"{gender}__Tennis__{clip}"):
        action = bpy.data.actions.get(name)
        if action:
            return action
    return None


def place(track, scene, name, action, start, length=CLIP_FRAMES):
    strip = track.strips.new(name, int(start), action)
    strip.frame_start = int(start)
    strip.frame_end = int(start + length)
    # Fit the source take into the slot rather than letting its own length set the range,
    # so every clip lands on the grid the exporter and Unity expect.
    strip.action_frame_start = action.frame_range[0]
    strip.action_frame_end = action.frame_range[1]
    scene.timeline_markers.new(f"V4 {name}", frame=int(start))
    return start + length + GAP


def run(save):
    scene = bpy.data.scenes["03 TENNIS"]
    bpy.context.window.scene = scene
    added = {}
    for gender in ("Male", "Female"):
        rig = bpy.data.objects[f"{gender}_Tennis_Rig"]
        track = v4_track(rig)
        existing = {s.name for s in track.strips}
        cursor = block_end(track) + GAP
        made = []
        for clip in CONVERT:
            for hand in ("RH", "LH"):
                name = f"{clip} {hand}"
                if name in existing:
                    continue
                action = source_action(gender, clip, hand)
                if not action:
                    print(f"SKIP {gender} {name}: no source action", flush=True)
                    continue
                cursor = place(track, scene, name, action, cursor)
                made.append(name)
        added[gender] = made
        print(f"ADDED {gender} {len(made)}: {', '.join(made)}", flush=True)
    scene.frame_end = max(scene.frame_end, block_end(v4_track(bpy.data.objects["Male_Tennis_Rig"])))
    print("TIMELINE_END", scene.frame_end, flush=True)
    if save:
        bpy.ops.wm.save_mainfile()
        print("SAVED", flush=True)


if __name__ == "__main__":
    run("--save" in sys.argv)
