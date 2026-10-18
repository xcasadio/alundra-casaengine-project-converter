import sys, os
sys.argv = ['x']
exec(open('mutants_crit.py').read().split("wall15 = fw.wall_col(15)")[0])
wall15 = fw.wall_col(15)
def inj(off, fn):
    st = {}
    def f(m, t):
        a = W.ENT0 + W.STRIDE * 2
        if m.peek(a + 0x220) and not st.get('done'):
            addr = W.ENT0 + W.STRIDE * 1 + off
            m.poke(addr, fn(m.peek(addr)) & 0xFFFFFFFF)
            st['done'] = t
    return f
def run_inj(off, fn, init=None):
    C.map14().record['Header']['Ubuff'][13] = 32
    spec = dict(item=5, n=75, holds={1: SQ}, after=0, hero_px=(100, 100), direction=24, cells=None, targets=[(24, 12, 0)], init=init or [], after_tick=[inj(off, fn)])
    res = fw.run(spec)
    C.map14().record['Header']['Ubuff'][13] = 8
    return res
def gone(res):
    for r in res['rows']:
        if 2 in r['slots'] and r['slots'][2]['status'] == 4:
            return r['tick']
    return None
for label, off, fn, pr in [
    ('E17-8 (+0xB4 & 0x40)', 0xB4, lambda v: v | 0x40, (0x8007F500, 0)),
    ('E17-9 (class bit 0x1 of target flags)', 0x6C, lambda v: v & ~1, (0x8007F524, 0)),
]:
    ctl = run_inj(off, fn)
    mut = run_inj(off, fn, init=[patch(pr)])
    print(label, 'control: arrow destroyed at tick', gone(ctl), '| mutant: arrow destroyed at tick', gone(mut), '| KILLED' if gone(ctl) != gone(mut) else '| SURVIVED')
