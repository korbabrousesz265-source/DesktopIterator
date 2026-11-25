"""Scan every 1 fps frame for the SS puppet-head pink blob.

Head sprite: Circle20 scaled (5/9, 5/11) -> 11.1 x 9.1 game px (aspect 1.220).
Skin colour: (1, 0.4, 0.796) -> approx (255, 102, 203) in sRGB-ish 8bit.
Thresholds from the task: R>180, 60<=G<=150, 140<=B<=230.

Outputs: calib/pink_scan.json  (per-frame components, chosen candidate stats)
"""
import json, os, sys
import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FRAMES = os.path.join(ROOT, "frames")
OUT = os.path.join(ROOT, "calib", "pink_scan.json")


def mask_pink(im):
    a = np.asarray(im.convert("RGB")).astype(np.int16)
    R, G, B = a[:, :, 0], a[:, :, 1], a[:, :, 2]
    return (R > 180) & (G >= 60) & (G <= 150) & (B >= 140) & (B <= 230)


def label(mask):
    """Tiny 4-connected union-find labeller (no scipy dependency)."""
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
    return flat[lab], nxt


def components(mask, min_px=40):
    lab, n = label(mask)
    out = []
    ids, counts = np.unique(lab[lab > 0], return_counts=True)
    for cid, cnt in zip(ids.tolist(), counts.tolist()):
        if cnt < min_px:
            continue
        ys, xs = np.nonzero(lab == cid)
        x0, x1, y0, y1 = xs.min(), xs.max(), ys.min(), ys.max()
        w, h = x1 - x0 + 1, y1 - y0 + 1
        # fill ratio inside bbox: a scaled circle -> ~pi/4 = 0.785
        fill = cnt / float(w * h)
        out.append(dict(area=cnt, x0=int(x0), y0=int(y0), x1=int(x1), y1=int(y1),
                        w=int(w), h=int(h), aspect=round(w / h, 3), fill=round(fill, 3),
                        cx=round((x0 + x1) / 2.0, 1), cy=round((y0 + y1) / 2.0, 1)))
    out.sort(key=lambda d: -d["area"])
    return out


def sharpness(im):
    g = np.asarray(im.convert("L")).astype(np.float32)
    k = np.array([[0, 1, 0], [1, -4, 1], [0, 1, 0]], np.float32)
    # separable conv via shifts
    lap = (np.roll(g, 1, 0) + np.roll(g, -1, 0) + np.roll(g, 1, 1) +
           np.roll(g, -1, 1) - 4 * g)[1:-1, 1:-1]
    return float(lap.var())


rows = []
files = sorted(os.listdir(FRAMES))
for i, fn in enumerate(files):
    p = os.path.join(FRAMES, fn)
    im = Image.open(p)
    m = mask_pink(im)
    comps = components(m)
    rows.append(dict(sec=i, file=fn, size=list(im.size), pink_px=int(m.sum()),
                     comps=comps[:8], sharp=round(sharpness(im), 1)))
    if i % 20 == 0:
        print(i, fn, "pink", int(m.sum()), "comps", len(comps), flush=True)

with open(OUT, "w", encoding="utf-8") as f:
    json.dump(rows, f, ensure_ascii=False, indent=1)
print("wrote", OUT, "frames", len(rows))
