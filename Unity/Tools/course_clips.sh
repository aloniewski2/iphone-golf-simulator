#!/bin/bash
# The app's golf course picker clips: BrollCaptureTests.CaptureCourseOrbits films every hole of each
# course from the old COURSE screen (Library/Captures/broll/course-<key>/<hole>/f####.jpg, 5 s a hole);
# this joins them with crossfades, dipping through black at the loop, into
# GolfArcade/Unity/MenuArt/course-golf-<key>.mp4 with a poster frame (.jpg) beside it.
#   Unity/Tools/course_clips.sh            (FFMPEG=/path/to/ffmpeg to use another build)
set -e
cd "$(dirname "$0")/.."
IN=Library/Captures/broll; OUT=../GolfArcade/Unity/MenuArt
FF=${FFMPEG:-ffmpeg}
HOLD=5; XFADE=0.5   # seconds a hole, crossfade
clip() {
  local key=$1; shift
  local args=() n=$# graph="" prev="[0]" i
  for hole in "$@"; do args+=(-framerate 30 -t $HOLD -i "$IN/course-$key/$hole/f%04d.jpg"); done
  for ((i = 1; i < n; i++)); do
    graph+="${prev}[$i]xfade=transition=fade:duration=$XFADE:offset=$(awk "BEGIN{print $i*($HOLD-$XFADE)}")[x$i];"; prev="[x$i]"
  done
  local end; end=$(awk "BEGIN{print $HOLD+($n-1)*($HOLD-$XFADE)-0.4}")
  graph+="${prev}fade=t=in:st=0:d=0.4,fade=t=out:st=$end:d=0.4,format=yuv420p[v]"
  "$FF" -loglevel error -y "${args[@]}" -filter_complex "$graph" -map "[v]" -c:v libx264 -preset slow -crf 25 -movflags +faststart -an "$OUT/course-golf-$key.mp4"
  "$FF" -loglevel error -y -i "$IN/course-$key/$1/f0060.jpg" -q:v 3 "$OUT/course-golf-$key.jpg"
  echo "$OUT/course-golf-$key.mp4 $(du -h "$OUT/course-golf-$key.mp4" | cut -f1)"
}
# = Course.Cliffside() / WildIsles() / Magma() in Unity/Assets/Scripts/Course/Hole.cs
clip cliffside 7 8 9 12 15
clip wildisles 13 14 17 18 19 20
clip magma 10 16 21 22 23
