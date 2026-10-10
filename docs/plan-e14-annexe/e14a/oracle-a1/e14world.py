"""e14world.py - E14.a1 oracle: the six native handlers of the Beannoide (type 335: A1, C1, D1) and the Muruta (type 347: A0, C10, D9), plus E1, run for REAL.

Built from the E20.c2-0 oracle (which is the E20.c1b oracle + a gate-faithful tick dispatcher): tools/ is a byte-identical copy of docs/plan-e20-annexe/e20-c2/oracle/tools (never edited);
hworld.py / hscen.py / hmodel.py are copies of that folder's files, only imported for their dispatcher (hworld.htick).  Everything below is a NEW file.

What is real code of ALUN_CD.EXE (France) here (run by emu.py, cross-checked on mips.py by e14run.py):
  * the eight passes of the entity block 0x8003B388 (slot wipe 0x80038634, events 0x800386D0 with MovePlayer 0x80031B50, counters 0x80038998, lists 0x800384F4, animation 0x80038E18,
    physics 0x80038364, hits 0x80039300), in the order and with the gate [0x800DC4B8] & 0x48 of hworld.htick;
  * the spawner 0x8003A1B8 and InitializeEntity (the spawn loot draw 0x80032A40 / 0x80032968 included), ComputeNewHp, the pad routine, UpdateItemEffectState;
  * THE SEVEN HANDLERS: A1 0x80061758, C1 0x80065ED4, D1 0x8007D9A4 (Beannoide), A0 0x8006174C, C10 0x80068154, D9 0x8007DE04 (Muruta), E1 0x8007ED10 (DestroyEntity(self, -1)),
    and everything they call: the relative-position helper 0x8007FE20, TryAttackPlayer2 0x8008048C, TryAttackPlayerFront 0x800805C8, HandleAnimationDirection 0x80080144,
    StartWalk 0x8007FEF8, GetDirectionToTarget 0x8002E814, DestroyEntity 0x8003A59C, the loot function 0x80032B90, ComputeNewHp 0x8003A374.
Stubs (journalled, as in E20.c1): sound 0x800490FC, effects 0x8003BDD8 / 0x8003BE74 (the break effect of E1 is 0x8003BE74(0, id, 0, entity, 1, ...)), spawn 0x80039F58 (the loot drop),
the effects pass 0x80038E84, the bytecode runner 0x8004205C and every OTHER handler of the five tables (inert, journalled).  Program A runs at status 1 (the spawner leaves status 1).

New in this file:
  * EMachine: a Machine whose exec1 notices the entry and the return of the live handlers and records, per CALL: the entity and the hero before and after (full 0x294 bytes), the RNG word in
    and out, v0, the journalled calls made during it.  This is the "one call at a time, RNG word as input and output" record the plan asks for.
  * real map cells (map_N.json MapTiles Flags = walkability | ground_property << 8 | slope << 16 | height << 24, the layout of the oracle's cells) and the map header constants of the map;
  * the wipe template 0x80134368: the binary's init (0x8003B214-0x8003B21C) writes -1 at +0x48, the rest is BSS (zero); the older oracles never wiped a slot so never needed it;
  * real sprite kinds: the Muruta (sprite 91 of map 115, balance record 347) and the Beannoide (sprite 79 of map 116 = map 14, balance record 335);
  * a map-event stand-in: the REAL search 0x8003C954 and the REAL opcode handler 0x8003DC84 (0x2C [N]) called before the entity block of every tick (UpdateWorld 0x8002E058: map events
    0x8003C67C, then entities 0x8003B388), owner = the hero;
  * optional per-frame RNG advance (0x8002BD14-0x8002BD34, a Python port of three instructions: s = s * 0x7D2B89DD + 0xE06A02E7), OFF by default as in D1.
Nothing here knows an expected value.
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
TOOLS = os.path.join(HERE, 'tools')
sys.path.insert(0, HERE)
sys.path.insert(0, TOOLS)
import c1world as C
import world as W
import kworld as K
import bank
import emu
import interp2
import hworld as H
import lib
from machine import Machine, M32, s32

ENT0, STRIDE = W.ENT0, W.STRIDE
RNG = C.RNG
LATCH = C.LATCH
DATA = C.DATA
TEMPLATE = 0x80134368          # the wipe template (0x80038634 copies it over a status-4 slot)
SLOTCOUNT = 0x80134360         # [0x80134360] = highest non-empty slot

# ---- the live handlers -----------------------------------------------------------------------------------------------------------
LIVE = {
    0x80061758: 'A1',   # Beannoide  TargetAnimationId := 2
    0x80065ED4: 'C1',   # Beannoide  tick
    0x8007D9A4: 'D1',   # Beannoide  touch (hit reaction, anim 5)
    0x8006174C: 'A0',   # Muruta     AIValues[0] := PosZ >> 16
    0x80068154: 'C10',  # Muruta     tick
    0x8007DE04: 'D9',   # Muruta     touch (hit reaction, anim 2)
    0x8007ED10: 'E1',   # DestroyEntity(self, -1) (E0 and E1 share it)
}
ENTRY = {v: k for k, v in LIVE.items()}

# ---- map data --------------------------------------------------------------------------------------------------------------------
_MAPS = {}


def mapjson(n):
    if n not in _MAPS:
        _MAPS[n] = json.load(open(DATA + '/map_%d.json' % n, encoding='utf-8-sig'))
    return _MAPS[n]


def cells_fn(n):
    d = mapjson(n)
    mt = d['Map']['MapTiles']
    assert d['Map']['Width'] == 52 and d['Map']['Height'] == 60 and len(mt) == 3120
    tab = {}
    for e in mt:
        tab[(e['TileX'], e['TileY'])] = (e['Height'] & 0xFF, e['Slope'] & 0xFF, e['Walkability'] & 0xFF, e['GroundProperty'] & 0xFF)

    def f(col, row):
        return tab[(col, row)]
    return f


KINDS = {
    'muruta': dict(map=115, index=91, label='Muruta (claws) Lv.1: sprite 91 of map 115, balance record 347 (91 | 0x100), header [3,1,3,0,10,9,1,0,...]'),
    'beannoide': dict(map=116, index=79, label='Beannoide: sprite 79 of map 116 (identical on the 18 maps), balance record 335 (79 | 0x100)'),
}


def record_template(kind, rec_index):
    """the REAL map entity record `rec_index` of the kind's map (dict from map_N.json)"""
    k = KINDS[kind]
    return mapjson(k['map'])['SpriteInfo']['Entities']['Entities'][rec_index]


