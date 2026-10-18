"""retest the non-dispatch survivors with the FINAL generator (slot 64, no-wall mode, 5th corner pointer, stack garbage)"""
import re, io, contextlib
src = open('my_wmut.py').read().replace("if __name__ == '__main__':", "if False:")
cur = None; rows = []
for line in open('../results/my_wmut_retest.txt'):
    m = re.match(r'^(\w+) \w+\s+mutants', line)
    if m: cur = m.group(1); continue
    m = re.match(r'\s+SURV (\w+) (\S+)\s+(.*?)\s+\[(.*)\]$', line)
    if m: rows.append((cur, int(m.group(1), 16), m.group(2), m.group(4)))
def skip(ins, tag):
    mn = ins.split()[0]
    if re.match(r'addiu \$sp, \$sp, -?0x', ins): return True
    if mn in ('sw', 'lw') and '($sp)' in ins: return True
    if tag == 'alu-swap' and mn == 'move': return True
    return False
seen = set()
for fn, a, tag, ins in rows:
    if fn == 'dispatch' or skip(ins, tag) or (fn, a) in seen: continue
    seen.add((fn, a))
    buf = io.StringIO()
    with contextlib.redirect_stdout(buf):
        ns = {}
        exec(compile(src.replace("for a in range(addr, end, 4):", "for a in range(%d, %d, 4):" % (a, a + 4)), 'wm', 'exec'), ns)
        ns['run'](fn, 700, 4242)
    print(fn, buf.getvalue().strip().replace('\n', ' | '), flush=True)
