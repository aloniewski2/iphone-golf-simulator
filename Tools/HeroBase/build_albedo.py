"""Deterministic pigment-only head atlases. No source photograph or baked illumination.

Male (2026-10-02, skin liveliness): a NEUTRAL, TINTABLE base. The atlas is white with a small RELATIVE flush (cheeks, ears: green/blue scaled by a few percent
after the tint multiplies in), so the player's SkinTone (GolferStyle.SkinTones, set as the material's _BaseColor) carries the colour and every tone gets the
same proportional warmth. It no longer bakes one RGB as 'the' skin and no longer paints the plate's grey collar (the body is skin; clothing is a later layer).
Use with a material whose _BaseColor = SkinTones[i]. No stubble, no pores.
NOTE: the current male Body_M mesh has no UV set, so the active prefab (Male_Form_Skin, flat tone-driven material) does not sample this atlas; it is kept in
step for UV-mapped heads and the legacy Skin_M path.

Female: unchanged legacy atlas (pigment + blush + grey collar baked from plate_profiles.FEMALE). Run with 'Female' to regenerate it; default is Male only.
usage: python3 build_albedo.py [Male] [Female]"""
import sys
from pathlib import Path
import numpy as np
from PIL import Image
from plate_profiles import MALE,FEMALE
P=Path(__file__).resolve().parents[2]/'ArtDir/hero/base_lock/unity_import/Textures';P.mkdir(parents=True,exist_ok=True)
N=2048;u=(np.arange(N)+.5)/N;v=(np.arange(N)+.5)/N;ang=(u-.5)*np.pi*2;x=48*np.sin(ang);y=24+v*158
sexes=[s for s in sys.argv[1:] if s in ('Male','Female')] or ['Male']
for sex,profile in [('Male',MALE),('Female',FEMALE)]:
 if sex not in sexes: continue
 Y=y[:,None];X=x[None,:];front=np.exp(-(ang[None,:]/1.0)**8)
 if sex=='Male':
  # relative flush mask in [0,1]: cheeks (front) and the ear rims (sides); the multiplier below is applied AFTER the material tint.
  cheek=(np.exp(-((X-27)/12)**2-((Y-96)/9)**2)+np.exp(-((X+27)/12)**2-((Y-96)/9)**2))*front
  ear=np.exp(-((np.abs(X)-44)/7)**2-((Y-113)/10)**2)*(1-front)
  flush=np.clip(.9*cheek+.7*ear,0,1)
  a=np.ones((N,N,3),np.float32)
  a[:,:,1]*=1-.035*flush       # green  -3.5 % at full flush
  a[:,:,2]*=1-.045*flush       # blue   -4.5 % at full flush  -> redder, proportionally, for every tone
 else:
  color=profile['skin']
  a=np.broadcast_to(np.array(color,dtype=np.float32),(N,N,3)).copy()
  blush=(np.exp(-((X-27)/12)**2-((Y-96)/9)**2)+np.exp(-((X+27)/12)**2-((Y-96)/9)**2))*front
  a+=blush[:,:,None]*np.array([.020,-.004,-.002])
  collar=135.5*front+135*(1-front)
  grey=np.clip((Y-collar+.6)/1.2,0,1)
  a=a*(1-grey[:,:,None])+np.array(profile['grey'])*grey[:,:,None]
 Image.fromarray(np.uint8(np.clip(a,0,1)*255)).save(P/('Head_'+sex+'_Albedo.png'))
