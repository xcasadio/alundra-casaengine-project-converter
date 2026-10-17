"""mips.py - a minimal MIPS-I interpreter over the ALUN_CD.EXE (France) image, written here (own code, ~100 lines), to RUN real instructions of the
binary (not a hand model): memory = the EXE image (text at 0x80020000 = file offset 0x800) plus an overlay of writes; callees outside the region under
test are stubbed (return value in v0).  Branch delay slots are honoured.  Only the integer subset the tested code uses is implemented; an unknown opcode raises."""
import struct

EXE = r"D:\development\repo\Alundra Remake\Alundra (France)\Alundra (France)_extracted\ALUN_CD.EXE"
IMG = open(EXE, "rb").read()
BASE, OFF = 0x80020000, 0x800
M32 = 0xFFFFFFFF


def s32(v):
    v &= M32
    return v - (1 << 32) if v & 0x80000000 else v


class Mem:
    def __init__(self):
        self.w = {}

    def _img(self, a):
        o = a - BASE + OFF
        return IMG[o] if BASE <= a < BASE + len(IMG) - OFF else 0

    def rb(self, a):
        return self.w[a] if a in self.w else self._img(a)

    def wb(self, a, v):
        self.w[a] = v & 0xFF

    def rh(self, a):
        return self.rb(a) | (self.rb(a + 1) << 8)

    def rw(self, a):
        return self.rh(a) | (self.rh(a + 2) << 16)

    def wh(self, a, v):
        self.wb(a, v); self.wb(a + 1, v >> 8)

    def ww(self, a, v):
        self.wh(a, v); self.wh(a + 2, v >> 16)


