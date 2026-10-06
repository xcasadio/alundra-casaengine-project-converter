"""E19.g G1-0: independent prediction of the effects export, and its checker (no converter code involved).

Everything is derived from data-extracted/ (the JSON of each map and the effect sheets), from the export of the
moment (alundra-project/, only to find each map folder and the catalog text) and from the plan section "E19.g G1".

  python g1-predict.py                  writes g1-export-prediction.md next to this file
                                        (run it on the reference export, BEFORE any converter code is written)
  python g1-predict.py check BEFORE.txt AFTER.txt REPORT_BEFORE.json
                                        checks an export against the committed g1-export-prediction.md and against a
                                        fresh derivation from data-extracted (SHA-1 manifests as written by
                                        manifest.py, the report.json of the reference export)

Canonical content of a companion: json.dumps(doc, sort_keys=True, separators=(",", ":")) of the parsed values
(the key order and the layout of the written file are the writer's business; values only).
"""
import glob
import hashlib
import json
import os
import re
import struct
import sys
import uuid

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, '..', '..'))
DATA = os.path.join(REPO, 'data-extracted', 'data')
PROJECT = os.path.join(REPO, 'alundra-project')
PREDICTION = os.path.join(HERE, 'g1-export-prediction.md')
SHEETS_TSV = os.path.join(REPO, 'docs', 'plan-e19-g0-annexe', 'expected_effect_sheets.tsv')
NS = uuid.UUID('2b7f6b6a-2d63-4e33-9f0e-0f7f2b6a5c11')  # Ids.ProjectNamespace

COUNTERS = [
    'Effects.Records', 'Effects.RecordsSpawnAtLoad', 'Effects.RecordsMapTable', 'Effects.RecordsGlobalTable',
    'Effects.Tables', 'Effects.Animations', 'Effects.AnimationSlotsDropped', 'Effects.Frames', 'Effects.ImageSets',
    'Effects.Images', 'Effects.ImagesDegenerateDropped', 'Effects.Companions', 'Effects.Sheets',
    'Effects.UnresolvedRecords',
]


def sha1(data):
    return hashlib.sha1(data).hexdigest()


def canon(doc):
    return json.dumps(doc, sort_keys=True, separators=(',', ':'))


def ids_for(key):
    return str(uuid.uuid5(NS, key))


def map_folders():
    """map id -> 'Maps/Zone/Name-id' (forward slashes), found from the .world files of the export."""
    out = {}
    for world in glob.glob(os.path.join(PROJECT, 'Maps', '*', '*', '*.world')):
        m = re.search(r'-(\d+)\.world$', world)
        if m:
            rel = os.path.relpath(os.path.dirname(world), PROJECT).replace(os.sep, '/')
            out[int(m.group(1))] = rel
    return out


