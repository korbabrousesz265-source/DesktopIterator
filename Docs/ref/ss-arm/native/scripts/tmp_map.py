import numpy as np, os
from PIL import Image
ROOT = r"D:\Unity Project\DesktopIterator\Docs\ref\ss-arm\native"
a = np.asarray(Image.open(os.path.join(ROOT,"frames","t112.png")).convert("RGB")).astype(np.float32)
m = a[:,:,2] - 0.8*a[:,:,1]
x0,y0 = 980, 788
W,H = 46, 34
ramp = " .:-=+*#%@"
print("pinkness map (m = B-0.8G), cols 980..1025, rows 788..821")
hdr = "     " + "".join(str((x0+i)//100%10) for i in range(W))
hdr2= "     " + "".join(str((x0+i)//10%10) for i in range(W))
hdr3= "     " + "".join(str((x0+i)%10) for i in range(W))
print(hdr); print(hdr2); print(hdr3)
for j in range(H):
    row = ""
    for i in range(W):
        v = m[y0+j, x0+i]
        if v <= 20: c = " "
        elif v <= 35: c = "."
        elif v <= 45: c = ":"
        elif v <= 55: c = "-"
        elif v <= 62: c = "="
        elif v <= 70: c = "+"
        elif v <= 80: c = "*"
        else: c = "#"
        row += c
    print("%4d %s" % (y0+j, row))
