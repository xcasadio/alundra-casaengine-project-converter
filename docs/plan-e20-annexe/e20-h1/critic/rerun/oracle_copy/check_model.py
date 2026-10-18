"""check_model.py - E20.h1 discovery: runs MY model (model_h1.py) on every vector of ./vec and compares it with what the real code did: the bytes written (exactly the same set),
the return value (for the functions that return one), the journal of the stub calls, and that the model read only bytes the vector lists.   usage: python -B check_model.py [vecdir] [model_module]
Exit code 0 when there is no mismatch."""
import glob
import importlib
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)


def check_vector(M, vec):
    stubs = {int(a, 16): list(r) for a, r in vec.get('stubs', {}).items()}
    c = M.Ctx(vec['reads'], stubs)
    fn = int(vec['fn'], 16)
    try:
        ret = M.FUNCTIONS[fn](c, vec['args'])
    except M.Unlisted as e:
        return 'model read an unlisted byte %s' % e
    except Exception as e:                                  # a mutant can break in any way: that is a mismatch
        return 'model raised %r' % (e,)
    if c.bad:
        return 'the model called a stub the vector does not list'
    if any(c.rets.values()):
        return 'stub values left unused'
    if c.calls != vec['calls']:
        return 'calls differ'
    want = {}
    for a, n, v in vec['writes']:
        for i in range(n):
            want[(a + i) & M.M] = (v >> (8 * i)) & 0xFF
    if want != c.out:
        diff = sorted(set(want.items()) ^ set(c.out.items()))[:2]
        return 'writes differ %s' % diff
    if fn in M.RETURNS_VALUE and (ret & M.M) != vec['ret']:
        return 'ret %#x want %#x' % (ret & M.M, vec['ret'])
    return None


def run_all(M, vecdir, first_only=False, verbose=True):
    tot = bad = 0
    for f in sorted(glob.glob(os.path.join(vecdir, 'vec_*.json'))):
        d = json.load(open(f))
        nb = 0
        for v in d['vectors']:
            r = check_vector(M, v)
            if r:
                nb += 1
                if verbose and nb <= 2:
                    print('  MISMATCH', os.path.basename(f), v['id'], v['label'], r)
                if first_only:
                    return 1, 1
        tot += len(d['vectors'])
        bad += nb
        if verbose:
            print('%-40s %5d vectors   model vs real code: %s' % (os.path.basename(f), len(d['vectors']), 'all equal' if nb == 0 else '%d MISMATCH' % nb))
    return tot, bad


if __name__ == '__main__':
    vecdir = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, 'vec')
    mod = importlib.import_module(sys.argv[2] if len(sys.argv) > 2 else 'model_h1')
    tot, bad = run_all(mod, vecdir)
    print('TOTAL %d vectors, %d mismatches' % (tot, bad))
    sys.exit(1 if bad else 0)
