# Repaired character 3D review

Source: the current `HeroMenu` binary/manifest exported from the repaired production prefab, the same data used by the phone locker. Shared skin/hair tints ported from `HeroV4.tintedAtlas`; body-size deltas are the exported `BuildSlim` / `BuildBroad` corrections.

This is an actual mesh viewer in a static idle pose. It is not a new character, a generated mockup or an animation demonstration. Lighting is a neutral browser review setup, not Unity or SceneKit lighting. The five haircuts offered in the app, both body presets, four headwear options and body-size morphs can be inspected. Choices are local to this review and do not edit the saved player.

Run from this directory:

```
python3 server.py
```

Open `http://127.0.0.1:8771/`. Drag to orbit, scroll/pinch to zoom, right-drag to pan. The download button exports the visible assembled look to the fixed local file `MotionClub_RepairedHero.glb` and reveals a link (static posed mesh, without a rig or animation clips). Export overwrites that review file only. For authoring/rig review use the adjacent `Hero_Integrity_v1.blend`.

Three.js 0.186.0 copied from the existing local character-render tool. MIT license at `vendor/three/LICENSE`. All dependencies/assets are local; no account, remote upload or network CDN is needed.
