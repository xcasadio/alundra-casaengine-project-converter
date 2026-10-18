import csv, sys, os, collections
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import decode
REPO = decode.REPO
rows = list(csv.DictReader(open(REPO + '/docs/plan-e19-sm-annexe/skipped-sites.tsv', encoding='utf-8'), delimiter='\t'))


def linear(codes, entry):
    pc = entry
    ops = []
    while pc < len(codes):
        op = codes[pc]
        ops.append((pc, op))
        if op == 0xFF:
            break
        pc += decode.SIZES.get(op, 1) or 1
    return ops


res = collections.defaultdict(list)
cache = {}
for r in rows:
    op = r['opcode'].upper()
    if op not in ('6B', '6C', '6D'):
        continue
    m = int(r['map'])
    d = cache.setdefault(m, decode.load(m))
    table = d['EventCodes%sTable' % r['slot']]
    prog = int(r['program'])
    ops = linear(d['Codes'], table[prog])
    has6a = any(o == 0x6A for _, o in ops)
    # does a 0x6A come BEFORE (lower pc) the read in the linear scan?
    pc = int(r['pc'])
    before = any(o == 0x6A and p < pc for p, o in ops)
    res[(m, op)].append((r['slot'], prog, pc, has6a, before))
tot = 0
for k in sorted(res):
    no6a = [x for x in res[k] if not x[3]]
    nobefore = [x for x in res[k] if not x[4]]
    print(k, 'reads', len(res[k]), 'no 0x6A in linear program', len(no6a), 'no 0x6A before the read', len(nobefore))
    if k[1] == '6D' and nobefore:
        print('   ', nobefore)
    tot += len(no6a)
print('reads with no 6A in their linear program:', tot)
