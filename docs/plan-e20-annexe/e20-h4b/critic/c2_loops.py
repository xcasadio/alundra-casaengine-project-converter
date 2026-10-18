"""c2_loops.py - DYNAMIC proof of the slot-loop bounds of every reader of [0x80134360]: each function is run in the critic's interpreter
with count=K (K=10 and K=1 and K=0) and EVERY entity slot (0..63) filled with status 2; the slots whose bytes are read are recorded
(callees outside [fn, fn_end) are not executed: they return 0).  max touched slot == K  -> inclusive loop ; == K-1 -> exclusive ; min touched slot -> start (0 or 1)."""
import sys
from cmips import *

HERO = 0x80127D30
STR = 0x294
COUNT = 0x80134360


class Cpu2(Cpu):
    def __init__(self, mem, lo, hi, **kw):
        super().__init__(mem, **kw)
        self.lo, self.hi_ = lo, hi
        self.ext_calls = 0

    def exec(self, ins, pc):
        op = ins >> 26
        fn = ins & 63
        if op == 3:
            tgt = (pc & 0xF0000000) | ((ins & 0x3FFFFFF) << 2)
            if not (self.lo <= tgt < self.hi_):
                self.ext_calls += 1
                self.r[2] = 0; self.r[3] = 0
                self.setr(31, pc + 8)
                return None  # skip the call: the delay slot executes as a plain instruction next
        if op == 0 and fn == 9:
            self.ext_calls += 1
            self.r[2] = 0; self.r[3] = 0
            return None
        return super().exec(ins, pc)


def run_fn(fn, end, K, args=(0, 0, 0, 0), status=2, extra=None, maxs=400000):
    ram = {}

    def p(a, v, n=4):
        for i in range(n):
            ram[a + i] = (v >> (8 * i)) & 0xFF
    for s in range(64):
        e = HERO + s * STR
        p(e, s)
        p(e + 0x10, status)
    p(COUNT, K & M)
    if extra:
        for a, v, n in extra:
            p(a, v, n)
    m = Mem(ram, strict=False)
    touched = set()
    orig = m.rd

    def rd(a, n):
        if HERO <= a < HERO + 64 * STR:
            touched.add((a - HERO) // STR)
        return orig(a, n)
    m.rd = rd
    c = Cpu2(m, fn, end)
    try:
        c.run(fn, args, max_steps=maxs)
        ok = True
    except RuntimeError as ex:
        ok = False
    return touched, ok, c


FUNCS = [
    ("events 0x800386D0 (2 loops)", 0x800386D0, 0x80038998),
    ("counters 0x80038998", 0x80038998, 0x80038A00 + 0x300),
    ("effects 0x80038E84", 0x80038E84, 0x80039300),
    ("cancel earth2 0x80034BDC", 0x80034BDC, 0x80034C54),
    ("cancel water 0x80034C54", 0x80034C54, 0x80034D2C),
    ("cancel fire1 0x80034D2C", 0x80034D2C, 0x80034E08),
    ("cancel fire2 0x80034E08", 0x80034E08, 0x80034EC4),
    ("cancel wind2 0x80034EC4", 0x80034EC4, 0x800350C0),
    ("item start 0x80034320", 0x80034320, 0x800344F8 + 0x40),
    ("hero interact 0x8002EEAC", 0x8002EEAC, 0x8002EFC0),
    ("lists 0x800384F4", 0x800384F4, 0x80038634),
    ("query 0x8003AF70", 0x8003AF70, 0x8003B0E8),
]

if __name__ == "__main__":
    for name, fn, end in FUNCS:
        row = []
        for K in (10, 3, 1, 0):
            t, ok, c = run_fn(fn, end, K, args=(HERO, 1, 0, 0))
            row.append((K, (min(t), max(t)) if t else None, ok))
        print("%-32s" % name, row)
