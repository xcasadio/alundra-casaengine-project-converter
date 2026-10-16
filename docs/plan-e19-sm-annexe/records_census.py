import json, os, collections
PROJECT = 'D:/development/repo/alundra-casaengine-project-converter/alundra-project'
index = json.load(open(PROJECT + '/Maps/world-index.json', encoding='utf-8'))
tot = collections.Counter()
rec = 0
mapevents = 0
bit80_clear = []
for mid_s, rel in index.items():
    rel = rel.replace('\\', '/')
    folder = os.path.dirname(PROJECT + '/' + rel)
    tm = [f for f in os.listdir(folder + '/tilemap') if f.endswith('.tileMap')][0]
    t = json.load(open(folder + '/tilemap/' + tm, encoding='utf-8'))
    for layer in t['object_layers']:
        if layer['name'] == 'MapEvents':
            mapevents += len(layer['objects'])
        if layer['name'] == 'Entities':
            for o in layer['objects']:
                rec += 1
                p = o['custom_properties']
                for slot, key in zip('ACDEF', ['EventCodesA_LoadIndex', 'EventCodesC_TickIndex', 'EventCodesD_TouchIndex', 'EventCodesE_DeactivateIndex', 'EventCodesF_InteractIndex']):
                    raw = int(p[key])
                    if raw & 0x7F:
                        tot[(slot, 'script')] += 1
                        if not raw & 0x80:
                            bit80_clear.append((int(mid_s), slot, o['name'], raw, p.get('EntityName'), p.get('SpriteTableIndex')))
                    else:
                        tot[(slot, 'native')] += 1
print('entity records', rec, 'map events', mapevents)
for k, v in sorted(tot.items()):
    print(k, v)
print('script index nonzero with bit 0x80 clear:')
for r in bit80_clear:
    print(r)
