"""Critic: compare MY capstone listing of the four handlers with the preparation's, instruction by instruction (address, mnemonic, operands)."""
import re

BASE = "C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/sm-prep/"
P = BASE + "sm1/binary/handlers-sm1.txt"
O = BASE + "sm1-critic/binary/my-handlers.txt"


def norm_theirs(path):
    out = {}
    for l in open(path, encoding='utf-8'):
        m = re.match(r'\s*0x([0-9a-fA-F]{8})\s+(\S+)\s*(.*)$', l)
        if m:
            out[int(m.group(1), 16)] = (m.group(2), re.sub(r'\s+', '', m.group(3)))
    return out


def norm_mine(path):
    out = {}
    for l in open(path, encoding='utf-8'):
        m = re.match(r'\s*([0-9a-fA-F]{8})\s+[0-9a-fA-F]{8}\s+(\S+)\s*(.*)$', l)
        if m:
            out[int(m.group(1), 16)] = (m.group(2), re.sub(r'\s+', '', m.group(3)))
    return out


a = norm_theirs(P)
b = norm_mine(O)
print('theirs', len(a), 'mine', len(b))
only_a = sorted(set(a) - set(b))
only_b = sorted(set(b) - set(a))
print('addresses only in theirs', [hex(x) for x in only_a][:10])
print('addresses only in mine', [hex(x) for x in only_b][:10])
diff = []
for k in sorted(set(a) & set(b)):
    x, y = a[k], b[k]
    # capstone prints 'move $t0, $zero' the same way; allow the formatting of immediates 0x / negative
    if x != y:
        diff.append((hex(k), x, y))
print('instruction differences', len(diff))
for d in diff[:30]:
    print(d)
