"""Head-disc sub-pixel fit v2 (centre refined, fixed sprite aspect).

Steps per frame:
  1. pinkness m = B - 0.8*G, window 80x80 around a seed
  2. threshold T = (background p25 + inside p75) / 2
  3. radial rays every 1 deg: outermost downward crossing of T = edge point
  4. iterate: centre = mean of the edge points, drop outliers (>2 sigma of the
     normalised radius), until the centre moves < 0.05 px
  5. final ellipse: axes keep a/b = 1.2222 (= (5/9)/(5/11)), solved in closed form
     min over scale s of  median |sqrt((x/(s*A))^2 + (y/(s*B))^2) - 1|
  6. free-aspect ellipse as a cross-check

usage: python head_fit2.py [t060 t112 ...]
"""
import json, os, sys
import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ASPECT = (5.0 / 9.0) / (5.0 / 11.0)
OUT = os.path.join(ROOT, "calib", "head_fit2.json")

SEEDS = {
    "t003": (981, 604), "t007": (872, 478), "t024": (784, 841), "t034": (1698, 897),
    "t043": (1642, 888), "t060": (1490, 828), "t067": (906, 764), "t098": (1724, 412),
    "t111": (1477, 921), "t112": (1003, 806), "t129": (1646, 890), "t040": (1690, 1024),
}


def pinkness(im):
    a = np.asarray(im.convert("RGB")).astype(np.float32)
    return a[:, :, 2] - 0.8 * a[:, :, 1]


def edge_points(m, cx, cy, T, rmin=7.0, rmax=22.0, step=0.05):
    pts = []
    for deg in range(0, 360, 1):
        ang = np.deg2rad(deg)
        ca, sa = np.cos(ang), np.sin(ang)
        prev = None
        for r in np.arange(rmax, rmin, -step):
            x, y = cx + ca * r, cy + sa * r
            xi, yi = int(round(x)), int(round(y))
            if xi < 0 or yi < 0 or yi >= m.shape[0] or xi >= m.shape[1]:
                break
            v = float(m[yi, xi])
            if prev is not None and prev[0] < T <= v:
                r_out, v_out = prev[1], prev[0]
                rr = r_out + (T - v_out) / (v - v_out) * (r - r_out) if v != v_out else r_out
                pts.append((deg, cx + ca * rr, cy + sa * rr, rr))
                break
            prev = (v, r)
    return pts


def fit(name, sx, sy):
    im = Image.open(os.path.join(ROOT, "frames", name + ".png")).convert("RGB")
    m = pinkness(im)
    win = m[sy - 40:sy + 40, sx - 40:sx + 40]
    bg = float(np.percentile(win, 30))
    hot = win[win > bg + 22]
    pl = float(np.percentile(hot, 80)) if hot.size else bg + 60
    T = (bg + pl) / 2.0

    cx, cy = float(sx), float(sy)
    for it in range(6):
        pts = edge_points(m, cx, cy, T)
        if len(pts) < 20:
            break
        X = np.array([[p[1] - cx, p[2] - cy] for p in pts])
        rr = np.array([p[3] for p in pts])
        q = np.hypot(X[:, 0] / (np.median(rr) * 1.22), X[:, 1] / np.median(rr))
        res = np.abs(q - 1)
        keep = res < max(0.35, np.percentile(res, 85))
        X = X[keep]
        ncx, ncy = cx + X[:, 0].mean(), cy + X[:, 1].mean()
        moved = np.hypot(ncx - cx, ncy - cy)
        cx, cy = ncx, ncy
        if moved < 0.05:
            break

    pts = edge_points(m, cx, cy, T)
    X = np.array([[p[1] - cx, p[2] - cy] for p in pts])
    r = np.hypot(X[:, 0], X[:, 1])
    keep = np.abs(r - np.median(r)) < max(1.5, 2.2 * np.std(r))
    X = X[keep]
    # fixed-aspect scale
    A = np.array([np.cos(np.arctan2(1, ASPECT)), 0])
    best = None
    for s in np.arange(9.0, 20.0, 0.01):
        a, b = s, s / ASPECT
        q = np.sqrt((X[:, 0] / a) ** 2 + (X[:, 1] / b) ** 2)
        c = float(np.median(np.abs(q - 1.0)))
        if best is None or c < best[0]:
            best = (c, a, b)
    # free aspect least squares: x^2 u + y^2 v = 1
    M = np.stack([X[:, 0] ** 2, X[:, 1] ** 2], 1)
    sol, *_ = np.linalg.lstsq(M, np.ones(len(X)), rcond=None)
    fa = 1 / np.sqrt(sol[0]) if sol[0] > 0 else float("nan")
    fb = 1 / np.sqrt(sol[1]) if sol[1] > 0 else float("nan")
    return dict(frame=name, cx=round(cx, 2), cy=round(cy, 2),
                a=round(best[1], 2), b=round(best[2], 2),
                width=round(2 * best[1], 2), height=round(2 * best[2], 2),
                fit_err=round(best[0], 4), rays=len(X),
                free_w=round(2 * fa, 2), free_h=round(2 * fb, 2),
                free_aspect=round(fa / fb, 3) if fb == fb else None,
                bg=round(bg, 1), plateau=round(pl, 1), T=round(T, 1))


names = sys.argv[1:] or list(SEEDS)
res = []
for n in names:
    sx, sy = SEEDS[n]
    r = fit(n, sx, sy)
    res.append(r)
    print("%s c=(%.1f,%.1f) W=%.2f H=%.2f err=%.4f | free %.2fx%.2f asp %.3f"
          % (r["frame"], r["cx"], r["cy"], r["width"], r["height"], r["fit_err"],
             r["free_w"], r["free_h"], r["free_aspect"] or -1))

json.dump(res, open(OUT, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
W = np.array([r["width"] for r in res])
H = np.array([r["height"] for r in res])
print("\nmedian W=%.2f H=%.2f  -> scale %.3f / %.3f"
      % (np.median(W), np.median(H), np.median(W) / 11.1111, np.median(H) / 9.0909))
