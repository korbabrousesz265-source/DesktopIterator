import os
from PIL import Image, ImageDraw, ImageFont

base = r"D:\Unity Project\DesktopIterator\Docs\ref\ss-arm\randomgods"
raw = os.path.join(base, "_raw")
files = sorted(f for f in os.listdir(raw) if f.startswith("s_"))
STEP = 570 / 30.0  # seconds between survey frames


def font(sz):
    for p in (r"C:\Windows\Fonts\arialbd.ttf", r"C:\Windows\Fonts\segoeuib.ttf"):
        if os.path.exists(p):
            return ImageFont.truetype(p, sz)
    return ImageFont.load_default(size=sz)


TILE = 192
LAB = 16
COLS, ROWS = 8, 5
f = font(12)
per = COLS * ROWS
n = 0
for s in range(0, len(files), per):
    chunk = files[s:s + per]
    sheet = Image.new("RGB", (COLS * TILE, ROWS * (TILE + LAB)), (10, 10, 10))
    d = ImageDraw.Draw(sheet)
    for i, fn in enumerate(chunk):
        t = (s + i) * STEP
        im = Image.open(os.path.join(raw, fn)).convert("RGB").resize((TILE, TILE), Image.LANCZOS)
        x = (i % COLS) * TILE
        y = (i // COLS) * (TILE + LAB)
        sheet.paste(im, (x, y + LAB))
        d.text((x + 2, y + 2), "%d s" % int(t), fill=(255, 220, 90), font=f)
    n += 1
    sheet.save(os.path.join(base, "qc", "survey_%d.png" % n))
print("sheets:", n, "frames:", len(files))
