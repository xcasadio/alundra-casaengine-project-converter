"""Critic's own MIPS-I interpreter, written from scratch (does not reuse the preparation's emu.py).
Little-endian, PS1 R3000 subset, delay slots honoured (branch-delay and jump-delay).  Memory = sparse byte map over the EXE image.
Read-only use of the EXE (via cx_exe)."""
import struct
import cx_exe as X

M32 = 0xFFFFFFFF


def s32(v):
    v &= M32
    return v - (1 << 32) if v & 0x80000000 else v


def s16(v):
    v &= 0xFFFF
    return v - 0x10000 if v & 0x8000 else v


def s8(v):
    v &= 0xFF
    return v - 0x100 if v & 0x80 else v


class Mem:
    def __init__(self):
        self.w = {}          # addr -> byte, only bytes written by the run
        self.reads = []      # (addr, size) of data reads (not instruction fetches)
        self.writes = []     # (addr, size, value)

    def _rb(self, a):
        a &= M32
        if a in self.w:
            return self.w[a]
        if X.TADDR <= a < X.TADDR + (len(X.D) - 0x800):
            return X.D[0x800 + a - X.TADDR]
        return 0

    def fetch(self, a):
        return self._rb(a) | self._rb(a + 1) << 8 | self._rb(a + 2) << 16 | self._rb(a + 3) << 24

    def rd(self, a, n, signed=False):
        self.reads.append((a & M32, n))
        v = 0
        for i in range(n):
            v |= self._rb(a + i) << (8 * i)
        if signed:
            v = v - (1 << (8 * n)) if v & (1 << (8 * n - 1)) else v
        return v

    def wr(self, a, n, v):
        a &= M32
        v &= (1 << (8 * n)) - 1
        self.writes.append((a, n, v))
        for i in range(n):
            self.w[a + i] = (v >> (8 * i)) & 0xFF

    def poke(self, a, n, v):  # setup write, not logged
        a &= M32
        v &= (1 << (8 * n)) - 1
        for i in range(n):
            self.w[a + i] = (v >> (8 * i)) & 0xFF

    def peek(self, a, n, signed=False):
        v = 0
        for i in range(n):
            v |= self._rb(a + i) << (8 * i)
        if signed:
            v = v - (1 << (8 * n)) if v & (1 << (8 * n - 1)) else v
        return v


