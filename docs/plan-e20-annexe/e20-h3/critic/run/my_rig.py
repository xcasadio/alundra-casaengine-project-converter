"""my_rig.py - critic's own differential rig: random structured states, REAL code of ALUN_CD.EXE in the copied MIPS interpreter vs MY OWN models
(written from my own reading of the disassembly, models in my_models.py).  Side effects of calls that leave the function under test are
journalled by stubs (sound, CreateEffectEntity, SpawnWarpEntity, DestroyEntity 1/2 args, CheckAndTriggerTileEffect) and compared too."""
import random
import sys
import numpy as np
from machine import Machine, M32, s32
import world as W

H = 0x80127D30
STRIDE = 0x294
RNG = 0x80098708
ITEMVAR = 0x80127164
EFFSLOT = 0x801E8000
CELLS = 0x801D0000        # random halfword region for the 4 cell pointers
ATKAREA = 0x801E6000      # attack bytes
STACKLO = 0x801FE000


def ent(i):
    return H + STRIDE * i


def u32(x):
    return x & M32


class Rig:
    def __init__(self):
        m = Machine()
        m.watch_uninit = False
        self.m = m
        self.J = []
        self.spawn_ret = 0
        self.eff_ret = 0
        h = m.hooks

        def sound(mm):
            self.J.append(('sound', mm.r[4] & M32)); mm.r[2] = 0
        h[0x800490FC] = sound

        def effect(mm):
            sp = mm.r[29]
            a = [mm.r[4], mm.r[5], mm.r[6], mm.r[7], mm.peek(sp + 0x10), mm.peek(sp + 0x14)]
            self.J.append(('effect', tuple(x & M32 for x in a)))
            if self.eff_ret:
                for i in range(0x300):
                    mm.ram[(EFFSLOT & 0x1FFFFFFF) + i] = 0
            mm.r[2] = self.eff_ret
        h[0x8003BDD8] = effect

        def spawn(mm):
            sp = mm.r[29]
            a = [mm.r[4], mm.r[5], mm.r[6], mm.r[7], mm.peek(sp + 0x10), mm.peek(sp + 0x14), mm.peek(sp + 0x18)]
            self.J.append(('spawn', tuple(x & M32 for x in a)))
            mm.r[2] = self.spawn_ret
        h[0x80039F58] = spawn

        def d1(mm):
            self.J.append(('destroy1', mm.r[4] & M32)); mm.r[2] = 0
        h[0x8003A774] = d1

        def d2(mm):
            self.J.append(('destroy2', mm.r[4] & M32, mm.r[5] & M32)); mm.r[2] = 0
        h[0x8003A59C] = d2

        def cate(mm):
            self.J.append(('cate', mm.r[4] & M32)); mm.r[2] = 0
        h[0x8003A7B0] = cate
        self.base = None

    def freeze(self):
        self.base = bytes(self.m.ram)

    def reset(self):
        self.m.ram[:] = self.base
        self.J = []

    def run(self, addr, *args):
        self.J = []
        before = bytes(self.m.ram)
        ret = self.m.call(addr, *args)
        after = bytes(self.m.ram)
        return ret, before, after, list(self.J)


def diffs(before, after):
    a = np.frombuffer(before, dtype=np.uint8)
    b = np.frombuffer(after, dtype=np.uint8)
    idx = np.nonzero(a != b)[0]
    return {0x80000000 + int(i): int(b[i]) for i in idx if int(i) < (STACKLO & 0x1FFFFFFF)}


class Mem:
    """overlay over a frozen RAM copy: reads see the overlay, writes accumulate (word granularity from the model, stored as bytes)"""
    def __init__(self, ram):
        self.ram = ram
        self.ov = {}

    def rb(self, a):
        a &= M32
        if a in self.ov:
            return self.ov[a]
        return self.ram[a & 0x1FFFFFFF]

    def rw(self, a):
        return self.rb(a) | self.rb(a + 1) << 8 | self.rb(a + 2) << 16 | self.rb(a + 3) << 24

    def rh(self, a):
        return self.rb(a) | self.rb(a + 1) << 8

    def rws(self, a):
        return s32(self.rw(a))

    def ww(self, a, v):
        v &= M32
        for i in range(4):
            self.ov[(a + i) & M32] = (v >> (8 * i)) & 0xFF

    def changed(self, ram):
        """overlay entries whose value differs from the original RAM (a write of the same value is not a change)"""
        return {a: v for a, v in self.ov.items() if ram[a & 0x1FFFFFFF] != v}
