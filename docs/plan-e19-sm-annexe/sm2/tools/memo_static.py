"""Memo static analysis of the sm2 corpus (own implementation, python -B, read-only).

  python -B memo_static.py [--tsv PATH]  -> evidence tables on stdout

For the 45 programs that contain 0x6A-0x6D (taken from skipped-sites.tsv, then RE-DERIVED from the export: see check below), prints
  * per reader: linear state (last 0x6A before it in the linear decode of its program, overwriters between), flow state (dataflow),
  * counts per axis and totals,
  * the 0x78/0x7B/0x7C calls between a 0x6A and a reader, and the opcodes of each callee.
Clobber sets: DLL (derived from Alundra/Events/EventProgramRunner.cs by hand: see evidence/clobbers_dll.txt) and BINARY (stateuse2.txt).
"""
import collections, csv, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import corpus2 as C
import cfg2

TSV = 'D:/development/repo/alundra-casaengine-project-converter/docs/plan-e19-sm-annexe/skipped-sites.tsv'
READERS = {0x6B: 'X', 0x6C: 'Y', 0x6D: 'Z'}
CLOB_DLL = {'X': {0x0B, 0x1C, 0x1D, 0x1E, 0x1F, 0x20, 0x21, 0x22, 0x23, 0x37}, 'Y': {0x0B, 0x1E, 0x1F}, 'Z': set()}
# binary writers (check.md 2.4 / stateuse2): +0xC: 0B 1C 1D 1E 1F 20 21 22 23 37 5F BB ; +0x10: 0B 1E 1F 5F ; +0x14: 5F
CLOB_BIN = {'X': CLOB_DLL['X'] | {0x5F, 0xBB}, 'Y': CLOB_DLL['Y'] | {0x5F}, 'Z': {0x5F}}


def programs_from_tsv():
    rows = list(csv.DictReader(open(TSV, encoding='utf-8'), delimiter='\t'))
    progs = collections.OrderedDict()
    sites = []
    for r in rows:
        op = int(r['opcode'], 16)
        if 0x6A <= op <= 0x6D:
            progs[(int(r['map']), r['slot'], int(r['program']))] = 1
            sites.append((int(r['map']), r['slot'], int(r['program']), int(r['pc']), op))
    return list(progs), sites


def linear_state(codes, entry, pc, axis_clob, extent_end):
    """Linear decode from the entry of the program to `pc`; returns (kind, writer_pc|None, overwriters between)."""
    seq = C.decode(codes, entry, count=100000, stop_at_end=False)
    seq = [(p, o, ops) for p, o, ops in seq if p < pc]
    last6a = None
    for p, o, ops in seq:
        if o == 0x6A:
            last6a = p
    if last6a is None:
        return 'NO6A', None, [], []
    over = [(p, o) for p, o, ops in seq if p > last6a and o in axis_clob]
    calls = [(p, o, ops) for p, o, ops in seq if p > last6a and o in (0x78, 0x7B, 0x7C, 0x79)]
    return 'LINEAR6A', last6a, over, calls


def main():
    progs, sites = programs_from_tsv()
    print('programs with a 0x6A-0x6D site: %d ; sites: %d' % (len(progs), len(sites)))
    cnt = collections.Counter(o for *_, o in sites)
    print('sites per opcode:', {hex(k): v for k, v in sorted(cnt.items())})
    tot = collections.Counter()
    for variant, clob in (('DLL', CLOB_DLL), ('BIN', CLOB_BIN)):
        flow = collections.Counter()
        lin = collections.Counter()
        for (m, s, p) in progs:
            base, d = C.load(m)
            codes = d['Codes']
            entry = d[C.TABLE_KEYS[s]][p]
            df, seen, marks, ru = cfg2.dataflow(codes, entry, clob, READERS)
            for pc, per_axis in df.items():
                for axis, st in per_axis.items():
                    flow[(axis, tuple(sorted(st)))] += 1
            for (mm, ss, pp, pc, op) in sites:
                if (mm, ss, pp) == (m, s, p) and op in READERS:
                    kind, w, over, calls = linear_state(codes, entry, pc, clob[READERS[op]], None)
                    lin[(READERS[op], kind, bool(over), len(calls))] += 1
        print('\n== variant %s ==' % variant)
        print('flow-sensitive (reader reached by states):')
        for k, v in sorted(flow.items()):
            print('  ', k, v)
        print('linear (axis, kind, overwritten?, #calls between):')
        for k, v in sorted(lin.items()):
            print('  ', k, v)


if __name__ == '__main__':
    main()