class CPU:
    def __init__(self, mem, hooks=None):
        self.m = mem
        self.r = [0] * 32
        self.hi = 0
        self.lo = 0
        self.hooks = hooks or {}   # addr -> python callable(cpu) that behaves as the function (returns to ra)
        self.steps = 0
        self.called = []

    def run(self, entry, args=(), maxsteps=2_000_000):
        STOP = 0xFFFF0000
        self.r = [0] * 32
        for i, a in enumerate(args):
            self.r[4 + i] = a & M32
        self.r[29] = 0x801FFF00
        self.r[31] = STOP
        pc = entry
        steps = 0
        while pc != STOP:
            steps += 1
            if steps > maxsteps:
                raise RuntimeError("step limit")
            if pc in self.hooks:
                self.called.append(pc)
                self.hooks[pc](self)
                pc = self.r[31]
                continue
            pc = self._step(pc)
        self.steps = steps
        return self.r[2]

    def _delay(self, pc):
        # execute the instruction at pc (a delay slot) - non-branching only
        return self._exec(pc, in_delay=True)

    def _step(self, pc):
        return self._exec(pc, in_delay=False)

    def _exec(self, pc, in_delay):
        w = self.m.fetch(pc)
        op = w >> 26
        rs = (w >> 21) & 31
        rt = (w >> 16) & 31
        rd = (w >> 11) & 31
        sh = (w >> 6) & 31
        fn = w & 63
        imm = w & 0xFFFF
        simm = s16(imm)
        r = self.r
        nxt = (pc + 4) & M32
        branch_target = None

        def setr(i, v):
            if i:
                r[i] = v & M32

        if op == 0:
            if fn == 0x00:
                setr(rd, r[rt] << sh)
            elif fn == 0x02:
                setr(rd, r[rt] >> sh)
            elif fn == 0x03:
                setr(rd, s32(r[rt]) >> sh)
            elif fn == 0x04:
                setr(rd, r[rt] << (r[rs] & 31))
            elif fn == 0x06:
                setr(rd, r[rt] >> (r[rs] & 31))
            elif fn == 0x07:
                setr(rd, s32(r[rt]) >> (r[rs] & 31))
            elif fn == 0x08:  # jr
                branch_target = r[rs]
            elif fn == 0x09:  # jalr
                branch_target = r[rs]
                setr(rd, pc + 8)
            elif fn == 0x10:
                setr(rd, self.hi)
            elif fn == 0x12:
                setr(rd, self.lo)
            elif fn == 0x18:
                p = s32(r[rs]) * s32(r[rt])
                self.lo = p & M32
                self.hi = (p >> 32) & M32
            elif fn == 0x19:
                p = r[rs] * r[rt]
                self.lo = p & M32
                self.hi = (p >> 32) & M32
            elif fn in (0x20, 0x21):
                setr(rd, r[rs] + r[rt])
            elif fn in (0x22, 0x23):
                setr(rd, r[rs] - r[rt])
            elif fn == 0x24:
                setr(rd, r[rs] & r[rt])
            elif fn == 0x25:
                setr(rd, r[rs] | r[rt])
            elif fn == 0x26:
                setr(rd, r[rs] ^ r[rt])
            elif fn == 0x27:
                setr(rd, ~(r[rs] | r[rt]))
            elif fn == 0x2A:
                setr(rd, 1 if s32(r[rs]) < s32(r[rt]) else 0)
            elif fn == 0x2B:
                setr(rd, 1 if r[rs] < r[rt] else 0)
            else:
                raise RuntimeError("special fn %02x at %08x" % (fn, pc))
        elif op == 1:
            cond = None
            if rt == 0:
                cond = s32(r[rs]) < 0
            elif rt == 1:
                cond = s32(r[rs]) >= 0
            else:
                raise RuntimeError("regimm %d at %08x" % (rt, pc))
            if cond:
                branch_target = (pc + 4 + (simm << 2)) & M32
            else:
                branch_target = (pc + 8) & M32
        elif op == 2:
            branch_target = ((pc + 4) & 0xF0000000) | ((w & 0x3FFFFFF) << 2)
        elif op == 3:
            branch_target = ((pc + 4) & 0xF0000000) | ((w & 0x3FFFFFF) << 2)
            setr(31, pc + 8)
        elif op in (4, 5, 6, 7):
            if op == 4:
                c = r[rs] == r[rt]
            elif op == 5:
                c = r[rs] != r[rt]
            elif op == 6:
                c = s32(r[rs]) <= 0
            else:
                c = s32(r[rs]) > 0
            branch_target = ((pc + 4 + (simm << 2)) & M32) if c else ((pc + 8) & M32)
        elif op in (8, 9):
            setr(rt, r[rs] + simm)
        elif op == 0x0A:
            setr(rt, 1 if s32(r[rs]) < simm else 0)
        elif op == 0x0B:
            setr(rt, 1 if r[rs] < (simm & M32) else 0)
        elif op == 0x0C:
            setr(rt, r[rs] & imm)
        elif op == 0x0D:
            setr(rt, r[rs] | imm)
        elif op == 0x0E:
            setr(rt, r[rs] ^ imm)
        elif op == 0x0F:
            setr(rt, imm << 16)
        elif op == 0x20:
            setr(rt, self.m.rd(r[rs] + simm, 1, True))
        elif op == 0x21:
            setr(rt, self.m.rd(r[rs] + simm, 2, True))
        elif op == 0x23:
            setr(rt, self.m.rd(r[rs] + simm, 4))
        elif op == 0x24:
            setr(rt, self.m.rd(r[rs] + simm, 1))
        elif op == 0x25:
            setr(rt, self.m.rd(r[rs] + simm, 2))
        elif op == 0x28:
            self.m.wr(r[rs] + simm, 1, r[rt])
        elif op == 0x29:
            self.m.wr(r[rs] + simm, 2, r[rt])
        elif op == 0x2B:
            self.m.wr(r[rs] + simm, 4, r[rt])
        else:
            raise RuntimeError("opcode %02x at %08x" % (op, pc))

        if in_delay:
            return nxt
        if branch_target is not None:
            # delay slot
            self._exec(pc + 4, in_delay=True)
            return branch_target
        return nxt
