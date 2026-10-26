from scen_lib import *
import sys
mode = sys.argv[1]
m, STATS = mk(0x23)
def hook(m, t, rec):
    if mode == 'dh7':
        m.poke8(0x80127008 + 2 + 4, 0x80)
    if 256 <= t <= 266:
        print('t%3d entity hp %2d  stats hp %d  resp[nibble4]=%#x  tanim %#x  touching %#x' % (t, m.peeks(HERO + 0x14), m.peek(STATS + 4) & 0xFFFF, m.rb(0x8012700E) if hasattr(m,'rb') else m.peek8(0x8012700E), m.peeks(HERO + 0x88), m.peek(HERO + 0x224)))
log = run(m, 270, lambda t: W.CIRCLE if t == 1 else 0, on_tick=hook)
