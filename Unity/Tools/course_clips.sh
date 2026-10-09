#!/bin/bash
# The app's golf course picker clips: BrollCaptureTests.CaptureCourseOrbits films each course's
# holes from the old COURSE screen (Library/Captures/broll/course-<key>/<hole>/f####.jpg, 8 s a
# hole); this joins them with crossfades, dipping through black at the loop, into
# GolfArcade/Unity/MenuArt/course-golf-<key>.mp4 with a poster frame (.jpg) beside it.
#   Unity/Tools/course_clips.sh
set -e
cd "$(dirname "$0")/.."
IN=Library/Captures/broll; OUT=../GolfArcade/Unity/MenuArt
clip() {
  local key=$1; shift
  local args=() n=0
  for hole in "$@"; do args+=(-framerate 30 -i "$IN/course-$key/$hole/f%04d.jpg"); n=$((n+1)); done
  # 8 s a hole, 0.6 s crossfades
  local graph="[0][1]xfade=transition=fade:duration=0.6:offset=7.4[a];[a][2]xfade=transition=fade:duration=0.6:offset=14.8,fade=t=in:st=0:d=0.4,fade=t=out:st=22.4:d=0.4,format=yuv420p[v]"
  ffmpeg -loglevel error -y "${args[@]}" -filter_complex "$graph" -map "[v]" -c:v libx264 -preset slow -crf 25 -movflags +faststart -an "$OUT/course-golf-$key.mp4"
  ffmpeg -loglevel error -y -i "$IN/course-$key/$1/f0060.jpg" -q:v 3 "$OUT/course-golf-$key.jpg"
  echo "$OUT/course-golf-$key.mp4 $(du -h "$OUT/course-golf-$key.mp4" | cut -f1)"
}
clip cliffside 12 13 7
clip postcards 8 9 10
clip wildisles 17 19 20
clip magma 21 22 23
