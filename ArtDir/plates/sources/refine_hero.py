from PIL import Image,ImageDraw
from pathlib import Path
p=Path('/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/ArtDir/plates/sources')
im=Image.open(p/'hero-refined.png').convert('RGBA')
a=im.getchannel('A').point(lambda v: 255 if v>220 else 0)
im.putalpha(a)
head=im.crop((480,80,1320,850));body=im.copy();d=ImageDraw.Draw(body);d.rectangle((480,0,1400,849),fill=(0,0,0,0))
head=head.resize((487,446),Image.Resampling.LANCZOS)
body.alpha_composite(head,(679,405))
b=body.getbbox();body=body.crop(b);body.save(p/'hero-proportion-composite.png')
bg=Image.new('RGBA',body.size,'#7EC8E3');bg.alpha_composite(body);bg.convert('RGB').save('/tmp/hero-check.jpg')
