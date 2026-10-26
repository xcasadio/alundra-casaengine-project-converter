import json, glob, re, collections, sys
R="D:/development/repo/alundra-casaengine-project-converter/data-extracted/data/"
e19 = collections.defaultdict(list)   # (page, sid) -> header E19
lids = collections.defaultdict(list)  # map -> records using global 241..243
bombs = collections.defaultdict(list) # map -> records using global 2
d12 = []
for f in glob.glob(R+'map_*.json'):
    mm = re.search(r'map_(\d+|alundra)\.json$', f.replace(chr(92), '/'))
    if not mm: continue
    mid = mm.group(1)
    try: d = json.load(open(f, encoding='utf-8-sig'))
    except Exception: continue
    if 'SpriteInfo' not in d: continue
    si = d['SpriteInfo']
    for x in si['SpriteRecords']:
        if not x: continue
        h = x['Header']
        if h['ProgramDeactivate'] == 19: e19[(mid, h['Sector5Id'])].append(1)
        if (h['ProgramDeactivate'], h['ProgramTouch'], h['ProgramTick']) in ((12,30,62),) : d12.append((mid, h['Sector5Id']))
        if h['ProgramDeactivate'] == 12 or h['ProgramTouch']==30 or h['ProgramTick']==62: d12.append(('any',mid,h['Sector5Id'],h['ProgramTick'],h['ProgramTouch'],h['ProgramDeactivate']))
    if mid == 'alundra': continue
    for r in ((si.get('Entities') or {}).get('Entities') or []):
        if not r: continue
        t = r.get('SpriteTableIndex'); sd = r.get('SpriteDirection', 0)
        if (sd & 0x80) == 0 and t in (241,242,243): lids[mid].append((t, r.get('XPos'), r.get('YPos'), r.get('Height'), r.get('IsEnabled')))
        if (sd & 0x80) == 0 and t == 2: bombs[mid].append((t, r.get('EventCodesC_TickIndex'), r.get('IsEnabled')))
print('E19 headers:', len(e19), sorted(e19.keys(), key=lambda k: (k[0]!='alundra', str(k))))
print('maps with records on global 241-243:', {k: v for k, v in sorted(lids.items(), key=lambda kv: int(kv[0]))})
print('maps with records on global bomb 2:', {k: v for k, v in sorted(bombs.items(), key=lambda kv: int(kv[0]))})
print('headers with C62/D30/E12 (any):', sorted(set(d12), key=str)[:20])
