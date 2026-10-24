"""enclosing.py - for 'file:line' arguments, print the enclosing test method name (nearest preceding 'public void/Task' line). Read-only."""
import sys, re
ROOT = "D:/development/repo/alundra-casaengine-project-converter/Alundra.Tests/"
for arg in sys.argv[1:]:
    f, ln = arg.rsplit(":", 1)
    lines = open(ROOT + f, encoding="utf-8-sig").read().splitlines()
    ln = int(ln)
    name = None
    for i in range(ln - 1, -1, -1):
        m = re.search(r"public\s+(?:async\s+)?(?:void|Task|static\s+\w+)\s+(\w+)\s*\(", lines[i])
        if m:
            name = (m.group(1), i + 1)
            break
    print("%-55s -> %s" % (arg, name))
