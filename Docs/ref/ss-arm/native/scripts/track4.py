"""Tracker v4: head-disc position via a size prior + robe context.

Head prior from the calibration on this clip: a stretched disc ~29.3 x 24.0 px
(aspect 1.222).  Among the pink blobs in a frame, the one whose bbox best matches
that prior (and that sits next to the orange cloak) is the head.  This is far more
stable than a centroid of the cloak, whose halo ring changes shape.

usage: python track4.py -> calib/track4.json, calib/track4_table.txt
"""
import json, os
import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FRAMES = os.path.join(ROOT, "frames")
PRIOR_W, PRIOR_H = 29.3, 24.0


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
                        cx=float(xs.mean()), cy=float(ys.mean())))
    return out


rows = []
prev = None
for i in range(130):
    fn = "t%03d.png" % i
    a = np.asarray(Image.open(os.path.join(FRAMES, fn)).convert("RGB")).astype(np.int16)
    R, G, B = a[:, :, 0], a[:, :, 1], a[:, :, 2]

    pink = (a[:, :, 2].astype(np.float32) - 0.8 * a[:, :, 1].astype(np.float32)) > 45
    pc = comps(pink, 110)
    orange = (R > 175) & ((R - G) > 85) & ((G - B) > 20) & (B < 80)
    orange[:215, :] = False
    orange[1200:, :] = False
    orange[:, :785] = False
    orange[:, 1875:] = False
    oc = [c for c in comps(orange, 700)
          if 20 <= c["w"] <= 120 and 20 <= c["h"] <= 120 and 0.45 <= c["w"] / float(c["h"]) <= 2.2]

    best = None
    for p in pc:
        if not (14 <= p["w"] <= 46 and 10 <= p["h"] <= 40):
            continue
        dh = abs(p["w"] - PRIOR_W) / PRIOR_W + abs(p["h"] - PRIOR_H) / PRIOR_H
        asp = p["w"] / float(p["h"])
        asp_pen = abs(asp - PRIOR_W / PRIOR_H)
        ctx = 0.0
        if oc:
            d = min(np.hypot(p["cx"] - o["cx"], p["cy"] - o["cy"]) for o in oc)
            ctx = min(d, 140.0) / 140.0
        cont = 0.0
        if prev is not None:
            cont = min(np.hypot(p["cx"] - prev[0], p["cy"] - prev[1]), 400.0) / 400.0
        score = dh * 2.0 + asp_pen * 1.5 + ctx * 1.2 + cont * 0.8
        cand = dict(x=round(p["cx"], 1), y=round(p["cy"], 1), w=p["w"], h=p["h"],
                    area=p["area"], dctx=None if not oc else round(min(
                        np.hypot(p["cx"] - o["cx"], p["cy"] - o["cy"]) for o in oc), 1),
                    score=round(score, 3))
        if best is None or score < best[0]:
            best = (score, cand)
    head = best[1] if best else None
    step = None
    if head:
        if prev is not None:
            step = round(float(np.hypot(head["x"] - prev[0], head["y"] - prev[1])), 1)
        prev = (head["x"], head["y"])
    rows.append(dict(sec=i, file=fn, head=head, step=step))
    if i % 20 == 0:
        print(i, head, step, flush=True)

json.dump(rows, open(os.path.join(ROOT, "calib", "track4.json"), "w", encoding="utf-8"),
          ensure_ascii=False, indent=1)

out = [f'{"t":>4} {"hx":>7} {"hy":>7} {"wh":>8} {"area":>5} {"score":>6} {"step":>6}']
for r in rows:
    h = r["head"]
    if h:
        out.append(f'{r["sec"]:4d} {h["x"]:7.1f} {h["y"]:7.1f} {h["w"]:3d}x{h["h"]:2d} '
                   f'{h["area"]:5d} {h["score"]:6.2f} {r["step"] if r["step"] is not None else -1:6.1f}')
    else:
        out.append(f'{r["sec"]:4d}       -       -       -     -      -      -')
txt = "\n".join(out)
open(os.path.join(ROOT, "calib", "track4_table.txt"), "w", encoding="utf-8").write(txt)
print(txt)
