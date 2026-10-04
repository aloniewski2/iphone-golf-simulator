"""Freeze source-only silhouette masks and cameras for the male blockout phase."""
from pathlib import Path
from collections import deque
from PIL import Image,ImageDraw,ImageFont
import numpy as np,json,hashlib
R=Path(__file__).resolve().parents[2];D=R/'ArtDir/hero/base_lock';W=R/'work/male-silhouette'
W.mkdir(parents=True,exist_ok=True)
VIEWS=[
 ('bald_front','male_body_bald.jpg',(105,51,604,659),353.0,654,593,0),
 ('bald_back','male_body_bald.jpg',(675,51,1160,655),915.75,651,589,180),
 ('sheet_front','male_multiangle_body.jpg',(55,49,315,347),184.5,344,292,0),
 ('sheet_back','male_multiangle_body.jpg',(350,49,620,347),479.75,344,292,180),
 ('left','male_multiangle_body.jpg',(739,49,825,347),796.5,343,291,90),
 ('right','male_multiangle_body.jpg',(1038,49,1138,347),1068.0,343,291,-90)]

def largest(mask):
    seen=np.zeros(mask.shape,bool);best=[]
    for y,x in zip(*np.where(mask)):
        if seen[y,x]:continue
        q=deque([(int(y),int(x))]);seen[y,x]=True;component=[]
        while q:
            yy,xx=q.popleft();component.append((yy,xx))
            for dy,dx in ((0,1),(1,0),(-1,0),(0,-1),(1,1),(1,-1),(-1,1),(-1,-1)):
                yn,xn=yy+dy,xx+dx
                if 0<=yn<mask.shape[0] and 0<=xn<mask.shape[1] and mask[yn,xn] and not seen[yn,xn]:seen[yn,xn]=True;q.append((yn,xn))
        if len(component)>len(best):best=component
    result=np.zeros(mask.shape,bool)
    for y,x in best:result[y,x]=True
    # Interior eye/brow/skin shading is not a silhouette hole. Flood the
    # exterior background and fill only enclosed source-mask holes.
    exterior=np.zeros(mask.shape,bool);q=deque()
    for y in range(mask.shape[0]):
        for x in (0,mask.shape[1]-1):
            if not result[y,x]:exterior[y,x]=True;q.append((y,x))
    for x in range(mask.shape[1]):
        for y in (0,mask.shape[0]-1):
            if not result[y,x] and not exterior[y,x]:exterior[y,x]=True;q.append((y,x))
    while q:
        y,x=q.popleft()
        for dy,dx in ((0,1),(1,0),(-1,0),(0,-1)):
            yn,xn=y+dy,x+dx
            if 0<=yn<mask.shape[0] and 0<=xn<mask.shape[1] and not result[yn,xn] and not exterior[yn,xn]:exterior[yn,xn]=True;q.append((yn,xn))
    result=~exterior
    return result

records=[];panel=Image.new('RGB',(1536,1480),(35,35,38));draw=ImageDraw.Draw(panel)
for i,(name,file,box,cx,sole,height,angle) in enumerate(VIEWS):
    source=Image.open(D/file).convert('RGB');crop=np.array(source.crop(box),dtype=np.float32)
    left=np.median(crop[:,:4,:],axis=1);right=np.median(crop[:,-4:,:],axis=1)
    u=np.linspace(0,1,crop.shape[1])[None,:,None];bg=left[:,None,:]*(1-u)+right[:,None,:]*u
    mask=largest(np.max(crop-bg,axis=2)>7.5)
    full=np.zeros((720,1280),bool);full[box[1]:box[3],box[0]:box[2]]=mask
    Image.fromarray(np.uint8(full)*255).save(W/(name+'_target.png'))
    rows=[]
    for y in range(box[1],box[3]):
        xs=np.where(full[y])[0]
        if len(xs):rows.append([y,*[int(x) for x in xs]])
    ys,xs=np.where(full)
    rec={'name':name,'file':file,'sha256':hashlib.sha256((D/file).read_bytes()).hexdigest(),'roi':box,'centre_x':cx,'sole_y':sole,'height_pixels':height,'metres_per_pixel':1.7/height,'angle_degrees':angle,'foreground_pixels':int(full.sum()),'bbox':[int(xs.min()),int(ys.min()),int(xs.max()),int(ys.max())]}
    records.append(rec)
    (W/(name+'_rows.json')).write_text(json.dumps(rows))
    overlay=np.array(source)
    overlay[full]=(.6*overlay[full]+.4*np.array([40,230,130])).astype(np.uint8)
    view=Image.fromarray(overlay).crop(box);view.thumbnail((490,670))
    x=(i%3)*512;y=(i//3)*730+35;panel.paste(view,(x+(512-view.width)//2,y))
    draw.text((x+15,y-25),name+' — reference extraction',fill='white')
(W/'reference-cameras.json').write_text(json.dumps({'height_metres':1.7,'origin_calibration':'Median ankle-outline centre at canonical rows 580–590 (about 0.18–0.21m above the sole), measured from source masks only. Front/back origin is the midpoint of the two ankles; profile origin is their overlapping cross-section centre. Initial approximate centres corrected to these anatomical measurements. Scale and calibrated origins are locked for subsequent modeling; no candidate mask determines them.','mask_method':'Source-only background subtraction against per-row ROI borders, threshold +7.5 sRGB levels, largest eight-connected foreground; enclosed shading holes filled. Excludes floor shadows and labels. No candidate render is used. Masks visually inspected.','views':records},indent=2))
panel.save(W/'reference-mask-inspection.png')
print(json.dumps(records,indent=2))
