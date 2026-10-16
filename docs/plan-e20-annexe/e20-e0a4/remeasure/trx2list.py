#!/usr/bin/env python3
"""trx2list.py <file.trx> <out.txt>: counts and the exact list of failing tests, one per line, with the first lines of the message."""
import re
import sys
sys.stdout.reconfigure(encoding="utf-8")
import xml.etree.ElementTree as ET

trx, out = sys.argv[1], sys.argv[2]
tree = ET.parse(trx)
root = tree.getroot()
ns = {"t": root.tag.split("}")[0].strip("{")}
counts = {}
fails = []
for r in root.iterfind(".//t:UnitTestResult", ns):
    o = r.get("outcome")
    counts[o] = counts.get(o, 0) + 1
    if o == "Failed":
        name = r.get("testName")
        msg_el = r.find(".//t:ErrorInfo/t:Message", ns)
        msg = (msg_el.text or "").strip() if msg_el is not None else ""
        stack_el = r.find(".//t:ErrorInfo/t:StackTrace", ns)
        stack = (stack_el.text or "").strip() if stack_el is not None else ""
        fails.append((name, msg, stack))
fails.sort()
with open(out, "w", encoding="utf-8") as f:
    f.write("COUNTS " + " ".join(f"{k}={v}" for k, v in sorted(counts.items())) + "\n")
    f.write(f"FAILED {len(fails)}\n")
    for name, msg, stack in fails:
        short = re.sub(r"\s+", " ", msg)[:600]
        f.write(f"- {name} - {short}\n")
    f.write("\n==== FULL MESSAGES ====\n")
    for name, msg, stack in fails:
        f.write(f"\n### {name}\n{msg}\n--stack--\n{stack[:1500]}\n")
print(open(out, encoding="utf-8").read().split("\n==== FULL")[0])
