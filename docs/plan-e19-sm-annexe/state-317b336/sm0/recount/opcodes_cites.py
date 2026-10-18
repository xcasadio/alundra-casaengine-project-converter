"""Read-only: the 256 'dll_handler' citations of docs/plan-e19-sm-annexe/opcodes.tsv (old runner lines) against HEAD.
For each opcode: old cited line, HEAD line = old - shift (by content match), and whether HEAD line holds 'case 0xNN' (or the UnknownOpcode default)."""
import csv
import difflib
import json
import re
import subprocess
import collections

REPO = 'D:/development/repo/alundra-casaengine-project-converter'
table = json.load(open(REPO + '/tools/dll-structure/table.json', encoding='utf-8'))
pairs = {t['old_name']: t['final']['name'] for t in table['types'] if t['old_name'] != t['final']['name']}
pat = re.compile(r'\b(' + '|'.join(re.escape(n) for n in sorted(pairs, key=len, reverse=True)) + r')\b')
rename = lambda s: pat.sub(lambda m: pairs[m.group(1)], s)

old = subprocess.run(['git', '-C', REPO, 'show', '9502f75:Alundra/Scripts/AlundraEventProgramRunner.cs'], capture_output=True).stdout.decode('utf-8', 'replace').split('\n')
new = open(REPO + '/Alundra/Events/EventProgramRunner.cs', 'rb').read().decode('utf-8', 'replace').split('\n')
no = [rename(l).rstrip('\r') for l in old]
nn = [l.rstrip('\r') for l in new]
mapping = {}
for tag, i1, i2, j1, j2 in difflib.SequenceMatcher(None, no, nn, autojunk=False).get_opcodes():
    if tag == 'equal':
        for k in range(i2 - i1):
            mapping[i1 + k] = j1 + k

rows = list(csv.DictReader(open(REPO + '/docs/plan-e19-sm-annexe/opcodes.tsv', encoding='utf-8'), delimiter='\t'))
shifts = collections.Counter()
bad = []
cases_ok = 0
for r in rows:
    m = re.search(r'AlundraEventProgramRunner\.cs:(\d+)', r['dll_handler'])
    if not m:
        bad.append((r['opcode'], 'no citation'))
        continue
    line = int(m.group(1))
    h = mapping.get(line - 1)
    if h is None:
        bad.append((r['opcode'], line, 'not mapped'))
        continue
    shifts[line - (h + 1)] += 1
    op = r['opcode'].upper().replace('0X', '')
    text = nn[h]
    expect_case = re.search(r'case 0x' + op + r'\b', text, re.I) is not None
    if r['dll_state'] in ('SKIPPED_BY_SIZE', 'TERMINATES_SIZE0', 'NOT_IN_TABLE'):
        ok = 'UnknownOpcode' in text
    elif r['dll_state'] == 'LOOP_BUILTIN':
        ok = True
    else:
        ok = expect_case
    if ok:
        cases_ok += 1
    else:
        bad.append((r['opcode'], r['dll_state'], line, h + 1, text.strip()[:90]))
print('shifts', dict(shifts))
print('citations whose HEAD line has the right case or UnknownOpcode', cases_ok, 'of', len(rows))
for b in bad:
    print(b)
print(collections.Counter(r['dll_state'] for r in rows))
