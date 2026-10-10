"""manifest.py - md5 of every output file of ../out (except logs and the manifests themselves): two complete runs must give identical manifests.
usage: python -B manifest.py [name]"""
import hashlib
import os
import sys

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'out')
name = sys.argv[1] if len(sys.argv) > 1 else 'manifest.txt'
lines = []
for root, dirs, files in os.walk(OUT):
    dirs.sort()
    for f in sorted(files):
        if f.startswith('log_') or f.startswith('manifest') or f.endswith('.md5') or f == 'd1_gate_run.log':
            continue
        p = os.path.join(root, f)
        rel = os.path.relpath(p, OUT).replace(os.sep, '/')
        lines.append('%s  %s' % (hashlib.md5(open(p, 'rb').read()).hexdigest(), rel))
open(os.path.join(OUT, name), 'w').write('\n'.join(lines) + '\n')
print(len(lines), 'files')
