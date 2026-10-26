"""k_dh7_prime.py - an alternative to D-H-7 at the level the hit pass reads: the response byte of the hero's OWN balance record (the record at hero+0x1C4, the sprite 0 entry of BALANCE.BIN) for the attack nibble 4
posed to 0x80, so that the pass does not write +0x224 (the DLL's HitPassTests.H6 precedent: the target's BalanceRecord replaced by a record with the response 0x80).  Same scenario as scen_bomb1.py.
usage: k_dh7_prime.py [plain|prime]    Written for the a362-h3a discovery (2026-10-25)."""
from scen_lib import *
import sys
mode = sys.argv[1] if len(sys.argv) > 1 else 'prime'
m, STATS = mk(0x23)
rec = m.peek(HERO + 0x1C4)
print('hero +0x1C4 (balance record pointer) = %#x ; record bytes: %s' % (rec, [m.rb(rec + i) if hasattr(m, 'rb') else m.peek8(rec + i) for i in range(0, 16)]))
if mode == 'prime':
    m.poke8(rec + 4 + 2, 0x80)
    print('response byte for nibble 4 (record + 6) posed to 0x80')
log = run(m, 300, lambda t: W.CIRCLE if t == 1 else 0)
prev = None
for r in log:
    h = r['hero']
    key = (h['hp'], h['tanim'])
    if key != prev and r['t'] > 240:
        print('t%3d hero hp %d state %#x x %.2f touching(+0x224) at that tick' % (r['t'], h['hp'], h['tanim'], h['x']))
    prev = key
print('final hero hp', log[-1]['hero']['hp'], 'x', log[-1]['hero']['x'])
