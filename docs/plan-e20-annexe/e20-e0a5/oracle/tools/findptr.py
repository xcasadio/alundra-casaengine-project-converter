import sys, os, struct
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lib
# find 32-bit words equal to given addresses in the image (jump/handler tables)
for t in sys.argv[1:]:
    t = int(t, 16)
    n = (len(lib.DATA) - lib.OFF) // 4
    hits = []
    for i in range(n):
        if struct.unpack_from('<I', lib.DATA, lib.OFF + 4 * i)[0] == t:
            hits.append(lib.BASE + 4 * i)
    print('%08X referenced as data at: %s' % (t, ' '.join('%08X' % a for a in hits)))
