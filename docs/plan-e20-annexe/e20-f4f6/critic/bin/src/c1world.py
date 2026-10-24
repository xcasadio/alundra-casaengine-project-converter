"""c1world.py - E20.c1 hit-pass oracle: the D1 MovePlayer oracle + the E20.b attack setup + REAL targets (Beannoids of map 14) + the REAL hit pass.

Real code of ALUN_CD.EXE (France), run by the emulated machine:  the tick passes of 0x8003B388 (slot pass 0x80038634, events 0x800386D0 with MovePlayer 0x80031B50, counters
0x80038998, lists 0x800384F4, animation 0x80038E18 = UpdateAnimation 0x80038AB4 on every active entity over its own bank, physics 0x80038364, hits 0x80039300),
the map-record spawner 0x8003A1B8 (-> 0x80039BD0, 0x80039B28, 0x80039AD0, InitializeEntity 0x80039D04, 0x8004201C, 0x80044550, 0x80032A40/0x80032968 the spawn loot draw),
ComputeNewHp 0x8003A374 / ResolveBalanceTarget 0x8004464C, UpdateItemEffectState 0x800307E8, the spark routine 0x80039188 and the pseudo-random state 0x80098708.

What is NOT real code (each is a Python stub that journals its call):
  * effects 0x8003BDD8 / 0x8003BE74 (the effect objects: the calls and their arguments are journalled, a zeroed dummy slot is returned so that the spark RNG draws happen),
    sound 0x800490FC, spawn 0x80039F58, the effects pass 0x80038E84;
  * THE TARGET'S OWN PROGRAMS (see INERT below).

INERT target.  The events pass 0x800386D0 chooses a program letter A..F per entity (its first loop, REAL, writes the choice to entity+0x228) and its second loop calls the handler
`table[letter][entity+0x70+4*letter]` (tables at *(0x80098F4C + 4*letter), 256 pointers each) or, when `entity+0x4C+4*letter & 0x7F != 0`, the map bytecode runner 0x8004205C.
The Beannoid's map records have event indices 0, so its handlers are the sprite handlers (header bytes: A=1, C=1, D=1, E=1).  Here EVERY non-null handler pointer of the five tables
and the bytecode runner 0x8004205C are hooked by one Python stub that journals (tick, letter chosen = entity+0x228, handler address, entity slot) and returns without touching the
entity.  So the choice (C every tick, D after a touch, ...) is the binary's, but nothing runs: the target neither moves, nor changes animation, nor reacts to a hit.
"""
import os
import sys
import json

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from machine import Machine, s32, M32
import emu
import world as W
import bank

DATA = r"D:/development/repo/alundra-casaengine-project-converter/data-extracted/data"
MAP14 = DATA + "/map_14.json"
ENT0, STRIDE = W.ENT0, W.STRIDE

# ---- memory map of the synthetic pieces (clear of the binary, of the D1 pieces and of each other) ----------------------------
BEAN_REC = 0x801C0000          # Beannoid sprite record + regions (~4 KB)
SPR_PTRS = 0x801CE000          # per-map sprite pointer table, 257 entries (SpriteTableSize 1028 / 4)
ENT_REC = 0x801CF000           # map entity records (20 bytes each)
MAPBLK = 0x80126E70            # per-map sprite block read by 0x80039B28 / 0x80039BD0: +8 entity records, +0xC sprite pointers, +0x10 sprite count, +0x14 entity count
EQUIP = 0x80146E00             # three equipment sheet pointers, stride 8 (filled by UpdateItemEffectState)
LATCH = 0x80146DF8             # g_balanceAnimIndex
RNG = 0x80098708
HERO_SHEET = 0x80127008
SPRITE_TABLE_INDEX = 79        # Beannoid, map 14; balance index = 79 | 0x100 = 335 (0x8003A304)

HANDLER_TABLES = 0x80098F4C    # 6 pointers (A..F), each to 256 handler pointers
LETTERS = 'ABCDEF'


def ent(i):
    return ENT0 + STRIDE * i


