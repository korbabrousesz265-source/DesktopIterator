import subprocess, json, sys
import numpy as np

FF = r"C:\Users\47317\AppData\Local\Programs\Python\Python312\Lib\site-packages\imageio_ffmpeg\binaries\ffmpeg-win-x86_64-v9.0.2.exe"
SRC = r"D:\SteamLibrary\steamapps\workshop\content\431960\3024177030\Random Gods.mp4"
OUT = r"D:\Unity Project\DesktopIterator\Docs\ref\ss-arm\randomgods\qc\scan_lowres.npy"
W, H = 320, 180

cmd = [FF, "-hide_banner", "-loglevel", "error", "-i", SRC,
       "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", "%dx%d" % (W, H), "-"]
p = subprocess.Popen(cmd, stdout=subprocess.PIPE)
rows = []
i = 0
size = W * H * 3
while True:
    buf = p.stdout.read(size)
    if len(buf) < size:
        break
    a = np.frombuffer(buf, np.uint8).reshape(H, W, 3).astype(np.int16)
    R, G, B = a[:, :, 0], a[:, :, 1], a[:, :, 2]
    # SS robe orange (bright orange-red)
    o = (R > 150) & (G > 45) & (G < 165) & (B < 85) & (R - B > 95) & (R - G > 45)
    # warm pale rod (arm): light warm tan
    rod = (R > 120) & (R - B >= 12) & (R - B <= 70) & (np.abs(G - B) <= 26) & (G > 95)
    ys, xs = np.nonzero(o)
    ay, ax = np.nonzero(rod)
    rows.append([i,
                 int(o.sum()), int(rod.sum()),
                 float(xs.mean()) if len(xs) else -1.0, float(ys.mean()) if len(ys) else -1.0,
                 float(xs.std()) if len(xs) else -1.0, float(ys.std()) if len(ys) else -1.0,
                 float(R.mean()), float(G.mean()), float(B.mean())])
    i += 1
p.stdout.close()
p.wait()
arr = np.array(rows, dtype=np.float32)
np.save(OUT, arr)
print("frames scanned:", len(arr), " shape:", arr.shape)
print("orange px: max %d mean %.1f" % (arr[:, 1].max(), arr[:, 1].mean()))
print("rod px:    max %d mean %.1f" % (arr[:, 2].max(), arr[:, 2].mean()))
