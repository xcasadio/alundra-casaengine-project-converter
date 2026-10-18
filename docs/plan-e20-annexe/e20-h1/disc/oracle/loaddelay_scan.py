"""loaddelay_scan.py - E20.h1 discovery: static scan for the R3000 load-delay hazard in the functions of this slice.  The interpreter used for the vectors gives a loaded register to the very next
instruction; the hardware gives it the OLD value (the delay slot of a load).  A hazard is an instruction that READS the register a load wrote in the instruction just before it (in program order, and
across a branch delay slot).  The scan lists every such pair in the function bodies; none means the interpreter and the hardware agree on these functions.
usage: python -B loaddelay_scan.py"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from bdis import u32       # noqa: E402

BODIES = {
    'tryuseitem': (0x8002ED64, 0x8002EDBC), 'herb_heal': (0x80032E50, 0x80032EEC), 'water1_heal': (0x80032F84, 0x800330FC), 'spotion_heal': (0x800330FC, 0x80033274),
    'lpotion_heal': (0x80033274, 0x800333AC), 'elixir_heal': (0x800336F0, 0x8003382C), 'cureale_heal': (0x80033A2C, 0x80033D34), 'herb_start': (0x80034680, 0x800346F0),
    'lpotion_start': (0x800346F0, 0x80034760), 'elixir_start': (0x80034760, 0x800347D4), 'cureale_start': (0x800347D4, 0x80034870), 'spotion_start': (0x80034870, 0x800348E0),
    'dispatch': (0x8003499C, 0x80034ACC), 'useitem': (0x8004E5C4, 0x8004E640), 'mp_getters_setters': (0x8004DDDC, 0x8004DDF4), 'getmp': (0x8004DEA4, 0x8004DEBC), 'setmp': (0x8004DEBC, 0x8004DF10),
    'sethp': (0x8004DD30, 0x8004DD80), 'createeffect': (0x8003BDD8, 0x8003BE74), 'frame_fn_startselect': (0x8002BAEC, 0x8002BD60),
}


def regs_read(w):
    op = w >> 26
    rs = (w >> 21) & 31
    rt = (w >> 16) & 31
    fn = w & 63
    if op == 0:
        if fn in (0, 2, 3):
            return {rt}
        if fn in (8, 9, 17, 19):
            return {rs}
        if fn in (16, 18, 12, 13):
            return set()
        return {rs, rt}
    if op in (2, 3, 15):
        return set()
    if op in (4, 5):
        return {rs, rt}
    if op in (6, 7, 1):
        return {rs}
    if op in (40, 41, 43, 42, 46):       # stores read the base and the data register
        return {rs, rt}
    return {rs}


LOADS = {32, 33, 34, 35, 36, 37, 38}


def main():
    total = 0
    for name, (lo, hi) in BODIES.items():
        hz = []
        for a in range(lo, hi - 4, 4):
            w = u32(a)
            if (w >> 26) in LOADS:
                rt = (w >> 16) & 31
                if rt and rt in regs_read(u32(a + 4)):
                    hz.append(a)
        total += len(hz)
        print('%-22s [%08x,%08x) hazards: %s' % (name, lo, hi, ' '.join('%08x' % x for x in hz) or 'none'))
    print('TOTAL hazards: %d' % total)
    return 0


if __name__ == '__main__':
    sys.exit(main())
