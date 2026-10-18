"""c1_h4_targets_cmp.py (own script): trace_targets.py of e20-h4 for item 0x2B with the six sprite-25 targets of targets_inv0.txt, as shipped (slot index words 0) and with the slot index word of
0x8003B24C written (the pworld convention), to show what the missing index changes in the hit keys."""
import sys
import spellworld as S
import trace_targets as T
import world as W

orig_make = S.make
def make_idx(*a, **k):
    m, STATS, placed = orig_make(*a, **k)
    for i in range(64):
        m.poke(S.ENT0 + S.STRIDE * i, i)
    return m, STATS, placed

TG = [(140, 100), (100, 140), (180, 100), (100, 60), (60, 100), (140, 140)]
for tag, mk in (('as shipped (+0 = 0)', orig_make), ('slot index written', make_idx)):
    S.make = mk
    m, res, hp0, STATS = T.run(0x2B, TG, inv=0, hp=5000, ticks=200, sheet=None)
    print(tag)
    for (x, y), p in zip(TG, res):
        r = res[p]
        print('   target (%d,%d): hits %d first tick %s' % (x, y, len(r['hits']), r['hits'][0][0] if r['hits'] else None), [h[0] for h in r['hits']])
