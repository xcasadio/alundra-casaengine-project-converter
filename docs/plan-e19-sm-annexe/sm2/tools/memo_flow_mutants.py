"""Mutants of the memo-flow function (S1) over the 45 programs of the corpus: how the 70 reads change. python -B memo_flow_mutants.py"""
import collections, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import cfg2, corpus2 as C, memo_static as M
progs, sites = M.programs_from_tsv()
orig = cfg2.successors


def run(label, clob=None, successors=None):
    if successors:
        cfg2.successors = successors
    flow = collections.Counter()
    reached = 0
    for (m, s, p) in progs:
        base, d = C.load(m)
        codes = d['Codes']
        entry = d[C.TABLE_KEYS[s]][p]
        df, seen, marks, ru = cfg2.dataflow(codes, entry, clob or M.CLOB_BIN, M.READERS)
        for pc, per in df.items():
            for axis, st in per.items():
                flow[(axis, tuple(sorted(st)))] += 1
                reached += 1
    cfg2.successors = orig
    print('%-52s reads reached %d  %s' % (label, reached, dict(sorted(flow.items()))))


run('baseline')
run('mutant: no return edges (the old memo_flow.py)', successors=lambda codes, entry, pc, marks: ([] if codes[pc] == 0x7D else [pc + (C.SIZES.get(codes[pc], 1) or 1)] if codes[pc] in (0x7E, 0x7F, 0x80, 0x81) else orig(codes, entry, pc, marks)))
run('mutant: 0x37 removed from the X writers', clob={'X': M.CLOB_BIN['X'] - {0x37}, 'Y': M.CLOB_BIN['Y'], 'Z': M.CLOB_BIN['Z']})
run('mutant: 0x37 added to the Z writers', clob={'X': M.CLOB_BIN['X'], 'Y': M.CLOB_BIN['Y'], 'Z': M.CLOB_BIN['Z'] | {0x37}})
run('mutant: Break ends the walk', successors=lambda codes, entry, pc, marks: ([] if codes[pc] == 0x00 else orig(codes, entry, pc, marks)))
run('mutant: only forward edges', successors=lambda codes, entry, pc, marks: [q for q in orig(codes, entry, pc, marks) if q > pc])