def convert_effects(effs, label, stats):
    """The Effects array of one bank (list of tables), following G1-R2 / G1-R3."""
    tables = []
    for ei, e in enumerate(effs):
        stats['tables'] += 1
        count = e['AnimationCount']
        offs = e['AnimationOffsets'][:count]
        nz = [i for i, o in enumerate(offs) if o != 0]
        assert not nz or len(nz) == nz[-1] + 1, '%s table %d: hole' % (label, ei)
        real = len(nz)
        stats['anim_slots_dropped'] += count - real
        anims, sets, set_index = [], [], {}
        for ai in range(real):
            a = e['PreloadedAnims'][ai]
            frames = a['Frames']
            end = frames[-1]
            assert not (end['Delay'] & 0x80) and end['Delay'] in (0, 1), '%s end delay' % label
            out_frames = []
            for fr in frames[:-1]:
                raw = fr['Delay']
                assert raw & 0x80 and 1 <= (raw & 0x7F) <= 127, '%s frame delay %d' % (label, raw)
                ptr = fr['ImageSetPointer']
                if ptr not in set_index:
                    im = fr['Images']
                    images = []
                    for q in im['Images']:
                        degenerate = (q['Swidth'] == 0 and q['Sheight'] == 0
                                      and all(q[k] == 0 for k in ('X1', 'Y1', 'X2', 'Y2', 'X3', 'Y3', 'X4', 'Y4')))
                        assert degenerate == (q['Swidth'] == 0 or q['Sheight'] == 0)
                        if degenerate:
                            stats['images_degenerate'] += 1
                            continue
                        stats['images'] += 1
                        images.append({
                            'U': q['AtlasX'], 'V': q['AtlasY'], 'W': q['Swidth'], 'H': q['Sheight'],
                            'C': [q['X1'], q['Y1'], q['X2'], q['Y2'], q['X3'], q['Y3'], q['X4'], q['Y4']],
                            'Semi': bool(q['Spritesheet'] & 8), 'Abr': (q['Spritesheet'] >> 4) & 3,
                        })
                    set_index[ptr] = len(sets)
                    sets.append({'Idsv': im['DepthSortValue'], 'Images': images})
                    stats['image_sets'] += 1
                out_frames.append([raw & 0x7F, set_index[ptr]])
                stats['frames'] += 1
            anims.append({'Frames': out_frames, 'End': 'Destroy' if end['Delay'] == 0 else 'Loop'})
            stats['animations'] += 1
        tables.append({'Animations': anims, 'ImageSets': sets})
    return tables


def derive():
    folders = map_folders()
    stats = {k: 0 for k in ('records', 'spawn', 'rec_map', 'rec_global', 'tables', 'animations', 'anim_slots_dropped',
                            'frames', 'image_sets', 'images', 'images_degenerate', 'companions', 'sheets',
                            'unresolved')}
    glob_doc = json.load(open(os.path.join(DATA, 'map_alundra.json'), encoding='utf-8'))
    glob_effs = glob_doc['SpriteInfo']['SpriteEffectRecords'] or []
    assert not (glob_doc['SpriteInfo']['MapEffectRecords'] or [])
    global_tables = convert_effects(glob_effs, 'global', stats)

    maps = []
    for p in glob.glob(os.path.join(DATA, 'map_*.json')):
        m = re.fullmatch(r'map_(\d+)\.json', os.path.basename(p))
        if m:
            maps.append(int(m.group(1)))
    maps.sort()

    companions = {}   # id -> (relative path, doc, sheet source or None)
    for n in maps:
        d = json.load(open(os.path.join(DATA, 'map_%d.json' % n), encoding='utf-8'))
        si = d['SpriteInfo']
        recs = si['MapEffectRecords'] or []
        effs = si['SpriteEffectRecords'] or []
        if not recs and not effs:
            continue
        folder = folders[n]
        name = folder.rsplit('/', 1)[1]
        tables = convert_effects(effs, 'map %d' % n, stats)
        sheet_id = None
        sheet = None
        if effs:
            sheet = os.path.join(DATA, 'map_%d_effectsheet.png' % n)
            assert os.path.exists(sheet)
            sheet_id = ids_for('texture-wrapper:' + (folder + '/effects/map_%d_effectsheet.texture' % n).replace('/', '\\'))
            stats['sheets'] += 1
        else:
            assert not os.path.exists(os.path.join(DATA, 'map_%d_effectsheet.png' % n))
        out_recs = []
        for r in recs:
            stats['records'] += 1
            stats['spawn'] += 1 if r['Flags'] & 0x40 else 0
            use_map = bool(r['Flags'] & 0x80)
            stats['rec_map' if use_map else 'rec_global'] += 1
            bank = tables if use_map else global_tables
            ok = r['EffectId'] < len(bank) and r['AnimId'] < len(bank[r['EffectId']]['Animations'])
            if not ok:
                stats['unresolved'] += 1
            out_recs.append({'X1': r['X1'], 'X2': r['X2'], 'Y1': r['Y1'], 'Y2': r['Y2'], 'Flags': r['Flags'],
                             'Effect': r['EffectId'], 'X': r['X'], 'Y': r['Y'], 'Z': r['Z'], 'Anim': r['AnimId']})
        doc = {'MapIndex': n, 'SheetTextureAssetId': sheet_id, 'Records': out_recs, 'Effects': tables}
        stats['companions'] += 1
        companions[n] = (folder + '/effects/' + name + '.effects.json', doc, sheet)

    # global sheet
    gsheet = os.path.join(DATA, 'map_alundra_effectsheet.png')
    assert os.path.exists(gsheet)
    stats['sheets'] += 1
    gid = ids_for('texture-wrapper:Data\\effects\\map_alundra_effectsheet.texture')
    gdoc = {'SheetTextureAssetId': gid, 'Effects': global_tables}
    return companions, ('Data/effects/effects-global.json', gdoc, gsheet), stats