def record_bytes(e, xpos, ypos, height, codes=None, sd=None):
    """20-byte map entity record (layout of c1world.entity_record_bytes); codes = (A, B, C, D, E, F) event code indices, default all 0 = native handlers"""
    a, b, c, d, ee, f = codes if codes is not None else (0, 0, 0, 0, 0, 0)
    # the area bytes (XMin..YMax) are the hero-tile window the spawner tests when force == 0 (0x8003A208-0x8003A258); the oracle spawns with force 1 (the 0x2D opcode does), so they are not read;
    # they are written as the whole map (0, 0, 51, 59) like the map-14 record the c1 oracle copied
    bs = bytes([0, 0, 51, 59, e['IsEnabled'], e['SpriteDirection'] if sd is None else sd, e['SpriteTableIndex'], xpos, ypos, height, a, b, c, d, ee, f])
    return bs + e['_10'].to_bytes(2, 'little') + e['Contents'].to_bytes(2, 'little')


DROP = 0x801E4000                # a zeroed dummy entity handed back by the SpawnWarpEntity stub (the loot function 0x80032B90 then initialises it and plays sound 0x54, as for a real drop)
READS = {}      # handler -> {'r': {(class, offset, size): count}, 'w': {...}}: the memory the live handlers (and the helpers they call) read and write, accumulated over every machine of the process
IMG_LO, IMG_HI = 0x80020000, 0x800CA800


