#!/bin/sh
# BAG: fitted-on-hero proof renders (maps on) -> proof/bag/hero/ ; run from work/accessories
BL=/Applications/Blender.app/Contents/MacOS/Blender
for S in Male Female; do
  OUT=proof/bag/hero_$S; mkdir -p $OUT
  $BL -b --factory-startup --python py/bag_hero_render.py -- $S $OUT h front,34,side,back,back34,front34r,sider,top 900x1100 2>&1 | grep -i "error\|Traceback" || true
  $BL -b --factory-startup --python py/bag_hero_render.py -- $S $OUT hc front:shoulder,front34r:strap,back34:bag,back:head,side:full 900x1100 2>&1 | grep -i "error\|Traceback" || true
done
