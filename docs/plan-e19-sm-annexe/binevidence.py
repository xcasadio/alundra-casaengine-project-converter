#!/usr/bin/env python
"""For each opcode, the doc lines (docs/*.md and DLL tests) that name 0xNN and a binary address 0x800xxxxx on the same line (label script2). Read-only.
Output: bin-evidence.json {op: {doc_lines: n, files: [..], example: 'file:line'}}"""
import re, os, json, collections
REPO = 'D:/development/repo/alundra-casaengine-project-converter'
OUT = os.path.dirname(os.path.abspath(__file__))
HEX = re.compile(r'(?<![0-9A-Za-z_])0x([0-9A-Fa-f]{2})(?![0-9A-Fa-f])')
ADDR = re.compile(r'0x80[0-9A-Fa-f]{6}')
res = collections.defaultdict(lambda: {'n': 0, 'files': collections.Counter(), 'example': None})
roots = [REPO + '/docs', REPO + '/Alundra.Tests', REPO + '/Alundra']
for root in roots:
    for dp, dns, fns in os.walk(root):
        dns[:] = [d for d in dns if d not in ('bin', 'obj', 'out', 'o63', 'verify')]
        for fn in fns:
            if not fn.endswith(('.md', '.cs', '.txt')):
                continue
            p = os.path.join(dp, fn)
            try:
                lines = open(p, encoding='utf-8', errors='replace').read().split('\n')
            except Exception:
                continue
            rel = os.path.relpath(p, REPO).replace('\\', '/')
            for i, l in enumerate(lines):
                if not ADDR.search(l):
                    continue
                for m in HEX.finditer(l):
                    op = int(m.group(1), 16)
                    r = res[op]
                    r['n'] += 1
                    r['files'][rel] += 1
                    if r['example'] is None:
                        r['example'] = '%s:%d' % (rel, i + 1)
out = {}
for op in range(256):
    r = res.get(op)
    out['%02X' % op] = {'lines': r['n'] if r else 0, 'files': len(r['files']) if r else 0, 'example': r['example'] if r else None}
json.dump(out, open(OUT + '/bin-evidence.json', 'w', encoding='utf-8'), indent=1)
print('done')
