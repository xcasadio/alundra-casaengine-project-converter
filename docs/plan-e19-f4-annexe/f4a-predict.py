"""E19.f4a F4A-0: export prediction, computed from data-extracted/, portraits_table.tsv and the export of this moment
(alundra-project/, the export after E19.f3b: proven identical to HEAD by a baseline re-export, only report.json timings differ).
Independent of the converter code (own uuid5). Writes the prediction markdown to argv[1]."""
import hashlib
import json
import os
import sys
import uuid

REPO = r'D:/development/repo/alundra-casaengine-project-converter'
NS = uuid.UUID('2b7f6b6a-2d63-4e33-9f0e-0f7f2b6a5c11')


def ids_for(key):
    return str(uuid.uuid5(NS, key))


tsv = [l.rstrip('\n').split('\t') for l in open(REPO + '/docs/plan-e19-f4-annexe/portraits_table.tsv', encoding='utf-8')]
hdr, rows = tsv[0], tsv[1:]
assert len(rows) == 25
T = [dict(zip(hdr, r)) for r in rows]

# 1. ids recomputed independently
for t in T:
    exp = ids_for('sprite:map_%s_spritesheet.png:%s' % (t['canonical_map'], t['signature']))
    assert exp == t['expected_sprite_id'], (t['bank'], exp, t['expected_sprite_id'])

# 2. canonical record from data-extracted: first map (numeric ascending) holding the bank; field of that record
maps = sorted(int(f[4:-5]) for f in os.listdir(REPO + '/data-extracted/data') if f.startswith('map_') and f.endswith('.json') and f[4:-5].isdigit())
canon = {}
for m in maps:
    d = json.load(open(REPO + '/data-extracted/data/map_%d.json' % m, encoding='utf-8'))
    recs = (d.get('SpriteInfo') or {}).get('SpriteRecords') or []
    for r in recs:
        if not isinstance(r, dict):
            continue
        b = r['Header']['Sector5Id']
        if b in canon:
            continue
        canon[b] = (m, r['Header'].get('FlagsPortraitShadowType', 0), r.get('DialoguePortrait'))
for t in T:
    m, flags, dp = canon[int(t['bank'])]
    assert m == int(t['canonical_map']), t
    assert flags & 0x80 and dp is not None, t
    assert dp['Signature'] == int(t['signature']) and dp['Swidth'] == int(t['w']) and dp['Sheight'] == int(t['h']), t
withflag = sorted(b for b, (m, f, dp) in canon.items() if f & 0x80)
assert withflag == sorted(int(t['bank']) for t in T), withflag
assert all((dp is not None) == bool(f & 0x80) for (m, f, dp) in canon.values())

# 3. prefab ids: Ids.For("entity:<bankKey>/<folder>") with the folder of the exported .entity, matched in AssetInfos.json
ai = json.load(open(REPO + '/alundra-project/AssetInfos.json', encoding='utf-8'))['asset_infos']
by_id = {a['id']: a for a in ai}
ent_folders = {}
for a in ai:
    fn = a['file_name'].replace('\\', '/')
    if a['asset_type'] == 'entity' and fn.startswith('Entities/'):
        ent_folders[a['id']] = fn.split('/')[1]
sr = json.load(open(REPO + '/alundra-project/Data/sprite-records.json', encoding='utf-8'))
flagged = sorted(k for k, v in sr.items() if v['FlagsPortraitShadowType'] & 0x80)
assert len(flagged) == 25
prefab_of = {}
for t in T:
    cands = [pid for pid, folder in ent_folders.items() if pid == ids_for('entity:%s/%s' % (t['bank'], folder))]
    assert len(cands) == 1, (t['bank'], cands)
    prefab_of[int(t['bank'])] = cands[0]
assert sorted(prefab_of.values()) == flagged

# 4. added files
added = []
for t in T:
    if t['expected_sprite_id'] in by_id:
        assert t['bank'] == '15', t
        continue
    p = 'UI/Portraits/sprite_%s.sprite' % t['signature']
    assert not os.path.exists(REPO + '/alundra-project/' + p), p
    added.append(p)
assert len(added) == 24
bonaire = by_id['cb5544da-8df5-58e5-8a09-dec693ec1d53']

