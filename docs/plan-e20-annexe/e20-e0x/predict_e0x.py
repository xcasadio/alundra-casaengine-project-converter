"""Independent prediction of sprite-records.json after adding BankKey (critic, own code, nothing imported from the design folder).

Three independent routes to the key of each current entry:
  R1 uuid5 brute force: for each (bank key from the extracted data) x (entity folder name on disk) recompute Ids.For("entity:KEY/FOLDER").
  R2 the exported .entity file: its "id", and its anim2d file names "bank<KEY>_anim..." (secondary).
  R3 content: first-record header tuple + AnimSets of the canonical bank.
usage: python -B -I predict_e0x.py <repo> <outdir> [<sprite-records.json before the change>]
The third argument is the Data/sprite-records.json of the state BEFORE E20.e0x (default: the one in alundra-project/); it must have
SHA-1 007f2660eb83d5a3d21aa35d7c71dd34dba0afcf. Writes into <outdir>: sprite-records.predicted.json and map-sprite-indices.predicted.json
(the exact text fixed by section 1.15 of docs/plan-e20-heros.md), plus the helper maps.
"""
import hashlib, json, os, re, sys, uuid, unicodedata
from collections import Counter, defaultdict

repo, outdir = sys.argv[1], sys.argv[2]
os.makedirs(outdir, exist_ok=True)
NS = uuid.UUID('2b7f6b6a-2d63-4e33-9f0e-0f7f2b6a5c11')
data_dir = os.path.join(repo, 'data-extracted', 'data')
proj = os.path.join(repo, 'alundra-project')
cur_path = sys.argv[3] if len(sys.argv) > 3 else os.path.join(proj, 'Data', 'sprite-records.json')

# ---- banks (converter rule re-derived from SpriteBankReader.cs: global first, then map_N ascending; first wins)
def bank_records(path):
    d = json.load(open(path, encoding='utf-8'))
    recs = d.get('SpriteInfo', {}).get('SpriteRecords')
    out = []
    if isinstance(recs, list):
        for r in recs:
            if isinstance(r, dict):
                out.append(r)
    return out

banks = {}   # key -> dict(source, sector, header, sets)
order_read = []
def read(path, mi, is_alundra):
    for r in bank_records(path):
        h = r['Header']
        sid = h['Sector5Id']
        key = ('alundra_%d' % sid) if is_alundra else ('%d' % sid)
        if key in banks:
            continue
        if not isinstance(r.get('AnimSets'), list):
            continue
        banks[key] = dict(src=mi, sid=sid, alundra=is_alundra, header=h, sets=r['AnimSets'])
        order_read.append(key)

read(os.path.join(data_dir, 'map_alundra.json'), -1, True)
nums = []
for n in os.listdir(data_dir):
    m = re.fullmatch(r'map_(\d+)\.json', n)
    if m:
        nums.append(int(m.group(1)))
nums.sort()
for n in nums:
    read(os.path.join(data_dir, 'map_%d.json' % n), n, False)

ordered = sorted(banks.values(), key=lambda b: (b['alundra'], b['sid']))   # OrderBy(IsAlundraBank).ThenBy(Sector5Id)
keys_ordered = [('alundra_%d' if b['alundra'] else '%d') % b['sid'] for b in ordered]
print('banks', len(banks), 'global', sum(1 for b in ordered if b['alundra']), 'map', sum(1 for b in ordered if not b['alundra']))
assert len(set(keys_ordered)) == len(keys_ordered)

# ---- current file
raw = open(cur_path, 'rb').read()
assert hashlib.sha1(raw).hexdigest() == '007f2660eb83d5a3d21aa35d7c71dd34dba0afcf', 'input is not the pre-E20.e0x file'
cur = json.loads(raw.decode('utf-8'))
print('current entries', len(cur), 'bytes', len(raw), 'sha1', hashlib.sha1(raw).hexdigest())

# ---- R1
folders = []
ent_dir = os.path.join(proj, 'Entities')
for n in os.listdir(ent_dir):
    if os.path.isdir(os.path.join(ent_dir, n)):
        folders.append(n)
