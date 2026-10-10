"""hworld.py - E20.c2-0: the hurt-hero world.  New file; no copied tool (oracle/tools/) is edited: everything below is added by pokes after the c1/c1b setup,
by entries added to dictionaries of the copied modules at import, or by this module's own tick dispatcher.

What it adds to the c1b world (c1world + kworld) - see c2-oracle.md section 2:
  * the hero's HP taken from the stats exactly as the binary does at spawn (0x800319F8-0x80031A08): the real SetPlayerHpMax 0x8004DC68 and SetPlayerHp 0x8004DD30 on
    the stats, then the real getters 0x8004DC50 / 0x8004DD18, stored into the entity (+0x18 / +0x14);
  * the hero sheet 0x80127008 and the boots level [0x80127000] warmed up by ONE real call of UpdateItemEffectState 0x800307E8 (pad hold word zeroed first, K1 does the same;
    the regeneration counters that call advances are zeroed again so that tick 1 starts from the BSS state);
  * BSS words zeroed (regeneration counters 0x80126FE8/EC/F0, item words 0x8012715C, 0x80127164);
  * a cell painter (hazard cost words 0x80 / 0x100 / 0x180, deep water 0x800), whole field;
  * two more real targets: the Orc of map 15 (sprite 85, balance 341) and the Fish of map 14 (sprite 136, balance 392), as c1world.KINDS entries;
  * synthetic attacks (2-byte attribute/power pairs) for the Beannoid;
  * inventories (armours 0x13 / 0x14, boots 0x19-0x1C and combinations, ring 0x37), built by the real new-game code;
  * the extra per-tick columns (c2-oracle.md section 4) and a TICK DISPATCHER used by both interpreters that follows the binary's gate [0x800DC4B8] & 0x48
    (0x8003B388-0x8003B408): when the gate is set only the lists pass 0x800384F4 runs; otherwise the eight passes of world.PASSES.

Nothing here knows an expected value.  The scenario specs hold inputs only.
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
TOOLS = os.path.join(HERE, 'tools')
sys.path.insert(0, TOOLS)
import c1world as C
import world as W
import kworld as K
import interp2
import scen as S
from machine import M32, s32

ENT0, STRIDE = W.ENT0, W.STRIDE
HERO = ENT0

CTL = 0x800DC4B8               # game-state / control flags
HOLD = 0x80126F28              # pad hold word (PAD + 0x10)
BOOTS = 0x80127000
SHEET = 0x80127008
REGEN = 0x80126FE8             # three counters
ITEM_STATE = 0x80127164
ITEM_TICKS = 0x8012715C
CHARGE = 0x80126FFC
WPTR = 0x80127110
EQ = 0x80146E00
STATS_PTR = 0x80176318
DRAWLIST = 0x80134250
DRAWCOUNT = 0x8013435C
SCRATCH2 = 0x801EA400          # synthetic attack pairs of this module (K.SYN_ATTACKS is 0x801EA000, 14 bytes used)

LISTS_PASS = 0x800384F4
GATE_MASK = 0x48


def s16(v):
    v &= 0xFFFF
    return v - 0x10000 if v & 0x8000 else v


# ---- real sprite kinds added to c1world.KINDS -----------------------------------------------------------------------------------------
C.KINDS['map15-orc'] = dict(map='map_15', index=85, label='real sprite 85 of map 15 (Orc), balance record index 341 (attack 0: attribute 0x02, power 4)')
C.KINDS['map14-fish'] = dict(map='map_14', index=136, label='real sprite 136 of map 14 (Fish), balance record index 392 (attack 0: attribute 0x01, power 1)')


# ---- inventories ---------------------------------------------------------------------------------------------------------------------
def _inv(name, owned, item, desc):
    return dict(name=name, owned=tuple(owned), weapon=1, item=item, desc=desc)


INV = dict(K.INV)
for _i in (
    _inv('ancient', (1, 17, 25, 0x13), None, 'ancient armour 0x13 owned (the highest owned armour wins), kit otherwise'),
    _inv('silver', (1, 17, 25, 0x14), None, 'silver armour 0x14 owned, kit otherwise'),
    _inv('b19', (1, 17, 25, 0x19), None, 'kit + item 0x19 (not a boots level)'),
    _inv('b1a', (1, 17, 25, 0x1A), None, 'kit + boots item 0x1A (level 1)'),
    _inv('b1b', (1, 17, 25, 0x1B), None, 'kit + boots item 0x1B (level 2)'),
    _inv('b1c', (1, 17, 25, 0x1C), None, 'kit + boots item 0x1C (level 3)'),
    _inv('b1a1c', (1, 17, 25, 0x1A, 0x1C), None, 'kit + boots items 0x1A and 0x1C (level 3)'),
    _inv('b1b1a', (1, 17, 25, 0x1B, 0x1A), None, 'kit + boots items 0x1B and 0x1A (level 2)'),
    _inv('ring37', (1, 17, 25, 0x37), 0x37, 'kit + ring 0x37 owned and set as the equipped item (regeneration record, MaxHp field 200)'),
):
    INV[_i['name']] = _i


# ---- the world builders ----------------------------------------------------------------------------------------------------------------
def paint(m, cells):
    """cells: {(col, row): word} or a function (col, row) -> word or None.  The word is the u16 at the cell (walkability | ground_property << 8); slope and height bytes are kept.
       0x80 = hazard cost 1, 0x100 = cost 2, 0x180 = cost 3, 0x800 = deep water (Slope_18c reads (word & 0xE00) >> 9 = 4)"""
    for row in range(60):
        for col in range(52):
            w = cells(col, row) if callable(cells) else cells.get((col, row))
            if w is not None:
                m.poke16(W.TILES + 0x604 + row * W.ROW + col * 8, w)


def fill(word):
    return lambda col, row: word


def syn_attack(m, i, attr, power):
    """the i-th synthetic attack pair of this module (attribute, power) at SCRATCH2 + 2 i; returns its address (to be poked into an entity's +0x1C8)"""
    m.poke_bytes(SCRATCH2 + 2 * i, bytes((attr, power)))
    return SCRATCH2 + 2 * i


def set_attack(attr, power, idx=0):
    """target_init: the target's CurrentAttack := a synthetic pair (stands for what its own program would select)"""
    def f(m, slot):
        m.poke(C.ent(slot) + 0x1C8, syn_attack(m, idx, attr, power))
    return f


def set_anim(anim):
    return S.bean_anim(anim)


def setup_h(spec):
    """fresh machine for a spec: c1b world with the inventory, the hero's life from the stats (real code), warm sheet, BSS, cells.  Always on emu.py."""
    inv = INV[spec.get('inv', 'kit')]
    kw = dict(hero_px=spec['hero_px'], targets=spec['targets'], direction=spec.get('direction', 24), seed=spec.get('seed', 0x12345678), kind=spec.get('kind'))
    m, meta = K.setup_inv(inv, **kw)
    hp, hpmax = spec.get('hp', 10), spec.get('hpmax', 10)
    m.call(0x8004DC68, hpmax & M32)                       # SetPlayerHpMax: >= 0x33 -> 0x32, < 0 -> 0
    m.call(0x8004DD30, hp & M32)                          # SetPlayerHp: > stats.HpMax -> HpMax, < 0 -> 0
    got_max = s16(m.call(0x8004DC50))                     # the two getters, stored as 0x800319F8-0x80031A08 does
    got_hp = s16(m.call(0x8004DD18))
    m.poke(HERO + 0x18, got_max)
    m.poke(HERO + 0x14, got_hp)
    W.write_pad(m, W.Pad())                               # pad hold word 0x80126F28 = 0 (the regeneration gate of 0x800307E8 reads it)
    m.call(0x800307E8)                                    # warm-up: boots level, equipment records, hero sheet
    for a in (REGEN, REGEN + 4, REGEN + 8, ITEM_TICKS, ITEM_STATE):
        m.poke(a, 0)
    m.poke(0x80134360, max([0] + list(meta['target_slots'])))   # last used entity index: the first slot pass 0x80038634 writes it; poked so that a run whose flags stop the slot pass from tick 1 (D1) sees the spawned targets in the lists
    if spec.get('cells') is not None:
        paint(m, spec['cells'])
    if spec.get('setup'):
        spec['setup'](m)
    m.uninit_reads.clear()
    m.journal.clear()
    meta['inventory'] = dict(name=inv['name'], owned=list(inv['owned']), item=inv['item'])
    meta['spawn_from_stats'] = dict(stats_hp_set=hp, stats_hpmax_set=hpmax, entity_hp=got_hp, entity_hpmax=got_max)
    return m, meta


# ---- the tick dispatcher (both interpreters) ----------------------------------------------------------------------------------------
def run_pass(m, engine, addr):
    if engine == 'emu':
        W.run_pass(m, addr)
    elif addr in m.hooks:
        m.hooks[addr](interp2.Shim(m, interp2.Cpu2(m)))
    else:
        interp2.call2(m, addr)


def htick(m, pad, hold, engine='emu', gate=True):
    """one logic tick as 0x8003B388 runs it: the pad words (cross-checked against the binary's own pad routine), then, if [0x800DC4B8] & 0x48 is set, ONLY the lists pass
       0x800384F4; otherwise the eight passes in the order of world.PASSES.  gate=False runs the eight passes whatever the flags (the control of hmut.py: the base world.tick does this)."""
    pad.update(hold)
    W.write_pad(m, pad)
    if engine == 'emu':
        m.call(0x8002E250, W.PAD2, hold)
    else:
        interp2.call2(m, 0x8002E250, W.PAD2, hold)
    got = (m.peek(W.PAD2 + 0x10) & 0xFFFF, m.peek(W.PAD2 + 0x12) & 0xFFFF, m.peek(W.PAD2 + 0x14) & 0xFFFF, m.peek(W.PAD2 + 0x16) & 0xFFFF)
    want = (pad.hold, pad.pressed, pad.released, pad.byint)
    if got != want:
        raise RuntimeError('pad port differs from the binary routine at hold 0x%04X: binary %s port %s' % (hold, got, want))
    passes = (LISTS_PASS,) if (gate and (m.peek(CTL) & GATE_MASK)) else W.PASSES
    for a in passes:
        run_pass(m, engine, a)


# ---- readers -------------------------------------------------------------------------------------------------------------------------------
def draw_list(m):
    n = m.peek(DRAWCOUNT)
    return [m.peek(DRAWLIST + 4 * i) for i in range(n)]


def snap_hero(m):
    d = C.snap_entity(m, 0)
    e = HERO
    d['hpmax_18'] = m.peeks(e + 0x18)
    d['vram_or_180'] = m.peek(e + 0x180)
    d['vram_and_184'] = m.peek(e + 0x184)
    d['slope_18c'] = m.peeks(e + 0x18C)
    d['anim_end_AC'] = m.peeks(e + 0xAC)
    d['carried_2C'] = m.peek(e + 0x2C)
    dl = draw_list(m)
    d['visible'] = 1 if HERO in dl else 0
    d['visible_count'] = len(dl)
    d['blink_expected'] = 1 if (d['c1cc'] & 3) == 3 else 0
    return d


def snap_globals(m):
    sp = m.peek(STATS_PTR)
    g = dict(stats_hp=s16(m.peek(sp)), stats_hpmax=s16(m.peek(sp + 2)), ctl_flags=m.peek(CTL), hold_word=m.peek(HOLD) & 0xFFFF, boots=m.peeks(BOOTS),
             regen0=m.peek(REGEN), regen1=m.peek(REGEN + 4), regen2=m.peek(REGEN + 8), item_state=m.peek(ITEM_STATE), item_ticks=m.peek(ITEM_TICKS),
             charge=m.peek(CHARGE), wptr=m.peek(WPTR), eq0=m.peek(EQ), eq1=m.peek(EQ + 8), eq2=m.peek(EQ + 16))
    for i in range(1, 12):
        g['sheet_r%d' % i] = m.peek(SHEET + 2 + i) & 0xFF
    return g


def sheet_bytes(m):
    return bytes(m.peek(SHEET + i) & 0xFF for i in range(255)).hex()


def fmt_journal(m, j0):
    return [C.fmt_effect(j) for j in m.journal[j0:] if not j[1].startswith('pass_') and j[1] != 'handler']


def apply_event(m, ev):
    """events are DATA (kind, argument) so that the independent model reads the same inputs:
       ('flags', v) [0x800DC4B8] := v; ('stats_hp', v) / ('stats_hpmax', v): the real SetPlayerHp 0x8004DD30 / SetPlayerHpMax 0x8004DC68 on the stats (through the interpreter under test);
       ('vessel',): the real IncreaseHpMaxAndCreateEffect 0x800333AC(hero); ('hero_hp', v): poke hero +0x14; ('teleport_x', px): poke hero PosX := px << 16"""
    kind = ev[0]
    if kind == 'flags':
        m.poke(CTL, ev[1])
    elif kind == 'stats_hp':
        K.call(m, 0x8004DD30, ev[1] & M32)
    elif kind == 'stats_hpmax':
        K.call(m, 0x8004DC68, ev[1] & M32)
    elif kind == 'vessel':
        K.call(m, 0x800333AC, HERO)
    elif kind == 'hero_hp':
        m.poke(HERO + 0x14, ev[1])
    elif kind == 'teleport_x':
        m.poke(HERO + 0x114, ev[1] << 16)
    else:
        raise ValueError(kind)


def run(spec, engine='emu', capture=False):
    """spec: name, desc, hero_px, targets [(xpos, ypos, height)], n, holds {tick: pad}, after (pad word for the other ticks), inv, hp, hpmax, kind, direction, seed,
       cells (painter), setup (fn(m) after the setup), target_init {target index: fn(m, slot)}, events {tick: [(kind, arg)]} applied before that tick (apply_event),
       flags_every_tick (value poked into [0x800DC4B8] before every tick),
       before {tick: fn(m, slots) or list of them} (harness writes / real calls between two ticks), gate (False: ungated dispatcher, the control of hmut.py), mutate."""
    K.use_engine(engine)
    m, meta = setup_h(spec)
    slots = meta['target_slots']
    for sl, fn in (spec.get('target_init') or {}).items():
        fn(m, slots[sl])
    pad = W.Pad()

    def snap(tick, hold):
        return {'tick': tick, 'hold': hold, 'rng': m.peek(C.RNG), 'latch_80146DF8': m.peeks(C.LATCH), 'hero': snap_hero(m),
                'targets': [C.snap_entity(m, s) for s in slots], 'g': snap_globals(m)}
    def before_vals():
        """what the tick starts from, after the events: the stats and the entity's life (so that 'the entity wins' is visible in the rows)"""
        sp = m.peek(STATS_PTR)
        return dict(stats_hp_before=s16(m.peek(sp)), stats_hpmax_before=s16(m.peek(sp + 2)), ent_hp_before=m.peeks(HERO + 0x14), ent_hpmax_before=m.peeks(HERO + 0x18))
    pre = snap(0, 0)
    pre['g'].update(before_vals())
    pre['sheet_full'] = sheet_bytes(m)
    holds = spec.get('holds', {})
    after = spec.get('after', 0)
    before = spec.get('before', {})
    events = spec.get('events', {})
    every = spec.get('flags_every_tick')
    rows, slotbytes = [], []
    gate = spec.get('gate', True)
    for t in range(1, spec['n'] + 1):
        if every is not None:
            m.poke(CTL, every)
        for ev in events.get(t, ()):
            apply_event(m, ev)
        b = before.get(t)
        if b is not None:
            for fn in (b if isinstance(b, (list, tuple)) else [b]):
                fn(m, slots)
        m.tick_no = t
        j0 = len(m.journal)
        hold = holds.get(t, after)
        bv = before_vals()
        htick(m, pad, hold, engine, gate)
        r = snap(t, hold)
        r['g'].update(bv)
        r['journal'] = fmt_journal(m, j0)
        r['journal_raw'] = [(j[1], C.trim_args(j[1], j[2])) for j in m.journal[j0:] if not j[1].startswith('pass_') and j[1] != 'handler']
        rows.append(r)
        if capture:
            slotbytes.append(bytes(m.ram[ENT0 & 0x1FFFFFFF: (ENT0 & 0x1FFFFFFF) + STRIDE * (1 + len(slots))]))
    meta['end_sheet_full'] = sheet_bytes(m)
    meta['end_equipment'] = C.equip_state(m)
    meta['uninit_reads'] = sorted((hex(pc), hex(a), n, c) for (pc, a, n), c in m.uninit_reads.items())
    K.use_engine('emu')
    return dict(spec=spec, meta=meta, pre=pre, rows=rows, machine=m, slotbytes=slotbytes, engine=engine)


def hero_col(res, name, with_pre=False):
    rows = ([res['pre']] if with_pre else []) + res['rows']
    return [r['hero'][name] for r in rows]


def g_col(res, name, with_pre=False):
    rows = ([res['pre']] if with_pre else []) + res['rows']
    return [r['g'][name] for r in rows]


def target_col(res, i, name, with_pre=False):
    rows = ([res['pre']] if with_pre else []) + res['rows']
    return [r['targets'][i][name] for r in rows]
