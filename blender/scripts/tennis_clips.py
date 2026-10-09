"""Derive Unity animation clip splits from the animation studio's timeline markers.

The tennis rig's animation library lives in `SportsLibrary/Blender/sports-animation-studio-v4.blend`
(scene `03 TENNIS`), where every clip's first frame is named by a marker `V4 <Clip> <RH|LH>`.
The runtime FBX previously exposed six hand-written clips out of the forty-four that are
authored, so the game drove one mirrored forehand/backhand pair and faked everything else.

Deriving the splits here keeps them in step with the source: add a marker in Blender and the
clip appears in Unity. The derivation is checked against the six clips that were hand-written
before, which is what pins the frame arithmetic down (see `self_check`).

No `bpy` import, so this is importable and testable outside Blender.
"""
from __future__ import annotations

import hashlib
import json
import re
from pathlib import Path

# Markers sit one frame after the clip's first frame, and consecutive clips are separated by
# this many frames of padding. Both facts are implied by the six clips Unity already shipped.
MARKER_LEAD = 1
CLIP_GAP = 9

# Locomotion cycles are the only clips that should loop; a stroke must play once and stop.
LOOPING = {"Ready", "SplitStep", "RunLeft", "RunRight", "Retreat"}

# Clips whose authored motion genuinely leaves the ground. Unity strips root height by
# default, which is why the serve never jumped despite 0.2m of rise being animated into it.
# Lateral root travel stays locked for everything: gameplay owns court position, and letting
# a run clip drive X would fight the simulation.
AIRBORNE = {"Serve", "Smash", "VolleyForehand", "VolleyBackhand",
            "DiveForehand", "DiveBackhand", "RunningForehand", "RunningBackhand", "LowPickup",
            "Intro"}   # the intro emotes hop and jump

MARKER_PATTERN = re.compile(r"^V4 (?P<clip>[A-Za-z]+) (?P<hand>RH|LH)$")
STRIP_PATTERN = re.compile(r"^(?P<clip>[A-Za-z]+) (?P<hand>RH|LH)$")


def derive_from_strips(strips):
    """Clip splits taken straight from the V4 NLA strips.

    Each strip already carries the exact frame range its action occupies, so this needs no
    arithmetic at all. The older marker-based derivation inferred a clip's length from the
    gap to the *next marker of the same hand*, which silently breaks the moment clips are
    appended out of hand order — adding a right-handed clip after the left-handed block
    stretched the previous right-handed clip across sixteen hundred frames.

    `strips` is an iterable of (name, frame_start, frame_end) such as ("Ready RH", 1, 61).
    """
    clips = []
    for name, start, end in sorted(strips, key=lambda s: s[1]):
        match = STRIP_PATTERN.match(name.strip())
        if not match:
            continue
        clip, hand = match.group("clip"), match.group("hand")
        clips.append({
            "name": f"{clip}_{hand}",
            "clip": clip,
            "hand": hand,
            # Strips are 1-based on the timeline; Unity clip frames are 0-based.
            "firstFrame": int(start) - 1,
            "lastFrame": int(end) - 1,
            "loop": clip in LOOPING,
            "lockHeight": clip not in AIRBORNE,
        })
    return clips


def parse_markers(markers):
    """markers: iterable of (frame, name). Returns {hand: [(frame, clip), ...]} sorted."""
    found = {"RH": [], "LH": []}
    for frame, name in markers:
        match = MARKER_PATTERN.match(name.strip())
        if match:
            found[match.group("hand")].append((int(frame), match.group("clip")))
    for hand in found:
        found[hand].sort()
    return found


def derive(markers, timeline_end):
    """Turn markers into clip definitions.

    A clip runs from its marker (less the lead) for as long as the space until the next
    marker allows, minus the padding gap. That makes most clips 60 frames and the longer
    dive animations 90, without hardcoding either number.
    """
    hands = parse_markers(markers)
    ordered = [("RH", hands["RH"]), ("LH", hands["LH"])]
    clips = []
    for index, (hand, entries) in enumerate(ordered):
        if not entries:
            continue
        # A block ends where the next hand's block begins, or at the end of the timeline.
        following_block = ordered[index + 1][1] if index + 1 < len(ordered) else []
        block_end = following_block[0][0] if following_block else timeline_end + CLIP_GAP
        for position, (frame, clip) in enumerate(entries):
            nxt = entries[position + 1][0] if position + 1 < len(entries) else block_end
            length = nxt - frame - CLIP_GAP
            if length <= 0:
                raise ValueError(f"{hand} {clip} at frame {frame} has no room before {nxt}")
            first = frame - MARKER_LEAD
            clips.append({
                "name": f"{clip}_{hand}",
                "clip": clip,
                "hand": hand,
                "firstFrame": first,
                "lastFrame": first + length,
                "loop": clip in LOOPING,
                "lockHeight": clip not in AIRBORNE,
            })
    return clips


def stable_id(name: str) -> int:
    """Deterministic internalID so regenerating the meta does not churn the file."""
    digest = hashlib.sha256(name.encode()).digest()
    value = int.from_bytes(digest[:8], "big", signed=True)
    return value or 1


