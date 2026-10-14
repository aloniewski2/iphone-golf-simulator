#!/bin/zsh
# ACCESSORIES: the review sheets (reference on top, ours below). Needs proof/final/* (final_proof.sh) and proof/cap_alone (cap_export product + acc_preview).
set -e
H=${0:A:h}; cd $H; REF=$H/../../ArtDir/hero/targets-2026-10-13/accessories; S=$H/proof/sheets; F=$H/proof/final; CA=$H/proof/cap_alone; mkdir -p $S
G=$F/golf; T=$F/tennis
# --- on the heroes
python3 py/make_sheet.py $S/tennis_cap_on_heroes.png $REF/05_fit_tennis_cap.jpg --title "Tennis cap on both heroes (Unity, game shaders)" \
  --row "Male: front / 3-4 / side / close-up|$T/MatchHero_Male_front.png,$T/MatchHero_Male_q34.png,$T/MatchHero_Male_side.png,$T/MatchHero_Male_head_q34.png" \
  --row "Female: front / 3-4 / side / close-up|$T/MatchHero_Female_front.png,$T/MatchHero_Female_q34.png,$T/MatchHero_Female_side.png,$T/MatchHero_Female_head_q34.png"
python3 py/make_sheet.py $S/golf_cap_on_heroes.png $REF/06_fit_golf_cap_glove_bag.jpg --title "Golf cap on both heroes (Unity, game shaders)" \
  --row "Male: front / 3-4 / side / close-up|$G/GolfKitHero_Male_bust_front.png,$G/GolfKitHero_Male_bust_q34.png,$G/GolfKitHero_Male_bust_side.png,$G/GolfKitHero_Male_head_q34.png" \
  --row "Female: front / 3-4 / side / close-up|$G/GolfKitHero_Female_bust_front.png,$G/GolfKitHero_Female_bust_q34.png,$G/GolfKitHero_Female_bust_side.png,$G/GolfKitHero_Female_head_q34.png"
python3 py/make_sheet.py $S/golf_glove_on_heroes.png $REF/06_fit_golf_cap_glove_bag.jpg --title "Left golf glove on both heroes (Unity, game shaders)" \
  --row "Male: front / 3-4 / hand (inside) / hand (outside)|$G/GolfKitHero_Male_front.png,$G/GolfKitHero_Male_q34.png,$G/GolfKitHero_Male_hand_in.png,$G/GolfKitHero_Male_hand_out.png" \
  --row "Female: front / 3-4 / hand (inside) / hand (outside)|$G/GolfKitHero_Female_front.png,$G/GolfKitHero_Female_q34.png,$G/GolfKitHero_Female_hand_in.png,$G/GolfKitHero_Female_hand_out.png"
python3 py/make_sheet.py $S/golf_bag_on_heroes.png $REF/06_fit_golf_cap_glove_bag.jpg --title "Golf carry bag on both heroes (Unity, game shaders; plaza / between shots)" \
  --row "Male: back 3-4 / side / back close-up / front|$G/GolfKitHero_Male_back34.png,$G/GolfKitHero_Male_side.png,$G/GolfKitHero_Male_bag_back.png,$G/GolfKitHero_Male_front.png" \
  --row "Female: back 3-4 / side / back close-up / front|$G/GolfKitHero_Female_back34.png,$G/GolfKitHero_Female_side.png,$G/GolfKitHero_Female_bag_back.png,$G/GolfKitHero_Female_front.png"
# --- each item alone (Blender product renders for the caps; Unity renders for all four, game shaders)
python3 py/make_sheet.py $S/tennis_cap_alone.png $REF/01_tennis_cap_product.jpg --title "Tennis cap alone (Blender product, maps on / Unity, game shaders)" \
  --row "Blender: front / side / back / 3-4|$CA/Product_tennis_cap_front.png,$CA/Product_tennis_cap_side.png,$CA/Product_tennis_cap_back.png,$CA/Product_tennis_cap_34.png" \
  --row "Blender: top / underside / brim / strap + buckle|$CA/Product_tennis_cap_top.png,$CA/Product_tennis_cap_under.png,$CA/Product_tennis_cap_34_brim.png,$CA/Product_tennis_cap_back34_strap.png" \
  --row "Unity: front / side / back / 3-4|$F/alone_tennis_cap/MatchHero_Male_alone_front.png,$F/alone_tennis_cap/MatchHero_Male_alone_side.png,$F/alone_tennis_cap/MatchHero_Male_alone_back.png,$F/alone_tennis_cap/MatchHero_Male_alone_q34.png"
python3 py/make_sheet.py $S/golf_cap_alone.png $REF/02_golf_cap_product.jpg --title "Golf cap alone (Blender product, maps on / Unity, game shaders)" \
  --row "Blender: front / side / back / 3-4|$CA/Product_golf_cap_front.png,$CA/Product_golf_cap_side.png,$CA/Product_golf_cap_back.png,$CA/Product_golf_cap_34.png" \
  --row "Blender: top / underside / brim + patch / strap holes|$CA/Product_golf_cap_top.png,$CA/Product_golf_cap_under.png,$CA/Product_golf_cap_34_brim.png,$CA/Product_golf_cap_back34_strap.png" \
  --row "Unity: front / side / back / 3-4|$F/alone_golf_cap/GolfKitHero_Male_alone_front.png,$F/alone_golf_cap/GolfKitHero_Male_alone_side.png,$F/alone_golf_cap/GolfKitHero_Male_alone_back.png,$F/alone_golf_cap/GolfKitHero_Male_alone_q34.png"
python3 py/make_sheet.py $S/golf_glove_alone_unity.png $REF/03_golf_glove_product.jpg --title "Left golf glove alone (Unity, game shaders; the Blender product sheet is proof/glove/glove_product_vs_reference.png)" \
  --row "Unity: front / side / back / 3-4|$F/alone_golf_glove/GolfKitHero_Male_alone_front.png,$F/alone_golf_glove/GolfKitHero_Male_alone_side.png,$F/alone_golf_glove/GolfKitHero_Male_alone_back.png,$F/alone_golf_glove/GolfKitHero_Male_alone_q34.png" \
  --row "Unity: top / underside|$F/alone_golf_glove/GolfKitHero_Male_alone_top.png,$F/alone_golf_glove/GolfKitHero_Male_alone_under.png"
python3 py/make_sheet.py $S/golf_bag_alone_unity.png $REF/04_golf_bag_product.jpg --title "Golf carry bag alone (Unity, game shaders, carried state: legs folded, one strap; the Blender product sheet is proof/bag/product_vs_reference.png)" \
  --row "Unity: front / side / back / 3-4|$F/alone_golf_bag/GolfKitHero_Male_alone_front.png,$F/alone_golf_bag/GolfKitHero_Male_alone_side.png,$F/alone_golf_bag/GolfKitHero_Male_alone_back.png,$F/alone_golf_bag/GolfKitHero_Male_alone_q34.png" \
  --row "Unity: top / underside|$F/alone_golf_bag/GolfKitHero_Male_alone_top.png,$F/alone_golf_bag/GolfKitHero_Male_alone_under.png"
ls -la $S
