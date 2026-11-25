import numpy as np, os, json
from PIL import Image
ROOT = r"D:\Unity Project\DesktopIterator\Docs\ref\ss-arm\native"
FR = os.path.join(ROOT,"frames")
cases = [("t000",981,464),("t050",1554,1064),("t100",1771,372),("t123",1577,286)]
tiles=[]
for name,cx,cy in cases:
    im = Image.open(os.path.join(FR,name+".png")).convert("RGB")
    x0,y0 = cx-110, cy-90
    crop = im.crop((x0,y0,x0+220,y0+180)).resize((220*5,180*5), Image.LANCZOS)
    tiles.append(crop)
sheet = Image.new("RGB",(2*220*5, 2*180*5),(0,0,0))
for i,t in enumerate(tiles):
    sheet.paste(t, ((i%2)*220*5, (i//2)*180*5))
sheet.save(os.path.join(ROOT,"qc","puppet_anatomy.png"))
print(sheet.size)
