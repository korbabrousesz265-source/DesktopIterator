import os
from PIL import Image, ImageDraw
ROOT = r"D:\Unity Project\DesktopIterator\Docs\ref\ss-arm\native"
cases = [("t021",1670,206),("t034",1789,206),("t098",1282,218),("t123",1262,206)]
W,H,SC = 180,120,6
sheet = Image.new("RGB",(2*W*SC,2*H*SC),(0,0,0))
d = ImageDraw.Draw(sheet)
for i,(n,ax,ay) in enumerate(cases):
    im = Image.open(os.path.join(ROOT,"frames",n+".png")).convert("RGB")
    x0,y0 = ax-W//2, ay-20
    crop = im.crop((x0,y0,x0+W,y0+H)).resize((W*SC,H*SC), Image.LANCZOS)
    ox,oy = (i%2)*W*SC,(i//2)*H*SC
    sheet.paste(crop,(ox,oy))
    d.text((ox+4,oy+4), "%s  origin=(%d,%d)"%(n,x0,y0), fill=(0,255,255))
sheet.save(os.path.join(ROOT,"qc","pivot_candidates.png"))
print(sheet.size)
