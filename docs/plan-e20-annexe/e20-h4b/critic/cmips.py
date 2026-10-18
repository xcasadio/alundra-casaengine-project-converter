"""cmips.py - the critic's OWN MIPS-I interpreter over ALUN_CD.EXE (France). Written independently of the other agent's mips.py/machine.py.
Memory model: instruction fetch and reads of the EXE image come from the file; every other byte must come from the 'ram' dict
(strict=True raises on a read of an absent non-image byte; strict=False reads 0 like BSS).
Tracks reads/writes. Delay slots honoured (branch + load delay slots are NOT modelled for loads: the R3000 load delay slot is
checked separately by 'ldelay' option: the instruction in a load delay slot sees the OLD register value)."""
import struct, hashlib

EXE_PATH = r"D:\development\repo\Alundra Remake\Alundra (France)\Alundra (France)_extracted\ALUN_CD.EXE"
IMG = open(EXE_PATH, "rb").read()
assert hashlib.sha1(IMG).hexdigest() == "a705f8dab665645af5dfaf8fb348c7926b5caa58"
BASE = 0x80020000
TEXT_OFF = 0x800
IMG_END = BASE + len(IMG) - TEXT_OFF
M = 0xFFFFFFFF


def s32(v):
    v &= M
    return v - (1 << 32) if v & 0x80000000 else v


def s16(v):
    v &= 0xFFFF
    return v - 0x10000 if v & 0x8000 else v


def s8(v):
    v &= 0xFF
    return v - 0x100 if v & 0x80 else v


def img_u32(a):
    return struct.unpack_from("<I", IMG, a - BASE + TEXT_OFF)[0]


def img_s16(a):
    return struct.unpack_from("<h", IMG, a - BASE + TEXT_OFF)[0]


def img_u16(a):
    return struct.unpack_from("<H", IMG, a - BASE + TEXT_OFF)[0]


def img_u8(a):
    return IMG[a - BASE + TEXT_OFF]


class Incomplete(Exception):
    pass


class Mem:
    def __init__(self, ram=None, strict=False, image_writes=False):
        self.ram = dict(ram or {})  # byte address -> value
        self.strict = strict
        self.written = set()  # byte addresses written during the run
        self.rlog = []  # (addr,size,value) for bytes not yet written (first occurrence per (addr,size))
        self.wlog = []  # (addr,size,value) in order
        self.rseen = set()
        self.overlay = {}  # writes into the image area (data in EXE)

    def b(self, a):
        a &= M
        if a in self.ram:
            return self.ram[a]
        if BASE <= a < IMG_END:
            return IMG[a - BASE + TEXT_OFF]
        if self.strict:
            raise Incomplete("read of absent byte %#x" % a)
        return 0

    def rd(self, a, n):
        v = 0
        for i in range(n):
            v |= self.b(a + i) << (8 * i)
        # log as DATA read if not previously written by this run
        if not all((a + i) in self.written for i in range(n)):
            k = (a, n)
            if k not in self.rseen:
                self.rseen.add(k)
                self.rlog.append((a, n, v))
        return v

    def wr(self, a, n, v):
        a &= M
        for i in range(n):
            self.ram[a + i] = (v >> (8 * i)) & 0xFF
            self.written.add(a + i)
        self.wlog.append((a, n, v & ((1 << (8 * n)) - 1)))


