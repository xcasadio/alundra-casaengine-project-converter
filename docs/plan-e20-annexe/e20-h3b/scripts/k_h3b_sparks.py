"""k_h3b_sparks.py - E20.h3b T0 (b) (c): the seven sparks of the carried bomb with the VALUES of the draws.  The scenario of scen_bomb1.py (Circle at tick 1, bomb carried then dropped at
the hero's feet); at each tick where the effect stub 0x8003BDD8 was called, prints the tick, the generator before the tick (rng0) and after it (rng1), the arguments of the call (id, x, y, z
words) and the NON-ZERO words of the effect slot the stub hands back (offset from the slot : value), where C62 writes the two drawn values; the stub hands back a non-null slot each time
(the effect is always 'obtained').  Run with pf_run.py to apply the per-frame step.  usage: python -B pf_run.py k_h3b_sparks.py"""
from scen_lib import *
RNG = 0x80098708
m, STATS = mk(0x23)
def hold(t):
    return W.CIRCLE if t == 1 else 0
state = {}
def on_tick(m_, t, rec):
    slots = [x for x in rec['jr'] if x[1] == 'effect_3BDD8']
    if slots:
        sl = {}
        for o in range(0, 0x300, 4):
            v = m_.peek(m_.effect_slot + o)
            if v: sl['%#x' % o] = '%#x' % v
        print('t%3d rng_after %08x  calls %d  args %s  slot %s' % (t, m_.peek(RNG), len(slots), [[hex(a) for a in x[2][:6]] for x in slots], sl))
log = run(m, 260, hold, on_tick=on_tick)
