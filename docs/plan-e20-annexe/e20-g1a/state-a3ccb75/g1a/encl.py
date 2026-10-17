import re, sys
# usage: encl.py file line [line ...]  -> prints the enclosing method/class name for each line (regex based).
path = sys.argv[1]
lines = open(path, encoding='utf-8-sig', errors='replace').read().split('\n')
meth = re.compile(r'^\s*(?:public|internal|private|protected)\s+(?:static\s+)?(?:async\s+)?(?:[\w<>\[\],.?]+\s+)+(\w+)\s*\(')
cls = re.compile(r'^\s*(?:public|internal|private)?\s*(?:static\s+|sealed\s+|abstract\s+|partial\s+)*(?:class|record|struct)\s+(\w+)')
for ln in sys.argv[2:]:
    n = int(ln)
    m = None
    c = None
    for i in range(n - 1, -1, -1):
        if m is None and meth.match(lines[i]) and not lines[i].strip().startswith('//'):
            m = (i + 1, meth.match(lines[i]).group(1))
        if cls.match(lines[i]):
            c = (i + 1, cls.match(lines[i]).group(1))
            break
    print(f"{path}:{n} -> method {m} class {c}")
