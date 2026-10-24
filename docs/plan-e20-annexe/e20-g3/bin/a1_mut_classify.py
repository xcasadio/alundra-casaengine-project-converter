"""a1_mut_classify.py - classifies the survivors of a1_mut.txt (read-only text processing).
Rules (each is an argument, not a proof by itself; the 'residual' list is what stays unexplained):
  equivalent-copy      : 'move x, y' mutated to subu/negu/zero-source whose result is the same register content or a dead one
  stack-frame          : addiu $sp / sw|lw ($sp) words (the frame of a leaf function: no observable effect on the return values)
  slt/sltu             : signed vs unsigned compare of values that are non-negative in every vector domain (all coordinates and extents are positive, deltas are tested >= 0 before) 
  third-list pointers  : lui/addiu/lw/sw of the 0x80134250 hit list (not read by the movement code)
  residual             : anything else"""
import re, collections
L = open('out/a1_mut.txt').read().splitlines()
print(L[0])
rows = []
for l in L[1:]:
    m = re.match(r'SURVIVOR (\w+)\s+(\S+)\s+(.*?)\s+->\s+(.*)$', l)
    a, name, orig, mut = m.groups()
    rows.append((int(a, 16), name, orig, mut))
cl = collections.defaultdict(list)
for a, name, orig, mut in rows:
    if name == 'addu/subu' and orig.startswith('move'):
        cl['equivalent-copy'].append((a, name, orig, mut))
    elif name == 'move->zero':
        cl['move->zero (value dead or equal in vectors)'].append((a, name, orig, mut))
    elif '$sp' in orig:
        cl['stack-frame'].append((a, name, orig, mut))
    elif name == 'slt/sltu':
        cl['slt/sltu'].append((a, name, orig, mut))
    elif 0x800385D0 <= a <= 0x80038614 or a in (0x800384FC, 0x80038500, 0x8003851C, 0x80038530):
        cl['third-list'].append((a, name, orig, mut))
    else:
        cl['residual'].append((a, name, orig, mut))
for k, v in cl.items():
    print('%-48s %d' % (k, len(v)))
print('--- residual:')
for a, name, orig, mut in cl['residual']:
    print('  %08X %-12s %-34s -> %s' % (a, name, orig, mut))
print('--- move->zero:')
for a, name, orig, mut in cl['move->zero (value dead or equal in vectors)']:
    print('  %08X %-34s -> %s' % (a, orig, mut))
