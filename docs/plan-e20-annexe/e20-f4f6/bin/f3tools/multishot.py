"""multishot.py - bin agent, E20.f3 discovery: does the binary limit simultaneous projectiles?  Hunter's Bow (item 5), Square pressed (a) at t1 and t25, (b) mashed every 2 ticks.
Real code (pworld).  Prints, per tick, the projectile slots alive (type68 in 3,4,7,11,12,14,15,27,28,29), their x, and the spawn ticks."""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import pworld as P
import world as W
OUT = os.path.join(HERE, '..', 'out')
PROJ = (3, 4, 7, 11, 12, 14, 15, 27, 28, 29)

def run(name, item, holds, n):
    res = P.run(dict(item=item, n=n, holds=holds, after=0))
    lines = ['# %s' % name]
    seen = {}
    for r in res['rows']:
        alive = {s: d for s, d in r['slots'].items() if s != 0 and d['type68'] in PROJ and d['status'] in (1, 2, 3)}
        for s, d in alive.items():
            if (s, d['px']) and s not in seen:
                seen[s] = r['tick']
                lines.append('  t%03d spawn slot %d type %d at x=%.0f z=%.1f (hero anim_t=%d, rec_ptr=%X)' % (r['tick'], s, d['type68'], d['px'] / 65536, d['pz'] / 65536, r['slots'][0]['anim_t'], r['rec_ptr']))
        if r['tick'] in (10, 20, 30, 35, 40, 50, 60):
            lines.append('  t%03d alive projectiles: %s' % (r['tick'], {s: round(d['px'] / 65536) for s, d in alive.items()}))
    return lines

if __name__ == '__main__':
    out = []
    out += run('(a) Hunter: Square at t1 and t25', 5, {1: W.SQUARE, 25: W.SQUARE}, 60)
    out += run('(b) Hunter: Square pressed every second tick t1..t41', 5, {t: W.SQUARE for t in range(1, 42, 2)}, 60)
    out += run('(c) William: Square held t1..t60 (auto-repeat is not a press)', 6, {t: W.SQUARE for t in range(1, 61)}, 80)
    open(os.path.join(OUT, 'multishot.txt'), 'w').write('\n'.join(out) + '\n')
    print('\n'.join(out))
