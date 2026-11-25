"""Head-disc measurement pass.

For every 1 fps frame:
  * build a hue-based 'pink skin' mask (robust to the room's warm lighting),
  * take connected components,
  * for the component that looks like the puppet head (area / aspect gate),
    record per-row chord widths and per-column heights.

The head sprite in code is Circle20 scaled (5/9, 5/11) -> 11.1 x 9.1 game px,
i.e. a 20 px circle stretched to aspect 1.2208.  So a clean head component must
be ~1.22 wider than tall, and its maximum row chord is the head diameter.

Output: calib/head_candidates.json  (ranked list, plus discard stats)
"""
import json, os, colorsys
import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FRAMES = os.path.join(ROOT, "frames")
OUT = os.path.join(ROOT, "calib", "head_candidates.json")


def rgb2hsv_np(a):
    a = a.astype(np.float32) / 255.0
    mx = a.max(axis=2)
    mn = a.min(axis=2)
    d = mx - mn
    h = np.zeros_like(mx)
    r, g, b = a[:, :, 0], a[:, :, 1], a[:, :, 2]
    nz = d > 1e-6
    idx = nz & (mx == r)
    h[idx] = ((g[idx] - b[idx]) / d[idx]) % 6
    idx = nz & (mx == g) & (mx != r)
    h[idx] = (b[idx] - r[idx]) / d[idx] + 2
    idx = nz & (mx == b) & (mx != r) & (mx != g)
    h[idx] = (r[idx] - g[idx]) / d[idx] + 4
    h = h * 60.0
    s = np.where(mx > 1e-6, d / np.maximum(mx, 1e-6), 0)
    return h, s, mx


def hue_mask(im, hlo=295.0, hhi=350.0, smin=0.25, vmin=0.35):
    a = np.asarray(im.convert("RGB"))
    h, s, v = rgb2hsv_np(a)
    return (h >= hlo) & (h <= hhi) & (s >= smin) & (v >= vmin)


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


def blob_shape(lab, cid):
    ys, xs = np.nonzero(lab == cid)
    x0, x1, y0, y1 = int(xs.min()), int(xs.max()), int(ys.min()), int(ys.max())
    area = len(ys)
    w, hh = x1 - x0 + 1, y1 - y0 + 1
    # chord widths per row, chord heights per column
    rows = {}
    for y, x in zip(ys.tolist(), xs.tolist()):
        r = rows.setdefault(y, [x, x])
        r[0] = min(r[0], x)
        r[1] = max(r[1], x)
    row_w = {y: r[1] - r[0] + 1 for y, r in rows.items()}
    cols = {}
    for y, x in zip(ys.tolist(), xs.tolist()):
        c = cols.setdefault(x, [y, y])
        c[0] = min(c[0], y)
        c[1] = max(c[1], y)
    col_h = {x: c[1] - c[0] + 1 for x, c in cols.items()}
    max_row = max(row_w.values())
    max_row_y = max(row_w, key=lambda k: row_w[k])
    max_col = max(col_h.values())
    max_col_x = max(col_h, key=lambda k: col_h[k])
    return dict(x0=x0, y0=y0, x1=x1, y1=y1, w=int(w), h=int(hh), area=int(area),
                fill=round(area / float(w * hh), 3),
                max_row_w=int(max_row), max_row_y=int(max_row_y),
                max_col_h=int(max_col), max_col_x=int(max_col_x),
                per_row_w={int(k): int(v) for k, v in sorted(row_w.items())},
                per_col_h={int(k): int(v) for k, v in sorted(col_h.items())})


rows = []
files = sorted(os.listdir(FRAMES))
for i, fn in enumerate(files):
    im = Image.open(os.path.join(FRAMES, fn))
    m = hue_mask(im)
    lab = label(m)
    ids, counts = np.unique(lab[lab > 0], return_counts=True)
    cands = []
    for cid, cnt in zip(ids.tolist(), counts.tolist()):
        if cnt < 120:
            continue
        b = blob_shape(lab, cid)
        if not (150 <= b["area"] <= 900):
            continue
        asp = b["w"] / float(b["h"])
        if not (1.0 <= asp <= 1.5):
            continue
        b["aspect"] = round(asp, 3)
        # disc-ness: a stretched circle has fill 0.785 and max_row_w ~ w
        b["disc_err"] = round(abs(b["fill"] - 0.785), 3)
        cands.append(b)
    cands.sort(key=lambda d: (d["disc_err"], -d["area"]))
    rows.append(dict(sec=i, file=fn, comps=cands[:4]))
    if i % 25 == 0:
        print(i, fn, len(cands), flush=True)

with open(OUT, "w", encoding="utf-8") as f:
    json.dump(rows, f, ensure_ascii=False, indent=1)
print("wrote", OUT)
