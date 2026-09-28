> **Historical legacy-base pipeline.** For the current Hero, tennis/golf differences and golf migration, start at [the character handoff](character-handoff/README.md). Current Hero size/face and golf appearance parity are not implemented by this older document.

# Shared player bases

The supplied male and female GLBs are the player's visual bases in the locker, home/loading previews, tennis and golf. Source files are preserved in `SportsLibrary/Customization/Bases/`.

The GLBs contain character sheets rather than single rigged avatars. `blender/scripts/export_player_bases.py` extracts the first complete front-facing figure and a separate hand, retains the supplied head and hands, then calls `build_sportswear.py` to build an original clothed torso, connected arms, relaxed shorts, legs, socks and sneakers on the tennis skeleton. The face uses a dedicated cropped atlas. `fit_golf_bases.py` fits the same figure to golf's taller rest skeleton. `fit_base_hair.py` creates fitted swept, curls and bob surfaces around each original head. Faces from the supplied bases remain intact; bald is also available.

## Customization

- Male/female base, six skin tones, four hairstyles, six hair colours and four face proportions.
- Continuous **Body size** slider from **Skinny** to **Big** (0–100%), with live preview; remote arrows move it in 5% increments.
- Shirt, shorts, shoe and racket colours; handedness.
- Controls on the left and the character on the right. Phone controls stack labels over their arrows to fit narrow widths; preview stays fully framed.
- `Player.bodySizeValue` is optional Codable storage. Older five-step `buildValue` profiles migrate through `bodySize` without resetting other choices. Old height storage remains readable, but is no longer exposed as an ineffective control.

Size blends authored Slim / Medium / Broad garment and body profiles without scaling heads, hands, skeletons, racket/club geometry or gameplay roots. Native JSON target positions and Unity FBX shape keys come from the same source. Golf receives the full appearance through `NativeSportsSession`, just like tennis.

## Hands and animation

The original supplied sheets had floating hands and shoulder stubs. The sportswear study replaces those with connected sleeves and forearms. Elbow and wrist endpoint weights accommodate the existing solver’s longer arm lengths. Tennis applies a common reach/clearance correction at every cosmetic size, then aligns equipment and applies contact guidance; the arm solver follows the final wrist positions while preserving the grips. Golf preserves its two-hand club constraint. Hair is attached using each sport's head bind pose, not the head's current animated pose.

## Rebuild assets

Run the three Blender scripts with `--background --python-exit-code 1 --python` in this order:

1. `export_player_bases.py`
2. `fit_golf_bases.py`
3. `fit_base_hair.py`

Then re-export Unity iOS, regenerate the Xcode projects with XcodeGen, and build the integrated app. Native JSON previews and runtime FBXs must ship together.

## Verification

`PlayerBaseTests` samples both bases at 0%, 50% and 100% size through tennis strokes and the golf swing, checks wrist-surface continuity, racket/club grip retention, UV seams and mesh bounds, and captures review images. Additional tennis tests cover moving arm/grip behaviour and live ball contact. Native tests verify precise slider persistence, backward-compatible profiles and hosted phone/TV locker layouts. Physical-device animation review remains a user acceptance step after installation.

## Sportswear study 01 (visual iteration)

This is a reviewable prototype, not a locked character design. Evaluate front / side / back at all three sizes, then tennis serve/drive/volley and golf motion. The original supplied face remains recognizable; facial expression, hairstyle variety and finer garment details can be refined after reviewing the silhouette. No third-party character art is bundled.

The new Blender script preserves source GLBs. Rerunning the three export scripts recreates both native and Unity assets. Golf transforms every size target from a snapshot: mesh Basis coordinates must not be transformed twice.

`render_sportswear_study.py` renders front/side/back female views and male front views at all three sizes into `/tmp/sportswear-review`. Runtime pose captures are written by `PlayerBaseTests` to `/tmp/player-base-review`.
