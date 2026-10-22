import json, glob, os, re, sys, collections
sys.stdout.reconfigure(encoding='utf-8')
root = r"D:\development\repo\alundra-casaengine-project-converter\alundra-project\Maps"
files = glob.glob(os.path.join(root, '*', '*', 'tilemap', '*.tmj'))
placed = []
nmaps = 0
props_seen = collections.Counter()
for f in files:
    mid = int(re.search(r'-(\d+)\.tmj$', f).group(1))
    d = json.load(open(f, encoding='utf-8'))
    nmaps += 1
    for l in d['layers']:
        if l['type'] == 'objectgroup' and l.get('name') == 'Entities':
            for o in l['objects']:
                p = {q['name']: q['value'] for q in o.get('properties', [])}
                for k in p: props_seen[k] += 1
                placed.append((mid, o, p))
print('maps', nmaps, 'entity objects', len(placed))
print('props', dict(props_seen))
sel = []
for mid, o, p in placed:
    sd = p.get('SpriteDirection')
    t = p.get('SpriteTableIndex')
    if sd is None or t is None: continue
    if p.get('IsEnabled', 1) in (0, False): continue
    if sd & 0x80: continue
    if 31 <= t <= 118 and t != 72:
        sel.append((mid, t, p))
print('placed pickups (exported):', len(sel), 'types', len(set(t for _, t, _ in sel)))
eff = lambda v: (v or 0) & 0x7F
nat = [s for s in sel if not eff(s[2].get('EventCodesC_TickIndex'))]
print('C native:', len(nat), 'maps', len(set(m for m, _, _ in nat)), 'A native among them', sum(1 for s in nat if not eff(s[2].get('EventCodesA_LoadIndex'))))
print('map 11 count among native:', sum(1 for m, _, _ in nat if m == 11))
print('flag _10 non zero:', sum(1 for s in sel if s[2].get('_10')), ' contents non-zero:', sum(1 for s in sel if s[2].get('Contents')))
print('A native / C script', sum(1 for s in sel if not eff(s[2].get('EventCodesA_LoadIndex')) and eff(s[2].get('EventCodesC_TickIndex'))), ' both script', sum(1 for s in sel if eff(s[2].get('EventCodesA_LoadIndex')) and eff(s[2].get('EventCodesC_TickIndex'))))
