#!/usr/bin/env python
"""List every reachable site of an opcode that the DLL skips by size (label script2). Read-only.
Output: skipped-sites.tsv with the operands and the NEXT instruction (decoded with the size table), to see whether a
Result-writing opcode is followed by a consumer (03/04/4A/4B/79/7E/7F...).
"""
import csv, json, os, re, collections
REPO = 'D:/development/repo/alundra-casaengine-project-converter'
PROJECT = REPO + '/alundra-project'
OUT = os.path.dirname(os.path.abspath(__file__))
src = open(REPO + '/Alundra/Scripts/EventOpcodeSizeTable.cs', encoding='utf-8').read()
SIZES = {int(m.group(1), 16): int(m.group(2)) for m in re.finditer(r'\{\s*0x([0-9A-Fa-f]{2}),\s*new\((\d+),', src)}
cases = {int(k, 16) for k in json.load(open(OUT + '/cases.json'))}
SKIPPED = sorted(op for op in SIZES if op not in cases and op not in (0, 0xFF) and SIZES[op] > 0)
index = json.load(open(PROJECT + '/Maps/world-index.json', encoding='utf-8'))
names = {}
docs = {}
for mid, rel in index.items():
    rel = rel.replace('\\', '/')
    base = os.path.splitext(os.path.basename(rel))[0]
    names[int(mid)] = base
    ev = os.path.dirname(PROJECT + '/' + rel) + '/events/' + base + '.events.json'
    docs[int(mid)] = ev
CONSUMERS = {0x03, 0x04, 0x4A, 0x4B, 0x79, 0x7E, 0x7F}
rows = list(csv.DictReader(open(OUT + '/census-sites.tsv', encoding='utf-8'), delimiter='\t'))
by_map = collections.defaultdict(list)
for r in rows:
    op = int(r['opcode'], 16)
    if op in SKIPPED:
        by_map[int(r['map'])].append(r)
out = []
cache = {}
for m, rs in sorted(by_map.items()):
    codes = json.load(open(docs[m], encoding='utf-8'))['Codes']
    seen = set()
    for r in rs:
        key = (r['opcode'], r['pc'])
        pc = int(r['pc']); op = int(r['opcode'], 16)
        size = SIZES[op]
        operands = ' '.join('%02X' % b for b in codes[pc + 1:pc + size])
        nxt = pc + size
        nop = codes[nxt] if nxt < len(codes) else -1
        out.append((m, names[m], r['slot'], int(r['program']), pc, op, operands, nop, nop in CONSUMERS))
with open(OUT + '/skipped-sites.tsv', 'w', encoding='utf-8', newline='\n') as f:
    f.write('map\tmap_name\tslot\tprogram\tpc\topcode\toperands\tnext_opcode\tnext_is_Result_consumer\n')
    for o in out:
        f.write('%d\t%s\t%s\t%d\t%d\t%02X\t%s\t%s\t%s\n' % (o[0], o[1], o[2], o[3], o[4], o[5], o[6], ('%02X' % o[7]) if o[7] >= 0 else '-', 'Y' if o[8] else 'N'))
print(len(out), 'skipped site-programs in', len(by_map), 'maps')
# summary per opcode: sites, maps, consumer-follow
summ = collections.defaultdict(lambda: [0, set(), 0])
for o in out:
    s = summ[o[5]]
    s[0] += 1; s[1].add(o[0]); s[2] += 1 if o[8] else 0
for op in sorted(summ):
    s = summ[op]
    print('%02X sites=%d maps=%d followed_by_consumer=%d' % (op, s[0], len(s[1]), s[2]))
