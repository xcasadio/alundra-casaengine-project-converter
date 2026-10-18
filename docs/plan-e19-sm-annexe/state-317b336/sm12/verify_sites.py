import csv, sys, os, json, collections
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import decode
REPO = decode.REPO
rows = list(csv.DictReader(open(REPO + '/docs/plan-e19-sm-annexe/skipped-sites.tsv', encoding='utf-8'), delimiter='\t'))
want = {'3C', '3D', '60', '61', '6A', '6B', '6C', '6D'}
cache = {}
bad = []
n = 0
for r in rows:
    op = r['opcode'].upper()
    if op not in want:
        continue
    n += 1
    m = int(r['map'])
    if m not in cache:
        cache[m] = decode.load(m)
    d = cache[m]
    pc = int(r['pc'])
    codes = d['Codes']
    ok = codes[pc] == int(op, 16)
    # operand bytes
    size = decode.SIZES[int(op, 16)]
    ops = ' '.join('%02X' % b for b in codes[pc + 1:pc + size])
    ok2 = ops == r['operands'].upper()
    # program entry table check
    table = d['EventCodes%sTable' % r['slot']]
    prog = int(r['program'])
    if not ok or not ok2:
        bad.append((m, r['slot'], prog, pc, op, codes[pc], ops, r['operands']))
print('checked', n, 'bad', len(bad))
for b in bad[:10]:
    print(b)
# also: sizes of the 8 opcodes at HEAD vs the annex
print({hex(k): decode.SIZES[k] for k in (0x3C, 0x3D, 0x60, 0x61, 0x6A, 0x6B, 0x6C, 0x6D)})
