# Receiving stance and gameplay camera — 2026-09-28

Scoped changes in HeroTennisDriver, TennisGame and TennisJuice, preserving concurrent venue/menu/character work.

- Receiving pose applies to either receiver during the opposing serve. Taller soft-knee stance (hip rise .14 m vs .085 m), racket in front, restrained weight shift.
- Apply stance after torso springs. Preserve both wrist world rotations during arm IK. Replant feet after all hip displacement/rotation, preserving foot orientations. This fixes the previous solve ordering that rotated grips and moved feet after planting.
- Camera anchor: height 5.8 m, 5.8 m behind player; look 7.2 m ahead at .95 m height; FOV 54. Lateral follow damped. Same camera through receiving, rallies and point results. Intro presentation remains separate.
- Point call remains; winner/loser reaction camera and live override cuts are no longer used. Player serve preparation/toss retain the original shoulder-view switch; no outgoing-ball opponent zoom.

Verification: TennisReceiveCameraTests passed in current project and isolated prior-behavior snapshot. Current test holds opponent in serve preparation, then awards a scripted point and checks that camera stays elevated and near its original position throughout the point result.

Review: ../ArtDir/review/receive-camera/Before_After_Receive_Camera_60fps.mp4 — 9 seconds, 60 fps, silent offline Unity capture. Left prior behavior, right revised. 0–2 s gameplay receiving; 2–4 s matching review-only close-up; 4–7 s gameplay receiving; 7–9 s point result. Close-up is an inspection camera used only by the test. Baseline copied current shared project into /private/tmp/tennis-receive-before and reversed only this task's stance/camera edits. No shared-project rollback.

Capture env: RECEIVE_FFMPEG points to an installed ffmpeg; RECEIVE_BEFORE=1 disables only new-camera assertions for the baseline snapshot. No phone installation, iOS framework export, commit or push performed for this change.

Camera follow-up: Adnan requested an intermediate, more personal view. Lowered height 8.2→5.8 m and brought distance 7.2→5.8 m. Retains the continuous point-result camera. The original comparison video records the earlier higher camera; receiver-gameplay.png is refreshed by the follow-up test.

Serve follow-up: restored original player serve shoulder framing (2.5 m above player, 4.4 m behind, 2.1 m toward tossing side; FOV 52) during hold/toss. Rally returns to the 5.8 m midpoint camera. Point reaction cuts remain disabled. Regression verifies both serve phases and return to rally, alongside result continuity.
