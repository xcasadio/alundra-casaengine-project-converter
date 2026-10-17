"""g4b_throw.py - S4b: air throws, throws onto a wall / a 16 px step / a 32 px step, throws in the 4 directions.
Run: python -B -I g4b_throw.py"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import gl
import ana
import g4_throw as G


def wall_cells(col, kind='wall', hb=0):
    if kind == 'wall':
        return lambda c, r: dict(wk=0x40) if c == col else None
    return lambda c, r: dict(h=hb) if c >= col else None


def run_one(tag, throw_hold, throw_at=60, jump_at=None, cells=None, n=120, crate_px=(130, 100), hero_px=(100, 100), lift_dir=gl.RIGHT, direction=24, cols_after=None, face=None):
    m = gl.world(cells=cells, hero_px=hero_px, direction=direction)
    c = gl.add_crate(m, *crate_px)
    seq = [(10, lift_dir), (1, lift_dir | gl.SQUARE)]
    t = 11
    if face is not None:                      # turn to face another direction while carrying (pad direction, no step into anything)
        seq += [(48 - t + 1 + 0, 0)]
        t = 49
        seq += [(6, face)]
        t += 6
    if jump_at:
        seq += [(jump_at - t, 0), (1, gl.CROSS), (throw_at - jump_at - 1, 0)]
    else:
        seq += [(throw_at - t, 0)]
    seq += [(1, throw_hold | gl.SQUARE), (n - throw_at, 0)]
    pads, nn = gl.pad_script(seq)
    rows = gl.run(m, nn, pads, crates=[c])
    s = G.throw_stats(rows, c, throw_at)
    txt = gl.table(rows, [c], first=throw_at - 2, last=min(n, throw_at + 40), hcols=['anim_t', 'carried2C', 'px', 'pz', 'ground', 'adir'],
                   ccols=['t24', 'anim_t', 'flags6C', 'px', 'py', 'pz', 'fx', 'fy', 'fz', 'ground', 'collz', 'adj', 'status'], title=tag)
    open(os.path.join(gl.OUT, 'g4b-%s.txt' % tag.replace(' ', '_')), 'w').write(txt + '\n')
    return rows, c, s


if __name__ == '__main__':
    out = []
    # air throws: jump at 60 (still), throw at apex (t=70)
    for nm, hold in (('air-still', 0), ('air-moving-east', gl.RIGHT)):
        rows, c, s = run_one('THROW ' + nm, hold, throw_at=70, jump_at=60, n=110)
        hz = ana.apex(rows, 'hero', first=60)
        out.append('%s: hero apex %s ; hero Z at throw %.2f ; hero anim changes %s ; crate changes %s ; land %s dead %s ; crate X/Z every 2 ticks %s' % (
            nm, hz, rows[69]['hero']['pz'] / 65536, s['change_hero'], s['change_crate'], s['land'], s['dead'], s['path'][:14]))
    # wall at column 6 (x 144..167): crate thrown east moving (3 px/tick) and still (1.5)
    for nm, hold in (('wall-moving', gl.RIGHT), ('wall-still', 0)):
        rows, c, s = run_one('THROW ' + nm, hold, cells=wall_cells(6), n=100)
        out.append('%s: crate changes %s ; land %s dead %s ; path %s' % (nm, s['change_crate'], s['land'], s['dead'], s['path'][:16]))
    # steps 16 / 32 px starting at column 6
    for nm, hb in (('step16', 1), ('step32', 2), ('step48', 3)):
        rows, c, s = run_one('THROW ' + nm, gl.RIGHT, cells=wall_cells(6, 'step', hb), n=100)
        out.append('%s: crate changes %s ; land %s dead %s ; path %s' % (nm, s['change_crate'], s['land'], s['dead'], s['path'][:16]))
    print('\n'.join(out))
    open(os.path.join(gl.OUT, 'g4b-summary.txt'), 'w').write('\n'.join(out) + '\n')
