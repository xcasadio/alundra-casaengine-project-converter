"""pworld.py - f-projectiles oracle (scratch copy of the E20.c1 oracle tools, never run in place).

Real code of ALUN_CD.EXE (France) on the E20.c1 machine, with these differences from c1world.py:
  * SpawnWarpEntity 0x80039F58 is REAL (c1world stubbed it); the sprite records of the common ("global") bank, SpriteRecords[i] of map_alundra.json, are laid out in
    RAM and reachable through the block 0x80126EC0 (+0xC pointer table, +0x10 count) that 0x80039B28 reads for table == 0;
  * the hero equips a weapon with the real AddItem/SetPlayerWeaponId and attacks with the real MovePlayer, so the shot is spawned by the real 0x8002F768;
  * the projectile handlers (C60 C61 C94 E1 E9 E10 E17) are REAL; every other handler stays an inert journalling stub (the targets are inert Beannoids of map 14);
  * effects 0x8003BDD8 / 0x8003BE74 / sound 0x800490FC / the effects pass 0x80038E84 stay stubs (journalled).
Nothing here knows an expected value.
"""
import os
import sys
import json

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from machine import Machine, s32, M32
import world as W
import bank
import c1world as C

ENT0, STRIDE = W.ENT0, W.STRIDE
GLOBAL_BLOCK = 0x80126EC0
GPTRS = 0x80152000                   # 257 pointers
GREC = 0x80160000                    # projectile sprite records, 0x4000 apart
MAP_ALUNDRA = r"D:/development/repo/alundra-casaengine-project-converter/data-extracted/data/map_alundra.json"
ITEM_PROPS = 0x800B9FE8

_G = None


def global_records():
    global _G
    if _G is None:
        d = json.load(open(MAP_ALUNDRA, encoding='utf-8-sig'))
        _G = d['SpriteInfo']['SpriteRecords']
    return _G


def install_global(m, types):
    recs = global_records()
    n = len(recs)
    for i in range(n):
        m.poke(GPTRS + 4 * i, 0)
    m.poke(GLOBAL_BLOCK + 0xC, GPTRS)
    m.poke(GLOBAL_BLOCK + 0x10, n)
    out = {}
    for k, t in enumerate(sorted(set(types))):
        addr = GREC + 0x4000 * k
        info = bank.install(m, addr, addr, recs[t])
        assert info['end'] - addr < 0x4000, (t, info['end'] - addr)
        m.poke(GPTRS + 4 * t, addr)
        out[t] = info
    return out


def install_inert_except(m, keep):
    """hook every handler of the five tables and the bytecode runner with the journalling no-op, EXCEPT the addresses in keep"""
    addrs = {}
    for k in range(6):
        p = m.peek(C.HANDLER_TABLES + 4 * k)
        if p < 0x80000000:
            continue
        for i in range(256):
            h = m.peek(p + 4 * i)
            if 0x80020000 <= h < 0x800CA800 and h not in keep:
                addrs.setdefault(h, []).append((C.LETTERS[k], i))

    def make(addr):
        def stub(m2):
            a0 = m2.r[4]
            slot = (a0 - ENT0) // STRIDE if ENT0 <= a0 < ENT0 + 64 * STRIDE else None
            letter = m2.peek(a0 + 0x228)
            m2.journal.append((m2.tick_no, 'handler', [slot, letter, addr]))
            m2.r[2] = 0
        return stub
    for a in sorted(addrs):
        m.hooks[a] = make(a)
    m.hooks[0x8004205C] = make(0x8004205C)
    return len(addrs)


REAL = {0x8007A8A0, 0x8007A958, 0x8007C0D8, 0x8007ED10, 0x8007EF50, 0x8007F23C, 0x8007F420}
PROJECTILE_TYPES = (3, 4, 7, 11, 12, 14, 15, 27, 28, 29)


def item_props(m, item):
    out = []
    for k in range(5):
        v = m.peek(ITEM_PROPS + 10 * item + 2 * k) & 0xFFFF
        out.append(v - 0x10000 if v & 0x8000 else v)
    return out


def setup(item=None, hero_px=(100, 100), direction=24, targets=(), cells=None, seed=0x12345678, types=PROJECTILE_TYPES, keep=REAL, target_kind=None):
    m = Machine()
    W.build_world(m, cells=cells)
    for i in range(64):                      # 0x8003B24C (world init): slot+0 = index for all 64 slots; build_world zeroed them
        m.poke(ENT0 + STRIDE * i, i)
    W.init_bss(m)
    W.load_balance(m, 0)
    kit = W.init_stats(m)
    if item is not None:
        p = item_props(m, item)
        m.call(0x8004E530, item)
        m.call(0x8004E484, p[0])
    W.place_hero(m, hero_px[0], hero_px[1], direction=direction)
    W.init_pad2(m)
    m.poke(C.RNG, seed)
    m.hooks.pop(0x80039300, None)           # real hit pass
    m.hooks.pop(0x80039F58, None)           # real SpawnWarpEntity
    ginfo = install_global(m, types)
    install_inert_except(m, keep)
    slots = []
    if targets:
        slots, binfo = C.spawn_targets(m, list(targets), target_kind)
    m.uninit_reads.clear()
    m.calls.clear()
    m.journal.clear()
    return m, dict(kit=kit, target_slots=slots, ginfo={t: i['stats'] for t, i in ginfo.items()})


