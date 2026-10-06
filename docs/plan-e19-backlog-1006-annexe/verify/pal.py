import struct
base = r"D:\development\repo\Alundra Remake\Alundra (France)\Alundra (France)_extracted\TAKI\SCREEN"
tim = open(base + r"\FONT3.TIM", "rb").read()
cl = open(base + r"\WIND.CL", "rb").read()
magic, flags = struct.unpack_from("<II", tim, 0)
pos = 8
bnum, x, y, w, h = struct.unpack_from("<IHHHH", tim, pos)
print("clut block", bnum, x, y, w, h)
vals = struct.unpack_from("<%dH" % ((bnum-12)//2), tim, pos+12)
pals = [vals[p*w:(p+1)*w] for p in range(h)]
pos2 = pos + bnum
ib, ix, iy, iw, ih = struct.unpack_from("<IHHHH", tim, pos2)
print("image block", ib, ix, iy, iw, ih, "data offset", pos2+12, "total len", len(tim))
def rep(c): return (c<<3)|(c>>2)
def rgb(v): return (rep(v&31), rep((v>>5)&31), rep((v>>10)&31), v>>15)
wind = struct.unpack("<256H", cl[:512])
print("FONT3 clut0:", [rgb(v)[:3] for v in pals[0]])
for p in range(16):
    pw = wind[p*16:(p+1)*16]
    diff = [i for i in range(16) if pw[i] != pals[0][i]]
    print("WIND pal", p, "differs from FONT3 clut0 at", diff)
print("pal8 :", [rgb(v)[:3] for v in wind[8*16:9*16]])
