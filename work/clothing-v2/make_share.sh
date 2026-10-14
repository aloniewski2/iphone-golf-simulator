#!/bin/zsh
# one zip per hero: blends + FBX of every V2 garment, Unity mesh assets + maps, lobby copies   (work/clothing-v2/make_share.sh)
set -e
cd ${0:A:h}
R=${0:A:h:h:h}; B=/Applications/Blender.app/Contents/MacOS/Blender
typeset -A SRC
SRC[MatchHero_Male]="polo/polo_v2_final.blend shorts/shorts_v2_final.blend shoes/MatchHero_Male_shoe_L_final.blend shoes/MatchHero_Male_shoe_R_final.blend shoes/MatchHero_Male_sock_L_final.blend shoes/MatchHero_Male_sock_R_final.blend"
SRC[MatchHero_Female]="tank/female_tank_final.blend shorts/female_bottom_final.blend shoes/MatchHero_Female_shoe_L_final.blend shoes/MatchHero_Female_shoe_R_final.blend shoes/MatchHero_Female_sock_L_final.blend shoes/MatchHero_Female_sock_R_final.blend"
SRC[GolfKitHero_Male]="polo/GolfKitHero_Male/polo_v2_final.blend shorts/GolfKitHero_Male_golf_male_trousers_final.blend golf/GolfKitHero_Male_belt_final.blend golf/GolfKitHero_Male_glove_L_final.blend golf/GolfKitHero_Male_glove_R_final.blend shoes/GolfKitHero_Male_shoe_L_final.blend shoes/GolfKitHero_Male_shoe_R_final.blend"
SRC[GolfKitHero_Female]="polo/GolfKitHero_Female/polo_v2_final.blend shorts/GolfKitHero_Female_skort_final.blend golf/GolfKitHero_Female_belt_final.blend golf/GolfKitHero_Female_glove_L_final.blend golf/GolfKitHero_Female_glove_R_final.blend shoes/GolfKitHero_Female_shoe_L_final.blend shoes/GolfKitHero_Female_shoe_R_final.blend"
for H in MatchHero_Male MatchHero_Female GolfKitHero_Male GolfKitHero_Female; do
  D=share/tmp/$H; rm -rf $D; mkdir -p $D/blender $D/fbx $D/unity $D/textures $D/app_lobby
  for f in ${=SRC[$H]}; do n=${f:t:r}; cp $f $D/blender/${H}_${n}.blend; $B -b $f --python py/fbx_one.py -- $D/fbx/${H}_${n}.fbx > /dev/null 2>&1; done
  cp $R/Unity/Assets/Characters/KitsV2/$H/*.asset $D/unity/
  sport=Tennis; [[ $H == Golf* ]] && sport=Golf; sex=Male; [[ $H == *Female ]] && sex=Female
  cp $R/Unity/Assets/Resources/$sport/KitsV2/$sex/*.png $R/Unity/Assets/Resources/$sport/KitsV2/$sex/*.bytes $D/textures/ 2>/dev/null || true
  cp $R/Unity/Assets/Resources/$sport/KitsV2/$sex/*.json $D/textures/ 2>/dev/null || true
  cp $R/Unity/Assets/Resources/Tennis/KitsV2/KitV2_FabricAtlas.png $D/textures/
  cp $R/GolfArcade/Unity/CharacterAssets/MatchHero_KitV2_${sport}_${sex}_* $R/GolfArcade/Unity/CharacterAssets/MatchHero_KitV2_FabricAtlas.* $D/app_lobby/ 2>/dev/null || true
  cat > $D/README.txt <<TXT
$H clothing V2 (2026-10-13). Every garment is its own product: blender/ = built + game meshes (metres, Z up, hero faces -Y), fbx/ = the same per piece,
unity/ = mesh assets (KitV2 <piece>), textures/ = N / M maps (+ skin cover, contact shadow, pose correctives), app_lobby/ = SceneKit copies (.ktx).
Colours are customisable (role materials Kit_Shirt, Kit_Shorts, Kit_ShortsBand, Kit_Shoe, Kit_Sole, Kit_Sock, Kit_Belt, Kit_Buckle, Kit_Glove).
TXT
  (cd share/tmp && rm -f ../${H}_V2.zip && zip -qr ../${H}_V2.zip $H)
  echo "$H: $(du -h share/${H}_V2.zip | cut -f1)"
done
rm -rf share/tmp
