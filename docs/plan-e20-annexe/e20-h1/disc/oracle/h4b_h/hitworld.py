"""hitworld.py - D2 oracle (E20.0): synthetic entity sets run through the REAL hit pass of ALUN_CD.EXE (France).

Real code (run by the emulated machine, copied from D1 / E19.h2):
    0x80038634  slot pass     (sets [0x80134360] = last non-empty slot)
    0x800384F4  list pass     (builds the active list 0x80127B28 / count 0x80127D28 from the slots)
    0x80039300  UpdateBalanceRecords (the hit pass)
    0x8003A374  ComputeNewHp  (called by the harness for every entity left with a TouchingEntity, as the D handlers do)
    0x8004464C  ResolveBalanceTarget (called by ComputeNewHp and read for the response by the hit pass)
    0x80044520  / 0x80044550  balance level + record selection (random sets)
    0x80038998  counters pass (extra checks)
Stubs, each journalled (machine.py): effects 0x8003BDD8 / 0x8003BE74 (note id and arguments, hand back a zeroed dummy slot), sound.  The pseudo-random
generator [0x80098708] runs in real code (the spark routine 0x80039188), with a noted seed.
Nothing here knows an expected result: expectations live in cases.py (hand-written from section 0.2.5 of the plan) and model.py (independent model).

Memory the harness builds (everything else is the EXE image or zero):
    balance blob       0x80143DF8   = balance_enc.build_blob() (alundra-project/Data/balance.json encoded, == disc DATA/BALANCE.BIN byte for byte)
    [0x80143DF0]       balance level D   (real 0x80044520(D); it also zeroes the three equipment sheet pointers 0x80146E00 + 8k)
    entity slots       0x80127D30 + 0x294 * i, 64 slots, only the fields listed in FIELDS are written, the shadow map reports any other read
    hero working sheet 0x80127008 (<= 0xFF bytes), read by ComputeNewHp for the entity at 0x80127D30 only
    synthetic attacks  0x801E6000 (2 bytes each: attribute byte, power byte), the pointer +0x1C8 may point there or into the blob
    box record         0x801E7000 (only its address is used: +0x1D4 is tested for non-zero by the pass)
"""
import os
import sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from machine import Machine, M32, s32
import balance_enc as B

ENT0 = 0x80127D30
STRIDE = 0x294
NSLOT = 64
BAL_BASE = 0x80143DF8
LEVEL_WORD = 0x80143DF0
EQUIP = 0x80146E00                 # 3 pointers, stride 8
G_ANIM = 0x80146DF8                # latched hero animation (written by the hit pass at 0x80039348)
HERO_SHEET = 0x80127008
ATK_SCRATCH = 0x801E6000
BOX_REC = 0x801E7000
RNG = 0x80098708                    # lw -0x78f8(0x800A0000) in the spark routine 0x80039188 (0x8003920C): 0x800A0000 - 0x78F8
LIST = 0x80127B28
LIST_N = 0x80127D28
LAST = 0x80134360

FIELDS = dict(cnt1c=0x1C, status=0x10, hp=0x14, hpmax=0x18, ctl20=0x20, c28=0x28, flags=0x6C, anim_target=0x88, animflags=0xB4, sheet=0x1C4, atk=0x1C8,
              c1cc=0x1CC, c1d0=0x1D0, box=0x1D4, mp=(0x1D8, 0x1DC, 0x1E0), tsize=(0x1F0, 0x1F4, 0x1F8), hb=(0x1FC, 0x200, 0x204),
              asize=(0x214, 0x218, 0x21C), hitcounter=0x220, touching=0x224)

CLASS_A, HITS_B, HITS_A, CLASS_B, CLASS_C, HITS_C = 0x1, 0x2, 0x4, 0x8, 0x800, 0x1000


def ent(i):
    return ENT0 + STRIDE * i


def px(n):
    """a length of n pixels in raw 16.16"""
    return n << 16


def size(n):
    """how the binary stores the size of an n-pixel box: (n << 16) - 1 (attacker: 0x80038C80-88; target: 0x80039CB8-C0, n = 0 stored 0)"""
    return (n << 16) - 1 if n else 0


BSS_ZERO = [
    (0x800DC058, 'sign bit set = debug/transition mode: the hit pass (0x8003952C), ComputeNewHp (0x8003A3D8) and ResolveBalanceTarget (0x80044658) '
                 'skip their printf-style branch when it is >= 0; zero = release behaviour'),
    (0x80127D2C, 'collidable-list count: written by 0x800384F4 before any read'),
    (0x80134600, 'entity serial counter (not read by the passes used here)'),
]


