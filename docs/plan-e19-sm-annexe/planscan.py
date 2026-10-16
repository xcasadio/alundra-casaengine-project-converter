#!/usr/bin/env python
"""Index of the mentions of each opcode in docs/plan-e19-opcodes.md and the other plans (label script2). Read-only.

For plan-e19-opcodes.md: each mention (line) of 0xNN (or a backticked bare list such as `05 06 30`) is attributed to the
nearest preceding '### 1.2x' (or '## 0.x', '## 3', '## 4') header; the slice id is read from that header.
Output: plan-mentions.json {op: {slices: {slice: count}, first_lines: [..], total: n, other_docs: {doc: count}}}
"""
import re, os, json, collections

REPO = 'D:/development/repo/alundra-casaengine-project-converter'
OUT = os.path.dirname(os.path.abspath(__file__))
E19 = REPO + '/docs/plan-e19-opcodes.md'

HEX = re.compile(r'(?<![0-9A-Za-z_])0x([0-9A-Fa-f]{2})(?![0-9A-Fa-f])')
RANGE = re.compile(r'`0x([0-9A-Fa-f]{2})`\s*(?:-|a|à|to)\s*`0x([0-9A-Fa-f]{2})`')

lines = open(E19, encoding='utf-8').read().split('\n')

def slice_id(h):
    m = re.match(r'#{2,4}\s+(\d+(?:\.\d+[a-z]?(?:\.\d+)?)?)\s+(.*)', h)
    title = h.lstrip('# ').strip()
    m2 = re.search(r'\b(E19\.[a-z]\d?[a-z]?\d?|E19\.[a-z]|X2|§1\.\d+[a-z]?|Enveloppe)', title)
    return (m2.group(1) if m2 else title[:30])

current = '## 0-preamble'
section_of_line = []
for i, l in enumerate(lines):
    if re.match(r'#{2,4}\s', l):
        current = l
    section_of_line.append(current)

res = collections.defaultdict(lambda: {'slices': collections.Counter(), 'first_lines': [], 'total': 0})
for i, l in enumerate(lines):
    ops = set(int(m.group(1), 16) for m in HEX.finditer(l))
    # inclusive ranges written `0x20` à `0x23`
    for m in RANGE.finditer(l):
        a, b = int(m.group(1), 16), int(m.group(2), 16)
        pass  # range expansion dropped: `0x50`-`0x56` etc. are often sub-case ids of 0x82, not opcodes
    for op in ops:
        r = res[op]
        r['total'] += 1
        sec = section_of_line[i]
        num = re.match(r'#{2,4}\s+(\S+)', sec).group(1)
        sid = num + ' ' + slice_id(sec)
        if sid.split(' ', 1)[1].startswith(num) or sid.split(' ', 1)[1].startswith('3. ') or sid.split(' ', 1)[1].startswith('1.1'):
            sid = sid.split(' ', 1)[1][:24]
        r['slices'][sid] += 1
        if len(r['first_lines']) < 4:
            r['first_lines'].append(i + 1)

# other docs
others = collections.defaultdict(collections.Counter)
docs = [f for f in os.listdir(REPO + '/docs') if f.endswith('.md') and f != 'plan-e19-opcodes.md']
for d in docs:
    t = open(REPO + '/docs/' + d, encoding='utf-8', errors='replace').read()
    for m in HEX.finditer(t):
        others[int(m.group(1), 16)][d] += 1

out = {}
for op in range(256):
    r = res.get(op)
    out['%02X' % op] = {
        'total': r['total'] if r else 0,
        'slices': dict(r['slices'].most_common(8)) if r else {},
        'first_lines': r['first_lines'] if r else [],
        'other_docs': dict(others[op].most_common(6)),
    }
json.dump(out, open(OUT + '/plan-mentions.json', 'w', encoding='utf-8'), indent=1)
print('done')
