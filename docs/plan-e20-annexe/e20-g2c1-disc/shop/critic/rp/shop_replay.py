"""shop_replay.py - replays on the REAL code of ALUN_CD.EXE (France) of a SHOP ITEM (global sprite 66 'Herbe medicinale', the same header bytes as 48, 50, 67..71, 81, 83, 85, 113).

Machine = the E20 oracle machine (copied under ./work/orc, never edited): REAL MovePlayer 0x80031B50 (TryLift / PlayerTryAction / throw), REAL SpawnWarpEntity 0x80039F58
(REAL InitializeEntity: the header flags come from the real code), REAL C4 0x80066984 (kept), REAL E0 0x8007ED10, REAL physics passes. Every other sprite handler is an inert stub.

Harness writes (each scenario says which): the pad words, the cell field, and - to stand for the two script instructions of the item's C program (C[4] of map 163 / 258 / 298,
C[1] of map 398), which the machine does not interpret - `flags |= 0x380` (opcode 0x62 [128,128,3]) and `+0x78 = 4` (opcode 0x41 [2,4]; +0x78 is the native C code,
InitializeEntity 0x80039D04 copies header bytes 0x13..0x17 to +0x70/+0x78/+0x7C/+0x80/+0x84). Nothing here knows an expected value.

Run: python -B -I -X utf8 shop_replay.py [scenario ...]    (cwd = this folder; capstone is added to sys.path by lib.py)"""
import os, sys, json
HERE = os.path.dirname(os.path.abspath(__file__))
ORC = os.path.join(HERE, 'work', 'orc')
sys.path.insert(0, ORC)
import gl
import k0lib as K
import pworld as P
from machine import M32

OUT = os.path.join(HERE, 'out')
os.makedirs(OUT, exist_ok=True)
C4, E0 = 0x80066984, 0x8007ED10
SHOP_TYPES = (66, 71, 68, 113, 50)          # 163 / 258 / 298: records 3, 4, 5, 6, 8
gl.CFIELDS.extend([('c78', 0x78, 0), ('e80', 0x80, 1), ('f6c', 0x6C, 0), ('t68', 0x68, 1), ('slope18C', 0x18C, 1), ('bounced', 0x278, 1)])


def build(keep, cells=None, hero_px=(100, 100), extra_types=()):
    return K.make_world(cells=K.cells_fn(cells) if cells else None, hero_px=hero_px, direction=24, seed=0x12345678, keep=keep, types=tuple(P.PROJECTILE_TYPES) + tuple(extra_types))


def add_item(m, typ, xpx, ypx):
    """REAL SpawnWarpEntity of the global sprite `typ` at (xpx, ypx), then the same placement steps as gl.add_crate (position, derived words, terrain)."""
    s = K.spawn_global(m, typ, xpx, ypx, 0, 0)
    assert s is not None, 'SpawnWarpEntity refused type %d' % typ
    e = K.ent(s)
    m.poke(e + 0x10, 2)                       # status 2 (Normal), as spawn_record_entities does for a map record
    m.poke(e + 0x114, (xpx * 65536) & M32)
    m.poke(e + 0x118, (ypx * 65536) & M32)
    m.poke(e + 0x1D8, (xpx * 65536 + m.peeks(e + 0x1E4)) & M32)
    m.poke(e + 0x1DC, (ypx * 65536 + m.peeks(e + 0x1E8)) & M32)
    T = m.call(0x800370C4, e)
    m.poke(e + 0x138, T)
    zraw = T + 1
    m.poke(e + 0x11C, zraw & M32)
    m.poke(e + 0x1E0, (zraw + m.peeks(e + 0x1EC)) & M32)
    return s


def script_effects(m, s, c_code=4, flags_or=0x380):
    """what the item's C program does at its first tick: 0x62 [128,128,3] and 0x41 [2,4]"""
    e = K.ent(s)
    if flags_or:
        m.poke(e + 0x6C, (m.peek(e + 0x6C) | flags_or) & M32)
    if c_code is not None:
        m.poke(e + 0x78, c_code)


def run_one(title, typ=66, scripted=True, c_code=4, keep=None, cells=None, obj_x=130, n=140, throw_t=61, hold_throw=gl.RIGHT, hero_px=(100, 100), seq=None):
    m = build(keep if keep is not None else {C4}, cells=cells, hero_px=hero_px, extra_types=(typ,))
    s = add_item(m, typ, obj_x, hero_px[1])
    e = K.ent(s)
    pre = dict(flags=m.peek(e + 0x6C), c78=m.peek(e + 0x78), e80=m.peeks(e + 0x80), a70=m.peek(e + 0x70), t68=m.peeks(e + 0x68), sizes=(m.peeks(e + 0x214), m.peeks(e + 0x218), m.peeks(e + 0x21C)))
    if scripted:
        script_effects(m, s, c_code=c_code)
    post = dict(flags=m.peek(e + 0x6C), c78=m.peek(e + 0x78))
    seq = seq or [(10, gl.RIGHT), (1, gl.RIGHT | gl.SQUARE), (throw_t - 12, 0), (1, hold_throw | gl.SQUARE), (n - throw_t, 0)]
    pads, nn = gl.pad_script(seq)
    rows = gl.run(m, nn, pads, crates=[s])
    return m, s, rows, pre, post