print('folders', len(folders))
cand = defaultdict(list)
guid_set = set(cur.keys())
tried = 0
for key in banks:
    for f in folders:
        for form in {f, unicodedata.normalize('NFC', f), unicodedata.normalize('NFD', f)}:
            g = str(uuid.uuid5(NS, 'entity:%s/%s' % (key, form)))
            tried += 1
            if g in guid_set:
                cand[g].append((key, f, form == f))
amb = {g: v for g, v in cand.items() if len(set((k, f) for k, f, _ in v)) != 1}
print('R1 resolved', len(cand), 'ambiguous', len(amb), 'unresolved', len(guid_set - set(cand)), 'tried', tried)
r1 = {g: v[0][0] for g, v in cand.items()}
# no two guids share a key
print('R1 key collisions:', [k for k, c in Counter(r1.values()).items() if c > 1])
# every bank used exactly once
print('R1 banks unused:', sorted(set(banks) - set(r1.values())))

# ---- R2 the .entity ids and anim2d names
r2 = {}
r2_bad = []
for f in folders:
    p = os.path.join(ent_dir, f, f + '.entity')
    ent = json.load(open(p, encoding='utf-8'))
    gid = ent['id']
    names = [n for n in os.listdir(os.path.join(ent_dir, f)) if n.endswith('.anim2d')]
    ks = set()
    for n in names:
        m = re.match(r'bank(alundra_\d+|\d+)_anim\d+_', n)
        if m:
            ks.add(m.group(1))
        else:
            r2_bad.append((f, n))
    r2[gid] = (f, ks)
print('R2 entity files', len(r2), 'anim2d names unparsable', len(r2_bad), 'folders with no anim2d', sum(1 for g, (f, ks) in r2.items() if not ks), 'with >1 key', sum(1 for g, (f, ks) in r2.items() if len(ks) > 1))
mism = [(g, r1.get(g), ks) for g, (f, ks) in r2.items() if ks and g in r1 and ks != {r1[g]}]
print('R2 vs R1 mismatches', len(mism), mism[:5])
print('entity ids == current keys:', set(r2) == set(cur))

# ---- R3 content
HDR = ['MoreFlags', 'CanPickup', 'FlagsPortraitShadowType', 'ProgramLoad', 'ProgramTick', 'ProgramTouch',
       'ProgramDeactivate', 'ProgramInteract', 'OffsetX', 'OffsetY', 'OffsetZ', 'SizeX', 'SizeY', 'SizeZ', 'BreakEffect', 'Contents']
bad = 0
for g, e in cur.items():
    b = banks[r1[g]]
    for f in HDR:
        if e[f] != b['header'].get(f, 0):
            bad += 1
            print('header mismatch', g, r1[g], f, e[f], b['header'].get(f, 0))
    if len(e['AnimSets']) != len(b['sets']):
        bad += 1
        print('animset count mismatch', g, r1[g])
print('R3 header mismatches', bad)

# ---- entry order == converter bank order
file_order = list(cur.keys())
exp_order = []
inv = {v: k for k, v in r1.items()}
for k in keys_ordered:
    exp_order.append(inv[k])
print('file order == converter bank order:', file_order == exp_order)

# ---- build predicted text: BankKey as the FIRST member
text = raw.decode('utf-8')
assert '\r\n' in text and not text.startswith('﻿')
lines = text.split('\r\n')
out = []
n_ins = 0
for ln in lines:
    out.append(ln)
    m = re.fullmatch(r'  "([0-9a-f\-]{36})": \{', ln)
    if m:
        out.append('    "BankKey": %s,' % json.dumps(r1[m.group(1)]))
        n_ins += 1
pred = '\r\n'.join(out).encode('utf-8')
open(os.path.join(outdir, 'sprite-records.predicted.json'), 'wb').write(pred)
print('FIRST: inserted', n_ins, 'size', len(pred), '(+%d)' % (len(pred) - len(raw)), 'sha1', hashlib.sha1(pred).hexdigest())
p = json.loads(pred.decode('utf-8'))
assert all(list(p[g].keys())[0] == 'BankKey' for g in p)
assert all({k: v for k, v in p[g].items() if k != 'BankKey'} == cur[g] for g in cur)
glob = [v['BankKey'] for v in p.values() if v['BankKey'].startswith('alundra_')]
mp = [v['BankKey'] for v in p.values() if not v['BankKey'].startswith('alundra_')]
print('global', len(glob), 'map', len(mp), 'distinct', len(set(glob + mp)))
print('global bytes', sum(len(('    "BankKey": "%s",\r\n' % k).encode()) for k in glob), 'map bytes', sum(len(('    "BankKey": "%s",\r\n' % k).encode()) for k in mp))

