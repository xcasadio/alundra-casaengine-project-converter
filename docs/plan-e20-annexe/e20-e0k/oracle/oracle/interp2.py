"""interp2.py - D1: a SECOND, independent interpreter for the same passes.

The oracle's machine (machine.py) runs on emu.py.  The E19 tools also contain mips.py (a different interpreter, written separately: decoded fields,
delay slots handled differently, memory as an overlay).  This module drives the SAME memory (a Machine object, used only as a memory) through mips.Cpu,
so that the two interpreters can be compared tick by tick on the whole pipeline: `diff2.py`.

mips.py lacks lwl/lwr/swl/swr, mthi/mtlo, div/divu: added here by subclassing, written bytewise (the machine.py version is shift-based), so that the
two implementations of these operations are independent too.
"""
import os
import sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import mips
from mips import M32, s32
import world as W

SENT = 0xFFFFFFF0
SP = 0x801FFF00


class Cpu2(mips.Cpu):
    def alu_r(self, ins):
        fn = ins & 63
        if fn in (0x11, 0x13, 0x1A, 0x1B):
            r = self.r
            rs, rt = (ins >> 21) & 31, (ins >> 16) & 31
            if fn == 0x11:
                self.hi = r[rs]
            elif fn == 0x13:
                self.lo = r[rs]
            elif fn == 0x1A:
                a, b = s32(r[rs]), s32(r[rt])
                if b == 0:
                    raise RuntimeError('div by zero at 0x%08x' % self.pc)
                q = abs(a) // abs(b)
                if (a < 0) != (b < 0):
                    q = -q
                self.lo, self.hi = q & M32, (a - q * b) & M32
            else:
                a, b = r[rs] & M32, r[rt] & M32
                self.lo, self.hi = (a // b) & M32, (a % b) & M32
            return
        super().alu_r(ins)

    def alu_i(self, ins, op, rs, rt, simm, imm):
        if op in (0x22, 0x26, 0x2A, 0x2E):
            r, m = self.r, self.m
            addr = (r[rs] + simm) & M32
            k = addr & 3
            al = addr & ~3
            if op == 0x22:      # lwl: bytes al..addr -> the top k+1 bytes of rt
                v = r[rt]
                for j in range(k + 1):
                    sh = 8 * (3 - k + j)
                    v = (v & ~(0xFF << sh)) | (m.rb(al + j) << sh)
                if rt: r[rt] = v & M32
            elif op == 0x26:    # lwr: bytes addr..al+3 -> the low 4-k bytes of rt
                v = r[rt]
                for j in range(4 - k):
                    sh = 8 * j
                    v = (v & ~(0xFF << sh)) | (m.rb(addr + j) << sh)
                if rt: r[rt] = v & M32
            elif op == 0x2A:    # swl: the top k+1 bytes of rt -> al..addr
                for j in range(k + 1):
                    m.wb(al + j, (r[rt] >> (8 * (3 - k + j))) & 0xFF)
            else:               # swr: the low 4-k bytes of rt -> addr..al+3
                for j in range(4 - k):
                    m.wb(addr + j, (r[rt] >> (8 * j)) & 0xFF)
            return
        super().alu_i(ins, op, rs, rt, simm, imm)


    def step(self):
        """c1 addition: mips.Cpu stubs only `jal` targets; the events pass calls the sprite handlers through `jalr`, so a stubbed `jalr` target is handled here the same way
        (delay slot first, then the stub, then continue after the delay slot)."""
        pc = self.pc
        ins = self.m.rw(pc)
        if (ins >> 26) == 0 and (ins & 63) == 9:
            tgt = self.r[(ins >> 21) & 31] & M32
            if tgt in self.stubs:
                self.calls.append(tgt)
                save = self.pc
                self.pc = pc + 4
                self.step_plain()
                self.pc = save
                rd = (ins >> 11) & 31
                if rd:
                    self.r[rd] = (pc + 8) & M32
                st = self.stubs[tgt]
                self.r[2] = (st(self) if callable(st) else st) & M32
                self.pc = pc + 8
                return
        super().step()


class Shim:
    """what the machine.py hooks expect (m.r, m.peek, m.zero, m.journal, ...), with the registers of the Cpu2 in use."""
    def __init__(self, m, cpu):
        self._m = m
        self.r = cpu.r

    def __getattr__(self, k):
        return getattr(self._m, k)


def stubs_for(m):
    out = {}
    for addr, hook in m.hooks.items():
        out[addr] = (lambda cpu, h=hook: (h(Shim(m, cpu)), cpu.r[2])[1])
    return out


def call2(m, addr, *args, max_steps=5_000_000):
    cpu = Cpu2(m, stubs_for(m))
    cpu.r[29] = SP
    for i, a in enumerate(args):
        cpu.r[4 + i] = a & M32
    saved_pc = m.cur_pc
    cpu.run(addr, SENT, max_steps=max_steps)
    return cpu.r[2]


def tick2(m, pad, hold, skip=(), check_pad=True):
    """W.tick through the second interpreter."""
    pad.update(hold)
    W.write_pad(m, pad)
    if check_pad:
        call2(m, 0x8002E250, W.PAD2, hold)
        got = (m.peek(W.PAD2 + 0x10) & 0xFFFF, m.peek(W.PAD2 + 0x12) & 0xFFFF, m.peek(W.PAD2 + 0x14) & 0xFFFF, m.peek(W.PAD2 + 0x16) & 0xFFFF)
        want = (pad.hold, pad.pressed, pad.released, pad.byint)
        if got != want:
            raise RuntimeError('pad port differs from the binary routine (interpreter 2) at hold 0x%04X: %s %s' % (hold, got, want))
    for a in W.PASSES:
        if a in skip:
            continue
        if a in m.hooks:                     # the two stubbed passes: journalled, not run
            m.hooks[a](Shim(m, Cpu2(m)))
        else:
            call2(m, a)
