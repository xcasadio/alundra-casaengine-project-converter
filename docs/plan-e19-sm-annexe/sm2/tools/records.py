"""Entity records that run a program with a 0x6A-0x6D site (read-only). python -B records.py"""
import collections, json, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import corpus2 as C, tilemap2 as T, memo_static as M

progs, sites = M.programs_from_tsv()
by_map = collections.defaultdict(set)
for m, s, p in progs:
    by_map[m].add((s, p))

KEYS = {'A': 'EventCodesA_LoadIndex', 'B': 'EventCodesB_MapIndex', 'C': 'EventCodesC_TickIndex', 'D': 'EventCodesD_TouchIndex',
        'E': 'EventCodesE_DeactivateIndex', 'F': 'EventCodesF_InteractIndex'}

if __name__ == '__main__':
    for m in sorted(by_map):
        base, d = C.load(m)
        tm = T.load_tilemap(m)
        ms = tm['map_size']
        print('=== map %d %s  map_size=%s  program targets %s' % (m, base, ms, sorted(by_map[m])))
        for lname, o in T.objects(m):
            if lname != 'Entities':
                continue
            cp = o['custom_properties']
            raw = int(cp[KEYS['C']])
            masked = raw & 0x7F
            if ('C', masked) in by_map[m]:
                print('  rec %-3s %-34s raw C=%3d (%s) -> C[%d]  sprite=%s  tile=(%s,%s) h=%s dispPix=(%s,%s) dir=%s enabled=%s A=%s B=%s D=%s E=%s F=%s prefab=%s'
                      % (cp['Index'], cp.get('EntityName', ''), raw, 'bit0x80 SET' if raw & 0x80 else 'bit0x80 CLEAR', masked,
                         cp['SpriteTableIndex'], cp['XPos'], cp['YPos'], cp['Height'], cp['DisplayPixelX'], cp['DisplayPixelY'], cp['SpriteDirection'], cp['IsEnabled'],
                         cp[KEYS['A']], cp[KEYS['B']], cp[KEYS['D']], cp[KEYS['E']], cp[KEYS['F']], cp.get('PrefabAssetId', '')[:8]))
