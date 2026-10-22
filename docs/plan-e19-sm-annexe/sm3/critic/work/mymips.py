"""Critic-own minimal MIPS-I interpreter (written from scratch, raw instruction decoding, delay slots).
Runs the REAL code of ALUN_CD.EXE (France). Read-only on the exe. Memory: sparse overlay over the exe image."""
import struct
import mylib as m

M32 = 0xFFFFFFFF
def s32(x): x &= M32; return x - (1 << 32) if x & 0x80000000 else x
def s16(x): x &= 0xFFFF; return x - 0x10000 if x & 0x8000 else x

class Stop(Exception): pass

class Mem:
    def __init__(self):
        self.ov = {}
        self.unmapped = set()
        self.reads = []      # (addr,size) data reads, for the read map
        self.writes = []     # (addr,size,val)
        self.trace_reads = False
    def rb(self, a, data=True):
        a &= M32
        if a in self.ov: return self.ov[a]
        o = 0x800 + a - m.TADDR
        if m.TADDR <= a < m.TADDR + m.TSIZE and 0x800 <= o < len(m.DATA): return m.DATA[o]
        self.unmapped.add(a)
        return 0
    def wb(self, a, v): self.ov[a & M32] = v & 0xFF
    def rw(self, a):
        assert a % 4 == 0, hex(a)
        v = self.rb(a) | self.rb(a+1) << 8 | self.rb(a+2) << 16 | self.rb(a+3) << 24
        if self.trace_reads: self.reads.append((a, 4))
        return v
    def rh(self, a):
        assert a % 2 == 0, hex(a)
        v = self.rb(a) | self.rb(a+1) << 8
        if self.trace_reads: self.reads.append((a, 2))
        return v
    def rbyte(self, a):
        if self.trace_reads: self.reads.append((a, 1))
        return self.rb(a)
    def ww(self, a, v):
        assert a % 4 == 0, hex(a)
        for i in range(4): self.wb(a+i, (v >> (8*i)) & 0xFF)
        self.writes.append((a, 4, v & M32))
    def wh(self, a, v):
        for i in range(2): self.wb(a+i, (v >> (8*i)) & 0xFF)
        self.writes.append((a, 2, v & 0xFFFF))
    def wbyte(self, a, v):
        self.wb(a, v); self.writes.append((a, 1, v & 0xFF))
    def fetch(self, a):
        o = 0x800 + a - m.TADDR
        return struct.unpack_from("<I", m.DATA, o)[0] if (a not in self.ov) else self.rw(a)

class CPU:
    def __init__(self, mem, stubs=None):
        self.mem = mem
        self.r = [0] * 32
        self.pc = 0
        self.stubs = stubs or {}     # addr -> callable(cpu) performing the call, result in v0
        self.steps = 0
    def run(self, entry, args, sp, ra_sentinel=0xDEAD0000, maxsteps=2_000_000):
        r = self.r
        r[29] = sp; r[31] = ra_sentinel
        for i, v in enumerate(args): r[4 + i] = v & M32
        self.pc = entry
        self.steps = 0
        while True:
            if self.pc == ra_sentinel: return
            if self.pc in self.stubs:
                self.stubs[self.pc](self)
                self.pc = r[31]
                continue
            self.step()
            self.steps += 1
            if self.steps > maxsteps: raise Stop("step limit")
    def step(self):
        mem = self.mem; r = self.r
        pc = self.pc
        w = mem.fetch(pc)
        # execute instruction at pc; handle branch with delay slot
        nxt = (pc + 4) & M32
        br = self.exec(w, pc)       # returns None or (target) for taken branch/jump
        if br is None:
            self.pc = nxt
        else:
            # delay slot
            ds = mem.fetch(nxt)
            b2 = self.exec(ds, nxt)
            assert b2 is None, "branch in delay slot @%x" % nxt
            self.pc = br
        r[0] = 0
    def exec(self, w, pc):
        r = self.r; mem = self.mem
        op = w >> 26; rs = (w >> 21) & 31; rt = (w >> 16) & 31; rd = (w >> 11) & 31; sh = (w >> 6) & 31; fn = w & 63
        imm = w & 0xFFFF; simm = s16(imm)
        tgt = (pc + 4 + (simm << 2)) & M32
        if op == 0:
            if fn == 0: r[rd] = (r[rt] << sh) & M32
            elif fn == 2: r[rd] = (r[rt] & M32) >> sh
            elif fn == 3: r[rd] = (s32(r[rt]) >> sh) & M32
            elif fn == 8: return r[rs] & M32
            elif fn == 9: ra = (pc + 8) & M32; t = r[rs]; r[rd] = ra; return t
            elif fn == 0x21: r[rd] = (r[rs] + r[rt]) & M32
            elif fn == 0x23: r[rd] = (r[rs] - r[rt]) & M32
            elif fn == 0x24: r[rd] = r[rs] & r[rt]
            elif fn == 0x25: r[rd] = r[rs] | r[rt]
            elif fn == 0x2A: r[rd] = 1 if s32(r[rs]) < s32(r[rt]) else 0
            elif fn == 0x2B: r[rd] = 1 if (r[rs] & M32) < (r[rt] & M32) else 0
            else: raise Stop("R fn %x @%x" % (fn, pc))
        elif op == 1:
            if rt == 0: return tgt if s32(r[rs]) < 0 else None
            if rt == 1: return tgt if s32(r[rs]) >= 0 else None
            raise Stop("regimm")
        elif op == 2: return ((pc + 4) & 0xF0000000) | ((w & 0x3FFFFFF) << 2)
        elif op == 3:
            r[31] = (pc + 8) & M32
            return ((pc + 4) & 0xF0000000) | ((w & 0x3FFFFFF) << 2)
        elif op == 4: return tgt if r[rs] == r[rt] else None
        elif op == 5: return tgt if r[rs] != r[rt] else None
        elif op == 6: return tgt if s32(r[rs]) <= 0 else None
        elif op == 7: return tgt if s32(r[rs]) > 0 else None
        elif op == 9: r[rt] = (r[rs] + simm) & M32
        elif op == 0xA: r[rt] = 1 if s32(r[rs]) < simm else 0
        elif op == 0xB: r[rt] = 1 if (r[rs] & M32) < (simm & M32) else 0
        elif op == 0xC: r[rt] = r[rs] & imm
        elif op == 0xD: r[rt] = r[rs] | imm
        elif op == 0xF: r[rt] = (imm << 16) & M32
        elif op == 0x23: r[rt] = mem.rw((r[rs] + simm) & M32)
        elif op == 0x24: r[rt] = mem.rbyte((r[rs] + simm) & M32)
        elif op == 0x25: r[rt] = mem.rh((r[rs] + simm) & M32)
        elif op == 0x2B: mem.ww((r[rs] + simm) & M32, r[rt])
        elif op == 0x29: mem.wh((r[rs] + simm) & M32, r[rt])
        elif op == 0x28: mem.wbyte((r[rs] + simm) & M32, r[rt])
        else: raise Stop("op %x @%x" % (op, pc))
        r[0] = 0
        return None
