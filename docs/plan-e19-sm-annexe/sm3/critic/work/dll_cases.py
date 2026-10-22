"""Critic-own: parse EventProgramRunner.Dispatch (read-only) and list, per case label, whether the case body mentions state.Result,
which helpers it calls with `state`, and the return statements. Used to build the Result-writer set and jump semantics."""
import re
import sys

SRC = open('D:/development/repo/alundra-casaengine-project-converter/Alundra/Events/EventProgramRunner.cs', encoding='utf-8').read().split('\n')
# Dispatch spans lines 563..2105 (1-based)
start, end = 562, 2105
cases = []
cur = None
for i in range(start, end):
    ln = SRC[i]
    m = re.match(r'\s{12}case (0x[0-9A-Fa-f]{2}):', ln)
    if m:
        cur = [int(m.group(1), 16), i + 1, []]
        cases.append(cur)
    # also "case 0x..: case 0x..:" stacked labels are handled by the next regex
    if cur is not None:
        cur[2].append((i + 1, ln))

# stacked labels: a case body that is empty up to the next case label shares the next body
out = {}
for k, (op, line, body) in enumerate(cases):
    txt = '\n'.join(t for _, t in body)
    out[op] = (line, txt)

if __name__ == '__main__':
    writers = []
    for op, (line, txt) in sorted(out.items()):
        code = re.sub(r'//.*', '', txt)
        w = bool(re.search(r'\.Result\s*=[^=]', code)) or bool(re.search(r'\bResult\s*=[^=]', code))
        helpers = sorted(set(re.findall(r'\b([A-Z][A-Za-z0-9]+)\([^;]*\bstate\b', code)))
        if w or helpers:
            print('0x%02X line %d  writesResult=%s helpers=%s' % (op, line, w, helpers))