def clip_yaml(clip) -> str:
    return f"""    - serializedVersion: 16
      name: {clip['name']}
      takeName:
      internalID: {stable_id(clip['name'])}
      firstFrame: {clip['firstFrame']}
      lastFrame: {clip['lastFrame']}
      wrapMode: 0
      orientationOffsetY: 0
      level: 0
      cycleOffset: 0
      loop: 0
      hasAdditiveReferencePose: 0
      loopTime: {1 if clip['loop'] else 0}
      loopBlend: 0
      loopBlendOrientation: 1
      loopBlendPositionY: 1
      loopBlendPositionXZ: 1
      keepOriginalOrientation: 0
      keepOriginalPositionY: 0
      keepOriginalPositionXZ: 0
      heightFromFeet: 0
      mirror: 0
      bodyMask: 01000000010000000100000001000000010000000100000001000000010000000100000001000000010000000100000001000000
      curves: []
      events: []
      transformMask: []
      maskType: 3
      maskSource: {{instanceID: 0}}
      additiveReferencePoseFrame: 0
"""


def rewrite_meta(meta_path: Path, clips) -> int:
    """Replace the clipAnimations block in an FBX .meta, leaving every other setting alone."""
    text = meta_path.read_text()
    start = text.index("    clipAnimations:\n")
    body_start = start + len("    clipAnimations:\n")
    # The block ends at the next key indented to the same level (six spaces of list items
    # and deeper belong to the clips themselves).
    end = body_start
    for line in text[body_start:].splitlines(keepends=True):
        if line.startswith("    ") and not line.startswith("    - ") and not line.startswith("      "):
            break
        end += len(line)
    replacement = "".join(clip_yaml(c) for c in clips)
    meta_path.write_text(text[:start] + "    clipAnimations:\n" + replacement + text[end:])
    return len(clips)


# The real marker layout in `03 TENNIS`, used to pin the frame arithmetic. A clip's length
# depends on the gap to the NEXT marker, so this has to be the complete set: a sparse
# fixture would stretch a clip across its missing neighbours and prove nothing.
CLIP_ORDER = ["Ready", "SplitStep", "RunLeft", "RunRight", "BrakeLeft", "BrakeRight",
              "DirectionChange", "Retreat", "Forehand", "Backhand", "RunningForehand",
              "RunningBackhand", "LowPickup", "DiveForehand", "DiveBackhand",
              "GroundRecovery", "MissedSwing", "Serve", "Smash", "VolleyForehand",
              "VolleyBackhand", "Lob"]
RH_FRAMES = [1, 70, 139, 208, 277, 346, 415, 484, 553, 622, 691, 760, 829, 898, 997, 1096,
             1165, 1234, 1303, 1372, 1441, 1510]
LH_FRAMES = [1579, 1648, 1717, 1786, 1855, 1924, 1993, 2062, 2131, 2200, 2269, 2338, 2407,
             2476, 2575, 2674, 2743, 2812, 2881, 2950, 3019, 3088]
TIMELINE_END = 3149


def reference_markers():
    return ([(f, f"V4 {n} RH") for f, n in zip(RH_FRAMES, CLIP_ORDER)]
            + [(f, f"V4 {n} LH") for f, n in zip(LH_FRAMES, CLIP_ORDER)])


# The V4 strip layout as it stands in the blend, used to pin the conversion.
REFERENCE_STRIPS = [("Ready RH", 1, 61), ("SplitStep RH", 70, 130), ("RunLeft RH", 139, 199),
                    ("RunRight RH", 208, 268), ("BrakeLeft RH", 277, 337),
                    ("Forehand RH", 553, 613), ("Backhand RH", 622, 682)]


def self_check():
    """The six clips that were hand-written before must come out identical."""
    derived = {c["clip"]: (c["firstFrame"], c["lastFrame"])
               for c in derive(reference_markers(), TIMELINE_END) if c["hand"] == "RH"}
    from_strips = {c["clip"]: (c["firstFrame"], c["lastFrame"])
                   for c in derive_from_strips(REFERENCE_STRIPS)}
    for name in ("Ready", "SplitStep", "RunLeft", "RunRight", "Forehand", "Backhand"):
        assert from_strips[name] == derived[name], \
            f"{name}: strips {from_strips[name]} != markers {derived[name]}"
    expected = {"Ready": (0, 60), "SplitStep": (69, 129), "RunLeft": (138, 198),
                "RunRight": (207, 267), "Forehand": (552, 612), "Backhand": (621, 681)}
    for name, want in expected.items():
        assert derived[name] == want, f"{name}: derived {derived[name]} != shipped {want}"
    return True


if __name__ == "__main__":
    import sys

    self_check()
    root = Path(__file__).resolve().parents[2]
    manifest = json.loads((root / "blender/tennis-clips.json").read_text())
    for gender in ("male", "female"):
        meta = root / f"Unity/Assets/Resources/StandardCharacters/standard_{gender}_tennis.fbx.meta"
        count = rewrite_meta(meta, manifest["clips"])
        print(f"{meta.name}: {count} clips")
    print(f"clip names: {sorted({c['clip'] for c in manifest['clips']})}")
