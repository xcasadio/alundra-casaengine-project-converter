import struct, sys
from PIL import Image
import numpy as np

base = r"D:\development\repo\Alundra Remake\Alundra (France)\Alundra (France)_extracted\TAKI\SCREEN"
tim = open(base + r"\FONT3.TIM", "rb").read()
cl = open(base + r"\WIND.CL", "rb").read()

# TIM header
magic, flags = struct.unpack_from("<II", tim, 0)
print("magic", hex(magic), "flags", hex(flags))
pos = 8
bpp = flags & 3
has_clut = (flags >> 3) & 1
print("bpp mode", bpp, "has_clut", has_clut)
clut_entries = []
if has_clut:
    bnum, x, y, w, h = struct.unpack_from("<IHHHH", tim, pos)
    print("clut block", bnum, x, y, w, h)
    data = tim[pos + 12: pos + bnum]
    vals = struct.unpack_from("<%dH" % (len(data) // 2), data)
    # h palettes of w entries
    for p in range(h):
        clut_entries.append(vals[p * w:(p + 1) * w])
    pos += bnum
bnum, x, y, w, h = struct.unpack_from("<IHHHH", tim, pos)
print("image block", bnum, x, y, w, h)
img = tim[pos + 12: pos + bnum]
print("n palettes in tim:", len(clut_entries), "entries each", len(clut_entries[0]) if clut_entries else 0)


def c5(v):
    return (v & 31, (v >> 5) & 31, (v >> 10) & 31, (v >> 15) & 1)


def rep(c):
    return (c << 3) | (c >> 2)


wind = struct.unpack_from("<%dH" % (len(cl) // 2), cl)
print("wind.cl entries", len(wind), "-> palettes", len(wind) // 16)
pal8 = wind[8 * 16:9 * 16]
tp0 = clut_entries[0]
print("idx  FONT3.TIM clut0 (r5,g5,b5,stp)   WIND.CL pal8   equal")
for i in range(16):
    a = c5(tp0[i]); b = c5(pal8[i])
    print(i, a, b, a == b, [rep(x) for x in a[:3]], [rep(x) for x in b[:3]])

# decode 4bpp image: width in 16-bit words w -> pixels = w*4 for 4bpp
if bpp == 0:
    pw = w * 4
    ph = h
    px = np.zeros((ph, pw), dtype=np.uint8)
    arr = np.frombuffer(img, dtype=np.uint8)
    for yy in range(ph):
        row = arr[yy * w * 2:(yy + 1) * w * 2]
        px[yy, 0::2] = row & 15
        px[yy, 1::2] = row >> 4
    print("pixels", px.shape, "index histogram", np.bincount(px.ravel(), minlength=16).tolist())
    np.save("font3_idx.npy", px)

# compare with data-extracted font3.png
p = Image.open(r"D:\development\repo\alundra-casaengine-project-converter\data-extracted\ui\font3.png")
print("png mode", p.mode, p.size)
pa = np.array(p.convert("RGBA"))
# map each index to its extracted colour
cols = {}
for i in range(16):
    m = (px == i)
    if m.any():
        uniq = {tuple(c) for c in pa[m]}
        cols[i] = uniq
        print("idx", i, "count", int(m.sum()), "png colours", list(uniq)[:3])
