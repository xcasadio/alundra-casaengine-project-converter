"""b2_readers.py - every access to [0x80134360] in the text of ALUN_CD.EXE (displacement 0x4360 from any base register, base 0x8013xxxx) with the compare that uses it."""
import re, collections, funcs, handler_map, tools
hm = handler_map.tables()
inv = {}
for k, (b, c) in hm.items():
    for code, p in c.items():
        inv.setdefault(p, []).append('%s%d' % (k, code))
KNOWN = {0x8002eeac: 'hero interact search (6 callers in MovePlayer)', 0x80034320: 'item start helper (caller 0x80034A0C)', 0x80034bdc: 'cancel earth2 0x2C', 0x80034c54: 'cancel water 0x2D/0x2E',
         0x80034d2c: 'cancel fire1 0x2F', 0x80034e08: 'cancel fire2 0x30', 0x80034ec4: 'cancel wind2 0x32', 0x80035790: 'pilot water 0x2D/0x2E (phase 2 scan)', 0x800384f4: 'lists pass',
         0x80038634: 'slot pass', 0x800386d0: 'events/programs pass', 0x80038998: 'counters pass', 0x80038e84: 'effects pass', 0x80039d04: 'birth (raise)', 0x8003a648: 'helper (callers 0x8003A6B0, 0x8007F264)',
         0x8003ad30: 'helper (callers 0x8007C654, 0x8007FD44)', 0x8003adac: 'helper (caller 0x8007C738)', 0x8003af70: 'TARGET QUERY', 0x8003b0e8: 'helper (caller 0x8002DF80)', 0x8003b24c: 'map-load reset',
         0x8003b51c: 'helper (caller 0x8002BE38)', 0x8003c954: 'entity search by type, 49 callers (script opcodes, C63/C-handlers)', 0x8003da70: 'script VM opcode function', 0x80064d90: 'C98', 0x8007f420: 'E17'}
rows = []
for l in open('../out/scan_off_4360.txt'):
    m = re.match(r"fn~(\w+) (\w+): \w+\s+(\w+) (.*)", l)
    a = int(m.group(2), 16)
    if a >= 0x800CA800:
        continue
    mn = m.group(3)
    rows.append((a, mn, m.group(4)))
rows.sort()
out = []
for a, mn, ops in rows:
    f = funcs.func_of(a)
    if mn == 'sw':
        use = 'WRITE'
        reg = ops.split(',')[0].strip()
    else:
        reg = ops.split(',')[0].strip()
        use = '?'
        for k in range(1, 14):
            j = tools.ins_at(a + 4 * k)
            o = [x.strip() for x in j.op_str.split(',')]
            if j.mnemonic in ('blez', 'bltz', 'bgtz', 'bgez') and o[0] == reg:
                use = {'blez': 'guard: skip when count<=0 (loop starts at slot 1)', 'bltz': 'guard: skip when count<0 (loop starts at slot 0)'}.get(j.mnemonic, j.mnemonic); break
            if j.mnemonic == 'slt' and reg in o[1:]:
                if o[1] == reg:
                    use = 'loop end: continue while idx <= count (INCLUSIVE)' if True else ''
                else:
                    use = 'loop: idx < count (EXCLUSIVE)'
                break
    out.append((a, f, mn, use))
w = 0
for a, f, mn, use in out:
    nm = ','.join(inv.get(f, [])) or KNOWN.get(f, '')
    print('%08X %s fn %08X %-28s %s' % (a, mn, f, nm[:28], use))
cnt = collections.Counter(u.split(':')[0].split('(')[0].strip() for _, _, mn, u in out)
print(cnt)
