import os

base = r"D:\Unity Project\DesktopIterator\Docs\ref\ss-arm"
tmp = os.path.join(base, "_raw")
fr = os.path.join(base, "frames")
os.makedirs(fr, exist_ok=True)
files = sorted([f for f in os.listdir(tmp) if f.startswith("raw_")])
print("count:", len(files))
moved = []
for i, f in enumerate(files):
    t = i * 0.5
    name = "t%05.2f.png" % t
    os.replace(os.path.join(tmp, f), os.path.join(fr, name))
    moved.append(name)
print(moved[:6])
print(moved[-3:])