def uuid_selfcheck():
    """The uuid5 of the catalog ids is checked against an existing catalog entry before it is trusted."""
    cat = json.load(open(os.path.join(PROJECT, 'AssetInfos.json'), encoding='utf-8'))['asset_infos']
    n = 0
    for a in cat:
        if a['asset_type'] in ('png', 'texture'):
            key = ('texture-raw:' if a['asset_type'] == 'png' else 'texture-wrapper:') + a['file_name']
            if ids_for(key) == a['id']:
                n += 1
            if n >= 20:
                break
    return n


def planned_files(companions, glob_entry):
    """(added paths with their predicted sha1, catalog entries in order)."""
    added = {}
    entries = []
    template = open(os.path.join(PROJECT, 'Maps', 'Test Map', 'Test Map-0', 'backdrop', 'Test Map-0-layer0.texture'),
                    'rb').read().decode('utf-8')
    t_id = re.search(r'"id": "([^"]+)"', template).group(1)
    t_name = 'Test Map-0-layer0'
    t_tex = re.search(r'"texture_asset_id": "([^"]+)"', template).group(1)

    def add_sheet(png_rel, src):
        stem = os.path.splitext(os.path.basename(png_rel))[0]
        win_png = png_rel.replace('/', '\\')
        win_tex = win_png[:-4] + '.texture'
        raw_id = ids_for('texture-raw:' + win_png)
        wr_id = ids_for('texture-wrapper:' + win_tex)
        added[png_rel] = sha1(open(src, 'rb').read())
        tex = template.replace(t_id, wr_id).replace(t_tex, raw_id).replace('"name": "%s"' % t_name, '"name": "%s"' % stem)
        added[png_rel[:-4] + '.texture'] = sha1(tex.encode('utf-8'))
        entries.append({'id': raw_id, 'name': stem, 'file_name': win_png, 'asset_type': 'png'})
        entries.append({'id': wr_id, 'name': stem, 'file_name': win_tex, 'asset_type': 'texture'})

    for n in sorted(companions):
        rel, doc, sheet = companions[n]
        added[rel] = None  # filled by the canonical hash
        if sheet:
            folder = rel.rsplit('/', 1)[0]
            add_sheet(folder + '/map_%d_effectsheet.png' % n, sheet)
    grel, gdoc, gsheet = glob_entry
    added[grel] = None
    add_sheet('Data/effects/map_alundra_effectsheet.png', gsheet)
    return added, entries


def catalog_sha(entries):
    old = open(os.path.join(PROJECT, 'AssetInfos.json'), 'rb').read().decode('utf-8')
    tail = '\r\n  ]\r\n}'
    assert old.endswith(tail)
    parts = []
    for e in entries:
        parts.append('    {\r\n      "id": %s,\r\n      "name": %s,\r\n      "file_name": %s,\r\n      "asset_type": %s\r\n    }'
                     % tuple(json.dumps(e[k], ensure_ascii=False) for k in ('id', 'name', 'file_name', 'asset_type')))
    new = old[:-len(tail)] + ',\r\n' + ',\r\n'.join(parts) + tail
    return sha1(new.encode('utf-8'))


