"""a3_contactmap.py - disc-e20g3 A3: which crate is the contact (+0x130) when the hero, walking west along a lane y, is stopped by the column?  All 7 barricade crates present.  Output out/a3_contactmap.txt"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import multiprocessing as mp

def one(y):
    import lib392 as L
    m, S = L.world392(hero_px=(720, 296), direction=8)
    h = L.K.ent(0)
    m.poke(h + 0x118, int(round(y * 65536)))
    pads, n = L.pad_seq([(40, L.LEFT)])
    rows = L.gl.run(m, n, pads, crates=list(S.values()))
    rec = {L.K.ent(s): 'rec%d' % r for r, s in S.items()}
    t = next((r['tick'] for r in rows if r['hero']['xcoll']), None)
    return y, t, rec.get(rows[-1]['hero']['xcoll'], '-'), round(rows[-1]['hero']['px'] / 65536, 3), round(rows[-1]['hero']['py'] / 65536, 3)

if __name__ == '__main__':
    ys = [295 + k * 0.5 for k in range(0, 68)]
    with mp.Pool(22) as p:
        res = p.map(one, ys)
    # compress
    lines = []
    prev = None
    for y, t, c, x, yy in res:
        key = c
        if key != prev:
            lines.append('from y=%.1f: contact = %s (first at tick %s, hero stops at x=%.3f y=%.3f)' % (y, c, t, x, yy))
            prev = key
    lines.append('last y tested = %.1f' % ys[-1])
    open(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'out', 'a3_contactmap.txt'), 'w').write('\n'.join(lines) + '\n')
    print('\n'.join(lines))