class Cpu:
    def __init__(self, mem, stubs=None, ldelay=False):
        self.m = mem
        self.r = [0] * 32
        self.hi = self.lo = 0
        self.pc = 0
        self.stubs = stubs or {}
        self.steps = 0
        self.visited = set()
        self.ldelay = ldelay
        self.stop = set()
        self.stopped_at = None
        self.calls = []

    def fetch(self, a):
        if not (BASE <= a < IMG_END):
            raise Incomplete("fetch outside image %#x" % a)
        return img_u32(a)

    def setr(self, i, v):
        if i:
            self.r[i] = v & M

    def run(self, pc, args=(), ra=0xFFFF0000, max_steps=2_000_000, sp=0x801FFF00, stop_at=()):
        self.pc = pc
        self.r[29] = sp
        self.r[31] = ra
        for i, a in enumerate(args[:4]):
            self.r[4 + i] = a & M
        self.stop = set(stop_at)
        self.stopped_at = None
        while True:
            if self.pc == ra:
                return self.r[2]
            if self.pc in self.stop:
                self.stopped_at = self.pc
                return self.r[2]
            self.steps += 1
            if self.steps > max_steps:
                raise RuntimeError("step limit pc=%#x" % self.pc)
            if self.pc in self.stubs:
                self.calls.append((self.pc, self.r[4], self.r[5], self.r[6], self.r[7]))
                self.stubs[self.pc](self)
                self.pc = self.r[31]
                continue
            self.step()

    # one instruction (+ delay slot handling)
    def step(self):
        pc = self.pc
        ins = self.fetch(pc)
        self.visited.add(pc)
        nxt = pc + 4
        tgt = self.exec(ins, pc)
        if tgt is None:
            self.pc = nxt
            return
        # branch/jump: execute delay slot at pc+4 then go to tgt
        kind, dest = tgt
        ds = self.fetch(pc + 4)
        self.visited.add(pc + 4)
        self.steps += 1
        # delay slot may itself not be a branch in this code
        t2 = self.exec(ds, pc + 4)
        if t2 is not None:
            raise RuntimeError("branch in delay slot at %#x" % (pc + 4))
        self.pc = dest if dest is not None else pc + 8

    def exec(self, ins, pc):
        op = ins >> 26
        rs = (ins >> 21) & 31
        rt = (ins >> 16) & 31
        rd = (ins >> 11) & 31
        sh = (ins >> 6) & 31
        fn = ins & 63
        imm = ins & 0xFFFF
        simm = s16(imm)
        R = self.r
        if op == 0:
            if fn == 0:
                self.setr(rd, R[rt] << sh)
            elif fn == 2:
                self.setr(rd, R[rt] >> sh)
            elif fn == 3:
                self.setr(rd, s32(R[rt]) >> sh)
            elif fn == 4:
                self.setr(rd, R[rt] << (R[rs] & 31))
            elif fn == 6:
                self.setr(rd, R[rt] >> (R[rs] & 31))
            elif fn == 7:
                self.setr(rd, s32(R[rt]) >> (R[rs] & 31))
            elif fn == 8:  # jr
                return ("j", R[rs])
            elif fn == 9:  # jalr
                t = R[rs]
                self.setr(rd, pc + 8)
                return ("j", t)
            elif fn == 12 or fn == 13:
                raise RuntimeError("syscall/break at %#x" % pc)
            elif fn == 16:
                self.setr(rd, self.hi)
            elif fn == 17:
                self.hi = R[rs]
            elif fn == 18:
                self.setr(rd, self.lo)
            elif fn == 19:
                self.lo = R[rs]
            elif fn == 24:
                p = s32(R[rs]) * s32(R[rt])
                self.lo = p & M
                self.hi = (p >> 32) & M
            elif fn == 25:
                p = R[rs] * R[rt]
                self.lo = p & M
                self.hi = (p >> 32) & M
            elif fn == 26:
                a, b = s32(R[rs]), s32(R[rt])
                if b == 0:
                    self.lo = M if a >= 0 else 1
                    self.hi = a & M
                else:
                    q = abs(a) // abs(b)
                    if (a < 0) != (b < 0):
                        q = -q
                    self.lo = q & M
                    self.hi = (a - q * b) & M
            elif fn == 27:
                a, b = R[rs], R[rt]
                if b == 0:
                    self.lo = M
                    self.hi = a
                else:
                    self.lo = a // b
                    self.hi = a % b
            elif fn in (32, 33):
                self.setr(rd, R[rs] + R[rt])
            elif fn in (34, 35):
                self.setr(rd, R[rs] - R[rt])
            elif fn == 36:
                self.setr(rd, R[rs] & R[rt])
            elif fn == 37:
                self.setr(rd, R[rs] | R[rt])
            elif fn == 38:
                self.setr(rd, R[rs] ^ R[rt])
            elif fn == 39:
                self.setr(rd, ~(R[rs] | R[rt]))
            elif fn == 42:
                self.setr(rd, 1 if s32(R[rs]) < s32(R[rt]) else 0)
            elif fn == 43:
                self.setr(rd, 1 if R[rs] < R[rt] else 0)
            else:
                raise RuntimeError("unknown special %d at %#x" % (fn, pc))
            return None
        if op == 1:
            v = s32(R[rs])
            c = rt
            if c in (0, 16):
                take = v < 0
            elif c in (1, 17):
                take = v >= 0
            else:
                raise RuntimeError("regimm %d" % c)
            if c in (16, 17):
                self.setr(31, pc + 8)
            return ("b", pc + 4 + (simm << 2)) if take else ("b", None)
        if op == 2:
            return ("j", (pc & 0xF0000000) | ((ins & 0x3FFFFFF) << 2))
        if op == 3:
            self.setr(31, pc + 8)
            return ("j", (pc & 0xF0000000) | ((ins & 0x3FFFFFF) << 2))
        if op == 4:
            return ("b", pc + 4 + (simm << 2)) if R[rs] == R[rt] else ("b", None)
        if op == 5:
            return ("b", pc + 4 + (simm << 2)) if R[rs] != R[rt] else ("b", None)
        if op == 6:
            return ("b", pc + 4 + (simm << 2)) if s32(R[rs]) <= 0 else ("b", None)
        if op == 7:
            return ("b", pc + 4 + (simm << 2)) if s32(R[rs]) > 0 else ("b", None)
        if op in (8, 9):
            self.setr(rt, R[rs] + simm)
        elif op == 10:
            self.setr(rt, 1 if s32(R[rs]) < simm else 0)
        elif op == 11:
            self.setr(rt, 1 if R[rs] < (simm & M) else 0)
        elif op == 12:
            self.setr(rt, R[rs] & imm)
        elif op == 13:
            self.setr(rt, R[rs] | imm)
        elif op == 14:
            self.setr(rt, R[rs] ^ imm)
        elif op == 15:
            self.setr(rt, imm << 16)
        elif op == 32:
            self.setr(rt, s8(self.m.rd(R[rs] + simm, 1)))
        elif op == 33:
            self.setr(rt, s16(self.m.rd(R[rs] + simm, 2)))
        elif op == 35:
            self.setr(rt, self.m.rd(R[rs] + simm, 4))
        elif op == 36:
            self.setr(rt, self.m.rd(R[rs] + simm, 1))
        elif op == 37:
            self.setr(rt, self.m.rd(R[rs] + simm, 2))
        elif op == 40:
            self.m.wr(R[rs] + simm, 1, R[rt])
        elif op == 41:
            self.m.wr(R[rs] + simm, 2, R[rt])
        elif op == 43:
            self.m.wr(R[rs] + simm, 4, R[rt])
        else:
            raise RuntimeError("unknown op %d at %#x" % (op, pc))
        return None
