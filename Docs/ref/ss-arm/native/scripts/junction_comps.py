"""Component report in a window around the puppet: grey (arm) + orange (robe).

usage: python junction_comps.py t034 1600 850 200 140
prints, for the window: every grey blob (area / bbox / centroid) and every
orange blob, so the arm's end face and the robe can be picked by number instead
of by eye.
"""
import sys, os
import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def label(mask):
    h, w = mask.shape
    lab = np.zeros((h, w), np.int32)
    parent = [0]

    def find(x):
        while parent[x] != x:
            parent[x] = parent[parent[x]]
            x = parent[x]
        return x

    nxt = 1
    ys, xs = np.nonzero(mask)
    for y, x in zip(ys.tolist(), xs.tolist()):
        up = lab[y - 1, x] if y > 0 else 0
        lf = lab[y, x - 1] if x > 0 else 0
        if up and lf:
            a, b = find(up), find(lf)
            if a != b:
                parent[b] = a
            lab[y, x] = a
        elif up or lf:
            lab[y, x] = find(up or lf)
        else:
            parent.append(nxt)
            lab[y, x] = nxt
            nxt += 1
    flat = np.array([find(i) for i in range(nxt)], np.int32)
    return flat[lab]


def comps(mask, minpx):
    lab = label(mask)
    out = []
    ids, counts = np.unique(lab[lab > 0], return_counts=True)
    for cid, cnt in zip(ids.tolist(), counts.tolist()):
        if cnt < minpx:
            continue
        ys, xs = np.nonzero(lab == cid)
        out.append(dict(area=int(cnt), x0=int(xs.min()), y0=int(ys.min()),
                        x1=int(xs.max()), y1=int(ys.max()),
                        w=int(xs.max() - xs.min() + 1), h=int(ys.max() - ys.min() + 1),
                        cx=round(float(xs.mean()), 1), cy=round(float(ys.mean()), 1)))
    out.sort(key=lambda d: -d["area"])
    return out


def main():
    name, x0, y0, w, h = sys.argv[1], int(sys.argv[2]), int(sys.argv[3]), int(sys.argv[4]), int(sys.argv[5])
    a = np.asarray(Image.open(os.path.join(ROOT, "frames", name + ".png")).convert("RGB")).astype(np.int16)
    sub = a[y0:y0 + h, x0:x0 + w]
    R, G, B = sub[:, :, 0], sub[:, :, 1], sub[:, :, 2]
    v = (R + G + B) / 3.0
    grey = (v > 48) & (v < 215) & (R - G >= 0) & (R - G <= 28) & (G - B >= -10) & (G - B <= 18) & (R > 55)
    orange = (R > 175) & ((R - G) > 85) & ((G - B) > 20) & (B < 80)
    pink = (B.astype(np.float32) - 0.8 * G.astype(np.float32)) > 45
    print("== %s window (%d,%d)+%dx%d  [absolute coords]" % (name, x0, y0, w, h))
    for tag, m, minpx in (("GREY", grey, 120), ("ORANGE", orange, 200), ("PINK", pink, 120)):
        print(" -- %s components (absolute bbox)" % tag)
        for c in comps(m, minpx)[:6]:
            print("    area %5d  x %4d..%4d  y %4d..%4d  %2dx%-2d  c=(%.1f, %.1f)"
                  % (c["area"], x0 + c["x0"], x0 + c["x1"], y0 + c["y0"], y0 + c["y1"],
                     c["w"], c["h"], x0 + c["cx"], y0 + c["cy"]))


main()
