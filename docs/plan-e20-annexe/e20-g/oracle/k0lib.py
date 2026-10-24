"""k0lib.py - E20.e0k binary survey ("bin"): a small driver over the E20.0..E20.f oracle machine (copied to ./oracle, never edited except lib.py's capstone path).

REAL code of ALUN_CD.EXE (France) is run by the emulated machine for the whole tick (0x8003B388 order: slots 0x80038634, events 0x800386D0 with MovePlayer, counters 0x80038998,
lists 0x800384F4, animation 0x80038E18 = UpdateAnimation 0x80038AB4, physics 0x80038364, effects stub, hit pass stub) on NON-HERO entities:
  * projectiles of the common bank (types 3, 4, 7, 11, 12, 14, 15, 27..29) by the REAL SpawnWarpEntity 0x80039F58 (real InitializeEntity, real header flags and sizes);
  * a crate (map 392 sprite 36) by the REAL map-record spawner 0x8003A1B8 (header flags 0x3323E4, C23 / E2 handlers kept REAL);
  * a Beannoid (map 14 sprite 79) as the "walker" (its handlers stay inert; the harness chooses its animation).
Handlers kept real: C60 C61 C94 E1 E9 E10 E17 (projectiles), C23 (0x8006B848) and E2 (0x8007ED30) (crate).  Everything else of the handler tables is an inert journalling stub.
Nothing here knows an expected value.
"""
import os
import sys
import json

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, 'oracle'))
import pworld as P
import world as W
import c1world as C
import bank
import interp2
from machine import M32, s32

ENT0, STRIDE = W.ENT0, W.STRIDE
DATA = r"D:/development/repo/alundra-casaengine-project-converter/data-extracted/data"
C23, E2 = 0x8006B848, 0x8007ED30

# (name, offset, signed)
FIELDS = [
    ('status', 0x10, 1), ('t1C', 0x1C, 1), ('t24', 0x24, 1), ('carrier28', 0x28, 0), ('carried2C', 0x2C, 0), ('type68', 0x68, 1), ('flags6C', 0x6C, 0),
    ('anim_t', 0x88, 1), ('dir_t', 0x8C, 1), ('anim_c', 0x90, 1), ('animflags_B4', 0xB4, 0), ('reset_AC', 0xAC, 1),
    ('izf_F8', 0xF8, 1), ('fz_B8', 0xB8, 1), ('fx_C4', 0xC4, 1), ('fy_C8', 0xC8, 1),
    ('dx_DC', 0xDC, 1), ('dy_E0', 0xE0, 1), ('fnx_E4', 0xE4, 1), ('fny_E8', 0xE8, 1), ('fnz_EC', 0xEC, 1),
    ('px', 0x114, 1), ('py', 0x118, 1), ('pz', 0x11C, 1), ('mz_1E0', 0x1E0, 1),
    ('floor_134', 0x134, 1), ('terr_138', 0x138, 1), ('adj_13C', 0x13C, 1), ('collz_140', 0x140, 1), ('ground_144', 0x144, 1),
    ('xcoll_130', 0x130, 0), ('support_12C', 0x12C, 0), ('moved_168', 0x168, 1),
    ('p180', 0x180, 0), ('p184', 0x184, 0), ('p188', 0x188, 0), ('p18C', 0x18C, 1), ('hitc_220', 0x220, 1), ('prog228', 0x228, 1),
]
OFF = {n: o for n, o, _ in FIELDS}


def ent(slot):
    return ENT0 + STRIDE * slot


def snap(m, slot):
    e = ent(slot)
    return {n: (m.peeks(e + o) if sg else m.peek(e + o)) for n, o, sg in FIELDS}


def active(m):
    return [s for s in range(64) if m.peek(ent(s) + 0x10) != 0]


# ---- cells ---------------------------------------------------------------------------------------------------------------
def cells_flat(col, row):
    return (0, 0, 0, 0)


def cells_fn(spec):
    """spec: callable (col,row) -> dict(h=, slope=, wk=, gp=) or None (flat)"""
    def f(col, row):
        d = spec(col, row)
        if not d:
            return (0, 0, 0, 0)
        return (d.get('h', 0), d.get('slope', 0), d.get('wk', 0), d.get('gp', 0))
    return f


