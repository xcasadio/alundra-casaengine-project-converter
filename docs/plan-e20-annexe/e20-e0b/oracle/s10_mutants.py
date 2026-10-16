"""s10_mutants.py - do the vectors bite?  One-word patches of the emulated binary (RAM only); each mutant is run on (1) the 2626 ChooseRandomlyAnItem calls, (2) the 52 roll vectors,
(3) a reduced frame run (gates 0x00 / 0x48, a0 0 / 1, trigger with the inventory refusing).  A mutant is CAUGHT when its signature differs from the unpatched one."""
import sys
import json
import hashlib
sys.path.insert(0, 'tools')
sys.path.insert(0, '.')
from machine import Machine, M32, s32
import espawn as X
import c1world as C
import emu

RNG = 0x80098708
E, REC, HDR = 0x80180000, 0x80180400, 0x80180500
BANK_P, BANK_T = 0x801EB344, 0x801EBA40
CHOOSE = json.load(open('out/choose_vectors.json'))
ROLL = json.load(open('out/roll_vectors.json'))


def word(a):
    import xdis
    return xdis.u32(a)


def sig_choose_roll(patches):
    m = Machine()
    m.watch_uninit = False
    for a, old, new in patches:
        if 0x80032000 <= a < 0x80033000:
            assert m.peek(a) == old, (hex(a), hex(m.peek(a)), hex(old))
            m.poke(a, new)
    h = hashlib.sha1()
    for code, s, _, _, _ in CHOOSE:
        m.poke(RNG, s)
        h.update(('%d,%d,%d;' % (m.call(0x80032968, code) & M32, m.peek(RNG), 0)).encode())
    for v in ROLL:
        for a in range(0, 0x300, 4):
            m.poke(E + a, 0)
        for a in range(0, 0x40, 4):
            m.poke(REC + a, 0)
            m.poke(HDR + a, 0)
        for b in (BANK_P, BANK_T):
            for a in range(0, 0x100, 4):
                m.poke(b + a, 0)
        m.poke(E + 0x64, HDR)
        m.poke(HDR + 0x1C, v['header_contents'] << 24)
        if v['rec']:
            m.poke(E + 0x44, REC)
            m.poke(REC + 0x10, (v['flag'] & 0xFFFF) | (v['rec_code'] << 16))
        for (bank, word_i, val) in v['banks']:
            m.poke(bank + 4 * word_i, val)
        m.poke(E + 0x3C, 0xA5A5A5A5)
        m.poke(E + 0x40, 0xA5A5A5A5)
        m.poke(RNG, v['rng_in'])
        m.call(0x80032A40, E)
        h.update(('%d,%d,%d;' % (s32(m.peek(E + 0x3C)), s32(m.peek(E + 0x40)), m.peek(RNG))).encode())
    return h.hexdigest()


_world = {}


def sig_frame(patches):
    class FM(Machine):
        SCR = 0x1FD000

        def _a(self, addr):
            a = addr & 0x1FFFFFFF
            if 0x1F800000 <= a < 0x1F800400:
                return self.SCR + (a - 0x1F800000)
            return a

        def _chk(self, a, n):
            pass

        def _mark(self, a, n):
            pass
    X.Machine = FM
    w = X.World(map_no=14)
    m = w.m
    m.watch_uninit = False
    for a, old, new in patches:
        if 0x8002BA00 <= a < 0x8002BE00:
            assert m.peek(a) == old, (hex(a), hex(m.peek(a)), hex(old))
            m.poke(a, new)
    inv = {'v': 1}

    def stub(draw=False):
        def f(mm):
            if draw:
                C.side_call(mm, 0x80032968, (0x80,), 0x801FE800)
            mm.r[2] = 0
        return f
    m.hooks[0x8002E38C] = stub()
    m.hooks[0x8004B1D4] = stub()
    m.hooks[0x80055570] = lambda mm: mm.r.__setitem__(2, inv['v'])
    m.hooks[0x80031B50] = stub(True)
    m.hooks[0x80038E84] = stub(True)
    m.hooks[0x80039300] = stub(True)
    h = hashlib.sha1()
    for a0, gate, trig, iv in ((0, 0, 0, 1), (0, 0x48, 0, 1), (1, 0, 0, 1), (1, 0x48, 0, 1), (0, 0, 1, 0), (1, 0, 1, 0), (0, 0, 1, 1), (0, 0x08, 1, 0)):
        for seed in (0x12345678, 0xFFFFFFFF):
            m.poke(RNG, seed)
            m.poke(0x800DC4B8, gate)
            m.poke(0x800DC4C4, 0)
            m.poke(0x800DC4B4, 0)
            m.poke(0x80127164, 0)
            m.poke(0x800C4980, 0)
            m.poke(0x80127D50, 0)
            m.poke(0x80126F28, 0)
            m.poke(0x80126F2A, 0x0800 if trig else 0)
            m.poke(0x80126E68, 0)
            m.poke(0x80134360, 0)
            inv['v'] = iv
            m.call(0x8002BAEC, a0, limit=20_000_000)
            h.update(('%d,%d;' % (m.peek(RNG), m.peek(0x800DC4C4))).encode())
    return h.hexdigest()


# (address, old word, new word, label)
MUT = [
    (0x800329A4, 0x24030010, 0x2403000F, 'choose: draw range 16 -> 15'),
    (0x80032980, 0x10400019, 0x14400019, 'choose: 0x80-bit test inverted (beqz -> bnez)'),
    (0x80032980, 0x10400019, 0x10400018, 'choose: branch target one word early (EQUIVALENT: lands on a delay-slot slti whose result is overwritten)'),
    (0x800329E8, 0x28a30062, 0x28a30061, 'choose: item limit 0x62 -> 0x61'),
    (0x80032A6C, 0x28420800, 0x28420400, 'roll: flag threshold 0x800 -> 0x400'),
    (0x80032A8C, 0x30628000, 0x30620000, 'roll: bank select bit 0x8000 ignored (always persistent)'),
    (0x80032AE0, 0x90c50012, 0x90c50013, 'roll: record code byte +0x12 -> +0x13'),
    (0x80032B04, 0x9045001f, 0x9045001e, 'roll: header Contents byte +0x1F -> +0x1E'),
    (0x80032AD8, 0x1480000b, 0x1080000b, 'roll: flag-set test inverted (bnez -> beqz)'),
    (0x8002BD18, 0x346389dd, 0x346389dc, 'frame: multiplier low bit'),
    (0x8002BD30, 0x12600003, 0x16600003, 'frame: a0 test inverted (beqz -> bnez)'),
    (0x8002BC78, 0x14400022, 0x10400022, 'frame: gate test before the inventory trigger inverted'),
    (0x8002BCF4, 0x14400003, 0x10400003, 'frame: DisplayInventory result test inverted'),
]
base_cr = sig_choose_roll([])
base_fr = sig_frame([])
print('baseline signatures', base_cr[:12], base_fr[:12])
caught = 0
rows = []
for a, o, n, lab in MUT:
    try:
        if 0x80032000 <= a < 0x80033000:
            s = sig_choose_roll([(a, o, n)])
            ok = s != base_cr
        else:
            s = sig_frame([(a, o, n)])
            ok = s != base_fr
    except RuntimeError as ex:          # a diverging mutant (step limit) is detected as well
        ok = True
    caught += ok
    rows.append((lab, ok))
    print('%-62s %s' % (lab, 'CAUGHT' if ok else 'SURVIVED'))
print('mutants caught: %d of %d' % (caught, len(MUT)))
json.dump(rows, open('out/mutants.json', 'w'))
