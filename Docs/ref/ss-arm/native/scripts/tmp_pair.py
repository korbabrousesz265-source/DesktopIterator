import os
from PIL import Image, ImageDraw
ROOT = r"D:\Unity Project\DesktopIterator\Docs\ref\ss-arm\native"
sheet = Image.new("RGB",(1280,1460),(0,0,0))
d = ImageDraw.Draw(sheet)
for i,n in enumerate(("half_t040","half_t085")):
    im = Image.open(os.path.join(ROOT,"qc",n+".png")).convert("RGB")
    sheet.paste(im,(0,i*730))
    d.text((4,i*730+4), n, fill=(0,255,120))
sheet.save(os.path.join(ROOT,"qc","pair_040_085.png"))
print("ok")
