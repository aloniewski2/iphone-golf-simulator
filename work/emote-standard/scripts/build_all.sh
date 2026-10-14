#!/bin/zsh
# build_all.sh <male|female> : every clip of the standard set on one hero (tennis + Free variants), then the FBX.  Logs in ../logs/build_<hero>.txt
B=/Applications/Blender.app/Contents/MacOS/Blender
H=$1; BL=../blend/$([ $H = male ] && echo Male || echo Female)_Std.blend
LOG=../logs/build_$H.txt; : > $LOG
EMOTES=(jumpjoy fistpump stomp slump shock victory defeat)
for c in $EMOTES; do
  for p in racket none; do
    echo "== $c $p" >> $LOG
    $B -b $BL --python build.py -- $H clip_$c $p 2>&1 | grep -E "REPLAY|INFO|MIN_BODY|BUILD_DONE|Error|Traceback|line [0-9]" >> $LOG
  done
done
for c in walk run; do
  echo "== $c none" >> $LOG
  $B -b $BL --python build.py -- $H clip_$c none 2>&1 | grep -E "REPLAY|INFO|MIN_BODY|BUILD_DONE|Error|Traceback|line [0-9]" >> $LOG
done
ACTS=(Emote_JumpJoy Emote_JumpJoy_Free Emote_FistPump Emote_FistPump_Free Emote_Stomp Emote_Stomp_Free Emote_Slump Emote_Slump_Free Emote_Shock Emote_Shock_Free
      Emote_Victory Emote_Victory_Free Emote_Defeat Emote_Defeat_Free Walk_Free Run_Free)
$B -b $BL --python export_fbx.py -- $H $ACTS 2>&1 | grep -E "^FBX |Error|Traceback" >> $LOG
grep -c BUILD_DONE $LOG; grep -c "^FBX " $LOG; grep -E "Error|Traceback" $LOG | head
