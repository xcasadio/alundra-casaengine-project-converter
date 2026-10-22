"""ents.py MAP [slotkey=value...] : list the Entities / MapEvents records of a map with their program indexes."""
import sys, json
sys.path.insert(0, '.')
import census_critic as c

mid = int(sys.argv[1])
idx = json.load(open(c.PROJ + '/Maps/world-index.json', encoding='utf-8'))
base, ev, tm = c.load_map(mid, idx)
print(base, 'tables', {s: ev['EventCodes%sTable' % s] for s in c.SLOTS})
for layer in tm['object_layers']:
    if layer['name'] in ('Entities', 'MapEvents'):
        print('--', layer['name'], len(layer['objects']))
        for o in layer['objects']:
            p = o['custom_properties']
            if layer['name'] == 'Entities':
                keep = {k: p.get(k) for k in ['Index', 'IsEnabled', 'SpriteTableIndex', 'SpriteDirection', 'XPos', 'YPos', 'Height',
                                              'EventCodesA_LoadIndex', 'EventCodesB_MapIndex', 'EventCodesC_TickIndex', 'EventCodesD_TouchIndex',
                                              'EventCodesE_DeactivateIndex', 'EventCodesF_InteractIndex', 'EntityName']}
            else:
                keep = {k: p.get(k) for k in ['Index', 'X1', 'Y1', 'X2', 'Y2', 'EventCodesBIndex']}
            print(o['name'], keep)
