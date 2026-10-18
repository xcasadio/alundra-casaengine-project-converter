"""my_goldcheck.py - critic: replay the OTHER agent's golden vectors (results/goldens_h3.json, meant for a C# replay) through MY models, reading only the sparse input words they list."""
import json, sys
from machine import M32, s32
import my_models as MM
g = json.load(open(r"C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-h/h3bin/results/goldens_h3.json"))
class SparseMem:
    def __init__(self, inn):
        self.inn = {int(k, 16): v for k, v in inn.items()}; self.ov = {}; self.miss = set()
    def _w(self, a):
        if a in self.ov: return self.ov[a]
        if a in self.inn: return self.inn[a]
        self.miss.add(a); return 0
    def rw(self, a):
        a &= M32
        if a & 3 == 0: return self._w(a)
        raise RuntimeError('unaligned word read')
    def rb(self, a):
        a &= M32
        return (self._w(a & ~3) >> (8 * (a & 3))) & 0xFF
    def rh(self, a):
        a &= M32
        return (self._w(a & ~3) >> (8 * (a & 2))) & 0xFFFF
    def rws(self, a): return s32(self.rw(a))
    def ww(self, a, v): self.ov[a & M32] = v & M32
class Ctx:
    pass
bad = 0; miss_total = 0; by = {}; MISSALL = []
for n, v in enumerate(g['vectors']):
    fn = v['fn']; M = SparseMem(v['inn']); J = []
    ctx = Ctx()
    sr = [int(x, 16) for x in v['spawn_results']]; fr = [int(x, 16) for x in v['fx_results']]
    ctx.spawn_ret = sr[0] if sr else 0
    ctx.eff_ret = fr[0] if fr else 0
    # the effect stub of the golden harness may return different slots per call: model only the first and compare loosely
    E = int(v['self'], 16) if v['self'] else None
    # monkeypatch effect zeroing off (sparse memory)
    old = MM.effect
    def eff(M_, J_, ctx_, a1, x, y, z, _old=old):
        J_.append(('effect', (0, a1, 0, x & M32, y & M32, z & M32)))
        return ctx_.eff_ret
    MM.effect = eff
    try:
        if fn == 'start_beans': ret = MM.start(M, J, ctx, 1)
        elif fn == 'start_bomb': ret = MM.start(M, J, ctx, 2)
        elif fn == 'drv_beans': ret = MM.driver(M, J, ctx, 1)
        elif fn == 'drv_bomb': ret = MM.driver(M, J, ctx, 2)
        elif fn == 'cancel_beans': ret = MM.cancel(M, J, ctx, 1)
        elif fn == 'cancel_bomb': ret = MM.cancel(M, J, ctx, 2)
        elif fn == 'dispatch': ret = MM.dispatch(M, J, ctx)
        else: ret = getattr(MM, fn)(M, J, ctx, E)
    finally:
        MM.effect = old
    out = {int(k, 16): val for k, val in v['out'].items()}
    mine = {a: val for a, val in M.ov.items() if M.inn.get(a) != val}
    # compare words the model changed with the golden 'out' (restricted to addresses the golden recorded as changed)
    ok = (mine == out)
    if v['ret'] is not None and fn not in ('dispatch',) and ret is not None:
        ok = ok and ((ret & M32) == (v['ret'] & M32 if isinstance(v['ret'], int) else int(str(v['ret']), 16)))
    if not ok:
        bad += 1
        if bad <= 5: print('DIFF', n, fn, 'ret', ret, v['ret'], '\n  mine-only', {hex(a): x for a, x in mine.items() if out.get(a) != x}, '\n  gold-only', {hex(a): x for a, x in out.items() if mine.get(a) != x}, '\n  J mine', J, '\n  J gold', v['journal'])
    miss_total += len(M.miss)
    if not ok: MISSALL.extend('%08X' % a for a in M.miss)
    by[fn] = by.get(fn, 0) + (0 if ok else 1)
import collections
print('most frequent absent words:', collections.Counter(MISSALL).most_common(12))
print('vectors', len(g['vectors']), 'differences', bad, 'reads of words absent from the golden input:', miss_total)
print(by)
