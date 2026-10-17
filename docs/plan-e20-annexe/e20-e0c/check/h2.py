"""own harness (critic). Real code of ALUN_CD.EXE in the copied MIPS interpreter. Stubs journalled."""
import os, sys, random
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, 'emu'))
import emu
M32 = 0xFFFFFFFF
RNG = 0x80098708
ENT0 = 0x80127D30
STRIDE = 0x294
ITEMTAB = 0x800C5F7C
RANDTAB = 0x80028E0C
def s32(x):
    x &= M32
    return x - (1 << 32) if x & 0x80000000 else x
def lcg(s): return (s * 0x7D2B89DD + 0xE06A02E7) & M32
class Cov(emu.Emu):
    def __init__(self):
        super().__init__(); self.cov = set(); self.tracing = False
    def step(self):
        if self.tracing:
            self.cov.add(self.pc)
            w = self.rw(self.pc); op = w >> 26
            if op in (1,2,3,4,5,6,7) or (op == 0 and (w & 63) in (8, 9)): self.cov.add(self.pc + 4)
        super().step()
def mk():
    m = Cov(); mm = emu.make_machine(); m.hooks.update(mm.hooks); return m
