# Hero V5 light recipe (Phase 2): one look for the locker and the court

The same idea everywhere: a warm, soft key at about 40° elevation; a cool fill at 30–40% of the key; low cool ambient; soft shadows tinted cool; low bloom; no grain.

## Court (Unity URP): `TennisLook.LightScene`
| Light | Value |
|---|---|
| Key ("Resort sun"), directional | toward-sun `(-.62, .76, .66)`, about 40° elevation (was 25°). Colour `(1, .90, .76)`, intensity 2.3, soft shadows at strength .72 (was .82). |
| Fill ("Hero fill"), directional, no shadows | Camera side, from-direction `(.30, .55, -.78)`. Colour `(.84, .90, 1)`, intensity .8 (35% of key). It keeps the rival's camera-facing face and the player's back from going gray. |
| Ambient | Trilight: sky `(.42,.58,.86)`, equator `(.66,.58,.50)`, ground `(.24,.26,.22)`. Unchanged; this is the cool shadow tint. |
| Post (`UrpSetup` → TennisPost) | ACES; bloom threshold 1.05, intensity .55; post-exposure +.2; contrast 18; saturation 20; vignette .22. Unchanged. |
| World vs hero | Env kit and V5 props use base colour ×.94, so characters sit a notch brighter and more saturated than the world. |

The per-object additional-light limit is 2 (URP asset), so the fill is always per-pixel on the heroes. It adds no shadow map, so the cost is one extra directional term.

## Locker (SceneKit): `CharacterModelPreview.Coordinator`
| Light | Value |
|---|---|
| Key, directional | 40° elevation, 38° to camera-left. Colour `(1, .92, .82)`, intensity 1050. Soft deferred shadow (radius 6, 8 samples, colour cool navy at .35 alpha). |
| Fill, directional | 18° elevation, 55° to camera-right. Colour `(.84, .90, 1)`, intensity 370 (35%). |
| Rim, directional | 35° elevation, from behind (160°). Colour `(1, .96, .90)`, intensity 520. |
| Ambient | Colour `(.86, .88, .96)`, intensity 210. It was 180 white, alongside two flat omnis. |
| Materials | Blinn; specular .05, shininess .15 (soft plastic, no specular blobs). |

## Cinematic cams (toss / ultimate / react)
These use the same scene lights. With the camera-side fill, close-ups of either player's face are never lit only by ambient.

## Blender review renders (`v5_tools/render_review.py`)
Key sun 3.2 at 50°/35°, fill 1.1 (34%), rim 1.6. The same ratios.
