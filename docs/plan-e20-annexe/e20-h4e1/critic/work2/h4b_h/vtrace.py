"""vtrace.py - replayable vectors over the REAL MIPS code of ALUN_CD.EXE (read-only use of the binary).

TM       : a Machine whose data accesses are recorded.  Instruction fetches are NOT recorded.
run(...) : run one call on a prepared TM, return a vector dict:
              {"fn", "args"[a0..a3], "reads":[[addr,size,value]...] (data words/halfwords/bytes read BEFORE the function wrote those bytes,
               first occurrence kept, in execution order), "writes":[[addr,size,value]...] (final value per (addr,size), execution order),
               "ret": v0, "ret_v1"}
SparseRun: replay a vector from an EMPTY memory: the real code runs, instruction fetch comes from the EXE image, every DATA read must hit a
           byte listed in the vector (otherwise the vector is incomplete: raises UnlistedRead).  Returns the writes it produced.
Stubbed callees (hooks) are not supported in SparseRun: vectors of functions that call stubs (sound, spawn) are recorded with hooks=[...] and the
journal of each stub call is part of the vector ("journal"); SparseRun re-installs the same hook behaviour only for the listed addresses.
"""
import os
import struct
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from machine import Machine, M32, s32
import emu as _emu

CODE_LO, CODE_HI = 0x80010000, 0x800CA800
FRAME_LO, FRAME_HI = 0x801FFE00, 0x801FFF00      # the callee's own stack frame (sp = 0x801FFF00 at the call): saved registers, not part of the contract


def in_frame(a):
    return FRAME_LO <= a < FRAME_HI


class UnlistedRead(Exception):
    pass


class StopRun(Exception):
    """raised by a hook placed at an instruction address to end a run in the middle of a function (state kept)"""
    pass


def _stop(m):
    raise StopRun()


class TM(Machine):
    def __init__(self):
        super().__init__()
        self.rec = None            # list of ('r'|'w', addr, size, value) while recording
        self.wrote = None          # set of byte addresses written during the recorded call
        self.watch_uninit = False

    # data accesses -------------------------------------------------------------------
    def _log_r(self, a, n, v):
        if self.rec is not None:
            self.rec.append(('r', a & M32, n, v))

    def _log_w(self, a, n, v):
        if self.rec is not None:
            self.rec.append(('w', a & M32, n, v))

    def rb(self, a):
        v = _emu.Emu.rb(self, a); self._log_r(a, 1, v); return v

    def rh(self, a):
        v = _emu.Emu.rh(self, a); self._log_r(a, 2, v); return v

    def rw(self, a):
        v = _emu.Emu.rw(self, a); self._log_r(a, 4, v); return v

    def wb(self, a, v):
        _emu.Emu.wb(self, a, v); self._log_w(a, 1, v & 0xFF)

    def wh(self, a, v):
        _emu.Emu.wh(self, a, v); self._log_w(a, 2, v & 0xFFFF)

    def ww(self, a, v):
        _emu.Emu.ww(self, a, v); self._log_w(a, 4, v & M32)

    def fetch(self, pc):
        return struct.unpack_from('<I', self.ram, pc & 0x1FFFFFFF)[0]

    def step(self):
        pc = self.pc
        w = self.fetch(pc)
        br = self.exec1(pc, w)
        if br is None:
            self.pc = pc + 4
        else:
            dw = self.fetch(pc + 4)
            self.exec1(pc + 4, dw)
            self.pc = br[1] if br[1] is not None else pc + 8


def summarize(rec):
    """reads = accesses to bytes not written earlier by the call; writes = final value per (addr,size)"""
    written = set()
    reads, seen = [], set()
    writes = {}
    order = []
    for kind, a, n, v in rec:
        span = range(a, a + n)
        if kind == 'r':
            if any(b not in written for b in span) and (a, n) not in seen:
                seen.add((a, n))
                reads.append([a, n, v])
        else:
            for b in span:
                written.add(b)
            if (a, n) not in writes:
                order.append((a, n))
            writes[(a, n)] = v
    return reads, [[a, n, writes[(a, n)]] for a, n in order if not in_frame(a)]


