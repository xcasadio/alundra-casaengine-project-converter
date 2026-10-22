"""manifest.py - SHA-1 manifest of the converter output tree (one line per file: '<sha1>  <relative/path>').

Perimeter (docs/plan-e9-backdrops-residus.md D-E9-8): the whole tree EXCEPT Alundra.dll, Alundra.pdb (build deposit of
Alundra.csproj) and .casaeditor/ (editor state). Paths use '/', lines sorted by path (ordinal), LF line ends.
Also prints the number of files and the SHA-1 of the manifest text itself (the 'empreinte' used by the plans).
usage: python -I manifest.py <tree-dir> <manifest-out>
"""
import hashlib
import os
import sys

root = os.path.abspath(sys.argv[1])
out = sys.argv[2]
EXCLUDED_FILES = {'Alundra.dll', 'Alundra.pdb'}   # only at the root of the tree
rows = []
for dirpath, dirnames, filenames in os.walk(root):
    rel_dir = os.path.relpath(dirpath, root).replace(os.sep, '/')
    if rel_dir == '.':
        rel_dir = ''
        dirnames[:] = [d for d in dirnames if d != '.casaeditor']
    for name in filenames:
        rel = (rel_dir + '/' + name) if rel_dir else name
        if not rel_dir and name in EXCLUDED_FILES:
            continue
        h = hashlib.sha1()
        with open(os.path.join(dirpath, name), 'rb') as f:
            for chunk in iter(lambda: f.read(1 << 20), b''):
                h.update(chunk)
        rows.append((rel, h.hexdigest()))
rows.sort(key=lambda r: r[0])
text = ''.join('%s  %s\n' % (digest, rel) for rel, digest in rows)
with open(out, 'w', encoding='utf-8', newline='\n') as f:
    f.write(text)
print('%d files ; manifest sha1 %s' % (len(rows), hashlib.sha1(text.encode('utf-8')).hexdigest()))
