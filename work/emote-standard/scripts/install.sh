#!/bin/zsh
# install.sh : EMOTE STANDARD into the real project (staged, never committed). Run only after the clone films passed.
#   1. refuses if the hero prefabs moved since the install set was merged (BASE_PREFAB_SHA256.txt)
#   2. keeps the originals in rollback/files (once), copies install/Unity/** over Unity/ (FBX + metas, HeroMoves, prefabs + 7 slots, 2 new scripts)
#   3. patch_unity.py (C#) and patch_swift.py (EmoteCatalog + locker focus rows)
#   4. writes INSTALLED_SHA256SUMS.txt.   Undo: rollback.sh
set -e
R=$(cd "$(dirname "$0")/../../.." && pwd); W=$R/work/emote-standard; S=$W/install
cd $R
shasum -a 256 -c $S/BASE_PREFAB_SHA256.txt >/dev/null || { echo "ABORT: a hero prefab changed since the merge - re-merge the slots"; exit 1; }
python3 $W/scripts/patch_unity.py $R/Unity --check && python3 $W/scripts/patch_swift.py $R --check
mkdir -p $W/rollback/files
for s in Male Female; do f=Unity/Assets/Resources/Tennis/Customization/Player$s.prefab; [[ -f $W/rollback/files/${f//\//__} ]] || cp $f $W/rollback/files/${f//\//__}; done
( cd $S && find Unity -type f ) | sort > $W/rollback/NEW_OR_REPLACED.txt
( cd $S && find Unity -type f ) | while read f; do mkdir -p $(dirname $R/$f); cp $S/$f $R/$f; done
python3 $W/scripts/patch_unity.py $R/Unity
python3 $W/scripts/patch_swift.py $R
( cd $R && cat $W/rollback/NEW_OR_REPLACED.txt | xargs shasum -a 256; shasum -a 256 Unity/Assets/Scripts/Tennis/HeroTennisDriver.cs Unity/Assets/Scripts/Tennis/TennisEmotes.cs \
  Unity/Assets/Scripts/Tennis/MatchHeroFacePerformance.cs Unity/Assets/Scripts/Hub/HubHeroAnimator.cs Unity/Assets/Scripts/Game/GolferView.cs \
  Unity/Assets/Scripts/Game/GolfGame.HoleOut.cs Unity/Assets/Editor/MatchHeroLockerExport.cs GolfArcade/Domain/Player.swift GolfArcade/Unity/TennisMenu.swift ) > $W/INSTALLED_SHA256SUMS.txt
echo "INSTALLED $(wc -l < $W/INSTALLED_SHA256SUMS.txt) files"
