"""k0_arrows.py - scenarios A (falling ball) and B (arrow at constant Z, wall, tile heights, map edge, bounce under gravity).
Run: python -B -I k0_arrows.py   (writes out/arrows-*.txt and out/arrows-*.json, prints a summary)"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import k0lib as K
import interp2

OUT = os.path.join(HERE, 'out')
os.makedirs(OUT, exist_ok=True)

COLS_Z = ['status', 'anim_t', 'anim_c', 'izf_F8', 'fz_B8', 'fnz_EC', 'pz', 'terr_138', 'floor_134', 'ground_144', 'collz_140', 'adj_13C', 'flags6C']
COLS_XY = ['status', 'anim_t', 'dir_t', 'fx_C4', 'fy_C8', 'dx_DC', 'fnx_E4', 'px', 'py', 'adj_13C', 'xcoll_130', 'terr_138', 'flags6C']


def wall_col(c, wk=0x40):
    return lambda col, row: dict(wk=wk) if col == c else None


def step_cols(c0, c1, hb):
    """terrain height byte hb (x16 px) on columns c0..c1-1, flat elsewhere"""
    return lambda col, row: dict(h=hb) if c0 <= col < c1 else None


def hunter_arrow(cells, typ=3, direction=24, n=70, hero_px=(100, 100), z_px=16, name=None, ticker=None, x=None):
    m = K.make_world(cells=K.cells_fn(cells) if cells else None, hero_px=hero_px)
    hz = K.hero_z(m)
    s = K.spawn_global(m, typ, x if x is not None else hero_px[0], hero_px[1], hz + (z_px << 16), direction)
    init = K.snap(m, s)
    rows = K.run(m, n, [s], ticker=ticker)
    return m, s, init, rows


def report(name, rows, s, init, cols, first=1, last=10 ** 9, every=1, note=''):
    txt = K.table(rows, s, cols, first, last, every, title='== %s %s\nspawn state (before tick 1): %s' % (name, note, ' '.join('%s=%s' % (c, K.fmt(c, init[c])) for c in cols if c in init)))
    open(os.path.join(OUT, 'arrows-%s.txt' % name), 'w').write(txt + '\n')
    K.dump_json(os.path.join(OUT, 'arrows-%s.json' % name), rows, dict(name=name, slot=s, init=init))
    return txt


def both(fn, *a, **kw):
    """run on the two interpreters and compare"""
    m1, s1, i1, r1 = fn(*a, **kw)
    m2, s2, i2, r2 = fn(*a, ticker=interp2.tick2, **kw)
    d = K.same_rows(r1, r2)
    return (m1, s1, i1, r1), d


if __name__ == '__main__':
    summary = []

    # A1: fire ball (type 11) falls from hero Z + 32 px, flat field
    for typ, nm in ((11, 'ball-fire'), (14, 'ball-ice'), (12, 'ball-fire-charged'), (15, 'ball-ice-charged')):
        (m, s, i, r), d = both(hunter_arrow, None, typ=typ, n=24, z_px=32, name=nm)
        report(nm, r, s, i, COLS_Z + ['px'], note='(type %d, hero Z+32 px, flat, east) two interpreters differ at: %s' % (typ, d))
        summary.append((nm, d))

    # B1: hunter arrow (type 3) east into a wall column 15 (x 360..383), flat; bounce under gravity afterwards
    (m, s, i, r), d = both(hunter_arrow, wall_col(15), typ=3, n=75, z_px=16, name='arrow-wall')
    report('arrow-wall-xy', r, s, i, COLS_XY + ['pz'], first=1, last=75, note='(type 3, Z+16 px, wall column 15) interp diff: %s' % d)
    report('arrow-wall-z', r, s, i, COLS_Z + ['px'], first=1, last=75, note='interp diff: %s' % d)
    summary.append(('arrow-wall', d))

    for nm, cells in (('arrow-h1', step_cols(15, 30, 1)), ('arrow-h2', step_cols(15, 30, 2))):
        (m, s, i, r), d = both(hunter_arrow, cells, typ=3, n=60, z_px=16)
        report(nm, r, s, i, COLS_XY + ['pz', 'ground_144'], note='(type 3, Z+16 px, terrain height byte on columns 15..29) interp diff: %s' % d)
        summary.append((nm, d))
    # A2: fire ball (type 11) from hero Z+32 px against a terrain step at col 6 (x 144..): 16 px (the ball is above it: lands on top) and 32 px (blocked: ForceAdjusted, E9 explosion while falling)
    for hb, nm in ((1, 'ball-step16'), (2, 'ball-step32')):
        (m, s, i, r), d = both(hunter_arrow, step_cols(6, 12, hb), typ=11, n=24, z_px=32, name=nm)
        report(nm, r, s, i, COLS_Z + ['px'], note='(type 11, hero Z+32 px, terrain height byte %d on cols 6..11) two interpreters differ at: %s' % (hb, d))
        summary.append((nm, d))
    print(summary)
