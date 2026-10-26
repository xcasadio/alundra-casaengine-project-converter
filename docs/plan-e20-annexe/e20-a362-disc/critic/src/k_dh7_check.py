"""k_dh7_check.py - does D-H-7 (type-4 response byte of the hero's working sheet 0x80127008 set to 0x80) keep the hero out of the hurt state?
Same scenario as scen_bomb1.py (Circle at t1, the bomb carried then dropped at the hero's feet), hero columns only.  Written for the a362-h3a discovery (2026-10-25)."""
from scen_lib import *
import sys
mode = sys.argv[1] if len(sys.argv) > 1 else 'plain'
m, STATS = mk(0x23)
if mode == 'dh7':
    A = 0x80127008 + 2 + 4          # response to the attack nibble 4 = byte (4 + 2) of the sheet (0x8004464C)
    print('D-H-7: the response byte @%08x (nibble 4) is posed to 0x80 after every tick' % A)
def poke_every_tick(m, t, rec):
    if mode == 'dh7':
        m.poke8(0x80127008 + 2 + 4, 0x80)
log = run(m, 300, lambda t: W.CIRCLE if t == 1 else 0, on_tick=poke_every_tick)
prev = None
for r in log:
    h = r['hero']
    key = (h['hp'], h['tanim'])
    if key != prev and r['t'] > 240:
        print('t%3d hero hp %d tanim %#x x %.2f y %.2f' % (r['t'], h['hp'], h['tanim'], h['x'], h['y']))
    prev = key
print('final stats hp', m.peek(STATS + 4) & 0xFFFF)
