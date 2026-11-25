import os
from PIL import Image, ImageDraw, ImageFont

base = r"D:\Unity Project\DesktopIterator\Docs\ref\ss-arm"
fr = os.path.join(base, "frames")
files = sorted(os.listdir(fr))
files = [f for f in files if f.endswith(".png")]
print(len(files))

# quick overview sheet for my own inspection
TILE = 180
COLS, ROWS = 6, 6
sheet = Image.new("RGB", (COLS * TILE, ROWS * TILE), (20, 20, 20))
d = ImageDraw.Draw(sheet)
for i, f in enumerate(files):
    im = Image.open(os.path.join(fr, f)).convert("RGB").resize((TILE, TILE), Image.LANCZOS)
    x = (i % COLS) * TILE
    y = (i // COLS) * TILE
    sheet.paste(im, (x, y))
    d.text((x + 3, y + 3), f[1:6], fill=(255, 255, 0))
sheet.save(os.path.join(base, "_overview.png"))
print("overview saved", sheet.size)
