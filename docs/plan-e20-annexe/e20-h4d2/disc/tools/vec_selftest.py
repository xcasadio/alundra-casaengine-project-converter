"""vec_selftest.py - h4d2 discovery (read-only): the SELF-CONTAINED use of the vector files, the way a C# adapter will use them: load "reads" into a sparse memory (nothing else is readable), run the model on it,
compare its writes / journal / return with the recorded ones.  Used (a) to show the vectors are complete for the models (0 mismatch with the right model) and (b) to run a set of MUTANTS of the models
against the vectors (every mutant must be killed by at least one vector; the number of killing vectors is printed) - the proof that the vectors are not vacuous.  Also prints how many vectors tell the
`corrected` mode (inclusive bound, sort from 2, shortest-path turn) from the binary.
usage: python -B vec_selftest.py VECDIR"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import f1model as F

M = 0xFFFFFFFF
VEC = sys.argv[1]
FRAME_LO, FRAME_HI = 0x801FF000, 0x801FFF00


class Unlisted(Exception):
    pass


class SView:
    def __init__(self, vec):
        self.mem = {}
        for a, n, v in vec['reads']:
            for i in range(n):
                self.mem[a + i] = (v >> (8 * i)) & 0xFF
        self.o = {}
        self.j = []
        self.query_calls = []

    def b(self, a):
        a &= M
        if a in self.o:
            return self.o[a]
        if a in self.mem:
            return self.mem[a]
        raise Unlisted(hex(a))

    def g(self, a):
        return self.b(a) | self.b(a + 1) << 8 | self.b(a + 2) << 16 | self.b(a + 3) << 24

    def p(self, a, v):
        for i in range(4):
            self.o[(a + i) & M] = (v >> (8 * i)) & 0xFF


def real_bytes(vec):
    d = {}
    for a, n, v in vec['writes']:
        if FRAME_LO <= a < FRAME_HI:
            continue
        for i in range(n):
            d[(a + i) & M] = (v >> (8 * i)) & 0xFF
    return d


def norm_real(js):
    out = []
    for j in js:
        k, a = j[1], list(j[2])
        if k == 'spawn_39F58':
            out.append(('spawn',) + tuple(a[0:7]))
        elif k == 'effect_3BDD8':
            out.append(('effect',) + tuple(a[0:6]))
        elif k == 'destroy_3A59C':
            out.append(('destroy', a[0], a[1]))
    return out


def run_vector(name, vec, impl):
    """impl(view, vec) -> returns nothing; True when the model agrees with the recorded vector"""
    v = SView(vec)
    try:
        ret = impl(v, vec)
    except Unlisted:
        return False
    except (RecursionError, IndexError, KeyError):
        return False
    if v.o != real_bytes(vec):
        return False
    if ret is not None and (ret & M) != (vec['ret'] & M):
        return False
    if [tuple(x) for x in v.j] != norm_real(vec.get('journal', [])):
        return False
    return True


def dirto_from_vec(view, vec):
    """GetDirectionToTarget is real code inside C63's vectors: the adapter of a C# test calls the DLL's own table-driven function; here the vector's own reads carry its table, so we run the REAL function"""
    raise NotImplementedError


# ---- the models as implementations over a vector -------------------------------------------------------------------------------------------------------------------------------------------
def impl_driver(view, vec, mod=None):
    sr = {int(k): v for k, v in vec.get('stub_spawn_ret', {}).items()}
    return (F.driver if mod is None else mod)(view, lambda t: sr.get(t, 0))


def impl_cancel(view, vec, mod=None):
    (F.cancel_fire1 if mod is None else mod)(view)


def impl_e3(view, vec, mod=None):
    (F.e3 if mod is None else mod)(view, int(vec['args'][0]))


_dirto_machine = None


def real_dirto(dx, dy):
    global _dirto_machine
    if _dirto_machine is None:
        sys.path.insert(0, os.path.join(HERE, '..', 'vech'))
        import vtrace as V
        _dirto_machine = V.TM()
    return _dirto_machine.call(0x8002E814, dx & M, dy & M) & M


