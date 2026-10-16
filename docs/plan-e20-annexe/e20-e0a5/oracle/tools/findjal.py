import sys, os, struct
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lib
def callers(target):
    w = 0x0C000000 | ((target >> 2) & 0x03FFFFFF)
    out = []
    n = (len(lib.DATA) - lib.OFF) // 4
    for i in range(n):
        if struct.unpack_from('<I', lib.DATA, lib.OFF + 4 * i)[0] == w:
            out.append(lib.BASE + 4 * i)
    return out
if __name__ == '__main__':
    for t in sys.argv[1:]:
        t = int(t, 16)
        print('%08X callers: %s' % (t, ' '.join('%08X' % a for a in callers(t))))
