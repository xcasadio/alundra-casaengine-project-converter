"""Find every `jal TARGET` in the EXE text (read-only). Usage: python -B findcalls.py HEX [HEX...]"""
import sys, struct
import exelib
targets = [int(a,16) for a in sys.argv[1:]]
text_start = exelib.T_ADDR
n = (len(exelib.DATA) - 0x800)//4
res = {t: [] for t in targets}
for i in range(n):
    w = struct.unpack_from("<I", exelib.DATA, 0x800 + 4*i)[0]
    if (w >> 26) == 3:
        tgt = ((w & 0x3ffffff) << 2) | 0x80000000
        if tgt in res:
            res[tgt].append(text_start + 4*i)
TABLE = 0x80098FAC
H = sorted({exelib.u32(TABLE+4*o):o for o in range(256)}.items())
def owner(a):
    best=None
    for h,o in H:
        if h<=a: best=(h,o)
    return best
for t,v in res.items():
    print(hex(t), [hex(x) for x in v])
    for x in v:
        b = owner(x)
        print("   caller", hex(x), "within handler of opcode", hex(b[1]) if b else None, hex(b[0]) if b else None)
