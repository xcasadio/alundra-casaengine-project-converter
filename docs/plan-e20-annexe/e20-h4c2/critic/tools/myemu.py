"""myemu.py - my own minimal MIPS-I interpreter over the text segment of ALUN_CD.EXE (France).
Independent of h4 machine.py/mips.py and of h4b cmips.py: written from the MIPS-I manual by the h4c2 critic.
Memory: writes go to an overlay dict (sparse bytes); reads fall back to the EXE image (text/data) then 0.
run(addr, args) runs until the jr $ra to a sentinel. Hooks: addr -> python fn(emu) replacing a JAL target (return via $ra)."""
import struct
from mydis import DATA, TADDR, off

M32 = 0xFFFFFFFF
SENTINEL = 0xDEAD0000

def s32(x):
    x &= M32
    return x - (1 << 32) if x & 0x80000000 else x

class Emu:
    def __init__(self):
        self.mem = {}
        self.r = [0] * 32
        self.hi = 0
        self.lo = 0
        self.hooks = {}
        self.cov = set()
        self.log = []

    def rb(self, a):
        a &= M32
        if a in self.mem:
            return self.mem[a]
        o = 0x800 + (a - TADDR)
        if a >= TADDR and o < len(DATA):
            return DATA[o]
        return 0
    def wb(self, a, v):
        self.mem[a & M32] = v & 0xFF
    def rw(self, a):
        a &= M32
        assert a & 3 == 0, hex(a)
        return self.rb(a) | self.rb(a+1) << 8 | self.rb(a+2) << 16 | self.rb(a+3) << 24
    def ww(self, a, v):
        a &= M32
        assert a & 3 == 0, hex(a)
        self.log.append((a, 4, v & M32))
        for i in range(4):
            self.wb(a+i, (v >> (8*i)) & 0xFF)
    def rh(self, a):
        a &= M32
        assert a & 1 == 0, hex(a)
        return self.rb(a) | self.rb(a+1) << 8
    def wh(self, a, v):
        a &= M32
        self.log.append((a, 2, v & 0xFFFF))
        self.wb(a, v); self.wb(a+1, v >> 8)
    def poke(self, a, v): self.ww(a, v)
    def peek(self, a): return self.rw(a)
    def peeks(self, a): return s32(self.rw(a))

    def run(self, addr, args=(), limit=2_000_000, stack=0x801FFE00, stop_at=None):
        self.r = [0] * 32
        for i, v in enumerate(args[:4]):
            self.r[4+i] = v & M32
        self.r[29] = stack
        self.r[31] = SENTINEL
        pc = addr
        steps = 0
        first = True
        while pc != SENTINEL:
            if stop_at is not None and pc == stop_at and not first:
                break
            if (not first) and pc in self.hooks:
                self.hooks[pc](self)
                pc = self.r[31] & M32
                continue
            first = False
            w = self.rw(pc)
            self.cov.add(pc)
            tgt = self.step(w, pc)
            steps += 1
            if steps > limit:
                raise RuntimeError("step limit")
            if tgt is None:
                pc += 4
            else:
                dpc = pc + 4
                dw = self.rw(dpc)
                self.cov.add(dpc)
                t2 = self.step(dw, dpc)
                assert t2 is None, "branch in delay slot"
                steps += 1
                pc = tgt
        return self.r[2]

    def step(self, w, pc):
        r = self.r
        op = w >> 26
        rs = (w >> 21) & 31; rt = (w >> 16) & 31; rd = (w >> 11) & 31; sh = (w >> 6) & 31
        fn = w & 63
        imm = w & 0xFFFF
        simm = imm - 0x10000 if imm & 0x8000 else imm
        def setr(i, v):
            if i: r[i] = v & M32
        if op == 0:
            if fn == 0: setr(rd, r[rt] << sh)
            elif fn == 2: setr(rd, r[rt] >> sh)
            elif fn == 3: setr(rd, s32(r[rt]) >> sh)
            elif fn == 4: setr(rd, r[rt] << (r[rs] & 31))
            elif fn == 6: setr(rd, r[rt] >> (r[rs] & 31))
            elif fn == 7: setr(rd, s32(r[rt]) >> (r[rs] & 31))
            elif fn == 8: return r[rs] & M32
            elif fn == 9:
                t = r[rs] & M32; setr(rd, pc + 8); return t
            elif fn == 0x10: setr(rd, self.hi)
            elif fn == 0x12: setr(rd, self.lo)
            elif fn == 0x11: self.hi = r[rs]
            elif fn == 0x13: self.lo = r[rs]
            elif fn == 0x18:
                p = s32(r[rs]) * s32(r[rt]); self.lo = p & M32; self.hi = (p >> 32) & M32
            elif fn == 0x19:
                p = (r[rs] & M32) * (r[rt] & M32); self.lo = p & M32; self.hi = (p >> 32) & M32
            elif fn == 0x1A:
                a = s32(r[rs]); b = s32(r[rt])
                if b == 0: self.lo = (-1 if a >= 0 else 1) & M32; self.hi = a & M32
                else:
                    q = abs(a)//abs(b); q = q if (a >= 0) == (b >= 0) else -q
                    self.lo = q & M32; self.hi = (a - q*b) & M32
            elif fn == 0x1B:
                a = r[rs]; b = r[rt]
                if b == 0: self.lo = M32; self.hi = a
                else: self.lo = a // b; self.hi = a % b
            elif fn in (0x20, 0x21): setr(rd, r[rs] + r[rt])
            elif fn in (0x22, 0x23): setr(rd, r[rs] - r[rt])
            elif fn == 0x24: setr(rd, r[rs] & r[rt])
            elif fn == 0x25: setr(rd, r[rs] | r[rt])
            elif fn == 0x26: setr(rd, r[rs] ^ r[rt])
            elif fn == 0x27: setr(rd, ~(r[rs] | r[rt]))
            elif fn == 0x2A: setr(rd, 1 if s32(r[rs]) < s32(r[rt]) else 0)
            elif fn == 0x2B: setr(rd, 1 if (r[rs] & M32) < (r[rt] & M32) else 0)
            else: raise NotImplementedError(f"special fn {fn:#x} at {pc:#x}")
            return None
        if op == 1:
            v = s32(r[rs])
            if rt == 0: take = v < 0
            elif rt == 1: take = v >= 0
            elif rt == 0x10: setr(31, pc + 8); take = v < 0
            elif rt == 0x11: setr(31, pc + 8); take = v >= 0
            else: raise NotImplementedError(f"regimm {rt}")
            return (pc + 4 + (simm << 2)) & M32 if take else None
        if op == 2: return ((pc + 4) & 0xF0000000) | ((w & 0x3FFFFFF) << 2)
        if op == 3:
            setr(31, pc + 8); return ((pc + 4) & 0xF0000000) | ((w & 0x3FFFFFF) << 2)
        if op in (4, 5, 6, 7):
            a = s32(r[rs]); b = s32(r[rt])
            take = {4: a == b, 5: a != b, 6: a <= 0, 7: a > 0}[op]
            return (pc + 4 + (simm << 2)) & M32 if take else None
        if op in (8, 9): setr(rt, r[rs] + simm)
        elif op == 0xA: setr(rt, 1 if s32(r[rs]) < simm else 0)
        elif op == 0xB: setr(rt, 1 if (r[rs] & M32) < (simm & M32) else 0)
        elif op == 0xC: setr(rt, r[rs] & imm)
        elif op == 0xD: setr(rt, r[rs] | imm)
        elif op == 0xE: setr(rt, r[rs] ^ imm)
        elif op == 0xF: setr(rt, imm << 16)
        elif op == 0x20:
            v = self.rb(r[rs] + simm); setr(rt, v - 256 if v & 0x80 else v)
        elif op == 0x21:
            v = self.rh(r[rs] + simm); setr(rt, v - 65536 if v & 0x8000 else v)
        elif op == 0x23: setr(rt, self.rw(r[rs] + simm))
        elif op == 0x24: setr(rt, self.rb(r[rs] + simm))
        elif op == 0x25: setr(rt, self.rh(r[rs] + simm))
        elif op == 0x28:
            self.log.append(((r[rs]+simm) & M32, 1, r[rt] & 0xFF)); self.wb(r[rs] + simm, r[rt])
        elif op == 0x29: self.wh(r[rs] + simm, r[rt])
        elif op == 0x2B: self.ww(r[rs] + simm, r[rt])
        else: raise NotImplementedError(f"op {op:#x} at {pc:#x}")
        return None
