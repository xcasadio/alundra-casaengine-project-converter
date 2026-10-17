"""h.py - single-call harness over the REAL code of ALUN_CD.EXE (France), slice e14b1 (scratch, read-only on the repo and the binary).
Runs one native handler or one helper in the minimal MIPS interpreter (emu/emu.py, a copy of docs/plan-e14-annexe/e14a/oracle-a1/tools/emu.py),
on an entity image of 0x294 bytes, with the hero in slot 0 (0x80127D30), the RNG word at 0x80098708, and journalled stubs for:
  PlaySoundEffect 0x800490FC, CreateEffectEntity 0x8003BDD8, ComputeNewHp 0x8003A374 (programmable), DestroyEntity 0x8003A59C.
Everything else (relative position, GetDirectionToTarget, TryAttackPlayerFront, StartFlying, CanMoveForward) is REAL code."""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, 'emu'))
import emu  # noqa: E402

M32 = 0xFFFFFFFF
ENT0 = 0x80127D30
STRIDE = 0x294
RNG = 0x80098708
SP = 0x801FFF00
MAPINFO_PTR = 0x800E4334          # [0x800E4334] -> map info struct; byte +0xA is the "C" byte read by C16
MAPINFO = 0x80140000              # where the harness puts the map info struct


def s32(x):
    x &= M32
    return x - (1 << 32) if x & 0x80000000 else x


def lcg(s):
    return (s * 0x7D2B89DD + 0xE06A02E7) & M32


class Ent:
    """an entity image (0x294 bytes) with typed accessors"""

    def __init__(self, raw=None):
        self.raw = bytearray(raw if raw is not None else bytes(STRIDE))

    def copy(self):
        return Ent(self.raw)

    def u32(self, o):
        return int.from_bytes(self.raw[o:o + 4], 'little')

    def s32(self, o):
        return s32(self.u32(o))

    def set32(self, o, v):
        self.raw[o:o + 4] = (v & M32).to_bytes(4, 'little')

    def u16(self, o):
        return int.from_bytes(self.raw[o:o + 2], 'little')

    def s16(self, o):
        v = self.u16(o)
        return v - 0x10000 if v & 0x8000 else v

    def set16(self, o, v):
        self.raw[o:o + 2] = (v & 0xFFFF).to_bytes(2, 'little')

    def u8(self, o):
        return self.raw[o]

    def set8(self, o, v):
        self.raw[o] = v & 0xFF


class World:
    def __init__(self):
        self.m = emu.make_machine()
        self.j = []                  # journal of stubbed calls
        self.hp_result = 0           # what the ComputeNewHp stub returns
        self.hp_write = None         # optional value the stub writes at +0x14 of its argument
        h = self.m.hooks

        def sound(m):
            self.j.append(('sound', m.r[4] & M32))

        def effect(m):
            sp = m.r[29]
            self.j.append(('effect', m.r[4] & M32, m.r[5] & M32, m.r[6] & M32, m.r[7] & M32, m.rw(sp + 0x10), m.rw(sp + 0x14)))

        def cnh(m):
            self.j.append(('cnh', ((m.r[4] & M32) - ENT0) // STRIDE))
            if self.hp_write is not None:
                m.ww(m.r[4] + 0x14, self.hp_write)
            m.r[2] = self.hp_result

        def destroy(m):
            self.j.append(('destroy', ((m.r[4] & M32) - ENT0) // STRIDE, s32(m.r[5])))

        h[0x800490FC] = sound
        h[0x8003BDD8] = effect
        h[0x8003A374] = cnh
        h[0x8003A59C] = destroy
        # the map info struct (byte 0xA = "C")
        self.m.ww(MAPINFO_PTR, MAPINFO)

    # ---- memory -----------------------------------------------------------------------------------------------------------------
    def put(self, slot, ent):
        a = ENT0 + slot * STRIDE
        for i, b in enumerate(ent.raw):
            self.m.wb(a + i, b)

    def get(self, slot):
        a = ENT0 + slot * STRIDE
        return Ent(bytes(self.m.ram[a & 0x1FFFFFFF:(a & 0x1FFFFFFF) + STRIDE]))

    def set_rng(self, s):
        self.m.ww(RNG, s & M32)

    def get_rng(self):
        return self.m.rw(RNG)

    def set_mapc(self, c):
        self.m.wb(MAPINFO + 0xA, c)

    def call(self, addr, *args, stack=()):
        for i, v in enumerate(stack):
            self.m.ww(SP + 0x10 + 4 * i, v & M32)
        return self.m.call(addr, *args)


class Rng:
    """the model of the generator: state word, the draw below n, and the log of the draws"""

    def __init__(self, s):
        self.s = s & M32
        self.draws = []

    def below(self, n):
        self.s = lcg(self.s)
        r = (self.s * n) >> 32
        self.draws.append((n, r))
        return r
