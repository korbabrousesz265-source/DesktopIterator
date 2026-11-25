import subprocess, sys
import numpy as np

FF = r"C:\Users\47317\AppData\Local\Programs\Python\Python312\Lib\site-packages\imageio_ffmpeg\binaries\ffmpeg-win-x86_64-v9.0.2.exe"
SRC = r"C:\Users\47317\Videos\Rain World\output.mp4"
W = H = 360

cmd = [FF, "-hide_banner", "-loglevel", "error", "-i", SRC,
       "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", "%dx%d" % (W, H), "-"]
p = subprocess.Popen(cmd, stdout=subprocess.PIPE)
res = []
i = 0
while True:
    buf = p.stdout.read(W * H * 3)
    if len(buf) < W * H * 3:
        break
    a = np.frombuffer(buf, np.uint8).reshape(H, W, 3).astype(np.int16)
    R, G, B = a[:, :, 0], a[:, :, 1], a[:, :, 2]
    reg = np.zeros((H, W), bool)
    reg[21:307, 16:344] = True
    o = (R > 165) & (G > 60) & (G < 170) & (B < 95) & (R - B > 100) & reg
    arm = (R > 115) & (R - B >= 12) & (R - B <= 62) & (np.abs(G - B) <= 22) & (G > 95) & reg
    ys, xs = np.nonzero(o)
    ay, ax = np.nonzero(arm)
    rec = [i, -1, -1, len(xs), -1]
    if len(xs):
        rec[1] = xs.mean(); rec[2] = ys.mean()
    top = ay < 39
    if top.sum() > 15:
        rec[4] = np.median(ax[top])
    res.append(rec)
    i += 1
p.stdout.close()
p.wait()
print("frames:", len(res))
arr = np.array(res, dtype=float)
t = arr[:, 0] / 24.0
px, py, n, topx = arr[:, 1], arr[:, 2], arr[:, 3], arr[:, 4]
out = []
for k in range(1, len(arr)):
    if px[k] < 0 or px[k - 1] < 0:
        jump = 999
    else:
        jump = ((px[k] - px[k - 1]) ** 2 + (py[k] - py[k - 1]) ** 2) ** 0.5
    tjump = 999 if (topx[k] < 0 or topx[k - 1] < 0) else abs(topx[k] - topx[k - 1])
    out.append((k, t[k], px[k] * 2, py[k] * 2, jump * 2, topx[k] * 2, tjump * 2))

big = [o for o in out if o[4] > 12]
print("\n--- frame-to-frame puppet jumps > 12 px (720 coords) ---")
for o in big:
    print("frame %3d  t=%.3fs  puppet=(%4.0f,%4.0f)  dPuppet=%6.1f px  topx=%5.0f dTop=%5.1f" % (
        o[0], o[1], o[2], o[3], o[4], o[5], o[6]))

print("\n--- per-second summary (max jump, mean pos) ---")
for s in range(0, 19):
    seg = [o for o in out if s <= o[1] < s + 1]
    if not seg:
        continue
    mx = max(seg, key=lambda o: o[4])
    print("t=%2ds  maxJump=%6.1f px at %.3fs   median puppet x=%5.0f y=%4.0f  medTop=%5.0f" % (
        s, mx[4], mx[1],
        np.median([o[2] for o in seg]), np.median([o[3] for o in seg]),
        np.median([o[5] for o in seg if o[5] > 0] or [-1])))
