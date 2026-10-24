"""a3_r3b.py - disc-e20g3 A3: lift rec5 from the east and throw it west AT ONCE (as soon as the lift gesture ends), then walk through the opened gap at y=296. Timeline of the whole opening. Output out/a3_r3b.txt"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lib392 as L
OUT = []
def P(s):
    OUT.append(s); print(s, flush=True)
m, S = L.world392(hero_px=(720, 296), direction=8)
h = L.K.ent(0); m.poke(h + 0x118, 296 * 65536)
# 27 ticks LEFT (contact at 27), Square at 28 (lift), hold LEFT until the gesture ends, Square again at the first tick the hero is in the carry state (anim 12), then LEFT
seq = [(27, L.LEFT), (1, L.LEFT | L.SQUARE)] + [(36, L.LEFT)] + [(1, L.LEFT | L.SQUARE)] + [(80, L.LEFT)]
pads, n = L.pad_seq(seq)
rows = L.gl.run(m, n, pads, crates=[S[5]])
nm = {L.K.ent(s): 'rec%d' % r for r, s in S.items()}
t_lift = next(r['tick'] for r in rows if r['hero']['carried2C'])
t_carry_state = next(r['tick'] for r in rows if r['tick'] > t_lift and r['hero']['anim_t'] in (12, 7))
t_throw = next(r['tick'] for r in rows if r['tick'] > t_lift and r['hero']['carried2C'] == 0)
d = lambda t: rows[t - 1]
c = S[5]
t_break = next((r['tick'] for r in rows if r['tick'] > t_throw and r['c'][c]['anim_t'] == 2), None)
t_gone = next((r['tick'] for r in rows if r['tick'] > t_throw and r['c'][c]['status'] == 0), None)
t_x615 = next((r['tick'] for r in rows if r['tick'] > t_throw and r['hero']['px'] / 65536 <= 615.0), None)
t_x562 = next((r['tick'] for r in rows if r['tick'] > t_throw and r['hero']['px'] / 65536 <= 562.0), None)
P('tick of the first blocked step (contact) : 27')
P('Square edge (lift) : tick 28 -> hero.+0x2C = rec5 at tick %d, gesture state 5' % t_lift)
P('hero leaves the gesture (state %d) at tick %d (%d ticks of gesture)' % (d(t_carry_state)['hero']['anim_t'], t_carry_state, t_carry_state - t_lift))
P('Square edge (throw) at tick %d : the crate leaves the hands in that tick, at (%.1f, %.1f, %.1f), hero state %d' % (t_throw, d(t_throw)['c'][c]['px'] / 65536, d(t_throw)['c'][c]['py'] / 65536, d(t_throw)['c'][c]['pz'] / 65536, d(t_throw)['hero']['anim_t']))
P('crate lands/starts breaking (anim 2) at tick %s at x=%.1f, slot freed at tick %s' % (t_break, d(t_break)['c'][c]['px'] / 65536 if t_break else -1, t_gone))
P('hero x <= 615 (past the column\'s west face) at tick %s ; x <= 562 at tick %s ; contact during the walk: %s' % (t_x615, t_x562, [r['tick'] for r in rows if r['tick'] > t_throw and r['hero']['xcoll']][:3]))
open(os.path.join(L.OUT, 'a3_r3b.txt'), 'w').write('\n'.join(OUT) + '\n')
