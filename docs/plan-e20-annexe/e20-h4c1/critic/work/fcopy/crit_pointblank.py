import os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.argv = ['x', os.path.join(HERE, '_crit_out')]
exec(open(os.path.join(HERE, 'c1_make_keys.py')).read().split("scen = []")[0])
import crit_relief as CR
# wall column 5 (x 120..143): rocks heading east are blocked 9 px from the hero's box
res = CR.report('crit_pointblank_wall_col5', fw.wall_col(5))
# detail of the first east rock (slot with dir 27 or 24)
for r in res['rows'][21:30]:
    for s, e in r['slots'].items():
        if s and e['type68'] == 16 and e['dir_t'] in (24, 27):
            print(r['tick'], s, 'status', e['status'], 'px %.2f py %.2f pz %.4f fz %d adj13C %d' % (e['px']/65536, e['py']/65536, e['pz']/65536, e['fz'], e['adj_13C']), 'anim_t', e['anim_t'])
