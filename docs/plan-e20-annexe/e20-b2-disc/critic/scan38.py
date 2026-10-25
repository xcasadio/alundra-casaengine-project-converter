import json, glob, re, os
D = r"D:/development/repo/alundra-casaengine-project-converter/data-extracted/data"
hits = []
chests = []
by8a = []
for f in glob.glob(D + "/map_*.json"):
    b = os.path.basename(f)
    m = re.fullmatch(r"map_(\d+)\.json", b)
    if not m:
        continue
    mi = int(m.group(1))
    d = json.load(open(f, encoding="utf-8"))
    E = d['SpriteInfo']['Entities']['Entities']
    for i, e in enumerate(E):
        if e is None:
            continue
        if e['SpriteTableIndex'] == 38:
            hits.append((mi, i, e['IsEnabled'], e['XPos'], e['YPos'], e['Height']))
        if e.get('Contents') == 8:
            chests.append((mi, i, e['SpriteTableIndex'], e['IsEnabled']))
print('placed records with SpriteTableIndex 38:', sorted(hits))
print('records with Contents == 8:', sorted(chests))
# the 0x8A/0x8B operands from the event scan: which records they name and what type those records have
S = json.load(open('scan8_out.json'))
types = {}
for k in ('8A', '8B'):
    for mi, pc, args in S[k]:
        d = json.load(open(D + "/map_%d.json" % mi, encoding="utf-8"))
        E = d['SpriteInfo']['Entities']['Entities']
        rid = args[0]
        t = E[rid]['SpriteTableIndex'] if rid < len(E) and E[rid] else None
        if t == 38:
            by8a.append((k, mi, pc, args))
print('spawn opcodes whose record has SpriteTableIndex 38:', by8a)
# the reader's / first critic's candidates: 0x8A [38] on maps 6, 52, 80 -> record 38 types
for mi in (6, 52, 80, 7):
    d = json.load(open(D + "/map_%d.json" % mi, encoding="utf-8"))
    E = d['SpriteInfo']['Entities']['Entities']
    r = E[38] if len(E) > 38 else None
    print('map', mi, 'record 38 ->', None if r is None else r['SpriteTableIndex'])
print('8A/8B sites on maps 6, 52, 80:', [(k, mi, pc, a) for k in ('8A', '8B') for mi, pc, a in S[k] if mi in (6, 52, 80)])
