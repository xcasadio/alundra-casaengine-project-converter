"""dll_had.py - the decision tree of the DLL's AlundraEnemyHelpers.HandleAnimationDirection (AlundraEnemyHelpers.cs:95-170, transcribed) against the binary's CanMoveForward 0x80080260
(model verified on the real code): a CanMoveForward helper for the DLL can reuse that decision tree, minus the Turn."""
import models as M
from h import Ent, M32

def dll_decision(thr, t, direction):
    a, b, d, c = thr < t[0], thr < t[1], thr < t[2], thr < t[3]
    inr = lambda dr, lo, cnt: ((dr - lo) & M32) < cnt
    state = 0x19C
    if a:
        if b:
            if inr(direction, 9, 15):
                return True
        else:
            state = 0x1CC
    if state == 0x19C:
        if not b:
            state = 0x1CC
        elif not c:
            state = 0x1F8
        else:
            if inr(direction, 0x11, 15):
                return True
            state = 0x1CC
    if state == 0x1CC:
        if c:
            if not d:
                return False
            if not inr(direction, 8, 0x11):
                return True
    if not d or not a:
        return False
    return direction < 0x10

bad = 0; n = 0
import itertools
for floor in (0x300000,):
    for t in itertools.product((-3, 0, 1, 2, 5), repeat=4):
        for dist in (0, 4):
            for d in range(32):
                e = Ent(); e.set32(M.FLOOR, floor); e.set32(M.TDIR, d)
                for k in range(4): e.set32(M.MH[k], floor - t[k])
                a = bool(M.can_move_forward(e, dist)); b = dll_decision(dist + 1, t, d)
                n += 1; bad += (a != b)
print('DLL decision tree vs binary CanMoveForward: %d differences in %d cases' % (bad, n))
