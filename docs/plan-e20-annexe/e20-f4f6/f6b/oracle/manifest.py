"""manifest.py <annex-root>: SHA-1 of every output of the f6b T0 oracle in a COPY of the annex (the ball and flame keys and traces of bin/out that the frozen scripts rewrite, f6b/oracle/out), sorted by path: '<sha1>  <relative path>'.
Two complete runs from two copies must give identical manifests (python -B manifest.py <copy-1> > m1.txt; same for <copy-2>; cmp m1.txt m2.txt)."""
import hashlib
import os
import sys

root = os.path.abspath(sys.argv[1])
lines = []
for base in ('bin/out', 'f6b/oracle/out'):
    for d, _, files in os.walk(os.path.join(root, base)):
        for f in files:
            p = os.path.join(d, f)
            rel = os.path.relpath(p, root).replace(os.sep, '/')
            if base == 'bin/out' and not (f.startswith('fire') or f.startswith('ice') or f.startswith('flames')):
                continue
            h = hashlib.sha1(open(p, 'rb').read().replace(b'\r\n', b'\n')).hexdigest()
            lines.append('%s  %s' % (h, rel))
print('\n'.join(sorted(lines, key=lambda l: l.split('  ', 1)[1])))
