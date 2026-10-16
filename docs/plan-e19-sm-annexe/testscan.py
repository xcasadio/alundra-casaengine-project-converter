#!/usr/bin/env python
"""Scan Alundra.Tests for the tests that name each opcode (label script2). Read-only.

Per opcode NN (0x00-0xFF):
  named_methods : [Fact]/[Theory] methods whose NAME contains 0xNN (or Op/Opcode_NN) -> strong evidence
  doc_files     : test files whose '///' or '//' comment lines (anywhere) name 0xNN together with 'opcode' (case-insens) -> medium
  opcode_cmp    : lines comparing a trace/runner Opcode with 0xNN (Opcode == / is / ==, "Opcode: 0xNN") -> medium
  body_methods  : test methods whose body contains the literal 0xNN as a token (program arrays: noisy, operand bytes also match) -> weak
Output: tests-by-opcode.json
"""
import os, re, json, collections

TESTS = 'D:/development/repo/alundra-casaengine-project-converter/Alundra.Tests'
OUT = os.path.dirname(os.path.abspath(__file__))

HEX = re.compile(r'(?<![0-9A-Za-z_])0x([0-9A-Fa-f]{1,2})(?![0-9A-Fa-f])')
NAME_HEX = re.compile(r'(?:0x|Op_?|Opcode_?)([0-9A-Fa-f]{2})(?![0-9A-Za-z])')
METHOD = re.compile(r'\[(?:Fact|Theory)[^\]]*\]\s*(?:\[[^\]]*\]\s*)*public\s+(?:async\s+)?(?:void|Task)\s+([A-Za-z0-9_]+)\s*\(', re.S)

INTERP = re.compile(r'AlundraEventProgramRunner|IEventProgramRunner|RunOneScriptCall|EventProgramDocument|EventProgramState|TraceSink|EventOpcodeSizeTable|MapEventProgramLoader|ProgramIndexes|EventTraceKind')
interp_files = set()
named = collections.defaultdict(list)    # op -> [(file, method)]
body = collections.defaultdict(set)      # op -> {(file, method)}
docfiles = collections.defaultdict(set)  # op -> {file}
cmp_files = collections.defaultdict(set)
files_any = collections.defaultdict(set)
total_methods = 0

def body_of(text, start):
    i = text.find('{', start)
    if i < 0:
        return ''
    depth = 0
    j = i
    n = len(text)
    while j < n:
        ch = text[j]
        if ch == '{':
            depth += 1
        elif ch == '}':
            depth -= 1
            if depth == 0:
                return text[i:j + 1]
        j += 1
    return text[i:]

for root, dirs, files in os.walk(TESTS):
    dirs[:] = [d for d in dirs if d not in ('bin', 'obj')]
    for fn in files:
        if not fn.endswith('.cs'):
            continue
        path = os.path.join(root, fn)
        rel = os.path.relpath(path, TESTS).replace('\\', '/')
        text = open(path, encoding='utf-8', errors='replace').read()
        interp = bool(INTERP.search(text))
        if interp:
            interp_files.add(rel)
        for m in HEX.finditer(text):
            files_any[int(m.group(1), 16)].add(rel)
        for line in text.split('\n'):
            s = line.strip()
            if s.startswith('//'):
                if re.search(r'opcode', s, re.I):
                    for m in HEX.finditer(s):
                        docfiles[int(m.group(1), 16)].add((rel, interp))
            if re.search(r'Opcode\s*(==|is|:)\s*(0x|\()|Opcode, 0x|\.Opcode\b.*0x', s) or re.search(r'record\.Opcode is 0x|Opcode is (0x[0-9A-Fa-f]{2}( or 0x[0-9A-Fa-f]{2})*)', s):
                for m in HEX.finditer(s):
                    cmp_files[int(m.group(1), 16)].add((rel, interp))
        for m in METHOD.finditer(text):
            total_methods += 1
            name = m.group(1)
            for nm in NAME_HEX.finditer(name):
                named[int(nm.group(1), 16)].append((rel, name, interp))
            b = body_of(text, m.end())
            seen = set()
            for hm in HEX.finditer(b):
                v = int(hm.group(1), 16)
                if v not in seen:
                    seen.add(v)
                    body[v].add((rel, name))

result = {}
for op in range(256):
    nm = [(f, n) for f, n, i in named.get(op, []) if i]
    result['%02X' % op] = {
        'named_methods': len(nm),
        'named_methods_all_files': len(named.get(op, [])),
        'named_files': sorted({f for f, _ in nm}),
        'named_examples': [n for _, n in nm][:4],
        'doc_files': sorted(f for f, i in docfiles.get(op, []) if i),
        'cmp_files': sorted(f for f, i in cmp_files.get(op, []) if i),
        'body_methods': len(body.get(op, [])),
        'body_files': len({f for f, _ in body.get(op, [])}),
        'files_any': len(files_any.get(op, [])),
    }
result['_interp_files'] = len(interp_files)
json.dump(result, open(OUT + '/tests-by-opcode.json', 'w', encoding='utf-8'), indent=1)
print('test methods scanned', total_methods)
