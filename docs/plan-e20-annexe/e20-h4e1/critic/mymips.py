"""mymips.py - my OWN minimal MIPS R3000 interpreter (no code shared with the h4e1 harness), executing the real ALUN_CD.EXE (France) text.
Memory: a sparse byte overlay; instruction fetch and any read of a byte never written come from the EXE image text (0x80020000..) or, outside it,
are reported as an uninitialised read (strict mode: raises Incomplete)."""
import mylib as L

M32 = 0xFFFFFFFF


class Incomplete(Exception):
    pass


def s32(x):
    x &= M32
    return x - (1 << 32) if x & 0x80000000 else x


def s16(x):
    return x - 0x10000 if x & 0x8000 else x


class Mem:
    def __init__(self, strict=True):
        self.b = {}                 # address -> byte (overlay)
        self.strict = strict
        self.written = set()        # addresses written during the run
        self.reads = []             # (addr,size,value) of data reads whose bytes were not written during the run
        self.logw = []              # (addr,size,value) in order

    def _rb(self, a):
        a &= M32
        if a in self.b:
            return self.b[a]
        if L.TBASE <= a < L.TBASE + L.TSIZE:
            return L.TEXT[a - L.TBASE]
        if self.strict:
            raise Incomplete('read of unlisted byte %#010x' % a)
        return 0

    def rd(self, a, n, fetch=False):
        a &= M32
        v = 0
        for k in range(n):
            v |= self._rb(a + k) << (8 * k)
        if not fetch:
            if not all((a + k) in self.written for k in range(n)):
                self.reads.append((a, n, v))
        return v

    def wr(self, a, n, v):
        a &= M32
        for k in range(n):
            self.b[a + k] = (v >> (8 * k)) & 0xFF
            self.written.add(a + k)
        self.logw.append((a, n, v & ((1 << (8 * n)) - 1)))

    def poke(self, a, n, v):
        """initial image (not counted as a run write)"""
        a &= M32
        for k in range(n):
            self.b[a + k] = (v >> (8 * k)) & 0xFF


