#!/bin/zsh
# sync_clone.sh : copy the one Unity file this job edits into the APFS clone the batch runs use.
CLONE=${LM_CLONE:-/private/tmp/claude-501/-Users-adnanyonathan-Documents-Codex-2026-09-20-wh-outputs-iphone-golf-simulator/b8c5974c-0658-48af-9507-14588d72a05b/scratchpad/UnityClone}
cd "$(dirname "$0")/../../.." || exit 1
cp Unity/Assets/Editor/MatchHeroLockerExport.cs "$CLONE/Assets/Editor/MatchHeroLockerExport.cs" && echo "synced MatchHeroLockerExport.cs"
