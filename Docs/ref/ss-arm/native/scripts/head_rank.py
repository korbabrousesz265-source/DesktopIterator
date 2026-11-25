"""Pinkness-metric scan + subpixel head edge estimate.

Pinkness  m = B - 0.8*G  (0..255).
  pink skin  ~ 63      grey arm ~ 30      brown bg ~ 4      orange robe ~ -63
A stretched circle's row chords are symmetric about the centre row, so the
widest chord = head diameter.  We report, per frame, the component that passes
an aspect gate, sorted by widest chord (= least occluded).

Output: calib/head_rank.json
"""
import json, os
import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FRAMES = os.path.join(ROOT, "frames")
OUT = os.path.join(ROOT, "calib", "head_rank.json")


def pinkness(im):
    a = np.asarray(im.convert("RGB")).astype(np.float32)
    return a[:, :, 2] - 0.8 * a[:, :, 1]


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


rows = []
for i in range(130):
    fn = "t%03d.png" % i
    im = Image.open(os.path.join(FRAMES, fn))
    m = pinkness(im)
    mask = m > 45
    lab = label(mask)
    ids, counts = np.unique(lab[lab > 0], return_counts=True)
    best = None
    for cid, cnt in zip(ids.tolist(), counts.tolist()):
        if cnt < 150:
            continue
        ys, xs = np.nonzero(lab == cid)
        x0, x1, y0, y1 = int(xs.min()), int(xs.max()), int(ys.min()), int(ys.max())
        w, h = x1 - x0 + 1, y1 - y0 + 1
        asp = w / float(h)
        if not (1.02 <= asp <= 1.45):
            continue
        rows_d = {}
        for y, x in zip(ys.tolist(), xs.tolist()):
            r = rows_d.setdefault(y, [x, x])
            r[0] = min(r[0], x)
            r[1] = max(r[1], x)
        widths = {y: r[1] - r[0] + 1 for y, r in rows_d.items()}
        mw = max(widths.values())
        mwy = max(widths, key=lambda k: widths[k])
        # how many rows are near the max width? a clean disc has a plateau
        plateau = sum(1 for v in widths.values() if v >= mw - 2)
        cand = dict(area=int(cnt), x0=x0, y0=y0, x1=x1, y1=y1, w=int(w), h=int(h),
                    aspect=round(asp, 3), fill=round(cnt / float(w * h), 3),
                    maxw=int(mw), maxw_y=int(mwy), plateau=int(plateau),
                    chords={int(k): int(v) for k, v in sorted(widths.items())})
        if best is None or (cand["maxw"], cand["area"]) > (best["maxw"], best["area"]):
            best = cand
    rows.append(dict(sec=i, file=fn, head=best))

with open(OUT, "w", encoding="utf-8") as f:
    json.dump(rows, f, ensure_ascii=False, indent=1)

ranked = [r for r in rows if r["head"]]
ranked.sort(key=lambda r: (-r["head"]["maxw"], -r["head"]["area"]))
print(f'{"t":>4} {"maxw":>4} {"w x h":>8} {"asp":>5} {"fill":>5} {"plat":>4} {"area":>4}  bbox')
for r in ranked[:25]:
    b = r["head"]
    print(f'{r["sec"]:4d} {b["maxw"]:4d} {b["w"]:3d}x{b["h"]:3d} {b["aspect"]:5.2f} {b["fill"]:5.2f} {b["plateau"]:4d} {b["area"]:4d}  ({b["x0"]},{b["y0"]})')
print("no-candidate frames:", [r["sec"] for r in rows if not r["head"]])
