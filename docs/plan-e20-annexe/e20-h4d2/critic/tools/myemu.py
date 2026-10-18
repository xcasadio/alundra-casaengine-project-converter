"""myemu.py (critic h4d2) - MY OWN MIPS-I (R3000) interpreter, written from the MIPS architecture manual, not from the harness' emu.py.
It is a drop-in replacement of e20-h4's emu.py (same module-level names: RAM_SIZE, SENTINEL, s32, Emu with rb/rh/rw/wb/wh/ww/rhs/rws/cstr/call/step/exec1/hooks/log/steps,
exec1 returning None | ('j', target) | ('b', target-or-None)) so that machine.Machine (the e20-h4 world harness) runs on it unchanged.
Differences with emu.py by construction (so that a shared bug is unlikely):
  * table-driven decode (dict of handlers by opcode / funct / regimm), numpy-free, memory by bytearray + int.from_bytes / to_bytes;
  * all the integer subset including lwl/lwr/swl/swr, mthi/mtlo, bltzal/bgezal, sllv..., slt/sltu, div/divu (hi = remainder, lo = quotient; divisor 0 raises);
  * LOAD-DELAY AUDIT: HAZARDS records every (pc, reg) where an instruction reads the register loaded by the instruction executed just before it (on the R3000 the new value is NOT yet visible there);
    it is 0 hazards on the code run by the keys iff 'load result visible at once' (this interpreter and the harness') equals the hardware.
"""
import os
import sys
import struct

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import lib

RAM_SIZE = 0x200000
SENTINEL = 0xFFFFFFF0
M = 0xFFFFFFFF
HAZARDS = {}


def s32(x):
    x &= M
    return x - 0x100000000 if x & 0x80000000 else x


def s16(x):
    x &= 0xFFFF
    return x - 0x10000 if x & 0x8000 else x