def fmtflags(v):
    return '0x%X' % v


def describe(title, rows, s, pre, post, throw_t=61):
    out = ['## ' + title,
           'header read by the REAL InitializeEntity: flags +0x6C = %s, native C code +0x78 = %d, native A +0x70 = %d, E +0x80 = %d, +0x68 = %d, box (%d,%d,%d)' % (
               fmtflags(pre['flags']), pre['c78'], pre['a70'], pre['e80'], pre['t68'], *pre['sizes']),
           'after the harness equivalent of the script: flags = %s, C code = %d' % (fmtflags(post['flags']), post['c78'])]
    first_lift = next((r['tick'] for r in rows if r['c'][s]['carrier28'] != 0), None)
    out.append('first tick with +0x28 (carrier) set: %s ; hero +0x2C at that tick: %s' % (first_lift, ('%X' % rows[first_lift - 1]['hero']['carried2C']) if first_lift else '-'))
    last = rows[-1]
    h, c = last['hero'], last['c'][s]
    out.append('final: item status %d t24 %d carrier28 %X anim_t %d flags %s pos (%.2f, %.2f, %.2f) ground %d ; hero anim_t %d +0x2C %X pos (%.2f, %.2f)' % (
        c['status'], c['t24'], c['carrier28'], c['anim_t'], fmtflags(c['flags6C']), c['px'] / 65536, c['py'] / 65536, c['pz'] / 65536, c['ground'], h['anim_t'], h['carried2C'], h['px'] / 65536, h['py'] / 65536))
    # changes of the item
    prev = None
    ch = []
    for r in rows:
        d = r['c'][s]
        key = (d['status'], d['t24'], d['carrier28'], d['anim_t'], d['flags6C'], d['ground'])
        if key != prev:
            ch.append('t%03d status=%d t24=%d carrier28=%X anim_t=%d flags=%s ground=%d pos=(%.1f,%.1f,%.1f)' % (r['tick'], d['status'], d['t24'], d['carrier28'], d['anim_t'], fmtflags(d['flags6C']), d['ground'], d['px'] / 65536, d['py'] / 65536, d['pz'] / 65536))
            prev = key
    out.append('changes of (status, t24, carrier, anim, flags, ground):')
    out.extend('   ' + x for x in ch[:40])
    jr = [(r['tick'], [x for x in r['journal'] if not x.startswith('stub')]) for r in rows if [x for x in r['journal'] if not x.startswith('stub')]]
    out.append('journal (non-stub): %s' % jr[:14])
    return '\n'.join(out)


SC = {}


def sc(f):
    SC[f.__name__] = f
    return f


@sc
def a_header_only():
    """A: the item as exported (no script yet): kind 0, C code 255 (inert stub). The hero walks into it and presses Square."""
    m, s, rows, pre, post = run_one('A', scripted=False, keep={C4})
    return describe('A: header only (flags as read by the real InitializeEntity), hero presses Square against it', rows, s, pre, post)


@sc
def b_script_then_c4():
    """B: after the two instructions of C[4] (flags |= 0x380, C code 4), C4 real: lift, walk, throw east on flat ground."""
    m, s, rows, pre, post = run_one('B', scripted=True, c_code=4, keep={C4})
    open(os.path.join(OUT, 'shop_b_table.txt'), 'w').write(gl.table(rows, [s], first=8, last=110, every=1, hcols=['anim_t', 'carried2C', 'px', 'pz', 'ground'], ccols=['status', 't24', 'carrier28', 'anim_t', 'flags6C', 'px', 'py', 'pz', 'ground', 'adj', 'support'], title='B') + '\n')
    return describe('B: C4 real after the script (lift, walk, throw east, flat ground)', rows, s, pre, post)


@sc
def c_script_without_c4():
    """C: flags |= 0x380 but the C code stays at the header value (255, inert stub): the DLL-at-HEAD situation if the type were wired without (C,4)."""
    m, s, rows, pre, post = run_one('C', scripted=True, c_code=None, keep=set())
    return describe('C: flags 0x380 set but no C4 (native code stays 255, inert): the item stays in the hands', rows, s, pre, post)


@sc
def d_hole():
    """D: thrown east onto cells whose walkability is 0x04 (a hole) at columns 7..8 (x 168..216 px; cells are 24 px wide): does the shop item vanish?"""
    cells = lambda cc, rr: dict(wk=0x04) if cc in (7, 8) else None
    m, s, rows, pre, post = run_one('D', scripted=True, c_code=4, keep={C4}, cells=cells)
    return describe('D: thrown onto a hole (walkability 0x04, columns 7-8)', rows, s, pre, post)


