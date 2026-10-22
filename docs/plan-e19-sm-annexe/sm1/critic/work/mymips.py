"""Independent minimal MIPS-I interpreter written from scratch by the sm1 critic.
Decodes raw instruction words by bit fields (no capstone, no reuse of the preparation's emu.py).
Memory: sparse bytes; the PS-X EXE image is the backing store for addresses it covers."""
import struct

EXE = r"D:/development/repo/Alundra Remake/Alundra (France)/Alundra (France)_extracted/ALUN_CD.EXE"
BASE = 0x80020000
HDR = 0x800

_img = open(EXE, 'rb').read()


def image_byte(addr):
    o = addr - BASE + HDR
    if 0 <= o < len(_img) and addr >= BASE:
        return _img[o]
    return 0


def u32(x):
    return x & 0xFFFFFFFF


def s32(x):
    x &= 0xFFFFFFFF
    return x - (1 << 32) if x & 0x80000000 else x


class Trap(Exception):
    pass


class Mem:
    def __init__(self):
        self.w = {}          # addr -> byte written
        self.reads = []      # (addr, size) data reads outside the code window
        self.writes = []     # (addr, size, value)
        self.code_lo = 0x80030000
        self.code_hi = 0x80098000

    def rb(self, a, track=True):
        a = u32(a)
        if track and not (self.code_lo <= a < self.code_hi):
            self.reads.append((a, 1))
        if a in self.w:
            return self.w[a]
        return image_byte(a) if a < 0x80120000 else 0

    def r32(self, a, track=True):
        a = u32(a)
        if a & 3:
            raise Trap("unaligned lw %08x" % a)
        if track and not (self.code_lo <= a < self.code_hi):
            self.reads.append((a, 4))
        v = 0
        for i in range(4):
            v |= self.rb(a + i, track=False) << (8 * i)
        return v

    def wb(self, a, v):
        self.w[u32(a)] = v & 0xFF

    def w32(self, a, v):
        a = u32(a)
        if a & 3:
            raise Trap("unaligned sw %08x" % a)
        self.writes.append((a, 4, u32(v)))
        for i in range(4):
            self.w[a + i] = (v >> (8 * i)) & 0xFF

    def fetch(self, a):
        a = u32(a)
        return struct.unpack('<I', bytes(self.rb(a + i, track=False) for i in range(4)))[0]


class CPU:
    SENT = 0xDEAD0000

    def __init__(self, mem, stubs=None):
        self.m = mem
        self.r = [0] * 32
        self.stubs = stubs or {}   # addr -> python callable(cpu) that replaces the function (returns via ra)
        self.steps = 0

    def reg(self, i):
        return self.r[i]

    def setreg(self, i, v):
        if i:
            self.r[i] = u32(v)

    def run(self, entry, max_steps=200000):
        pc = entry
        self.r[31] = self.SENT
        pending = None  # branch target after the delay slot
        while True:
            if pending is None and pc == self.SENT:
                return
            self.steps += 1
            if self.steps > max_steps:
                raise Trap("runaway")
            if pending is None and pc in self.stubs:
                self.stubs[pc](self)
                pc = self.r[31]
                continue
            w = self.m.fetch(pc)
            nxt = pc + 4
            target = None
            op = w >> 26
            rs = (w >> 21) & 31
            rt = (w >> 16) & 31
            rd = (w >> 11) & 31
            sh = (w >> 6) & 31
            fn = w & 63
            imm = w & 0xFFFF
            simm = imm - 0x10000 if imm & 0x8000 else imm
            R = self.r
            if op == 0:
                if fn == 0:
                    self.setreg(rd, R[rt] << sh)
                elif fn == 2:
                    self.setreg(rd, R[rt] >> sh)
                elif fn == 3:
                    self.setreg(rd, s32(R[rt]) >> sh)
                elif fn == 8:
                    target = R[rs]
                elif fn == 0x21:
                    self.setreg(rd, R[rs] + R[rt])
                elif fn == 0x23:
                    self.setreg(rd, R[rs] - R[rt])
                elif fn == 0x24:
                    self.setreg(rd, R[rs] & R[rt])
                elif fn == 0x25:
                    self.setreg(rd, R[rs] | R[rt])
                elif fn == 0x2A:
                    self.setreg(rd, 1 if s32(R[rs]) < s32(R[rt]) else 0)
                elif fn == 0x2B:
                    self.setreg(rd, 1 if R[rs] < R[rt] else 0)
                else:
                    raise Trap("funct %02x at %08x" % (fn, pc))
            elif op == 1:
                cond = (s32(R[rs]) < 0) if rt == 0 else (s32(R[rs]) >= 0) if rt == 1 else None
                if cond is None:
                    raise Trap("regimm %d at %08x" % (rt, pc))
                if cond:
                    target = pc + 4 + (simm << 2)
            elif op == 2:
                target = (pc & 0xF0000000) | ((w & 0x3FFFFFF) << 2)
            elif op == 3:
                self.r[31] = pc + 8
                target = (pc & 0xF0000000) | ((w & 0x3FFFFFF) << 2)
            elif op == 4:
                if R[rs] == R[rt]:
                    target = pc + 4 + (simm << 2)
            elif op == 5:
                if R[rs] != R[rt]:
                    target = pc + 4 + (simm << 2)
            elif op == 6:
                if s32(R[rs]) <= 0:
                    target = pc + 4 + (simm << 2)
            elif op == 7:
                if s32(R[rs]) > 0:
                    target = pc + 4 + (simm << 2)
            elif op == 9:
                self.setreg(rt, R[rs] + simm)
            elif op == 0xA:
                self.setreg(rt, 1 if s32(R[rs]) < simm else 0)
            elif op == 0xB:
                self.setreg(rt, 1 if R[rs] < u32(simm) else 0)
            elif op == 0xC:
                self.setreg(rt, R[rs] & imm)
            elif op == 0xD:
                self.setreg(rt, R[rs] | imm)
            elif op == 0xF:
                self.setreg(rt, imm << 16)
            elif op == 0x23:
                self.setreg(rt, self.m.r32(R[rs] + simm))
            elif op == 0x24:
                self.setreg(rt, self.m.rb(R[rs] + simm))
            elif op == 0x2B:
                self.m.w32(R[rs] + simm, R[rt])
            else:
                raise Trap("op %02x at %08x" % (op, pc))
            R[0] = 0
            # delay-slot mechanics
            if pending is not None:
                # we are executing a delay slot instruction that itself was a branch: not used by these handlers
                if target is not None:
                    raise Trap("branch in delay slot at %08x" % pc)
                pc = pending
                pending = None
                continue
            if target is not None:
                pending = u32(target)
                pc = nxt
                continue
            pc = nxt
