from pathlib import Path
import json,sys
import numpy as np
from PIL import Image,ImageDraw,ImageFont
from plate_profiles import MALE,FEMALE
R=Path(__file__).resolve().parents[2];D=R/'ArtDir/hero/base_lock';P=D/'proof'
font=ImageFont.truetype('/System/Library/Fonts/Helvetica.ttc',18)
reports=[]
for sex,suf,pr in [('male','M',MALE),('female','F',FEMALE)]:
    plate=Image.open(D/(sex+'_body_plain.jpg')).convert('RGB');panels=[];matches=[];metrics={}
    for view in ['front','back']:
        target=Image.new('L',(1280,720));ImageDraw.Draw(target).polygon(pr[view+'_outline'],fill=255);target.save(P/'masks'/(suf+'_'+view+'_target.png'))
        render=Image.open(P/'blender'/(suf+'_'+view+'.png')).convert('RGBA');cx=pr['cx' if view=='front' else 'back_cx'];aligned=Image.new('RGBA',plate.size);aligned.paste(render,(cx-640,0))
        a=np.array(aligned.getchannel('A'))>127;b=np.array(target)>0;union=a|b;xor=a^b;err=xor.sum()/union.sum();metrics[view]={'symmetric_difference_over_union':float(err),'intersection_over_union':float((a&b).sum()/union.sum()),'target_pixels':int(b.sum()),'model_pixels':int(a.sum()),'render_translation_px':[cx-640,0]}
        overlay=np.asarray(plate).copy();overlay[a&b]=(overlay[a&b]*.62+np.array([75,190,70])*.38).astype('uint8');overlay[b&~a]=[225,60,55];overlay[a&~b]=[45,135,250];im=Image.fromarray(overlay)
        draw=ImageDraw.Draw(im);draw.rectangle((0,685,1280,720),fill=(27,27,30));draw.text((16,692),f'{sex} {view} — silhouette difference {err*100:.2f}% | green overlap · red missing · blue excess',font=font,fill='white');panels.append(im)
        comp=plate.convert('RGBA');comp.alpha_composite(aligned);matches.append(comp.convert('RGB'))
    out=Image.new('RGB',(1280,1440));out.paste(panels[0],(0,0));out.paste(panels[1],(0,720));out.save(P/('01_silhouette_overlay_'+suf.lower()+'.png'))
    out=Image.new('RGB',(1280,1440));out.paste(matches[0],(0,0));out.paste(matches[1],(0,720));out.save(P/('02_match_'+suf.lower()+'.png'))
    reports.append({'sex':sex,'metrics':metrics})
(P/'silhouette-metrics.json').write_text(json.dumps({'method':'Independent manual polygon tracing of unmodified front/back plates. Orthographic renders at frozen metre/pixel scale, translated to measured source x center; no scaling/warping or threshold fitting of proof. Target boundary precision estimated ±2px. Symmetric difference over union; alpha threshold 127.','measurements':reports},indent=2));print(json.dumps(reports,indent=2))
