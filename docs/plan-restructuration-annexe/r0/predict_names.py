"""predict_names.py - READ-ONLY prediction of the export after a change of the script class-name strings only.

It never runs the converter: it reads the current export tree, replaces the one expected occurrence of each old string by
its new string in memory, and prints/writes the predicted SHA-1 and size of every changed file, plus the list of paths that
diff_manifests.py (docs/plan-e20-annexe/e20-e0x/) must accept. The tree is never written.

Keys and files (counts measured on the export of 2026-10-15 08:15):
  *.entity   "script_class_name": "<old entity proxy>"    expected once in 395 files, 0 in the others (AlundraCamera.entity: null)
  *.world    "script_class_name": "<old world proxy>"     expected once in 483 files (the 2 inline entities per world stay null)
  *.gameMode "player_controller_class": "<old ctrl>"      expected once in Entities/Alundra/Alundra.gameMode
  UI/Screens/*.design.json "view_model_type": "<old vm>"  versioned by hand (ADR-0002), never written by the converter
usage: python -I predict_names.py <tree> <out-dir> entity=OLD:NEW world=OLD:NEW controller=OLD:NEW [vm=OLD:NEW ...]
"""
import hashlib
import os
import sys

tree = os.path.abspath(sys.argv[1])
out_dir = sys.argv[2]
pairs = {}
vms = []
for arg in sys.argv[3:]:
    key, value = arg.split('=', 1)
    old, new = value.split(':', 1)
    if key == 'vm':
        vms.append((old, new))
    else:
        pairs[key] = (old, new)

RULES = [
    # (label, extension, json key, mapping key, expected file count)
    ('entity', '.entity', 'script_class_name', 'entity', 395),
    ('world', '.world', 'script_class_name', 'world', 483),
    ('gameMode', '.gameMode', 'player_controller_class', 'controller', 1),
]

files_by_ext = {}
for dirpath, dirnames, filenames in os.walk(tree):
    if os.path.relpath(dirpath, tree) == '.':
        dirnames[:] = [d for d in dirnames if d != '.casaeditor']
    for name in filenames:
        ext = os.path.splitext(name)[1]
        rel = os.path.relpath(os.path.join(dirpath, name), tree).replace(os.sep, '/')
        files_by_ext.setdefault(ext, []).append(rel)

changed = []   # (rel, old size, new size, new sha1)
problems = []
for label, ext, key, mkey, expected in RULES:
    old, new = pairs[mkey]
    needle_old = ('"%s": "%s"' % (key, old)).encode('ascii')
    needle_new = ('"%s": "%s"' % (key, new)).encode('ascii')
    hit_files = 0
    for rel in sorted(files_by_ext.get(ext, [])):
        data = open(os.path.join(tree, rel), 'rb').read()
        n = data.count(needle_old)
        if n == 0:
            continue
        if n != 1:
            problems.append('%s: %d occurrences of %s' % (rel, n, needle_old.decode()))
        if data.count(needle_new) and old != new:
            problems.append('%s: already carries the new string' % rel)
        hit_files += 1
        new_data = data.replace(needle_old, needle_new)
        changed.append((rel, len(data), len(new_data), hashlib.sha1(new_data).hexdigest()))
    if hit_files != expected:
        problems.append('%s: %d files carry the old string, %d expected' % (label, hit_files, expected))
    print('%-9s %4d files, old "%s" -> new "%s", delta %+d bytes per file' % (label, hit_files, old, new, len(new) - len(old)))

for old, new in vms:
    hits = []
    for rel in sorted(files_by_ext.get('.json', [])):
        if not rel.startswith('UI/Screens/') or not rel.endswith('.design.json'):
            continue
        data = open(os.path.join(tree, rel), 'rb').read()
        needle_old = ('"view_model_type": "%s"' % old).encode('ascii')
        if data.count(needle_old) == 1:
            new_data = data.replace(needle_old, ('"view_model_type": "%s"' % new).encode('ascii'))
            changed.append((rel, len(data), len(new_data), hashlib.sha1(new_data).hexdigest()))
            hits.append(rel)
    if len(hits) != 1:
        problems.append('view model %s: found in %d design.json files, 1 expected' % (old, len(hits)))
    print('vm        %s -> %s : %s' % (old, new, ', '.join(hits)))

os.makedirs(out_dir, exist_ok=True)
with open(os.path.join(out_dir, 'predicted-changes.tsv'), 'w', encoding='utf-8', newline='\n') as f:
    f.write('path\told_size\tnew_size\tnew_sha1\n')
    for rel, a, b, h in changed:
        f.write('%s\t%d\t%d\t%s\n' % (rel, a, b, h))
with open(os.path.join(out_dir, 'allowed-paths.txt'), 'w', encoding='utf-8', newline='\n') as f:
    for rel in sorted([c[0] for c in changed] + ['report.json']):
        f.write(rel + '\n')
delta_converter = sum(b - a for rel, a, b, h in changed if not rel.startswith('UI/Screens/'))
delta_screens = sum(b - a for rel, a, b, h in changed if rel.startswith('UI/Screens/'))
print('changed files: %d converter outputs + %d hand-versioned design.json + report.json'
      % (sum(1 for c in changed if not c[0].startswith('UI/Screens/')), sum(1 for c in changed if c[0].startswith('UI/Screens/'))))
print('byte delta of converter outputs %+d ; design.json %+d (OutputSizeBytes = all files of the tree: both deltas, plus DLL, PDB, .casaeditor and previous report.json deltas, ConversionReport.cs:56-75)'
      % (delta_converter, delta_screens))
print('PROBLEMS: %d' % len(problems))
for p in problems:
    print('  ' + p)
