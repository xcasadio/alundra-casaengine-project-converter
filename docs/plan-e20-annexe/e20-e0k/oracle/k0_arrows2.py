"""k0_arrows2.py - arrows: halving residual at a wall, NoObstacleSlide off (clipping a corner), map edge, descending terrain, bounce that lands, charged arrow (E1), directions.
Run: python -B -I k0_arrows2.py"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import k0lib as K
import interp2
from k0_arrows import wall_col, step_cols, COLS_XY, COLS_Z, OUT

COLS_C = ['status', 'anim_t', 'dir_t', 'fx_C4', 'fy_C8', 'fnx_E4', 'fny_E8', 'px', 'py', 'pz', 'adj_13C', 'xcoll_130', 'ground_144', 'collz_140', 'flags6C']


def arrow_run(cells, typ=3, direction=24, n=60, hero_px=(100, 100), z_px=16, z_extra=0, x_frac=0, clear_flags=0, ticker=None, x=None, y=None, hero_cells_ok=True):
    m = K.make_world(cells=K.cells_fn(cells) if cells else None, hero_px=hero_px)
    hz = K.hero_z(m)
    s = K.spawn_global(m, typ, x if x is not None else hero_px[0], y if y is not None else hero_px[1], hz + (z_px << 16) + z_extra, direction)
    if x_frac:
        K.poke_entity(m, s, px=m.peek(K.ent(s) + 0x114) + x_frac)
    if clear_flags:
        K.poke_entity(m, s, flags6C=m.peek(K.ent(s) + 0x6C) & ~clear_flags)
    init = K.snap(m, s)
    rows = K.run(m, n, [s], ticker=ticker)
    return m, s, init, rows


def pair(**kw):
    a = arrow_run(**kw)
    b = arrow_run(ticker=interp2.tick2, **kw)
    return a, K.same_rows(a[3], b[3])


def first_row(rows, s, pred):
    for r in rows:
        if pred(r['slots'][s]):
            return r['tick']
    return None


def save(name, rows, s, init, cols, title, first=1, last=10 ** 9):
    txt = K.table(rows, s, cols, first, last, 1, title=title)
    open(os.path.join(OUT, 'arrows2-%s.txt' % name), 'w').write(txt + '\n')
    K.dump_json(os.path.join(OUT, 'arrows2-%s.json' % name), rows, dict(name=name, slot=s, init=init))


if __name__ == '__main__':
    lines = []

    # C1: the halving at a wall.  Wall column 15 (x 360..383).  Arrow body right edge pixel = posX - 10 + 21 - 1/65536 -> blocked from posX > 349.0.
    # sweep the start x (integer and fractional) and report: tick of first ForceAdjusted, X at that tick, X after, whether X lands exactly on 349.0 (= 349 << 16)
    lines.append('C1 halving residual at a wall column 15: start x -> (tick of ForceAdjusted, X raw at that tick, 349<<16 - X, +0x130)')
    for x0, frac in ((100, 0), (101, 0), (102, 0), (103, 0), (104, 0), (105, 0), (100, 12345), (100, 1), (100, 40000), (101, 65535)):
        (m, s, init, rows), d = pair(cells=wall_col(15), typ=3, n=60, x=x0, x_frac=frac)
        t = first_row(rows, s, lambda r: r['adj_13C'] != 0)
        r = rows[t - 1]['slots'][s] if t else None
        p = rows[t - 2]['slots'][s] if t else None
        lines.append('  x0=%d+%d/65536: ForceAdjusted at tick %s, X raw %s (prev tick %s), gap to 349<<16 = %s, xcoll=%X, interp diff %s' % (
            x0, frac, t, r['px'] if r else None, p['px'] if p else None, (349 * 65536 - r['px']) if r else None, r['xcoll_130'] if r else 0, d))
        if x0 == 101 and frac == 0:
            save('halving-x101', rows, s, init, COLS_C, 'arrow type 3 start x=101, wall col 15', first=t - 3 if t else 1, last=(t or 0) + 3)
        if x0 == 100 and frac == 12345:
            save('halving-x100.188', rows, s, init, COLS_C, 'arrow type 3 start x=100+12345/65536, wall col 15', first=t - 3 if t else 1, last=(t or 0) + 3)

    # C2: NoObstacleSlide cleared on the arrow (flag 0x2000), arrow clips the top of a wall: wall column 15 only on rows >= 7 (y >= 112); arrow at y=105: body y range 98..112 -> corner row ...
    lines.append('C2 NoObstacleSlide cleared (flags &= ~0x2000), wall column 15 only on rows >= 7:')

    def partial_wall(col, row):
        return dict(wk=0x40) if col == 15 and row >= 7 else None
    for clr, nm in ((0, 'slide-forbidden'), (0x2000, 'slide-allowed')):
        (m, s, init, rows), d = pair(cells=partial_wall, typ=3, n=70, y=106, hero_px=(100, 106), clear_flags=clr)
        t = first_row(rows, s, lambda r: r['adj_13C'] != 0)
        fin = rows[-1]['slots'][s]
        lines.append('  %s: first ForceAdjusted tick %s ; last row status %d X %.3f Y %.3f ; interp diff %s' % (nm, t, fin['status'], fin['px'] / 65536, fin['py'] / 65536, d))
        save(nm, rows, s, init, COLS_C, 'arrow type 3 y=106 flags clear %X, wall col 15 rows>=7' % clr, first=36, last=60)

    # C3: map edge: arrow fired east from x=1200 (map 1248 px wide): the forces pass clamps the force to the entity bounds (+0xFC) and sets ForceAdjusted
    (m, s, init, rows), d = pair(cells=None, typ=3, n=20, x=1200, hero_px=(1200, 100))
    t = first_row(rows, s, lambda r: r['adj_13C'] != 0)
    lines.append('C3 map edge: arrow from x=1200 east: first ForceAdjusted tick %s, X %s px (bounds +0xFC = 0x4E00000 - ((offX + sizeX) << 16) = %.4f px) interp diff %s' % (
        t, rows[t - 1]['slots'][s]['px'] / 65536 if t else None, (0x4E00000 - ((-10 + 21) << 16)) / 65536, d))
    save('map-edge', rows, s, init, COLS_C, 'arrow type 3 east from x=1200 to the map edge', first=1, last=14)

    # C4: constant Z over a drop: hero on a 32 px plateau (cols 0..9 height byte 2), arrow flies off the edge: Z stays, no gravity
    plateau = lambda col, row: dict(h=2) if col <= 9 else None
    (m, s, init, rows), d = pair(cells=plateau, typ=3, n=30, x=100, hero_px=(100, 100))
    lines.append('C4 arrow leaves a 32 px plateau (x 240): Z rows (every 4 ticks): ' + ' '.join('%d:%.2f' % (r['tick'], r['slots'][s]['pz'] / 65536) for r in rows[::4]) + ' ; ground_144 values ' + str(sorted(set(r['slots'][s]['ground_144'] for r in rows))) + ' ; interp diff %s' % d)
    save('plateau-drop', rows, s, init, COLS_C + ['terr_138'], 'arrow type 3 over a plateau edge at x=240')

    # C5: bounce that LANDS: arrow flies 0.5 px above the ground (z_px=0, z_extra=32768), hits the wall, E17 bounce (gravity bit), lands before the animation ends
    (m, s, init, rows), d = pair(cells=wall_col(15), typ=3, n=70, z_px=0, z_extra=32768)
    t_imp = first_row(rows, s, lambda r: r['adj_13C'] != 0)
    t_land = first_row(rows, s, lambda r: r['collz_140'] != 0 and r['status'] == 3)
    lines.append('C5 arrow 0.5 px above ground: wall impact tick %s ; first landing (CollidedWithEntityZ) tick %s ; interp diff %s' % (t_imp, t_land, d))
    save('bounce-lands', rows, s, init, COLS_Z + ['px'], 'arrow type 3 at ground+0.5 px into wall col 15', first=(t_imp or 1) - 1, last=(t_imp or 1) + 30)

    # C6: spawn heights: hero Z (raw 1) + z_extra; the spawner adds +1 (PosZ = Z - offZ + 1) and raises PosZ to T+1 when lower (0x80039EE8-0x80039F04)
    for zx, nm in ((0, 'hero Z (PosZ = T + 2 raw: 1 raw above the rest height)'), (-1, 'hero Z - 1 raw (PosZ = T + 1: at rest height)'), (-(20 << 16), '20 px below the ground (raised to T + 1)')):
        (m, s, init, rows), d = pair(cells=None, typ=3, n=6, z_px=0, z_extra=zx)
        lines.append('C6 spawn %s: initial PosZ raw %d ; statuses %s ; IsOnGround %s ; Z per tick %s ; interp diff %s' % (nm, init['pz'], [r['slots'][s]['status'] for r in rows], [r['slots'][s]['ground_144'] for r in rows], [r['slots'][s]['pz'] for r in rows], d))
        save('spawn-height%d' % (zx if zx > -100 else -20), rows, s, init, COLS_Z + ['px'], 'arrow type 3 spawn height %s' % nm, first=1, last=6)

    # C6b: spawn inside a raised terrain (arrow Z+16 over a 48 px plateau start): raised to T+1 -> the arrow flies at the terrain top + 1 raw...
    plat = lambda col, row: dict(h=3) if col <= 6 else None
    (m, s, init, rows), d = pair(cells=plat, typ=3, n=8, x=100, hero_px=(100, 100), z_px=0, z_extra=-(60 << 16))
    lines.append('C6b arrow spawned 60 px under a 48 px plateau top: PosZ raw %d (T+1 = %d) statuses %s ; interp diff %s' % (init['pz'], 48 * 65536 + 1, [r['slots'][s]['status'] for r in rows], d))

    # C7: charged arrow (type 7) into a wall: E1 destroy, no bounce
    (m, s, init, rows), d = pair(cells=wall_col(15), typ=7, n=60, z_px=8)
    t = first_row(rows, s, lambda r: r['adj_13C'] != 0)
    lines.append('C7 charged arrow (type 7, Z+8 px) into wall col 15: ForceAdjusted tick %s ; statuses after: %s ; interp diff %s' % (t, [(r['tick'], r['slots'][s]['status']) for r in rows[t - 1:t + 3]] if t else None, d))
    save('charged-wall', rows, s, init, COLS_C, 'arrow type 7 into wall col 15', first=(t or 1) - 2, last=(t or 1) + 4)

    # C8: directions: south and north (vertical offset table 2/3), west
    for dr, nm in ((0, 'south'), (16, 'north'), (8, 'west')):
        (m, s, init, rows), d = pair(cells=None, typ=3, n=6, direction=dr)
        lines.append('C8 direction %d (%s): X/Y per tick ' % (dr, nm) + ' '.join('(%.2f,%.2f)' % (r['slots'][s]['px'] / 65536, r['slots'][s]['py'] / 65536) for r in rows) + ' interp diff %s' % d)

    txt = '\n'.join(lines)
    open(os.path.join(OUT, 'arrows2-summary.txt'), 'w').write(txt + '\n')
    print(txt)
