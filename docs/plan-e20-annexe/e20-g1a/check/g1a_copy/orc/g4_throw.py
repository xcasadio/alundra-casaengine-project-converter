"""g4_throw.py - S4: throw the crate (Square edge in a carry state) east on flat ground (still / moving / in the air), onto a wall, onto a step; break on landing (E2).
Pad script: lift at t11, throw at t=throw_at.  Crate record 36 of map 392 (real), C23 + E2 real.
Run: python -B -I g4_throw.py"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import gl
import ana


def scenario(throw_hold=0, throw_at=60, n=110, cells=None, hero_px=(100, 100), crate_px=(130, 100), direction=24, lift_dir=gl.RIGHT, pre_hold=0, jump_before=None, after=0):
    m = gl.world(cells=cells, hero_px=hero_px, direction=direction)
    c = gl.add_crate(m, *crate_px)
    seq = [(10, lift_dir), (1, lift_dir | gl.SQUARE), (throw_at - 11, pre_hold)]
    seq += [(1, throw_hold | gl.SQUARE), (n - throw_at, after)]
    pads, nn = gl.pad_script(seq)
    rows = gl.run(m, nn, pads, crates=[c])
    return m, c, rows


def throw_stats(rows, c, throw_at):
    t0 = throw_at
    r0 = rows[t0 - 1]
    c_first = rows[t0 - 1]['c'][c]
    ch_c = ana.changes(rows, c, ['status', 'anim_t', 't24', 'carrier28', 'flags6C'], first=t0 - 1)
    ch_h = ana.changes(rows, 'hero', ['anim_t', 'carried2C'], first=t0 - 1)
    land = ana.first_tick(rows, lambda r: r['c'][c]['ground'] == 1 and r['c'][c]['carrier28'] == 0, first=t0 + 1)
    dead = ana.first_tick(rows, lambda r: r['c'][c]['status'] == 4, first=t0)
    crate_path = [(r['tick'], round(r['c'][c]['px'] / 65536, 2), round(r['c'][c]['pz'] / 65536, 2)) for r in rows if t0 <= r['tick'] <= t0 + 30 and r['tick'] % 2 == 0]
    return dict(change_crate=ch_c, change_hero=ch_h, land=land, dead=dead, path=crate_path, hero_x=rows[t0 - 1]['hero']['px'] / 65536, hero_adir=rows[t0 - 1]['hero']['adir'])


if __name__ == '__main__':
    out = []
    for nm, hold in (('still', 0), ('moving-east', gl.RIGHT)):
        m, c, rows = scenario(throw_hold=hold)
        txt = gl.table(rows, [c], first=58, last=100, hcols=['anim_t', 'anim_c', 'carried2C', 'off30', 'off34', 'off38', 'px', 'pz', 'fx', 'ground'],
                       ccols=['t24', 'carrier28', 'anim_t', 'anim_c', 'flags6C', 'px', 'pz', 'fx', 'fz', 'ground', 'collz', 'adj', 'status', 'letter'], title='THROW %s on flat ground' % nm)
        open(os.path.join(gl.OUT, 'g4-throw-%s.txt' % nm), 'w').write(txt + '\n')
        s = throw_stats(rows, c, 60)
        out.append('THROW %s flat: %s' % (nm, s))
    print('\n'.join(out))
    open(os.path.join(gl.OUT, 'g4-throw-summary.txt'), 'w').write('\n'.join(out) + '\n')
