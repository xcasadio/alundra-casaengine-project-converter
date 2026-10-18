"""critic h4d2: EXTRA mutants of the models (not in the 36 of vec_selftest) run against the same vector files: do the vectors kill them?"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.argv = ['x', os.path.join(HERE, '..', '..', 'h4d2', 'vec')]
sys.path.insert(0, os.path.join(HERE, 'tools'))
import vec_selftest as S
F = S.F
suites = {
    'driver': (S.load('vec_fire1_driver_80035A84.json'), S.impl_driver),
    'cancel': (S.load('vec_cancel_fire1_80034D2C.json'), S.impl_cancel),
    'e3': (S.load('vec_e3_8007EDA0.json'), S.impl_e3),
    'c63': (S.load('vec_c63_8007AC60.json'), S.impl_c63),
}
muts = [
 ('c63', 'ForceZ written before the delay test (also while the delay runs)', F.c63,
  "    d = S.g(a0 + 0x274)\n    if d != 0:", "    d = S.g(a0 + 0x274)\n    S.p(a0 + 0xB8, 0xFFFD0000)\n    if d != 0:"),
 ('c63', '+0x274 = 2 also when the direction is unchanged', F.c63,
  "    if nd is None:\n        return\n", "    if nd is None:\n        S.p(a0 + 0x274, 2)\n        return\n"),
 ('c63', 'target forgotten also when its status is 3 only (kept when status 2 or 3)', F.c63,
  "if S.g(tp + 0x10) != 2 or", "if S.g(tp + 0x10) not in (2, 3) or"),
 ('c63', 'turn deferred: no turn in the acquisition tick', F.c63,
  "        S.p(a0 + 0x27C, S.g(tp + 4))\n", "        S.p(a0 + 0x27C, S.g(tp + 4))\n        return\n"),
 ('c63', 'target identity compared with the ball id instead of +0x27C', F.c63,
  "S.g(tp + 4) != S.g(a0 + 0x27C)", "S.g(tp + 4) != S.g(a0 + 4)"),
 ('c63', 'delay decremented by 2', F.c63, "S.p(a0 + 0x274, (d - 1) & M)", "S.p(a0 + 0x274, (d - 2) & M)"),
 ('c63', 'no-target draw stored without the draw (keeps the generator)', F.c63, "S.p(a0 + 0x274, draw(S, 9))", "S.p(a0 + 0x274, 4)"),
 ('driver', 'counter read AFTER the increment (c+1) & 7', F.driver, "S.g(COUNTER) & 7", "(S.g(COUNTER) + 1) & 7"),
 ('driver', 'draw consumed only if the ball is born', F.driver, "    d = draw(S, 0x20)\n    q = spawn_ret(0x17)", "    q = spawn_ret(0x17)\n    d = draw(S, 0x20) if q else 0"),
 ('driver', 'column pointer stored only when non null', F.driver, "        S.p(PTR, p)\n        if p == 0:", "        if p: S.p(PTR, p)\n        if p == 0:"),
 ('driver', 'phase advanced before the identity is stored (identity from column of the previous cast)', F.driver, "        S.p(IDENT, ident)", "        pass"),
 ('driver', 'guard result ignored in phase 1 (cancel done, spell goes on)', F.driver, "    if guard(S):\n        return 1\n    p = S.g(PTR)", "    guard(S)\n    p = S.g(PTR)"),
 ('cancel', 'effect Z read from +0x118 instead of +0x11C', F.cancel_fire1, "S.g(e + 0x11C)))\n                destroy_nofx", "S.g(e + 0x118)))\n                destroy_nofx"),
 ('cancel', 'balls cancelled by position instead of type 23 (any type != 20 at status 2)', F.cancel_fire1, "            if t == 0x17:", "            if t != 0x14:"),
 ('cancel', 'column destroyed before the effect is created', F.cancel_fire1, "                S.j.append(('effect', 0, 0x13, 2, S.g(e + 0x114), S.g(e + 0x118), S.g(e + 0x11C)))\n                destroy_nofx(S, e)", "                destroy_nofx(S, e)\n                S.j.append(('effect', 0, 0x13, 2, S.g(e + 0x114), S.g(e + 0x118), S.g(e + 0x11C)))"),
 ('cancel', 'count read as unsigned (count<=0 not tested)', F.cancel_fire1, "    if count <= 0:\n        return\n", ""),
 ('e3', 'destroy when anim==1 only (no +0xAC)', F.e3, "if S.g(a0 + 0x88) == 1 and S.g(a0 + 0xAC) == 1:", "if S.g(a0 + 0x88) == 1:"),
 ('e3', 'destroy when +0xAC==1 only (no anim==1)', F.e3, "if S.g(a0 + 0x88) == 1 and S.g(a0 + 0xAC) == 1:", "if S.g(a0 + 0xAC) == 1:"),
 ('e3', 'anim set to 2 instead of 1', F.e3, "S.p(a0 + 0x88, 1)", "S.p(a0 + 0x88, 2)"),
]
alive = []
for suite, label, fn, a, b in muts:
    vecs, impl = suites[suite]
    try:
        newfn, ns = S.patched(fn, a, b)
    except ValueError as e:
        print('SITE NOT FOUND', label, e); continue
    old = getattr(F, fn.__name__)
    setattr(F, fn.__name__, newfn)
    try:
        killers = [v['id'] for v in vecs if not S.run_vector(suite, v, impl)]
    finally:
        setattr(F, fn.__name__, old)
    print('%-7s %-85s killed by %4d of %d vectors%s' % (suite, label, len(killers), len(vecs), '' if killers else '   *** SURVIVES'))
    if not killers: alive.append(label)
print('SURVIVORS:', alive)
