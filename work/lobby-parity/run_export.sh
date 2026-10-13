#!/bin/zsh
# Re-export the native hero assets from the CURRENT Unity project (APFS clone) with the game's default look flags.
S=/private/tmp/claude-501/-Users-adnanyonathan-Documents-Codex-2026-09-20-wh-outputs-iphone-golf-simulator/451be1c4-979a-4a9d-8c04-3ec2c2839d31/scratchpad
R=/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/work/lobby-parity
U=/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/MacOS/Unity
rm -f $R/logs/status.txt
for job in Run RunDistance RunGolf; do
  MH_LOCKER_FACE_PARITY=0 MH_LOCKER_OUT=$R/export MH_LOCKER_GOLDEN=$R/golden MH_LOCKER_REPORT=$R/logs/$job-report.txt $U -batchmode -nographics -quit -projectPath $S/UnityClone -executeMethod GolfArcade.EditorTools.MatchHeroLockerExport.$job -logFile $R/logs/$job.log
  echo "$job exit $?" >> $R/logs/status.txt
done
