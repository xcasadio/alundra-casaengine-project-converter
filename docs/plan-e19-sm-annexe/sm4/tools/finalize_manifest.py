import hashlib, os, sys
root = sys.argv[1]
lines = []
for dp, dn, fn in os.walk(root):
    dn.sort()
    for f in sorted(fn):
        p = os.path.join(dp, f)
        rel = os.path.relpath(p, root).replace(os.sep, '/')
        if rel == 'MANIFEST.sha256':
            continue
        data = open(p, 'rb').read().replace(b'\r\n', b'\n')
        lines.append('%s  %s\n' % (hashlib.sha256(data).hexdigest(), rel))
lines.sort(key=lambda s: s.split('  ', 1)[1])
open(os.path.join(root, 'MANIFEST.sha256'), 'w', encoding='utf-8', newline='\n').write(''.join(lines))
print(''.join(lines))