# ---- variant: after BreakEffect
out2 = []
cur_g = None
n2 = 0
for ln in lines:
    out2.append(ln)
    m = re.fullmatch(r'  "([0-9a-f\-]{36})": \{', ln)
    if m:
        cur_g = m.group(1)
    if cur_g and re.fullmatch(r'    "BreakEffect": -?\d+,', ln):
        out2.append('    "BankKey": %s,' % json.dumps(r1[cur_g]))
        n2 += 1
pred2 = '\r\n'.join(out2).encode('utf-8')
print('AFTER-BREAK: inserted', n2, 'size', len(pred2), 'sha1', hashlib.sha1(pred2).hexdigest())

json.dump({g: r1[g] for g in cur}, open(os.path.join(outdir, 'guid_to_key.json'), 'w'), indent=1)
json.dump({k: {'guid': inv[k], 'folder': next(f for kk, f, _ in cand[inv[k]])} for k in keys_ordered}, open(os.path.join(outdir, 'key_to_prefab.json'), 'w', encoding='utf-8'), indent=1, ensure_ascii=False)


# ======================= Data/map-sprite-indices.json (D-E20-33) =======================
# Rule of record: index i (0..255) of the map's SpriteInfo.SpriteRecords whose record is not null.
# Cross-check: the same set as the indices i (0..255) of SpriteInfo.SpriteTable whose entry is not -1.
mapdata = {}
mismatch = 0
for n in nums:
    d = json.load(open(os.path.join(data_dir, 'map_%d.json' % n), encoding='utf-8'))
    si = d.get('SpriteInfo') or {}
    recs = si.get('SpriteRecords')
    by_records = []
    if isinstance(recs, list):
        by_records = [i for i, r in enumerate(recs) if i <= 255 and isinstance(r, dict)]
    tab = si.get('SpriteTable')
    by_table = [i for i, t in enumerate(tab) if i <= 255 and t != -1] if isinstance(tab, list) else []
    if by_records != by_table:
        mismatch += 1
        print('RULE MISMATCH map', n, by_records, by_table)
    mapdata[n] = by_records
print('maps', len(mapdata), 'records-vs-table mismatches', mismatch)
assert mismatch == 0 and nums == list(range(483))
CR = chr(13) + chr(10)
lines = ['{']
for k, n in enumerate(nums):
    comma = ',' if k < len(nums) - 1 else ''
    v = mapdata[n]
    if not v:
        lines.append('  "%d": []%s' % (n, comma))
    else:
        lines.append('  "%d": [' % n)
        for j, i in enumerate(v):
            lines.append('    %d%s' % (i, ',' if j < len(v) - 1 else ''))
        lines.append('  ]' + comma)
lines.append('}')
text2 = CR.join(lines).encode('utf-8')           # no BOM, no final line break
open(os.path.join(outdir, 'map-sprite-indices.predicted.json'), 'wb').write(text2)
chk = json.loads(text2.decode('utf-8'))
assert list(chk.keys()) == [str(n) for n in nums] and all(chk[str(n)] == mapdata[n] for n in nums)
total = sum(len(v) for v in mapdata.values())
empty = sum(1 for v in mapdata.values() if not v)
print('MAP-INDICES: keys', len(chk), 'indices', total, 'empty arrays', empty, 'size', len(text2), 'sha1', hashlib.sha1(text2).hexdigest())
print('witness 0', chk['0'], '| 388', chk['388'], '| 451', chk['451'], '| 432', chk['432'], '| 213 in 388/432/451:', [213 in chk[k] for k in ('388', '432', '451')])
print('95 has 233..236:', all(x in chk['95'] for x in (233, 234, 235, 236)), '| 322:', all(x in chk['322'] for x in (233, 234, 235, 236)))
print('distinct indices over all maps', len(set(i for v in mapdata.values() for i in v)), '| max per map', max(len(v) for v in mapdata.values()))
# witnesses of sprite-records.json
for k in ('91', '79', 'alundra_7', 'alundra_2', 'alundra_0', '255'):
    print('witness', k, '->', next(g for g, v in r1.items() if v == k))
