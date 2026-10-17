"""lh.py - loot harness (scratch). Real code of ALUN_CD.EXE (France) in the minimal MIPS interpreter (emu/emu.py copy).
Stubs only for what is outside the loot logic: SpawnWarpEntity 0x80039F58 (journal + returns a fake entity), PlaySoundEffect 0x800490FC (journal).
Instruction coverage is recorded in self.cov (set of executed pcs)."""
import os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, 'emu'))
import emu
M32 = 0xFFFFFFFF
RNG = 0x80098708
ENT0 = 0x80127D30
STRIDE = 0x294
PLAYER_PTR = 0x80176318      # pointer to the player data struct
PLAYER = 0x80141000          # where the harness puts the player data struct
ITEMTAB = 0x800C5F7C
RANDTAB = 0x80028E0C
FAKE_BASE = 0x80150000       # fake entities returned by the SpawnWarpEntity stub

def s32(x):
    x &= M32
    return x - (1 << 32) if x & 0x80000000 else x

def lcg(s):
    return (s * 0x7D2B89DD + 0xE06A02E7) & M32

class Cov(emu.Emu):
    def __init__(self):
        super().__init__()
        self.cov = set()
        self.tracing = False
    def step(self):
        if self.tracing:
            self.cov.add(self.pc)
            # delay slots are executed inside exec1 path: record pc+4 as well when pc is a branch/jump
            w = self.rw(self.pc)
            op = w >> 26
            if op in (1, 2, 3, 4, 5, 6, 7) or (op == 0 and (w & 63) in (8, 9)):
                self.cov.add(self.pc + 4)
        super().step()

class World:
    def __init__(self):
        m = Cov()
        # re-use emu.make_machine's libc stubs by copying hooks
        mm = emu.make_machine()
        m.hooks.update(mm.hooks)
        self.m = m
        self.j = []
        self.spawn_fail = False
        self.nspawn = 0
        h = m.hooks
        def sound(mc):
            self.j.append(('sound', mc.r[4] & M32))
        def spawn(mc):
            sp = mc.r[29]
            a = (mc.r[4] & M32, mc.r[5] & M32, mc.r[6] & M32, mc.r[7] & M32, mc.rw(sp + 0x10), mc.rw(sp + 0x14), mc.rw(sp + 0x18))
            self.j.append(('spawn',) + a)
            if self.spawn_fail:
                mc.r[2] = 0
            else:
                addr = FAKE_BASE + self.nspawn * 0x300
                self.nspawn += 1
                # zero the fake entity; the real code under test writes into it
                for i in range(0x300): mc.wb(addr + i, 0)
                # a flags word the real code masks (+0x6C): all ones so the &~0x80 is visible
                mc.ww(addr + 0x6C, 0xFFFFFFFF)
                mc.r[2] = addr
        h[0x800490FC] = sound
        h[0x80039F58] = spawn
        m.ww(PLAYER_PTR, PLAYER)

    def set_rng(self, s): self.m.ww(RNG, s & M32)
    def get_rng(self): return self.m.rw(RNG)
    def set_mp(self, cur, mx):
        self.m.wh(PLAYER + 4, cur); self.m.wh(PLAYER + 6, mx)
    def call(self, addr, *args):
        self.m.tracing = True
        try:
            return self.m.call(addr, *args)
        finally:
            self.m.tracing = False
