import sys, os, struct
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from lib import D, off, BASE, tsize
M32 = 0xffffffff
def s32(x): x &= M32; return x - (1 << 32) if x & 0x80000000 else x
def s16(x): x &= 0xffff; return x - 0x10000 if x & 0x8000 else x

class Mem:
    def __init__(self):
        self.b = {}   # sparse overlay of bytes
    def r8(self, a):
        a &= M32
        if a in self.b: return self.b[a]
        if BASE <= a < BASE + tsize: return D[off(a)]
        return 0
    def w8(self, a, v): self.b[a & M32] = v & 0xff
    def r16(self, a): return self.r8(a) | (self.r8(a + 1) << 8)
    def r32(self, a): return self.r16(a) | (self.r16(a + 2) << 16)
    def w16(self, a, v): self.w8(a, v); self.w8(a + 1, v >> 8)
    def w32(self, a, v): self.w16(a, v); self.w16(a + 2, v >> 16)

class CPU:
    def __init__(self, mem, stubs=None):
        self.m = mem; self.r = [0] * 32; self.hi = 0; self.lo = 0
        self.stubs = stubs or {}   # addr -> fn(cpu) returning v0 (or None)
        self.log = []; self.steps = 0; self.covered = set()
    def call(self, addr, args=(), maxsteps=2_000_000):
        r = self.r
        for i, a in enumerate(args[:4]): r[4 + i] = a & M32
        sp0 = 0x801ffff0
        r[29] = sp0 - 0x100
        # args beyond 4 on stack
        for i, a in enumerate(args[4:]): self.m.w32(r[29] + 0x10 + 4 * i, a)
        RET = 0xdeadbee0
        r[31] = RET
        pc = addr; self.steps = 0
        while True:
            if pc == RET: break
            if pc in self.stubs:
                v = self.stubs[pc](self)
                if v is not None: r[2] = v & M32
                pc = r[31]; continue
            pc = self.step(pc)
            self.steps += 1
            if self.steps > maxsteps: raise RuntimeError('too many steps')
        return r[2]
    def step(self, pc):
        r = self.r; m = self.m
        w = m.r32(pc); self.covered.add(pc)
        npc = (pc + 4) & M32
        # execute delay slot after computing branch
        op = w >> 26; rs = (w >> 21) & 31; rt = (w >> 16) & 31; rd = (w >> 11) & 31; sh = (w >> 6) & 31; fn = w & 63
        imm = w & 0xffff; simm = s16(imm); tgt = (w & 0x3ffffff)
        branch = None
        def ds():
            # execute delay slot instruction at pc+4 (non-branch)
            self.covered.add(pc + 4)
            self.exec1(pc + 4)
        if op == 0:
            if fn == 8:   # jr
                branch = r[rs]; ds(); return branch
            if fn == 9:   # jalr
                branch = r[rs]; r[rd] = (pc + 8) & M32; ds(); return branch
        elif op == 2:
            ds(); return ((pc + 4) & 0xf0000000) | (tgt << 2)
        elif op == 3:
            r[31] = (pc + 8) & M32; ds(); return ((pc + 4) & 0xf0000000) | (tgt << 2)
        elif op in (4, 5, 6, 7, 1):
            a = s32(r[rs]); b = s32(r[rt])
            if op == 4: c = a == b
            elif op == 5: c = a != b
            elif op == 6: c = a <= 0
            elif op == 7: c = a > 0
            else:
                if rt == 0: c = a < 0
                elif rt == 1: c = a >= 0
                elif rt == 0x10: c = a < 0; r[31] = (pc + 8) & M32
                elif rt == 0x11: c = a >= 0; r[31] = (pc + 8) & M32
                else: raise RuntimeError('regimm %x' % rt)
            ds()
            return ((pc + 4 + (simm << 2)) & M32) if c else (pc + 8) & M32
        self.exec1(pc)
        return npc
    def exec1(self, pc):
        r = self.r; m = self.m
        w = m.r32(pc)
        op = w >> 26; rs = (w >> 21) & 31; rt = (w >> 16) & 31; rd = (w >> 11) & 31; sh = (w >> 6) & 31; fn = w & 63
        imm = w & 0xffff; simm = s16(imm)
        def setr(i, v):
            if i: r[i] = v & M32
        if w == 0: return
        if op == 0:
            if fn == 0: setr(rd, r[rt] << sh)
            elif fn == 2: setr(rd, r[rt] >> sh)
            elif fn == 3: setr(rd, s32(r[rt]) >> sh)
            elif fn == 4: setr(rd, r[rt] << (r[rs] & 31))
            elif fn == 6: setr(rd, r[rt] >> (r[rs] & 31))
            elif fn == 7: setr(rd, s32(r[rt]) >> (r[rs] & 31))
            elif fn == 0x10: setr(rd, self.hi)
            elif fn == 0x12: setr(rd, self.lo)
            elif fn == 0x18:
                p = s32(r[rs]) * s32(r[rt]); self.lo = p & M32; self.hi = (p >> 32) & M32
            elif fn == 0x19:
                p = r[rs] * r[rt]; self.lo = p & M32; self.hi = (p >> 32) & M32
            elif fn == 0x1a:
                a, b = s32(r[rs]), s32(r[rt]);
                if b: 
                    q = int(a / b); self.lo = q & M32; self.hi = (a - q * b) & M32
            elif fn == 0x1b:
                a, b = r[rs], r[rt]
                if b: self.lo = a // b; self.hi = a % b
            elif fn in (0x20, 0x21): setr(rd, r[rs] + r[rt])
            elif fn in (0x22, 0x23): setr(rd, r[rs] - r[rt])
            elif fn == 0x24: setr(rd, r[rs] & r[rt])
            elif fn == 0x25: setr(rd, r[rs] | r[rt])
            elif fn == 0x26: setr(rd, r[rs] ^ r[rt])
            elif fn == 0x27: setr(rd, ~(r[rs] | r[rt]))
            elif fn == 0x2a: setr(rd, 1 if s32(r[rs]) < s32(r[rt]) else 0)
            elif fn == 0x2b: setr(rd, 1 if r[rs] < r[rt] else 0)
            else: raise RuntimeError('special %x at %08x' % (fn, pc))
        elif op in (8, 9): setr(rt, r[rs] + simm)
        elif op == 10: setr(rt, 1 if s32(r[rs]) < simm else 0)
        elif op == 11: setr(rt, 1 if r[rs] < (simm & M32) else 0)
        elif op == 12: setr(rt, r[rs] & imm)
        elif op == 13: setr(rt, r[rs] | imm)
        elif op == 14: setr(rt, r[rs] ^ imm)
        elif op == 15: setr(rt, imm << 16)
        elif op == 0x20: setr(rt, s16(m.r8((r[rs] + simm) & M32)) if False else (m.r8(r[rs] + simm) - 256 if m.r8(r[rs] + simm) & 0x80 else m.r8(r[rs] + simm)))
        elif op == 0x21: setr(rt, s16(m.r16(r[rs] + simm)))
        elif op == 0x23: setr(rt, m.r32(r[rs] + simm))
        elif op == 0x24: setr(rt, m.r8(r[rs] + simm))
        elif op == 0x25: setr(rt, m.r16(r[rs] + simm))
        elif op == 0x28: m.w8(r[rs] + simm, r[rt])
        elif op == 0x29: m.w16(r[rs] + simm, r[rt])
        elif op == 0x2b: m.w32(r[rs] + simm, r[rt])
        else: raise RuntimeError('op %x at %08x' % (op, pc))
