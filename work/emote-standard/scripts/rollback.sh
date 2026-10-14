#!/bin/zsh
# rollback.sh : undo install.sh exactly: the edited sources come back from rollback/code, the prefabs from rollback/files, the new files are removed.
set -e
R=$(cd "$(dirname "$0")/../../.." && pwd); W=$R/work/emote-standard
for f in $W/rollback/code/*; do rel=${${f:t}//__//}; [[ -f $R/Unity/$rel ]] && cp $f $R/Unity/$rel || cp $f $R/$rel; done
for f in $W/rollback/files/*; do cp $f $R/${${f:t}//__//}; done
while read f; do [[ $f == *Player*.prefab ]] && continue; rm -f $R/$f; done < $W/rollback/NEW_OR_REPLACED.txt
rmdir $R/Unity/Assets/Resources/HeroMoves/Male $R/Unity/Assets/Resources/HeroMoves/Female $R/Unity/Assets/Resources/HeroMoves 2>/dev/null || true
echo "ROLLED BACK"
