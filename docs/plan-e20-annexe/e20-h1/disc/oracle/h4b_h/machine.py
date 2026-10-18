"""machine.py - D1 oracle (E20.0): the MIPS machine for the tick passes of ALUN_CD.EXE (France).

Built on the E19.h2 interpreter (emu.py, copied here unchanged).  Adds, without touching the interpreter's instruction semantics:
  * a shadow map of which RAM bytes were ever written (the EXE image counts as written), so that a READ of a byte nobody initialised
    (RAM outside the image that neither the harness nor the real code wrote) is recorded with its PC: nothing is silently assumed zero;
  * a count of every function entered by jal/jalr (the call trace of a tick);
  * stubs (hooks) that journal their calls: sound 0x800490FC, effects 0x8003BDD8 / 0x8003BE74, spawn 0x80039F58, and the passes
    0x80038E84 / 0x80039300 (no effect in D1).
Nothing in this file knows an expected value of the annex.
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import lib
import emu

U = 65536
M32 = 0xFFFFFFFF
IMG_BASE = 0x80020000
IMG_END = IMG_BASE + (len(lib.DATA) - lib.OFF)          # 0x800CA800
RAM_END = 0x80200000

s32 = emu.s32


class Machine(emu.Emu):
    def __init__(self):
        super().__init__()
        self.init = bytearray(emu.RAM_SIZE)             # 1 = byte initialised
        a0 = IMG_BASE & 0x1FFFFFFF
        a1 = IMG_END & 0x1FFFFFFF
        for i in range(a0, a1):
            self.init[i] = 1
        self.uninit_reads = {}                          # (pc, addr, size) -> count
        self.calls = {}                                 # function entry -> count
        self.cov = set()                                # every PC executed (coverage of MovePlayer's early exits)
        self.journal = []                               # (tick, kind, ...) of stubbed calls
        self.tick_no = 0
        self.watch_uninit = True
        self.cur_pc = 0
        self.effect_slot = 0x801E8000                   # dummy effect slot returned by the effect stubs
        self.install_stubs()

    # ---- memory with the initialised-byte shadow --------------------------------------------------------------------
    def _chk(self, a, n):
        if not self.watch_uninit:
            return
        o = a & 0x1FFFFFFF
        if o + n > emu.RAM_SIZE:
            raise RuntimeError('access outside the 2 MB RAM at %08x (pc %08x)' % (a, self.cur_pc))
        for i in range(n):
            if not self.init[o + i]:
                k = (self.cur_pc, a, n)
                self.uninit_reads[k] = self.uninit_reads.get(k, 0) + 1
                return

    def rb(self, a):
        self._chk(a, 1); return super().rb(a)

    def rh(self, a):
        self._chk(a, 2); return super().rh(a)

    def rw(self, a):
        self._chk(a, 4); return super().rw(a)

    def _mark(self, a, n):
        o = a & 0x1FFFFFFF
        if o + n > emu.RAM_SIZE:
            raise RuntimeError('write outside the 2 MB RAM at %08x (pc %08x)' % (a, self.cur_pc))
        for i in range(n):
            self.init[o + i] = 1

    def wb(self, a, v):
        self._mark(a, 1); super().wb(a, v)

    def wh(self, a, v):
        self._mark(a, 2); super().wh(a, v)

    def ww(self, a, v):
        self._mark(a, 4); super().ww(a, v)

    # harness-side access that does not trip the tracker --------------------------------------------------------------
    def peek(self, a):
        o = a & 0x1FFFFFFF
        return int.from_bytes(self.ram[o:o + 4], 'little')

    def peeks(self, a):
        return s32(self.peek(a))

    def poke(self, a, v):
        self.ww(a, v & M32)

    def poke16(self, a, v):
        self.wh(a, v & 0xFFFF)

    def poke8(self, a, v):
        self.wb(a, v & 0xFF)

    def poke_bytes(self, a, bs):
        for i, b in enumerate(bs):
            self.wb(a + i, b)

    def zero(self, a, n):
        for i in range(n):
            self.wb(a + i, 0)

    # ---- instruction hook: current PC and the call trace -----------------------------------------------------------
    def exec1(self, pc, w):
        self.cur_pc = pc
        self.cov.add(pc)
        op = w >> 26
        if op in (0x22, 0x26, 0x2A, 0x2E):              # lwl, lwr, swl, swr: absent from emu.py, little-endian semantics
            self._unaligned(op, w)
            return None
        if op == 0 and (w & 63) in (0x11, 0x13):        # mthi, mtlo
            rs = (w >> 21) & 31
            if (w & 63) == 0x11: self.hi = self.r[rs]
            else: self.lo = self.r[rs]
            return None
        br = super().exec1(pc, w)
        if br is not None and br[0] == 'j':
            op = w >> 26
            if op == 3 or (op == 0 and (w & 63) == 9):
                self.calls[br[1]] = self.calls.get(br[1], 0) + 1
        return br

    def _unaligned(self, op, w):
        r = self.r
        rs = (w >> 21) & 31
        rt = (w >> 16) & 31
        imm = w & 0xFFFF
        simm = imm - 0x10000 if imm & 0x8000 else imm
        addr = (r[rs] + simm) & M32
        k = addr & 3
        al = addr & ~3
        word = self.rw(al) if op in (0x22, 0x26) else self.peek(al)
        if op == 0x22:      # lwl
            sh = 24 - 8 * k
            v = ((word << sh) & M32) | (r[rt] & ((1 << sh) - 1))
            if rt: r[rt] = v & M32
        elif op == 0x26:    # lwr
            sh = 8 * k
            v = (word >> sh) | (r[rt] & (~(M32 >> sh) & M32))
            if rt: r[rt] = v & M32
        elif op == 0x2A:    # swl
            sh = 24 - 8 * k
            keep = word & (~(M32 >> sh) & M32)
            self.ww(al, keep | (r[rt] >> sh))
        else:               # swr
            sh = 8 * k
            keep = word & ((1 << sh) - 1)
            self.ww(al, keep | ((r[rt] << sh) & M32))

    # ---- stubs ---------------------------------------------------------------------------------------------------
    def install_stubs(self):
        h = self.hooks

        def sound(m):                                   # PlaySoundEffect(id): journalled, no effect
            m.journal.append((m.tick_no, 'sound', m.r[4] & M32))
            m.r[2] = 0
        h[0x800490FC] = sound

        def effect(name):
            def f(m):                                   # CreateEffect-like: note the id and the arguments, hand back a zeroed dummy slot
                sp = m.r[29]
                args = [m.r[4], m.r[5], m.r[6], m.r[7]] + [m.peek(sp + 0x10 + 4 * i) for i in range(6)]
                m.journal.append((m.tick_no, name, [x & M32 for x in args]))
                m.zero(m.effect_slot, 0x300)
                m.r[2] = m.effect_slot
            return f
        h[0x8003BDD8] = effect('effect_3BDD8')
        h[0x8003BE74] = effect('effect_3BE74')

        def spawn(m):                                   # SpawnWarpEntity: noted, returns 0
            sp = m.r[29]
            args = [m.r[4], m.r[5], m.r[6], m.r[7]] + [m.peek(sp + 0x10 + 4 * i) for i in range(4)]
            m.journal.append((m.tick_no, 'spawn_39F58', [x & M32 for x in args]))
            m.r[2] = 0
        h[0x80039F58] = spawn

        def noeffect(name):
            def f(m):
                m.journal.append((m.tick_no, name, None))
                m.r[2] = 0
            return f
        h[0x80038E84] = noeffect('pass_38E84_effects_stub')
        h[0x80039300] = noeffect('pass_39300_hits_stub')
