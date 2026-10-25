import sys
sys.path.insert(0, r'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/theme2-disc/critic')
import lib
def refs(hi, lo):
    out = []
    for i in range(lib.N):
        x = lib.W[i]
        if (x >> 26) == 0xF and (x & 0xFFFF) == hi:  # lui rt, hi
            rt = (x >> 16) & 31
            for j in range(1, 8):
                if i + j >= lib.N: break
                y = lib.W[i + j]
                op = y >> 26
                if op in (0x23, 0x2B, 0x21, 0x25, 0x20, 0x24, 0x29, 0x28, 0x09) and ((y >> 21) & 31) == rt:
                    off = y & 0xFFFF
                    if off & 0x8000: off -= 0x10000
                    if (hi << 16) + off == lo:
                        out.append((lib.BASE + 4 * (i + j), {0x23:'lw',0x2B:'sw',0x21:'lh',0x25:'lhu',0x20:'lb',0x24:'lbu',0x29:'sh',0x28:'sb',0x09:'addiu'}[op]))
    return out
lo = int(sys.argv[1], 16)
hi = lo >> 16
if (lo & 0x8000): hi += 1  # compensation not needed for our probing, try both
for h in (lo >> 16, (lo >> 16) + 1):
    for a, k in refs(h, lo):
        f = lib.func_of(a)
        print('%s at %x in func %x' % (k, a, f))
