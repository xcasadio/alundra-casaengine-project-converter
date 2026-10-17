import pickle, collections, csv, json, sys
sys.stdout.reconfigure(encoding='utf-8')
d = pickle.load(open('cache/maps.pkl', 'rb'))
REPO = r"D:\development\repo\alundra-casaengine-project-converter"
names = {}
with open(REPO + r"\alundra-datas-analyser\AlundraTools\AlundraTools\EntityNames.csv", encoding='utf-8-sig') as fh:
    lines = fh.read().split("\n")
rows = []
for ln in lines[1:]:
    if not ln.strip(): continue
    cols = ln.rstrip("\r").split(";")
    rows.append(cols[1].strip() if len(cols) > 1 else "")
def tname(t): return rows[t] if t < len(rows) else '?'
g = d['global']['records']
# A. headers
hdr = collections.defaultdict(lambda: collections.Counter())   # type -> Counter(Contents)
hdr_where = collections.defaultdict(set)
for mid, m in d['maps'].items():
    for i, r in m['records'].items():
        hdr[r['Sector5Id']][r['Contents']] += 1
        hdr_where[r['Sector5Id']].add(mid)
for i, r in g.items():
    hdr[r['Sector5Id']][r['Contents']] += 1000   # global bank, marked by +1000
print('== A. sprite types whose header Contents != 0 (count; 1000 = global bank) ==')
for t in sorted(hdr):
    c = {k: v for k, v in hdr[t].items() if k != 0}
    if c:
        print(f'type {t:3d} {tname(t)!r}: header Contents', {hex(k): v for k, v in sorted(c.items())}, 'zero-in:', hdr[t].get(0, 0))
# B. placed entities
print()
eff = collections.Counter(); byt = collections.defaultdict(collections.Counter)
n_en = 0; hi = 0; flagged = 0; badflag = 0
placed = []
for mid, m in d['maps'].items():
    for e in m['entities']:
        if not e['IsEnabled']: continue
        n_en += 1
        t = e['SpriteTableIndex']
        rec = m['records'].get(t) or g.get(t)
        hc = rec['Contents'] if rec else None
        ec = e['Contents'] or 0
        if ec > 0xFF: hi += 1
        fl = e['_10'] or 0
        if fl: flagged += 1
        if fl and (fl & 0x7FFF) >= 0x800: badflag += 1
        code = (ec & 0xFF) or hc or 0
        placed.append((mid, e['idx'], t, ec & 0xFF, hc, fl, code))
        byt[t][code] += 1
print('placed enabled entities', n_en, 'entity Contents > 0xFF', hi, 'with flag', flagged, 'flag invalid (>=0x800)', badflag)
cnt = collections.Counter()
for mid, idx, t, ec, hc, fl, code in placed:
    kind = 'none' if code == 0 else ('fixed' if code < 0x62 else ('invalid62-7f' if code < 0x80 else 'random'))
    cnt[kind] += 1
print('effective code kinds:', dict(cnt))
print()
print('== B1. placed entities with effective code != 0, by type and code ==')
for t in sorted(byt):
    c = {k: v for k, v in byt[t].items() if k}
    if c: print(f'type {t:3d} {tname(t)!r}:', {hex(k): v for k, v in sorted(c.items())})
pickle.dump(placed, open('cache/placed.pkl', 'wb'))
