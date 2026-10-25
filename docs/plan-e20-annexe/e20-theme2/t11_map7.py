import sys, json, re
P = r'D:/development/repo/alundra-casaengine-project-converter/'
sizes = {}
for m in re.finditer(r'\{\s*0x([0-9A-Fa-f]{2}),\s*new\((\d+),\s*"([^"]*)"\)', open(P + 'Alundra/Events/EventOpcodeSizeTable.cs', encoding='utf-8').read()):
    sizes[int(m.group(1), 16)] = (int(m.group(2)), m.group(3))
mid = int(sys.argv[1]); focus_op = int(sys.argv[2], 0); focus_arg = int(sys.argv[3], 0)
d = json.load(open(P + 'data-extracted/data/map_%d.json' % mid, encoding='utf-8'))
ec = d['SpriteInfo']['EventCodes']
codes = ec['Codes']
starts = set()
tabname = {}
for k in ('EventCodesATable', 'EventCodesBTable', 'EventCodesCTable', 'EventCodesDTable', 'EventCodesETable', 'EventCodesFTable'):
    for i, o in enumerate(ec[k]):
        if 0 < o < len(codes):
            starts.add(o); tabname.setdefault(o, []).append('%s[%d]' % (k[10], i))
starts = sorted(starts)
for i, s in enumerate(starts):
    end = starts[i + 1] if i + 1 < len(starts) else len(codes)
    pc = s; prog = []
    while pc < end:
        op = codes[pc]; sz = sizes.get(op, (0, '?'))[0]
        if sz <= 0: break
        prog.append((pc, op, codes[pc + 1: pc + sz])); pc += sz
    if any(op == focus_op and args and args[0] == focus_arg for _, op, args in prog):
        print('program', tabname.get(s), 'start', s, 'len', len(prog))
        for pc, op, args in prog:
            print('  %4d %02X %-40s %s' % (pc, op, sizes[op][1][:40], list(args)))