class Map14:
    """what the decoded data say about the Beannoid and about map 14 (read once)"""
    def __init__(self):
        d = json.load(open(MAP14, encoding='utf-8-sig'))
        si = d['SpriteInfo']
        self.info = d['Info']
        self.record = si['SpriteRecords'][SPRITE_TABLE_INDEX]
        self.entities = [e for e in si['Entities']['Entities'] if e is not None and e['SpriteTableIndex'] == SPRITE_TABLE_INDEX]
        self.sprite_table_size = si['Header']['SpriteTableSize'] // 4


_M14 = None


def map14():
    global _M14
    if _M14 is None:
        _M14 = Map14()
    return _M14


def entity_record_bytes(e, xpos, ypos, height=0):
    """the 20-byte map entity record, layout of SiEntityRecord (alundra-datas-analyser .../DatasBin/SiEntityRecord.cs) and of the reads at 0x8003A1F4-0x8003A2E0:
       0..3 XMin YMin XMax YMax, 4 IsEnabled, 5 SpriteDirection, 6 SpriteTableIndex, 7 XPos, 8 YPos, 9 Height, 0xA..0xF event code indices A..F, 0x10 u16 _10, 0x12 u16 Contents"""
    b = bytes([e['XMin'], e['YMin'], e['XMax'], e['YMax'], e['IsEnabled'], e['SpriteDirection'], e['SpriteTableIndex'], xpos, ypos, height,
               e['EventCodesA_LoadIndex'] & 0xFF, e['EventCodesB_MapIndex'] & 0xFF, e['EventCodesC_TickIndex'] & 0xFF, e['EventCodesD_TouchIndex'] & 0xFF,
               e['EventCodesE_DeactivateIndex'] & 0xFF, e['EventCodesF_InteractIndex'] & 0xFF])
    return b + e['_10'].to_bytes(2, 'little') + e['Contents'].to_bytes(2, 'little')


class KindData:
    """a REAL sprite record of another map used as the target (spec['kind']): the decoded record, entity 0 of that sprite in that map, its sprite table size.  The world stays map 14's
       (flat field, map constants asserted equal); only the target's sprite record and its balance index (table index | 0x100, the entity byte 5 has bit 0x80) differ."""
    def __init__(self, name):
        k = KINDS[name]
        d = json.load(open(DATA + '/' + k['map'] + '.json', encoding='utf-8-sig'))
        si = d['SpriteInfo']
        self.index = k['index']
        self.label = k['label']
        self.map = k['map']
        self.record = si['SpriteRecords'][self.index]
        self.entity = [e for e in si['Entities']['Entities'] if e is not None and e['SpriteTableIndex'] == self.index and e['SpriteDirection'] & 0x80][0]
        self.sprite_table_size = si['Header']['SpriteTableSize'] // 4
        self.balance_index = self.index | 0x100


KINDS = {
    # balance record 382: MaxHp 1, DamageResponses [128, 16, 128, ...]: nibble 1 -> 0x80 = immune (r & 0xC0 == 0x80); a real placed sprite (15 entities in map 69, 20 in map 80)
    'map69-sprite126': dict(map='map_69', index=126, label='real sprite 126 of map 69, balance record index 382'),
}
_KD = {}


def kind_data(name):
    if name not in _KD:
        _KD[name] = KindData(name)
    return _KD[name]


def target_px(xpos, ypos):
    """position the spawner gives (0x8003A2B0-0x8003A2E4): X = 12 * XPos + 12 px, Y = 8 * YPos + 8 px"""
    return 12 * xpos + 12, 8 * ypos + 8


# ---- handler stubs -----------------------------------------------------------------------------------------------------------
def install_inert(m, on_d=None):
    """hook every handler of the five tables and the bytecode runner with the journalling no-op; on_d(machine, slot) is called first when the D program (letter 3) is chosen"""
    addrs = {}
    for k in range(6):
        p = m.peek(HANDLER_TABLES + 4 * k)
        if p < 0x80000000:
            continue
        for i in range(256):
            h = m.peek(p + 4 * i)
            if 0x80020000 <= h < 0x800CA800:
                addrs.setdefault(h, []).append((LETTERS[k], i))

    def make(addr):
        def stub(m2):
            a0 = m2.r[4]
            slot = (a0 - ENT0) // STRIDE if ENT0 <= a0 < ENT0 + 64 * STRIDE else None
            letter = m2.peek(a0 + 0x228)
            if letter == 3 and on_d is not None and slot is not None:
                on_d(m2, slot)
            m2.journal.append((m2.tick_no, 'handler', [slot, letter, addr]))
            m2.r[2] = 0
        return stub
    for a in sorted(addrs):
        m.hooks[a] = make(a)
    m.hooks[0x8004205C] = make(0x8004205C)
    return len(addrs)


