"""Critic's own MIPS-I (R3000) interpreter, written from scratch.
Instruction fetch from the EXE image; data memory = bytes that were explicitly provided or written.
Features: branch delay slots, load delay slot (hardware semantics, hazards counted), mult/multu/div/divu,
hooks (journalled replacements of callees), strict memory (an unlisted byte read raises)."""
import struct
from c_exe import load

M32 = 0xFFFFFFFF
MAGIC_RA = 0x80FFFF00
SP0 = 0x801FFF00


def s32(x):
    x &= M32
    return x - (1 << 32) if x & 0x80000000 else x


def s16(x):
    x &= 0xFFFF
    return x - 0x10000 if x & 0x8000 else x


def s8(x):
    x &= 0xFF
    return x - 0x100 if x & 0x80 else x


class MemErr(Exception):
    pass


class Cpu:
    def __init__(self, hooks=None, trace=False):
        pc0, gp0, tbase, tsize, img = load()
        self.tbase, self.tsize, self.img = tbase, tsize, img
        self.mem = {}          # addr -> byte (data)
        self.reads = []        # (addr, size, value) first-read-of-unwritten bytes, in order
        self.writes = {}       # (addr,size) -> value, insertion ordered
        self.written_bytes = set()
        self.provided = set()
        self.touched = set()
        self.hooks = hooks or {}   # addr -> callable(cpu) returning (v0, v1|None)
        self.calls = []
        self.r = [0] * 32
        self.hi = 0
        self.lo = 0
        self.pc = 0
        self.npc = 0
        self.pending = None
        self.hazards = 0
        self.covered = set()
        self.steps = 0
        self.trace = trace

    # ---- memory ----
    def provide(self, addr, size, val):
        for i in range(size):
            b = (val >> (8 * i)) & 0xFF
            a = addr + i
            if a in self.mem and self.mem[a] != b and a not in self.written_bytes:
                raise MemErr("conflicting provided byte at %08X" % a)
            self.mem[a] = b
            self.provided.add(a)

    def in_image(self, a):
        return self.tbase <= a < self.tbase + self.tsize

    def rd(self, addr, size):
        addr &= M32
        if addr % size:
            raise MemErr("unaligned read %08X size %d" % (addr, size))
        v = 0
        for i in range(size):
            a = addr + i
            if a in self.mem:
                b = self.mem[a]
                if a not in self.written_bytes:
                    self.touched.add(a)
            else:
                raise MemErr("read of unlisted byte %08X (size %d at %08X) pc=%08X" % (a, size, addr, self.cur_pc))
            v |= b << (8 * i)
        return v

    def wr(self, addr, size, val):
        addr &= M32
        if addr % size:
            raise MemErr("unaligned write %08X size %d" % (addr, size))
        val &= (1 << (8 * size)) - 1
        for i in range(size):
            self.mem[addr + i] = (val >> (8 * i)) & 0xFF
            self.written_bytes.add(addr + i)
        if not (0x801FFE00 <= addr <= 0x801FFEFF):
            key = (addr, size)
            self.writes[key] = val
            self.writes_order.append(key) if key not in self.writes_seen else None
            self.writes_seen.add(key)

    writes_order = None
    writes_seen = None

    # ---- registers ----
    def setr(self, n, v):
        if n:
            self.r[n] = v & M32

    # ---- execution ----
    def fetch(self, pc):
        if not self.in_image(pc):
            raise MemErr("fetch outside image %08X" % pc)
        return struct.unpack_from("<I", self.img, pc - self.tbase)[0]

    def run(self, entry, args, maxsteps=2_000_000):
        self.writes_order = []
        self.writes_seen = set()
        self.r = [0] * 32
        self.r[29] = SP0
        self.r[31] = MAGIC_RA
        for i, a in enumerate(args[:4]):
            self.r[4 + i] = a & M32
        self.pc = entry
        self.npc = entry + 4
        self.pending = None
        n = 0
        while True:
            if self.pc == MAGIC_RA:
                break
            if self.pc in self.hooks:
                if self.pending:
                    reg, val = self.pending
                    self.setr(reg, val)
                    self.pending = None
                v0, v1 = self.hooks[self.pc](self)
                self.setr(2, v0)
                if v1 is not None:
                    self.setr(3, v1)
                self.pc = self.r[31]
                self.npc = self.pc + 4
                continue
            self.step()
            n += 1
            if n > maxsteps:
                raise MemErr("step limit")
        if self.pending:
            reg, val = self.pending
            self.setr(reg, val)
            self.pending = None
        return self.r[2], self.r[3]

    def step(self):
        pc = self.pc
        self.cur_pc = pc
        w = self.fetch(pc)
        self.covered.add(pc)
        pend = self.pending
        self.pending = None
        self.pc = self.npc
        self.npc = (self.pc + 4) & M32
        r = self.r
        op = w >> 26
        rs = (w >> 21) & 31
        rt = (w >> 16) & 31
        rd = (w >> 11) & 31
        sh = (w >> 6) & 31
        fn = w & 63
        imm = w & 0xFFFF
        simm = s16(imm)
        # hazard accounting: instruction right after a load reading the load target
        if pend is not None:
            reads_regs = self.regs_read(w)
            if pend[0] in reads_regs and pend[0] != 0:
                self.hazards += 1
        load_target = None

        if op == 0:
            if fn == 0:
                self.setr(rd, r[rt] << sh)
            elif fn == 2:
                self.setr(rd, (r[rt] & M32) >> sh)
            elif fn == 3:
                self.setr(rd, s32(r[rt]) >> sh)
            elif fn == 4:
                self.setr(rd, r[rt] << (r[rs] & 31))
            elif fn == 6:
                self.setr(rd, (r[rt] & M32) >> (r[rs] & 31))
            elif fn == 7:
                self.setr(rd, s32(r[rt]) >> (r[rs] & 31))
            elif fn == 8:
                self.npc = r[rs]
            elif fn == 9:
                self.setr(rd, pc + 8)
                self.npc = r[rs]
            elif fn == 0x10:
                self.setr(rd, self.hi)
            elif fn == 0x11:
                self.hi = r[rs]
            elif fn == 0x12:
                self.setr(rd, self.lo)
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
            elif fn == 0x1A:
                a, b = s32(r[rs]), s32(r[rt])
                if b == 0:
                    self.lo = M32 if a >= 0 else 1
                    self.hi = a & M32
                else:
                    q = abs(a) // abs(b)
                    if (a < 0) != (b < 0):
                        q = -q
                    self.lo = q & M32
                    self.hi = (a - q * b) & M32
            elif fn == 0x1B:
                a, b = r[rs] & M32, r[rt] & M32
                if b == 0:
                    self.lo = M32
                    self.hi = a
                else:
                    self.lo = a // b
                    self.hi = a % b
            elif fn in (0x20, 0x21):
                self.setr(rd, r[rs] + r[rt])
            elif fn in (0x22, 0x23):
                self.setr(rd, r[rs] - r[rt])
            elif fn == 0x24:
                self.setr(rd, r[rs] & r[rt])
            elif fn == 0x25:
                self.setr(rd, r[rs] | r[rt])
            elif fn == 0x26:
                self.setr(rd, r[rs] ^ r[rt])
            elif fn == 0x27:
                self.setr(rd, ~(r[rs] | r[rt]))
            elif fn == 0x2A:
                self.setr(rd, 1 if s32(r[rs]) < s32(r[rt]) else 0)
            elif fn == 0x2B:
                self.setr(rd, 1 if (r[rs] & M32) < (r[rt] & M32) else 0)
            else:
                raise MemErr("unsupported SPECIAL fn %02X at %08X" % (fn, pc))
        elif op == 1:
            v = s32(r[rs])
            if rt == 0:
                if v < 0:
                    self.npc = (pc + 4 + (simm << 2)) & M32
            elif rt == 1:
                if v >= 0:
                    self.npc = (pc + 4 + (simm << 2)) & M32
            elif rt == 0x10:
                self.setr(31, pc + 8)
                if v < 0:
                    self.npc = (pc + 4 + (simm << 2)) & M32
            elif rt == 0x11:
                self.setr(31, pc + 8)
                if v >= 0:
                    self.npc = (pc + 4 + (simm << 2)) & M32
            else:
                raise MemErr("unsupported REGIMM at %08X" % pc)
        elif op == 2:
            self.npc = ((pc + 4) & 0xF0000000) | ((w & 0x3FFFFFF) << 2)
        elif op == 3:
            self.setr(31, pc + 8)
            self.npc = ((pc + 4) & 0xF0000000) | ((w & 0x3FFFFFF) << 2)
        elif op == 4:
            if r[rs] == r[rt]:
                self.npc = (pc + 4 + (simm << 2)) & M32
        elif op == 5:
            if r[rs] != r[rt]:
                self.npc = (pc + 4 + (simm << 2)) & M32
        elif op == 6:
            if s32(r[rs]) <= 0:
                self.npc = (pc + 4 + (simm << 2)) & M32
        elif op == 7:
            if s32(r[rs]) > 0:
                self.npc = (pc + 4 + (simm << 2)) & M32
        elif op in (8, 9):
            self.setr(rt, r[rs] + simm)
        elif op == 0xA:
            self.setr(rt, 1 if s32(r[rs]) < simm else 0)
        elif op == 0xB:
            self.setr(rt, 1 if (r[rs] & M32) < (simm & M32) else 0)
        elif op == 0xC:
            self.setr(rt, r[rs] & imm)
        elif op == 0xD:
            self.setr(rt, r[rs] | imm)
        elif op == 0xE:
            self.setr(rt, r[rs] ^ imm)
        elif op == 0xF:
            self.setr(rt, imm << 16)
        elif op == 0x20:
            load_target = (rt, s8(self.rd(r[rs] + simm, 1)) & M32)
        elif op == 0x21:
            load_target = (rt, s16(self.rd(r[rs] + simm, 2)) & M32)
        elif op == 0x23:
            load_target = (rt, self.rd(r[rs] + simm, 4))
        elif op == 0x24:
            load_target = (rt, self.rd(r[rs] + simm, 1))
        elif op == 0x25:
            load_target = (rt, self.rd(r[rs] + simm, 2))
        elif op == 0x28:
            self.wr(r[rs] + simm, 1, r[rt])
        elif op == 0x29:
            self.wr(r[rs] + simm, 2, r[rt])
        elif op == 0x2B:
            self.wr(r[rs] + simm, 4, r[rt])
        else:
            raise MemErr("unsupported opcode %02X at %08X (%08X)" % (op, pc, w))
        # commit the previous load (visible after this instruction)
        if pend is not None:
            self.setr(pend[0], pend[1])
        if load_target is not None:
            self.pending = load_target

    @staticmethod
    def regs_read(w):
        op = w >> 26
        rs = (w >> 21) & 31
        rt = (w >> 16) & 31
        if op == 0:
            fn = w & 63
            if fn in (0, 2, 3):
                return {rt}
            if fn in (8, 9, 0x11, 0x13):
                return {rs}
            if fn in (0x10, 0x12):
                return set()
            return {rs, rt}
        if op == 1:
            return {rs}
        if op in (2, 3, 0xF):
            return set()
        if op in (4, 5):
            return {rs, rt}
        if op in (6, 7):
            return {rs}
        if op in (0x28, 0x29, 0x2B):
            return {rs, rt}
        return {rs}
