"""Own counter-check of the binary facts about the tables. Run from this folder: python -B tables_check.py (uses the exelib.py copy next to it, read-only)."""
import exelib, math

def tbl(base):
    return [exelib.s16(base + 2 * i) for i in range(256)]

sin, cos = tbl(0x800204F0), tbl(0x800206F0)
tr = [int(256 * math.sin(2 * math.pi * i / 256)) for i in range(256)]
rd = [round(256 * math.sin(2 * math.pi * i / 256)) for i in range(256)]
print("sin 256 shorts: [0,1,64,128,192] =", [sin[i] for i in (0, 1, 64, 128, 192)])
print("trunc(256 sin) matches %d/256, round matches %d/256" % (sum(a == b for a, b in zip(sin, tr)), sum(a == b for a, b in zip(sin, rd))))
print("cos[i] == sin[(i+64)&255]:", all(cos[i] == sin[(i + 64) & 255] for i in range(256)))
print("sin[(i+128)&255] == -sin[i]:", all(sin[(i + 128) & 255] == -sin[i] for i in range(256)))
print("force tables indexed by (+0x188 & 0xF) in 0x800366FC:")
for base, nm in ((0x80028B94, "X (0x80028B94)"), (0x80028BD4, "Y (0x80028BD4)")):
    print("  ", nm, [(i, exelib.s32(base + 4 * i) if hasattr(exelib, "s32") else (lambda v: v - (1 << 32) if v >> 31 else v)(exelib.u32(base + 4 * i))) for i in range(16)])