# ---- the machine -----------------------------------------------------------------------------------------------------------------
class EMachine(Machine):
    """Machine + a record of every call of a live handler (entry address in LIVE) made by the events pass"""
    def __init__(self):
        super().__init__()
        self.watching = False
        self.stack = []
        self.calls_log = []
        self.letters = []
        self.in_stub = False

    # read / write sets of the live handlers (the memory touched while a handler call is open: the handler and everything it calls)
    def _note(self, kind, a, n):
        st = self.stack
        if not st or self.in_stub:
            return
        a &= M32
        if kind == 'r' and (a == self.cur_pc or a == self.cur_pc + 4 or a == self.pc or a == self.pc + 4):
            return                                       # instruction fetch
        fr = st[-1]
        if ENT0 <= a < ENT0 + 64 * STRIDE:
            sl, off = divmod(a - ENT0, STRIDE)
            key = ('hero' if sl == 0 else ('self' if sl == fr['slot'] else 'slot%d' % sl), off, n)
        elif a >= 0x801FE000:
            return                                       # the stack
        elif DROP <= a < DROP + STRIDE:
            key = ('drop', a - DROP, n)
        elif 0x801E8000 <= a < 0x801E8400:
            return                                       # the effect slot handed back by the effect stubs
        elif IMG_LO <= a < IMG_HI:
            key = ('table', a, n)
        else:
            key = ('global', a, n)
        d = READS.setdefault(fr['handler'], {'r': {}, 'w': {}})[kind]
        d[key] = d.get(key, 0) + 1

    def rb(self, a):
        self._note('r', a, 1)
        return super().rb(a)

    def rh(self, a):
        self._note('r', a, 2)
        return super().rh(a)

    def rw(self, a):
        self._note('r', a, 4)
        return super().rw(a)

    def wb(self, a, v):
        self._note('w', a, 1)
        super().wb(a, v)

    def wh(self, a, v):
        self._note('w', a, 2)
        super().wh(a, v)

    def ww(self, a, v):
        self._note('w', a, 4)
        super().ww(a, v)

    def exec1(self, pc, w):
        if self.watching:
            st = self.stack
            if st and pc == st[-1]['ra']:
                self._leave(st.pop())
            elif pc in LIVE:
                self._enter(pc)
        return super().exec1(pc, w)

    def _atk(self, ent_ptr):
        """(attribute, power) of the entity's current attack (+0x1C8 points at the 2-byte pair), or None"""
        p = self.peek(ent_ptr + 0x1C8)
        if not p:
            return None
        v = self.peek(p)
        return (v & 0xFF, (v >> 8) & 0xFF)

    def _raw(self, a):
        o = a & 0x1FFFFFFF
        return bytes(self.ram[o:o + STRIDE])

    def begin_call(self, pc, a0, ra=0):
        """the record of a handler call about to start (a0 = the entity)"""
        slot = (a0 - ENT0) // STRIDE if ENT0 <= a0 < ENT0 + 64 * STRIDE else None
        att = self.peek(a0 + 0x224)
        sheet = self.peek(a0 + 0x1C4)
        return dict(tick=self.tick_no, handler=LIVE[pc], entry=pc, ra=ra, slot=slot, letter=self.peek(a0 + 0x228) if slot is not None else None,
                    rng_in=self.peek(RNG), latch=s32(self.peek(LATCH)), j0=len(self.journal), ent_before=self._raw(a0), hero_before=self._raw(ENT0), a0=a0,
                    steps0=self.steps, hero_atk=self._atk(ENT0), attacker_atk=self._atk(att) if att else None,
                    sheet=bytes(self.ram[sheet & 0x1FFFFFFF:(sheet & 0x1FFFFFFF) + 20]) if sheet else None, equip=C.equip_state(self))

    def end_call(self, rec):
        rec['ent_after'] = self._raw(rec['a0'])
        rec['hero_after'] = self._raw(ENT0)
        rec['rng_out'] = self.peek(RNG)
        rec['v0'] = s32(self.r[2])
        rec['j1'] = len(self.journal)
        rec['journal'] = [(j[1], C.trim_args(j[1], j[2])) for j in self.journal[rec['j0']:rec['j1']] if not j[1].startswith('pass_') and j[1] != 'handler']
        return rec

    def _enter(self, pc):
        self.stack.append(self.begin_call(pc, self.r[4] & M32, self.r[31] & M32))

    def _leave(self, rec):
        self.calls_log.append(self.end_call(rec))


