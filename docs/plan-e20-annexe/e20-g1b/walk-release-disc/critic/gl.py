"""gl.py - E20.g binary survey ("bin"): scenario library on top of k0lib (E20.e0k oracle machine, copied to ./oracle).

REAL code of ALUN_CD.EXE (France): the hero is driven by the REAL MovePlayer 0x80031B50 (called by the events pass 0x800386D0) fed with a scripted pad; the crate is the REAL map record 36 of map 392
spawned by the REAL map-record spawner 0x8003A1B8; C23 0x8006B848 and E2 0x8007ED30 are REAL; every other sprite handler is an inert journalling stub.
Harness writes (each scenario documents them): the pad words, the cell field, the crate/platform placement, nothing else.
"""
import os
import sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, 'oracle'))
import k0lib as K
import world as W
import pworld as P
from machine import M32, s32

UP, RIGHT, DOWN, LEFT = W.UP, W.RIGHT, W.DOWN, W.LEFT
TRI, CIRCLE, CROSS, SQUARE = W.TRIANGLE, W.CIRCLE, W.CROSS, W.SQUARE
OUT = os.path.join(HERE, 'out')

HERO = K.ent(0)

HFIELDS = [
    ('status', 0x10, 1), ('anim_t', 0x88, 1), ('anim_c', 0x90, 1), ('dir_t', 0x8C, 1), ('adir', 0x98, 1),
    ('carried2C', 0x2C, 0), ('off30', 0x30, 1), ('off34', 0x34, 1), ('off38', 0x38, 1),
    ('px', 0x114, 1), ('py', 0x118, 1), ('pz', 0x11C, 1),
    ('fx', 0xC4, 1), ('fy', 0xC8, 1), ('fz', 0xB8, 1), ('izf', 0xF8, 1),
    ('ground', 0x144, 1), ('collz', 0x140, 1), ('adj', 0x13C, 1), ('xcoll', 0x130, 0), ('support', 0x12C, 0),
    ('terr', 0x138, 1), ('floor', 0x134, 1), ('slope', 0x18C, 1), ('p180', 0x180, 0), ('p188', 0x188, 0),
    ('flags6C', 0x6C, 0), ('b0', 0xB0, 1), ('reset_AC', 0xAC, 1),
]
CFIELDS = [
    ('status', 0x10, 1), ('t24', 0x24, 1), ('carrier28', 0x28, 0), ('carried2C', 0x2C, 0),
    ('anim_t', 0x88, 1), ('anim_c', 0x90, 1), ('dir_t', 0x8C, 1), ('flags6C', 0x6C, 0), ('animflags_B4', 0xB4, 0), ('reset_AC', 0xAC, 1),
    ('izf', 0xF8, 1), ('fx', 0xC4, 1), ('fy', 0xC8, 1), ('fz', 0xB8, 1),
    ('px', 0x114, 1), ('py', 0x118, 1), ('pz', 0x11C, 1),
    ('ground', 0x144, 1), ('collz', 0x140, 1), ('adj', 0x13C, 1), ('xcoll', 0x130, 0), ('support', 0x12C, 0), ('moved', 0x168, 1),
    ('terr', 0x138, 1), ('floor', 0x134, 1), ('hitc', 0x220, 1), ('p184', 0x184, 0), ('letter', 0x228, 1), ('sheet', 0x1C4, 0),
]


def snapf(m, base, fields):
    return {n: (m.peeks(base + o) if sg else m.peek(base + o)) for n, o, sg in fields}


def fmt(n, v):
    if n in ('px', 'py', 'pz', 'terr', 'floor', 'off30', 'off34', 'off38', 'fx', 'fy', 'fz'):
        return '%.4f' % (v / 65536)
    if n in ('carried2C', 'carrier28', 'xcoll', 'support', 'flags6C', 'p180', 'p188', 'p184', 'animflags_B4', 'sheet'):
        return '%X' % v
    return str(v)


def pad_script(seq):
    """seq = list of (n_ticks, hold) -> dict tick -> hold (1-based)"""
    d, t = {}, 1
    for n, h in seq:
        for _ in range(n):
            d[t] = h
            t += 1
    return d, t - 1


