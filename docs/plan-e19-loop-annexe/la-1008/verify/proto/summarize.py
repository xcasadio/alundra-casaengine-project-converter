"""Summarizes trx files: counters and failed test names (argv: trx paths)."""
import re
import sys

for path in sys.argv[1:]:
    try:
        t = open(path, encoding="utf-8").read()
    except OSError as e:
        print(path, "MISSING", e)
        continue
    c = re.search(r'<Counters total="(\d+)" executed="(\d+)" passed="(\d+)" failed="(\d+)"', t)
    print(f"== {path.split('/')[-1]}: total {c.group(1)} passed {c.group(3)} failed {c.group(4)}")
    for m in re.finditer(r'<UnitTestResult [^>]*testName="([^"]+)"[^>]*outcome="Failed"', t):
        print("   FAILED", m.group(1).replace("CasaEngine.Tests.", ""))
