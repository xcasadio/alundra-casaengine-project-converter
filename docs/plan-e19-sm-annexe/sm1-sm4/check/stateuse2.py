"""Independent audit: for every opcode handler of the table 0x80098FAC, which offsets of the state struct (a3 at entry) are written
or read, following register copies of a3 and calls that receive the state in a0..a3.  Flow-insensitive but conservative about
register overwrite (an alias is dropped when the register is written by something else)."""
import sys, re
import capstone, exelib
import dh

REG_ARG = ['a0', 'a1', 'a2', 'a3']
LOAD = {'lw': 4, 'lh': 2, 'lhu': 2, 'lb': 1, 'lbu': 1}
STORE = {'sw': 4, 'sh': 2, 'sb': 1}


def parse_mem(op):
    m = re.match(r'(\$\w+), (-?0x[0-9a-f]+|-?\d+)\((\$\w+)\)', op)
    if not m:
        m2 = re.match(r'(\$\w+), \((\$\w+)\)', op)
        if m2:
            return m2.group(1), 0, m2.group(2)
        return None
    return m.group(1), int(m.group(2), 0), m.group(3)


def dest_reg(i):
    mn = i.mnemonic
    ops = [x.strip() for x in i.op_str.split(',')]
    if mn in STORE or mn in ('beq', 'bne', 'beqz', 'bnez', 'blez', 'bgtz', 'bltz', 'bgez', 'j', 'jr', 'nop', 'mult', 'multu', 'div', 'divu'):
        return None
    if mn in ('jal', 'jalr'):
        return '$ra'
    return ops[0] if ops and ops[0].startswith('$') else None


def analyse(fn, aliases_in, depth, seen, out):
    key = (fn, tuple(sorted(aliases_in)))
    if key in seen or depth > 4:
        return
    seen.add(key)
    alias = set(aliases_in)
    for a in dh.reach(fn):
        i = dh.insn_at(a)
        mn = i.mnemonic
        if mn in STORE or mn in LOAD:
            p = parse_mem(i.op_str)
            if p and p[2] in alias:
                out.setdefault(('W' if mn in STORE else 'R', p[1]), []).append(a)
        if mn == 'move':
            ops = [x.strip() for x in i.op_str.split(',')]
            if ops[1] in alias:
                alias.add(ops[0])
                continue
        if mn in ('addu', 'or') and i.op_str.count('$zero') >= 1:
            ops = [x.strip() for x in i.op_str.split(',')]
            src = [x for x in ops[1:] if x != '$zero']
            if len(src) == 1 and src[0] in alias:
                alias.add(ops[0])
                continue
        if mn == 'jal':
            tgt = int(i.op_str, 16)
            ar = {'$' + r for r in REG_ARG}
            callee_alias = {'$a%d' % k for k in range(4) if ('$a%d' % k) in alias}
            if callee_alias:
                analyse(tgt, callee_alias, depth + 1, seen, out)
            # registers clobbered by a call
            for r in ('$v0', '$v1', '$a0', '$a1', '$a2', '$a3', '$t0', '$t1', '$t2', '$t3', '$t4', '$t5', '$t6', '$t7', '$t8', '$t9'):
                alias.discard(r)
            continue
        d = dest_reg(i)
        if d and d in alias:
            alias.discard(d)


if __name__ == '__main__':
    table = {}
    for op in range(0x100):
        table.setdefault(exelib.u32(0x80098FAC + 4 * op), []).append(op)
    only = [int(x, 16) for x in sys.argv[1:]]
    for h, ops in sorted(table.items(), key=lambda kv: kv[1][0]):
        if only and not (set(ops) & set(only)):
            continue
        if not (0x80020000 <= h < 0x800ca800):
            continue
        out = {}
        try:
            analyse(h, {'$a3'}, 0, set(), out)
        except Exception as e:
            print('%s %s ERR %s' % ([hex(o) for o in ops], hex(h), e))
            continue
        w = sorted({k[1] for k in out if k[0] == 'W'})
        r = sorted({k[1] for k in out if k[0] == 'R'})
        print('%s %s  W=%s R=%s' % (','.join('%02X' % o for o in ops), hex(h), [hex(x) for x in w], [hex(x) for x in r]))
