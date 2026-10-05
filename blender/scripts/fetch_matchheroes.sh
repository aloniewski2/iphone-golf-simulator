#!/bin/bash
# Downloads Adnan's match-hero sources (the rig, body, face, kit, hair, and the clips the golfer borrows: Walk, RunForward, the emotes, the intros) (Git LFS, branch newmapsandmenus) into blender/matchhero/ for blender/scripts/matchhero_golf.py.
# git-lfs is not needed: each file is fetched from GitHub's media URL with the gh login and checked against the sha256 in its LFS pointer.
#   blender/scripts/fetch_matchheroes.sh [branch]
set -e
BR=${1:-newmapsandmenus}
REPO=$(cd "$(dirname "$0")/../.." && pwd)
OWNER_REPO=aloniewski2/iphone-golf-simulator
DEST=$REPO/blender/matchhero
mkdir -p "$DEST"
git -C "$REPO" fetch origin "$BR" --quiet
A=Unity/Assets/Characters
for p in $A/MatchHeroes/Male/Male_ReadyIdle.fbx $A/MatchHeroes/Female/Female_ReadyIdle.fbx \
         $A/MatchHeroKit/Male_Kit.fbx $A/MatchHeroKit/Female_Kit.fbx \
         $A/HeroBase/Models/Hair_Default_Male.fbx $A/HeroBase/Models/Hair_Default_Female.fbx \
         $(for sex in Male Female; do for c in Walk RunForward Emote_Scuba Emote_Spike Emote_Thrust Intro_BringIt Intro_Pushups Intro_Wave; do echo $A/MatchHeroes/$sex/${sex}_$c.fbx; done; done); do
  OID=$(git -C "$REPO" show "origin/$BR:$p" | sed -n 's/^oid sha256://p')
  [ -n "$OID" ] || { echo "$p is not an LFS pointer on $BR"; exit 1; }
  OUT=$DEST/$(basename "$p")
  curl -sfL -H "Authorization: token $(gh auth token)" -o "$OUT" "https://media.githubusercontent.com/media/$OWNER_REPO/$BR/$p"
  [ "$(shasum -a 256 "$OUT" | cut -d' ' -f1)" = "$OID" ] && echo "ok $OUT" || { echo "checksum mismatch: $OUT"; exit 1; }
done
