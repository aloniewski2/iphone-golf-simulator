#!/bin/zsh
cd "$(dirname "$0")"
for s in ponytail bob long curly; do
  img=$(ls $s/tripo-out/*/generated_image.png)
  tripo generate image-to-model $img --name m-$s -o $s/model --yes --no-open -p face_limit=60000 -p texture_quality=detailed > $s/model.json 2> $s/model.err; tail -1 $s/model.err
  g=$(ls $s/model/tripo-out/*/model.glb)
  tripo mesh segment $g --name seg-$s -o $s/seg --yes --no-open > $s/seg.json 2> $s/seg.err; tail -1 $s/seg.err
  tripo mesh complete @seg-$s --completion-mode ai_completion --name comp-$s -o $s/comp --yes --no-open > $s/comp.json 2> $s/comp.err; tail -1 $s/comp.err
done
tripo balance
echo STYLES_DONE
