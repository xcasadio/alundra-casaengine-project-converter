import pickle, collections, sys
sys.stdout.reconfigure(encoding='utf-8')
d = pickle.load(open('cache/maps.pkl', 'rb'))
REPO = r"D:\development\repo\alundra-casaengine-project-converter"
with open(REPO + r"\alundra-datas-analyser\AlundraTools\AlundraTools\EntityNames.csv", encoding='utf-8-sig') as fh:
    lines = fh.read().split("\n")
rows = []
for ln in lines[1:]:
    if not ln.strip(): continue
    cols = ln.rstrip("\r").split(";")
    rows.append(cols[1].strip() if len(cols) > 1 else "")
def tname(page, t):
    i = t + (256 if page == 'M' else 0)
    return rows[i] if i < len(rows) else '?'
g = d['global']['records']
# (page, type) -> header info
H = collections.defaultdict(lambda: collections.defaultdict(int))
for mid, m in d['maps'].items():
    for i, r in m['records'].items():
        H[('M', r['Sector5Id'])][(r['Contents'], r['ProgramLoad'], r['ProgramTick'], r['BreakEffect'])] += 1
for i, r in g.items():
    H[('G', r['Sector5Id'])][(r['Contents'], r['ProgramLoad'], r['ProgramTick'], r['BreakEffect'])] += 1
print('== A. (page,type) with header Contents != 0 : header combos (Contents, A, C, BreakEffect) x maps ==')
for k in sorted(H, key=lambda k: (k[0], k[1])):
    combos = {c: n for c, n in H[k].items() if c[0]}
    if combos:
        print(f'{k[0]}{k[1]:3d} {tname(*k)!r}:', {f'{c[0]:#04x}/A{c[1]}/C{c[2]}/B{c[3]}': n for c, n in sorted(combos.items())}, ' other:', sum(n for c, n in H[k].items() if not c[0]))
# B. placed
placed = []
byt = collections.defaultdict(collections.Counter)
for mid, m in d['maps'].items():
    for e in m['entities']:
        if not e['IsEnabled']: continue
        t = e['SpriteTableIndex']
        if t in m['records']: page, rec = 'M', m['records'][t]
        elif t in g: page, rec = 'G', g[t]
        else: page, rec = '?', None
        ec = (e['Contents'] or 0) & 0xFF
        hc = rec['Contents'] if rec else 0
        fl = e['_10'] or 0
        code = ec or hc
        placed.append(dict(map=mid, idx=e['idx'], page=page, t=t, ec=ec, hc=hc, fl=fl, code=code, A=rec['ProgramLoad'] if rec else None, C=rec['ProgramTick'] if rec else None))
        byt[(page, t)][(code, 'E' if ec else 'H')] += 1
pickle.dump(placed, open('cache/placed.pkl', 'wb'))
print()
print('== B. placed enabled entities: ', len(placed), ' unresolved types:', sum(1 for p in placed if p['page'] == '?'))
print('== B1. non-zero effective code, by (page,type): code(source E=entity H=header) x count ==')
for k in sorted(byt):
    c = {kk: v for kk, v in byt[k].items() if kk[0]}
    if c:
        if k == ('G', 30):
            tot = sum(c.values()); print(f'{k[0]}{k[1]:3d} {tname(*k)!r}: {tot} chests with entity Contents (fixed item ids), see B2'); continue
        print(f'{k[0]}{k[1]:3d} {tname(*k)!r}:', {f'{kk[0]:#04x}{kk[1]}': v for kk, v in sorted(c.items())})
# B2 chests
ch = collections.Counter(p['code'] for p in placed if (p['page'], p['t']) == ('G', 30))
print('B2 chests (G30): n=', sum(ch.values()), 'distinct items', len(ch), 'top', ch.most_common(12))
# C. kinds
kinds = collections.Counter()
for p in placed:
    c = p['code']
    kinds['none' if c == 0 else 'fixed' if c < 0x62 else 'invalid' if c < 0x80 else 'random'] += 1
print('kinds', dict(kinds))
# D. flags
fl = [p for p in placed if p['fl']]
print('placed with ContentsGameFlag:', len(fl), 'of which by type:', collections.Counter((p['page'], p['t']) for p in fl).most_common(8))
print('flag bit15 (temporary flags) set:', sum(1 for p in fl if p['fl'] & 0x8000))