# 5. counters
rep = json.load(open(REPO + '/alundra-project/report.json', encoding='utf-8'))
C = rep['Counters']
chg = [('Sprites.DialoguePortrait', None, 25)]
for k in ('Assets.Sprite', 'Verify.LoadableFilesOnDisk', 'Verify.Assets', 'Verify.Loaded', 'Verify.Loaded.sprite'):
    chg.append((k, C[k], C[k] + 24))
M = rep['Metrics']['OutputFileCount']
chg.append(('Metrics.OutputFileCount', M, M + 24))

with open(sys.argv[1], 'w', encoding='utf-8', newline='\n') as o:
    w = o.write
    w('# E19.f4a - export prediction (F4A-0)\n\n')
    w('Written and committed BEFORE any converter or DLL code of E19.f4a. Computed by `docs/plan-e19-f4-annexe/f4a-predict.py` from `data-extracted/`,\n')
    w('`portraits_table.tsv` and the export of this moment (`alundra-project/` after E19.f3b; a baseline in-place re-export at `30a845c`\n')
    w('changed only `report.json` timings, so the tree is exactly the export of HEAD). The script is independent of the converter (own uuid5, own\n')
    w('reading of the extraction) and asserts: the 25 ids recomputed from `uuid5(2b7f6b6a-..., "sprite:map_<map>_spritesheet.png:<signature>")`\n')
    w('equal `portraits_table.tsv`; the canonical record of each of the 25 banks (first map in numeric order) carries `DialoguePortrait` with the\n')
    w('table signature and size; the banks whose canonical header has bit `0x80` are exactly the 25; each bank\'s prefab id is recovered as\n')
    w('`uuid5("entity:<bank>/<folder>")` against the exported `.entity` folders and the 25 prefab ids equal the 25 entries of\n')
    w('`Data/sprite-records.json` whose `FlagsPortraitShadowType` has bit `0x80`.\n\n')
    w('## Paths (manifest SHA-1 of `alundra-project/`, without `Alundra.dll`, `Alundra.pdb`, `.casaeditor/`)\n\n')
    w('**Added (24)**: one `.sprite` per portrait bank, bank 15 (Bonaire, `cb5544da-8df5-58e5-8a09-dec693ec1d53`, already exported as\n')
    w('`%s`) excluded:\n\n' % bonaire['file_name'].replace('\\', '/'))
    for p in added:
        w('- A %s\n' % p)
    w('\n**Modified (3)**: `Data/sprite-records.json`, `AssetInfos.json`, `report.json`.\n\n')
    w('**Removed**: none.\n\n')
    w('## `Data/sprite-records.json`: 25 entries gain a `DialoguePortrait` object, the 370 others are identical to the byte\n\n')
    w('| bank | prefab id | SpriteAssetId | Width | Height |\n|---|---|---|---|---|\n')
    for t in T:
        w('| %s | %s | %s | %s | %s |\n' % (t['bank'], prefab_of[int(t['bank'])], t['expected_sprite_id'], t['w'], t['h']))
    w('\n(Bank 15 reuses the id of its animation sprite; 48 x 72 for banks 122 and 162.)\n\n')
    w('## `AssetInfos.json`\n\n24 new `sprite` entries (the added files, name `sprite_<signature>`), nothing else.\n\n')
    w('## `report.json`\n\n| counter | now | predicted |\n|---|---|---|\n')
    for k, a, b in chg:
        w('| %s | %s | %s |\n' % (k, 'absent' if a is None else a, b))
    w('\n`Warnings` (%d), `WarningsByCategory`, `Errors` and every other counter unchanged; the `Metrics` durations and output size vary from run to run.\n' % len(rep['Warnings']))
    w('\n## Not changed\n\nNo texture (the portraits are cells of the map sheets exported since G0/G0b), no Yarn, no map, no entity prefab file, no UI screen.\n')

# the predicted change list for cmpman.py
with open(sys.argv[2], 'w', encoding='utf-8', newline='\n') as o:
    for p in added:
        o.write('A ' + p + '\n')
    for p in ('AssetInfos.json', 'Data/sprite-records.json', 'report.json'):
        o.write('M ' + p + '\n')
print('ok', len(added), len(flagged))
