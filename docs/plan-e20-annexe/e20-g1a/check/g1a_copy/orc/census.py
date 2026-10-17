"""census.py - list sprite records with CanPickup kind != 0 (flags &0x600) and their programs/anim speeds/attack counts, over data-extracted/data/map_*.json (read only)"""
import json, glob, os, sys, collections
root = "D:/development/repo/alundra-casaengine-project-converter/data-extracted/data"
bal = json.load(open(root + "/BALANCE.BIN.json", encoding='utf-8-sig'))['EntriesBySpriteIndex']
res = collections.OrderedDict()
for f in sorted([p for p in glob.glob(root + "/map_*.json") if os.path.basename(p)[4:-5].isdigit()], key=lambda p: int(os.path.basename(p)[4:-5])):
    mid = int(os.path.basename(f)[4:-5])
    try:
        d = json.load(open(f, encoding='utf-8-sig'))
    except Exception as e:
        continue
    si = d.get('SpriteInfo')
    if not si: continue
    ents = si['Entities']['Entities']
    cnt = collections.Counter(e['SpriteTableIndex'] for e in ents if e is not None)
    for idx, rec in enumerate(si['SpriteRecords']):
        if rec is None or 'Header' not in rec: continue
        h = rec['Header']
        kind = (h['CanPickup'] >> 1) & 3
        if kind == 0 or idx not in cnt: continue
        key = (kind, h['ProgramTick'], h['ProgramTouch'], h['ProgramDeactivate'], h['SizeX'], h['SizeY'], h['SizeZ'], h['MoreFlags'], h['BreakEffect'], h['FlagsPortraitShadowType'])
        b = bal[idx | 0x100] if (idx | 0x100) < len(bal) else None
        sp = tuple((a['Speed'], a['IsZForceApplied']) for a in rec['AnimSets'][:5])
        res.setdefault(key, []).append((mid, idx, cnt[idx], b['AttackCount'] if b else None, sp))
for key, v in res.items():
    maps = sorted(set(x[0] for x in v))
    print('kind %d C%d D%d E%d size %dx%dx%d MoreFlags %02X Break %d Flags %02X : %d placements in %d (map,sprite) pairs; ex map %d sprite %d attacks %s anims(speed,izf)=%s' % (
        key[0], key[1], key[2], key[3], key[4], key[5], key[6], key[7], key[8], key[9], sum(x[2] for x in v), len(v), v[0][0], v[0][1], v[0][3], v[0][4]))
