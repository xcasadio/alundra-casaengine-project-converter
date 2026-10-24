"""a3_hug.py - disc-e20g3 A3: a hero inside the doorway (x = 690, y = 312) presses UP until the wall stops him: his Y, and what the first LEFT ticks do.  Output out/a3_hug.txt"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lib392 as L
OUT = []
m, S = L.world392(hero_px=(690, 312), direction=8)
pads, n = L.pad_seq([(25, L.UP), (30, L.LEFT)])
rows = L.gl.run(m, n, pads, crates=[S[5]])
for r in rows:
    if r['tick'] in (1, 2, 3, 5, 8, 10, 12, 14, 16, 18, 20, 24, 25, 26, 27, 30, 40, 50, 55):
        H = r['hero']
        OUT.append('t%02d pad=%04X x=%.3f y=%.3f z=%.3f fy=%.3f xcoll=%s' % (r['tick'], r['hold'], H['px'] / 65536, H['py'] / 65536, H['pz'] / 65536, H['fy'] / 65536, hex(H['xcoll'])))
print('\n'.join(OUT))
open(os.path.join(L.OUT, 'a3_hug.txt'), 'w').write('\n'.join(OUT) + '\n')
