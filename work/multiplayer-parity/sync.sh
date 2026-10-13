#!/bin/zsh
# repo scripts -> both clones (U2 is made from U on first use)
for D in /private/tmp/claude-501/-Users-adnanyonathan-Documents-Codex-2026-09-20-wh-outputs-iphone-golf-simulator/d0a599a3-5168-48ea-a405-d9dbd314ba83/scratchpad/U /private/tmp/claude-501/-Users-adnanyonathan-Documents-Codex-2026-09-20-wh-outputs-iphone-golf-simulator/d0a599a3-5168-48ea-a405-d9dbd314ba83/scratchpad/U2; do
  [ -d $D ] || cp -c -R /private/tmp/claude-501/-Users-adnanyonathan-Documents-Codex-2026-09-20-wh-outputs-iphone-golf-simulator/d0a599a3-5168-48ea-a405-d9dbd314ba83/scratchpad/U $D
  rsync -a /Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/Unity/Assets/Scripts/ $D/Assets/Scripts/
  rsync -a /private/tmp/claude-501/-Users-adnanyonathan-Documents-Codex-2026-09-20-wh-outputs-iphone-golf-simulator/d0a599a3-5168-48ea-a405-d9dbd314ba83/scratchpad/U/Assets/Tests/PlayMode/VersusPeerCapture.cs $D/Assets/Tests/PlayMode/ 2>/dev/null
  rm -rf $D/Temp
done
