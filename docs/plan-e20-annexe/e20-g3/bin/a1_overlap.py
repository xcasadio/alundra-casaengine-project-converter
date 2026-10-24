"""a1_overlap.py - disc-e20g3 A1: a hero that already overlaps a crate (rec5, east face at x=647; hero box [x-10, x+11)): can he leave?  The binary tests only the NEW position (0x80037908), no exit rule.
Hero put at x = 657 - d (overlap of d px) by a harness write of PosX, then RIGHT held (away from the crate) for 12 ticks.  Output out/a1_overlap.txt"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lib392 as L
OUT = []
for d in (0.5, 1.0, 1.2, 1.3, 2.0, 5.0, 10.0):
    m, S = L.world392(hero_px=(657, 296), direction=24, rec_ids=(5,))
    h = L.K.ent(0)
    m.poke(h + 0x114, int(round((657 - d) * 65536)))
    pads, n = L.pad_seq([(12, L.RIGHT)])
    rows = L.gl.run(m, n, pads, crates=list(S.values()))
    xs = [round(r['hero']['px'] / 65536, 3) for r in rows]
    OUT.append('overlap %4.1f px (hero x=%.1f): RIGHT x12 -> x by tick %s ; contact at tick 1: %s' % (d, 657 - d, xs[:6] + ['...', xs[-1]], hex(rows[0]['hero']['xcoll'])))
print('\n'.join(OUT))
open(os.path.join(L.OUT, 'a1_overlap.txt'), 'w').write('\n'.join(OUT) + '\n')
