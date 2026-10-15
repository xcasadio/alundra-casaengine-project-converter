"""k0_crate.py - a crate (map 392 sprite 36, real record, real C23 / E2) thrown by the hero: state left by the hero's throw code (docs h-g-deps 3.1), then the REAL passes.
The throw state is written by the harness exactly as 0x8002F... leaves it (documented in h-g-deps.md 3.1): crate.+0x28 = hero, hero.+0x2C = crate, crate.+0x24 = state (1 ground still, 2 ground moving or air still, 3 air moving),
crate.+0x8C = byte table 0x80023644[hero.+0x98] (east = 0x18), crate position = hero X, Y, Z + 0x200000.
Run: python -B -I k0_crate.py"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import k0lib as K
import interp2
from k0_arrows import wall_col, step_cols, OUT

COLS = ['status', 'anim_t', 'anim_c', 'izf_F8', 'fz_B8', 'fnz_EC', 'fx_C4', 'fnx_E4', 'px', 'pz', 'terr_138', 'floor_134', 'ground_144', 'collz_140', 'adj_13C', 'flags6C', 'carrier28']


def crate_throw(cells, state=1, direction=24, n=60, hero_px=(100, 100), ticker=None, hero_dz=0, crate_flags_and=None):
    m = K.make_world(cells=K.cells_fn(cells) if cells else None, hero_px=hero_px, direction=direction)
    slots, info = K.spawn_record_entities(m, 'map_392', 36, [(10, 11, 0)])
    s = slots[0]
    hero = K.ent(0)
    hz = m.peeks(hero + 0x11C) + hero_dz
    K.poke_entity(m, s, carrier28=hero, t24=state, dir_t=direction, px=m.peek(hero + 0x114), py=m.peek(hero + 0x118), pz=hz + 0x200000)
    m.poke(hero + 0x2C, K.ent(s))
    init = K.snap(m, s)
    rows = K.run(m, n, [s], ticker=ticker)
    return m, s, init, rows


def pair(**kw):
    a = crate_throw(**kw)
    b = crate_throw(ticker=interp2.tick2, **kw)
    return a, K.same_rows(a[3], b[3])


def save(name, rows, s, init, cols, title, first=1, last=10 ** 9):
    txt = K.table(rows, s, cols, first, last, 1, title=title)
    open(os.path.join(OUT, 'crate-%s.txt' % name), 'w').write(txt + '\n')
    K.dump_json(os.path.join(OUT, 'crate-%s.json' % name), rows, dict(name=name, slot=s, init=init))


def first(rows, s, pred):
    for r in rows:
        if pred(r['slots'][s]):
            return r['tick']
    return None


if __name__ == '__main__':
    lines = []
    for st in (1, 2, 3):
        (m, s, init, rows), d = pair(cells=None, state=st, n=40)
        t_imp = first(rows, s, lambda r: r['collz_140'] != 0 and r['status'] == 2 and r['ground_144'] != 0)
        t_e2 = first(rows, s, lambda r: r['anim_t'] == 2)
        t_dead = first(rows, s, lambda r: r['status'] == 4)
        pz = [round(r['slots'][s]['pz'] / 65536, 2) for r in rows[:22]]
        lines.append('D1 flat, throw state %d: first row X %.2f (spawn %.2f) ; landing tick %s ; E2 (anim 2) tick %s ; status 4 tick %s ; interp diff %s' % (
            st, rows[0]['slots'][s]['px'] / 65536, init['px'] / 65536, t_imp, t_e2, t_dead, d))
        lines.append('    Z by tick: %s' % pz)
        save('flat-state%d' % st, rows, s, init, COLS, 'crate throw east, state %d, flat' % st, first=1, last=24)

    # D2: step 16 px (height byte 1) on columns 9..12 (x 120..215): the crate lands ON the step
    for hb, nm in ((1, 'step16'), (2, 'step32'), (3, 'step48')):
        (m, s, init, rows), d = pair(cells=step_cols(5, 9, hb), state=2, n=40)
        t_adj = first(rows, s, lambda r: r['adj_13C'] != 0)
        t_land = first(rows, s, lambda r: r['collz_140'] != 0 and r['ground_144'] != 0)
        fin = [r for r in rows if r['slots'][s]['status'] == 2][-1]['slots'][s]
        lines.append('D2 %s (height byte %d on cols 5..8), state 2: first ForceAdjusted tick %s ; first landing tick %s ; last live X %.2f Z %.2f terr %.2f ; interp diff %s' % (
            nm, hb, t_adj, t_land, fin['px'] / 65536, fin['pz'] / 65536, fin['terr_138'] / 65536, d))
        save(nm, rows, s, init, COLS, 'crate throw east state 2, terrain height byte %d on cols 5..8' % hb, first=1, last=30)

    # D3: wall cell (walkability 0x40) at column 8 (x 192..215): crate at hero Z+32
    (m, s, init, rows), d = pair(cells=wall_col(5), state=2, n=30)
    t_adj = first(rows, s, lambda r: r['adj_13C'] != 0)
    lines.append('D3 wall column 5, state 2: first ForceAdjusted tick %s ; interp diff %s' % (t_adj, d))
    save('wall', rows, s, init, COLS, 'crate throw east state 2, wall col 5', first=1, last=22)

    # D4: thrown from a 48 px plateau (cols 0..7 height 3) off its edge: lands on the low ground
    plateau = lambda col, row: dict(h=3) if col <= 4 else None
    (m, s, init, rows), d = pair(cells=plateau, state=2, n=40)
    t_land = first(rows, s, lambda r: r['collz_140'] != 0 and r['ground_144'] != 0)
    lines.append('D4 thrown off a 48 px plateau (edge x=120): landing tick %s ; interp diff %s' % (t_land, d))
    save('plateau', rows, s, init, COLS, 'crate throw east state 2 from a 48 px plateau', first=1, last=36)

    txt = '\n'.join(lines)
    open(os.path.join(OUT, 'crate-summary.txt'), 'w').write(txt + '\n')
    print(txt)
