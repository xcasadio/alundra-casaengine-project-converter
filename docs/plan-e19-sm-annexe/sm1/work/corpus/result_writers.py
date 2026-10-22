"""Which `case 0xNN:` blocks of Dispatch (EventProgramRunner.cs HEAD) mention state.Result, and which call a private helper that does.
Usage: python -B result_writers.py   (reads the repository, writes nothing)"""
import re, sys
REPO = "D:/development/repo/alundra-casaengine-project-converter"
src = open(REPO + "/Alundra/Events/EventProgramRunner.cs", encoding="utf-8-sig").read().split("\n")
# Dispatch is lines 563-2105 (1-based) at HEAD.
lo, hi = 562, 2105
cases = []
cur = None
for i in range(lo, hi):
    line = src[i]
    m = re.match(r"^            case 0x([0-9A-Fa-f]{2}):", line)
    if m:
        cur = [int(m.group(1), 16), i + 1, []]
        cases.append(cur)
    elif re.match(r"^            default:", line):
        cur = None
    if cur is not None:
        cur[2].append(line)
# helper methods that write state.Result: scan the whole file for methods whose body mentions "state.Result =" or ".Result ="
text = "\n".join(src)
methods = {}
for m in re.finditer(r"\n    (?:private|internal|public)[^\n(]*?\b(\w+)\(([^)]*)\)\s*\n    \{", text):
    name = m.group(1)
    start = m.end()
    depth = 1
    j = start
    while depth > 0 and j < len(text):
        c = text[j]
        if c == '{':
            depth += 1
        elif c == '}':
            depth -= 1
        j += 1
    body = text[start:j]
    methods[name] = body
ASSIGN = re.compile(r"\bResult\s*=(?!=)")
writer_methods = {n for n, b in methods.items() if ASSIGN.search(b)}
print("helper methods that assign Result:", sorted(writer_methods))
rows = []
for op, line, body in cases:
    b = "\n".join(body)
    direct = bool(ASSIGN.search(b))
    via = sorted({n for n in writer_methods if re.search(r"\b" + n + r"\(", b)})
    rows.append((op, line, direct, via))
for op, line, direct, via in rows:
    print("0x%02X line %d direct=%s via=%s" % (op, line, direct, ",".join(via)))
