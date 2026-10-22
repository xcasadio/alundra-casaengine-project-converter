"""Instrumented MIPS-I interpreter for the E19.sm4 value vectors (a thin layer over the annex's emu.py, copied next to it).

It logs every DATA byte the handler reads and writes (instruction fetches are not logged), so the vector generator can map each
address to a named logical field and to a DLL member. An address that maps to nothing is a STOP of the generator.
Read-only use of ALUN_CD.EXE (France) through lib.py.
"""
import os
import struct
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import emu  # noqa: E402


class TEmu(emu.Emu):
    def __init__(self):
        super().__init__()
        self.rlog = None   # set of byte addresses read (data), or None when not tracing
        self.wlog = None   # set of byte addresses written, or None
        self.calls = []    # addresses of the hooked functions that were reached

    # data accesses ---------------------------------------------------------------------------------------------
    def _note_r(self, a, n):
        if self.rlog is not None:
            base = a & 0x1FFFFFFF
            for i in range(n):
                self.rlog.add(base + i)

    def _note_w(self, a, n):
        if self.wlog is not None:
            base = a & 0x1FFFFFFF
            for i in range(n):
                self.wlog.add(base + i)

    def rb(self, a):
        self._note_r(a, 1)
        return emu.Emu.rb(self, a)

    def rh(self, a):
        self._note_r(a, 2)
        return emu.Emu.rh(self, a)

    def rw(self, a):
        self._note_r(a, 4)
        return emu.Emu.rw(self, a)

    def wb(self, a, v):
        self._note_w(a, 1)
        emu.Emu.wb(self, a, v)

    def wh(self, a, v):
        self._note_w(a, 2)
        emu.Emu.wh(self, a, v)

    def ww(self, a, v):
        self._note_w(a, 4)
        emu.Emu.ww(self, a, v)

    # instruction fetch: NOT logged ----------------------------------------------------------------------------
    def step(self):
        pc = self.pc
        w = emu.Emu.rw(self, pc)
        br = self.exec1(pc, w)
        if br is None:
            self.pc = pc + 4
        else:
            dw = emu.Emu.rw(self, pc + 4)
            self.exec1(pc + 4, dw)
            self.pc = br[1] if br[1] is not None else pc + 8


def make_machine():
    m = TEmu()

    def stub(name):
        def f(mm):
            mm.calls.append(name)
        return f

    m.hooks[0x8003C914] = stub('CheckEntityRecord(0x8003C914)')   # id validator: result discarded by every caller; fatal in the binary on an invalid id
    m.hooks[0x8003C840] = stub('DebugMessage(0x8003C840)')        # message + BIOS exit in the binary; reaching it is a STOP of the generator
    return m
