"""handlers.py - names of the native handler entry points (slots A..F, 0x80098F4C master table) of ALUN_CD.EXE; nearest(addr) gives the handler whose entry is the closest below addr.
Read-only. Usage: python -B handlers.py ADDR [ADDR..]"""
import sys, exelib
NAMES = 'ABCDEF'
H = {}
for s in range(6):
    base = exelib.slot_table_base(s)
    if not base:
        continue
    for code in range(0, 200):
        try:
            a = exelib.u32(base + 4 * code)
        except Exception:
            break
        if 0x80020000 <= a < 0x800C0000:
            H.setdefault(a, []).append('%s%d' % (NAMES[s], code))
KEYS = sorted(H)

def nearest(addr):
    best = None
    for k in KEYS:
        if k <= addr:
            best = k
        else:
            break
    return best, H.get(best)

if __name__ == '__main__':
    for x in sys.argv[1:]:
        a = int(x, 16)
        b, n = nearest(a)
        print('%#010x -> %s' % (a, ('handler entry %#010x %s (+%#x)' % (b, n, a - b)) if b else 'below the first handler entry'))