def world(cells=None, hero_px=(100, 100), direction=24, seed=0x12345678, keep=None):
    return K.make_world(cells=K.cells_fn(cells) if cells else None, hero_px=hero_px, direction=direction, seed=seed, keep=keep)


def add_crate(m, xpx, ypx, mapname='map_392', sprite=36, rec_addr=None, ent_addr=None, ptr_addr=None, height=0, zraw=None):
    """REAL map-record spawner; then the harness places the crate at (xpx, ypx) exactly as the spawner would for a record there (position, derived words, terrain)."""
    kw = {}
    if rec_addr:
        kw = dict(rec_addr=rec_addr, ent_addr=ent_addr, ptr_addr=ptr_addr)
    slots, info = K.spawn_record_entities(m, mapname, sprite, [(10, 11, height)], **kw)
    s = slots[0]
    e = K.ent(s)
    m.poke(e + 0x114, (xpx * 65536) & M32)
    m.poke(e + 0x118, (ypx * 65536) & M32)
    m.poke(e + 0x1D8, (xpx * 65536 + m.peeks(e + 0x1E4)) & M32)
    m.poke(e + 0x1DC, (ypx * 65536 + m.peeks(e + 0x1E8)) & M32)
    T = m.call(0x800370C4, e)
    m.poke(e + 0x138, T)
    if zraw is None:
        zraw = T + 1
    m.poke(e + 0x11C, zraw & M32)
    m.poke(e + 0x1E0, (zraw + m.peeks(e + 0x1EC)) & M32)
    return s


def run(m, n, pads, crates=(), extra=(), pre=None, post=None, ticker=None):
    """n ticks; pads = dict tick->hold; returns rows with hero + each slot in crates/extra"""
    pad = W.Pad()
    rows = []
    for t in range(1, n + 1):
        m.tick_no = t
        j0 = len(m.journal)
        if pre:
            pre(m, t)
        (ticker or W.tick)(m, pad, pads.get(t, 0))
        r = dict(tick=t, hold=pads.get(t, 0), hero=snapf(m, HERO, HFIELDS), c={s: snapf(m, K.ent(s), CFIELDS) for s in list(crates) + list(extra)},
                 journal=[P.fmt_j(j) for j in m.journal[j0:] if not j[1].startswith('pass_')],
                 lists=dict(active=m.peeks(0x80127D28), coll=m.peeks(0x80127D2C)), carry_ctr=m.peeks(0x80098F28), use164=m.peeks(0x80127164))
        if post:
            post(m, t, r)
        rows.append(r)
    return rows


def hrow(r, cols):
    return ' '.join('%s=%s' % (c, fmt(c, r['hero'][c])) for c in cols)


def crow(r, s, cols):
    d = r['c'][s]
    return ' '.join('%s=%s' % (c, fmt(c, d[c])) for c in cols)


HCOLS = ['anim_t', 'anim_c', 'dir_t', 'adir', 'carried2C', 'off30', 'off34', 'off38', 'px', 'py', 'pz', 'fx', 'fz', 'izf', 'ground', 'adj', 'xcoll', 'support']
CCOLS = ['status', 't24', 'carrier28', 'anim_t', 'anim_c', 'flags6C', 'px', 'py', 'pz', 'fx', 'fz', 'ground', 'collz', 'adj', 'xcoll', 'support']


def table(rows, crates, first=1, last=10 ** 9, every=1, hcols=HCOLS, ccols=CCOLS, title=''):
    out = []
    if title:
        out.append(title)
    for r in rows:
        if first <= r['tick'] <= last and (r['tick'] - first) % every == 0:
            line = 't%03d pad=%04X | H %s' % (r['tick'], r['hold'], hrow(r, hcols))
            for s in crates:
                line += ' | C%d %s' % (s, crow(r, s, ccols))
            j = [x for x in r['journal'] if not x.startswith('stub')]
            if j:
                line += ' | J ' + ' '.join(j)
            out.append(line)
    return '\n'.join(out)
