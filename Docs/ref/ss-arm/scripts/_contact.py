import os, sys
from PIL import Image, ImageDraw, ImageFont

base = r"D:\Unity Project\DesktopIterator\Docs\ref\ss-arm"
fr = os.path.join(base, "frames")
files = sorted(f for f in os.listdir(fr) if f.endswith(".png"))

TILE = 300
LABEL = 26
COLS, ROWS = 4, 3
PER = COLS * ROWS


def get_font(size):
    for p in (r"C:\Windows\Fonts\arialbd.ttf", r"C:\Windows\Fonts\segoeuib.ttf",
              r"C:\Windows\Fonts\msyhbd.ttc", r"C:\Windows\Fonts\arial.ttf"):
        if os.path.exists(p):
            try:
                return ImageFont.truetype(p, size)
            except Exception:
                pass
    return ImageFont.load_default(size)


f_lab = get_font(17)
f_hdr = get_font(22)

sheets = []
for s in range(0, len(files), PER):
    chunk = files[s:s + PER]
    W = COLS * TILE
    H = 46 + ROWS * (TILE + LABEL)
    sheet = Image.new("RGB", (W, H), (14, 16, 18))
    d = ImageDraw.Draw(sheet)
    d.text((10, 12), "ss-arm contact %d/%d   t=%s - %s   (%dx%d src, 2 fps)" % (
        s // PER + 1, (len(files) + PER - 1) // PER,
        chunk[0][1:6] + "s", chunk[-1][1:6] + "s", 720, 720),
        fill=(230, 230, 230), font=f_hdr)
    for i, f in enumerate(chunk):
        im = Image.open(os.path.join(fr, f)).convert("RGB").resize((TILE, TILE), Image.LANCZOS)
        x = (i % COLS) * TILE
        y = 46 + (i // COLS) * (TILE + LABEL)
        sheet.paste(im, (x, y + LABEL))
        d.rectangle([x, y, x + TILE - 1, y + LABEL - 1], fill=(0, 0, 0))
        d.text((x + 5, y + 4), "t=%ss" % f[1:6], fill=(255, 214, 90), font=f_lab)
        d.rectangle([x, y, x + TILE - 1, y + LABEL + TILE - 1], outline=(70, 70, 70))
    out = os.path.join(base, "contact_%d.png" % (s // PER + 1))
    sheet.save(out)
    sheets.append((out, sheet.size))
for s in sheets:
    print(s)
