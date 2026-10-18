"""b4_tables.py - brick (4): dump the 256-entry sin/cos tables (0x800204F0 / 0x800206F0), the 32-direction tables (0x80023654 / 0x80023694) and the slide tables
(0x80028B94 / 0x80028BD4); closed-form checks; comparison with the DLL's AnimationTables.cs (read-only). Writes ../vec/tables.json."""
import json, math, re, struct, os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import tools
def h(a, n): return [struct.unpack_from('<h', tools.DATA, tools.off(a + 2 * i))[0] for i in range(n)]
def w(a, n): return [struct.unpack_from('<i', tools.DATA, tools.off(a + 4 * i))[0] for i in range(n)]
sin256, cos256 = h(0x800204F0, 256), h(0x800206F0, 256)
x32, y32 = h(0x80023654, 32), h(0x80023694, 32)
sx, sy = w(0x80028B94, 16), w(0x80028BD4, 16)
T = {
 "sin256": {"addr": "0x800204F0", "type": "int16[256]", "values": sin256},
 "cos256": {"addr": "0x800206F0", "type": "int16[256]", "values": cos256},
 "dir32_x": {"addr": "0x80023654", "type": "int16[32]", "values": x32},
 "dir32_y": {"addr": "0x80023694", "type": "int16[32]", "values": y32},
 "slide_x": {"addr": "0x80028B94", "type": "int32[16]", "values": sx},
 "slide_y": {"addr": "0x80028BD4", "type": "int32[16]", "values": sy},
}
print("cos256[i] == sin256[(i+64)&255] for all i:", all(cos256[i] == sin256[(i + 64) & 255] for i in range(256)))
print("sin256 odd symmetry sin[i+128] == -sin[i]:", all(sin256[(i + 128) & 255] == -sin256[i] for i in range(256)))
print("sin256 mirror sin[128-i]... sin[64+k]==sin[64-k]:", all(sin256[64 + k] == sin256[64 - k] for k in range(0, 65)))
for name, f in (("trunc", math.trunc), ("round", round), ("floor", math.floor)):
    ok = all(f(256 * math.sin(2 * math.pi * i / 256)) == sin256[i] for i in range(256))
    print("sin256[i] == %s(256*sin(2*pi*i/256)):" % name, ok)
    bad = [i for i in range(256) if f(256 * math.sin(2 * math.pi * i / 256)) != sin256[i]]
    if bad: print("   mismatches", len(bad), "first", bad[:8], [(sin256[i], f(256 * math.sin(2 * math.pi * i / 256))) for i in bad[:4]])
for name, f in (("trunc", math.trunc), ("round", round)):
    okx = all(f(-768 * math.sin(2 * math.pi * d / 32)) == x32[d] for d in range(32))
    oky = all(f(512 * math.cos(2 * math.pi * d / 32)) == y32[d] for d in range(32))
    print("dir32 x == %s(-768 sin(2 pi d/32)):" % name, okx, "; y == %s(512 cos):" % name, oky)
print("slide tables: X nonzero idx", [i for i, v in enumerate(sx) if v], "Y nonzero idx", [i for i, v in enumerate(sy) if v])
# DLL AnimationTables.cs (read only)
p = r"D:/development/repo/alundra-casaengine-project-converter/Alundra/Entities/AnimationTables.cs"
src = open(p, encoding='utf-8-sig').read()
def grab(name):
    i = src.index("short[] " + name + " =")
    j = src.index("};", i)
    blk = src[i:j]
    toks = re.findall(r"0x[0-9a-fA-F]+", blk)
    vals = [int(t, 16) for t in toks]
    return [v - 0x10000 if v & 0x8000 else v for v in vals]
for nm, ref in (("OffsetXList", x32), ("OffsetYList", y32)):
    g = grab(nm)
    print("DLL AnimationTables.%s:" % nm, "found %s values" % (len(g) if g else None), "equal to the binary table:", g == ref)
os.makedirs(os.path.join(HERE, '..', 'vec'), exist_ok=True)
json.dump(T, open(os.path.join(HERE, '..', 'vec', 'tables.json'), 'w'), indent=0)