FIELDS = [
    ('status', 0x10, 0), ('hp', 0x14, 1), ('type68', 0x68, 1), ('flags6C', 0x6C, 0),
    ('anim_t', 0x88, 1), ('dir_t', 0x8C, 1), ('anim_c', 0x90, 1), ('dir_c', 0x94, 1), ('adir', 0x98, 1),
    ('b0', 0xB0, 1), ('reset_AC', 0xAC, 1), ('animflags_B4', 0xB4, 0), ('izf_F8', 0xF8, 1),
    ('fx', 0xC4, 1), ('fy', 0xC8, 1), ('fz', 0xB8, 1),
    ('px', 0x114, 1), ('py', 0x118, 1), ('pz', 0x11C, 1),
    ('adj_13C', 0x13C, 1), ('coll_140', 0x140, 1), ('ground_144', 0x144, 1), ('terr_138', 0x138, 1), ('slope_18C', 0x18C, 1),
    ('cnt1c', 0x1C, 1), ('ctl20', 0x20, 1), ('c1cc', 0x1CC, 1), ('c1d0', 0x1D0, 1), ('hitc', 0x220, 1), ('touch', 0x224, 0), ('prog228', 0x228, 1),
    ('box1d4', 0x1D4, 0), ('atk1c8', 0x1C8, 0), ('hbx', 0x1FC, 1), ('hby', 0x200, 1), ('hbz', 0x204, 1), ('bw', 0x214, 1), ('bd', 0x218, 1), ('bh', 0x21C, 1),
    ('loot3c', 0x3C, 1), ('b274', 0x274, 1), ('b278', 0x278, 1), ('parent', 0xC, 0),
]


def snap(m, slot):
    e = ENT0 + STRIDE * slot
    d = {}
    for name, off, sg in FIELDS:
        d[name] = m.peeks(e + off) if sg else m.peek(e + off)
    p = d['atk1c8']
    d['atk_attr'] = (m.peek(p) & 0xFF) if p else None
    d['atk_pow'] = ((m.peek(p) >> 8) & 0xFF) if p else None
    d['touch_slot'] = ((d['touch'] - ENT0) // STRIDE) if d['touch'] else None
    return d


def active_slots(m):
    return [s for s in range(64) if m.peek(ENT0 + STRIDE * s + 0x10) != 0]


def fmt_j(j):
    tick, kind, a = j[0], j[1], j[2]
    if kind == 'effect_3BDD8':
        return 'fx(id=%d,a2=%d,x=%.3f,y=%.3f,z=%.3f)' % (a[1], a[2] & M32, s32(a[3]) / 65536, s32(a[4]) / 65536, s32(a[5]) / 65536)
    if kind == 'effect_3BE74':
        t = a[3]
        return 'fxattach(id=%d,target=%s)' % (a[1], ('slot %d' % ((t - ENT0) // STRIDE)) if ENT0 <= t < ENT0 + 64 * STRIDE else hex(t))
    if kind == 'sound':
        return 'snd(%d)' % (a if not isinstance(a, list) else a[0])
    if kind == 'handler':
        slot, letter, pc = a
        return 'stub%s@%X(slot%s)' % (C.LETTERS[letter] if letter is not None and 0 <= letter < 6 else '?', pc, slot)
    return kind


def run(spec):
    """spec: item, hero_px, direction, targets, cells, n, holds{tick:pad}, after, track (list of slots or None=all active), types, keep"""
    m, meta = setup(item=spec.get('item'), hero_px=spec.get('hero_px', (100, 100)), direction=spec.get('direction', 24), targets=spec.get('targets', ()),
                    cells=spec.get('cells'), seed=spec.get('seed', 0x12345678), types=spec.get('types', PROJECTILE_TYPES), keep=spec.get('keep', REAL),
                    target_kind=spec.get('target_kind'))
    for fn in spec.get('init', []):
        fn(m)
    pad = W.Pad()
    rows = []
    holds = spec.get('holds', {})
    after = spec.get('after', 0)
    rng0 = m.peek(C.RNG)
    for t in range(1, spec['n'] + 1):
        m.tick_no = t
        j0 = len(m.journal)
        hold = holds.get(t, after)
        (spec.get('ticker') or W.tick)(m, pad, hold)
        act = active_slots(m)
        r = dict(tick=t, hold=hold, rng=m.peek(C.RNG), rec_ptr=m.peek(0x80127110), rec_cnt=m.peeks(0x80127114), charge=m.peeks(0x80126FFC),
                 slots={s: snap(m, s) for s in act}, journal=[fmt_j(j) for j in m.journal[j0:] if not j[1].startswith('pass_')])
        rows.append(r)
    return dict(spec=spec, meta=meta, rows=rows, rng0=rng0, m=m)
