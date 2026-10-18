"""scen_allstates.py - critic: for EVERY hero state s (target animation +0x88 = s, the key of the state table 0x80022D3C) press Circle for one tick and record
what the real MovePlayer does with the equipped bomb: started (sound 44 + link), refused (sound 3), or ignored (TryUseItem never reached / no reaction).
Machine state restored between trials (snapshot)."""
import sys
from scen_lib import *
import spellworld as S

def trial_state(m, snap, s, air):
    m.ram[:] = snap[0]; m.init[:] = snap[1]; m.journal.clear(); m.hi = m.lo = 0
    m.poke(HERO + 0x88, s); m.poke(HERO + 0x90, s)
    if air:
        m.poke(HERO + 0x144, 0); m.poke(HERO + 0x11C, 40 << 16)
    pad = W.Pad()
    # one idle tick to let the pad edge machinery start from zero, then Circle
    m.tick_no = 1
    W.tick(m, pad, 0, check_pad=False)
    m.poke(HERO + 0x88, s)
    m.journal.clear()
    called = []
    import machine
    m.tick_no = 2
    W.tick(m, pad, W.CIRCLE, check_pad=False)
    snd = [x[2] for x in m.journal if x[1] == 'sound']
    started = bool(m.peek(HERO + 0x2C)) and 44 in snd
    return started, (3 in snd), snd, m.peeks(HERO + 0x88)

m, STATS = mk(0x23)
snap = (bytes(m.ram), bytes(m.init))
res = {}
for air in (False, True):
    ok = []; refused = []; none = []
    for s in range(0, 0x50):
        try:
            started, ref, snd, after = trial_state(m, snap, s, air)
        except Exception as ex:
            none.append((s, 'EXC ' + str(ex)[:30])); continue
        if started: ok.append(s)
        elif ref: refused.append(s)
        else: none.append(s)
    print('air' if air else 'ground', 'STARTS from states:', ['%02x' % x for x in ok])
    print('   refused with sound 3:', ['%02x' % x for x in refused])
    print('   no reaction:', ['%02x' % x if isinstance(x, int) else x for x in none])
