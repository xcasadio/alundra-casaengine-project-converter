"""Independent check: every 'AlundraEventProgramRunner.cs:NNN' of opcodes.tsv, translated by -50, against HEAD; and test citations."""
import collections
import re
import sys

repo = sys.argv[1]
head = open(f"{repo}/Alundra/Events/EventProgramRunner.cs", encoding="utf-8").read().split("\n")
rows = [l.rstrip("\n").split("\t") for l in open(f"{repo}/docs/plan-e19-sm-annexe/opcodes.tsv", encoding="utf-8")]
hdr = rows[0]
col = hdr.index("dll_handler")
ok = bad = 0
kinds = collections.Counter()
for r in rows[1:]:
    op = int(r[0], 16)
    for m in re.finditer(r"AlundraEventProgramRunner\.cs:(\d+)", r[col]):
        n = int(m.group(1)) - 50
        text = head[n - 1]
        if re.search(r"case 0x%02X\b" % op, text, re.I):
            ok += 1; kinds["case"] += 1
        elif "UnknownOpcode" in text:
            ok += 1; kinds["UnknownOpcode"] += 1
        elif op in (0x00, 0xFF) and ("command == 0x%02X" % op) in text:
            ok += 1; kinds["loop"] += 1
        else:
            bad += 1
            print("MISMATCH", r[0], m.group(0), "->", n, text.strip()[:100])
print("runner cites ok", ok, "bad", bad, dict(kinds))
tests = collections.Counter()
for r in rows[1:]:
    for c in r:
        for m in re.finditer(r"(Alundra[A-Za-z]+Tests\.cs):(\d+)(?:-(\d+))?", c):
            tests[m.group(0)] += 1
print("test citations (distinct):", len(tests))
for k, v in sorted(tests.items()):
    print("  ", k, v)
