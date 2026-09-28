# Tennis source motion preview

`tennis_source_preview.mp4`: four 4.4-second excerpts at real-time speed, 30 fps, 1280×720, two fixed camera angles. Forehand, backhand hard hit, first service and forehand volley. Source takes are 120 Hz C3D recordings included alongside the matching FBXs in the downloaded tennis pack.

The neutral capsule figure follows measured shoulder/elbow/wrist/hip/knee/ankle/head markers. The orange line is the recorded right-hand path over the previous quarter-second. No racket or ball track is inferred. This is a source-motion visualization, not a render of the locked V4 hero or a claim of successful retargeting. No character asset or Unity scene was changed.

Excerpts center on high right-wrist velocity away from capture startup/end. Selected windows have 100% valid markers for the tracked body landmarks. Exact source filenames/frame ranges and playback rate are in `preview_manifest.json`. Marker surface positions are an approximation to joint centers; proxy limb widths are illustrative.

The old binary FBX version (header 3000 / FBXVersion 5000) was rejected by Blender's importer, which requires 7100+. The matching raw recordings were used directly rather than changing the files' headers or inventing motions. A compatible conversion or C3D solving/retargeting step is still needed for Unity integration.

Source: MocapData / Eyes, JAPAN Co. Ltd. See ../SOURCE.md for the source URL and the publisher's ambiguous BY / BY-SA license references. Preview is local evaluation; not a shipping license determination.
