"""find jal callers of given targets (hex) in ALUN_CD.EXE; also data words equal to target. Read-only."""
import sys, struct
import exelib as e
D = e.DATA
BASE = 0x80020000
N = (len(D)-0x800)//4
def scan(targets):
    res = {t: [] for t in targets}
    dat = {t: [] for t in targets}
    for i in range(N):
        w = struct.unpack_from('<I', D, 0x800+4*i)[0]
        a = BASE+4*i
        if w >> 26 in (2,3):
            t = (((a+4) & 0xF0000000) | ((w & 0x3FFFFFF) << 2))
            if t in res: res[t].append((a, 'jal' if w>>26==3 else 'j'))
        if w in dat: dat[w].append(a)
    return res, dat
if __name__ == '__main__':
    ts = [int(x,16) for x in sys.argv[1:]]
    r, d = scan(ts)
    for t in ts:
        print(hex(t), 'calls:', [(hex(a), k) for a,k in r[t]], 'data:', [hex(a) for a in d[t]])