def write_prediction():
    companions, glob_entry, stats = derive()
    selfcheck = uuid_selfcheck()
    assert selfcheck >= 20, 'uuid5 self-check failed (%d)' % selfcheck
    added, entries = planned_files(companions, glob_entry)
    hashes = {}
    for n in sorted(companions):
        rel, doc, _ = companions[n]
        hashes[rel] = sha1(canon(doc).encode('utf-8'))
    hashes[glob_entry[0]] = sha1(canon(glob_entry[1]).encode('utf-8'))
    for k, v in hashes.items():
        added[k] = v
    assert len(entries) == 174 and len(added) == 157 + 1 + 87 + 87

    base = json.load(open(os.path.join(PROJECT, 'report.json'), encoding='utf-8'))
    bc = base['Counters']
    new_c = {
        'Effects.Records': stats['records'], 'Effects.RecordsSpawnAtLoad': stats['spawn'],
        'Effects.RecordsMapTable': stats['rec_map'], 'Effects.RecordsGlobalTable': stats['rec_global'],
        'Effects.Tables': stats['tables'], 'Effects.Animations': stats['animations'],
        'Effects.AnimationSlotsDropped': stats['anim_slots_dropped'], 'Effects.Frames': stats['frames'],
        'Effects.ImageSets': stats['image_sets'], 'Effects.Images': stats['images'],
        'Effects.ImagesDegenerateDropped': stats['images_degenerate'], 'Effects.Companions': stats['companions'],
        'Effects.Sheets': stats['sheets'], 'Effects.UnresolvedRecords': stats['unresolved'],
        'Verify.Assets': bc['Verify.Assets'] + len(entries),
        'Verify.LoadableFilesOnDisk': bc['Verify.LoadableFilesOnDisk'] + stats['sheets'],
        'Verify.Loaded': bc['Verify.Loaded'] + stats['sheets'],
        'Verify.Loaded.texture': bc['Verify.Loaded.texture'] + stats['sheets'],
        'Verify.ExistenceChecked': bc['Verify.ExistenceChecked'] + stats['sheets'],
        'Verify.ExistenceChecked.png': bc['Verify.ExistenceChecked.png'] + stats['sheets'],
    }
    unchanged = {k: v for k, v in bc.items() if k not in new_c}
    n_files_before = base['Metrics']['OutputFileCount']

    L = []
    L.append('# E19.g G1-0: prediction of the effects export\n')
    L.append('Generated by `g1-predict.py` (independent of the converter) from `data-extracted/` and the reference export, '
             'before any converter code was written. `g1-predict.py check` verifies an export against it.\n')
    L.append('## Totals\n')
    L.append('Derived counts (stats): ' + json.dumps(stats, sort_keys=True) + '\n')
    L.append('uuid5 self-check against %d existing catalog entries: passed.\n' % selfcheck)
    L.append('## File-level prediction\n')
    L.append('- Added: exactly %d files (157 map companions, 1 global companion, %d PNG, %d `.texture`).' % (
        len(added), stats['sheets'], stats['sheets']))
    L.append('- Modified: exactly `AssetInfos.json` (+%d entries appended after the existing ones, in the order of the list '
             'below) and `report.json`.' % len(entries))
    L.append('- Deleted: none.')
    L.append('- `AssetInfos.json` predicted SHA-1: `%s`' % catalog_sha(entries))
    L.append('- Reference export before any code: only `report.json` is expected to differ from the previous export, except '
             'the 383 `.entity` files that carry the engine field `min_move_distance` added by E19.h1b3 H1B3-R1 '
             '(measured 2026-10-06: the committed export predated the engine pointer bump; this is the engine, not G1).\n')
    L.append('## Added paths (path, SHA-1 of the file for PNG and `.texture`, SHA-1 of the canonical content for companions)\n')
    L.append('```')
    for p in sorted(added):
        L.append('%s\t%s\t%s' % ('canon' if p.endswith('.json') else 'bytes', added[p], p))
    L.append('```\n')
    L.append('## Catalog entries appended to `AssetInfos.json` (order as written)\n')
    L.append('```')
    for e in entries:
        L.append('%s\t%s\t%s\t%s' % (e['id'], e['asset_type'], e['name'], e['file_name']))
    L.append('```\n')
    L.append('## report.json\n')
    L.append('Counters that change (reference export value, predicted value); every other counter is unchanged.\n')
    L.append('```')
    for k in sorted(new_c):
        L.append('%s\t%s\t%s' % (k, bc.get(k, 'absent'), new_c[k]))
    L.append('```\n')
    L.append('- `Metrics.OutputFileCount`: %d -> %d.' % (n_files_before, n_files_before + len(added)))
    L.append('- `Metrics.OutputSizeBytes`: not predicted exactly (depends on the serialised companion size); it grows.')
    L.append('- Unchanged counters: %d counters, canonical SHA-1 `%s`.' % (
        len(unchanged), sha1(canon(unchanged).encode('utf-8'))))
    L.append('- `Assets.Texture` stays %s. `Errors` stay 0. `Warnings` (%d, SHA-1 `%s`) and `Messages` (%d, SHA-1 `%s`) '
             'are unchanged.' % (bc['Assets.Texture'], len(base['Warnings']), sha1(canon(base['Warnings']).encode('utf-8')),
                                 len(base['Messages']), sha1(canon(base['Messages']).encode('utf-8'))))
    open(PREDICTION, 'w', encoding='utf-8', newline='\n').write('\n'.join(L) + '\n')
    print('prediction written:', len(added), 'added files,', len(entries), 'catalog entries')