# ---- field decoding (offsets of the entity record, see the discovery annexes and the binary) -----------------------------------------
def _w(raw, off, signed=False):
    v = int.from_bytes(raw[off:off + 4], 'little')
    return v - (1 << 32) if signed and v & 0x80000000 else v


def _h(raw, off):
    return int.from_bytes(raw[off:off + 2], 'little')


FIELDS = [   # (name, offset, signed)
    ('status', 0x10, 0), ('hp', 0x14, 1), ('ctl20', 0x20, 1), ('ref48', 0x48, 1), ('flags6C', 0x6C, 0),
    ('anim_target88', 0x88, 1), ('dir_target8C', 0x8C, 1), ('anim_cur90', 0x90, 1), ('dir_cur94', 0x94, 1), ('animdir98', 0x98, 1),
    ('anim_end_AC', 0xAC, 1), ('anim_flags_B4', 0xB4, 0),
    ('forcez_B8', 0xB8, 1), ('tforcex_BC', 0xBC, 1), ('tforcey_C0', 0xC0, 1), ('forcex_C4', 0xC4, 1), ('forcey_C8', 0xC8, 1), ('fstepx_D4', 0xD4, 1), ('fstepy_D8', 0xD8, 1),
    ('posx', 0x114, 1), ('posy', 0x118, 1), ('posz', 0x11C, 1), ('tilex_120', 0x120, 1), ('tiley_124', 0x124, 1), ('tilez_128', 0x128, 1),
    ('floor_134', 0x134, 1), ('terrain_138', 0x138, 1), ('forceadj_13C', 0x13C, 1), ('onground_144', 0x144, 1),
    ('mh0_158', 0x158, 1), ('mh1_15C', 0x15C, 1), ('mh2_160', 0x160, 1), ('mh3_164', 0x164, 1),
    ('curattack_1C8', 0x1C8, 0), ('c1cc', 0x1CC, 1), ('c1d0', 0x1D0, 1), ('box_1D4', 0x1D4, 0), ('hitcounter_220', 0x220, 1), ('touching_224', 0x224, 0), ('letter_228', 0x228, 1),
    ('contents_3C', 0x3C, 1), ('platform_28', 0x28, 0), ('activeeffect_1B8', 0x1B8, 0), ('hdr_64', 0x64, 0),
    ('b0_274', 0x274, 'b'), ('b1_275', 0x275, 'b'), ('b2_276', 0x276, 'b'), ('b3_277', 0x277, 'b'), ('b4_278', 0x278, 'b'), ('b5_279', 0x279, 'b'),
    ('ai0_280', 0x280, 'h'), ('ai1_282', 0x282, 'h'), ('w284', 0x284, 1),
]


def decode(raw):
    d = {}
    for name, off, sg in FIELDS:
        if sg == 'b':
            d[name] = raw[off]
        elif sg == 'h':
            d[name] = _h(raw, off)
        else:
            d[name] = _w(raw, off, bool(sg))
    return d


def changed(before, after):
    """word-level differences of two 0x294 byte images: [(offset, before, after)] with the words as unsigned hex ints"""
    out = []
    for o in range(0, STRIDE, 4):
        b = int.from_bytes(before[o:o + 4], 'little')
        a = int.from_bytes(after[o:o + 4], 'little')
        if a != b:
            out.append((o, b, a))
    return out


def lcg(s):
    return (s * 0x7D2B89DD + 0xE06A02E7) & M32


def lcg_inv(s):
    inv = pow(0x7D2B89DD, -1, 1 << 32)
    return ((s - 0xE06A02E7) * inv) & M32


def draw(s, n):
    """the binary's draw: step, then (s * n) >> 32.  returns (new state, result)"""
    s2 = lcg(s)
    return s2, (s2 * n) >> 32


