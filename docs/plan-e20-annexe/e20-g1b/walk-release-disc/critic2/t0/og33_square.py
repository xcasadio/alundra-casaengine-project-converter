"""og33_square.py - E20.g1b T0 (b3), O-G3-3: the hero stands on rec5 of the map 392 (the route R7 of e20-g3/bin/a3_routes.py: the same pad script up to the first DOWN tick, t37)
and pushes south into the pile of the row 19; the binary (REAL code of ALUN_CD.EXE, France, in the MIPS machine of the oracle) gives, tick by tick, the contact (hero +0x130)
and, for a Square (DOWN+SQUARE) at each tick S from t37 to t43, what the hero does (lift: hero +0x2C set, animation 5; sword: animation 16 family).
Needs a COPY of e20-g3/bin (*.py) with a copy of e20-g1a/work as ./g1a next to it, in the directory named by the environment variable G1B_BIN.
Run: python -B og33_square.py > out/og33-square.txt"""
import sys, os
BIN = os.environ['G1B_BIN']
sys.path.insert(0, BIN)
import lib392 as L


def run(sq):
    m, S = L.world392(hero_px=(720, 296), direction=8, rec_ids=(4, 5, 6, 7, 8, 10, 11))
    h = L.K.ent(0)
    m.poke(h + 0x118, int(round(296.0 * 65536)))
    seq = [(13, L.LEFT), (1, L.LEFT | L.CROSS), (17, L.LEFT), (5, 0)]
    if sq is None:
        seq += [(10, L.DOWN), (6, 0)]
    else:
        seq += [(sq - 37, L.DOWN), (1, L.DOWN | L.SQUARE), (45 - sq, L.DOWN)]
    pads, n = L.pad_seq(seq)
    rows = L.gl.run(m, n, pads, crates=list(S.values()))
    return rows, {L.K.ent(s): 'rec%d' % r for r, s in S.items()}


def main():
    rows, nm = run(None)
    print('== the base run (DOWN from t37, no Square): contact by tick')
    for r in rows[35:44]:
        H = r['hero']
        print('t%03d pad=%04X y=%.3f z=%.1f anim=%d xcoll=%s adj=%d' % (r['tick'], r['hold'], H['py'] / 65536, H['pz'] / 65536, H['anim_t'], nm.get(H['xcoll'], hex(H['xcoll']) if H['xcoll'] else '-'), H['adj']))
    arrival = next(r['tick'] for r in rows if r['hero']['py'] >= 297 * 65536)
    print('arrival at y = 297.000: t%03d ; contact first set after t%03d' % (arrival, next(r['tick'] for r in rows if r['hero']['xcoll'])))
    for sq in range(37, 44):
        rows, nm = run(sq)
        after = rows[sq - 1:sq + 2]
        lifted = any(r['hero']['carried2C'] for r in rows[sq - 1:sq + 4])
        print('Square at t%03d (contact read by that MovePlayer = the one after t%03d: %s): %s ; anim by tick %s' % (
            sq, sq - 1, nm.get(rows[sq - 2]['hero']['xcoll'], '-') if rows[sq - 2]['hero']['xcoll'] else '-',
            'LIFT %s' % nm.get(next(r['hero']['carried2C'] for r in rows[sq - 1:sq + 4] if r['hero']['carried2C'])) if lifted else 'sword (no lift)',
            [(r['tick'], r['hero']['anim_t']) for r in after]))


if __name__ == '__main__':
    main()
