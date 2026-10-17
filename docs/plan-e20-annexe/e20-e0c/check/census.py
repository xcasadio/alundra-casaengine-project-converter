import json, glob, re, os, collections, pickle, sys
DATA = r"D:\development\repo\alundra-casaengine-project-converter\data-extracted\data"
def load(p):
    with open(p, encoding='utf-8') as f: d = json.load(f)
    si = d['SpriteInfo']
    ents = [(i, e) for i, e in enumerate((si.get('Entities') or {}).get('Entities') or []) if e is not None]
    recs = {i: r['Header'] for i, r in enumerate(si.get('SpriteRecords') or []) if r}
    return ents, recs
files = sorted(glob.glob(os.path.join(DATA, 'map_[0-9]*.json')), key=lambda p: int(re.search(r'map_(\d+)\.json', p).group(1)))
print('maps', len(files))
gents, grecs = load(os.path.join(DATA, 'map_alundra.json'))
print('global recs', len(grecs))
placed = []
for p in files:
    mid = int(re.search(r'map_(\d+)\.json', p).group(1))
    ents, recs = load(p)
    for i, e in ents:
        if not e['IsEnabled']: continue
        page = 'M' if (e['SpriteDirection'] & 0x80) else 'G'
        t = e['SpriteTableIndex']
        rec = (recs if page == 'M' else grecs).get(t)
        ec = e['Contents'] & 0xFF if e['Contents'] is not None else 0
        hc = rec['Contents'] if rec else None
        placed.append(dict(map=mid, idx=i, page=page, t=t, ec=ec, hc=hc, big=e['Contents']>0xFF, fl=e['_10'], dirb=e['SpriteDirection'], hasrec=rec is not None))
pickle.dump(placed, open('placed2.pkl','wb'))
print('placed enabled', len(placed))
print('missing record', sum(1 for p in placed if not p['hasrec']))
print('Contents>0xFF', sum(1 for p in placed if p['big']))
print('flag invalid', sum(1 for p in placed if (p['fl'] & 0x7FFF) >= 0x800))
kinds = collections.Counter()
codes = collections.Counter()
for p in placed:
    # emulate InitializeContents (ignoring flag state at runtime = bit low)
    code = p['ec'] or (p['hc'] or 0)
    p['code'] = code
    k = 'none' if code == 0 else 'fixed' if code < 0x62 else 'bad' if code < 0x80 else 'table'
    kinds[k] += 1
    if code >= 0x80: codes[code] += 1
print(kinds); print(sorted((hex(k), v) for k, v in codes.items()))
bysprite = collections.defaultdict(collections.Counter)
for p in placed:
    if p['code'] >= 0x80: bysprite[hex(p['code'])][(p['page'], p['t'])] += 1
for c in sorted(bysprite): print(c, sorted(bysprite[c].items(), key=lambda kv: (kv[0][0], kv[0][1])))
fl = [p for p in placed if p['fl']]
print('with flag', len(fl), collections.Counter((p['page'], p['t']) for p in fl))
print('bit15 flags', sum(1 for p in fl if p['fl'] & 0x8000))
chests = [p for p in placed if p['page']=='G' and p['t']==30]
print('chests G30', len(chests), 'fixed', sum(1 for p in chests if 0 < p['code'] < 0x62))
fixed_other = collections.Counter((p['page'],p['t']) for p in placed if 0 < p['code'] < 0x62 and not (p['page']=='G' and p['t']==30))
print('fixed non-chest', fixed_other)