# ---- world ---------------------------------------------------------------------------------------------------------------
def make_world(cells=None, hero_px=(100, 100), direction=24, keep=None, types=P.PROJECTILE_TYPES, seed=0x12345678):
    k = set(P.REAL) | {C23, E2}
    if keep:
        k |= set(keep)
    m, meta = P.setup(item=None, hero_px=hero_px, direction=direction, targets=(), cells=cells, seed=seed, types=types, keep=k)
    return m


def hero_z(m):
    return m.peeks(ent(0) + 0x11C)


def spawn_global(m, typ, x_px, y_px, z_raw, direction, owner=None):
    """REAL SpawnWarpEntity 0x80039F58(owner, table 0, type, X, Y, Z, dir); returns the slot index (None when refused)"""
    sp = 0x801FFF00
    m.poke(sp + 0x10, (y_px * 65536) & M32)
    m.poke(sp + 0x14, z_raw & M32)
    m.poke(sp + 0x18, direction)
    p = m.call(0x80039F58, ent(0) if owner is None else owner, 0, typ, (x_px * 65536) & M32, limit=5_000_000) & M32
    if p == 0:
        return None
    return (p - ent(0)) // STRIDE


def _record(path_map, sprite_index):
    d = json.load(open(DATA + '/' + path_map + '.json', encoding='utf-8-sig'))
    si = d['SpriteInfo']
    ents = [e for e in si['Entities']['Entities'] if e is not None and e['SpriteTableIndex'] == sprite_index]
    return si['SpriteRecords'][sprite_index], ents, si['Header']['SpriteTableSize'] // 4


_CACHE = {}


