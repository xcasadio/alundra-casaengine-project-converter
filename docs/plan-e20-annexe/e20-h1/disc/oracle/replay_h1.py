"""replay_h1.py - E20.h1 discovery: replays EVERY vector of ./vec from an EMPTY memory on the real MIPS code (instruction fetch from the EXE image, every data read must
be a byte listed in the vector, the callees named in the vector's "stubs" are hooks that return the listed values), and checks the writes, the return value and the journal of the
stub calls bit for bit.  It also reports the instruction coverage of the functions under test.   usage: python -B replay_h1.py [vecdir]
Read-only on the binary; writes nothing."""
import glob
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from h1rig import *          # noqa: E402,F403
import vtrace as V           # noqa: E402
from machine import M32      # noqa: E402

# function bodies: [start, end) (from the listings: the jr ra and its delay slot end each function)
BODIES = {
    'herb_heal 0x80032E50': (0x80032E50, 0x80032EEC),
    'water1_heal 0x80032F84': (0x80032F84, 0x800330FC),
    'spotion_heal 0x800330FC': (0x800330FC, 0x80033274),
    'lpotion_heal 0x80033274': (0x80033274, 0x800333AC),
    'elixir_heal 0x800336F0': (0x800336F0, 0x8003382C),
    'cureale_heal 0x80033A2C': (0x80033A2C, 0x80033D34),
    'herb_start 0x80034680': (0x80034680, 0x800346F0),
    'lpotion_start 0x800346F0': (0x800346F0, 0x80034760),
    'elixir_start 0x80034760': (0x80034760, 0x800347D4),
    'cureale_start 0x800347D4': (0x800347D4, 0x80034870),
    'spotion_start 0x80034870': (0x80034870, 0x800348E0),
    'dispatch 0x8003499C': (0x8003499C, 0x80034ACC),
    'useitem 0x8004E5C4': (0x8004E5C4, 0x8004E640),
    'tryuseitem 0x8002ED64': (0x8002ED64, 0x8002EDBC),
}


def replay_one(vec):
    m = V.SparseRun(vec)
    m.hooks.clear()
    calls = []
    stubs = {int(a, 16): list(r) for a, r in vec.get('stubs', {}).items()}
    pending = install_stubs(m, stubs, calls)
    fn = int(vec['fn'], 16)
    try:
        v0 = m.call(fn, *vec['args'])
    except V.UnlistedRead as e:
        return False, 'INCOMPLETE: ' + str(e), m.cov
    except RuntimeError as e:
        return False, 'RUNTIME: ' + str(e), m.cov
    if any(pending.values()):
        return False, 'stub values left unused: %s' % {hex(a): r for a, r in pending.items() if r}, m.cov
    if calls != vec['calls']:
        return False, 'calls differ: got %s want %s' % (calls, vec['calls']), m.cov
    listed = set()
    for a, n, v in vec['writes']:
        x = 0
        for i in range(n):
            b = (a + i) & M32
            listed.add(b)
            if b not in m.out:
                return False, 'write %#010x not reproduced' % a, m.cov
            x |= m.out[b] << (8 * i)
        if x != v:
            return False, 'write %#010x: got %#x want %#x' % (a, x, v), m.cov
    extra = [b for b in m.out if b not in listed and not V.in_frame(b)]
    if extra:
        return False, 'extra written byte %#010x' % extra[0], m.cov
    if 'ret' in vec and (v0 & M32) != vec['ret']:
        return False, 'ret %#x want %#x' % (v0 & M32, vec['ret']), m.cov
    return True, 'ok', m.cov


def main(vecdir):
    tot = bad = 0
    cov = {}                     # file -> pcs
    allcov = set()
    gamecov = set()
    for f in sorted(glob.glob(os.path.join(vecdir, 'vec_*.json'))):
        d = json.load(open(f))
        nb = 0
        c = set()
        for v in d['vectors']:
            ok, msg, pcs = replay_one(v)
            c |= pcs
            if v['class'] == 'game':
                gamecov |= pcs
            if not ok:
                nb += 1
                if nb <= 3:
                    print('  FAIL', os.path.basename(f), v.get('id'), v['label'], msg)
        allcov |= c
        cov[os.path.basename(f)] = c
        tot += len(d['vectors'])
        bad += nb
        print('%-40s %5d vectors   reads=%6d words writes=%6d words   replay: %s' % (os.path.basename(f), len(d['vectors']), sum(len(v['reads']) for v in d['vectors']),
              sum(len(v['writes']) for v in d['vectors']), 'all ok' if nb == 0 else '%d FAIL' % nb))
    print('TOTAL %d vectors, %d failures' % (tot, bad))
    print()
    print('COVERAGE (instructions of each function executed by the replay; all vectors / game vectors only)')
    for name, (lo, hi) in BODIES.items():
        pcs = set(range(lo, hi, 4))
        miss = sorted(pcs - allcov)
        missg = sorted(pcs - gamecov)
        print('  %-26s %3d/%3d (all)  %3d/%3d (game)   missing(all): %s' % (name, len(pcs) - len(miss), len(pcs), len(pcs) - len(missg), len(pcs), ' '.join('%08x' % x for x in miss) or '-'))
    return 1 if bad else 0


if __name__ == '__main__':
    sys.exit(main(sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, 'vec')))
