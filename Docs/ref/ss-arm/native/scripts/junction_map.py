"""ASCII class map around the arm-end / robe junction (2 px bins).

classes: P pink skin | A grey arm/carriage | O bright orange robe | o dark red-orange
         . dark room       (bin = 2x2 px, letter is the dominant class)

usage: python junction_map.py t060 1450 780 110 70
"""
import sys, os
import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def classify(a):
    R = a[:, :, 0].astype(np.float32)
    G = a[:, :, 1].astype(np.float32)
    B = a[:, :, 2].astype(np.float32)
    m = B - 0.8 * G
    v = (R + G + B) / 3
    cls = np.full(R.shape, ".", dtype="<U1")
    grey = (v > 48) & (v < 215) & (R - G >= 0) & (R - G <= 28) & (G - B >= -10) & (G - B <= 18) & (R > 55)
    pink = m > 45
    orange = (R > 175) & (R - G > 85) & (G - B > 20) & (B < 80)
    darkred = (R > 110) & (R - G > 45) & (R - G <= 110) & (G - B > 0) & (B < 90) & (m < 40)
    cls[grey] = "A"
    cls[darkred] = "o"
    cls[orange] = "O"
    cls[pink] = "P"
    return cls


def main():
    name, x0, y0, w, h = sys.argv[1], int(sys.argv[2]), int(sys.argv[3]), int(sys.argv[4]), int(sys.argv[5])
    a = np.asarray(Image.open(os.path.join(ROOT, "frames", name + ".png")).convert("RGB"))
    cls = classify(a)
    print("== %s  window x %d..%d  y %d..%d   (1 char = 2x2 px)" % (name, x0, x0 + w, y0, y0 + h))
    hdr = "      " + "".join(str(((x0 + 2 * i) // 10) % 10) for i in range(w // 2))
    print(hdr)
    for j in range(h // 2):
        row = ""
        for i in range(w // 2):
            block = cls[y0 + 2 * j:y0 + 2 * j + 2, x0 + 2 * i:x0 + 2 * i + 2].ravel()
            vals, cnt = np.unique(block, return_counts=True)
            row += vals[np.argmax(cnt)]
        print("%5d %s" % (y0 + 2 * j, row))


main()
