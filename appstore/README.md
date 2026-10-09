# App Store assets: Motion Club

Everything here is built from the **Oct 8 `final-build`** visuals only. The older illustrated menu cast, club-room
art, early tennis clips and dev-review character renders are deliberately excluded.

| Folder | What | Spec |
| --- | --- | --- |
| `icons/` | 10 icon variations (`_preview-all-icons.png` shows them with iOS rounding and at 120/60 px) | 1024x1024 PNG, sRGB, no alpha, square corners |
| `screenshots/` | 3 sets x 6 shots: **A Resort** (light/scenic), **B Arena Night** (dark, gameplay-first), **C Party Pop** (bold colour) | 1320x2868 JPEG (iPhone 6.9"). `_preview-set-*.jpg` are contact sheets |
| `final/header/` | Header art. 5244x2950 is **.png only** (Apple); 3840x1646 may be .png or .jpg | see `final/` |
| `search/` | App Store search-result mockups (icon, name, subtitle, first 3 shots) | review comps, not for upload |
| `LISTING.md` | Name, subtitle, promo text, keywords, description, claims to verify | |
| `tools/` | Build scripts. `fetch_assets.sh` pulls source art, then `prep_upscale.py`, `build_icons.py`, `build_screenshots.py`, `build_header_search.py` | |

## What each image is made of
- **Real captures:** phone UI (`proof/menu-beta`, `ArtDir/review/post-match`), in-engine golf frames (`work/postcard-refresh`), tennis gameplay, and frames from the bundled Cliffside golf and Skyscraper tennis clips.
- **Shipped art:** `golf-controller-artwork.png`, venue map art, club crest and sport icons.
- **Added by us:** layout, backgrounds, type, tags, arcs and glow.

## Known limits (read before uploading)
- These are composites, not raw device screenshots. Apple expects screenshots that show the app in use; the layouts keep real UI and gameplay front and centre, but a few fresh **native-resolution iPhone captures** (controller screen, a mid-rally TV frame) would be stronger.
- The controller shown is the shipped marketing artwork, not a live capture; the live capture was stuck on "Connecting to the course".
- Phone UI captures are 402x874 and the clip frames are 960x540, upscaled (Lanczos + sharpen). Native captures will look crisper.
- No Locker, Emotes or customization screen was available as a current capture, so none is shown.
- **Set C's players are illustrated vector characters** drawn in-house (`tools/characters.py`) in the spirit of the in-game cast (round bald heads, game outfit colours). They are not renders of the game's 3D models. Polished AI-generated versions of the real characters were blocked by this environment's network policy (Higgsfield upload and result hosts), so that remains open.
- **Type** follows the in-game HUD: **Rubik** ExtraBold/Black, the face the game's HUD and moments like "SMASH!" use (`UiKit.cs`, `TennisJuice.cs`). Set C also copies the game's dark-navy outline treatment. Bricolage (the menu-title face) is intentionally not used for headlines.
- Copy never states how many sports the game has.
