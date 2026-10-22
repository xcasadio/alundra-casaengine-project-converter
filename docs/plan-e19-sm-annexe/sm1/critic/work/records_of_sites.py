"""Critic: for each raw id named by a 0x60/0x61 site, print the record's sprite and event-program indexes (which family are the objects?)."""
import csv, json, os, collections

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
                out[pr['Index']] = (pr, o)
    return out


cache = {}
seen = set()
by = collections.defaultdict(list)
for r in sel:
    v = int(r['operands'].split()[0], 16)
    if v & 0x80:
        continue
    m = int(r['map'])
    if m not in cache:
        cache[m] = ents(m)
    key = (m, v)
    if key in seen:
        continue
    seen.add(key)
    pr, o = cache[m][v]
    by[m].append((v, pr['SpriteTableIndex'], pr.get('SpriteDirection'), pr.get('EntityName'), [pr.get('EventCodes%s' % s) for s in ('A_LoadIndex', 'B_MapIndex', 'C_TickIndex', 'D_TouchIndex', 'E_DeactivateIndex', 'F_InteractIndex')], pr.get('XPos'), pr.get('YPos'), pr.get('Height'), pr.get('IsEnabled'), pr.get('XMin'), pr.get('XMax'), pr.get('YMin'), pr.get('YMax')))
for m in sorted(by):
    print('map', m)
    for t in sorted(by[m]):
        print('   id %3d sprite %4s dir %s name %s progs %s pos (%s,%s,h%s) enabled %s zone x[%s..%s] y[%s..%s]' % t)
