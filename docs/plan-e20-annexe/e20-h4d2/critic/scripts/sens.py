"""critic: separate the three defects. For every scenario, run (binary), (query only corrected = qfix), (turn only corrected), (all corrected) and report the first tick where the rows differ from the binary run"""
import sys, os, json, hashlib
HERE = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'h4o')
sys.path.insert(0, HERE)
import fire1_lib as L
# reuse their patch functions by re-declaring (same words)
def patch_query_corrected(m):
    assert m.peek(0x8003AF98) == 0x0163102A and m.peek(0x8003AFA8) == 0x00607021 and m.peek(0x8003B0B4) == 0x2A020003
    m.poke(0x8003AF98, 0x0003102A); m.poke(0x8003AFA8, 0x246E0001); m.poke(0x8003B0B4, 0x2A020002)
def patch_bound_only(m):
    m.poke(0x8003AF98, 0x0003102A); m.poke(0x8003AFA8, 0x246E0001)
def patch_sort_only(m):
    m.poke(0x8003B0B4, 0x2A020002)
def patch_turn_only(m):
    L.install_turn_fix(m)
def patch_all(m):
    patch_query_corrected(m); L.install_turn_fix(m)
DECOY = (1100, 900)
SC = {
 'one': dict(ticks=200, targets=[(140, 100)]),
 'three': dict(ticks=200, targets=[(140, 100), (100, 140), (180, 100)]),
 'vanish75': dict(ticks=200, targets=[(140, 100)], remove={75: [0]}),
 'col_hit': dict(ticks=130, targets=[(108, 100)]),
 'two_wrong_order': dict(ticks=200, targets=[(180, 100), (130, 100)]),
 'topslot': dict(ticks=200, targets=[DECOY] * 11 + [(140, 100)], remove={1: list(range(11))}),
}
def sig(res):
    rows = []
    for r in res['rows']:
        ents = {s: (e['status'], e['type'], e['x'], e['y'], e['z'], e['dir_t'], e['b274'], e['b278_slot']) for s, e in r['ents'].items()}
        rows.append((ents, r['rng1'], r['hits'], tuple(map(str, r['journal']))))
    return rows
def first_diff(a, b):
    for i, (x, y) in enumerate(zip(a, b)):
        if x != y: return i + 1
    return None
names = sys.argv[1:] or list(SC)
for nm in names:
    kw = SC[nm]
    base = sig(L.run(**kw))
    out = []
    for lab, p in (('bound-only', patch_bound_only), ('sort-only', patch_sort_only), ('query(bound+sort)', patch_query_corrected), ('turn-only', patch_turn_only), ('all', patch_all)):
        out.append('%s:%s' % (lab, first_diff(base, sig(L.run(patch=p, **kw)))))
    print(nm, ' | '.join(out), flush=True)
