# High-frame-rate animation tests

These are short motion-review renders from the existing V4 Blender actions, at normal speed. The source .blend files and live Blender session were not changed.

| File | Actions | Rate |
|---|---|---|
| tennis-60fps.mp4 | Lateral run, running forehand | 60 fps |
| tennis-120fps.mp4 | Same tennis actions and framing | 120 fps |
| bowling-60fps.mp4 | Straight and hook deliveries | 60 fps |
| golf-60fps.mp4 | Drive and putt | 60 fps |
| boxing-60fps.mp4 | Jab, lead hook, rear uppercut, slip left | 60 fps |

720×480 Workbench renders isolate motion from final lighting. Boxing uses an external review camera to expose full-body mechanics, not the planned first-person gameplay camera. Cuts separate actions, not runtime animation transitions.

## What was tested

The source timeline is authored at 30 fps. Each new output frame evaluates Blender at a fractional source frame: 0.5-frame increments for 60 fps, 0.25 for 120 fps. This preserves duration and interpolates the existing animation curves; it does not duplicate the old 10 fps video frames or speed up playback. Each clip excludes its terminal boundary frame to preserve exact duration.

Higher sampling does not add newly authored motion detail, improve poses, correct clothing intersections, or prove smooth transitions. Sparse authored keys and interpolation still determine the underlying motion quality.

## What remains for Unity

These are offline renders, not performance benchmarks. Test imported characters, actual arenas/crowds, lighting, physics and phone input on target devices. Measure frame time, input latency, sustained performance and thermal behavior. Budgets are approximately 16.67 ms at 60 fps and 8.33 ms at 120 fps. Display and playback support determine whether the 120 fps comparison is visible; a 60 Hz display cannot show all 120 frames each second.

Use 60 fps as an initial integration target, and evaluate an optional 120 fps mode on supported hardware after profiling. Do not merely change the original Blender timeline rate: that would change animation speed without retiming.
