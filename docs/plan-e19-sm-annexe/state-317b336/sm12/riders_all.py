import json, glob, sys, os, csv, collections
ROOT = 'D:/development/repo/alundra-casaengine-project-converter/alundra-project'
recs = json.load(open(ROOT + '/Data/sprite-records.json', encoding='utf-8'))
idx = json.load(open(ROOT + '/Maps/world-index.json', encoding='utf-8'))
rows = list(csv.DictReader(open('D:/development/repo/alundra-casaengine-project-converter/docs/plan-e19-sm-annexe/skipped-sites.tsv', encoding='utf-8'), delimiter='\t'))


def tilemap_of(m):
    rel = idx[str(m)].replace('\\', '/')
    folder = os.path.dirname(ROOT + '/' + rel)
    return glob.glob(folder + '/tilemap/*.tileMap')[0]


def flags(cp):
    r = recs.get(cp.get('PrefabAssetId'))
    if not r:
        return None
    return r['MoreFlags'] | (r['CanPickup'] << 8) | (r['FlagsPortraitShadowType'] << 16)


cache = {}


def entities(m):
    if m not in cache:
        d = json.load(open(tilemap_of(m), encoding='utf-8'))
        ents = {}
        for layer in d['object_layers']:
            if layer['name'] == 'Entities':
                for o in layer['objects']:
                    cp = o['custom_properties']
                    ents[int(cp['Index'])] = cp
        cache[m] = ents
    return cache[m]


summary = collections.defaultdict(collections.Counter)
detail = {}
for r in rows:
    op = r['opcode'].upper()
    if op not in ('60', '61'):
        continue
    m = int(r['map'])
    ents = entities(m)
    oper = int(r['operands'].split()[0], 16)
    prog = int(r['program'])
    slot = r['slot']
    key = (op, m)
    # owners of the C program
    owners = [i for i, cp in ents.items() if slot == 'C' and (int(cp['EventCodesC_TickIndex']) & 0x7F) == prog]
    cand = None
    if oper & 0x80 == 0:
        c = ents.get(oper)
        cand = c
    cflags = flags(cand) if cand else None
    oflags = [flags(ents[i]) for i in owners]
    ok_owner = all(f is not None and (f & 0x80) for f in oflags) if oflags else None
    ok_rider = (cflags is not None and (cflags & 0x4100) == 0x100) if cand else None
    summary[key]['sites'] += 1
    summary[key]['owner_collidable=%s' % ok_owner] += 1
    summary[key]['rider_grid=%s' % ok_rider] += 1
    detail.setdefault(key, []).append((r['slot'], prog, int(r['pc']), '%02X' % oper, owners[:3], [hex(f) if f is not None else None for f in oflags[:2]], hex(cflags) if cflags is not None else None))
for k in sorted(summary):
    print(k, dict(summary[k]))
for k in sorted(detail):
    print(k, detail[k][:2])
