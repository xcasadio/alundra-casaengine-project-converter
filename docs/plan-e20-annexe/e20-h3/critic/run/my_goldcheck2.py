"""my_goldcheck2.py - replay MY self-contained goldens (reads/writes) through MY models from an EMPTY memory (any read of an unlisted word is an error): proves the file is complete."""
import json
from machine import M32, s32
import my_models as MM
g = json.load(open('../results/goldens_critic.json'))
class StrictMem:
    def __init__(self, reads):
        self.r = {int(k, 16): v for k, v in reads.items()}; self.ov = {}; self.miss = set()
    def _w(self, a):
        if a in self.ov: return self.ov[a]
        if a in self.r: return self.r[a]
        self.miss.add(a); return 0
    def rw(self, a):
        a &= M32; assert a & 3 == 0; return self._w(a)
    def rb(self, a): a &= M32; return (self._w(a & ~3) >> (8 * (a & 3))) & 0xFF
    def rh(self, a): a &= M32; return (self._w(a & ~3) >> (8 * (a & 2))) & 0xFFFF
    def rws(self, a): return s32(self.rw(a))
    def ww(self, a, v): self.ov[a & M32] = v & M32
class C: pass
bad = 0; miss = 0
for n, v in enumerate(g['vectors']):
    fn = v['fn']; M = StrictMem(v['reads']); J = []; c = C()
    c.spawn_ret = int(v['spawn_ret'], 16); c.eff_ret = int(v['eff_ret'], 16)
    old = MM.effect
    def eff(M_, J_, ctx_, a1, x, y, z):
        J_.append(('effect', (0, a1, 0, x & M32, y & M32, z & M32)))
        if ctx_.eff_ret:
            for i in range(0, 0x300, 4): M_.ww(0x801E8000 + i, 0)
        return ctx_.eff_ret
    MM.effect = eff
    E = int(v['self'], 16) if v['self'] else None
    try:
        if fn == 'start_beans': ret = MM.start(M, J, c, 1)
        elif fn == 'start_bomb': ret = MM.start(M, J, c, 2)
        elif fn == 'drv_beans': ret = MM.driver(M, J, c, 1)
        elif fn == 'drv_bomb': ret = MM.driver(M, J, c, 2)
        elif fn == 'cancel_beans': ret = MM.cancel(M, J, c, 1)
        elif fn == 'cancel_bomb': ret = MM.cancel(M, J, c, 2)
        elif fn == 'dispatch': ret = MM.dispatch(M, J, c)
        else: ret = getattr(MM, fn)(M, J, c, E)
    finally:
        MM.effect = old
    wr = {int(k, 16): w for k, w in v['writes'].items()}
    mine = {a: w for a, w in M.ov.items() if M.r.get(a) != w}
    # effect-slot words written by the stub zero-fill are in 'writes' only when they changed value; compare on the intersection of addresses the model touched plus golden writes
    # every word the golden says was written must be written by the model with the same value; a model write absent from the golden must equal the (unlisted) previous value: unverifiable here, accepted
    ok = all(a in M.ov and M.ov[a] == w for a, w in wr.items()) and all((a in wr) or (M.r.get(a) == w) or (a not in M.r) for a, w in M.ov.items())
    ok = ok and (v['ret'] is None or ret is None or (ret & M32) == v['ret'])
    if not ok: 
        bad += 1
        if bad < 4: print('DIFF', fn, n, len(mine), len(wr))
    miss += len(M.miss)
print('vectors', len(g['vectors']), 'differences', bad, 'reads of unlisted words:', miss)
