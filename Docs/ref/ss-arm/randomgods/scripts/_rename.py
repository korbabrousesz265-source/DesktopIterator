import os

base = r"D:\Unity Project\DesktopIterator\Docs\ref\ss-arm\randomgods"
raw = os.path.join(base, "_raw")
fr = os.path.join(base, "frames")
os.makedirs(fr, exist_ok=True)
files = sorted(f for f in os.listdir(raw) if f.startswith("s_"))
STEP = 570 / 30.0
n = 0
for i, f in enumerate(files):
    t = i * STEP
    name = "t%07.2f.png" % t
    os.replace(os.path.join(raw, f), os.path.join(fr, name))
    n += 1
print("moved", n)
print(sorted(os.listdir(fr))[:3], sorted(os.listdir(fr))[-2:])