class Cpu:
    def __init__(self, mem, stubs=None):
        self.m = mem
        self.r = [0] * 32
        self.hi = self.lo = 0
        self.pc = 0
        self.stubs = stubs or {}
        self.calls = []
        self.steps = 0

    def run(self, pc, stop_ra, max_steps=20000):
        self.pc = pc
        self.r[31] = stop_ra
        while True:
            self.steps += 1
            if self.steps > max_steps:
                raise RuntimeError("step limit at 0x%08x" % self.pc)
            if self.pc == stop_ra:
                return
            self.step()

    def step(self):
        m, r = self.m, self.r
        pc = self.pc
        ins = m.rw(pc)
        op = ins >> 26
        rs, rt, rd = (ins >> 21) & 31, (ins >> 16) & 31, (ins >> 11) & 31
        sh, fn = (ins >> 6) & 31, ins & 63
        imm = ins & 0xFFFF
        simm = imm - 0x10000 if imm & 0x8000 else imm
        nxt = pc + 4
        branch = None          # target taken after the delay slot

        def delay():           # execute the delay-slot instruction at pc+4 without following control flow
            save = self.pc
            self.pc = pc + 4
            self.step_plain()
            self.pc = save

        if op == 0:
            if fn == 8:        # jr
                branch = r[rs]
            elif fn == 9:      # jalr
                branch = r[rs]
                r[rd] = pc + 8
            else:
                self.alu_r(ins)
        elif op == 2:
            branch = ((pc + 4) & 0xF0000000) | ((ins & 0x3FFFFFF) << 2)
        elif op == 3:
            tgt = ((pc + 4) & 0xF0000000) | ((ins & 0x3FFFFFF) << 2)
            self.calls.append(tgt)
            if tgt in self.stubs or "*" in self.stubs:
                delay()
                st = self.stubs[tgt] if tgt in self.stubs else self.stubs["*"]
                r[2] = (st(self) if callable(st) else st) & M32
                self.pc = pc + 8
                return
            r[31] = pc + 8
            branch = tgt
        elif op in (4, 5, 6, 7) or (op == 1):
            if op == 4: taken = r[rs] == r[rt]
            elif op == 5: taken = r[rs] != r[rt]
            elif op == 6: taken = s32(r[rs]) <= 0
            elif op == 7: taken = s32(r[rs]) > 0
            else:
                taken = s32(r[rs]) < 0 if rt == 0 else s32(r[rs]) >= 0     # bltz / bgez
            if taken:
                branch = pc + 4 + (simm << 2)
        else:
            self.alu_i(ins, op, rs, rt, simm, imm)
        r[0] = 0
        if branch is not None:
            # delay slot executes first (values read after the branch condition was evaluated: standard MIPS)
            self.pc = pc + 4
            self.step_plain()
            self.pc = branch & M32
        else:
            self.pc = nxt

    def step_plain(self):
        """execute one non-branching instruction at self.pc (delay slots here are always ALU/load/store)"""
        m, r = self.m, self.r
        ins = m.rw(self.pc)
        op = ins >> 26
        rs, rt = (ins >> 21) & 31, (ins >> 16) & 31
        imm = ins & 0xFFFF
        simm = imm - 0x10000 if imm & 0x8000 else imm
        if op == 0:
            self.alu_r(ins)
        else:
            self.alu_i(ins, op, rs, rt, simm, imm)
        r[0] = 0

    def alu_r(self, ins):
        r = self.r
        rs, rt, rd = (ins >> 21) & 31, (ins >> 16) & 31, (ins >> 11) & 31
        sh, fn = (ins >> 6) & 31, ins & 63
        a, b = r[rs] & M32, r[rt] & M32
        if fn == 0: r[rd] = (b << sh) & M32
        elif fn == 2: r[rd] = b >> sh
        elif fn == 3: r[rd] = s32(b) >> sh & M32
        elif fn == 4: r[rd] = (b << (a & 31)) & M32
        elif fn == 6: r[rd] = b >> (a & 31)
        elif fn == 7: r[rd] = (s32(b) >> (a & 31)) & M32
        elif fn == 0x10: r[rd] = self.hi
        elif fn == 0x12: r[rd] = self.lo
        elif fn == 0x18:
            p = s32(a) * s32(b); self.lo, self.hi = p & M32, (p >> 32) & M32
        elif fn == 0x19:
            p = a * b; self.lo, self.hi = p & M32, (p >> 32) & M32
        elif fn in (0x20, 0x21): r[rd] = (a + b) & M32
        elif fn in (0x22, 0x23): r[rd] = (a - b) & M32
        elif fn == 0x24: r[rd] = a & b
        elif fn == 0x25: r[rd] = a | b
        elif fn == 0x26: r[rd] = a ^ b
        elif fn == 0x27: r[rd] = ~(a | b) & M32
        elif fn == 0x2A: r[rd] = 1 if s32(a) < s32(b) else 0
        elif fn == 0x2B: r[rd] = 1 if a < b else 0
        elif ins == 0:
            pass
        else:
            raise NotImplementedError("R fn 0x%x at 0x%08x" % (fn, self.pc))

    def alu_i(self, ins, op, rs, rt, simm, imm):
        r, m = self.r, self.m
        a = r[rs] & M32
        if op in (8, 9): r[rt] = (a + simm) & M32
        elif op == 0xA: r[rt] = 1 if s32(a) < simm else 0
        elif op == 0xB: r[rt] = 1 if a < (simm & M32) else 0
        elif op == 0xC: r[rt] = a & imm
        elif op == 0xD: r[rt] = a | imm
        elif op == 0xE: r[rt] = a ^ imm
        elif op == 0xF: r[rt] = (imm << 16) & M32
        elif op == 0x20: v = m.rb((a + simm) & M32); r[rt] = (v - 256 if v & 0x80 else v) & M32
        elif op == 0x21: v = m.rh((a + simm) & M32); r[rt] = (v - 65536 if v & 0x8000 else v) & M32
        elif op == 0x23: r[rt] = m.rw((a + simm) & M32)
        elif op == 0x24: r[rt] = m.rb((a + simm) & M32)
        elif op == 0x25: r[rt] = m.rh((a + simm) & M32)
        elif op == 0x28: m.wb((a + simm) & M32, r[rt])
        elif op == 0x29: m.wh((a + simm) & M32, r[rt])
        elif op == 0x2B: m.ww((a + simm) & M32, r[rt])
        else:
            raise NotImplementedError("opcode 0x%x at 0x%08x" % (op, self.pc))
