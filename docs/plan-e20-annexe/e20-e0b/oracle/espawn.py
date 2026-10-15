"""espawn.py - E20.e0a test-means probe (scratch only).  The REAL SpawnWarpEntity 0x80039F58 (-> 0x80039B28 page lookup, 0x80039AD0 FindFreeEntitySlot, 0x80039D04 InitializeEntity,
0x80032A40 loot roll) on the E20 oracle machine (tools/ = byte copy of docs/plan-e14-annexe/e14a/oracle-a1/tools + e14world/hworld), with BOTH pages installed:
  * global page  = block 0x80126EC0 (+0xC pointer array, +0x10 count), the 151 non-null SpriteRecords of map_alundra.json,
  * map page     = block 0x80126E70 (same layout), the non-null SpriteRecords of one map_N.json.
Count word [+0x10] = 256 (the loader 0x8002D808 stores the number of pointer-table entries scanned before a zero entry, capped at 0x100: 0x8002D84C-0x8002D894).
A table entry for a null record is 0xFFFFFFFF (file value -1, kept by the loader).
Nothing here knows an expected value; every number printed is read from the machine's RAM."""
import os
import sys
import json
import hashlib

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, 'tools'))
REPO = os.path.abspath(os.path.join(HERE, '..', '..', '..', '..'))     # docs/plan-e20-annexe/e20-e0a/oracle -> repository root
from machine import Machine, M32, s32
import world as W
import bank
import c1world as C

DATA = os.path.join(REPO, "data-extracted", "data")         # read only; the tools/ copies keep their own absolute paths (byte-identical to e14a)
GT = 0x80126EC0           # global page block
MT = 0x80126E70           # map page block
GPTRS = 0x80152000        # 256 pointers
MPTRS = 0x80152800        # 256 pointers
GREC = 0x80160000         # global records (209 KB)
MREC = 0x80194000         # map records (0x80194000 .. 0x801A0000 = 48 KB; map 388 needs 25 KB)
RNG = C.RNG
TEMPLATE = 0x80134368
SLOTCOUNT = 0x80134360
SERIAL = 0x80134600
ENT0, STRIDE = W.ENT0, W.STRIDE

_J = {}


def mapjson(n):
    if n not in _J:
        _J[n] = json.load(open(DATA + ('/map_%d.json' % n if n != 'alundra' else '/map_alundra.json'), encoding='utf-8-sig'))['SpriteInfo']
    return _J[n]


def ent(i):
    return ENT0 + STRIDE * i


def slot_of(p):
    return (p - ENT0) // STRIDE if ENT0 <= p < ENT0 + 64 * STRIDE else None


FIELDS = [   # (name, offset, signed)
    ('index', 0x00, 0), ('serial', 0x04, 0), ('root', 0x08, 0), ('parent', 0x0C, 0), ('status', 0x10, 0), ('hp', 0x14, 1), ('hpmax', 0x18, 1),
    ('loot_item', 0x3C, 1), ('loot_flag', 0x40, 1), ('rec44', 0x44, 0), ('ref48', 0x48, 1),
    ('prog4c_0', 0x4C, 1), ('prog4c_1', 0x50, 1), ('prog4c_2', 0x54, 1), ('prog4c_3', 0x58, 1), ('prog4c_4', 0x5C, 1), ('prog4c_5', 0x60, 1),
    ('sprite64', 0x64, 0), ('id68', 0x68, 0), ('flags6C', 0x6C, 0),
    ('cA', 0x70, 0), ('cB', 0x74, 0), ('cC', 0x78, 0), ('cD', 0x7C, 0), ('cE', 0x80, 0), ('cF', 0x84, 0),
    ('anim_t', 0x88, 1), ('dir_t', 0x8C, 1), ('anim_c', 0x90, 0), ('dir_c', 0x94, 0),
    ('pal1b0', 0x1B0, 0), ('sheet1b4', 0x1B4, 0), ('bal1c4', 0x1C4, 0), ('self230', 0x230, 0),
    ('bodyoff', 0x1E4, 1), ('bodyoffy', 0x1E8, 1), ('bodyoffz', 0x1EC, 1), ('bodysx', 0x1F0, 1), ('bodysy', 0x1F4, 1), ('bodysz', 0x1F8, 1),
    ('px', 0x114, 1), ('py', 0x118, 1), ('pz', 0x11C, 1), ('terrain138', 0x138, 1), ('modx', 0x1D8, 1), ('mody', 0x1DC, 1), ('modz', 0x1E0, 1),
    ('tilex120', 0x120, 1), ('tiley124', 0x124, 1), ('trig228', 0x228, 1), ('ai280', 0x280, 0),
    ('box1d4', 0x1D4, 0), ('atk1c8', 0x1C8, 0), ('animflags_b4', 0xB4, 0),
    ('floor134', 0x134, 1), ('mh0', 0x158, 1), ('mh1', 0x15C, 1), ('mh2', 0x160, 1), ('mh3', 0x164, 1), ('tilez128', 0x128, 1),
]


