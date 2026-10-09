#!/usr/bin/env bash
# Pulls the source art from the final-build branch into appstore/src (git-ignored).
# Needs git-lfs. Run from the repo root:  bash appstore/tools/fetch_assets.sh
set -euo pipefail
ROOT="$(git rev-parse --show-toplevel)"
WT="${TMPDIR:-/tmp}/motionclub-final-build"
git -C "$ROOT" fetch origin final-build
git -C "$ROOT" worktree add --no-checkout "$WT" origin/final-build 2>/dev/null || true
cd "$WT"
git sparse-checkout init --cone
git sparse-checkout set GolfArcade/Unity/MenuArt GolfArcade/Unity/Fonts proof/menu-beta/menu-proof \
  work/postcard-refresh ArtDir/review/post-match
git lfs pull --include="GolfArcade/Unity/MenuArt/*,GolfArcade/Unity/Fonts/*,proof/menu-beta/menu-proof/*,work/postcard-refresh/release-gates/*,work/postcard-refresh/tennis-release/*,ArtDir/review/post-match/*"
A="$ROOT/appstore/src"; mkdir -p "$A"/{art,brand,current,fonts,frames,phone}
cp GolfArcade/Unity/MenuArt/map-*.jpg "$A/art/"
cp GolfArcade/Unity/MenuArt/club-{crest,golf,tennis,trophy}.png "$A/brand/"
cp GolfArcade/Unity/Fonts/{Bricolage,Rubik}.ttf "$A/fonts/"
cp proof/menu-beta/menu-proof/*.png proof/menu-beta/menu-proof/feedback-composer.jpg "$A/phone/"
cp GolfArcade/Unity/MenuArt/golf-controller-artwork.png ArtDir/review/post-match/phone-finish-actions.png "$A/current/"
cp work/postcard-refresh/tennis-release/resort_1_gameplay.png "$A/current/tennis_resort.png"
for h in 07 08 09 10 12 13 14 15 16 17 18 19 20 21 22 23; do for p in tee approach; do
  cp work/postcard-refresh/release-gates/hole$h-$p.png "$A/current/golf_h$h-$p.png"; done; done
# bundled gameplay clips -> still frames (960x540)
for v in gameplay-golf-cliffside:cliff:1 gameplay-tennis-skyscraper:sky:0.5; do
  IFS=: read -r f n r <<<"$v"; ffmpeg -v error -y -i "GolfArcade/Unity/MenuArt/$f.mp4" -vf fps=$r "$A/frames/${n}_%02d.png"; done
echo "assets ready in $A; now: python3 appstore/tools/prep_upscale.py && python3 appstore/tools/build_icons.py"
