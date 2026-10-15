"""k0_walker.py - a gravity entity walking (Beannoid of map 14, sprite 79, real record; ClassA, Gravity, size 20x14x8, anim 7 = walk 160, anim 8 = hop impulse 1280 speed 208).
The harness chooses the animation (+0x88) and direction (+0x8C) at tick 1; handlers of the Beannoid are inert stubs.  Everything else is REAL code.
Scenarios: E1 walk off a 32 px plateau, E2 slope descent (magnet), E3 slope climb, E4 step up 16 px (blocked), E5 hop on flat ground, E6 hop onto a 16 px step, E7 pit cell (ClassA mask 0x1000).
Run: python -B -I k0_walker.py"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import k0lib as K
import interp2
from k0_arrows import wall_col, step_cols, OUT

COLS = ['status', 'anim_t', 'anim_c', 'izf_F8', 'fz_B8', 'fnz_EC', 'fx_C4', 'fnx_E4', 'px', 'pz', 'terr_138', 'floor_134', 'ground_144', 'collz_140', 'adj_13C', 'p18C']


def walker(cells, anim=7, direction=24, n=60, pos=(100, 100), ticker=None, start_after=0, pre_extra=None, flags_or=0, record_xy=False, speed_override=None):
    m = K.make_world(cells=K.cells_fn(cells) if cells else None, hero_px=(30, 30))
    slots, info = K.spawn_record_entities(m, 'map_14', 79, [(10, 11, 0)])
    s = slots[0]
    if speed_override:                       # harness mutation of the real record: animation-set speed (u16 at +8 of the 14-byte set)
        for a_, v_ in speed_override.items():
            m.poke16(info['animsets'] + 14 * a_ + 8, v_)
    K.poke_entity(m, s, px=pos[0] * 65536, py=pos[1] * 65536)
    # the spawner computed the terrain under the record position; place the entity on the terrain under the new position (what InitializeEntity does: T = probe, PosZ = max(PosZ, T + 1))
    # the pre-pass of 0x80038364 recomputes the derived words +0x1D8.. each tick; before tick 1 they are stale, so recompute them for the placement
    e = K.ent(s)
    mx = (m.peeks(e + 0x114) + m.peeks(e + 0x1E4)) & K.M32
    my = (m.peeks(e + 0x118) + m.peeks(e + 0x1E8)) & K.M32
    m.poke(e + 0x1D8, mx); m.poke(e + 0x1DC, my)
    T = m.call(0x800370C4, e)
    m.poke(e + 0x138, T)
    m.poke(e + 0x11C, T + 1); m.poke(e + 0x1E0, T + 1 + m.peeks(e + 0x1EC))
    if flags_or:
        m.poke(e + 0x6C, m.peek(e + 0x6C) | flags_or)
    init = K.snap(m, s)

    def pre(m_, t):
        if t == 1 + start_after:
            K.poke_entity(m_, s, anim_t=anim, dir_t=direction)
        if pre_extra:
            pre_extra(m_, t, s)
    rows = K.run(m, n, [s], pre=pre, ticker=ticker)
    return m, s, init, rows


def pair(**kw):
    a = walker(**kw)
    b = walker(ticker=interp2.tick2, **kw)
    return a, K.same_rows(a[3], b[3])


def save(name, rows, s, init, cols, title, first=1, last=10 ** 9):
    txt = K.table(rows, s, cols, first, last, 1, title=title)
    open(os.path.join(OUT, 'walker-%s.txt' % name), 'w').write(txt + '\n')
    K.dump_json(os.path.join(OUT, 'walker-%s.json' % name), rows, dict(name=name, slot=s, init=init))


def first(rows, s, pred, start=0):
    for r in rows:
        if r['tick'] > start and pred(r['slots'][s]):
            return r['tick']
    return None


if __name__ == '__main__':
    lines = []

    # E1: walk east off a 32 px plateau (cols 0..6 = x < 168)
    plateau = lambda col, row: dict(h=2) if col <= 6 else None
    (m, s, init, rows), d = pair(cells=plateau, anim=7, n=70, pos=(120, 100))
    t_drop = first(rows, s, lambda r: r['terr_138'] == 0)
    t_fall = first(rows, s, lambda r: r['pz'] < 32 * 65536, t_drop or 0)
    t_land = first(rows, s, lambda r: r['collz_140'] != 0 and r['pz'] == 1, t_drop or 0)
    lines.append('E1 walk off a 32 px plateau (edge x=168): TerrainHeight drops to 0 at tick %s (X %.3f); PosZ first below 32 px at tick %s; landing (CollidedWithEntityZ, PosZ=T+1) tick %s ; interp diff %s' % (
        t_drop, rows[t_drop - 1]['slots'][s]['px'] / 65536 if t_drop else 0, t_fall, t_land, d))
    save('edge-drop', rows, s, init, COLS, 'Beannoid walks east (anim 7) off a 32 px plateau at x=168', first=(t_drop or 1) - 2, last=(t_land or 60) + 3)

    # E2/E3: ramps.  A slope cell (byte2 & 3 = 2: descending with x, 3: ascending) of height byte h spans (h-1)*16 + table[..] = 1..16 px over its 24 px (0x800236D4).
    # E2 descending ramp: cols 3..6 = (h 4,3,2,1; slope 2), flat 64 px before, flat 0 after.  E3 ascending ramp: cols 3..6 = (h 1,2,3,4; slope 3), flat 0 before, flat 64 after.
    def ramp_down(col, row):
        if col < 3:
            return dict(h=4)
        if col <= 6:
            return dict(h=4 - (col - 3), slope=2)
        return None

    def ramp_up(col, row):
        if col < 3:
            return None
        if col <= 6:
            return dict(h=1 + (col - 3), slope=3)
        return dict(h=4)
    for nm, fn_ in (('ramp-down', ramp_down), ('ramp-up', ramp_up)):
        (m, s, init, rows), d = pair(cells=fn_, anim=7, n=70, pos=(40, 100))
        trace = [(round(r['slots'][s]['px'] / 65536, 1), round(r['slots'][s]['terr_138'] / 65536, 2), round(r['slots'][s]['pz'] / 65536, 2), r['slots'][s]['fz_B8'], r['slots'][s]['ground_144']) for r in rows[::5]]
        off = [r['tick'] for r in rows if r['slots'][s]['ground_144'] == 0]
        lines.append('E2/E3 %s, walking east from x=40 (anim 7, 1.875 px/tick): (X, TerrainHeight, PosZ, ForceZ, IsOnGround) every 5 ticks: %s ; ticks with IsOnGround=0: %s ; interp diff %s' % (nm, trace, off, d))
        save(nm, rows, s, init, COLS, 'Beannoid walks east on %s (cols 3..6)' % nm, first=1, last=70)

    # E8: magnet threshold: the descending ramp walked faster (anim 7 speed overridden by the harness): vertical drop per tick = 2/3 of the horizontal speed on this ramp
    def ramp_down2(col, row):
        if col < 3:
            return dict(h=4)
        if col <= 6:
            return dict(h=4 - (col - 3), slope=2)
        return None
    for sp in (160, 320, 480, 600, 640, 720, 800):
        (m, s, init, rows), d = pair(cells=ramp_down2, anim=7, n=45, pos=(40, 100), speed_override={7: sp})
        air = [r['tick'] for r in rows if r['slots'][s]['ground_144'] == 0]
        lines.append('E8 ramp-down walked at speed %d (%.2f px/tick horizontal): ticks with IsOnGround=0: %s ; interp diff %s' % (sp, sp * 768 / 65536, air, d))

    # E4: step up 16 px (height byte 1 from col 7): blocked, ForceAdjusted, slide handler for dir 24
    (m, s, init, rows), d = pair(cells=step_cols(7, 12, 1), anim=7, n=70, pos=(100, 100))
    t = first(rows, s, lambda r: r['adj_13C'] != 0)
    lines.append('E4 step up 16 px at x=168 (monster, slide allowed): first ForceAdjusted tick %s ; X then %.3f ; Y %.3f ; interp diff %s' % (t, rows[t - 1]['slots'][s]['px'] / 65536 if t else 0, rows[t - 1]['slots'][s]['py'] / 65536 if t else 0, d))
    save('step-up16', rows, s, init, COLS + ['py'], 'Beannoid walks east into a 16 px step at x=168', first=(t or 1) - 3, last=(t or 1) + 4)

    # E5: hop (anim 8) on flat ground
    (m, s, init, rows), d = pair(cells=None, anim=8, n=40, pos=(100, 100))
    t_land = first(rows, s, lambda r: r['collz_140'] != 0 and r['fz_B8'] == 0 and r['pz'] == 1, 1)
    lines.append('E5 hop anim 8 (speed 208, impulse 1280) on flat ground: PosZ by tick %s ; landing tick %s ; interp diff %s' % ([round(r['slots'][s]['pz'] / 65536, 2) for r in rows[:30]], t_land, d))
    save('hop-flat', rows, s, init, COLS, 'Beannoid hop (anim 8) on flat ground', first=1, last=30)

    # E6: hop onto a 16 px step (cols 6..): X reaches the step while rising
    (m, s, init, rows), d = pair(cells=step_cols(6, 12, 1), anim=8, n=40, pos=(100, 100))
    t_adj = first(rows, s, lambda r: r['adj_13C'] != 0)
    lines.append('E6 hop (anim 8) toward a 16 px step at x=144: first ForceAdjusted tick %s ; landing ticks %s ; final X %.2f PosZ %.2f ; interp diff %s' % (
        t_adj, [r['tick'] for r in rows if r['slots'][s]['collz_140'] != 0][:3], rows[-1]['slots'][s]['px'] / 65536, rows[-1]['slots'][s]['pz'] / 65536, d))
    save('hop-step16', rows, s, init, COLS, 'Beannoid hop (anim 8) toward a 16 px step at x=144', first=1, last=36)

    # E7: pit cell (ground property byte 0x10 -> word bit 0x1000 = the ClassA mask) at col 6: the monster stops; an arrow (not ClassA) flies over (see arrows)
    pit = lambda col, row: dict(gp=0x10) if col == 6 else None
    (m, s, init, rows), d = pair(cells=pit, anim=7, n=50, pos=(100, 100))
    t = first(rows, s, lambda r: r['adj_13C'] != 0)
    lines.append('E7 pit cell at col 6 (x 144..167), ClassA monster walking east: first ForceAdjusted tick %s at X %.3f ; interp diff %s' % (t, rows[t - 1]['slots'][s]['px'] / 65536 if t else 0, d))
    save('pit', rows, s, init, COLS, 'Beannoid walks east toward a pit cell (gp 0x10) at x=144', first=(t or 1) - 2, last=(t or 1) + 3)

    txt = '\n'.join(lines)
    open(os.path.join(OUT, 'walker-summary.txt'), 'w').write(txt + '\n')
    print(txt)
