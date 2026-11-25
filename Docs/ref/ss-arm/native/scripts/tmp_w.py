import os
from PIL import Image, ImageDraw
ROOT = r"D:\Unity Project\DesktopIterator\Docs\ref\ss-arm\native"
d = os.path.join(ROOT,"frames_5fps")
files = sorted(os.listdir(d))
TW,TH = 426,240
COLS = 6; ROWS = 5
sheet = Image.new("RGB",(COLS*TW, ROWS*(TH+16)),(0,0,0))
dr = ImageDraw.Draw(sheet)
for i,f in enumerate(files):
    im = Image.open(os.path.join(d,f)).convert("RGB").resize((TW,TH), Image.LANCZOS)
    x,y = (i%COLS)*TW,(i//COLS)*(TH+16)
    sheet.paste(im,(x,y+16))
    t = 23.0 + i*0.2
    dr.text((x+4,y+3), "%.1fs"%t, fill=(0,255,120))
sheet.save(os.path.join(ROOT,"qc","window_23_29_5fps.png"))
print(sheet.size)
