"""decomp_cmf.py - the decompilation's CanMoveForward (FunctionTypeC.cs:10796-10857, transcribed verbatim, uint arithmetic where the C# fields are uint)
compared with the binary's 0x80080260 (the model verified against the real code in vec.py) on every pattern of the four corners x 32 directions.
Prints the number of differing cases and a few examples."""
import models as M
from h import Ent, M32

def decomp(floor, mh, tdir, z_offset):
    adj = z_offset + 1
    f0, f1, f2 = floor - mh[0], floor - mh[1], floor - mh[2]
    f3 = floor - mh[3]
    u = lambda x: x & M32
    check_south = True
    if adj < f0:
        check_south = False
        if adj < f1:
            if u(tdir - 9) < 0xF:
                return 1
            check_south = True
    if check_south:
        if adj < f1:
            if f3 <= adj:
                pass  # goto SkipAllChecks
                return skip(f0, f2, adj, tdir)
            if u(tdir - 0x11) < 0xF:
                return 1
    if adj < f3:
        if f2 <= adj:
            return 0
        if 0x10 < u(tdir - 8):
            return 1
    return skip(f0, f2, adj, tdir)

def skip(f0, f2, adj, tdir):
    result = 0
    if f2 <= adj or adj < f0:
        result = 1
        if 0xF < (tdir & M32):
            result = 0
    return result

diff = 0; total = 0; ex = []
for pat in range(16):
    for d in range(32):
        for dist in (0, 1, 0x100000):
            floor = 0x300000
            thr = dist + 1
            mh = [floor - (thr + 6) if (pat >> k) & 1 else floor - max(thr - 1, 0) for k in range(4)]
            e = Ent(); e.set32(M.FLOOR, floor); e.set32(M.TDIR, d)
            for k in range(4): e.set32(M.MH[k], mh[k])
            a = M.can_move_forward(e, dist); b = decomp(floor, mh, d, dist)
            total += 1
            if a != b:
                diff += 1
                if len(ex) < 12: ex.append((format(pat, '04b')[::-1] + ' (b0..b3)', 'dir %d' % d, 'dist %d' % dist, 'binary %d' % a, 'decompilation %d' % b))
print('%d of %d cases differ' % (diff, total))
for x in ex: print(x)

# breakdown by corner pattern (b0 b1 b2 b3: 1 = that corner is lower than the floor by more than dist + 1)
from collections import Counter
by = Counter(); tot = Counter()
for pat in range(16):
    for d in range(32):
        for dist in (0,):
            floor = 0x300000
            thr = dist + 1
            mh = [floor - (thr + 6) if (pat >> k) & 1 else floor - max(thr - 1, 0) for k in range(4)]
            e = Ent(); e.set32(M.FLOOR, floor); e.set32(M.TDIR, d)
            for k in range(4): e.set32(M.MH[k], mh[k])
            a = M.can_move_forward(e, dist); b = decomp(floor, mh, d, dist)
            tot[pat] += 1
            if a != b: by[pat] += 1
print('dist 0, per pattern (b0 b1 b2 b3): differing directions of 32')
for pat in range(16):
    print(format(pat, '04b')[::-1], by[pat], '/', tot[pat])
