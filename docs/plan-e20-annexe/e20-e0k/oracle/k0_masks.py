"""k0_masks.py - the wall mask built by 0x800373E4 per call: 0x40, | 0x01 when the entity has ClassB (flag 0x08), | 0x1000 when it has ClassA (flag 0x01).  An arrow (flags 0x2074: neither class) must be blocked by walkability 0x40 only.
Run: python -B -I k0_masks.py"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import k0lib as K
from k0_arrows2 import arrow_run
from k0_arrows import OUT

lines = []
for wk, gp, nm in ((0x40, 0, 'walkability 0x40'), (0x01, 0, 'walkability 0x01 (ClassB wall)'), (0x00, 0x10, 'ground property 0x10 (word 0x1000, ClassA pit)'), (0x80, 0, 'walkability 0x80'), (0x02, 0, 'walkability 0x02'), (0x04, 0, 'walkability 0x04'), (0x08, 0, 'walkability 0x08'), (0x20, 0, 'walkability 0x20'), (0x10, 0, 'walkability 0x10')):
    for add, anm in ((0, 'arrow flags 0x2074 (no class)'), (0x08, 'arrow + ClassB (0x08)'), (0x01, 'arrow + ClassA (0x01)')):
        cells = lambda col, row, wk=wk, gp=gp: dict(wk=wk, gp=gp) if col == 15 else None
        m, s, init, rows = arrow_run(cells, typ=3, n=60)
        if add:
            # re-run with the class flag set on the arrow (harness write of +0x6C right after the spawn)
            pass
        lines.append((nm, anm))
# the class-flag variants need the flag poked before tick 1: do it explicitly
lines = []
import interp2
for wk, gp, nm in ((0x40, 0, 'walkability 0x40'), (0x01, 0, 'walkability 0x01'), (0x00, 0x10, 'ground property 0x10 (cell word 0x1000)'), (0x80, 0, 'walkability 0x80'), (0x02, 0, 'walkability 0x02'), (0x04, 0, 'walkability 0x04'), (0x08, 0, 'walkability 0x08'), (0x20, 0, 'walkability 0x20'), (0x10, 0, 'walkability 0x10')):
    res = []
    for add in (0, 0x08, 0x01):
        cells = lambda col, row, wk=wk, gp=gp: dict(wk=wk, gp=gp) if col == 15 else None
        m = K.make_world(cells=K.cells_fn(cells), hero_px=(100, 100))
        s = K.spawn_global(m, 3, 100, 100, K.hero_z(m) + (16 << 16), 24)
        if add:
            K.poke_entity(m, s, flags6C=m.peek(K.ent(s) + 0x6C) | add)
        rows = K.run(m, 60, [s])
        t = next((r['tick'] for r in rows if r['slots'][s]['adj_13C']), None)
        res.append('%s: %s' % ({0: 'no class', 8: 'ClassB', 1: 'ClassA'}[add], ('BLOCKED at tick %d (X %.1f)' % (t, rows[t - 1]['slots'][s]['px'] / 65536)) if t else 'flies through'))
    lines.append('%-45s -> %s' % (nm, ' ; '.join(res)))
txt = '\n'.join(lines)
open(os.path.join(OUT, 'masks-summary.txt'), 'w').write(txt + '\n')
print(txt)
