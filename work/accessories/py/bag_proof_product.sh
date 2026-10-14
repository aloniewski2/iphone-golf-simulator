#!/bin/sh
# BAG: product-alone proof renders (maps on) -> proof/bag/product/ ; run from work/accessories
BL=/Applications/Blender.app/Contents/MacOS/Blender
OUT=proof/bag/product
mkdir -p $OUT
r() { $BL -b --factory-startup --python py/bag_render.py -- "$@" 2>&1 | grep -i "error\|Traceback" || true; }
r out/bag_product_open $OUT po front,side,34,back34 900x1100
r out/bag_product_folded $OUT pf side,back,top,34,back34,front 900x1100
# close-ups
r out/bag_product_folded $OUT cf front:pocket,34:side_pocket,34:covers,top:rim,back34:rim,back:legs,back34:strap,side:buckle,34:base,back:hub,back34:handle 900x1000
r out/bag_product_open $OUT co back34:legs,side:legs 900x1000