# ---- setup -----------------------------------------------------------------------------------------------------------------
def spawn_targets(m, specs, kind=None):
    """specs: list of (xpos, ypos, height).  Builds the per-map block, the Beannoid record and the entity records; runs the REAL spawner 0x8003A1B8(0, index, 0) for each;
       sets status 2 (as place_hero does for the hero; the real first events pass would run program A and do it).  Returns the list of slots.
       kind: None = the Beannoid of map 14; else a name of KINDS (a real sprite record of another map, same layout, same spawner)."""
    M = map14()
    K = kind_data(kind) if kind else None
    rec = K.record if K else M.record
    tidx = K.index if K else SPRITE_TABLE_INDEX
    ent0 = K.entity if K else M.entities[0]
    tsize = K.sprite_table_size if K else M.sprite_table_size
    info = bank.install(m, BEAN_REC, BEAN_REC, rec)
    for i in range(tsize):
        m.poke(SPR_PTRS + 4 * i, 0)
    m.poke(SPR_PTRS + 4 * tidx, BEAN_REC)
    recs = b''.join(entity_record_bytes(ent0, xp, yp, h) for xp, yp, h in specs)
    m.poke_bytes(ENT_REC, recs)
    m.poke(MAPBLK + 8, ENT_REC)
    m.poke(MAPBLK + 0xC, SPR_PTRS)
    m.poke(MAPBLK + 0x10, tsize)
    m.poke(MAPBLK + 0x14, len(specs))
    slots = []
    for i in range(len(specs)):
        p = m.call(0x8003A1B8, 0, i, 0, limit=5_000_000) & M32
        if p == 0:
            raise RuntimeError('the real spawner refused target %d' % i)
        slots.append((p - ENT0) // STRIDE)
        m.poke(p + W.F['status'], 2)
    return slots, info


def setup(hero_px=(100, 100), targets=((20, 11, 0),), direction=24, seed=0x12345678, mutate=None, hero_anim=0, inert=True, experiments=None, kind=None):
    """fresh machine: flat field, BSS words, balance blob at level D of map 14, new-game kit + WeaponId 1 (real code), the hero from the real InitializeEntity, the targets from the
       real spawner, the pad, the RNG word.  targets = (xpos, ypos, height) of map entity records copied from entity 0 of map 14 (only the three fields change)."""
    M = map14()
    m = Machine()
    W.build_world(m)
    # map 14 header constants = the D1 world's (map 10) constants: checked here, not assumed
    inf = M.info
    assert (inf['Gravity'], inf['ZViscosity'], inf['SlideEffectId'], inf['BalanceLevel'], inf['C'], inf['D']) == (128, 4096, 0, 2, 7, 0), inf
    W.init_bss(m)
    W.load_balance(m, inf['D'])                                 # level D of map 14 = 0 (0x8002C39C: lbu 0xB(header))
    kit = W.init_stats(m)
    hinfo = W.place_hero(m, hero_px[0], hero_px[1], direction=direction, anim=hero_anim)
    W.init_pad2(m)
    m.poke(RNG, seed)
    m.hooks.pop(0x80039300, None)                               # the hit pass runs for real (D1 stubbed it)
    tslots = set()

    def on_d(m2, slot):
        if experiments is not None and slot in tslots:
            experiments.append(dict(tick=m2.tick_no, slot=slot, **damage_experiments(m2, slot)))
    nh = install_inert(m, on_d) if inert else 0
    slots, binfo = spawn_targets(m, list(targets), kind) if targets else ([], None)
    tslots.update(slots)
    if mutate:
        mutate(m)
    m.uninit_reads.clear()
    m.calls.clear()
    m.journal.clear()
    meta = dict(kit=kit, seed=seed, hero_start_px=list(hero_px), target_slots=slots, handler_hooks=nh, map14_D=inf['D'],
                targets=[dict(xpos=t[0], ypos=t[1], height=t[2], px=list(target_px(t[0], t[1]))) for t in targets],
                bean_bank=(binfo or {}).get('stats'), hero_bank=hinfo['stats'])
    if kind:
        meta['target_kind'] = kind_data(kind).label
    return m, meta


# ---- reading entities --------------------------------------------------------------------------------------------------------
E_FIELDS = [      # (name, offset, signed)
    ('status', 0x10, 0), ('hp', 0x14, 1), ('anim_target', 0x88, 1), ('dir_target', 0x8C, 1), ('anim_cur', 0x90, 1), ('dir_cur', 0x94, 1),
    ('anim_flags_B4', 0xB4, 0), ('force_x', 0xC4, 1), ('force_y', 0xC8, 1), ('force_z', 0xB8, 1),
    ('pos_x', 0x114, 1), ('pos_y', 0x118, 1), ('pos_z', 0x11C, 1), ('on_ground', 0x144, 1),
    ('flags_6C', 0x6C, 0), ('frame_counter_1C', 0x1C, 1), ('ctl_20', 0x20, 1),
    ('frame_box_1D4', 0x1D4, 0), ('cur_attack_1C8', 0x1C8, 0),
    ('c1cc', 0x1CC, 1), ('c1d0', 0x1D0, 1), ('hit_counter', 0x220, 1), ('touching', 0x224, 0), ('prog_228', 0x228, 1),
    ('modded_x', 0x1D8, 1), ('modded_y', 0x1DC, 1), ('modded_z', 0x1E0, 1), ('tsize_x', 0x1F0, 1), ('tsize_y', 0x1F4, 1), ('tsize_z', 0x1F8, 1),
    ('hitbox_x', 0x1FC, 1), ('hitbox_y', 0x200, 1), ('hitbox_z', 0x204, 1),
    ('boff_x', 0x208, 1), ('boff_y', 0x20C, 1), ('boff_z', 0x210, 1), ('bsize_x', 0x214, 1), ('bsize_y', 0x218, 1), ('bsize_z', 0x21C, 1),
]


def snap_entity(m, slot):
    e = ent(slot)
    d = {}
    for name, off, sg in E_FIELDS:
        d[name] = m.peeks(e + off) if sg else m.peek(e + off)
    d['pos_z_dll'] = d['pos_z'] - 1
    p = d['cur_attack_1C8']
    d['atk_attr'] = (m.peek(p) & 0xFF) if p else None
    d['atk_power'] = ((m.peek(p) >> 8) & 0xFF) if p else None
    d['touching_slot'] = ((d['touching'] - ENT0) // STRIDE) if d['touching'] else None
    return d


def fmt_effect(e):
    tick, kind, a = e[0], e[1], e[2]
    if kind == 'effect_3BDD8':     # 0x8003BDD8(0, id, 0, cx, cy, cz)
        return 'effect(id=%d, cx=%s, cy=%s, cz=%s px)' % (a[1], px(s32(a[3])), px(s32(a[4])), px(s32(a[5])))
    if kind == 'effect_3BE74':     # 0x8003BE74(0, id, 0, target)
        t = a[3]
        return 'effect_on_target(id=%d, target=%s)' % (a[1], ('slot %d' % ((t - ENT0) // STRIDE)) if ENT0 <= t < ENT0 + 64 * STRIDE else hex(t))
    if kind == 'sound':
        return 'sound(%d)' % a if not isinstance(a, list) else 'sound(%d)' % a[0]
    if kind == 'spawn_39F58':
        return 'spawn(table=%d,type=%d)' % (a[1], a[2])
    if kind == 'handler':
        slot, letter, pc = a
        return 'handler %s of slot %s at 0x%08X (stub)' % (LETTERS[letter] if letter is not None and 0 <= letter < 6 else '?', slot, pc)
    return kind


def px(v):
    """a 16.16 value as pixels, exact decimal (v / 65536)"""
    if v % 65536 == 0:
        return str(v // 65536)
    return '%.5f' % (v / 65536.0)


def equip_state(m):
    """the three equipment sheet pointers 0x80146E00 + 8k and, for each non-null one, what the formula reads (level, MaxHp, AttackCount, Attacks[]) - read from memory"""
    out = []
    for k in range(3):
        p = m.peek(EQUIP + 8 * k)
        if not p:
            out.append(dict(k=k, ptr=0))
            continue
        cnt = m.peek(p + 0xE) & 0xFF
        atk = [(m.peek(p + 0xF + 2 * i) & 0xFF, (m.peek(p + 0xF + 2 * i) >> 8) & 0xFF) for i in range(cnt)]
        out.append(dict(k=k, ptr=p, ptr_off=p - 0x80143DF8, level=m.peek(p) & 0xFF, max_hp=m.peek(p + 2) & 0xFF, attack_count=cnt, attacks=atk))
    return out


# ---- side calls (inside a hook: below the live stack) ---------------------------------------------------------------------------
def side_call(raw, addr, args, sp, limit=5_000_000):
    """the Emu.call loop with an explicit stack pointer, so that a call made from inside a hook does not overwrite the frames of the pass that is running"""
    saved_r, saved_hi, saved_lo, saved_pc = list(raw.r), raw.hi, raw.lo, raw.pc
    for i, a in enumerate(args):
        raw.r[4 + i] = a & M32
    raw.r[29] = sp
    raw.r[31] = emu.SENTINEL
    raw.pc = addr
    n = 0
    while raw.pc != emu.SENTINEL:
        if raw.pc in raw.hooks:
            raw.hooks[raw.pc](raw)
            raw.pc = raw.r[31]
            continue
        raw.step()
        n += 1
        if n > limit:
            raise RuntimeError('step limit in side call at %08x' % raw.pc)
    v0 = raw.r[2]
    raw.r, raw.hi, raw.lo, raw.pc = saved_r, saved_hi, saved_lo, saved_pc
    return v0 & M32


class Sandbox:
    """snapshot of the whole machine (RAM, initialised-byte shadow, journals, uninit-read dict, call counts); restore() puts everything back"""
    def __init__(self, raw):
        self.raw = raw
        self.ram = bytes(raw.ram)
        self.init = bytes(raw.init)
        self.nj = len(raw.journal)
        self.uninit = dict(raw.uninit_reads)
        self.calls = dict(raw.calls)

    def restore(self):
        r = self.raw
        r.ram[:] = self.ram
        r.init[:] = self.init
        del r.journal[self.nj:]
        r.uninit_reads.clear()
        r.uninit_reads.update(self.uninit)
        r.calls.clear()
        r.calls.update(self.calls)


def damage_experiments(m, target_slot, attacker_slot=0):
    """ComputeNewHp(target) called from the harness, on copies of the live state, at the point where it is called (inside the target's D handler stub).
       Three variants: 'live' (the memory as it is: the equipment records MovePlayer's UpdateItemEffectState filled this very tick), 'empty' (the three equipment pointers zeroed),
       'filled' (the real UpdateItemEffectState 0x800307E8 run on the copy first).  Each restores the machine."""
    raw = getattr(m, '_m', m)
    sp = ((m.r[29] - 0x800) & ~7)
    e = ent(target_slot)
    res = {}
    base_hp = raw.peeks(e + 0x14)
    atk_ptr = raw.peek(raw.peek(e + 0x224) + 0x1C8) if raw.peek(e + 0x224) else 0
    ctx = dict(target_hp_before=base_hp, touching=raw.peek(e + 0x224), attack_ptr=atk_ptr,
               attack_attr=(raw.peek(atk_ptr) & 0xFF) if atk_ptr else None, attack_power=((raw.peek(atk_ptr) >> 8) & 0xFF) if atk_ptr else None,
               latched_anim=raw.peeks(LATCH), equipment_live=equip_state(raw))
    sheet = raw.peek(e + 0x1C4)
    attr = ctx['attack_attr'] or 0
    ctx['response'] = raw.peek(sheet + (attr & 0xF) + 2) & 0xFF if sheet else None
    for variant in ('live', 'empty', 'filled'):
        sb = Sandbox(raw)
        if variant == 'empty':
            for k in range(3):
                raw.poke(EQUIP + 8 * k, 0)
        elif variant == 'filled':
            side_call(raw, 0x800307E8, (), sp)
        eq = equip_state(raw)
        ret = side_call(raw, 0x8003A374, (e,), sp)
        hp_after = raw.peeks(e + 0x14)
        res[variant] = dict(returns=ret, hp_before=base_hp, hp_after=hp_after, damage=base_hp - hp_after, equipment=eq,
                            unset_reads=sorted((hex(pc), hex(a), n, c) for (pc, a, n), c in raw.uninit_reads.items() if (pc, a, n) not in sb.uninit))
        sb.restore()
    return dict(context=ctx, variants=res)


# ---- the tick loop ----------------------------------------------------------------------------------------------------------
def trim_args(kind, a):
    """the stubs record 4 register arguments + 6 stack words; only the real arguments are kept (the rest is stale stack): 0x8003BDD8(0, id, 0, cx, cy, cz) = 6, 0x8003BE74(0, id, 0, target) = 4,
       0x80039F58 = 8"""
    if kind == 'effect_3BDD8':
        return list(a[:6])
    if kind == 'effect_3BE74':
        return list(a[:4])
    if kind == 'spawn_39F58':
        return list(a[:8])
    return a


def run(spec, ticker=None, capture=False):
    """spec: dict(name, desc, hero_px, targets=[(xpos,ypos,height)], n, holds={tick: pad}, after=pad word after the last listed tick, hold_all=pad word on every tick not listed,
       before={tick: fn(m)}, experiments=True|False, setup_kw).  Returns dict(meta, pre, rows (per tick: dict of entity snaps by slot + hero), journal, experiments, machine)"""
    kw = dict(hero_px=spec.get('hero_px', (100, 100)), targets=spec.get('targets', ((20, 11, 0),)), direction=spec.get('direction', 24), mutate=spec.get('mutate'),
              seed=spec.get('seed', 0x12345678), kind=spec.get('kind'))
    experiments = [] if spec.get('experiments', True) else None
    m, meta = setup(experiments=experiments, **kw)
    slots = meta['target_slots']
    for sl, fn in (spec.get('target_init') or {}).items():
        fn(m, slots[sl])
    pad = W.Pad()
    rows = []
    tick_fn = ticker or W.tick

    def snap(tick, hold):
        d = {'tick': tick, 'hold': hold, 'rng': m.peek(RNG), 'latch_80146DF8': m.peeks(LATCH), 'hero': snap_entity(m, 0), 'targets': [snap_entity(m, s) for s in slots]}
        return d
    pre = snap(0, 0)
    holds = spec.get('holds', {})
    after = spec.get('after', 0)
    before = spec.get('before', {})
    jlog = []
    slotbytes = []
    for t in range(1, spec['n'] + 1):
        if t in before:
            before[t](m, slots)
        m.tick_no = t
        j0 = len(m.journal)
        hold = holds.get(t, after)
        tick_fn(m, pad, hold)
        r = snap(t, hold)
        r['journal'] = [fmt_effect(j) for j in m.journal[j0:] if not j[1].startswith('pass_')]
        r['journal_raw'] = [(j[1], trim_args(j[1], j[2])) for j in m.journal[j0:] if not j[1].startswith('pass_')]
        rows.append(r)
        if capture:
            slotbytes.append(bytes(m.ram[ent(0) & 0x1FFFFFFF: (ent(0) & 0x1FFFFFFF) + STRIDE * (1 + len(slots))]))
    meta['end_hero_sheet_0x80127008_responses'] = [m.peek(HERO_SHEET + 2 + k) & 0xFF for k in range(1, 12)]   # bytes [3..13]: response for attack nibbles 1..11 (byte [nibble + 2]) of the hero as target
    meta['end_equipment'] = equip_state(m)
    return dict(spec=spec, meta=meta, pre=pre, rows=rows, experiments=experiments or [], machine=m, slotbytes=slotbytes)
