import os
from PIL import Image, ImageDraw, ImageFont

base = r"D:\Unity Project\DesktopIterator\Docs\ref\ss-arm\randomgods"
FR = os.path.join(base, "frames")
files = sorted(f for f in os.listdir(FR) if f.endswith(".png"))

TILE_W, TILE_H = 320, 180
LAB = 18
COLS, ROWS = 6, 5
PER = COLS * ROWS


def font(sz):
    for p in (r"C:\Windows\Fonts\arialbd.ttf", r"C:\Windows\Fonts\segoeuib.ttf"):
        if os.path.exists(p):
            return ImageFont.truetype(p, sz)
    return ImageFont.load_default(size=sz)


f_h = font(20)
f_l = font(13)
n = 0
for s in range(0, len(files), PER):
    chunk = files[s:s + PER]
    W = COLS * TILE_W
    H = 40 + ROWS * (TILE_H + LAB)
    sheet = Image.new("RGB", (W, H), (12, 12, 14))
    d = ImageDraw.Draw(sheet)
    d.text((10, 10), "Random Gods wallpaper  contact %d/4   t=%s - %s   (1920x1080 src, 1 frame / 19 s)" % (
        s // PER + 1, chunk[0][1:8] + " s", chunk[-1][1:8] + " s"),
        fill=(235, 235, 235), font=f_h)
    for i, fn in enumerate(chunk):
        im = Image.open(os.path.join(FR, fn)).convert("RGB").resize((TILE_W, TILE_H), Image.LANCZOS)
        x = (i % COLS) * TILE_W
        y = 40 + (i // COLS) * (TILE_H + LAB)
        sheet.paste(im, (x, y + LAB))
        d.rectangle([x, y, x + TILE_W - 1, y + LAB - 1], fill=(0, 0, 0))
        d.text((x + 4, y + 3), "t=%ss" % fn[1:8], fill=(255, 210, 90), font=f_l)
        d.rectangle([x, y, x + TILE_W - 1, y + LAB + TILE_H - 1], outline=(60, 60, 60))
    n += 1
    out = os.path.join(base, "contact_%d.png" % n)
    sheet.save(out)
    print(out, sheet.size)
