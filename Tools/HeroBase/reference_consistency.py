"""Compare the frozen source traces without fitting a camera or changing a target."""
from pathlib import Path
import json
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from plate_profiles import MALE, FEMALE

R=Path(__file__).resolve().parents[2]
D=R/'ArtDir/hero/base_lock'; P=D/'proof'
font=ImageFont.truetype('/System/Library/Fonts/Helvetica.ttc',18)
small=ImageFont.truetype('/System/Library/Fonts/Helvetica.ttc',15)
panel=Image.new('RGB',(1260,1180),(30,30,33)); draw=ImageDraw.Draw(panel)
draw.text((16,12),'Source front/back consistency · frozen scale · mirrored back',font=font,fill='white')
rows=[]
for row,(sex,suf,pr) in enumerate([('male','M',MALE),('female','F',FEMALE)]):
    masks=[]; native=Image.open(D/(sex+'_body_plain.jpg')).convert('RGB')
    for view in ['front','back']:
        cx=pr['cx' if view=='front' else 'back_cx']
        im=Image.new('L',(1280,720)); ImageDraw.Draw(im).polygon(pr[view+'_outline'],fill=255)
        crop=np.asarray(im.crop((cx-280,0,cx+280,720)))>127
        masks.append(crop if view=='front' else np.fliplr(crop))
    a,b=masks; distance=float((a^b).sum()/(a|b).sum())
    actual=[]
    for view in ['front','back']:
        alpha=np.asarray(Image.open(P/'blender'/(suf+'_'+view+'.png')).getchannel('A').crop((360,0,920,720)))>127
        actual.append(alpha if view=='front' else np.fliplr(alpha))
    ma,mb=actual
    rows.append({'sex':sex,'reference_front_vs_mirrored_back_jaccard_distance':distance,
        'lower_bound_on_worst_of_two_fixed_orthographic_errors':distance/2,
        'actual_model_front_vs_mirrored_back_jaccard_distance':float((ma^mb).sum()/(ma|mb).sum()),
        'model_front_back_difference_pixels':int((ma^mb).sum())})
    y=55+row*555
    for col,view in enumerate(['front','back']):
        cx=pr['cx' if view=='front' else 'back_cx']
        crop=native.crop((cx-280,0,cx+280,720))
        if view=='back':crop=crop.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
        panel.paste(crop.resize((420,540),Image.Resampling.LANCZOS),(420*col,y+22))
        draw.text((420*col+10,y),sex.title()+' '+('front' if view=='front' else 'mirrored back'),font=small,fill='white')
    rgb=np.full((720,560,3),32,dtype=np.uint8);rgb[a&b]=[80,175,90];rgb[a&~b]=[235,80,70];rgb[b&~a]=[70,140,240]
    panel.paste(Image.fromarray(rgb).resize((420,540),Image.Resampling.NEAREST),(840,y+22))
    draw.text((850,y),f'Difference / union: {distance*100:.2f}%',font=small,fill='white')
panel.save(P/'reference-consistency.png')
method='Frozen native scale and source centres. Back target is horizontally mirrored to the front frame; no scale, roll or local deformation. Manual trace precision is estimated ±2 pixels. Jaccard distance is a metric, so a single identical orthographic projection cannot be within 5% of two masks whose pairwise distance exceeds 10%.'
(P/'reference-consistency.json').write_text(json.dumps({'method':method,'measurements':rows},indent=2))
text=['# Reference front/back consistency','','The unchanged replacement JPEGs have different front/back body proportions in their currently traced outlines. This is an input/framing conflict for the existing fixed orthographic silhouette gate. It does not excuse remaining face or surface differences.','','| Base | Front vs mirrored back | Minimum possible worst of the two errors |','|---|---:|---:|']
text += [f"| {r['sex'].title()} | {100*r['reference_front_vs_mirrored_back_jaccard_distance']:.2f}% | {100*r['lower_bound_on_worst_of_two_fixed_orthographic_errors']:.2f}% |" for r in rows]
text += ['','Under this framing, reversing the view preserves the same projected geometry. The Jaccard triangle inequality requires at least one error to reach half the distance between the reference masks. Both source pairs exceed the 10% distance compatible with two ≤5% errors.','','The manual source trace has estimated ±2-pixel boundary precision. The model and source comparison images use the frozen metre/pixel scale and source centres. The diagnostic display uses uniform thumbnail scaling only. Targets, proof cameras and the ~5% gate remain unchanged.','','Green is shared source area, red is front only, blue is mirrored back only. See reference-consistency.png and the machine-readable JSON.','','Front-view likeness is being refined while the reference-priority question is pending. No overall BASE PASS is claimed.']
(P/'REFERENCE_CONSISTENCY.md').write_text('\n'.join(text)+'\n')
print(json.dumps(rows,indent=2))
