"""exp_spawn.py - e0-spawn discovery probe (scratch only): runs the REAL SpawnWarpEntity 0x80039F58 (-> 0x80039B28 table lookup, 0x80039AD0 FindFreeEntitySlot,
0x80039D04 InitializeEntity, 0x80032A40 loot roll) in the MIPS machine of the D1/c1 oracles, over a synthetic GLOBAL sprite table made of real records of map_alundra.json.
What is real: every function named above and the events pass 0x800386D0, 0x80038634, 0x80038998, 0x800384F4, 0x80038E18, 0x80038364.
What is synthetic: the table block 0x80126EC0 (+0xC pointer array, +0x10 count), the sprite records (bank.install), the hero (as in D1), the flat field, a handful of BSS words.
Nothing here is an annex value; every result printed is read from the machine's RAM."""
import os
import sys
import json

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from machine import Machine, M32, s32
import emu
import world as W
import bank
import c1world as C

DATA = "D:/development/repo/alundra-casaengine-project-converter/data-extracted/data"
GL = json.load(open(DATA + "/map_alundra.json", encoding="utf-8-sig"))["SpriteInfo"]["SpriteRecords"]
GT = 0x80126EC0          # global sprite table block: +0xC pointer array, +0x10 count
MT = 0x80126E70          # per-map sprite table block (same layout)
PTRS = 0x801EE000        # global pointer array (257 entries)
RECS = 0x801F0000        # synthetic records, 0x2000 apart
RNG = 0x80098708
ENT0, STRIDE = W.ENT0, W.STRIDE


def ent(i):
    return ENT0 + STRIDE * i


def slot_of(p):
    return (p - ENT0) // STRIDE if ENT0 <= p < ENT0 + 64 * STRIDE else None


class Probe:
    def __init__(self, indices=(3, 4, 7, 27, 2, 1, 99, 114, 236), seed=0x12345678):
        m = Machine()
        W.build_world(m)
        W.init_bss(m)
        W.load_balance(m, 0)
        W.init_stats(m)
        W.place_hero(m, 100, 100)
        W.init_pad2(m)
        m.hooks.pop(0x80039F58, None)                 # the REAL spawn
        self.printed = []

        def printer(mm):                                # libc printf-like of the error paths: noted
            self.printed.append((mm.cur_pc, mm.r[4] & M32))
            mm.r[2] = 0
        m.hooks[0x800815EC] = printer
        m.hooks[0x800815FC] = printer
        m.poke(GT + 0xC, PTRS)
        m.poke(GT + 0x10, 257)
        for k in range(257):
            m.poke(PTRS + 4 * k, 0xFFFFFFFF)            # invalid entry (file value -1)
        self.installed = {}
        cursor = RECS
        for idx in indices:
            if not GL[idx]:
                continue
            info = bank.install(m, cursor, cursor, GL[idx])
            m.poke(PTRS + 4 * idx, cursor)
            self.installed[idx] = cursor
            cursor = (info['end'] + 0xF) & ~0xF
            assert cursor < 0x801FE000, hex(cursor)
        m.poke(RNG, seed)
        m.uninit_reads.clear()
        self.m = m

    # ---- the real call -------------------------------------------------------------------------------------------
    def spawn(self, parent, is_map, index, x, y, z, direction):
        m = self.m
        sp = 0x801FFF00
        m.poke(sp + 0x10, y)
        m.poke(sp + 0x14, z)
        m.poke(sp + 0x18, direction)
        return m.call(0x80039F58, parent, is_map, index, x, limit=5_000_000) & M32

    def snap(self, p):
        m = self.m
        g = lambda o: m.peeks(p + o)
        u = lambda o: m.peek(p + o)
        return dict(slot=slot_of(p), index=u(0x0), serial=u(0x4), root=slot_of(u(0x8)), parent=slot_of(u(0xC)), status=u(0x10), hp=g(0x14), hpmax=g(0x18),
                    loot_item=u(0x3C), loot_flag=u(0x40), rec44=u(0x44), ref48=g(0x48), prog4c=[g(0x4C + 4 * i) for i in range(6)], sprite64=hex(u(0x64)), id68=u(0x68),
                    flags6c=hex(u(0x6C)), codes70=[u(0x70 + 4 * i) for i in range(6)], anim88=g(0x88), dir8c=g(0x8C), cur90=hex(u(0x90)), cur94=hex(u(0x94)),
                    pos=(g(0x114), g(0x118), g(0x11C)), terrain138=g(0x138), modded=(g(0x1D8), g(0x1DC), g(0x1E0)), tilex120=g(0x120), tiley124=g(0x124),
                    pal1b0=u(0x1B0), sheet1b4=u(0x1B4), bal1c4=hex(u(0x1C4)), trig228=g(0x228), logic230=slot_of(u(0x230)), ai280=m.peek(p + 0x280) & 0xFFFF,
                    bytes274=m.peek(p + 0x274), c1c=g(0x1C), box1d4=hex(u(0x1D4)), atk1c8=hex(u(0x1C8)), animflags_b4=hex(u(0xB4)))


