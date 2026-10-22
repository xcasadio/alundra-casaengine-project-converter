"""For every raw-id site (operand < 0x80) of 0x60/0x61: does the id name a record of the map's Entities layer? (the binary's validator 0x8003C914 is fatal for an invalid id.)
Also lists the group types used by the sm1 sites. Usage: python -B rawid_check.py"""
import csv, json, glob, os
from collections import Counter
ROOT = 'D:/development/repo/alundra-casaengine-project-converter/alundra-project'
BS = chr(92)
idx = json.load(open(ROOT + '/Maps/world-index.json', encoding='utf-8'))
rows = [r for r in csv.DictReader(open('D:/development/repo/alundra-casaengine-project-converter/docs/plan-e19-sm-annexe/skipped-sites.tsv', encoding='utf-8'), delimiter='\t') if r['opcode'].upper() in ('60', '61')]
cache = {}


def records(m):
    if m not in cache:
        rel = idx[str(m)].replace(BS, '/')
        f = glob.glob(os.path.dirname(ROOT + '/' + rel) + '/tilemap/*.tileMap')[0]
        d = json.load(open(f, encoding='utf-8'))
        ids = {}
        for layer in d['object_layers']:
            if layer['name'] == 'Entities':
                for o in layer['objects']:
                    cp = o['custom_properties']
                    ids[int(cp['Index'])] = cp
        cache[m] = ids
    return cache[m]


bad = []
types = Counter()
for r in rows:
    m = int(r['map'])
    t = int(r['operands'].split()[0], 16)
    if t & 0x80:
        types['group 0x%02X' % t] += 1
        continue
    types['raw id'] += 1
    rec = records(m).get(t)
    if rec is None:
        bad.append((m, r['program'], r['pc'], t))
    elif rec.get('IsEnabled') != '1':
        bad.append((m, r['program'], r['pc'], t, 'disabled'))
print('sites', len(rows), 'types', dict(types))
print('raw ids that are not an enabled record of their map:', bad)
