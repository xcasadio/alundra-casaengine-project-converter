"""w2_trace.py - critic: tick trace of one jump run (side, PosY px, Cross tick), to explain the 'past without rec5' outcomes of w1 (read-only).
Usage: python -B -I w2_trace.py west 294.5 [tj ...]"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import my392 as G

U = 65536
side, y = sys.argv[1], float(sys.argv[2])
tjs = [int(a) for a in sys.argv[3:]] or list(range(8, 27))
DIR = G.LEFT if side == 'east' else G.RIGHT
for tj in tjs:
    m, S = G.world((720 if side == 'east' else 560, 296), 8 if side == 'east' else 24, yraw=int(round(y * U)))
    rec_of = {G.K.ent(s): r for r, s in S.items()}
    rows = G.run(m, [(tj - 1, DIR), (1, DIR | G.CROSS), (80 - tj, DIR)], S)
    sups = []
    for r in rows:
        h = r['hero']
        s = rec_of.get(h['support'])
        if s is not None and (not sups or sups[-1][1] != s):
            sups.append((r['tick'], s))
    fx = rows[-1]['hero']['px'] / U
    print('tj %2d final x %.4f y %.4f z %.4f supports %s' % (tj, fx, rows[-1]['hero']['py'] / U, rows[-1]['hero']['pz'] / U, sups))
    if len(tjs) == 1:
        for r in rows:
            h = r['hero']
            print('  t%02d x %.4f y %.4f z %.4f anim %d ground %d xcoll %s support %s' % (r['tick'], h['px'] / U, h['py'] / U, h['pz'] / U, h['anim_t'], h['ground'],
                  rec_of.get(h['xcoll'], hex(h['xcoll'])), rec_of.get(h['support'], hex(h['support']))))
