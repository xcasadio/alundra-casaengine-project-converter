"""dirtest.py - bin agent, E20.f3 discovery: which hero field gives the arrow's direction, and when is it read?  Real code (pworld).
The spawn passes dir = [0x80023644 + 4 * hero(+0x98)] where +0x98 is the direction INDEX latched by the previous animation pass (0x80038E18 -> 0x80038AB4)."""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import pworld as P
import world as W
OUT = os.path.join(HERE, '..', 'out')

def run(name, holds, n, item=5, direction=24, cells=None):
    res = P.run(dict(item=item, n=n, holds=holds, after=0, direction=direction, cells=cells))
    lines = ['# %s' % name]
    spawn = None
    for r in res['rows']:
        a = r['slots'].get(1)
        if a and a['type68'] in (3, 4, 7, 11, 12, 14, 15, 27, 28, 29) and spawn is None:
            spawn = r['tick']
    for r in res['rows']:
        h = r['slots'][0]
        if r['tick'] <= (spawn or 0) + 1 and (r['tick'] <= 14 or (spawn and r['tick'] >= spawn - 3)):
            a = r['slots'].get(1)
            lines.append('  t%03d hold=%04X hero anim_t=%d dir_t(+0x8C)=%d adir(+0x98)=%d gnd=%d z=%.1f rec=%X cnt=%d | arrow: %s' % (
                r['tick'], r['hold'], h['anim_t'], h['dir_t'], h['adir'], h['ground_144'], h['pz'] / 65536, r['rec_ptr'], r['rec_cnt'],
                ('dir_t=%d adir=%d' % (a['dir_t'], a['adir'])) if a and a['type68'] in (3, 4) else '-'))
    lines.append('  arrow spawned at t%s' % spawn)
    return lines

if __name__ == '__main__':
    out = []
    out += run('ground: facing east, Square+Up pressed together at t1', {1: W.SQUARE | W.UP}, 16)
    out += run('ground: facing east, Square at t1, Up held from t3 (turn request during the swing)', {1: W.SQUARE, **{t: W.UP for t in range(3, 16)}}, 16)
    out += run('air: Cross t1 (jump), Square+Left at t8 (turn + shot in the air)', {1: W.CROSS, 8: W.SQUARE | W.LEFT, 9: W.LEFT, 10: W.LEFT, 11: W.LEFT}, 20)
    open(os.path.join(OUT, 'dirtest.txt'), 'w').write('\n'.join(out) + '\n')
    print('\n'.join(out))
