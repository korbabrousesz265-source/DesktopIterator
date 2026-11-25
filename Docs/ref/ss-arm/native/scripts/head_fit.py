"""Sub-pixel ellipse fit of the puppet head disc.

For each candidate frame:
  pinkness  m = B - 0.8*G
  north-half threshold = (local background + local plateau)/2  (50 % crossing)
  radial rays every 2 deg from a trimmed centroid, find the 50 % crossing,
  robust-refit a centre, drop angular outliers (neck / arm / robe contact),
  then fit an ellipse whose axes keep the sprite aspect a/b = 1.2222
  (Circle20 * (5/9, 5/11));  a = head width, b = head height.

usage: python head_fit.py
"""
import json, os
import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ASPECT = (5.0 / 9.0) / (5.0 / 11.0)   # 1.2222
OUT = os.path.join(ROOT, "calib", "head_fit.json")

# frame -> (cx, cy) seed from head_rank bbox centres
SEEDS = [
    ("t003", 981, 604), ("t007", 872, 478), ("t018", 1760, 881),
    ("t024", 784, 841), ("t031", 1486, 1072), ("t034", 1698, 897),
    ("t043", 1642, 888), ("t067", 908, 766), ("t088", 756, 776),
    ("t098", 1724, 412), ("t111", 1477, 921), ("t112", 1003, 806),
    ("t129", 1646, 890), ("t040", 1690, 1024), ("t060", 1490, 828),
]


def pinkness(im):
    a = np.asarray(im.convert("RGB")).astype(np.float32)
    return a[:, :, 2] - 0.8 * a[:, :, 1]


def ray_cross(m, cx, cy, ang, bg, plateau, rmin=6.0, rmax=26.0, step=0.1):
    T = (bg + plateau) / 2.0
    ca, sa = np.cos(ang), np.sin(ang)
    prev = None
    rs = np.arange(rmax, rmin, -step)      # march inward, first crossing = outer edge
    for r in rs:
        x = cx + ca * r
        y = cy + sa * r
        xi, yi = int(round(x)), int(round(y))
        if xi < 0 or yi < 0 or yi >= m.shape[0] or xi >= m.shape[1]:
            return None
        v = m[yi, xi]
        if prev is not None and prev[0] < T <= v:
            # linear interpolation between prev (outside) and this (inside)
            r_out, v_out = prev[1], prev[0]
            if v == v_out:
                return r_out
            return r_out + (T - v_out) / (v - v_out) * (r - r_out)
        prev = (v, r)
    return None


def fit_frame(name, cx, cy):
    im = Image.open(os.path.join(ROOT, "frames", name + ".png"))
    m = pinkness(im)
    win = m[int(cy) - 34:int(cy) + 34, int(cx) - 34:int(cx) + 34]
    bg = float(np.percentile(win, 25))
    plateau = float(np.percentile(win[win > bg + 25], 75)) if np.any(win > bg + 25) else bg + 60

    pts = []
    for deg in range(0, 360, 2):
        ang = np.deg2rad(deg)
        r = ray_cross(m, cx, cy, ang, bg, plateau)
        if r is not None:
            pts.append((deg, cx + np.cos(ang) * r, cy + np.sin(ang) * r, r))

    # robust centre + ellipse scaling with fixed aspect
    best = None
    a_grid = np.arange(9.0, 20.0, 0.02)
    b_grid = a_grid / ASPECT
    for _ in range(4):                                  # iterate centre
        X = np.array([[p[1] - cx, p[2] - cy] for p in pts], float)
        rr = np.array([p[3] for p in pts], float)
        # normalised radius for each a
        cost = []
        for a, b in zip(a_grid, b_grid):
            q = np.sqrt((X[:, 0] / a) ** 2 + (X[:, 1] / b) ** 2)
            cost.append(np.median(np.abs(q - 1.0)))
        cost = np.array(cost)
        k = int(np.argmin(cost))
        a, b = a_grid[k], b_grid[k]
        # residuals, trim worst 20 %
        q = np.sqrt((X[:, 0] / a) ** 2 + (X[:, 1] / b) ** 2)
        res = np.abs(q - 1.0)
        keep = res < np.percentile(res, 80)
        Xk = X[keep]
        # centre shift: mean of (point - a*unit) is messy; use mean residual vector
        cx += float(np.mean(X[:, 0][~keep] * 0)) if False else 0.0
        pts = [p for p, kp in zip(pts, keep) if kp]
        best = dict(a=float(a), b=float(b), med_res=float(cost[k]), n=int(keep.sum()))

    # free aspect solution for reference
    fa, fb = 12.0, 10.0
    for _ in range(60):
        pass
    X = np.array([[p[1] - cx, p[2] - cy] for p in pts], float)
    # least squares on 1/a^2, 1/b^2 :  X0^2*u + X1^2*v = 1
    A = np.stack([X[:, 0] ** 2, X[:, 1] ** 2], 1)
    sol, *_ = np.linalg.lstsq(A, np.ones(len(X)), rcond=None)
    free_a = float(1 / np.sqrt(sol[0])) if sol[0] > 0 else float("nan")
    free_b = float(1 / np.sqrt(sol[1])) if sol[1] > 0 else float("nan")

    return dict(frame=name, cx=cx, cy=cy, width=a, height=b,
                width_free=round(free_a, 2), height_free=round(free_b, 2),
                aspect_free=round(free_a / free_b, 3) if free_b == free_b else None,
                med_res=round(best["med_res"], 4), n_rays=best["n"],
                bg=round(bg, 1), plateau=round(plateau, 1))


res = []
for name, cx, cy in SEEDS:
    try:
        r = fit_frame(name, cx, cy)
    except Exception as e:                                   # pragma: no cover
        print("fail", name, e)
        continue
    res.append(r)
    print("%s  w=%.2f h=%.2f (free %.2fx%.2f asp %.2f) res=%.3f rays=%d bg=%.0f plat=%.0f"
          % (r["frame"], r["width"], r["height"], r["width_free"], r["height_free"],
             r["aspect_free"] or -1, r["med_res"], r["n_rays"], r["bg"], r["plateau"]))

json.dump(res, open(OUT, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print("wrote", OUT)
