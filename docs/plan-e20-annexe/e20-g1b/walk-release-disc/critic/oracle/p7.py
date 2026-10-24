import sys, os, json
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import pworld as P
import world as W
import c1world as C
from summ import timeline, traj
from p3 import wall_col

ITEMS = {'hunter': 5, 'william': 6, 'fire': 16, 'ice': 14, 'steel': 2, 'demon': 3, 'holy': 4, 'chain': 9, 'crusher': 10}


def charged(name, item, hold_until, n, direction=24, cells=None, targets=(), slots=(1,), cols=None, every=0, tall=False, hero_px=(100, 100), first=0):
    if tall:
        C.map14().record['Header']['Ubuff'][13] = 32
    holds = {t: W.SQUARE for t in range(1, hold_until + 1)}
    spec = dict(item=item, n=n, holds=holds, after=0, hero_px=hero_px, direction=direction, cells=cells, targets=list(targets))
    res = P.run(spec)
    print('######', name, 'item', item, 'hold t1..t%d, release t%d' % (hold_until, hold_until + 1))
    # charge timeline of the hero
    prev = None
    for r in res['rows']:
        h = r['slots'][0]
        key = (h['anim_t'], h['anim_c'], r['charge'] // 10, r['rec_ptr'] != 0)
        if key != prev or r['journal'] and any(j.startswith(('snd', 'fx')) for j in r['journal']):
            pass
        prev = key
    # hero anim changes
    last = None
    for r in res['rows']:
        h = r['slots'][0]
        k = (h['anim_t'], h['anim_c'])
        if k != last:
            print('  t%03d hero anim_t=%d anim_c=%d charge=%d rec=%X/%d  %s' % (r['tick'], h['anim_t'], h['anim_c'], r['charge'], r['rec_ptr'], r['rec_cnt'], ' '.join(j for j in r['journal'] if not j.startswith('stubC'))[:200]))
            last = k
    for s in slots:
        print('-- slot', s)
        sub = dict(res); sub['rows'] = [r for r in res['rows'] if r['tick'] >= first]
        print(timeline(sub, s, every=every, cols=cols or ['status', 'type68', 'anim_t', 'dir_t', 'px', 'py', 'pz', 'adj_13C', 'ground_144', 'hitc', 'reset_AC', 'flags6C', 'atk_attr', 'atk_pow', 'box1d4']))
    return res


if __name__ == '__main__':
    w = sys.argv[1]
    if w == 'william':
        r = charged('william charged east', 6, 95, 150)
        # first arrow slot is 1 (normal shot), charged is the next
    if w == 'william2':
        r = charged('william charged east', 6, 95, 150, slots=(2,))

    if w == 'william_wall':
        r = charged('william charged east, wall col 15 and a target at x=240', 6, 95, 175, slots=(1,), first=140, cells=wall_col(15), cols=['status', 'type68', 'anim_t', 'dir_t', 'px', 'pz', 'adj_13C', 'hitc', 'reset_AC', 'flags6C', 'c1d0'], hero_px=(100, 100))
        for rr in r['rows']:
            if 105 <= rr['tick'] <= 140 and rr['journal']:
                pass
    if w == 'william_target':
        # tall target so both arrows can touch it; the charged arrow is the second shot
        r = charged('william charged east vs target at x=300', 6, 95, 160, slots=(2,), first=130, targets=[(24, 12, 0)], tall=True,
                    cols=['status', 'type68', 'anim_t', 'dir_t', 'px', 'pz', 'adj_13C', 'hitc', 'reset_AC', 'flags6C', 'c1d0', 'touch_slot'])
    if w == 'steel':
        r = charged('steel edge charged east', 2, 95, 150, slots=(1,), first=96, cols=['status', 'type68', 'anim_t', 'dir_t', 'px', 'pz', 'adj_13C', 'hitc', 'reset_AC', 'flags6C', 'atk_attr', 'atk_pow', 'box1d4', 'hbx', 'bw'])
    if w == 'steel_target':
        r = charged('steel edge charged east vs target x=156', 2, 95, 150, slots=(2,), first=96, targets=[(12, 12, 0)], tall=True, cols=['status', 'type68', 'anim_t', 'dir_t', 'px', 'pz', 'adj_13C', 'hitc', 'reset_AC', 'flags6C', 'atk_attr', 'atk_pow', 'box1d4', 'hbx', 'bw'])
    if w == 'fire_ch':
        r = charged('fire rod charged east', 16, 95, 190, slots=(1,), first=100, cols=['status', 'type68', 'anim_t', 'dir_t', 'px', 'pz', 'fz', 'ground_144', 'hitc', 'reset_AC', 'flags6C', 'atk_attr', 'atk_pow', 'box1d4', 'bw', 'bd', 'bh'])
        print(traj(r, 1, 105, 125, cols=('px', 'pz', 'fx', 'fz', 'ground_144')))
    if w == 'ice_ch':
        r = charged('ice rod charged east', 14, 95, 190, slots=(1,), first=100, cols=['status', 'type68', 'anim_t', 'dir_t', 'px', 'pz', 'fz', 'ground_144', 'hitc', 'reset_AC', 'flags6C', 'atk_attr', 'atk_pow', 'box1d4', 'bw', 'bd', 'bh'])