def spawn_record_entities(m, mapname, sprite_index, positions, slot_status=2, dirbyte=0xC0, rec_addr=None, ent_addr=None, ptr_addr=None):
    """REAL map-record spawner 0x8003A1B8(0, i, 0) on `positions` = list of (xpos, ypos, height) records of the sprite `sprite_index` of `mapname`
    (the real sprite record is laid out in RAM by bank.py; entity 0 of that sprite in that map is the template).  Returns the slots."""
    key = (mapname, sprite_index)
    if key not in _CACHE:
        _CACHE[key] = _record(mapname, sprite_index)
    rec, ents, tsize = _CACHE[key]
    tmpl = dict(ents[0])
    tmpl['SpriteDirection'] = dirbyte
    rec_addr = rec_addr or C.BEAN_REC
    ent_addr = ent_addr or C.ENT_REC
    ptr_addr = ptr_addr or C.SPR_PTRS
    info = bank.install(m, rec_addr, rec_addr, rec)
    for i in range(tsize):
        m.poke(ptr_addr + 4 * i, 0)
    m.poke(ptr_addr + 4 * sprite_index, rec_addr)
    recs = b''.join(C.entity_record_bytes(tmpl, xp, yp, h) for xp, yp, h in positions)
    m.poke_bytes(ent_addr, recs)
    m.poke(C.MAPBLK + 8, ent_addr)
    m.poke(C.MAPBLK + 0xC, ptr_addr)
    m.poke(C.MAPBLK + 0x10, tsize)
    m.poke(C.MAPBLK + 0x14, len(positions))
    slots = []
    for i in range(len(positions)):
        p = m.call(0x8003A1B8, 0, i, 0, limit=5_000_000) & M32
        if p == 0:
            raise RuntimeError('map-record spawner refused %s sprite %d record %d' % (mapname, sprite_index, i))
        slots.append((p - ent(0)) // STRIDE)
        m.poke(p + 0x10, slot_status)
    return slots, info


def poke_entity(m, slot, **kw):
    """harness write of named fields (documented in each scenario)"""
    e = ent(slot)
    for n, v in kw.items():
        m.poke(e + OFF[n], v & M32)


def place(m, slot, x_px=None, y_px=None, z_raw=None):
    """harness write of the position AND the derived words the spawner computed (+0x1D8, +0x1DC, +0x1E0) - the binary recomputes them in the pre-pass of 0x80038364 (0x800383BC-0x800383E0)"""
    e = ent(slot)
    if x_px is not None:
        m.poke(e + 0x114, (x_px * 65536) & M32)
    if y_px is not None:
        m.poke(e + 0x118, (y_px * 65536) & M32)
    if z_raw is not None:
        m.poke(e + 0x11C, z_raw & M32)


# ---- running -------------------------------------------------------------------------------------------------------------
def fmt(name, v):
    if name in ('px', 'py', 'pz', 'mz_1E0', 'floor_134', 'terr_138'):
        return '%.4f' % (v / 65536)
    if name in ('flags6C', 'p180', 'p184', 'p188', 'carrier28', 'carried2C', 'xcoll_130', 'support_12C', 'animflags_B4'):
        return '%X' % v
    return str(v)


def run(m, n, slots, pre=None, post=None, ticker=None, hold=0):
    """n ticks of the whole pipeline; pre(m, t) before the tick, post(m, t, rows_of_tick) after.  Returns rows = list of dict(tick, slots{slot: snap}, journal)."""
    pad = W.Pad()
    rows = []
    for t in range(1, n + 1):
        m.tick_no = t
        j0 = len(m.journal)
        if pre:
            pre(m, t)
        (ticker or W.tick)(m, pad, hold)
        r = dict(tick=t, slots={s: snap(m, s) for s in slots}, journal=[P.fmt_j(j) for j in m.journal[j0:] if not j[1].startswith('pass_')])
        if post:
            post(m, t, r)
        rows.append(r)
    return rows


def table(rows, slot, cols, first=1, last=10 ** 9, every=1, title=''):
    out = []
    if title:
        out.append(title)
    out.append('tick ' + ' '.join('%s' % c for c in cols))
    for r in rows:
        if first <= r['tick'] <= last and (r['tick'] - first) % every == 0:
            d = r['slots'].get(slot)
            if d is None:
                out.append('t%03d slot%d absent' % (r['tick'], slot))
                continue
            j = [x for x in r['journal'] if not x.startswith('stub')]
            out.append('t%03d ' % r['tick'] + ' '.join('%s=%s' % (c, fmt(c, d[c])) for c in cols) + ('  | ' + ' '.join(j) if j else ''))
    return '\n'.join(out)


def same_rows(a, b):
    for ra, rb in zip(a, b):
        if ra['slots'] != rb['slots'] or ra['journal'] != rb['journal']:
            return ra['tick']
    return None


def dump_json(path, rows, meta=None):
    with open(path, 'w') as f:
        json.dump(dict(meta=meta, rows=[dict(tick=r['tick'], journal=r['journal'], slots={str(s): d for s, d in r['slots'].items()}) for r in rows]), f, indent=0, sort_keys=True)


# ---- real map cells (exported tilemap, same arrays as docs/plan-e19-h2-annexe/tools/zsim.py) -----------------------------------
def cells_from_export(mid):
    import glob, re
    root = "D:/development/repo/alundra-casaengine-project-converter/alundra-project/Maps"
    for f in glob.glob(root + "/*/*/tilemap/*.tileMap"):
        f = f.replace("\\", "/")
        folder = os.path.basename(os.path.dirname(os.path.dirname(f)))
        mm = re.search(r"-(\d+)$", folder)
        if mm and int(mm.group(1)) == mid:
            tm = json.load(open(f, encoding='utf-8'))
            cp = tm['custom_properties']
            cells = json.loads(cp['AlundraCells'])
            W_, H_ = tm['map_size']['w'], tm['map_size']['h']
            gravity, visc = int(cp.get('Gravity', 128)), int(cp.get('ZViscosity', 4096))

            def f_(col, row):
                if col >= W_ or row >= H_:
                    return None
                i = row * W_ + col
                return dict(h=cells['height'][i], slope=cells['slope'][i], wk=cells['walkability'][i], gp=cells['ground_property'][i])
            return f_, (W_, H_, gravity, visc)
    raise KeyError(mid)