@sc
def e_wall_rebound_and_stay():
    """E: thrown east against a wall at column 6 (walkability 0x40): does it break? stays?"""
    cells = lambda cc, rr: dict(wk=0x40) if cc == 6 else None
    m, s, rows, pre, post = run_one('E', scripted=True, c_code=4, keep={C4}, cells=cells)
    return describe('E: thrown against a wall (column 6)', rows, s, pre, post)


@sc
def f_still_throw():
    """F: thrown while standing still (no direction held)."""
    m, s, rows, pre, post = run_one('F', scripted=True, c_code=4, keep={C4}, hold_throw=0)
    return describe('F: thrown standing still', rows, s, pre, post)


# ---- extra scenario G (appended after the first runs): item on a 16 px ledge, hero on the floor ----
def run_ledge(title, jump_first=False, n=120):
    cells = lambda cc, rr: dict(h=1) if cc in (5, 6, 7) else None
    m = build({C4}, cells=cells, extra_types=(66,))
    s = add_item(m, 66, 132, 100)
    script_effects(m, s, c_code=4)
    e = K.ent(s)
    pre = dict(flags=m.peek(e + 0x6C), c78=m.peek(e + 0x78), e80=m.peeks(e + 0x80), a70=m.peek(e + 0x70), t68=m.peeks(e + 0x68), sizes=(0, 0, 0))
    post = dict(flags=m.peek(e + 0x6C), c78=m.peek(e + 0x78))
    seq = [(14, gl.RIGHT), (1, gl.RIGHT | gl.SQUARE), (n - 15, 0)]
    pads, nn = gl.pad_script(seq)
    rows = gl.run(m, nn, pads, crates=[s])
    return m, s, rows, pre, post


@sc
def g_ledge_from_floor():
    """G: the item stands on a 16 px ledge (cells columns 5-7, height 1; the shop's counter cells are 16 px above the floor), the hero walks right on the floor and presses Square."""
    m, s, rows, pre, post = run_ledge('G')
    return describe('G: item on a 16 px ledge, hero on the floor walks into the ledge and presses Square', rows, s, pre, post)


@sc
def h_ledge_jump_then_lift():
    """H: as G but the hero jumps (Cross + Right) onto the ledge first, walks to the item and presses Square, then carries it back to the floor edge and throws it east."""
    cells = lambda cc, rr: dict(h=1) if cc in (5, 6, 7) else None
    m = build({C4}, cells=cells, extra_types=(66,))
    s = add_item(m, 66, 140, 100)
    script_effects(m, s, c_code=4)
    e = K.ent(s)
    pre = dict(flags=m.peek(e + 0x6C), c78=m.peek(e + 0x78), e80=m.peeks(e + 0x80), a70=m.peek(e + 0x70), t68=m.peeks(e + 0x68), sizes=(0, 0, 0))
    post = dict(flags=m.peek(e + 0x6C), c78=m.peek(e + 0x78))
    seq = [(8, gl.RIGHT), (1, gl.RIGHT | gl.CROSS), (30, gl.RIGHT), (1, gl.RIGHT | gl.SQUARE), (40, 0), (1, gl.RIGHT | gl.SQUARE), (60, 0)]
    pads, nn = gl.pad_script(seq)
    rows = gl.run(m, nn, pads, crates=[s])
    open(os.path.join(OUT, 'shop_h_table.txt'), 'w').write(gl.table(rows, [s], first=1, last=nn, every=2, hcols=['anim_t', 'carried2C', 'px', 'pz', 'ground'], ccols=['status', 't24', 'carrier28', 'anim_t', 'px', 'pz', 'ground'], title='H') + chr(10))
    return describe('H: hero jumps onto a 16 px ledge, lifts the item, throws it', rows, s, pre, post)


@sc
def i_kind1_but_not_collidable():
    """I: kind 1 and C4, but the Collidable bit (0x80) cleared (flags 0x230300): the collision list holds only collidables (0x800384F4-0x800385D0), so no contact, no lift (and the hero walks through it)."""
    m = build({C4}, extra_types=(66,))
    s = add_item(m, 66, 130, 100)
    script_effects(m, s, c_code=4)
    e = K.ent(s)
    m.poke(e + 0x6C, m.peek(e + 0x6C) & ~0x80 & M32)
    pre = dict(flags=m.peek(e + 0x6C), c78=m.peek(e + 0x78), e80=m.peeks(e + 0x80), a70=m.peek(e + 0x70), t68=m.peeks(e + 0x68), sizes=(0, 0, 0))
    post = dict(flags=m.peek(e + 0x6C), c78=m.peek(e + 0x78))
    seq = [(10, gl.RIGHT), (1, gl.RIGHT | gl.SQUARE), (60, 0)]
    pads, nn = gl.pad_script(seq)
    rows = gl.run(m, nn, pads, crates=[s])
    return describe('I: kind 1 + C4 but Collidable cleared', rows, s, pre, post)


if __name__ == '__main__':
    names = sys.argv[1:] or list(SC)
    for nme in names:
        res = SC[nme]()
        print(res)
        print()
        open(os.path.join(OUT, 'shop_replay_%s.txt' % nme), 'w').write(res + '\n')
