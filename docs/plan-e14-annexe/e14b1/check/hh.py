"""hh.py - own Ent / Rng helpers for the critic harness (same accessors as the models expect)."""
M32 = 0xFFFFFFFF
STRIDE = 0x294
def s32(x):
    x &= M32
    return x - (1 << 32) if x & 0x80000000 else x
def lcg(s):
    return (s * 0x7D2B89DD + 0xE06A02E7) & M32
class Ent:
    def __init__(self, raw=None):
        self.raw = bytearray(raw if raw is not None else bytes(STRIDE))
    def copy(self): return Ent(self.raw)
    def u32(self, o): return int.from_bytes(self.raw[o:o+4], 'little')
    def s32(self, o): return s32(self.u32(o))
    def set32(self, o, v): self.raw[o:o+4] = (v & M32).to_bytes(4, 'little')
    def u16(self, o): return int.from_bytes(self.raw[o:o+2], 'little')
    def s16(self, o):
        v = self.u16(o); return v - 0x10000 if v & 0x8000 else v
    def set16(self, o, v): self.raw[o:o+2] = (v & 0xFFFF).to_bytes(2, 'little')
    def u8(self, o): return self.raw[o]
    def set8(self, o, v): self.raw[o] = v & 0xFF
class Rng:
    def __init__(self, s): self.s = s & M32; self.draws = []
    def below(self, n):
        self.s = lcg(self.s); r = (self.s * n) >> 32; self.draws.append((n, r)); return r
