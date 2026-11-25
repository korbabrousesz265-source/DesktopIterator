import os
from PIL import Image, ImageDraw

base = r"D:\Unity Project\DesktopIterator\Docs\ref\ss-arm"
fr = os.path.join(base, "frames")

crops = [
    ("t06.00.png", (262, 282, 320, 330), "elbow_t06_left"),
    ("t06.00.png", (392, 322, 450, 366), "disc_t06_mid"),
    ("t15.00.png", (415, 275, 455, 315), "disc_t15_a"),
    ("t15.00.png", (330, 335, 370, 375), "disc_t15_b"),
    ("t04.00.png", (255, 300, 320, 360), "elbow_t04"),
]
tiles = []
for f, box, tag in crops:
    im = Image.open(os.path.join(fr, f)).convert("RGB").crop(box)
    im = im.resize((im.width * 8, im.height * 8), Image.LANCZOS)
    tiles.append((tag, box, im))

W = sum(t[2].width + 10 for t in tiles)
H = max(t[2].height for t in tiles) + 20
sheet = Image.new("RGB", (W, H), (0, 0, 0))
d = ImageDraw.Draw(sheet)
x = 0
for tag, box, im in tiles:
    d.text((x + 2, 3), tag, fill=(0, 255, 0))
    sheet.paste(im, (x, 18))
    x += im.width + 10
sheet.save(os.path.join(base, "_joints.png"))
print(sheet.size)
