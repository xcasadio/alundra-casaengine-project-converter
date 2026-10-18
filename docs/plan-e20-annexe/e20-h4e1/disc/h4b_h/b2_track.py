"""b2_track.py - for each guard read of [0x80134360], follow the register (and its 'move' copies) to the first slt/slti that compares it with another register, within 150 instructions (same function)."""
import re, tools, funcs
sites = []
for l in open('../out/highest_slot_readers.txt'):
    if l.startswith('8') and 'guard' in l:
        sites.append(int(l.split()[0], 16))
res = []
for s in sites:
    i = tools.ins_at(s)
    reg = i.op_str.split(',')[0].strip()
    tracked = {reg}
    fn_end = None
    found = None
    for k in range(1, 160):
        a = s + 4 * k
        j = tools.ins_at(a)
        if j is None: break
        ops = [x.strip() for x in j.op_str.split(',')]
        if j.mnemonic == 'move' and ops[1] in tracked:
            tracked.add(ops[0])
        elif j.mnemonic in ('addu', 'or') and len(ops) == 3 and ops[1] in tracked and ops[2] == '$zero':
            tracked.add(ops[0])
        elif j.mnemonic == 'slt' and len(ops) == 3 and (ops[1] in tracked or ops[2] in tracked):
            found = (hex(a), 'count first (count < idx -> INCLUSIVE loop)' if ops[1] in tracked else 'idx first (idx < count -> EXCLUSIVE)', tools.text(a).split(':', 1)[1].strip())
            break
        elif j.mnemonic == 'slti' and ops[1] in tracked:
            found = (hex(a), 'count < const', tools.text(a).split(':', 1)[1].strip()); break
        elif ops and ops[0] in tracked and j.mnemonic in ('lw', 'lui', 'addiu', 'li', 'lbu', 'lhu', 'lh', 'lb', 'sll', 'subu', 'and', 'andi') and ops[0] != reg:
            tracked.discard(ops[0])
        elif ops and ops[0] == reg and j.mnemonic in ('lw', 'lui', 'addiu', 'lbu', 'sll', 'subu', 'addu', 'andi', 'slt', 'sltiu') and j.mnemonic != 'slt':
            if reg in tracked and len(tracked) == 1:
                break                      # the only copy is overwritten
            tracked.discard(reg)
        if tools.w32(a) == 0x03E00008:
            break
    res.append((s, funcs.func_of(s), found))
for s, f, found in res:
    print('%08X fn %08X  %s' % (s, f, found if found else 'NOT FOUND (tracked register overwritten / reloaded)'))
