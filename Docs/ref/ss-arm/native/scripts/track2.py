"""Final per-frame tracker for the SS arm reference clip.

puppet   : orange robe blob (compact, in-room) + pink head blob nearest to it
arm      : warm-grey blob nearest to the puppet (arm + carriage + plate)
carriage : that blob's pixel nearest the room's inner rectangle -> the rail
           contact point, i.e. where the carriage grips the edge

usage: python track2.py  -> calib/track2.json, calib/track2_table.txt
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
                        cx=float(xs.mean()), cy=float(ys.mean()), mask=(cid,)))
    out.sort(key=lambda d: -d["area"])
    return out, lab


def frame_data(a):
    R = a[:, :, 0].astype(np.int16)
    G = a[:, :, 1].astype(np.int16)
    B = a[:, :, 2].astype(np.int16)

    # --- puppet: orange robe -------------------------------------------------
    orange = (R > 140) & (G > 50) & (G < 150) & (B < 80) & ((R - G) > 60)
    orange[:215, :] = False
    orange[1205:, :] = False
    orange[:, :780] = False
    orange[:, 1888:] = False
    robes = [c for c in comps(orange, 700)[0]
             if 25 <= c["w"] <= 100 and 25 <= c["h"] <= 100 and 0.75 <= c["w"] / c["h"] <= 1.7]

    # --- head ---------------------------------------------------------------
    pink = (B.astype(np.float32) - 0.8 * G.astype(np.float32)) > 50
    pinks = [c for c in comps(pink, 120)[0] if 0.55 <= c["w"] / float(c["h"]) <= 1.6]

    best = None
    for r in robes:
        for p in pinks:
            d = np.hypot(r["cx"] - p["cx"], r["cy"] - p["cy"])
            if d > 120:
                continue
            score = d - p["area"] / 400.0
            if best is None or score < best[0]:
                best = (score, r, p, d)
    puppet = None
    if best:
        _, r, p, d = best
        puppet = dict(head=(round(p["cx"], 1), round(p["cy"], 1)), head_wh=(p["w"], p["h"]),
                      head_area=p["area"], robe=(round(r["cx"], 1), round(r["cy"], 1)),
                      robe_area=r["area"], gap=round(d, 1))

    # --- arm ----------------------------------------------------------------
    grey = ((R + G + B) / 3.0 > 50) & ((R + G + B) / 3.0 < 210) & \
           (R - G >= 2) & (R - G <= 25) & (G - B >= -9) & (G - B <= 17) & (R > 60)
    gs, glab = comps(grey, 1500)
    anchor = None
    arm = None
    if gs:
        if puppet:
            px, py = puppet["head"]
            cand = []
            for c in gs:
                ys, xs = np.nonzero(glab == (np.unique(glab[glab > 0])[0] if False else 0))
            # nearest grey component to the puppet, by bbox distance
            def bd(c):
                dx = max(c["x0"] - px, 0, px - c["x1"])
                dy = max(c["y0"] - py, 0, py - c["y1"])
                return np.hypot(dx, dy)
            arm = min(gs, key=bd)
        else:
            arm = gs[0]
        ys, xs = np.nonzero(glab == np.unique(glab[glab > 0])[0])
        # recompute the selected component's pixels
        sub = gs
        # cheap: re-label only the selected bbox
        sel = grey[arm["y0"]:arm["y1"] + 1, arm["x0"]:arm["x1"] + 1]
        slab = label(sel)
        ids, counts = np.unique(slab[slab > 0], return_counts=True)
        # pick the sub-component whose size matches arm area best
        k = int(np.argmin([abs(c - arm["area"]) for c in counts.tolist()]))
        ys, xs = np.nonzero(slab == ids[k])
        ys = ys + arm["y0"]
        xs = xs + arm["x0"]
        x0, y0, x1, y1 = ROOM
        cands = [(np.abs(xs - x0).min(), "L"), (np.abs(xs - x1).min(), "R"),
                 (np.abs(ys - y0).min(), "T"), (np.abs(ys - y1).min(), "B")]
        dmin, side = min(cands)
        if side == "L":
            selp = np.abs(xs - x0) <= dmin + 1
        elif side == "R":
            selp = np.abs(xs - x1) <= dmin + 1
        elif side == "T":
            selp = np.abs(ys - y0) <= dmin + 1
        else:
            selp = np.abs(ys - y1) <= dmin + 1
        anchor = dict(side=side, gap=float(dmin),
                      x=float(xs[selp].mean()), y=float(ys[selp].mean()),
                      bbox=[int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max())],
                      area=int(len(xs)))
    return dict(puppet=puppet, anchor=anchor)


rows = []
for i in range(130):
    fn = "t%03d.png" % i
    a = np.asarray(Image.open(os.path.join(FRAMES, fn)).convert("RGB"))
    d = frame_data(a)
    d["sec"] = i
    d["file"] = fn
    rows.append(d)
    if i % 10 == 0:
        print(i, d["puppet"] and d["puppet"]["head"], d["anchor"] and (d["anchor"]["side"], round(d["anchor"]["x"]), round(d["anchor"]["y"])), flush=True)

json.dump(rows, open(os.path.join(ROOT, "calib", "track2.json"), "w", encoding="utf-8"),
          ensure_ascii=False, indent=1)

lines = []
lines.append(f'{"t":>4} {"head_x":>7} {"head_y":>7} {"wh":>8} {"robe_x":>7} {"robe_y":>7} '
             f'{"side":>4} {"anc_x":>7} {"anc_y":>7} {"gap":>4} {"v":>6}')
prev = None
for r in rows:
    p, an = r["puppet"], r["anchor"]
    if p:
        hx, hy = p["head"]
        v = 0.0 if prev is None else np.hypot(hx - prev[0], hy - prev[1])
        prev = (hx, hy)
        hs = f'{hx:7.0f} {hy:7.0f} {p["head_wh"][0]:3d}x{p["head_wh"][1]:2d} {p["robe"][0]:7.0f} {p["robe"][1]:7.0f}'
    else:
        v = float("nan")
        hs = "      -       -       -       -       -"
    if an:
        lines.append(f'{r["sec"]:4d} {hs} {an["side"]:>4} {an["x"]:7.0f} {an["y"]:7.0f} '
                     f'{an["gap"]:4.0f} {v:6.1f}')
    else:
        lines.append(f'{r["sec"]:4d} {hs}    -       -       -     - {v:6.1f}')

open(os.path.join(ROOT, "calib", "track2_table.txt"), "w", encoding="utf-8").write("\n".join(lines))
print("\n".join(lines))
