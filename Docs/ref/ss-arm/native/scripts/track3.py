"""Tracker v3: orange robe (compact blob) + arm anchor, all 130 frames @1 fps.

robe  : saturated orange, compact  -> the puppet's cloak; its halo ring is the
        large orange arc, so we take the largest compact component in-room
arm   : warm grey, largest component within 260 px of the puppet
anchor: the arm pixel nearest the room border = carriage rail contact

usage: python track3.py -> calib/track3.json, calib/track3_table.txt
"""
import json, os
import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FRAMES = os.path.join(ROOT, "frames")
ROOM = (768, 206, 1896, 1212)


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


def comps(mask, minpx, lab=None):
    if lab is None:
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
                        fill=round(float(cnt) / ((xs.max() - xs.min() + 1) * (ys.max() - ys.min() + 1)), 3),
                        cx=float(xs.mean()), cy=float(ys.mean())))
    out.sort(key=lambda d: -d["area"])
    return out


rows = []
for i in range(130):
    fn = "t%03d.png" % i
    a = np.asarray(Image.open(os.path.join(FRAMES, fn)).convert("RGB")).astype(np.int16)
    R, G, B = a[:, :, 0], a[:, :, 1], a[:, :, 2]

    orange = (R > 175) & ((R - G) > 85) & ((G - B) > 20) & (B < 80)
    orange[:215, :] = False
    orange[1200:, :] = False
    orange[:, :785] = False
    orange[:, 1875:] = False
    oc = [c for c in comps(orange, 500)
          if 20 <= c["w"] <= 120 and 20 <= c["h"] <= 120 and 0.45 <= c["w"] / float(c["h"]) <= 2.2]
    puppet = oc[0] if oc else None

    grey = ((R + G + B) / 3.0 > 48) & ((R + G + B) / 3.0 < 215) & \
           (R - G >= 0) & (R - G <= 28) & (G - B >= -10) & (G - B <= 18) & (R > 55)
    gl = label(grey)
    gc = comps(grey, 1200, gl)
    arm, anchor = None, None
    if gc:
        if puppet:
            px, py = puppet["cx"], puppet["cy"]

            def bd(c):
                dx = max(c["x0"] - px, 0, px - c["x1"])
                dy = max(c["y0"] - py, 0, py - c["y1"])
                return np.hypot(dx, dy)
            near = [c for c in gc if bd(c) < 400]
            arm = min(near, key=bd) if near else gc[0]
        else:
            arm = gc[0]
        sub = grey[arm["y0"]:arm["y1"] + 1, arm["x0"]:arm["x1"] + 1]
        sl = label(sub)
        ids, counts = np.unique(sl[sl > 0], return_counts=True)
        k = int(np.argmin([abs(int(c) - arm["area"]) for c in counts.tolist()]))
        ys, xs = np.nonzero(sl == ids[k])
        ys = ys + arm["y0"]
        xs = xs + arm["x0"]
        x0, y0, x1, y1 = ROOM
        cands = [(int(np.abs(xs - x0).min()), "L"), (int(np.abs(xs - x1).min()), "R"),
                 (int(np.abs(ys - y0).min()), "T"), (int(np.abs(ys - y1).min()), "B")]
        dmin, side = min(cands)
        selp = {"L": np.abs(xs - x0) <= dmin + 1, "R": np.abs(xs - x1) <= dmin + 1,
                "T": np.abs(ys - y0) <= dmin + 1, "B": np.abs(ys - y1) <= dmin + 1}[side]
        anchor = dict(side=side, gap=dmin,
                      x=round(float(xs[selp].mean()), 1), y=round(float(ys[selp].mean()), 1),
                      bbox=[int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max())],
                      area=int(len(xs)))
    rows.append(dict(sec=i, file=fn, puppet=puppet, anchor=anchor))
    if i % 10 == 0:
        print(i, puppet and (round(puppet["cx"]), round(puppet["cy"]), puppet["area"]),
              anchor and (anchor["side"], round(anchor["x"]), round(anchor["y"]), anchor["gap"]), flush=True)

json.dump(rows, open(os.path.join(ROOT, "calib", "track3.json"), "w", encoding="utf-8"),
          ensure_ascii=False, indent=1)

out = [f'{"t":>4} {"px":>6} {"py":>6} {"robe":>5} {"wh":>8} {"side":>4} {"ax":>6} {"ay":>6} {"gap":>4} {"step":>6}']
prev = None
for r in rows:
    p, an = r["puppet"], r["anchor"]
    if p:
        step = 0.0 if prev is None else float(np.hypot(p["cx"] - prev[0], p["cy"] - prev[1]))
        prev = (p["cx"], p["cy"])
        ps = f'{p["cx"]:6.0f} {p["cy"]:6.0f} {p["area"]:5d} {p["w"]:3d}x{p["h"]:2d}'
    else:
        step = float("nan")
        ps = "     -      -     -      -  "
    if an:
        out.append(f'{r["sec"]:4d} {ps} {an["side"]:>4} {an["x"]:6.0f} {an["y"]:6.0f} {an["gap"]:4d} {step:6.1f}')
    else:
        out.append(f'{r["sec"]:4d} {ps}    -      -      -    - {step:6.1f}')
txt = "\n".join(out)
open(os.path.join(ROOT, "calib", "track3_table.txt"), "w", encoding="utf-8").write(txt)
print(txt)
