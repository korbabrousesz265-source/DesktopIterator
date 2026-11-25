import numpy as np, os
from PIL import Image
ROOT = r"D:\Unity Project\DesktopIterator\Docs\ref\ss-arm\native"
im = Image.open(os.path.join(ROOT,"frames","t060.png")).convert("RGB")
a = np.asarray(im).astype(int)
print("size", im.size)
# scan across the arm: horizontal line y=420 through the arm's mid part
for y in (300, 420, 560):
    row = a[y]
    # print runs of "bright grey"
    idx = [x for x in range(1200,1900) if abs(row[x][0]-row[x][1])<20 and abs(row[x][1]-row[x][2])<25 and row[x].mean()>70]
    if idx:
        print("y=%d grey run x=%d..%d  sample colours:"%(y,min(idx),max(idx)), [tuple(row[x]) for x in range(min(idx), max(idx)+1, max(1,(max(idx)-min(idx))//6))])
    else:
        print("y=%d none"%y)
# carriage plate colour sample (top right area)
print("plate samples:", [tuple(a[y,x]) for y,x in ((150,1780),(160,1800),(140,1760),(170,1830))])
print("arm samples:", [tuple(a[y,x]) for y,x in ((300,1690),(400,1600),(430,1580),(500,1520))])
print("bg samples:", [tuple(a[y,x]) for y,x in ((700,1200),(900,1300),(400,1100))])
