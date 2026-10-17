#!/usr/bin/env python3
"""Read-only census of the liftable placements (pickup kind != 0) in the exported alundra-project maps.

Flags = MoreFlags | CanPickup << 8 | FlagsPortraitShadowType << 16  (EntitySpawnFactory.ApplyHeaderInitialization)
kind  = (Flags & 0x600) >> 9 = (CanPickup >> 1) & 3 ; Collidable = MoreFlags & 0x80
Position of a record (EntityRecordMapper): X = XPos*12+12 px, Y = YPos*8+8 px, Z = Height<<19 raw = Height*8 px.
Box (logical px, semi-open): [Pos+Offset, Pos+Offset+Size).

Usage: census_liftables.py [--json out.json] ; prints a table of maps.
"""
import json, os, re, sys, glob

ROOT = 'D:/development/repo/alundra-casaengine-project-converter/alundra-project'
CHAIN = [389, 390, 476, 478, 392, 391, 416, 163, 162, 165, 164, 172, 169, 170, 171, 173, 174, 175, 10, 179, 176, 177, 178, 180, 181,
         182, 135, 183, 184, 185]  # AlundraStoryChainOpcodeAudit.ChainMaps (Alundra.Tests/AlundraStoryChainOpcodeAudit.cs:37)


def load_records():
    return json.load(open(f'{ROOT}/Data/sprite-records.json', encoding='utf-8'))


def map_files():
    out = {}
    for f in glob.glob(f'{ROOT}/Maps/*/*/tilemap/*.tileMap'):
        base = os.path.basename(f)
        m = re.search(r'-(\d+)\.tileMap$', base)
        if m:
            out[int(m.group(1))] = f
    return out


def entities_of(path):
    d = json.load(open(path, encoding='utf-8'))
    for l in d['object_layers']:
        if l['name'] == 'Entities':
            return d, l['objects']
    return d, []


def build(records, path, mapidx):
    d, objs = entities_of(path)
    rows = []
    for o in objs:
        p = o['custom_properties']
        pid = p.get('PrefabAssetId')
        h = records.get(pid)
        if h is None:
            continue
        more = h['MoreFlags']; cp = h['CanPickup']; fps = h['FlagsPortraitShadowType']
        flags = more | (cp << 8) | (fps << 16)
        kind = (flags & 0x600) >> 9
        coll = (flags & 0x80) != 0
        x = int(p['XPos']) * 12 + 12
        y = int(p['YPos']) * 8 + 8
        z = int(p.get('Height', 0)) * 8
        rows.append(dict(map=mapidx, idx=int(p['Index']), name=p.get('EntityName'), enabled=p.get('IsEnabled'),
                         xpos=int(p['XPos']), ypos=int(p['YPos']), height=int(p.get('Height', 0)),
                         x=x, y=y, z=z, flags=flags, kind=kind, collidable=coll,
                         offx=h['OffsetX'], offy=h['OffsetY'], offz=h['OffsetZ'],
                         sx=h['SizeX'], sy=h['SizeY'], sz=h['SizeZ'],
                         progs=[p.get('EventCodes%s' % k) for k in ('A_LoadIndex', 'B_MapIndex', 'C_TickIndex', 'D_TouchIndex', 'E_DeactivateIndex', 'F_InteractIndex')],
                         prog_hdr=(h['ProgramLoad'], h['ProgramTick'], h['ProgramTouch'], h['ProgramDeactivate'], h['ProgramInteract']),
                         bank=h.get('BankKey')))
    return rows


def main():
    records = load_records()
    files = map_files()
    allrows = []
    for mi in sorted(files):
        allrows += build(records, files[mi], mi)
    if '--json' in sys.argv:
        out = sys.argv[sys.argv.index('--json') + 1]
        json.dump(allrows, open(out, 'w', encoding='utf-8'), indent=0)
    lift = [r for r in allrows if r['kind'] != 0]
    liftc = [r for r in lift if r['collidable']]
    print('maps with an exported tileMap:', len(files))
    print('entity records total:', len(allrows), ' liftable (kind!=0):', len(lift), ' liftable AND collidable:', len(liftc))
    bymap = {}
    for r in lift:
        bymap.setdefault(r['map'], []).append(r)
    print('maps with >=1 liftable:', len(bymap))
    print()
    print('CHAIN MAPS (30)  map: liftable records / of which Collidable / kinds / names')
    tot = 0; totc = 0; n = 0
    for m in CHAIN:
        rs = bymap.get(m, [])
        kinds = {}
        names = {}
        for r in rs:
            kinds[r['kind']] = kinds.get(r['kind'], 0) + 1
            names[r['name']] = names.get(r['name'], 0) + 1
        c = sum(1 for r in rs if r['collidable'])
        tot += len(rs); totc += c; n += 1 if rs else 0
        print(f'  {m}: {len(rs)} / {c} / kinds={kinds} / {names}')
    print(f'  TOTAL chain: {tot} liftable records ({totc} collidable) on {n} maps')


if __name__ == '__main__':
    main()