def seed_chain(draws):
    """exact: find the start word such that the successive draws give `draws`.  Each draw k needs the state after it, w_k, with (w_k * n_k) >> 32 == r_k and w_k = lcg(w_{k-1}).
       Chain them by search over the free low bits: pick w_last in its allowed interval, go back through lcg_inv, and check the earlier draws; the intervals are wide for small n, so a
       randomised walk over the last interval finds a solution quickly (deterministic: a fixed LCG-based generator drives the walk)."""
    ns = [d[0] for d in draws]
    rs = [d[1] for d in draws]
    k = len(draws)
    lo = -(-rs[-1] * (1 << 32) // ns[-1])
    hi = -(-(rs[-1] + 1) * (1 << 32) // ns[-1]) - 1
    gen = 12345
    for _ in range(2_000_000):
        gen = lcg(gen)
        w = lo + (gen * (hi - lo + 1) >> 32)
        s = w
        ok = True
        for j in range(k - 1, 0, -1):
            s = lcg_inv(s)                       # state after draw j-1
            if (s * ns[j - 1]) >> 32 != rs[j - 1]:
                ok = False
                break
        if ok:
            return lcg_inv(s)
    raise RuntimeError('no seed found for %r' % (draws,))


# ---- the world --------------------------------------------------------------------------------------------------------------------
def install_hooks(m, live=LIVE):
    """hook every handler of the five tables and the bytecode runner with the journalling no-op, EXCEPT the live handlers (c1world.install_inert, with an exception list)"""
    addrs = {}
    for k in range(6):
        p = m.peek(C.HANDLER_TABLES + 4 * k)
        if p < 0x80000000:
            continue
        for i in range(256):
            h = m.peek(p + 4 * i)
            if 0x80020000 <= h < 0x800CA800:
                addrs.setdefault(h, []).append((C.LETTERS[k], i))

    def make(addr):
        def stub(m2):
            a0 = m2.r[4]
            slot = (a0 - ENT0) // STRIDE if ENT0 <= a0 < ENT0 + 64 * STRIDE else None
            m2.journal.append((m2.tick_no, 'handler', [slot, m2.peek(a0 + 0x228), addr]))
            m2.r[2] = 0
        return stub
    n = 0
    for a in sorted(addrs):
        if a in live:
            continue
        m.hooks[a] = make(a)
        n += 1
    m.hooks[0x8004205C] = make(0x8004205C)
    return n, addrs


PATCHES = []                     # (address, old word, new word) applied to every machine built by setup (empty except in the mutation tests)


def install_drop_stub(m):
    """0x80039F58 SpawnWarpEntity: journalled like machine.py's stub (tick, 'spawn_39F58', a0..a3 + 4 stack words) but it returns DROP, a zeroed dummy entity, instead of 0, so that the code after the call in
       0x80032B90 (ForceZ 0xA0000, Bytes[0] 1, Flags &= ~0x80, lifetime, sound 0x54) runs and is journalled.  Nothing is spawned: the dummy is not in the entity table"""
    def spawn(m2):
        sp = m2.r[29]
        args = [m2.r[4], m2.r[5], m2.r[6], m2.r[7]] + [m2.peek(sp + 0x10 + 4 * i) for i in range(4)]
        m2.journal.append((m2.tick_no, 'spawn_39F58', [x & M32 for x in args]))
        m2.in_stub = True
        m2.zero(DROP, STRIDE)
        m2.in_stub = False
        m2.r[2] = DROP
    m.hooks[0x80039F58] = spawn


def init_template(m):
    """the wipe template: the binary's init (0x8003B214-0x8003B21C) stores -1 at template + 0x48, the rest is BSS (zero)"""
    m.zero(TEMPLATE, STRIDE)
    m.poke(TEMPLATE + 0x48, -1)


SPAWN_LOG = []      # one dict per record spawned by the last setup (cleared by setup): RNG word before / after the real spawner, the rolled ContentsItemId, HP, direction, flags


def spawn(m, kind, recs):
    """recs: list of dicts, one per map entity record INDEX 0..n-1: either None (a dummy enabled record that is never spawned) or dict(x, y, h, codes, sd, spawn=True).
       Builds the per-map block, the sprite record of `kind`, the 20-byte records, runs the REAL spawner 0x8003A1B8(0, i, 1) for every record with spawn=True.  Status stays 1
       (program A runs at the first events pass).  returns the list of (record index, slot)."""
    k = KINDS[kind]
    d = mapjson(k['map'])
    si = d['SpriteInfo']
    rec = si['SpriteRecords'][k['index']]
    tsize = si['Header']['SpriteTableSize'] // 4
    info = bank.install(m, C.BEAN_REC, C.BEAN_REC, rec)
    for i in range(tsize):
        m.poke(C.SPR_PTRS + 4 * i, 0)
    m.poke(C.SPR_PTRS + 4 * k['index'], C.BEAN_REC)
    tmpl = [e for e in si['Entities']['Entities'] if e is not None and e['SpriteTableIndex'] == k['index'] and e['SpriteDirection'] & 0x80][0]
    blobs = []
    for r in recs:
        if r is None:
            blobs.append(record_bytes(tmpl, 0, 0, 0))
        else:
            e = r.get('template') or tmpl
            blobs.append(record_bytes(e, r['x'], r['y'], r['h'], r.get('codes'), r.get('sd')))
    m.poke_bytes(C.ENT_REC, b''.join(blobs))
    m.poke(C.MAPBLK + 8, C.ENT_REC)
    m.poke(C.MAPBLK + 0xC, C.SPR_PTRS)
    m.poke(C.MAPBLK + 0x10, tsize)
    m.poke(C.MAPBLK + 0x14, len(recs))
    out = []
    for i, r in enumerate(recs):
        if r is None or not r.get('spawn', True):
            continue
        rng0 = m.peek(RNG)
        p = m.call(0x8003A1B8, 0, i, 1, limit=5_000_000) & M32     # force = 1: the window test and the 0x40 auto-spawn bit are skipped, as for a record spawned by 0x2D
        if p == 0:
            raise RuntimeError('the real spawner refused record %d' % i)
        out.append((i, (p - ENT0) // STRIDE))
        SPAWN_LOG.append(dict(record=i, slot=(p - ENT0) // STRIDE, rng_before=rng0, rng_after=m.peek(RNG), contents_item=s32(m.peek(p + 0x3C)), hp=s32(m.peek(p + 0x14)), contents_flag=s32(m.peek(p + 0x40)),
                              anim=s32(m.peek(p + 0x88)), direction=s32(m.peek(p + 0x8C)), flags=m.peek(p + 0x6C), pos=[s32(m.peek(p + 0x114 + 4 * k)) for k in range(3)]))
    return out, info


def setup(spec):
    """fresh machine for a spec.  spec keys: map (None = flat field with the map-14 constants, else a map number whose MapTiles are loaded), kind, recs (see spawn), hero_px, direction,
       seed, hp, hpmax, inv.  Returns (machine, meta)."""
    mp = spec.get('map')
    m = EMachine()
    inf = mapjson(mp)['Info'] if mp else mapjson(14)['Info']
    assert (inf['Gravity'], inf['ZViscosity'], inf['SlideEffectId'], inf['BalanceLevel'], inf['C'], inf['D']) == (128, 4096, 0, 2, 7, 0), inf   # = the constants W.build_world pokes
    W.build_world(m, cells=cells_fn(mp) if mp else None)
    W.init_bss(m)
    m.poke(0x800DC5A0, mp if mp else 14)               # the current map id: C10 reads it (0x80068164, the map-0x99 branch); BSS in the older oracles, which never ran C10
    W.load_balance(m, inf['D'])
    inv = H.INV[spec.get('inv', 'kit')]
    K.use_engine('emu')
    K.build_inventory(m, inv)
    hx, hy = spec['hero_px']
    hinfo = W.place_hero(m, hx, hy, direction=spec.get('direction', 24), anim=spec.get('hero_anim', 0), hp=spec.get('hp', 10))
    W.init_pad2(m)
    m.poke(RNG, spec.get('seed', 0x12345678))
    m.hooks.pop(0x80039300, None)                      # the hit pass runs for real
    nh, _ = install_hooks(m, live=(set() if spec.get('inert') else LIVE))
    install_drop_stub(m)
    init_template(m)
    del SPAWN_LOG[:]
    slots, binfo = spawn(m, spec['kind'], spec['recs']) if spec.get('kind') else ([], None)
    spawn_log = [dict(d) for d in SPAWN_LOG]
    if spec.get('status2'):                             # E20.c1's setup: status 2 at once (the real first events pass would run program A and do it)
        for _, sl in slots:
            m.poke(ENT0 + STRIDE * sl + 0x10, 2)
    # the hero's life from the stats (real code, as hworld.setup_h), warm sheet, BSS words
    hp, hpmax = spec.get('hp', 10), spec.get('hpmax', 10)
    m.call(0x8004DC68, hpmax & M32)
    m.call(0x8004DD30, hp & M32)
    got_max = H.s16(m.call(0x8004DC50))
    got_hp = H.s16(m.call(0x8004DD18))
    m.poke(H.HERO + 0x18, got_max)
    m.poke(H.HERO + 0x14, got_hp)
    W.write_pad(m, W.Pad())
    m.call(0x800307E8)
    for a in (H.REGEN, H.REGEN + 4, H.REGEN + 8, H.ITEM_TICKS, H.ITEM_STATE):
        m.poke(a, 0)
    m.poke(SLOTCOUNT, max([0] + [s for _, s in slots]))
    for addr, old, new in PATCHES:                       # mutation tests: one-word patches of the emulated binary (RAM only); the old word is checked
        cur = m.peek(addr)
        if cur != old:
            raise RuntimeError('word at %08X is %08X, expected %08X' % (addr, cur, old))
        m.poke(addr, new)
    m.uninit_reads.clear()
    m.calls.clear()
    m.journal.clear()
    meta = dict(kind=spec.get('kind'), map=mp, hero_start_px=list(spec['hero_px']), slots=slots, handler_hooks=nh, seed=spec.get('seed', 0x12345678), level_D=inf['D'],
                bank=(binfo or {}).get('stats'), inventory=inv['name'], spawn=spawn_log)
    return m, meta


# ---- the map-event stand-in: 0x2C [N] by the real handler ------------------------------------------------------------------------
PROG = 0x801EA400 + 0x200       # scratch program bytes (free RAM of kworld.SCRATCH area, above hworld's SCRATCH2)
STATE_OFF, PC_OFF, RESULT_OFF, SELF_OFF = 0x234, 0x238, 0x260, 0x230


def _call(m, engine, addr, *args):
    if engine == 'emu':
        return m.call(addr, *args)
    return interp2.call2(m, addr, *args)


def count_op2c(m, rec_id, owner_slot=0, self_slot=0, engine='emu'):
    """run the REAL opcode handler 0x8003DC84 for the program [0x2C, rec_id] on the owner entity (the hero, as in the hall after 0x42); returns (Result, matches).  The harness-side words
       it needs (program counter cell, state struct, self pointer) are the ones k3.py decoded.  Restores nothing: the handler only writes the Result word, the list 0x8013D8D8 and the search state."""
    o = ENT0 + STRIDE * owner_slot
    m.poke_bytes(PROG, bytes([0x2C, rec_id, 0xFF]))
    m.poke(o + SELF_OFF, ENT0 + STRIDE * self_slot)
    m.poke(o + STATE_OFF, PROG)
    m.poke(o + PC_OFF, PROG)
    saved = {off: m.peek(o + off) for off in (SELF_OFF, STATE_OFF, PC_OFF, RESULT_OFF)}
    m.poke(o + RESULT_OFF, 0xAAAA5555)
    v0 = _call(m, engine, 0x8003DC84, ENT0 + STRIDE * self_slot, o, o + PC_OFF, o + STATE_OFF) & M32
    res = m.peek(o + RESULT_OFF)
    for off, v in saved.items():
        m.poke(o + off, v)
    return res, v0


def count_search(m, rec_id, owner_slot=0, engine='emu'):
    """the REAL search 0x8003C954(owner, id): the number of matching slots (status of the OWNER 1..3, candidate tested on +0x48 only)"""
    return _call(m, engine, 0x8003C954, ENT0 + STRIDE * owner_slot, rec_id) & M32
