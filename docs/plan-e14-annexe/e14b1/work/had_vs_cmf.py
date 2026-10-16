"""had_vs_cmf.py - REAL 0x80080144 (HandleAnimationDirection) against REAL 0x80080260 (CanMoveForward) on 16 corner patterns x 32 directions x 3 distances x 3 floors."""
import itertools
from h import World, Ent, ENT0, STRIDE, M32
import models as M
w = World()
n = bad = turned = 0
for t in itertools.product((-3, 0, 1, 2, 5, 0x100001), repeat=4):
    for dist in (0, 4):
        for d in range(0, 32, 1):
            e = Ent(); e.set32(M.FLOOR, 0x300000); e.set32(M.TDIR, d); e.set32(M.TANIM, 99)
            for k in range(4): e.set32(M.MH[k], 0x300000 - t[k])
            w.put(5, e)
            v_cmf = w.call(0x80080260, ENT0 + 5 * STRIDE, dist) & M32
            ent_cmf = bytes(w.get(5).raw)
            v_had = w.call(0x80080144, ENT0 + 5 * STRIDE, 0xC, dist) & M32
            after = w.get(5)
            n += 1
            same_ent = (ent_cmf == bytes(e.raw))      # CanMoveForward must not write the entity
            if v_cmf != v_had or not same_ent:
                bad += 1
            turned += v_had
print('%d cases, %d differences (return value, or CanMoveForward writing the entity); HandleAnimationDirection turned in %d' % (n, bad, turned))
