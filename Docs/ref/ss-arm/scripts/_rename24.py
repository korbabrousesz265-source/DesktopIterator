import os, re

m = r"D:\Unity Project\DesktopIterator\Docs\ref\ss-arm\frames_24fps"
starts = {"tr1": 1.0, "tr2": 5.5, "tr3": 11.5}
for f in sorted(os.listdir(m)):
    mm = re.match(r"(tr\d)_(\d{4})\.png", f)
    if not mm:
        continue
    tag, n = mm.group(1), int(mm.group(2))
    t = starts[tag] + (n - 1) / 24.0
    new = "%s_t%06.3f.png" % (tag, t)
    os.replace(os.path.join(m, f), os.path.join(m, new))
print(len(os.listdir(m)))
print(sorted(os.listdir(m))[:3], sorted(os.listdir(m))[-2:])
