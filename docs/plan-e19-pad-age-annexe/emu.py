"""Minimal MIPS-I interpreter over ALUN_CD.EXE (France), read-only use of the binary.
Runs the REAL code of the choice box (opener 0x80050BA8, the three update states, the pad update 0x8002E250,
UpdateUiBoxesPosition 0x80047DD0, the 0x44 handler 0x8003E88C) with python stubs only for libc, sound, VRAM upload,
the cell initialiser and the text-width glyph table lookups that are not needed (CalcTextWidth runs for real).
"""
import sys, os, struct
SP = r"C:\Users\casad\AppData\Local\Temp\claude\D--development-repo-alundra-casaengine-project-converter\b00d1a72-420d-4acc-ab34-0c25fbdad3a1\scratchpad"
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lib

RAM_SIZE = 0x200000
SENTINEL = 0xFFFFFFF0


def s32(x):
    x &= 0xFFFFFFFF
    return x - (1 << 32) if x & 0x80000000 else x


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
        self.log = []          # (kind, ...) events
        self.steps = 0

    # memory -----------------------------------------------------------
    def _a(self, addr):
        return addr & 0x1FFFFFFF

    def rb(self, a): return self.ram[self._a(a)]
    def rh(self, a):
        a = self._a(a); return struct.unpack_from('<H', self.ram, a)[0]
    def rw(self, a):
        a = self._a(a); return struct.unpack_from('<I', self.ram, a)[0]
    def wb(self, a, v): self.ram[self._a(a)] = v & 0xFF
    def wh(self, a, v):
        a = self._a(a); struct.pack_into('<H', self.ram, a, v & 0xFFFF)
    def ww(self, a, v):
        a = self._a(a); struct.pack_into('<I', self.ram, a, v & 0xFFFFFFFF)
    def rhs(self, a):
        v = self.rh(a); return v - 0x10000 if v & 0x8000 else v
    def rws(self, a): return s32(self.rw(a))
    def cstr(self, a):
        o = self._a(a); e = self.ram.index(b'\0', o); return bytes(self.ram[o:e])

    # execution --------------------------------------------------------
    def call(self, addr, *args, limit=2_000_000):
        saved = list(self.r)
        for i, a in enumerate(args):
            self.r[4 + i] = a & 0xFFFFFFFF
        self.r[29] = 0x801FFF00
        self.r[31] = SENTINEL
        self.pc = addr
        n = 0
        npc = None
        while True:
            if self.pc == SENTINEL:
                break
            if self.pc in self.hooks and npc is None:
                self.hooks[self.pc](self)
                self.pc = self.r[31]
                continue
            self.step()
            n += 1
            if n > limit:
                raise RuntimeError('step limit at %08x' % self.pc)
        self.steps += n
        v0 = self.r[2]
        # keep callee-saved context: restore everything but v0/v1
        v1 = self.r[3]
        self.r = saved
        self.r[2] = v0
        self.r[3] = v1
        return v0

    def step(self):
        pc = self.pc
        w = self.rw(pc)
        # execute instruction at pc, then delay slot semantics via pending branch
        br = self.exec1(pc, w)      # returns None or ('b', target) / ('j', target)
        if br is None:
            self.pc = pc + 4
        else:
            # delay slot
            dw = self.rw(pc + 4)
            if br[1] is not None:
                # delay slot executes before the jump; but may be hooked? (not supported)
                pass
            self.exec1(pc + 4, dw)
            self.pc = br[1] if br[1] is not None else pc + 8

    def exec1(self, pc, w):
        r = self.r
        op = w >> 26
        rs = (w >> 21) & 31
        rt = (w >> 16) & 31
        rd = (w >> 11) & 31
        sh = (w >> 6) & 31
        fn = w & 63
        imm = w & 0xFFFF
        simm = imm - 0x10000 if imm & 0x8000 else imm
        tgt = pc + 4 + (simm << 2)

        def setr(i, v):
            if i:
                r[i] = v & 0xFFFFFFFF
        if op == 0:
            if fn == 0x00: setr(rd, r[rt] << sh)
            elif fn == 0x02: setr(rd, r[rt] >> sh)
            elif fn == 0x03: setr(rd, s32(r[rt]) >> sh)
            elif fn == 0x04: setr(rd, r[rt] << (r[rs] & 31))
            elif fn == 0x06: setr(rd, r[rt] >> (r[rs] & 31))
            elif fn == 0x07: setr(rd, s32(r[rt]) >> (r[rs] & 31))
            elif fn == 0x08: return ('j', r[rs])
            elif fn == 0x09:
                t = r[rs]; setr(rd, pc + 8); return ('j', t)
            elif fn == 0x0D: raise RuntimeError('break at %08x' % pc)
            elif fn == 0x10: setr(rd, self.hi)
            elif fn == 0x12: setr(rd, self.lo)
            elif fn == 0x18:
                p = s32(r[rs]) * s32(r[rt]); self.lo = p & 0xFFFFFFFF; self.hi = (p >> 32) & 0xFFFFFFFF
            elif fn == 0x19:
                p = r[rs] * r[rt]; self.lo = p & 0xFFFFFFFF; self.hi = (p >> 32) & 0xFFFFFFFF
            elif fn == 0x1A:
                a, b = s32(r[rs]), s32(r[rt])
                if b == 0: raise RuntimeError('div0')
                q = abs(a) // abs(b); q = q if (a < 0) == (b < 0) else -q
                self.lo = q & 0xFFFFFFFF; self.hi = (a - q * b) & 0xFFFFFFFF
            elif fn == 0x1B:
                self.lo = (r[rs] // r[rt]) & 0xFFFFFFFF; self.hi = r[rs] % r[rt]
            elif fn in (0x20, 0x21): setr(rd, r[rs] + r[rt])
            elif fn in (0x22, 0x23): setr(rd, r[rs] - r[rt])
            elif fn == 0x24: setr(rd, r[rs] & r[rt])
            elif fn == 0x25: setr(rd, r[rs] | r[rt])
            elif fn == 0x26: setr(rd, r[rs] ^ r[rt])
            elif fn == 0x27: setr(rd, ~(r[rs] | r[rt]))
            elif fn == 0x2A: setr(rd, 1 if s32(r[rs]) < s32(r[rt]) else 0)
            elif fn == 0x2B: setr(rd, 1 if r[rs] < r[rt] else 0)
            else: raise RuntimeError('R fn %02x at %08x' % (fn, pc))
            return None
        if op == 1:
            v = s32(r[rs])
            if rt == 0: return ('b', tgt) if v < 0 else ('b', None)
            if rt == 1: return ('b', tgt) if v >= 0 else ('b', None)
            raise RuntimeError('regimm %d at %08x' % (rt, pc))
        if op == 2: return ('j', ((pc + 4) & 0xF0000000) | ((w & 0x3FFFFFF) << 2))
        if op == 3:
            setr(31, pc + 8); return ('j', ((pc + 4) & 0xF0000000) | ((w & 0x3FFFFFF) << 2))
        if op == 4: return ('b', tgt) if r[rs] == r[rt] else ('b', None)
        if op == 5: return ('b', tgt) if r[rs] != r[rt] else ('b', None)
        if op == 6: return ('b', tgt) if s32(r[rs]) <= 0 else ('b', None)
        if op == 7: return ('b', tgt) if s32(r[rs]) > 0 else ('b', None)
        if op in (8, 9): setr(rt, r[rs] + simm)
        elif op == 0xA: setr(rt, 1 if s32(r[rs]) < simm else 0)
        elif op == 0xB: setr(rt, 1 if r[rs] < (simm & 0xFFFFFFFF) else 0)
        elif op == 0xC: setr(rt, r[rs] & imm)
        elif op == 0xD: setr(rt, r[rs] | imm)
        elif op == 0xE: setr(rt, r[rs] ^ imm)
        elif op == 0xF: setr(rt, imm << 16)
        elif op == 0x20:
            v = self.rb(r[rs] + simm); setr(rt, v - 256 if v & 0x80 else v)
        elif op == 0x21: setr(rt, self.rhs(r[rs] + simm))
        elif op == 0x23: setr(rt, self.rw(r[rs] + simm))
        elif op == 0x24: setr(rt, self.rb(r[rs] + simm))
        elif op == 0x25: setr(rt, self.rh(r[rs] + simm))
        elif op == 0x28: self.wb(r[rs] + simm, r[rt])
        elif op == 0x29: self.wh(r[rs] + simm, r[rt])
        elif op == 0x2B: self.ww(r[rs] + simm, r[rt])
        else: raise RuntimeError('op %02x at %08x' % (op, pc))
        return None


def make_machine():
    e = Emu()
    h = e.hooks

    # libc stubs ----------------------------------------------------
    def strlen(m):
        m.r[2] = len(m.cstr(m.r[4]))
    def strcpy(m):
        s = m.cstr(m.r[5]); d = m.r[4]
        for i, c in enumerate(s + b'\0'): m.wb(d + i, c)
        m.r[2] = d
    def strncpy(m):
        d, s, n = m.r[4], m.r[5], m.r[6]
        src = m.cstr(s)
        for i in range(n):
            m.wb(d + i, src[i] if i < len(src) else 0)
        m.r[2] = d
    def bzero(m):
        d, n = m.r[4], m.r[5]
        for i in range(n): m.wb(d + i, 0)
    h[0x80081FFC] = strlen
    h[0x80081FB8] = strcpy
    h[0x8008202C] = strncpy
    h[0x80081D80] = bzero

    # engine stubs --------------------------------------------------
    def sound(m):
        m.log.append(('sound', m.r[4] & 0xFFFFFFFF))
    h[0x800490FC] = sound

    def rtb(m):
        # RenderTextBitmap(str, buf, vramX, vramY, pen, rank, width, height): sp+0x10.. are the stack args
        sp = m.r[29]
        m.log.append(('rtb', m.cstr(m.r[4]), m.r[6], m.r[7], m.rw(sp + 0x10), m.rw(sp + 0x14), m.rw(sp + 0x18), m.rw(sp + 0x1C)))
    h[0x800478C4] = rtb

    def cellsinit(m):
        m.log.append(('cellsinit', m.r[4]))
    h[0x80048304] = cellsinit
    return e
