# Original Hero V5 — interactive review

Uses the restored original V5 production FBXs/prefabs, freshly baked to the same native menu format as the phone. Both body presets, the five offered haircuts, four headwear choices and skin/hair colors are available. Original V5 proportions are locked; its meshes do not contain the integrity experiment’s BuildSlim/BuildBroad morphs.

Run `python3 server.py` here and open http://127.0.0.1:8771/. Drag to rotate, scroll/pinch to zoom, right-drag to pan. The local export action writes MotionClub_HeroV5.glb (static idle pose, no skeleton/animation tracks). Full authoring rig: ../Hero_01_V5.blend.

Review lighting differs from game lighting. V5 hair retains its original two-sided rendering. Three.js 0.186.0 is copied from the existing local tool; MIT license at vendor/three/LICENSE. No remote upload or CDN.
