# Portable Dense15 source

Run from a repository checkout; Blender uses two CPU threads and performs no image render unless `--render` is requested.

```sh
/Applications/Blender.app/Contents/MacOS/Blender -b -t 2 --python ArtDir/golf/tools/build-coastal-turf.py -- --out work/turf-rebuild
/Applications/Blender.app/Contents/MacOS/Blender -b -t 2 --python ArtDir/golf/tools/verify-coastal-turf.py -- --rebuilt work/turf-rebuild/CoastalTurf.blend --out work/turf-rebuild/portable-geometry-proof.json
```

The builder resolves its repository root from its canonical tools location or an explicit --repo argument. Required immutable packed-map/material input: ArtDir/golf/source/turf-history/CoastalTurf8.blend. The historical factory is retained at ArtDir/golf/tools/build-coastal-turf8.py. Canonical current source: ArtDir/golf/source/CoastalTurf.blend. No input depends on work/proof scratch.

The saved portable-geometry-proof.json shows exact vertex, face, UV and object-transform digests for all three rebuilt source meshes. Binary FBX identity is not claimed because export metadata/timestamps may differ. The installed FBX retains its accepted ce71822… hash and unchanged GUID; rebuilding source does not automatically overwrite Unity Assets.

These source plates certify the physical Dense15 fan geometry. Surface/color appearance is runtime-owned by GolfTurfPalette.cs and requires actual Unity photos. Initial source/actual pictures predate Turf Hierarchy16.