class CPU:
    def __init__(self, mem, hooks=None):
        self.m = mem
        self.r = [0] * 32
        self.hi = 0
        self.lo = 0
        self.pc = 0
        self.hooks = hooks or {}
        self.steps = 0
        self.executed = set()

    def run(self, start, sp=0x801FFF00, args=(), maxsteps=3_000_000):
        SENT = 0xDEAD0000
        self.r = [0] * 32
        self.r[29] = sp
        self.r[31] = SENT
        for i, a in enumerate(args):
            self.r[4 + i] = a & M32
        self.pc = start
        while self.pc != SENT:
            self.step()
            self.steps += 1
            if self.steps > maxsteps:
                raise RuntimeError('too many steps')
        return self.r[2]

    def _exec(self, w):
        """execute one non-control instruction"""
        r = self.r
        m = self.m
        op = w >> 26
        rs = (w >> 21) & 31
        rt = (w >> 16) & 31
        rd = (w >> 11) & 31
        sh = (w >> 6) & 31
        fn = w & 63
        imm = w & 0xFFFF
        simm = s16(imm)
        if op == 0:
            if fn == 0:
                r[rd] = (r[rt] << sh) & M32
            elif fn == 2:
                r[rd] = (r[rt] & M32) >> sh
            elif fn == 3:
                r[rd] = (s32(r[rt]) >> sh) & M32
            elif fn == 4:
                r[rd] = (r[rt] << (r[rs] & 31)) & M32
            elif fn == 6:
                r[rd] = (r[rt] & M32) >> (r[rs] & 31)
            elif fn == 7:
                r[rd] = (s32(r[rt]) >> (r[rs] & 31)) & M32
            elif fn == 0x10:
                r[rd] = self.hi & M32
            elif fn == 0x11:
                self.hi = r[rs]
            elif fn == 0x12:
                r[rd] = self.lo & M32
            elif fn == 0x13:
                self.lo = r[rs]
            elif fn == 0x18:
                p = s32(r[rs]) * s32(r[rt])
                self.lo = p & M32
                self.hi = (p >> 32) & M32
            elif fn == 0x19:
                p = (r[rs] & M32) * (r[rt] & M32)
                self.lo = p & M32
                self.hi = (p >> 32) & M32
            elif fn in (0x20, 0x21):
                r[rd] = (r[rs] + r[rt]) & M32
            elif fn in (0x22, 0x23):
                r[rd] = (r[rs] - r[rt]) & M32
            elif fn == 0x24:
                r[rd] = r[rs] & r[rt]
            elif fn == 0x25:
                r[rd] = r[rs] | r[rt]
            elif fn == 0x26:
                r[rd] = r[rs] ^ r[rt]
            elif fn == 0x27:
                r[rd] = ~(r[rs] | r[rt]) & M32
            elif fn == 0x2A:
                r[rd] = 1 if s32(r[rs]) < s32(r[rt]) else 0
            elif fn == 0x2B:
                r[rd] = 1 if (r[rs] & M32) < (r[rt] & M32) else 0
            else:
                raise NotImplementedError('special fn %#x at %#x' % (fn, self.pc))
        elif op in (8, 9):
            r[rt] = (r[rs] + simm) & M32
        elif op == 0xA:
            r[rt] = 1 if s32(r[rs]) < simm else 0
        elif op == 0xB:
            r[rt] = 1 if (r[rs] & M32) < (simm & M32) else 0
        elif op == 0xC:
            r[rt] = r[rs] & imm
        elif op == 0xD:
            r[rt] = r[rs] | imm
        elif op == 0xE:
            r[rt] = r[rs] ^ imm
        elif op == 0xF:
            r[rt] = (imm << 16) & M32
        elif op == 0x20:
            v = m.rd(r[rs] + simm, 1)
            r[rt] = (v - 0x100 if v & 0x80 else v) & M32
        elif op == 0x21:
            v = m.rd(r[rs] + simm, 2)
            r[rt] = (v - 0x10000 if v & 0x8000 else v) & M32
        elif op == 0x23:
            r[rt] = m.rd(r[rs] + simm, 4)
        elif op == 0x24:
            r[rt] = m.rd(r[rs] + simm, 1)
        elif op == 0x25:
            r[rt] = m.rd(r[rs] + simm, 2)
        elif op == 0x28:
            m.wr(r[rs] + simm, 1, r[rt])
        elif op == 0x29:
            m.wr(r[rs] + simm, 2, r[rt])
        elif op == 0x2B:
            m.wr(r[rs] + simm, 4, r[rt])
        else:
            raise NotImplementedError('op %#x at %#x (w=%08x)' % (op, self.pc, w))
        r[0] = 0

    def step(self):
        pc = self.pc
        if pc in self.hooks:
            self.hooks[pc](self)
            self.pc = self.r[31]
            return
        m = self.m
        w = m.rd(pc, 4, fetch=True)
        self.executed.add(pc)
        op = w >> 26
        rs = (w >> 21) & 31
        rt = (w >> 16) & 31
        simm = s16(w & 0xFFFF)
        fn = w & 63
        r = self.r
        is_jr = (op == 0 and fn in (8, 9))
        if not (is_jr or op in (1, 2, 3, 4, 5, 6, 7)):
            self._exec(w)
            self.pc = pc + 4
            return
        target = None
        link = None
        if is_jr:
            target = r[rs] & M32
            if fn == 9:
                link = ((w >> 11) & 31, pc + 8)
        elif op in (2, 3):
            target = ((pc + 4) & 0xF0000000) | ((w & 0x3FFFFFF) << 2)
            if op == 3:
                link = (31, pc + 8)
        else:
            a = s32(r[rs])
            if op == 4:
                t = (r[rs] == r[rt])
            elif op == 5:
                t = (r[rs] != r[rt])
            elif op == 6:
                t = a <= 0
            elif op == 7:
                t = a > 0
            else:
                if rt == 0:
                    t = a < 0
                elif rt == 1:
                    t = a >= 0
                elif rt == 0x10:
                    t = a < 0
                    link = (31, pc + 8)
                elif rt == 0x11:
                    t = a >= 0
                    link = (31, pc + 8)
                else:
                    raise NotImplementedError('regimm %#x' % rt)
            target = ((pc + 4 + (simm << 2)) & M32) if t else None
        if link is not None:
            r[link[0]] = link[1] & M32
        dw = m.rd(pc + 4, 4, fetch=True)
        self.executed.add(pc + 4)
        self.pc = pc + 4
        self._exec(dw)
        self.pc = target if target is not None else pc + 8
