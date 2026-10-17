"""census_platforms.py - read-only: the platform-like prefabs, their header codes and where they are placed (data-extracted maps)."""
import json, glob, re, collections
R = r"D:/development/repo/alundra-casaengine-project-converter"
D = R + "/data-extracted/data"
SPR = json.load(open(R + '/alundra-project/Data/sprite-records.json', encoding='utf-8-sig'))
names = {}
for f in glob.glob(R + '/alundra-project/Entities/*/*.entity'):
    j = json.load(open(f, encoding='utf-8-sig')); names[j['id']] = j['name']
WANT = [k for k, n in names.items() if re.search(r'Plateforme|asiba|Chariot|Flotteur|pont en bois|Bloc . pousser', n)]
SIG = collections.defaultdict(list)
for k in WANT:
    v = SPR[k]
    SIG[(v['MoreFlags'], v['CanPickup'], v['FlagsPortraitShadowType'], v['ProgramLoad'], v['ProgramTick'], v['ProgramTouch'], v['ProgramDeactivate'], v['ProgramInteract'], v['SizeX'], v['SizeY'], v['SizeZ'], v['BreakEffect'], v['Contents'])].append(names[k])
cnt = collections.Counter(); maps = collections.defaultdict(set)
for f in sorted(p for p in glob.glob(D + '/map_*.json') if re.search(r'map_\d+\.json$', p)):
    n = int(re.search(r'map_(\d+)\.json', f).group(1))
    d = json.load(open(f, encoding='utf-8-sig'))
    si = d['SpriteInfo']; recs = si['SpriteRecords']
    for e in si['Entities']['Entities']:
        if not e or not e['IsEnabled']: continue
        r = recs[e['SpriteTableIndex']] if e['SpriteTableIndex'] < len(recs) else None
        if r is None: continue
        h = r['Header']
        sig = (h['MoreFlags'], h['CanPickup'], h['FlagsPortraitShadowType'], h['ProgramLoad'], h['ProgramTick'], h['ProgramTouch'], h['ProgramDeactivate'], h['ProgramInteract'], h['SizeX'], h['SizeY'], h['SizeZ'], h['BreakEffect'], h['Contents'])
        if sig in SIG:
            cnt[sig] += 1; maps[sig].add(n)
for sig, nms in SIG.items():
    print('%-70s MoreFlags=0x%02X C=%d D=%d E=%d size=%s placements=%d maps=%d %s' % (' / '.join(nms)[:70], sig[0], sig[4], sig[5], sig[6], sig[8:11], cnt[sig], len(maps[sig]), sorted(maps[sig])[:10]))
