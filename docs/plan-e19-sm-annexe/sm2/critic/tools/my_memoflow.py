"""Own dataflow: for every program that owns a 6A-6D site, the set of states {I(init), S(saved), C(clobbered)} per axis at each read.
Edges follow the interpreter (WalkProgram semantics) with an explicit call/return graph. Writers = the BINARY set (my_statewrites2)."""
import sys, json, collections
sys.path.insert(0, 'tools')
from mycorpus import *

WRITE = {
    0: {0x0B, 0x1C, 0x1D, 0x1E, 0x1F, 0x20, 0x21, 0x22, 0x23, 0x37, 0x5F, 0xBB},  # X  (P2)
    1: {0x0B, 0x1E, 0x1F, 0x5F},                                                   # Y  (P3)
    2: {0x5F},                                                                      # Z  (P4)
}

def successors(codes, pc, entry, marks_all):
    """list of successor pcs for the instruction at pc (interpreter semantics, static)"""
    n = len(codes)
    B = lambda i: codes[i] if 0 <= i < n else 0
    S = lambda lo, hi: s16(B(lo) | (B(hi) << 8))
    op = codes[pc]
    if op not in SIZE or SIZE[op] == 0 or op == 0xFF:
        return []
    size = SIZE[op]
    if op == 0x02: return [pc + S(pc + 1, pc + 2)]
    if op in (3, 4): return [pc + size, pc + S(pc + 1, pc + 2)]
    if op in (0x30, 0x31): return [pc + size, pc + S(pc + 3, pc + 4)]
    if op == 0x74: return [pc + size, pc + S(pc + 1, pc + 2)]
    if op in (0x57, 0x58): return [pc + S(pc + 1 + 2 * d, pc + 2 + 2 * d) for d in range(4)]
    if op == 0x78: return [pc + S(pc + 1, pc + 2)]
    if op == 0x79: return [pc + size, pc + S(pc + 1, pc + 2)]
    if op in (0x7B, 0x7C): return [pc + size, pc + S(pc + 3, pc + 4)]
    if op == 0x7D: return sorted(marks_all)
    if op in (0x7E, 0x7F, 0x80, 0x81): return [pc + size] + sorted(marks_all)
    if op == 0x49: return [entry]
    if op == 0x4B: return [pc + size, entry]
    return [pc + size]

def flow(codes, entry):
    n = len(codes)
    # find reachable + marks like WalkProgram, then recompute with return edges to a fixpoint
    marks = set(); st = {}
    # iterate: marks known so far; compute reachable set; add marks of reachable call sites
    while True:
        reach = set(); work = [entry]
        while work:
            pc = work.pop()
            if pc in reach or not (0 <= pc < n): continue
            reach.add(pc)
            for q in successors(codes, pc, entry, marks): work.append(q)
        newm = set()
        for pc in reach:
            op = codes[pc]
            B = lambda i: codes[i] if 0 <= i < n else 0
            if op in (0x78, 0x79): newm.add(pc + 3)
            if op in (0x7B, 0x7C): newm.add(pc + 5)
        if newm <= marks: break
        marks |= newm
    # dataflow: state per (pc, axis) = set of tags
    IN = collections.defaultdict(lambda: [set(), set(), set()])
    IN[entry] = [{'I'}, {'I'}, {'I'}]
    work = collections.deque([entry]); inq = {entry}
    while work:
        pc = work.popleft(); inq.discard(pc)
        op = codes[pc]
        out = [set(x) for x in IN[pc]]
        if op == 0x6A:
            out = [{'S'}, {'S'}, {'S'}]
        else:
            for ax in range(3):
                if op in WRITE[ax]: out[ax] = {'C'}
        for q in successors(codes, pc, entry, marks):
            if not (0 <= q < n): continue
            changed = False
            for ax in range(3):
                if not out[ax] <= IN[q][ax]:
                    IN[q][ax] |= out[ax]; changed = True
            if changed and q not in inq:
                work.append(q); inq.add(q)
    return reach, IN

if __name__ == '__main__':
    sites = json.load(open('evidence/my_sites_6A_6D.json'))
    progs = sorted(set((m, s, i) for m, s, i, pc, op in sites))
    print('programs with a site:', len(progs))
    rows = []
    cache = {}
    for (m, s, i) in progs:
        ev, tm, base = load(m)
        codes = ev['Codes']; entry = table(ev, s)[i]
        reach, IN = flow(codes, entry)
        for (mm, ss, ii, pc, op) in sites:
            if (mm, ss, ii) != (m, s, i): continue
            if pc not in reach:
                print('site not in my reach!', m, s, i, pc); continue
            if op in (0x6B, 0x6C, 0x6D):
                ax = op - 0x6B
                rows.append((m, s, i, pc, op, 'XYZ'[ax], ''.join(sorted(IN[pc][ax]))))
    cnt = collections.Counter((r[5], r[6]) for r in rows)
    print('reads', len(rows), dict(cnt))
    with open('evidence/my_memo_reads.tsv', 'w') as f:
        for r in rows: f.write('\t'.join(map(str, r)) + '\n')
    # which Z reads with I
    print('INIT reads:', [(r[0], r[1], r[2], r[3]) for r in rows if 'I' in r[6]])
    # compare with prep table
    P = 'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/sm-prep/sm2/evidence/memo_reads_table.tsv'
    prep = {}
    for line in open(P).read().splitlines()[1:]:
        c = line.split('\t'); prep[(int(c[0]), c[1], int(c[2]), int(c[3]))] = (c[4], c[5], c[6])
    mine = {(r[0], r[1], r[2], r[3]): (('%02X' % r[4]), r[5], {'S': 'SAVED', 'I': 'INIT', 'C': 'CLOBBERED'}.get(r[6], r[6])) for r in rows}
    print('same keys:', set(prep) == set(mine))
    diffs = [(k, prep[k], mine[k]) for k in prep if k in mine and prep[k] != mine[k]]
    print('state diffs:', diffs[:10])
