"""Flow-aware check of the position memo (0x6A) against the wait memos, per axis, on every C program that has 0x6B/0x6C/0x6D.
Per-axis clobber sets come from the BINARY (stateuse.py): X = Parameters[2] is written by 0x0B 0x1C 0x1D 0x1E 0x1F 0x20-0x23 0x37;
Y = Parameters[3] by 0x0B 0x1E 0x1F; Z = Parameters[4] by nothing ported (0x5F is not ported). Abstract states: INIT / SAVED / CLOB.
The CFG follows census.py's successors (both branches of every conditional); 0x7D/0x7E-0x81 (stored-return family) are treated as dead ends.
Reports, for each reader, the set of states that can reach it. Read-only."""
import csv, collections, struct
import corpus

CLOB = {'X': {0x0B, 0x1C, 0x1D, 0x1E, 0x1F, 0x20, 0x21, 0x22, 0x23, 0x37}, 'Y': {0x0B, 0x1E, 0x1F}, 'Z': set()}
AXIS = {0x6B: 'X', 0x6C: 'Y', 0x6D: 'Z'}


def s16(codes, lo):
    v = codes[lo] | (codes[lo + 1] << 8) if lo + 1 < len(codes) else 0
    return v - 0x10000 if v >= 0x8000 else v


def succ(codes, entry, pc):
    op = codes[pc]
    size = corpus.SIZES.get(op, 1) or 1
    nxt = pc + size
    if op == 0xFF:
        return []
    if op == 0x02:
        return [pc + s16(codes, pc + 1)]
    if op in (0x03, 0x04, 0x74):
        return [nxt, pc + s16(codes, pc + 1)]
    if op in (0x30, 0x31):
        return [nxt, pc + s16(codes, pc + 3)]
    if op == 0x49:
        return [entry]
    if op in (0x4A, 0x4B):
        return [nxt, entry]
    if op in (0x57, 0x58):
        return [pc + s16(codes, pc + 1 + 2 * d) for d in range(4)]
    if op == 0x78:
        return [pc + s16(codes, pc + 1)]
    if op == 0x79:
        return [nxt, pc + s16(codes, pc + 1)]
    if op in (0x7B, 0x7C):
        return [nxt, pc + s16(codes, pc + 3)]
    if op in (0x7D, 0x7E, 0x7F, 0x80, 0x81):
        return []
    return [nxt]


rows = list(csv.DictReader(open('D:/development/repo/alundra-casaengine-project-converter/docs/plan-e19-sm-annexe/skipped-sites.tsv', encoding='utf-8'), delimiter='\t'))
progs = collections.OrderedDict()
for r in rows:
    op = int(r['opcode'], 16)
    if 0x6A <= op <= 0x6D:
        progs[(int(r['map']), r['slot'], int(r['program']))] = 1
summary = collections.Counter()
visited = {}
for (m, s, p) in progs:
    base, d = corpus.load(m)
    codes = d['Codes']
    entry = d[corpus.TABLE_KEYS[s]][p]
    # per-axis fixpoint
    for axis in 'XYZ':
        states = collections.defaultdict(set)
        states[entry].add('INIT')
        work = [entry]
        while work:
            pc = work.pop()
            if pc < 0 or pc >= len(codes):
                continue
            op = codes[pc]
            cur = set(states[pc])
            out = set()
            for st in cur:
                if op == 0x6A:
                    out.add('SAVED')
                elif op in CLOB[axis]:
                    out.add('CLOB')
                else:
                    out.add(st)
            for q in succ(codes, entry, pc):
                if q < 0 or q >= len(codes):
                    continue
                if not out <= states[q]:
                    states[q] |= out
                    work.append(q)
        visited_axis = visited.setdefault((m,s,p,axis), set())
        visited_axis.update(states.keys())
        for pc, st in sorted(states.items()):
            op = codes[pc]
            if op in AXIS and AXIS[op] == axis:
                key = tuple(sorted(st))
                summary[(axis, key)] += 1
                if key != ('SAVED',):
                    print('map %d %s[%d] @%d  %02X  states reaching the read: %s' % (m, s, p, pc, op, key))
print()
for k, v in sorted(summary.items()):
    print(k, v)

print('--- 6B/6C/6D sites never visited by the CFG of the axis:')
for r in rows:
    op=int(r['opcode'],16)
    if op in AXIS:
        key=(int(r['map']),r['slot'],int(r['program']),AXIS[op])
        if int(r['pc']) not in visited.get(key,set()):
            print(r['map'],r['slot'],r['program'],r['pc'],r['opcode'],r['operands'])