def line(title):
    print('\n=== ' + title)


def main():
    pr = Probe()
    m = pr.m
    hero = ent(0)
    print('uninit reads after setup:', len(m.uninit_reads))

    line('E1  real SpawnWarpEntity(hero, global, 3 = Fleches Niv.1) into an empty table (hero in slot 0)')
    rng0 = m.peek(RNG)
    p = pr.spawn(hero, 0, 3, 150 << 16, 100 << 16, 0, 0x10)
    print('returned slot', slot_of(p), ' RNG advanced by the spawn:', rng0 != m.peek(RNG), 'count word 0x80134360 =', m.peeks(0x80134360), ' serial counter 0x80134600 =', m.peek(0x80134600))
    for k, v in pr.snap(p).items():
        print('  %-10s %s' % (k, v))

    line('E2  second spawn (global 4) takes the next free slot; third (global 7)')
    p2 = pr.spawn(hero, 0, 4, 150 << 16, 100 << 16, 0, 0x10)
    p3 = pr.spawn(0, 0, 7, 160 << 16, 100 << 16, 0, 0x08)
    print('slots', slot_of(p), slot_of(p2), slot_of(p3), '; parent/root of the third (no parent):', pr.snap(p3)['parent'], pr.snap(p3)['root'], ' root of the second:', pr.snap(p2)['root'])

    line('E3  free-slot reuse: destroy slot 2 (status 4), run the real slot pass 0x80038634, spawn again')
    m.poke(p2 + 0x10, 4)
    m.call(0x80038634)
    print('after the slot pass: status of slot 2 =', m.peek(p2 + 0x10), ' index word =', m.peek(p2), ' trigger word +0x228 =', m.peeks(p2 + 0x228), ' ref48 =', m.peeks(p2 + 0x48), ' [0x80134360] =', m.peeks(0x80134360))
    p4 = pr.spawn(hero, 0, 27, 170 << 16, 100 << 16, 0, 0x18)
    print('new entity went to slot', slot_of(p4), '(slot 2 was the first free one); serial', m.peek(p4 + 4), 'vs previous serials', m.peek(p + 4), m.peek(p3 + 4))

    line('E4  a destroyed slot is NOT reusable in the same tick (status 4 until the next slot pass)')
    m.poke(p + 0x10, 4)
    p5 = pr.spawn(hero, 0, 2, 100 << 16, 90 << 16, 0, 0)
    print('slot of the spawn made while slot 1 is status 4:', slot_of(p5), '(slot 1 is', slot_of(p), ')')

    line('E5  invalid index (-1 table entry 5) and out-of-range index 300')
    r1 = pr.spawn(hero, 0, 5, 0, 0, 0, 0)
    r2 = pr.spawn(hero, 0, 300, 0, 0, 0, 0)
    print('returns', hex(r1), hex(r2), 'error-print calls', [(hex(a), hex(b)) for a, b in pr.printed])

    line('E6  64-slot limit: fill the table, then spawn')
    n = 0
    while True:
        q = pr.spawn(hero, 0, 3, 150 << 16, 100 << 16, 0, 0x10)
        if q == 0:
            break
        n += 1
        if n > 70:
            break
    print('spawned until full:', n, 'entities; the next call returned 0; error prints:', len(pr.printed))
    print('slot count word 0x80134360 =', m.peeks(0x80134360))


if __name__ == '__main__':
    main()
