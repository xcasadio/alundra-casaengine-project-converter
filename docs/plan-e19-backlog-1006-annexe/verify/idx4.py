import struct, numpy as np
from PIL import Image
base = r"D:\development\repo\Alundra Remake\Alundra (France)\Alundra (France)_extracted\TAKI\SCREEN"
tim = open(base + r"\FONT3.TIM", "rb").read()
data = np.frombuffer(tim[64:64+32768], dtype=np.uint8)
px = np.zeros((256,256), np.uint8)
for y in range(256):
    row = data[y*128:(y+1)*128]
    px[y,0::2] = row & 15
    px[y,1::2] = row >> 4
print("hist", np.bincount(px.ravel(), minlength=16).tolist())
ys, xs = np.nonzero(px == 4)
print("index4 texels", len(ys))
# glyph cell geometry? print distribution by 16x16 cells
cells = {}
for y,x in zip(ys,xs):
    cells.setdefault((y//16)*16 + x//16, 0)
    cells[(y//16)*16 + x//16] += 1
print(sorted(cells.items()))
# compare with extracted PNG
for path in [r"D:\development\repo\alundra-casaengine-project-converter\data-extracted\ui\font3.png",
             r"D:\development\repo\Alundra Remake\remaster-data-extracted\ui\font3.png",
             r"D:\development\repo\alundra-casaengine-project-converter\alundra-project\UI\Textures\font3.png"]:
    try:
        p = np.array(Image.open(path).convert("RGBA"))
    except Exception as e:
        print(path, "ERR", e); continue
    print(path, p.shape)
    if p.shape[:2] == (256,256):
        cols = {}
        for i in range(16):
            m = px == i
            if m.any():
                u = {tuple(c) for c in p[m]}
                print("  idx", i, int(m.sum()), sorted(u)[:3])
