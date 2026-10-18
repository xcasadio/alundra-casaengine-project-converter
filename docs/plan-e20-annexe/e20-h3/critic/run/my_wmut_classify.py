import re, sys
cur = None; rows = []
for line in open('../results/my_wmut_all.txt'):
    m = re.match(r'^(\w+)\s+mutants', line)
    if m: cur = m.group(1); continue
    m = re.match(r'\s+SURV (\w+) (\S+)\s+(.*?)\s+\[(.*)\]$', line)
    if m: rows.append((cur, m.group(1), m.group(2), m.group(3), m.group(4)))
def cat(fn, a, tag, enc, ins):
    mn = ins.split()[0]
    if re.match(r'addiu \$sp, \$sp, -?0x', ins): return 'stack-frame size'
    if mn in ('sw', 'lw') and '($sp)' in ins and re.search(r'\$(s\d|ra|fp)', ins): return 'callee-saved save/restore (harness restores registers)'
    if tag == 'alu-swap' and mn in ('move',): return 'move via addu/subu with $zero (equivalent)'
    if mn in ('sw', 'lw') and '($sp)' in ins: return 'stack scratch'
    return None
left = []
cnt = {}
for r in rows:
    c = cat(*[r[0], r[1], r[2], r[3], r[4]])
    if c: cnt[c] = cnt.get(c, 0) + 1
    else: left.append(r)
print('survivors total', len(rows), ' classified equivalent:', cnt)
print('to inspect', len(left))
for r in left: print('  %-12s %s %-8s %s  [%s]' % r)
