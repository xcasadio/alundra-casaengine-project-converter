"""wave_mut.py - bin agent: teeth of the wave oracle.  One-word patches applied after the world is built (so the hero's own InitializeEntity is not affected).
W1: no '+1' in the spawn Z (0x80039EA4 addiu v0,v0,1 -> addiu v0,v0,0): the wave is born ON the terrain-height reading (+0x134) and is grounded.
W2: C61 -> nop (0x8007A960 jal CheckAndTriggerTileEffect removed): no grass cut.  W3: E10 -> nop (jal DestroyEntity removed at 0x8007F244): the wave never dies (status 3 forever)."""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import pworld as P
from wave_run import run_wave
from wave_grass import grass, cut_events
OUT = os.path.join(HERE, '..', 'out')

def patch(*pw):
    def f(m):
        for a, w in pw:
            m.poke(a, w)
    return f

def summarize(res):
    st = [(r['tick'], r['slots'][1]['status']) for r in res['rows'] if r['slots'].get(1)]
    first = st[0][0] if st else None
    last = st[-1][0] if st else None
    return 'wave visible t%s..t%s (%d ticks), last status %s' % (first, last, len(st), st[-1][1] if st else None)

if __name__ == '__main__':
    out = []
    res = run_wave('c', direction='east', n=170)
    out.append('control                       : ' + summarize(res))
    res = run_wave('w1', direction='east', n=170, init=[patch((0x80039EA4, 0x24420000))])
    w = [r['slots'][1] for r in res['rows'] if r['slots'].get(1)]
    out.append('W1 no +1 in the spawn Z       : ' + summarize(res) + ' ; first rows (status, rawZ, ground_144, adj): ' + str([(d['status'], d['pz'], d['ground_144'], d['adj_13C']) for d in w[:3]]))
    res = run_wave('w2', direction='east', n=170, cells=grass, init=[patch((0x8007A960, 0x00000000))])
    out.append('W2 C61 call removed           : ' + summarize(res) + ' ; cuts at t>=117: %d (control has 3)' % len([e for e in cut_events(res) if e[0] >= 117]))
    res = run_wave('w3', direction='east', n=170, init=[patch((0x8007F244, 0x00000000))])
    out.append('W3 E10 DestroyEntity removed  : ' + summarize(res))
    open(os.path.join(OUT, 'wave_mutations.txt'), 'w').write('\n'.join(out) + '\n')
    print('\n'.join(out))