class Emu:
    def __init__(self):
        self.ram = bytearray(RAM_SIZE)
        img = lib.DATA[lib.OFF:]
        base = lib.BASE & 0x1FFFFFFF
        self.ram[base:base + len(img)] = img
        self.r = [0] * 32
        self.hi = 0
        self.lo = 0
        self.pc = 0
        self.hooks = {}
        self.log = []
        self.steps = 0
        self._ldreg = 0          # destination register of the load executed just before (0 = none)

    # ---- memory (KSEG0/KSEG1 mirrors fold onto the 2 MB RAM) ----
    @staticmethod
    def _a(a):
        return a & 0x1FFFFFFF

    def rb(self, a):
        return self.ram[a & 0x1FFFFFFF]

    def rh(self, a):
        o = a & 0x1FFFFFFF
        return int.from_bytes(self.ram[o:o + 2], 'little')

    def rw(self, a):
        o = a & 0x1FFFFFFF
        return int.from_bytes(self.ram[o:o + 4], 'little')

    def wb(self, a, v):
        self.ram[a & 0x1FFFFFFF] = v & 0xFF

    def wh(self, a, v):
        o = a & 0x1FFFFFFF
        self.ram[o:o + 2] = (v & 0xFFFF).to_bytes(2, 'little')

    def ww(self, a, v):
        o = a & 0x1FFFFFFF
        self.ram[o:o + 4] = (v & M).to_bytes(4, 'little')

    def rhs(self, a):
        return s16(self.rh(a))

    def rws(self, a):
        return s32(self.rw(a))

    def cstr(self, a):
        o = a & 0x1FFFFFFF
        return bytes(self.ram[o:self.ram.index(b'\0', o)])

    # ---- execution drivers (same contract as emu.Emu.call / step) ----
    def call(self, addr, *args, limit=2_000_000):
        saved = list(self.r)
        for i, a in enumerate(args):
            self.r[4 + i] = a & M
        self.r[29] = 0x801FFF00
        self.r[31] = SENTINEL
        self.pc = addr
        self._ldreg = 0
        n = 0
        while self.pc != SENTINEL:
            h = self.hooks.get(self.pc)
            if h is not None:
                h(self)
                self._ldreg = 0
                self.pc = self.r[31]
                continue
            self.step()
            n += 1
            if n > limit:
                raise RuntimeError('step limit at %08x' % self.pc)
        self.steps += n
        v0, v1 = self.r[2], self.r[3]
        self.r = saved
        self.r[2], self.r[3] = v0, v1
        return v0

    def step(self):
        pc = self.pc
        w = self.rw(pc)
        br = self.exec1(pc, w)
        if br is None:
            self.pc = (pc + 4) & M
            return
        self.exec1(pc + 4, self.rw(pc + 4))          # the delay slot (never a branch in compiled code)
        self.pc = br[1] if br[1] is not None else (pc + 8) & M

    # ---- the instruction set ----
    def _audit(self, pc, w, reads):
        l = self._ldreg
        if l and l in reads:
            HAZARDS[(pc, l)] = HAZARDS.get((pc, l), 0) + 1

    def exec1(self, pc, w):
        r = self.r
        op = (w >> 26) & 63
        rs = (w >> 21) & 31
        rt = (w >> 16) & 31
        rd = (w >> 11) & 31
        sa = (w >> 6) & 31
        fn = w & 63
        im = w & 0xFFFF
        si = s16(im)
        # ---- load-delay audit: which registers does this instruction read? ----
        if self._ldreg:
            if op == 0:
                if fn in (0, 2, 3):
                    reads = (rt,)
                elif fn in (8, 9, 0x11, 0x13):
                    reads = (rs,)
                elif fn in (0x10, 0x12, 0x0C, 0x0D):
                    reads = ()
                else:
                    reads = (rs, rt)
            elif op in (2, 3):
                reads = ()
            elif op == 1:
                reads = (rs,)
            elif op in (4, 5):
                reads = (rs, rt)
            elif op in (6, 7):
                reads = (rs,)
            elif op == 0xF:
                reads = ()
            elif op in (0x28, 0x29, 0x2A, 0x2B, 0x2E, 0x22, 0x26):
                reads = (rs, rt)
            else:
                reads = (rs,)
            self._audit(pc, w, reads)
        self._ldreg = 0
        a = r[rs]
        b = r[rt]

        def put(i, v):
            if i:
                r[i] = v & M

        if op == 0:
            if fn == 0x00: put(rd, b << sa)
            elif fn == 0x02: put(rd, b >> sa)
            elif fn == 0x03: put(rd, s32(b) >> sa)
            elif fn == 0x04: put(rd, b << (a & 31))
            elif fn == 0x06: put(rd, b >> (a & 31))
            elif fn == 0x07: put(rd, s32(b) >> (a & 31))
            elif fn == 0x08: return ('j', a)
            elif fn == 0x09:
                t = a
                put(rd, pc + 8)
                return ('j', t)
            elif fn == 0x0C or fn == 0x0D: raise RuntimeError('syscall/break at %08x' % pc)
            elif fn == 0x10: put(rd, self.hi)
            elif fn == 0x11: self.hi = a
            elif fn == 0x12: put(rd, self.lo)
            elif fn == 0x13: self.lo = a
            elif fn == 0x18:
                p = s32(a) * s32(b)
                self.lo, self.hi = p & M, (p >> 32) & M
            elif fn == 0x19:
                p = a * b
                self.lo, self.hi = p & M, (p >> 32) & M
            elif fn == 0x1A:
                x, y = s32(a), s32(b)
                if y == 0: raise RuntimeError('div by zero at %08x' % pc)
                q = abs(x) // abs(y)
                if (x < 0) != (y < 0): q = -q
                self.lo, self.hi = q & M, (x - q * y) & M
            elif fn == 0x1B:
                if b == 0: raise RuntimeError('divu by zero at %08x' % pc)
                self.lo, self.hi = (a // b) & M, a % b
            elif fn in (0x20, 0x21): put(rd, a + b)
            elif fn in (0x22, 0x23): put(rd, a - b)
            elif fn == 0x24: put(rd, a & b)
            elif fn == 0x25: put(rd, a | b)
            elif fn == 0x26: put(rd, a ^ b)
            elif fn == 0x27: put(rd, ~(a | b))
            elif fn == 0x2A: put(rd, 1 if s32(a) < s32(b) else 0)
            elif fn == 0x2B: put(rd, 1 if a < b else 0)
            else: raise RuntimeError('SPECIAL funct %02x at %08x' % (fn, pc))
            return None
        if op == 1:
            neg = s32(a) < 0
            cond = {0: neg, 1: not neg, 0x10: neg, 0x11: not neg}.get(rt)
            if cond is None: raise RuntimeError('REGIMM %d at %08x' % (rt, pc))
            if rt >= 0x10: put(31, pc + 8)
            return ('b', (pc + 4 + (si << 2)) & M) if cond else ('b', None)
        if op == 2: return ('j', ((pc + 4) & 0xF0000000) | ((w & 0x3FFFFFF) << 2))
        if op == 3:
            put(31, pc + 8)
            return ('j', ((pc + 4) & 0xF0000000) | ((w & 0x3FFFFFF) << 2))
        if op in (4, 5, 6, 7):
            c = {4: a == b, 5: a != b, 6: s32(a) <= 0, 7: s32(a) > 0}[op]
            return ('b', (pc + 4 + (si << 2)) & M) if c else ('b', None)
        # I-type ALU / memory
        if op in (8, 9): put(rt, a + si)
        elif op == 0xA: put(rt, 1 if s32(a) < si else 0)
        elif op == 0xB: put(rt, 1 if a < (si & M) else 0)
        elif op == 0xC: put(rt, a & im)
        elif op == 0xD: put(rt, a | im)
        elif op == 0xE: put(rt, a ^ im)
        elif op == 0xF: put(rt, im << 16)
        elif op in (0x20, 0x21, 0x23, 0x24, 0x25, 0x22, 0x26):
            ea = (a + si) & M
            if op == 0x20: v = self.rb(ea); v = v - 256 if v & 0x80 else v
            elif op == 0x21: v = s16(self.rh(ea))
            elif op == 0x23: v = self.rw(ea)
            elif op == 0x24: v = self.rb(ea)
            elif op == 0x25: v = self.rh(ea)
            elif op == 0x22:      # lwl (little endian): bytes ea..(ea&~3)+... go to the high end of rt
                k = ea & 3
                word = self.rw(ea & ~3)
                sh = 24 - 8 * k
                v = ((word << sh) & M) | (b & ((1 << sh) - 1))
            else:                 # lwr
                k = ea & 3
                word = self.rw(ea & ~3)
                sh = 8 * k
                v = (word >> sh) | (b & (M ^ (M >> sh)))
            put(rt, v)
            self._ldreg = rt
        elif op in (0x28, 0x29, 0x2B, 0x2A, 0x2E):
            ea = (a + si) & M
            if op == 0x28: self.wb(ea, b)
            elif op == 0x29: self.wh(ea, b)
            elif op == 0x2B: self.ww(ea, b)
            elif op == 0x2A:      # swl
                k = ea & 3
                word = self.rw(ea & ~3)
                sh = 24 - 8 * k
                self.ww(ea & ~3, (word & (M ^ (M >> sh))) | (b >> sh))
            else:                 # swr
                k = ea & 3
                word = self.rw(ea & ~3)
                sh = 8 * k
                self.ww(ea & ~3, (word & ((1 << sh) - 1)) | ((b << sh) & M))
        else:
            raise RuntimeError('opcode %02x at %08x' % (op, pc))
        return None
