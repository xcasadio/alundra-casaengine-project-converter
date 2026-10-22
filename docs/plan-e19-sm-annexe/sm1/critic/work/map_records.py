"""Critic: dump the entity records of a map (Index, sprite, programs, position, zone, enabled).  usage: map_records.py <mapIndex> [lo hi]"""
import json, os, sys

REPO = 'D:/development/repo/alundra-casaengine-project-converter'
idx = json.load(open(REPO + '/alundra-project/Maps/world-index.json', encoding='utf-8'))


def tmj(mid):
    rel = idx[str(mid)].replace(chr(92), '/')
    base = os.path.splitext(os.path.basename(rel))[0]
    return os.path.dirname(REPO + '/alundra-project/' + rel) + '/tilemap/' + base + '.tmj'


def main():
    mid = int(sys.argv[1])
    lo = int(sys.argv[2]) if len(sys.argv) > 2 else 0
    hi = int(sys.argv[3]) if len(sys.argv) > 3 else 999
    d = json.load(open(tmj(mid), encoding='utf-8-sig'))
    for l in d['layers']:
        if l['name'] == 'Entities':
            for o in l['objects']:
                pr = {q['name']: q['value'] for q in o['properties']}
                if lo <= pr['Index'] <= hi:
                    print('id %3d spr %4s dir %3s en %s progs A%s B%s C%s D%s E%s F%s pos(%s,%s,h%s) zone x[%s..%s] y[%s..%s] %s' % (
                        pr['Index'], pr['SpriteTableIndex'], pr.get('SpriteDirection'), pr.get('IsEnabled'),
                        pr.get('EventCodesA_LoadIndex'), pr.get('EventCodesB_MapIndex'), pr.get('EventCodesC_TickIndex'),
                        pr.get('EventCodesD_TouchIndex'), pr.get('EventCodesE_DeactivateIndex'), pr.get('EventCodesF_InteractIndex'),
                        pr.get('XPos'), pr.get('YPos'), pr.get('Height'), pr.get('XMin'), pr.get('XMax'), pr.get('YMin'), pr.get('YMax'),
                        pr.get('EntityName')))
        if l['name'] in ('MapEvents',):
            for o in l['objects']:
                pr = {q['name']: q['value'] for q in o['properties']}
                print('MAPEVENT', {k: v for k, v in pr.items()})
        if l['name'] in ('Portals',) and len(sys.argv) > 4:
            for o in l['objects']:
                pr = {q['name']: q['value'] for q in o['properties']}
                print('PORTAL', o['name'], pr)


main()
