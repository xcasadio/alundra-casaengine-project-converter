"""Read-only: for each sm3 site, the entity records / map events that run its program (from the exported tileMap object layers)."""
import json, os, sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'tools'))
import corpus
PROJECT = corpus.PROJECT
idx = corpus._index
def tilemap(mid):
    rel = idx[str(mid)].replace(chr(92), '/')
    base = os.path.splitext(os.path.basename(rel))[0]
    p = PROJECT + '/' + os.path.dirname(rel) + '/tilemap/' + base + '.tileMap'
    return base, json.load(open(p, encoding='utf-8'))
SITES = [(54,'B',14),(159,'C',13),(160,'C',1),(160,'C',6),(258,'B',6),(298,'B',6),(421,'C',8),(421,'C',14),(421,'C',17),(423,'C',25),(423,'C',26),
         (424,'C',3),(424,'C',4),(424,'C',5),(424,'C',6),(424,'C',9),(424,'C',15),(424,'C',16),(424,'C',17),(425,'C',12),(426,'C',13),(426,'C',14),
         (426,'C',15),(426,'C',16),(426,'C',17),(440,'B',9),(440,'C',2),(440,'C',3),(440,'C',4),(440,'C',16),(440,'C',18),(440,'C',21)]
KEY = {'A':'EventCodesA_LoadIndex','B':'EventCodesB_MapIndex','C':'EventCodesC_TickIndex','D':'EventCodesD_TouchIndex','E':'EventCodesE_DeactivateIndex','F':'EventCodesF_InteractIndex'}
cache = {}
for mid, slot, prog in SITES:
    if mid not in cache:
        cache[mid] = tilemap(mid)
    base, d = cache[mid]
    print('== map %d %s  %s[%d]' % (mid, base, slot, prog))
    for layer in d['object_layers']:
        if layer['name'] == 'Entities':
            for o in layer['objects']:
                cp = o['custom_properties']
                v = int(cp[KEY[slot]])
                if (v & 0x7F) == prog and (slot != 'B'):
                    print('   Entities rec %s  sprite %s  tile(%s,%s) px(%s,%s) Height %s  zone x%s..%s y%s..%s  flags: A=%s B=%s C=%s D=%s E=%s F=%s name=%s' % (
                        cp['Index'], cp['SpriteTableIndex'], cp['DisplayX'], cp['DisplayY'], cp['DisplayPixelX'], cp['DisplayPixelY'], cp['Height'],
                        cp['XMin'], cp['XMax'], cp['YMin'], cp['YMax'], cp['EventCodesA_LoadIndex'], cp['EventCodesB_MapIndex'], cp['EventCodesC_TickIndex'], cp['EventCodesD_TouchIndex'], cp['EventCodesE_DeactivateIndex'], cp['EventCodesF_InteractIndex'], cp.get('EntityName')))
        if layer['name'] == 'MapEvents' and slot == 'B':
            for o in layer['objects']:
                cp = o['custom_properties']
                print('   MapEvent', json.dumps(cp)[:300])