def run(m, fn, args=(), limit=2_000_000, stop_at=None):
    """run fn(args) on the TM m; return the vector (memory was prepared by the caller, with tracing off).
    stop_at: an address inside the function where the run ends (the vector then has no 'ret')"""
    m.rec = []
    m.journal_start = len(m.journal)
    v0 = None
    if stop_at is not None:
        m.hooks[stop_at] = _stop
    try:
        v0 = m.call(fn, *args, limit=limit)
    except StopRun:
        v0 = None
    finally:
        if stop_at is not None:
            m.hooks.pop(stop_at, None)
    rec, m.rec = m.rec, None
    reads, writes = summarize(rec)
    vec = {"fn": "%#010x" % fn, "args": [a & M32 for a in args], "reads": reads, "writes": writes}
    if stop_at is not None:
        vec["stop_at"] = "%#010x" % stop_at
    else:
        vec["ret"] = v0 & M32
        vec["ret_v1"] = m.r[3] & M32
    jr = m.journal[m.journal_start:]
    if jr:
        vec["journal"] = [list(x) for x in jr]
    return vec


class SparseRun(Machine):
    """replay a vector from an empty memory: only the bytes the vector lists can be read as data"""

    def __init__(self, vec):
        super().__init__()
        self.watch_uninit = False
        self.mem = {}
        for a, n, v in vec["reads"]:
            for i in range(n):
                self.mem[a + i] = (v >> (8 * i)) & 0xFF
        self.out = {}
        self.vec = vec

    def _rd(self, a, n):
        v = 0
        for i in range(n):
            b = (a + i) & M32
            if b in self.out:
                x = self.out[b]
            elif b in self.mem:
                x = self.mem[b]
            else:
                raise UnlistedRead("data read of %#010x (size %d) not in the vector (pc %#010x)" % (a, n, self.cur_pc))
            v |= x << (8 * i)
        return v

    def rb(self, a): return self._rd(a, 1)
    def rh(self, a): return self._rd(a, 2)
    def rw(self, a): return self._rd(a, 4)
    def peek(self, a): return self._rd(a, 4)

    def _wr(self, a, n, v):
        for i in range(n):
            self.out[(a + i) & M32] = (v >> (8 * i)) & 0xFF

    def wb(self, a, v): self._wr(a, 1, v)
    def wh(self, a, v): self._wr(a, 2, v)
    def ww(self, a, v): self._wr(a, 4, v)

    def fetch(self, pc):
        return struct.unpack_from('<I', self.ram, pc & 0x1FFFFFFF)[0]

    def step(self):
        pc = self.pc
        w = self.fetch(pc)
        br = self.exec1(pc, w)
        if br is None:
            self.pc = pc + 4
        else:
            dw = self.fetch(pc + 4)
            self.exec1(pc + 4, dw)
            self.pc = br[1] if br[1] is not None else pc + 8


def replay(vec, hooks=None):
    """run the vector's function on an empty memory; return (ok, message). ok iff the final writes (and v0) equal the vector's."""
    m = SparseRun(vec)
    if hooks:
        for k, f in hooks.items():
            m.hooks[k] = f
    fn = int(vec["fn"], 16)
    stop = int(vec["stop_at"], 16) if "stop_at" in vec else None
    if stop is not None:
        m.hooks[stop] = _stop
    try:
        v0 = m.call(fn, *vec["args"])
    except StopRun:
        v0 = None
    except UnlistedRead as e:
        return False, "INCOMPLETE: " + str(e)
    got = {}
    for a, n, v in vec["writes"]:
        got[(a, n)] = v
    for a, n, v in vec["writes"]:
        x = 0
        for i in range(n):
            b = (a + i) & M32
            if b not in m.out:
                return False, "write %#010x not reproduced" % a
            x |= m.out[b] << (8 * i)
        if x != v:
            return False, "write %#010x: got %#x want %#x" % (a, x, v)
    # no extra written byte
    listed = set()
    for a, n, v in vec["writes"]:
        for i in range(n):
            listed.add((a + i) & M32)
    extra = [b for b in m.out if b not in listed and not in_frame(b)]
    if extra:
        return False, "extra written byte %#010x" % extra[0]
    if "ret" in vec and (v0 & M32) != vec["ret"]:
        return False, "ret %#x want %#x" % (v0 & M32, vec["ret"])
    return True, "ok"
