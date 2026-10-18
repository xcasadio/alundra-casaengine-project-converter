"""c1_probe_passes.py (h4c1 discovery, own script, copy of the harness): per-pass snapshot of the first earth-1 rock around its birth tick."""
import sys
import spellworld as S
import world as W
import h4_models as M

m, STATS, placed = S.make(item=0x2B, mp=4, mpmax=4, seed=0x12345678, indices=(1, 2, 16, 17, 19, 20, 21, 22, 23, 24, 25))
pad = W.Pad()
names = {0x80038634: 'slots', 0x800386D0: 'events', 0x80038998: 'counters', 0x800384F4: 'lists', 0x80038E18: 'anim', 0x80038364: 'physics', 0x80038E84: 'effects', 0x80039300: 'hits'}
def snap(i):
    e = S.ent(i)
    g = m.peeks
    return 'st=%d id=%d x=%d y=%d z=%d fz=%d tdir=%d tanim=%d canim=%d row98=%d animend=%d flags=%#x' % (
        g(e + 0x10), g(e + 4), g(e + 0x114) >> 16, g(e + 0x118) >> 16, g(e + 0x11C), g(e + 0xB8), g(e + 0x8C), g(e + 0x88), g(e + 0x90), g(e + 0x98), g(e + 0xAC), m.peek(e + 0x6C))
T0 = int(sys.argv[1]) if len(sys.argv) > 1 else 22
for t in range(1, T0 + 3):
    hold = W.CIRCLE if t == 1 else 0
    m.tick_no = t
    pad.update(hold)
    W.write_pad(m, pad)
    for a in W.PASSES:
        W.run_pass(m, a)
        if t >= T0 - 1:
            print('tick %d after %-9s slot1: %s | slot2: %s | count=%d' % (t, names[a], snap(1), snap(2), m.peeks(0x80134360)))
