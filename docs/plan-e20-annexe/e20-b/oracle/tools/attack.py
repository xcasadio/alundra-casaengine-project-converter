"""attack.py - E20.b: the attack scenarios on the D1 MovePlayer oracle (real code of ALUN_CD.EXE, France).

Everything is computed by the emulated binary.  This file holds the SETUP (field, weapon, warm-up) and the INPUTS (the pad word of each tick); it holds no
expected value of any output: not an animation id, not a tick, not a force.  The animation a press chooses comes from the binary's own weapon table
(0x80028C78), the sound from the animation set, the end of a swing from the animation bytes.

A trace row is the state AFTER the tick (all passes of 0x8003B388 run once), in the binary convention (PosZ raw: rest = terrain + 1, so the rest value on a
flat field of height 0 is 1; the DLL convention is raw - 1, given in the column pos_z_dll).  PosX/PosY are absolute 16.16 values.
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from machine import Machine, s32
import world as W
import interp2

START_X, START_Y = 100, 100
SQUARE, CROSS, CIRCLE, TRIANGLE = W.SQUARE, W.CROSS, W.CIRCLE, W.TRIANGLE
UP, RIGHT, DOWN, LEFT = W.UP, W.RIGHT, W.DOWN, W.LEFT

# ---- addresses --------------------------------------------------------------------------------------------------------------------------
ITEM_PROPS = 0x800B9FE8        # 10 bytes per item: s16 slot, u16 flags, s16 rank, s16 max count, s16 ... (read by 0x8004E18C GetItemIdFromSlotId and 0x8004E530 AddItem)
WEAPON_MAP = 0x80028C14        # byte per item -> weapon record index (read by MovePlayer 0x80031B70-0x80031B8C)
WEAPON_TAB = 0x80028C78        # 13 bytes per record: chargeable, ground[anim,type,delay,z], air[...], charge[...]
G = dict(charge_timer=0x80126FFC, rec_ptr=0x80127110, rec_cnt=0x80127114, weapon_rec=0x8012710C, sprint_cnt=0x80126FF4)
F = W.F
ROW_FIELDS = [      # (name, entity offset, signed)
    ('anim_target', F['anim_target'], 1), ('anim_cur', F['anim_cur'], 1), ('dir_target', F['dir_target'], 1), ('dir_cur', F['dir_cur'], 1), ('anim_dir', F['animdir'], 1),
    ('force_x', F['forcex'], 1), ('force_y', F['forcey'], 1), ('force_z', F['forcez'], 1),
    ('pos_x', F['posx'], 1), ('pos_y', F['posy'], 1), ('pos_z', F['posz'], 1),
    ('on_ground', F['ground'], 1), ('b0', F['animcounter'], 1), ('cnt1c', F['cnt1c'], 1),
    ('vram_or_180', F['vramor'], 0), ('frame_box_1d4', 0x1D4, 0), ('cur_attack_1c8', 0x1C8, 0), ('hit_counter_220', F['hitcounter'], 1), ('terrain', F['terrain'], 1),
    ('box_off_x', 0x208, 1), ('box_off_y', 0x20C, 1), ('box_off_z', 0x210, 1), ('box_size_x', 0x214, 1), ('box_size_y', 0x218, 1), ('box_size_z', 0x21C, 1),
    ('hitbox_x', 0x1FC, 1), ('hitbox_y', 0x200, 1), ('hitbox_z', 0x204, 1),
]
COLS = ['tick', 'hold', 'anim_target', 'anim_cur', 'dir_target', 'dir_cur', 'force_x', 'force_y', 'force_z', 'pos_x', 'pos_y', 'pos_z', 'pos_z_dll', 'on_ground', 'b0',
        'charge_timer', 'rec_ptr', 'rec_cnt', 'weapon_rec', 'events']


def _props(m, a):
    out = []
    for k in range(5):
        v = (m.peek(a + 2 * k) & 0xFFFF)
        out.append(v - 0x10000 if v & 0x8000 else v)
    return out


def setup(item=None, cells=None, direction=24, start=(START_X, START_Y), mutate=None, no_weapon=False, seed=0x12345678):
    """a fresh machine = oracle.setup() (world, BSS words, balance, the new-game kit and WeaponId 1, the hero from the real InitializeEntity, the RNG word), with
    - cells: the field (callable (col,row) -> (height_byte, slope_byte, walk, ground_property)), default flat
    - item: an item id to own on top of the kit; WeaponId is then set to the slot of that item, by the real 0x8004E530 AddItem and 0x8004E484 SetPlayerWeaponId
      (the slot is read from the item property table of the binary, 0x800B9FE8 + 10 * item, first half-word).  No test of the oracle depends on the slot value."""
    m = Machine()
    W.build_world(m, cells=cells)
    W.init_bss(m)
    W.load_balance(m, 0)
    kit = W.init_stats(m)                                       # real: InitPlayerStats, AddItem of the new-game items, SetPlayerWeaponId(1)
    info = dict(kit=kit)
    if item is not None:
        p = _props(m, ITEM_PROPS + 10 * item)
        info['item'] = item
        info['item_props'] = p
        info['slot'] = p[0]
        m.call(0x8004E530, item)                                # AddItem(item)
        m.call(0x8004E484, p[0])                                # SetPlayerWeaponId(slot of the item)
    if no_weapon:
        m.call(0x8004E484, 0xFFFFFFFF)                          # SetPlayerWeaponId(-1): no weapon
    info['weapon_slot'] = (m.peek(m.peek(0x80176318) + 0xA) & 0xFFFF)
    info['weapon_item_now'] = s32(m.call(0x8004E030))           # GetItemIdFromCurrentWeapon (real)
    wi = info['weapon_item_now']
    info['weapon_rec_index'] = m.peek(WEAPON_MAP + wi) & 0xFF if wi >= 0 else (m.peek(0x80028C13) & 0xFF)
    bank = W.place_hero(m, start[0], start[1], direction=direction)
    W.init_pad2(m)
    m.poke(0x80098708, seed)
    if mutate:
        mutate(m)
    m.uninit_reads.clear()
    m.calls.clear()
    m.journal.clear()
    info['start_px'] = list(start)
    info['bank'] = bank['stats']
    info['seed'] = seed
    return m, info


def fmt_ev(e):
    tick, kind = e[0], e[1]
    if kind == 'sound':
        return 'sound(%d)' % e[2]
    if kind == 'spawn_39F58':
        a = e[2]
        return 'spawn(owner=%08X,table=%d,type=%d,x=%d,y=%d,z=%d,dir=0x%X)' % (a[0], a[1], a[2], s32(a[3]), s32(a[4]), s32(a[5]), a[6] & 0xFFFFFFFF)
    if kind.startswith('effect'):
        a = e[2]
        return '%s(a0=%d,id=%d,a2=%d,a3=0x%X,stack=%s)' % (kind, a[0], a[1], a[2], a[3], ','.join('%X' % x for x in a[4:]))
    return kind


def snap(m, tick, hold, pad, events):
    P = W.ent(0)
    d = {'tick': tick, 'hold': hold, 'pad_pressed': pad.pressed, 'pad_released': pad.released}
    for name, off, sg in ROW_FIELDS:
        d[name] = m.peeks(P + off) if sg else m.peek(P + off)
    d['pos_z_dll'] = d['pos_z'] - 1
    d['charge_timer'] = m.peeks(G['charge_timer'])
    d['rec_ptr'] = m.peek(G['rec_ptr'])
    d['rec_cnt'] = m.peeks(G['rec_cnt'])
    d['weapon_rec'] = m.peeks(G['weapon_rec'])
    d['events'] = events
    return d


def step(m, pad, hold, rows, tick, ticker=None, skip=()):
    m.tick_no = tick
    j0 = len(m.journal)
    (ticker or W.tick)(m, pad, hold, skip=skip)
    ev = [fmt_ev(e) for e in m.journal[j0:] if not e[1].startswith('pass_')]
    rows.append(snap(m, tick, hold, pad, ev))


def warm_east(m, pad, ticker=None, max_warm=60):
    """as S-B: Right held until two consecutive ticks in Moving (current animation 1) with ForceX 159744, the steady walk of the binary (read from the run, 208 * 768)."""
    warm = []
    streak = 0
    for t in range(-max_warm, 0):
        step(m, pad, RIGHT, warm, t, ticker)
        r = warm[-1]
        if r['anim_cur'] == 1 and r['force_x'] == 159744:
            streak += 1
        else:
            streak = 0
        if streak == 2:
            break
    else:
        raise RuntimeError('warm-up did not reach two Moving ticks at ForceX 159744')
    for i, r in enumerate(warm):
        r['tick'] = i - len(warm)
    return warm


COV = set()           # every PC executed by any run (for the load-delay check)
GLOBAL_RANGES = [(0x80126FF0, 0x80127004), (0x80127008, 0x80127118), (0x801EB82C, 0x801EB82C + 0x240)]    # charge/sprint counters, hero sheet, weapon words, stats block


def slot_bytes(m):
    P = W.ent(0) & 0x1FFFFFFF
    return bytes(m.ram[P:P + W.STRIDE])


def run(spec, ticker=None, capture=False, **setup_kw):
    """spec: dict(name, holds={tick: pad word}, n, after=pad word after the last listed tick (default 0), warm='east'|None, item, cells, start, mutate, skip,
    before={tick: fn(machine)} (the harness changes the world just before that tick: used only where stated), no_weapon).
    capture=True also keeps the hero's whole 0x294-byte slot after every tick (for the two-interpreter comparison).
    Returns dict(spec, meta, pre, rows, warm, machine, slots)."""
    kw = dict(item=spec.get('item'), cells=spec.get('cells'), start=spec.get('start', (START_X, START_Y)), mutate=spec.get('mutate'),
              no_weapon=spec.get('no_weapon', False))
    kw.update(setup_kw)
    m, info = setup(**kw)
    pad = W.Pad()
    warm = []
    if spec.get('warm') == 'east':
        warm = warm_east(m, pad, ticker)
    hold_after = spec.get('after', 0)
    pre = snap(m, 0, spec.get('warm_hold', RIGHT if warm else 0), pad, [])
    rows = []
    slots = []
    holds = spec['holds']
    skip = spec.get('skip', ())
    before = spec.get('before', {})
    for t in range(1, spec['n'] + 1):
        if t in before:
            before[t](m)
        step(m, pad, holds.get(t, hold_after), rows, t, ticker, skip)
        if capture:
            slots.append(slot_bytes(m))
    COV.update(m.cov)
    gl = b''.join(bytes(m.ram[a & 0x1FFFFFFF:b & 0x1FFFFFFF]) for a, b in GLOBAL_RANGES)
    return dict(spec=spec, meta=info, pre=pre, rows=rows, warm=warm, machine=m, slots=slots, globals=gl)


# ---- reading a run -------------------------------------------------------------------------------------------------------------------------
def changes(rows, key='anim_cur', first=None):
    """list of (tick, old, new) at which `key` changes (the first row compares with `first`)"""
    out = []
    prev = first
    for r in rows:
        if prev is not None and r[key] != prev:
            out.append((r['tick'], prev, r[key]))
        prev = r[key]
    return out


def first_tick(rows, pred):
    for r in rows:
        if pred(r):
            return r['tick']
    return None


def hx(v):
    return '0x%X' % v
