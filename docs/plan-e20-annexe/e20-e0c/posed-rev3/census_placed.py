"""census_placed.py - the placed pickup objects of every map (global-page sprite types 31..118 except 72), read from data-extracted/.
Read-only. Page rule of the binary (0x8003A270): SpriteDirection bit 7 = map page, else global page.
Writes placed.json next to itself and prints the tables."""
import glob, json, os, re, sys, collections
sys.stdout.reconfigure(encoding='utf-8')
REPO = r"D:\development\repo\alundra-casaengine-project-converter"
DATA = os.path.join(REPO, 'data-extracted', 'data')
HERE = os.path.dirname(os.path.abspath(__file__))
props = json.load(open(os.path.join(REPO, 'alundra-project', 'Data', 'item-drop-properties.json'), encoding='utf-8'))
# EntityNames.csv: row index = page offset (global 0.., map 256..)
with open(os.path.join(REPO, 'alundra-datas-analyser', 'AlundraTools', 'AlundraTools', 'EntityNames.csv'), encoding='utf-8-sig') as fh:
    lines = fh.read().split("\n")
names = []
for ln in lines[1:]:
    if not ln.strip():
        continue
    cols = ln.rstrip("\r").split(";")
    names.append(cols[1].strip() if len(cols) > 1 else "")

def gname(t):
    return names[t] if t < len(names) else '?'

files = sorted(glob.glob(os.path.join(DATA, 'map_[0-9]*.json')), key=lambda p: int(re.search(r'map_(\d+)\.json', p).group(1)))
placed = []
nmaps = 0
total_enabled = 0
for p in files:
    mid = int(re.search(r'map_(\d+)\.json', p).group(1))
    d = json.load(open(p, encoding='utf-8'))
    nmaps += 1
    si = d['SpriteInfo']
    recs = si.get('SpriteRecords') or []
    ents = (si.get('Entities') or {}).get('Entities') or []
    for i, e in enumerate(ents):
        if e is None or not e.get('IsEnabled'):
            continue
        total_enabled += 1
        sd = e['SpriteDirection']
        if sd & 0x80:
            continue  # map page: C255/A255 are registered on the global page only
        t = e['SpriteTableIndex']
        if 31 <= t <= 118 and t != 72:
            item = t - 0x1E
            prop = props[item] if item < len(props) else None
            placed.append(dict(map=mid, idx=i, type=t, item=item, name=gname(t), x=e['XPos'], y=e['YPos'], h=e['Height'],
                               sd=sd, auto=bool(sd & 0x40), flag10=e['_10'], contents=e['Contents'],
                               A=e['EventCodesA_LoadIndex'], B=e['EventCodesB_MapIndex'], C=e['EventCodesC_TickIndex'],
                               D=e['EventCodesD_TouchIndex'], E=e['EventCodesE_DeactivateIndex'], F=e['EventCodesF_InteractIndex'],
                               b4=prop[0] if prop else None, b5=prop[1] if prop else None, b6=prop[2] if prop else None, b7=prop[3] if prop else None))
json.dump(placed, open(os.path.join(HERE, 'placed.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
print('maps', nmaps, 'enabled entities', total_enabled, 'placed pickups', len(placed))
bt = collections.Counter(p['type'] for p in placed)
print('types', len(bt))
print('flag10 != 0:', sum(1 for p in placed if p['flag10']), ' contents != 0:', sum(1 for p in placed if p['contents']))
print('auto-appear (bit 0x40):', sum(1 for p in placed if p['auto']), ' forced only:', sum(1 for p in placed if not p['auto']))
print('entity event codes non-zero:', collections.Counter((k, p[k]) for p in placed for k in 'ABCDEF' if p[k]).most_common(12))
print()
print('type item  b4(600) b5(sound) count  name')
for t in sorted(bt):
    p0 = next(p for p in placed if p['type'] == t)
    print(f"{t:4d} {p0['item']:#05x} {p0['b4']:5d} {p0['b5']:6d} {bt[t]:5d}  {p0['name']}")
key = [p for p in placed if p['b5'] == 0]
print()
print('KEY objects (sound octet 5 == 0):', len(key), 'types', sorted(set(p['type'] for p in key)))
for p in key:
    print('  map', p['map'], 'idx', p['idx'], 'type', p['type'], p['name'], 'auto', p['auto'], 'flag10', p['flag10'])
print()
print('b4==0 (no 600-tick rule) placed:', sum(1 for p in placed if p['b4'] == 0), ' b5!=0 placed:', sum(1 for p in placed if p['b5']))
print('sound distribution:', collections.Counter(p['b5'] for p in placed))
print('b6/b7 distribution:', collections.Counter((p['b6'], p['b7']) for p in placed))
print('maps holding placed pickups:', len(set(p['map'] for p in placed)))
