"""For each of the 90 sm4 sites: linear scan of its program from the entry to the site (by size), listing 0x42 / 0x43 / 0x40 (program index
changes) and other opcodes that retarget or branch (jumps) found on the way. Flags the sites whose logic entity may differ from
the default (hero for B, owner for C/D/E/F)."""
import json, sys
sys.path.insert(0, '.')
import census_critic as c

idx = json.load(open(c.PROJ + '/Maps/world-index.json', encoding='utf-8'))
rows = [l.rstrip('\n').split('\t') for l in open('my-skipped-sites.tsv')][1:]
sm4 = {'0x15', '0x66', '0x72', '0x7A', '0x88', '0x96', '0x97', '0xB3'}
cache = {}
res = []
for r in rows:
    if r[4] not in sm4:
        continue
    mid, slot, prog, pc, op = int(r[0]), r[1], int(r[2]), int(r[3]), r[4]
    if mid not in cache:
        cache[mid] = c.load_map(mid, idx)
    ev = cache[mid][1]
    codes = ev['Codes']
    entry = c.tables(ev)[c.SLOTS.index(slot)][prog]
    p = entry
    seen42 = []
    seen43 = []
    jumps = []
    ok = False
    while p < len(codes):
        o = codes[p]
        if p == pc:
            ok = True
            break
        if o == 0x42:
            seen42.append(p)
        if o == 0x43:
            seen43.append(p)
        if o in (0x02, 0x03, 0x04, 0x30, 0x31, 0x74, 0x57, 0x58, 0x78, 0x79, 0x7A, 0x7B, 0x7C, 0x49, 0x4B):
            jumps.append((p, o))
        size = c.SIZES.get(o, 1) or 1
        if o == 0xFF:
            break
        p += size
    res.append((mid, slot, prog, pc, op, ok, seen42, seen43, len(jumps)))

for x in res:
    mid, slot, prog, pc, op, ok, s42, s43, nj = x
    flag = ''
    if not ok:
        flag = 'PC-NOT-ON-LINEAR-PATH'
    print(mid, slot, prog, pc, op, 'linear=%s' % ok, '0x42@%s' % s42 if s42 else '', '0x43@%s' % s43 if s43 else '', 'jumps=%d' % nj, flag)
