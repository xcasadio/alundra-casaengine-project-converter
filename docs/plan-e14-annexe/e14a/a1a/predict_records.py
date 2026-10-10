"""predict_records.py - READ-ONLY prediction of Data/sprite-records.json after the BreakEffect export.

Matches each entry of the current alundra-project/Data/sprite-records.json to its canonical bank (first record of
the bank, converter order: map_alundra.json then map_<N>.json ascending) by the full header tuple + AnimSets, then
inserts one line `    "BreakEffect": N,` after the `"SizeZ"` line of every entry (CRLF, 4-space indent, like
System.Text.Json WriteIndented in SpriteWriter.SpriteRecordsSerializerOptions).
usage: python -I predict_records.py <repo> <out-predicted-json> <out-report>
"""
import hashlib
import json
import os
import re
import sys
from collections import defaultdict

repo = sys.argv[1]
out_pred = sys.argv[2]
out_rep = sys.argv[3]
data_dir = os.path.join(repo, 'data-extracted', 'data')
cur_path = os.path.join(repo, 'alundra-project', 'Data', 'sprite-records.json')

HDR = ['MoreFlags', 'CanPickup', 'FlagsPortraitShadowType', 'ProgramLoad', 'ProgramTick', 'ProgramTouch',
       'ProgramDeactivate', 'ProgramInteract', 'OffsetX', 'OffsetY', 'OffsetZ', 'SizeX', 'SizeY', 'SizeZ', 'Contents']


def animset_tuple(a):
    flags = a.get('Flags', 0)
    unk = a.get('Unknown', 0)
    isz = a['IsZForceApplied'] if 'IsZForceApplied' in a else ((((flags << 8) | unk) + 0x8000) & 0xFFFF) - 0x8000
    return (a.get('Speed', 0), a.get('Acceleration', 0), isz, a.get('Sfx', 0), flags, unk)


files = []
if os.path.exists(os.path.join(data_dir, 'map_alundra.json')):
    files.append((-1, os.path.join(data_dir, 'map_alundra.json')))
nums = sorted(int(m.group(1)) for m in (re.fullmatch(r'map_(\d+)\.json', n) for n in os.listdir(data_dir)) if m)
files += [(n, os.path.join(data_dir, 'map_%d.json' % n)) for n in nums]

canon = {}
for mi, path in files:
    d = json.load(open(path, 'r', encoding='utf-8'))
    for rec in (d.get('SpriteInfo', {}).get('SpriteRecords') or []):
        if not isinstance(rec, dict):
            continue
        h = rec['Header']
        key = ('alundra_%d' if mi == -1 else '%d') % h['Sector5Id']
        if key in canon:
            continue
        sets = tuple(animset_tuple(a) if isinstance(a, dict) else (0, 0, 0, 0, 0, 0) for a in rec.get('AnimSets', []))
        canon[key] = (tuple(h.get(f, 0) for f in HDR), sets, h.get('BreakEffect', 0))
    del d

raw = open(cur_path, 'rb').read()
cur = json.loads(raw.decode('utf-8'))

by_key = defaultdict(list)
for bank, (hdr, sets, be) in canon.items():
    by_key[(hdr, sets)].append((bank, be))

rep = []
rep.append('canonical banks: %d ; current entries: %d' % (len(canon), len(cur)))
entry_be = {}
ambiguous = []
unmatched = []
for guid, e in cur.items():
    hdr = tuple(e[f] for f in HDR)
    sets = tuple((a['Speed'], a['Acceleration'], a['IsZForceApplied'], a['Sfx'], a['Flags'], a['Unknown']) for a in e['AnimSets'])
    cands = by_key.get((hdr, sets), [])
    if not cands:
        unmatched.append(guid)
    elif len(set(be for _, be in cands)) > 1:
        ambiguous.append((guid, cands))
    else:
        entry_be[guid] = cands[0][1]
rep.append('unmatched entries: %d ; ambiguous (candidate banks disagree on BreakEffect): %d ; resolved: %d'
           % (len(unmatched), len(ambiguous), len(entry_be)))
shared = [k for k, v in by_key.items() if len(v) > 1]
rep.append('header+AnimSets keys shared by several banks: %d' % len(shared))
for k in shared[:5]:
    rep.append('  shared by %s' % [b for b, _ in by_key[k]])

# Byte-level insertion. Entries are separated by their guid key lines: '  "<guid>": {' at 2-space indent.
text = raw.decode('utf-8')
assert '\r\n' in text, 'expected CRLF'
lines = text.split('\r\n')
out = []
current = None
inserted = 0
for ln in lines:
    m = re.fullmatch(r'  "([0-9a-f\-]{36})": \{', ln)
    if m:
        current = m.group(1)
    out.append(ln)
    if current is not None and re.fullmatch(r'    "SizeZ": -?\d+,', ln):
        be = entry_be.get(current)
        if be is None:
            rep.append('NO BreakEffect for %s' % current)
        else:
            out.append('    "BreakEffect": %d,' % be)
            inserted += 1
pred = '\r\n'.join(out)
open(out_pred, 'wb').write(pred.encode('utf-8'))
rep.append('inserted lines: %d ; old size %d bytes ; predicted size %d bytes (+%d)' % (inserted, len(raw), len(pred.encode('utf-8')), len(pred.encode('utf-8')) - len(raw)))
rep.append('old sha1 %s' % hashlib.sha1(raw).hexdigest())
rep.append('pred sha1 %s' % hashlib.sha1(pred.encode('utf-8')).hexdigest())
# sanity: the predicted JSON parses, differs only by BreakEffect, and the removal gives back the old object
p = json.loads(pred)
ok = all(({k: v for k, v in p[g].items() if k != 'BreakEffect'} == cur[g]) for g in cur)
rep.append('predicted parses; equals old minus BreakEffect for every entry: %s' % ok)
rep.append('key order of the first predicted entry: %s' % list(next(iter(p.values())).keys()))
muruta = 'e3a39ec4-45d5-55f2-85e9-894c7b6e4a08'
rep.append('Muruta entry BreakEffect %s Contents %s' % (p[muruta].get('BreakEffect'), p[muruta]['Contents']))
from collections import Counter
rep.append('histogram over entries: %s' % sorted(Counter(v.get('BreakEffect') for v in p.values()).items()))
open(out_rep, 'w', encoding='utf-8').write('\n'.join(rep) + '\n')
print('\n'.join(rep))