# ---------------------------------------------------------------- check
def load_manifest(path):
    d = {}
    for line in open(path, encoding='utf-8'):
        line = line.rstrip('\n')
        if line:
            h, r = line.split('  ', 1)
            d[r] = h
    return d


def block(text, heading):
    m = re.search(r'## %s[^\n]*\n.*?```\n(.*?)```' % re.escape(heading), text, re.S)
    return [l.split('\t') for l in m.group(1).splitlines() if l]


def check(before_path, after_path, report_before_path):
    ok = True

    def fail(msg):
        nonlocal ok
        ok = False
        print('FAIL', msg)

    text = open(PREDICTION, encoding='utf-8').read()
    before, after = load_manifest(before_path), load_manifest(after_path)
    added_p = {r[2]: (r[0], r[1]) for r in block(text, 'Added paths')}
    A = {p for p in after if p not in before}
    D = {p for p in before if p not in after}
    M = {p for p in after if p in before and after[p] != before[p]}
    print('A', len(A), 'M', len(M), 'D', len(D))
    if A != set(added_p):
        fail('added set differs: only actual %s only predicted %s' % (sorted(A - set(added_p))[:5], sorted(set(added_p) - A)[:5]))
    if D:
        fail('deleted: %s' % sorted(D)[:5])
    if M != {'AssetInfos.json', 'report.json'}:
        fail('modified set: %s' % sorted(M)[:10])
    m = re.search(r'`AssetInfos.json` predicted SHA-1: `([0-9a-f]+)`', text)
    if after.get('AssetInfos.json') != m.group(1):
        fail('AssetInfos.json sha %s != %s' % (after.get('AssetInfos.json'), m.group(1)))

    # fresh derivation must still equal the committed prediction (the md is not stale)
    companions, glob_entry, stats = derive()
    fresh = {companions[n][0]: sha1(canon(companions[n][1]).encode('utf-8')) for n in companions}
    fresh[glob_entry[0]] = sha1(canon(glob_entry[1]).encode('utf-8'))
    for p, (kind, h) in added_p.items():
        if kind == 'canon':
            if fresh.get(p) != h:
                fail('stale prediction for %s' % p)
            doc = json.load(open(os.path.join(PROJECT, p), encoding='utf-8'))
            if sha1(canon(doc).encode('utf-8')) != h:
                fail('companion content differs: %s' % p)
        elif after.get(p) != h:
            fail('file sha differs: %s' % p)

    # catalog entries
    cat = json.load(open(os.path.join(PROJECT, 'AssetInfos.json'), encoding='utf-8'))['asset_infos']
    pred_entries = block(text, 'Catalog entries')
    tail = cat[-len(pred_entries):]
    for e, row in zip(tail, pred_entries):
        if [e['id'], e['asset_type'], e['name'], e['file_name']] != row:
            fail('catalog entry differs: %s' % row)
            break
    by_id = {e['id']: e for e in cat}
    if len(by_id) != len(cat):
        fail('duplicate catalog ids')

    # report
    rep = json.load(open(os.path.join(PROJECT, 'report.json'), encoding='utf-8'))
    base = json.load(open(report_before_path, encoding='utf-8'))
    exp = {r[0]: int(r[2]) for r in block(text, 'report.json')}
    rc = rep['Counters']
    for k, v in exp.items():
        if rc.get(k) != v:
            fail('counter %s = %s, predicted %s' % (k, rc.get(k), v))
    rest = {k: v for k, v in rc.items() if k not in exp}
    bc = {k: v for k, v in base['Counters'].items() if k not in exp}
    if rest != bc:
        fail('other counters changed: %s' % [k for k in set(rest) | set(bc) if rest.get(k) != bc.get(k)][:10])
    if rep['Errors']:
        fail('Errors: %s' % rep['Errors'][:3])
    if rep['Warnings'] != base['Warnings'] or rep['Messages'] != base['Messages']:
        fail('Warnings or Messages changed')
    if rep['Metrics']['OutputFileCount'] != base['Metrics']['OutputFileCount'] + len(added_p):
        fail('OutputFileCount %s' % rep['Metrics']['OutputFileCount'])

    # referential proof
    sheets = {}
    for line in open(SHEETS_TSV, encoding='utf-8').read().splitlines()[1:]:
        c = line.split('\t')
        sheets[c[0]] = (int(c[2]), int(c[3]), c[4])
    from PIL import Image
    n_ref = 0
    root = os.path.join(PROJECT)
    docs = {n: (companions[n][0], json.load(open(os.path.join(root, companions[n][0]), encoding='utf-8')))
            for n in companions}
    docs[None] = (glob_entry[0], json.load(open(os.path.join(root, glob_entry[0]), encoding='utf-8')))
    for n, (rel, doc) in docs.items():
        tid = doc['SheetTextureAssetId']
        if not doc['Effects']:
            if tid is not None:
                fail('%s: sheet id without table' % rel)
            continue
        wr = by_id.get(tid)
        if not wr or wr['asset_type'] != 'texture':
            fail('%s: sheet id %s unresolved' % (rel, tid))
            continue
        tex = json.load(open(os.path.join(root, wr['file_name'].replace('\\', '/')), encoding='utf-8'))
        raw = by_id[tex['texture_asset_id']]
        png = os.path.join(root, raw['file_name'].replace('\\', '/'))
        im = Image.open(png).convert('RGBA')
        key = 'map_alundra' if n is None else 'map_%d' % n
        w, h, sha = sheets[key]
        if im.size != (w, h) or hashlib.sha256(im.tobytes()).hexdigest() != sha:
            fail('%s: sheet pixels differ from expected_effect_sheets.tsv' % key)
        for eff in doc['Effects']:
            for s in eff['ImageSets']:
                for q in s['Images']:
                    n_ref += 1
                    if q['U'] < 0 or q['V'] < 0 or q['U'] + q['W'] > w or q['V'] + q['H'] > h:
                        fail('%s: window outside the sheet %s' % (key, q))
    print('referential proof: %d images, %d sheets' % (n_ref, stats['sheets']))
    print('CHECK', 'PASSED' if ok else 'FAILED')
    return 0 if ok else 1


if __name__ == '__main__':
    if len(sys.argv) > 1 and sys.argv[1] == 'check':
        sys.exit(check(sys.argv[2], sys.argv[3], sys.argv[4]))
    write_prediction()
