"""Critic's own minimal MIPS-I interpreter (written from scratch, NOT derived from the preparation's emu.py).
Runs real handlers of ALUN_CD.EXE (France). Decoding is done by hand from the instruction word (no capstone)."""
import struct

EXE = r"D:\development\repo\Alundra Remake\Alundra (France)\Alundra (France)_extracted\ALUN_CD.EXE"
BASE = 0x80020000
HDR = 0x800
M = 0xFFFFFFFF


def s32(x):
    x &= M
    return x - (1 << 32) if x & 0x80000000 else x


def s16(x):
    x &= 0xFFFF
    return x - 0x10000 if x & 0x8000 else x


def s8(x):
    x &= 0xFF
    return x - 0x100 if x & 0x80 else x


class Machine:
    LO = 0x80000000
    SIZE = 0x00200000  # 0x80000000..0x80200000

    def __init__(self):
        raw = open(EXE, "rb").read()
        self.mem = bytearray(self.SIZE)
        img = raw[HDR:]
        off = BASE - self.LO
        self.mem[off:off + len(img)] = img
        self.r = [0] * 32
        self.pc = 0
        self.stores = []  # (addr, size, value)
        self.hooks = {}   # addr -> python callable(m) (called instead of executing the function)
        self.steps = 0
        self.jal_ret = {}
        self.search_returns = []

    # memory
    def a(self, addr):
        o = (addr & M) - self.LO
        if not (0 <= o < self.SIZE):
            raise RuntimeError("address out of window %08x (pc %08x)" % (addr & M, self.pc))
        return o

    def rb(self, addr):
        return self.mem[self.a(addr)]

    def rh(self, addr):
        return struct.unpack_from("<H", self.mem, self.a(addr))[0]

    def rw(self, addr):
        return struct.unpack_from("<I", self.mem, self.a(addr))[0]

    def wb(self, addr, v, log=True):
        self.mem[self.a(addr)] = v & 0xFF
        if log:
            self.stores.append((addr & M, 1, v & 0xFF))

    def wh(self, addr, v, log=True):
        struct.pack_into("<H", self.mem, self.a(addr), v & 0xFFFF)
        if log:
            self.stores.append((addr & M, 2, v & 0xFFFF))

    def ww(self, addr, v, log=True):
        struct.pack_into("<I", self.mem, self.a(addr), v & M)
        if log:
            self.stores.append((addr & M, 4, v & M))

    # execution
    def step_instr(self, w, pc):
        """Executes one instruction word. Returns ('next') or ('branch', target) or ('jump', target)."""
        op = w >> 26
        rs = (w >> 21) & 31
        rt = (w >> 16) & 31
        rd = (w >> 11) & 31
        sh = (w >> 6) & 31
        fn = w & 63
        imm = w & 0xFFFF
        simm = s16(imm)
        r = self.r
        if op == 0:
            if fn == 0:
                r[rd] = (r[rt] << sh) & M
            elif fn == 2:
                r[rd] = (r[rt] & M) >> sh
            elif fn == 3:
                r[rd] = (s32(r[rt]) >> sh) & M
            elif fn == 4:
                r[rd] = (r[rt] << (r[rs] & 31)) & M
            elif fn == 6:
                r[rd] = (r[rt] & M) >> (r[rs] & 31)
            elif fn == 7:
                r[rd] = (s32(r[rt]) >> (r[rs] & 31)) & M
            elif fn == 8:
                return ('jump', r[rs] & M)
            elif fn == 9:
                t = r[rs] & M
                r[rd] = (pc + 8) & M
                return ('jump', t)
            elif fn in (0x20, 0x21):
                r[rd] = (r[rs] + r[rt]) & M
            elif fn in (0x22, 0x23):
                r[rd] = (r[rs] - r[rt]) & M
            elif fn == 0x24:
                r[rd] = r[rs] & r[rt]
            elif fn == 0x25:
                r[rd] = r[rs] | r[rt]
            elif fn == 0x26:
                r[rd] = r[rs] ^ r[rt]
            elif fn == 0x27:
                r[rd] = ~(r[rs] | r[rt]) & M
            elif fn == 0x2A:
                r[rd] = 1 if s32(r[rs]) < s32(r[rt]) else 0
            elif fn == 0x2B:
                r[rd] = 1 if (r[rs] & M) < (r[rt] & M) else 0
            else:
                raise RuntimeError("unhandled special fn %02x at %08x" % (fn, pc))
        elif op == 1:
            cond = rt
            v = s32(r[rs])
            if cond == 0:
                t = v < 0
            elif cond == 1:
                t = v >= 0
            else:
                raise RuntimeError("regimm %d" % cond)
            if t:
                return ('branch', (pc + 4 + (simm << 2)) & M)
        elif op == 2:
            return ('jump', ((pc + 4) & 0xF0000000) | ((w & 0x3FFFFFF) << 2))
        elif op == 3:
            r[31] = (pc + 8) & M
            return ('jump', ((pc + 4) & 0xF0000000) | ((w & 0x3FFFFFF) << 2))
        elif op == 4:
            if r[rs] == r[rt]:
                return ('branch', (pc + 4 + (simm << 2)) & M)
        elif op == 5:
            if r[rs] != r[rt]:
                return ('branch', (pc + 4 + (simm << 2)) & M)
        elif op == 6:
            if s32(r[rs]) <= 0:
                return ('branch', (pc + 4 + (simm << 2)) & M)
        elif op == 7:
            if s32(r[rs]) > 0:
                return ('branch', (pc + 4 + (simm << 2)) & M)
        elif op in (8, 9):
            r[rt] = (r[rs] + simm) & M
        elif op == 10:
            r[rt] = 1 if s32(r[rs]) < simm else 0
        elif op == 11:
            r[rt] = 1 if (r[rs] & M) < (simm & M) else 0
        elif op == 12:
            r[rt] = r[rs] & imm
        elif op == 13:
            r[rt] = r[rs] | imm
        elif op == 14:
            r[rt] = r[rs] ^ imm
        elif op == 15:
            r[rt] = (imm << 16) & M
        elif op == 0x20:
            r[rt] = s8(self.rb(r[rs] + simm)) & M
        elif op == 0x21:
            r[rt] = s16(self.rh(r[rs] + simm)) & M
        elif op == 0x23:
            r[rt] = self.rw(r[rs] + simm)
        elif op == 0x24:
            r[rt] = self.rb(r[rs] + simm)
        elif op == 0x25:
            r[rt] = self.rh(r[rs] + simm)
        elif op == 0x28:
            self.wb(r[rs] + simm, r[rt])
        elif op == 0x29:
            self.wh(r[rs] + simm, r[rt])
        elif op == 0x2B:
            self.ww(r[rs] + simm, r[rt])
        else:
            raise RuntimeError("unhandled op %02x at %08x" % (op, pc))
        r[0] = 0
        return ('next',)

    def call(self, addr, a0=0, a1=0, a2=0, a3=0, sp=0x801FF000, maxsteps=200000):
        SENT = 0x80000004
        self.r = [0] * 32
        self.r[4], self.r[5], self.r[6], self.r[7] = a0 & M, a1 & M, a2 & M, a3 & M
        self.r[29] = sp
        self.r[31] = SENT
        self.pc = addr
        self.steps = 0
        while self.pc != SENT:
            self.steps += 1
            if self.steps > maxsteps:
                raise RuntimeError("step limit")
            pc = self.pc
            if pc in self.hooks and pc != addr:
                self.hooks[pc](self)
                self.pc = self.r[31]
                continue
            if pc in self.hooks and pc == addr and self.hooks[pc] is not None and False:
                pass
            w = self.rw(pc)
            res = self.step_instr(w, pc)
            if res[0] == 'next':
                self.pc = pc + 4
            else:
                # execute delay slot
                dw = self.rw(pc + 4)
                # for jr/jalr the target register was read before the delay slot
                tgt = res[1]
                dres = self.step_instr(dw, pc + 4)
                if dres[0] != 'next':
                    raise RuntimeError("branch in delay slot at %08x" % pc)
                self.pc = tgt
                if (w >> 26) == 3 or ((w >> 26) == 0 and (w & 63) == 9):
                    ret = (pc + 8) & M
                    self.jal_ret.setdefault(ret, tgt)
            # record search return value
            if self.pc in self.jal_ret and self.jal_ret[self.pc] == 0x8003C954:
                self.search_returns.append(self.r[2])
        return self.r[2]
