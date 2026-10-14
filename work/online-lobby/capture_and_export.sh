#!/bin/zsh
set -e
export MH_EMOTE_FRAMES="$PWD/work/online-lobby/emote_parity/unity_frames"
/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath "$PWD/work/online-lobby/UnityClone" -executeMethod GolfArcade.EditorTools.MatchHeroLockerExport.CaptureEmoteParity -logFile "$PWD/work/online-lobby/emote_parity/unity-film.log"
/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -projectPath "$PWD/work/online-lobby/UnityClone" -buildTarget iOS -executeMethod OnlineLobbySimulatorBuild.Run -logFile "$PWD/work/online-lobby/simulator-export.log"
