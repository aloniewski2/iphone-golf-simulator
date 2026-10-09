# Playable tutorial and setup fixes — 24 September 2026

## Current gameplay and lesson

The latest tennis runtime automatically runs to incoming balls. The previous tutorial still required walking from the right serving half onto a left-side ring, while normal serving rules prevented crossing the centre line.

The tutorial now teaches five distinct actions on the actual court:

1. Serve: press TOSS at the meter's centre, then swing near the toss apex. Land one legal serve.
2. Forehand: return one coach feed after its bounce.
3. Backhand: return one feed from the other side.
4. Aim: land a return left, then right. The target and phone instruction update together.
5. Play one real point against Coach Ray, win or lose.

Timing is taught during the strokes. There is no separate walking or repeated timing exercise. Misses retry and trigger coaching; they do not silently complete the lesson. An explicit Skip this exercise option remains available. Touch controls include shot aim as well as power and Swing.

New steps are applied after the current physics update, preventing the previous drill's bounce or fault from affecting the next step. Netted serves count as failed attempts and retry normally. The final real point starts fresh.

## Screen setup and calibration

- A close LiDAR reading is no longer a setup blocker: it can be a laptop's desk or bezel rather than its display.
- The setup accepts a display below eye level (up to 60 degrees of lens pitch), shows a centre crosshair, and immediately offers explicit manual direction capture.
- A fresh camera frame and locked heading permit starting even when ARKit lacks visual features. IMU swings continue while positional tracking recovers.
- The brightness probe samples a smaller central patch to fit a laptop display.
- Swing detection retains a quiet re-arm observed during follow-through/recovery and recovers without requiring good camera tracking.
- Calibration uses 1.2-second beats and a wider matching window. Only confirmed swings count; seven scored beats follow two warm-up beats. The HUD shows the recorded count.

## Media

All 11 bundled menu videos were sampled across their duration; every clip includes characters. They and their posters are excluded from both app targets through project.yml and regenerated Xcode projects. The existing empty-court artwork is used in menus/loading screens. Original source media remains on disk but is not bundled or played. No new AI video was generated.

## Verification

- 153 Unity EditMode tests passed.
- Two Unity PlayMode tests passed: the complete five-step lesson using actual serves and returns without skipping, and coach feeds returned to both aim directions with GOOD-or-better timing on both wings.
- 49 native motion, screen-delay, and navigation tests passed; final native motion/delay rerun passed after the screen-start fallback change.
- Native menu snapshot and campaign tests passed. Reviewed MacBook 16:10 menus and phone serve/aim tutorial layouts. ImageRenderer does not render UIKit sliders; their yellow placeholder in snapshot output is a renderer limitation.
- Physical iPhone-to-MacBook AirPlay, camera tracking and real arm-motion acceptance still need device verification. Synthetic tests do not establish hardware performance.

## Build

Unity iOS export succeeded with zero errors. The unsigned integrated Release iPhone build succeeded. Its app bundle contains UnityFramework and Data, the empty-court artwork, and no menu MP4 files. The updated app has not been installed on a physical phone by this task.
