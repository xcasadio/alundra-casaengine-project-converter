"""k_throw_explode.py - the h3b throw keys (scen_throwdirs.py) prolonged to the explosion: the hero throws the bomb in the four directions and stays where he threw it; is he in the zone when it explodes?
Prints the landing offset, then the first tick of the hero's contact and hurt state, hp at the end.  Written for the a362-h3a discovery (2026-10-25)."""
from scen_lib import *
for name, bit in (('RIGHT', W.RIGHT), ('LEFT', W.LEFT), ('UP', W.UP), ('DOWN', W.DOWN)):
    m, STATS = mk(0x23)
    def hold(t):
        if t == 1: return bit
        if t == 4: return W.CIRCLE
        if t == 50: return W.SQUARE
        return 0
    log = run(m, 330, hold)
    hx = hy = None
    first_touch = first_hurt = None
    rest = None
    for r in log:
        h = r['hero']
        if r['t'] == 60: hx, hy = h['x'], h['y']
        thrown = [e for e in r['ents'].values() if e['sp'] == 2]
        if r['t'] == 200 and thrown: rest = (thrown[0]['x'], thrown[0]['y'])
        if first_hurt is None and r['t'] > 100 and h['hp'] < 10: first_hurt = (r['t'], h['hp'], hex(h['tanim']))
    print('%-5s hero at (%.2f,%.2f)  bomb at rest (%.2f,%.2f) offset (%.2f,%.2f)  first hurt: %s   final hp %d' % (name, hx, hy, rest[0], rest[1], rest[0] - hx, rest[1] - hy, first_hurt, log[-1]['hero']['hp']))
