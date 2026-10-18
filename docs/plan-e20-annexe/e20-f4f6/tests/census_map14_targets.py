# Read-only census: records of a map with their class bits and body sizes (map-local or common bank).
import json, sys
ROOT = 'D:/development/repo/alundra-casaengine-project-converter/data-extracted/data'
mid = int(sys.argv[1]) if len(sys.argv) > 1 else 14
d = json.load(open(f'{ROOT}/map_{mid}.json', encoding='utf-8'))
g = json.load(open(f'{ROOT}/map_alundra.json', encoding='utf-8'))
def hdr(rec_list, idx):
    r = rec_list[idx]
    return None if r is None else r.get('Header')
ents = d['SpriteInfo']['Entities']['Entities']
local = d['SpriteInfo']['SpriteRecords']
glob = g['SpriteInfo']['SpriteRecords'] if 'SpriteInfo' in g else g['SpriteRecords']
print('rec  bank   idx  flags(MoreFlags)  ClassA  sizeXYZ  offZ  pos(px) height  ProgramLoad/Tick/Touch/Deact')
for i, e in enumerate(ents):
    if e is None: continue
    ismap = (e['SpriteDirection'] & 0x80) != 0
    h = hdr(local if ismap else glob, e['SpriteTableIndex'])
    if h is None:
        print(i, 'map' if ismap else 'glob', e['SpriteTableIndex'], 'NO HEADER'); continue
    print(i, 'map ' if ismap else 'glob', e['SpriteTableIndex'], hex(h['MoreFlags']), 'A' if h['MoreFlags'] & 1 else '-', (h['SizeX'], h['SizeY'], h['SizeZ']), h['OffsetZ'],
          (e['XPos']*12+12, e['YPos']*8+8), e['Height'], (h['ProgramLoad'], h['ProgramTick'], h['ProgramTouch'], h['ProgramDeactivate']), 'zone', (e['XMin'], e['YMin'], e['XMax'], e['YMax']))
