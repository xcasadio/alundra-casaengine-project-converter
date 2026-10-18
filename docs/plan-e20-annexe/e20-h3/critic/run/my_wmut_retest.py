"""my_wmut_retest.py - re-run the survivors of my_wmut_all_120.txt with many more vectors (a mutant detected with probability ~3 % per vector needs a few hundred)."""
import re, sys, struct
src = open('my_wmut.py').read().replace("if __name__ == '__main__':", "if False:")
ns = {}
exec(compile(src, 'wm', 'exec'), ns)
import my_diff as D
rows = []
cur = None
for line in open('../results/my_wmut_all_120.txt'):
    m = re.match(r'^(\w+)\s+mutants', line)
    if m: cur = m.group(1); continue
    m = re.match(r'\s+SURV (\w+) (\S+)\s+(.*?)\s+\[(.*)\]$', line)
    if m: rows.append((cur, int(m.group(1), 16), m.group(2)))
nvec = int(sys.argv[1]) if len(sys.argv) > 1 else 800
byfn = {}
for fn, a, tag in rows: byfn.setdefault(fn, set()).add(a)
import io, contextlib
for fn, addrs in byfn.items():
    for a in sorted(addrs):
        buf = io.StringIO()
        with contextlib.redirect_stdout(buf):
            s2 = src.replace("for a in range(addr, end, 4):", "for a in range(%d, %d, 4):" % (a, a + 4))
            ns2 = {}
            exec(compile(s2, 'wm', 'exec'), ns2)
            ns2['run'](fn, nvec, 77)
        print(fn, buf.getvalue().strip(), flush=True)
