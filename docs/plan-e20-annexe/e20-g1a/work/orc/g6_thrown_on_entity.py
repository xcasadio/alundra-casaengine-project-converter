"""g6_thrown_on_entity.py - S6: a thrown crate (non-collidable after C23, not a subject) over another crate (16 px tall, collidable, a rider-capable platform) and over a Beannoid made Collidable.
The target crate (2nd instance of map 392 sprite 36, real spawner) is placed by the harness at x = TX on the throw line; the hero throws at t=60 (moving east: 3 px/tick, lands at X = 178 on flat ground).
Run: python -B -I g6_thrown_on_entity.py"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import gl
import ana
import g4_throw as G


def run_one(tx, throw_hold=gl.RIGHT, n=100, target='crate'):
    m = gl.world(hero_px=(100, 100), direction=24)
    c = gl.add_crate(m, 130, 100)
    t = gl.add_crate(m, tx, 100, rec_addr=0x801F0000, ent_addr=0x801F8000, ptr_addr=0x801F9000)
    seq = [(10, gl.RIGHT), (1, gl.RIGHT | gl.SQUARE), (49, 0), (1, throw_hold | gl.SQUARE), (n - 61, 0)]
    pads, nn = gl.pad_script(seq)
    rows = gl.run(m, nn, pads, crates=[c, t])
    return m, c, t, rows


if __name__ == '__main__':
    out = []
    for tx in (160, 178, 172, 184):
        m, c, t, rows = run_one(tx)
        s = G.throw_stats(rows, c, 60)
        path = [(r['tick'], round(r['c'][c]['px'] / 65536, 2), round(r['c'][c]['pz'] / 65536, 2), r['c'][c]['support'] == gl.K.ent(t)) for r in rows if 74 <= r['tick'] <= 90 and r['c'][c]['status'] != 0]
        tstat = [(r['tick'], r['c'][t]['status'], round(r['c'][t]['pz'] / 65536, 1)) for r in rows if r['tick'] in (60, 75, 85, 95)]
        out.append('target crate at x=%d: thrown crate changes %s ; land %s dead %s ; path(t,x,z,support==target) %s ; target (t,status,z) %s' % (tx, s['change_crate'], s['land'], s['dead'], path, tstat))
    print('\n'.join(out))
    open(os.path.join(gl.OUT, 'g6-thrown-on-entity.txt'), 'w').write('\n'.join(out) + '\n')
