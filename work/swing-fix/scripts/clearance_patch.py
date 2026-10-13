"""Swing fix, runtime side: GolfSwingClearance.cs (the new Drive's release shaft; a weight for the follow-through wrap) and GolferView.cs (the wrap fades out
with the finish pose during a crossfade instead of vanishing). Exact-string, idempotent: python3 clearance_patch.py <GolfSwingClearance.cs> <GolferView.cs>"""
import sys
def patch(path, pairs):
    s = open(path).read(); changed = False
    for old, new in pairs:
        if new in s: continue
        assert s.count(old) == 1, f"{path}: not found:\n{old[:120]}"
        s = s.replace(old, new); changed = True
    if changed: open(path, "w").write(s)
    print(("patched " if changed else "already patched ") + path)
patch(sys.argv[1], [
    ('''            // Authored Drive shaft at F076 (2.50 s), in model space. Anchoring the
            // direction here prevents the changing source pose from flipping the blend.
            bool female = hero.Root.GetComponent<GolfArcade.Tennis.MatchHeroLook>().female;
            driveReleaseShaft = female ? new Vector3(-.177348f, .9043239f, .38826f)
                                      : new Vector3(-.1848222f, .8964227f, .4028237f);''',
     '''            // Authored Drive shaft at F076 (2.50 s), in model space. Anchoring the
            // direction here prevents the changing source pose from flipping the blend.
            // (Re-measured for the 2026-10-12 swing fix: work/swing-fix/scripts/release_shaft.py.)
            bool female = hero.Root.GetComponent<GolfArcade.Tennis.MatchHeroLook>().female;
            driveReleaseShaft = female ? new Vector3(-.2134009f, .8788473f, .4267172f)
                                      : new Vector3(-.2184253f, .8734274f, .4352183f);'''),
    ('''        public void ApplyDriveFollowThrough(float clipTime)
        {
            float progress = Mathf.InverseLerp(2.50f, 3.02f, clipTime);
            if (progress <= 0) return;''',
     '''        /// `weight` is how much of the pose on screen is this clip time's (a crossfade leaving the finish behind fades the wrap out with it).
        public void ApplyDriveFollowThrough(float clipTime, float weight = 1f)
        {
            float progress = Mathf.InverseLerp(2.50f, 3.02f, clipTime);
            if (progress <= 0 || weight <= 0) return;'''),
    ('''            var turn = Quaternion.FromToRotation(shaft, desiredShaft);
''', '''            var turn = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(shaft, desiredShaft), weight);
'''),
    ('''            var targetGrip = headCentre + radialDirection * radialDistance + up * height;
''', '''            var targetGrip = Vector3.Lerp(originalGrip, headCentre + radialDirection * radialDistance + up * height, weight);
'''),
    ('''            float elbowBlend = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(2.50f, 2.63f, clipTime));''',
     '''            float elbowBlend = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(2.50f, 2.63f, clipTime)) * weight;'''),
])
patch(sys.argv[2], [
    ('''                if (clipName == "Drive" && clip.IsValid()) swingClearance?.ApplyDriveFollowThrough((float)clip.GetTime());''',
     '''                if (clipName == "Drive" && clip.IsValid())
                {
                    // The driver's wrap belongs to the pose on screen: while a crossfade leaves the finish behind (Settle, the next backswing) it fades
                    // out with that pose, instead of vanishing on the crossfade's first frame (the club jumped ~1.2 m back to the raw clip's finish).
                    if (fade > 0 && ghost.IsValid() && ghost.GetTime() >= 2.5) swingClearance?.ApplyDriveFollowThrough((float)ghost.GetTime(), fade);
                    else swingClearance?.ApplyDriveFollowThrough((float)clip.GetTime());
                }'''),
])
