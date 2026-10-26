"""scan_opcodes_362.py - every opcode used by the programs of map 362 against the `case 0x..` labels of EventProgramRunner.Dispatch at HEAD (head/EventProgramRunner.cs, `git show HEAD:`)."""
import re, json, sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, 'h3critic'))
from my_maps import SIZES, DATA, programs, decode
src = open(os.path.join(HERE, 'head', 'EventProgramRunner.cs'), encoding='utf-8').read()
cases = {int(m.group(1), 16) for m in re.finditer(r'case 0x([0-9A-Fa-f]{2}):', src)}
d = json.load(open(DATA + 'map_362.json', encoding='utf-8-sig'))
codes, tabs, offs = programs(d)
used = {}
for nm, t in tabs.items():
    for pi, off in enumerate(t):
        if off <= 0 or off >= len(codes): continue
        nxt = [o for o in offs if o > off]; end = nxt[0] if nxt else len(codes)
        for (p, op, raw, name) in decode(codes, off, end):
            if raw is None: break
            used.setdefault(op, []).append((nm, pi, p))
missing = sorted(op for op in used if op not in cases and op not in (0x00, 0xFF))
print('opcodes used by the programs of map 362:', len(used), sorted('0x%02X' % o for o in used))
print('opcodes with no case in Dispatch at HEAD (0x00 Break and 0xFF End excluded):', ['0x%02X' % o for o in missing])
