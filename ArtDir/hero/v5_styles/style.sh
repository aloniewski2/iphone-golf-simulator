#!/bin/zsh
# one style: image-to-model -> segment -> complete; skips steps whose output exists (waits for an in-flight model)
cd "$(dirname "$0")"; s=$1
until ls $s/model/tripo-out/*/model.glb >/dev/null 2>&1; do
  if ! pgrep -f "name m-$s" >/dev/null; then
    img=$(ls $s/tripo-out/*/generated_image.png)
    tripo generate image-to-model $img --name m-$s -o $s/model --yes --no-open -p face_limit=60000 -p texture_quality=detailed > $s/model.json 2> $s/model.err
  else sleep 20; fi
done
g=$(ls $s/model/tripo-out/*/model.glb)
ls $s/seg/tripo-out/*/model.glb >/dev/null 2>&1 || tripo mesh segment $g --name seg-$s -o $s/seg --yes --no-open > $s/seg.json 2> $s/seg.err
ls $s/comp/tripo-out/*/model.glb >/dev/null 2>&1 || tripo mesh complete @seg-$s --completion-mode ai_completion --name comp-$s -o $s/comp --yes --no-open > $s/comp.json 2> $s/comp.err
echo "DONE $s $(ls $s/comp/tripo-out/*/model.glb 2>/dev/null)"
