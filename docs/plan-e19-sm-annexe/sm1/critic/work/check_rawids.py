"""Critic: every raw-id site of 0x60/0x61 (178) names an enabled record of its own map (the validator 0x8003C914 is fatal otherwise).
Read-only on the repository."""
import csv, json, os, sys

REPO = 'D:/development/repo/alundra-casaengine-project-converter'
rows = list(csv.DictReader(open(REPO + '/docs/plan-e19-sm-annexe/skipped-sites.tsv', encoding='utf-8'), delimiter='\t'))
sel = [r for r in rows if r['opcode'] in ('60', '61')]
idx = json.load(open(REPO + '/alundra-project/Maps/world-index.json', encoding='utf-8'))


def ents(mid):
    rel = idx[str(mid)].replace(chr(92), '/')
    base = os.path.splitext(os.path.basename(rel))[0]
    p = os.path.dirname(REPO + '/alundra-project/' + rel) + '/tilemap/' + base + '.tmj'
    d = json.load(open(p, encoding='utf-8-sig'))
    out = {}
    for l in d['layers']:
        if l['name'] == 'Entities':
            for o in l['objects']:
                pr = {q['name']: q['value'] for q in o['properties']}
                out[pr['Index']] = pr
    return out


cache = {}
bad = []
n = 0
hero = 0
for r in sel:
    v = int(r['operands'].split()[0], 16)
    if v & 0x80:
        hero += 1
        continue
    m = int(r['map'])
    if m not in cache:
        cache[m] = ents(m)
    e = cache[m].get(v)
    n += 1
    if e is None or not e.get('IsEnabled'):
        bad.append((m, r['slot'], r['program'], r['pc'], v, None if e is None else e.get('IsEnabled')))
print('raw-id sites', n, 'hero-group sites', hero, 'bad', bad)
for m in sorted(cache):
    print(m, 'records', len(cache[m]), 'max index', max(cache[m]))