class World:
    """one machine, built once; restore() resets it to the pristine state between sets (RAM and initialised-byte shadow)"""

    def __init__(self):
        m = Machine()
        m.hooks.pop(0x80039300, None)                    # D1 stubbed the hit pass; D2 runs it for real
        self.m = m
        blob, recs = B.build_blob()
        self.blob_len = len(blob)
        for a, _ in BSS_ZERO:
            m.poke(a, 0)
        m.poke_bytes(BAL_BASE, blob)
        m.call(0x80044520, 0)                            # real: [0x80143DF0] = 0, equipment sheet pointers = 0
        # the slots: contents unknown until the harness writes them -> clear their init shadow, so that any read of an unset byte is reported
        o = ENT0 & 0x1FFFFFFF
        m.ram[o:o + NSLOT * STRIDE] = bytes(NSLOT * STRIDE)
        m.init[o:o + NSLOT * STRIDE] = bytes(NSLOT * STRIDE)
        o = HERO_SHEET & 0x1FFFFFFF
        m.ram[o:o + 0x100] = bytes(0x100)
        m.init[o:o + 0x100] = bytes(0x100)
        m.poke(RNG, 0x12345678)
        self.snap = (bytes(m.ram), bytes(m.init))
        self.recs = recs                                 # FileOffset -> record bytes

    def restore(self):
        m = self.m
        m.ram[:] = self.snap[0]
        m.init[:] = self.snap[1]
        m.uninit_reads.clear()
        m.journal.clear()
        m.calls.clear()
        m.hi = m.lo = 0

    # ---- builders -------------------------------------------------------------------------------------------------
    def set_level(self, d):
        self.m.call(0x80044520, d)

    def record_addr(self, sprite_index):
        """the balance record the binary selects for a sprite index at the current level: REAL 0x80044550"""
        return self.m.call(0x80044550, sprite_index) & M32

    def put(self, slot, **sp):
        """writes one entity slot.  Keys: see FIELDS; scalars are written as u32, tuples as three u32.  Unlisted fields stay unset (reads reported)."""
        m = self.m
        e = ent(slot)
        for k, v in sp.items():
            f = FIELDS[k]
            if isinstance(f, tuple):
                for off, x in zip(f, v):
                    m.poke(e + off, x & M32)
            else:
                m.poke(e + f, v & M32)

    def blank_slots(self, used):
        """slots not described by the set: empty (status 0).  The slot pass reads the status of all 64 slots; the list pass reads +0x20 (only for
        status 2/3), +0x28, +0x6C, +0xB4 and +0x1CC of every slot up to the last non-empty one; the counters pass also reads +0x1C and +0x1D0: set to 0 explicitly."""
        for i in range(NSLOT):
            if i not in used:
                self.put(i, status=0, ctl20=0, c28=0, flags=0, animflags=0, c1cc=0, c1d0=0, cnt1c=0)

    def put_attack(self, idx, attr, power):
        a = ATK_SCRATCH + 2 * idx
        self.m.poke8(a, attr)
        self.m.poke8(a + 1, power)
        return a

    def load_hero_sheet(self, rec_bytes):
        for i, b in enumerate(rec_bytes):
            self.m.poke8(HERO_SHEET + i, b)

    def set_equipment(self, ptrs):
        for k in range(3):
            self.m.poke(EQUIP + 8 * k, ptrs[k] if k < len(ptrs) else 0)

    # ---- running --------------------------------------------------------------------------------------------------
    def pre_passes(self):
        """slot pass then list pass, both real"""
        self.m.call(0x80038634, limit=5_000_000)
        self.m.call(0x800384F4, limit=5_000_000)

    def hit_pass(self):
        self.m.call(0x80039300, limit=5_000_000)

    def damage_all(self, slots):
        """the D handlers: ComputeNewHp (real) for every entity that the pass left with a TouchingEntity.  Returns {slot: returned flag (HP == 0)}"""
        m = self.m
        out = {}
        for i in slots:
            if m.peek(ent(i) + FIELDS['touching']) != 0:
                out[i] = m.call(0x8003A374, ent(i)) & M32
        return out

    def counters(self):
        self.m.call(0x80038998, limit=5_000_000)

    def snapshot(self, slots):
        """observable state of the given slots after a run"""
        m = self.m
        d = {}
        for i in slots:
            e = ent(i)
            t = m.peek(e + FIELDS['touching'])
            d[i] = dict(touching=t, touching_slot=((t - ENT0) // STRIDE) if t else None, c1d0=m.peek(e + FIELDS['c1d0']), hitcounter=m.peek(e + FIELDS['hitcounter']),
                        hp=s32(m.peek(e + FIELDS['hp'])), c1cc=m.peek(e + FIELDS['c1cc']))
        return d

    def effects(self):
        """journal entries of the stubbed effect calls, as (kind, arg_list)"""
        return [(j[1], j[2]) for j in self.m.journal if j[1] in ('effect_3BDD8', 'effect_3BE74')]

    def active_list(self):
        m = self.m
        n = m.peek(LIST_N)
        return [(m.peek(LIST + 4 * k) - ENT0) // STRIDE for k in range(n)]
