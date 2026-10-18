import json, csv, collections, os
os.chdir('D:/development/repo/alundra-casaengine-project-converter')
idx = json.load(open('alundra-project/Maps/world-index.json', encoding='utf-8-sig'))
BS = chr(92)

def codes(m):
    w = idx[str(m)].replace(BS, '/')
    folder = os.path.dirname(w)
    name = os.path.basename(w)[:-6]
    p = f'alundra-project/{folder}/events/{name}.events.json'
    return json.load(open(p, encoding='utf-8-sig'))['Codes']

sm3 = {'A9', 'AA', 'B2', 'B4', 'B5', 'B6', 'B7', '9E'}
sm4 = {'72', '88', '96', '97', 'B3', '15', '66', '7A'}
rows = list(csv.DictReader(open('docs/plan-e19-sm-annexe/skipped-sites.tsv', encoding='utf-8'), delimiter='\t'))
print(len(rows), list(rows[0].keys()))
cache = {}
bad = []
stat = {'sm3': collections.Counter(), 'sm4': collections.Counter()}
maps = {'sm3': set(), 'sm4': set()}
per_op_maps = collections.defaultdict(set)
for r in rows:
    op = r['opcode'].upper()
    g = 'sm3' if op in sm3 else 'sm4' if op in sm4 else None
    if not g:
        continue
    m = int(r['map'])
    pc = int(r['pc'])
    if m not in cache:
        cache[m] = codes(m)
    c = cache[m]
    if c[pc] != int(op, 16):
        bad.append((m, pc, op, c[pc]))
    stat[g][op] += 1
    maps[g].add(m)
    per_op_maps[op].add(m)
print('sm3', sum(stat['sm3'].values()), dict(stat['sm3']), 'maps', len(maps['sm3']), sorted(maps['sm3']))
print('sm4', sum(stat['sm4'].values()), dict(stat['sm4']), 'maps', len(maps['sm4']))
print('mismatches at HEAD data:', bad[:10], len(bad))
for op in sorted(per_op_maps):
    print(op, len(per_op_maps[op]), sorted(per_op_maps[op])[:20])
