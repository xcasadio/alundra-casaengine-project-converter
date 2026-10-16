#!/usr/bin/env python
"""Find, in Alundra.Tests, the hand-written bytecode programs that contain each opcode AS AN INSTRUCTION (label script2). Read-only.

A program literal is: NewDocument(a, b, c...) / Codes = new[] { ... } / Codes = new int[] { ... } / any new[] { ... } or new int[] { ... }
whose items are literals (0x.., decimal) or identifiers/expressions (operand value unknown -> 0), and which decodes with the
size table (EventOpcodeSizeTable.cs) as a sequence of whole instructions ending exactly on its last item (a trailing 0xFF is
allowed, or an unterminated program ending on an instruction boundary). Instructions are counted at instruction boundaries only,
so an operand byte equal to an opcode value is NOT counted (unlike a plain hex-literal grep).
Output: tests-instr.json {op: {methods, files, examples:[file::method]}}
"""
import os, re, json, collections

REPO = 'D:/development/repo/alundra-casaengine-project-converter'
TESTS = REPO + '/Alundra.Tests'
OUT = os.path.dirname(os.path.abspath(__file__))

src = open(REPO + '/Alundra/Scripts/EventOpcodeSizeTable.cs', encoding='utf-8').read()
SIZES = {int(m.group(1), 16): int(m.group(2)) for m in re.finditer(r'\{\s*0x([0-9A-Fa-f]{2}),\s*new\((\d+),', src)}

METHOD_SIG = re.compile(r'public\s+(?:async\s+)?(?:static\s+)?(?:void|Task)\s+([A-Za-z0-9_]+)\s*\(')
TEST_ATTR = re.compile(r'\[(?:Fact|Theory)')
ARRAY_START = re.compile(r'(?:new\s*\[\s*\]|new\s+int\s*\[\s*\])\s*\{|NewDocument\s*\(|Codes\s*=\s*new\s*\[\s*\]\s*\{')


def split_top(s):
    items, depth, cur = [], 0, []
    for ch in s:
        if ch in '([{':
            depth += 1
        elif ch in ')]}':
            depth -= 1
        if ch == ',' and depth == 0:
            items.append(''.join(cur).strip()); cur = []
        else:
            cur.append(ch)
    last = ''.join(cur).strip()
    if last:
        items.append(last)
    return items


def tok_val(t):
    t = t.strip()
    m = re.fullmatch(r'0x([0-9A-Fa-f]+)', t)
    if m:
        return int(m.group(1), 16), True
    if re.fullmatch(r'\d+', t):
        return int(t), True
    return 0, False


def match_close(text, i, open_ch, close_ch):
    depth = 0
    for j in range(i, len(text)):
        if text[j] == open_ch:
            depth += 1
        elif text[j] == close_ch:
            depth -= 1
            if depth == 0:
                return j
    return -1


def decode(vals):
    """vals: list of (value, literal?) -> list of opcodes at instruction boundaries, or None if not a whole-instruction program."""
    ops, i, n = [], 0, len(vals)
    if n < 1:
        return None
    while i < n:
        v, lit = vals[i]
        if not lit or v not in SIZES:
            return None
        size = SIZES[v]
        if v == 0xFF:
            ops.append(0xFF)
            return ops if i == n - 1 else None
        if size <= 0 or i + size > n:
            return None
        ops.append(v)
        i += size
    return ops


found = collections.defaultdict(set)  # op -> {(file, method)}
programs_total = 0
for root, dirs, files in os.walk(TESTS):
    dirs[:] = [d for d in dirs if d not in ('bin', 'obj')]
    for fn in files:
        if not fn.endswith('.cs'):
            continue
        path = os.path.join(root, fn)
        rel = os.path.relpath(path, TESTS).replace('\\', '/')
        text = open(path, encoding='utf-8', errors='replace').read()
        # method spans (start offsets of method signatures)
        sigs = [(m.start(), m.group(1)) for m in METHOD_SIG.finditer(text)]

        def method_at(pos):
            name = None
            for s, nm in sigs:
                if s <= pos:
                    name = nm
                else:
                    break
            return name or '<class>'

        for m in ARRAY_START.finditer(text):
            if m.group(0).startswith('NewDocument'):
                open_i = m.end() - 1
                close_i = match_close(text, open_i, '(', ')')
            else:
                open_i = m.end() - 1
                close_i = match_close(text, open_i, '{', '}')
            if close_i < 0:
                continue
            body = text[open_i + 1:close_i]
            if len(body) > 4000:
                continue
            items = split_top(body)
            vals = [tok_val(t) for t in items]
            ops = decode(vals)
            if ops is None:
                continue
            programs_total += 1
            meth = method_at(m.start())
            for op in set(ops):
                found[op].add((rel, meth))

out = {}
for op in range(256):
    s = found.get(op, set())
    out['%02X' % op] = {
        'methods': len(s),
        'files': len({f for f, _ in s}),
        'examples': ['%s::%s' % (f, mm) for f, mm in sorted(s)[:3]],
    }
out['_programs_decoded'] = programs_total
json.dump(out, open(OUT + '/tests-instr.json', 'w', encoding='utf-8'), indent=1)
print('programs decoded', programs_total)