def impl_c63(view, vec, mod=None, mode='binary'):
    F.c63(view, int(vec['args'][0]), real_dirto, mode=mode)


def load(name):
    return json.load(open(os.path.join(VEC, name)))['vectors']


def patched(fn, src_from, src_to):
    """build a mutated copy of a model function by replacing a source line (text mutation of f1model.py)"""
    import inspect
    import types
    src = inspect.getsource(fn)
    if src_from not in src:
        raise ValueError('mutation site not found: ' + src_from)
    ns = dict(F.__dict__)
    exec(src.replace(src_from, src_to), ns)
    return ns[fn.__name__], ns


def main():
    suites = {
        'driver': (load('vec_fire1_driver_80035A84.json'), impl_driver),
        'cancel': (load('vec_cancel_fire1_80034D2C.json'), impl_cancel),
        'e3': (load('vec_e3_8007EDA0.json'), impl_e3),
        'c63': (load('vec_c63_8007AC60.json'), impl_c63),
    }
    for nm, (vecs, impl) in suites.items():
        print('%-7s binary_only vectors: %d of %d' % (nm, sum(1 for v in vecs if v.get('binary_only')), len(vecs)))
        ok = sum(1 for v in vecs if run_vector(nm, v, impl))
        print('%-7s right model: %d/%d vectors agree' % (nm, ok, len(vecs)))
    # mutants: (suite, label, function to patch, from, to)
    muts = [
        ('driver', 'counter & 7 -> & 15', F.driver, 'S.g(COUNTER) & 7', 'S.g(COUNTER) & 15'),
        ('driver', 'draw range 32 -> 16', F.driver, 'draw(S, 0x20)', 'draw(S, 0x10)'),
        ('driver', 'ball Z + 16 px -> + 8 px', F.driver, '+ 0x100000) & M', '+ 0x80000) & M'),
        ('driver', 'ForceZ 0x80000 -> 0x40000', F.driver, "S.p(q + 0xB8, 0x80000)", "S.p(q + 0xB8, 0x40000)"),
        ('driver', '+0x274 = 0x10 -> 0x0F', F.driver, "S.p(q + 0x274, 0x10)", "S.p(q + 0x274, 0x0F)"),
        ('driver', '+0x278 not cleared', F.driver, "        S.p(q + 0x278, 0)\n", "        pass\n"),
        ('driver', 'column death -> anim 0x33', F.driver, "S.p(HERO + 0x88, 0x34)", "S.p(HERO + 0x88, 0x33)"),
        ('driver', 'identity ignored', F.driver, "if S.g(p + 4) != S.g(IDENT):", "if False:"),
        ('driver', 'phase 1 without the guard', F.driver, "    # phase 1 (also reached from phase 0 in the same tick)\n    if guard(S):\n        return 1\n", "    # phase 1\n"),
        ('driver', 'no chaining of phase 1 at birth', F.driver, "    # phase 1 (also reached from phase 0 in the same tick)", "    return 0\n    # phase 1"),
        ('driver', 'null column not returning 1', F.driver, "        if p == 0:\n            return 1\n", "        if p == 0:\n            return 0\n"),
        ('driver', 'phase 2 waits for 0x33', F.driver, "return 0 if S.g(HERO + 0x88) == 0x34 else 1", "return 0 if S.g(HERO + 0x88) == 0x33 else 1"),
        ('driver', 'guard accepts 0x34', F.guard, "((anim - 0x32) & M) >= 2", "((anim - 0x32) & M) >= 3"),
        ('driver', 'guard ignores slope 4', F.guard, "if slope == 4 or", "if False or"),
        ('cancel', 'type 20 / 23 swapped', F.cancel_fire1, "if t == 0x14:", "if t == 0x17:"),
        ('cancel', 'effect variant 2 -> 1', F.cancel_fire1, "('effect', 0, 0x13, 2,", "('effect', 0, 0x13, 1,"),
        ('cancel', 'effect id 19 -> 18', F.cancel_fire1, "('effect', 0, 0x13, 2,", "('effect', 0, 0x12, 2,"),
        ('cancel', 'balls -> status 4', F.cancel_fire1, "S.p(e + 0x10, 3)", "S.p(e + 0x10, 4)"),
        ('cancel', 'ctl20 not cleared', F.cancel_fire1, "                S.p(e + 0x20, 0)\n", "                pass\n"),
        ('cancel', 'loop exclusive of the last slot', F.cancel_fire1, "if count < i:", "if count <= i:"),
        ('cancel', 'status 3 balls also cancelled', F.cancel_fire1, "if S.g(e + 0x10) == 2:", "if S.g(e + 0x10) in (2, 3):"),
        ('e3', 'no | 0x40', F.e3, "| 0x40)", "| 0x00)"),
        ('e3', '~0x10 instead of ~0x30', F.e3, "(0xFFFFFFCF)", "(0xFFFFFFEF)"),
        ('e3', 'destroy without the anim end', F.e3, "and S.g(a0 + 0xAC) == 1", ""),
        ('c63', 'delay not decremented', F.c63, "S.p(a0 + 0x274, (d - 1) & M)", "S.p(a0 + 0x274, d)"),
        ('c63', 'ForceZ -3 px -> -2 px', F.c63, "S.p(a0 + 0xB8, 0xFFFD0000)", "S.p(a0 + 0xB8, 0xFFFE0000)"),
        ('c63', 'no-target draw 9 -> 8', F.c63, "draw(S, 9)", "draw(S, 8)"),
        ('c63', 'type & 0xF -> & 0x7', F.c63, "S.b(atk) & 0xF", "S.b(atk) & 0x7"),
        ('c63', 'mask ClassA 1 -> 5', F.c63, "query(S, a0, 1, attr, mode)", "query(S, a0, 5, attr, mode)"),
        ('c63', 'identity not stored', F.c63, "S.p(a0 + 0x27C, S.g(tp + 4))", "pass"),
        ('c63', 'dead target not forgotten', F.c63, "S.p(a0 + 0x278, 0)\n            return", "return"),
        ('c63', '+0x274 = 2 -> 3', F.c63, "S.p(a0 + 0x274, 2)", "S.p(a0 + 0x274, 3)"),
        ('c63', 'turn step 4 -> 3', F.turn_binary, "cur - 4", "cur - 3"),
        ('c63', 'turn threshold < 4 -> < 3', F.turn_binary, "(cur - tgt) < 4", "(cur - tgt) < 3"),
        ('c63', 'turn up step 4 -> 5', F.turn_binary, "tgt if (tgt - cur) < 4 else cur + 4", "tgt if (tgt - cur) < 4 else cur + 5"),
        ('c63', 'first result: last instead of first', F.c63, "tp = ptrs[0]", "tp = ptrs[-1]"),
    ]
    killed_all = True
    for suite, label, fn, a, b in muts:
        vecs, impl = suites[suite]
        newfn, ns = patched(fn, a, b)
        # install the mutated function where the model looks it up
        old = getattr(F, fn.__name__)
        setattr(F, fn.__name__, newfn)
        # the other functions of F resolve names through F's globals: they see the mutated one when it is called by name
        try:
            killers = [v['id'] for v in vecs if not run_vector(suite, v, impl)]
        finally:
            setattr(F, fn.__name__, old)
        print('%-7s %-40s killed by %4d of %d vectors%s' % (suite, label, len(killers), len(vecs), '' if killers else '   *** SURVIVES'))
        if not killers:
            killed_all = False
    # the corrected mode versus the binary on the C63 vectors
    vecs = suites['c63'][0]
    diff = [v for v in vecs if not run_vector('c63', v, lambda view, vec: F.c63(view, int(vec['args'][0]), real_dirto, mode='corrected'))]
    turn = [v for v in vecs if v.get('kind') == 'turn']
    dt = [v for v in diff if v.get('kind') == 'turn']
    print('corrected mode (inclusive bound + sort from 2 + shortest-path turn) differs from the binary on %d of %d C63 vectors (%d of %d turn-grid vectors, %d random)' % (len(diff), len(vecs), len(dt), len(turn), len(diff) - len(dt)))
    print('ALL MUTANTS KILLED' if killed_all else 'SOME MUTANT SURVIVES')


if __name__ == '__main__':
    main()
