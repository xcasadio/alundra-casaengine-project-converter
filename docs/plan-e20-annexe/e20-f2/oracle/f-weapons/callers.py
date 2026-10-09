"""callers.py - E20.f2 T2: WHO asks each sound and each effect of the flail probes (the return address of the stubbed call), on the same scenarios as probe.py ('flail').

probe.py journals `sound(N)` and `effect_3BDD8(...)` without their caller. This script wraps the two stubs (0x800490FC PlaySoundEffect, 0x8003BDD8 CreateEffect) to note the return
address, and writes out/F-sound-callers.txt: one line per call, `probe tick kind id ra origin`, origin being
  CreatePlayerAnimationEffects  when the return address is inside 0x8002FB14-0x80030678 (the function E20.f2 enters in the DLL as a skeleton),
  UpdateAnimation               when it is 0x80038BD0 (the sound of the animation set, played at every switch; id 0 = no sound),
  other                         otherwise.
It changes nothing in the oracle's machine (the wrappers call the original stubs). Writes only into out/."""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, 'tools'))
import attack as A
import world as W

SQ, CR, RT = W.SQUARE, W.CROSS, W.RIGHT
OUT = os.path.join(HERE, 'out')
os.makedirs(OUT, exist_ok=True)
LOG = []


def hc(m):
    m.poke(W.ent(0) + 0x220, 1)


def wall(height, walk):
    return lambda c, r: (height, 0, walk, 0) if c >= 5 else (0, 0, 0, 0)


# the specs of probe.flail_main (same items, holds, pokes and cells)
SPECS = {
    'F-iron-ground': dict(item=9, holds={1: SQ}, n=40, before={6: hc}),
    'F-iron-air': dict(item=9, holds={1: CR, 5: SQ}, n=40, before={8: hc}),
    'F-steel-ground': dict(item=10, holds={1: SQ}, n=50, before={6: hc}),
    'F-steel-air': dict(item=10, holds={1: CR, 5: SQ}, n=50, before={8: hc}),
    'F-iron-walk': dict(item=9, holds={1: RT | SQ}, after=RT, warm='east', n=40, before={6: hc}),
    'F-iron-nohit': dict(item=9, holds={1: SQ}, n=40),
    'F-iron-wall2': dict(item=9, holds={1: SQ}, n=40, cells=wall(2, 0)),
    'F-iron-wall1': dict(item=9, holds={1: SQ}, n=40, cells=wall(1, 0)),
    'F-iron-wk40': dict(item=9, holds={1: SQ}, n=40, cells=wall(0, 0x40)),
    'F-steel-wall2': dict(item=10, holds={1: SQ}, n=50, cells=wall(2, 0)),
}


def origin(ra):
    if 0x8002FB14 <= ra < 0x80030678:
        return 'CreatePlayerAnimationEffects'
    if ra == 0x80038BD0:
        return 'UpdateAnimation'
    return 'other'


def install(name):
    def patch(m):
        sound = m.hooks[0x800490FC]
        effect = m.hooks[0x8003BDD8]

        def wrapped_sound(mm):
            LOG.append((name, mm.tick_no, 'sound', mm.r[4] & 0xFFFFFFFF, mm.r[31] & 0xFFFFFFFF))
            sound(mm)

        def wrapped_effect(mm):
            LOG.append((name, mm.tick_no, 'effect', mm.r[5] & 0xFFFFFFFF, mm.r[31] & 0xFFFFFFFF))
            effect(mm)

        m.hooks[0x800490FC] = wrapped_sound
        m.hooks[0x8003BDD8] = wrapped_effect
    return patch


for name, spec in SPECS.items():
    A.run(dict(name=name, holds=spec['holds'], n=spec['n'], after=spec.get('after', 0), item=spec['item'], before=spec.get('before', {}), cells=spec.get('cells'),
               warm=spec.get('warm'), mutate=install(name)))

with open(os.path.join(OUT, 'F-sound-callers.txt'), 'w') as f:
    f.write('# probe tick kind id return-address origin   (ticks of the scenario, 1 = the first tick after the warm-up)\n')
    for name, tick, kind, ident, ra in LOG:
        if tick >= 1:
            f.write('%s %d %s %d %08X %s\n' % (name, tick, kind, ident, ra, origin(ra)))
print('%d calls journalled' % len(LOG))
