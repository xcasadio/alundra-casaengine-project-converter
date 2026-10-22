"""Which mutants of the DLL model the real-bytes scenarios kill (end state per call differs from the expected one). Usage: python -B rb_kills.py OUT.tsv"""
import io, os, sys, contextlib
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import sm1lib as L
import realbytes as RB
RB.DEFAULT_MUTANTS = tuple(L.MUTANTS_60_61 + L.MUTANTS_3C_3D)
sys.argv = [sys.argv[0], os.devnull if False else sys.argv[1] + '.scenarios.json']
with contextlib.redirect_stdout(io.StringIO()):
    R = RB.main()
kills = {}
for s in R:
    exp = [(p['end_pc'], p['result'], p['logic']) for p in s['expected']]
    for m in RB.DEFAULT_MUTANTS:
        got = [(p['end_pc'], p['result'], p['logic']) for p in s['mut:' + m]]
        if got != exp:
            kills.setdefault(m, []).append(s['id'])
with open(sys.argv[1].replace('.scenarios.json', ''), 'w', encoding='utf-8', newline='\n') as f:
    f.write('mutant\treal_bytes_groups_that_kill_it\n')
    for m in RB.DEFAULT_MUTANTS:
        ids = sorted(set(kills.get(m, [])))
        f.write('%s\t%s\n' % (m, ' '.join(ids) or 'none'))
        print('%-30s %s' % (m, ' '.join(ids) or 'none'))
os.remove(sys.argv[1])
