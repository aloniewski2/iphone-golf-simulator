#!/bin/zsh
# The demo reel's frames and sound (Library/Captures/demo, from Golf Arcade → Record Demo Video)
# into an MP4 at phone resolution: H.264 30 fps, AAC. Under the game's own sound goes a quiet bed
# of sea and breeze (brown noise, low-passed, swelling like waves), so the quiet stretches are the
# island rather than silence; half a second's fade in and out.
# Usage: Tools/demo_video.sh [out.mp4]
cd "$(dirname "$0")/.."
in=Library/Captures/demo
out=${1:-$in/golf-arcade-demo.mp4}
[[ -f $in/f_00000.jpg ]] || { echo "no frames in $in: record them first (Golf Arcade → Record Demo Video)"; exit 1; }
frames=$(ls $in/f_*.jpg | wc -l | tr -d ' ')
secs=$(echo "scale=3; $frames / 30" | bc)
end=$(echo "scale=3; $secs - 0.8" | bc)
if [[ -f $in/sound.wav ]]; then
  ffmpeg -v error -y -framerate 30 -i $in/f_%05d.jpg -i $in/sound.wav \
    -f lavfi -t $secs -i "anoisesrc=color=brown:amplitude=0.6:sample_rate=48000:seed=12" \
    -f lavfi -t $secs -i "anoisesrc=color=pink:amplitude=0.25:sample_rate=48000:seed=7" \
    -filter_complex "[0:v]fade=t=in:d=0.5,fade=t=out:st=$end:d=0.8[v];\
[1:a]aresample=48000,aformat=channel_layouts=stereo[game];\
[2:a]lowpass=f=420,tremolo=f=0.12:d=0.6,volume=0.5,aformat=channel_layouts=stereo[sea];\
[3:a]highpass=f=900,lowpass=f=4000,tremolo=f=0.1:d=0.5,volume=0.05,aformat=channel_layouts=stereo[breeze];\
[game][sea][breeze]amix=inputs=3:duration=first:normalize=0,afade=t=in:d=0.5,afade=t=out:st=$end:d=0.8[a]" \
    -map "[v]" -map "[a]" -c:v libx264 -preset slow -crf 19 -pix_fmt yuv420p -c:a aac -b:a 160k -movflags +faststart -t $secs "$out"
else
  ffmpeg -v error -y -framerate 30 -i $in/f_%05d.jpg -vf "fade=t=in:d=0.5,fade=t=out:st=$end:d=0.8" \
    -c:v libx264 -preset slow -crf 19 -pix_fmt yuv420p -movflags +faststart "$out"
fi
ls -la "$out"