class World:
    def __init__(self, map_no=14, seed=0x12345678, global_all=True, raised=0):
        m = Machine()
        # raised > 0: every cell of the field has this height byte (1 = 16 px), so the terrain under any point is raised*16 px
        W.build_world(m, cells=(lambda col, row: (raised, 0, 0, 0)) if raised else None)
        W.init_bss(m)
        W.load_balance(m, 0)
        W.init_stats(m)
        W.place_hero(m, 100, 100)
        W.init_pad2(m)
        m.hooks.pop(0x80039F58, None)                # the REAL spawn
        self.printed = []

        def printer(mm):
            self.printed.append((mm.cur_pc, mm.r[4] & M32))
            mm.r[2] = 0
        m.hooks[0x800815EC] = printer                 # the two error printers of 0x80039B28 and 0x80039AD0
        m.hooks[0x800815FC] = printer
        # slot initialisation of the engine (0x8003B214-0x8003B24C): template zeros + (+0x48 = -1), every slot = template + its index
        m.zero(TEMPLATE, STRIDE)
        m.poke(TEMPLATE + 0x48, 0xFFFFFFFF)
        for i in range(1, 64):                         # the map-load init 0x8003B24C copies the template into all 64 slots: reproduced by the REAL slot pass over 63 'destroyed' slots
            m.poke(ent(i), i)
            m.poke(ent(i) + 0x10, 4)
        m.call(0x80038634)
        self.m = m
        self.rec_g = {}
        self.rec_m = {}
        self.install_page(GT, GPTRS, GREC, mapjson('alundra')['SpriteRecords'], self.rec_g)
        self.map_no = map_no
        if map_no is not None:
            self.install_page(MT, MPTRS, MREC, mapjson(map_no)['SpriteRecords'], self.rec_m)
        m.poke(RNG, seed)
        m.uninit_reads.clear()
        m.journal.clear()

    def install_page(self, block, ptrs, base, records, out):
        m = self.m
        m.poke(block + 0xC, ptrs)
        m.poke(block + 0x10, 256)
        cursor = base
        for k in range(256):
            m.poke(ptrs + 4 * k, 0xFFFFFFFF)
        for k, rec in enumerate(records[:256]):
            if not rec:
                continue
            info = bank.install(m, cursor, cursor, rec)
            m.poke(ptrs + 4 * k, cursor)
            out[k] = cursor
            cursor = (info['end'] + 0xF) & ~0xF
        m.poke(ptrs + 4 * 256 if False else block + 0x10, 256)
        return cursor

    def swap_map(self, map_no):
        self.rec_m = {}
        self.map_no = map_no
        self.install_page(MT, MPTRS, MREC, mapjson(map_no)['SpriteRecords'], self.rec_m)

    def spawn(self, parent, is_map, index, x_px, y_px, z, direction):
        m = self.m
        sp = 0x801FFF00
        m.poke(sp + 0x10, (y_px << 16) & M32)
        m.poke(sp + 0x14, z & M32)
        m.poke(sp + 0x18, direction)
        return m.call(0x80039F58, parent, is_map, index & M32, (x_px << 16) & M32, limit=5_000_000) & M32

    def snap(self, p):
        m = self.m
        d = {}
        for name, off, sg in FIELDS:
            v = m.peeks(p + off) if sg else m.peek(p + off)
            d[name] = v
        for k in ('root', 'parent', 'self230'):
            d[k] = slot_of(d[k])
        return d

    def image(self, p):
        return bytes(self.m.ram[(p & 0x1FFFFFFF):(p & 0x1FFFFFFF) + STRIDE])

    def wipe(self, p):
        """destroy as the engine does: status 4, then the real slot pass 0x80038634 (copies the template, rewrites the index word)"""
        self.m.poke(p + 0x10, 4)
        self.m.call(0x80038634)

    def draws(self, before, after, limit=8):
        s = before
        n = 0
        while s != after and n < limit:
            s = (s * 0x7D2B89DD + 0xE06A02E7) & M32
            n += 1
        return n if s == after else -1
