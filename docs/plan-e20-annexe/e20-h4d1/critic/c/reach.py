"""reach.py - critic's own control-flow reachability scan over ALUN_CD.EXE (France).
From root address(es), follows branches, j, jal (callee roots) and RESOLVES jump tables (jr rX preceded by sltiu N + a table base formed
by lui/addiu in the previous 20 instructions); reports unresolved indirect jumps/calls.  Then lists every reachable instruction that
accesses the random word 0x80098708 (lui 0x800a + off -0x78f8, or addiu/lw/sw patterns).
Usage: python -B reach.py ROOT [ROOT ...]  (hex)
Read-only.
"""
import sys
import capstone
import exelib

md = capstone.Cs(capstone.CS_ARCH_MIPS, capstone.CS_MODE_MIPS32 + capstone.CS_MODE_LITTLE_ENDIAN)
cache = {}


def dec(a):
    if a not in cache:
        c = exelib.DATA[exelib.off(a):exelib.off(a) + 4]
        cache[a] = next(md.disasm(c, a), None)
    return cache[a]


def ops_of(ins):
    return [o.strip() for o in ins.op_str.split(',')] if ins.op_str else []


def resolve_table(a):
    """jr at address a: find sltiu bound and table base in the previous 24 instructions"""
    n = None
    base = None
    luis = {}
    for k in range(24, 0, -1):
        ins = dec(a - 4 * k)
        if ins is None:
            continue
        o = ops_of(ins)
        if ins.mnemonic == 'sltiu':
            n = int(o[2], 0)
        if ins.mnemonic == 'lui':
            luis[o[0]] = int(o[1], 0) << 16
        if ins.mnemonic == 'addiu' and o[1] in luis:
            base = (luis[o[1]] + int(o[2], 0)) & 0xFFFFFFFF
        if ins.mnemonic == 'ori' and o[1] in luis:
            base = luis[o[1]] | int(o[2], 0)
    if n is None or base is None:
        return None
    return [exelib.u32(base + 4 * i) for i in range(n)]


def reach(roots):
    seen = set()
    work = list(roots)
    callees = set(roots)
    unresolved = []
    tables = []
    while work:
        a = work.pop()
        while True:
            if a in seen:
                break
            ins = dec(a)
            if ins is None:
                break
            seen.add(a)
            m = ins.mnemonic
            o = ops_of(ins)
            if m in ('beq', 'bne', 'beqz', 'bnez', 'blez', 'bgtz', 'bltz', 'bgez', 'bltzal', 'bgezal', 'b'):
                tgt = int(o[-1], 0)
                work.append(tgt)
                # delay slot
                seen.add(a + 4)
                if m == 'b':
                    break
                a += 8
                continue
            if m == 'j':
                work.append(int(o[0], 0)); seen.add(a + 4); break
            if m == 'jal':
                t = int(o[0], 0)
                if t not in callees:
                    callees.add(t); work.append(t)
                seen.add(a + 4)
                a += 8
                continue
            if m == 'jalr':
                unresolved.append((a, 'jalr ' + ins.op_str)); seen.add(a + 4); a += 8; continue
            if m == 'jr':
                seen.add(a + 4)
                if o[0] == '$ra':
                    break
                tb = resolve_table(a)
                if tb is None:
                    unresolved.append((a, 'jr ' + ins.op_str))
                else:
                    tables.append((a, len(tb)))
                    for t in tb:
                        if 0x80020000 <= t < 0x800CA800:
                            work.append(t)
                break
            a += 4
    return seen, callees, unresolved, tables


def seed_refs(addrs):
    """instructions in addrs that form/access 0x80098708 within the same basic stretch (lui tracking is reset at each prologue only)"""
    out = []
    for a in sorted(addrs):
        ins = dec(a)
        if ins is None:
            continue
        o = ops_of(ins)
        m = ins.mnemonic
        if m in ('lw', 'sw') and '(' in o[1]:
            off, rest = o[1].split('(')
            if off.strip() in ('-0x78f8', '0x8708'):
                # confirm base is formed from lui 0x800a nearby
                for k in range(1, 40):
                    p = dec(a - 4 * k)
                    if p is not None and p.mnemonic == 'lui' and ops_of(p)[0] == rest.rstrip(')') and int(ops_of(p)[1], 0) in (0x800a, 0x8009):
                        out.append((a, ins.mnemonic + ' ' + ins.op_str))
                        break
                    if p is not None and p.mnemonic == 'lui' and ops_of(p)[0] == rest.rstrip(')'):
                        break
    return out


if __name__ == '__main__':
    roots = [int(x, 16) for x in sys.argv[1:]]
    seen, callees, unresolved, tables = reach(roots)
    print('reachable instructions:', len(seen), ' functions:', len(callees))
    print('jump tables resolved:', len(tables), ' unresolved indirect:', len(unresolved))
    for u in unresolved[:40]:
        print('   unresolved %#010x %s' % u)
    refs = seed_refs(seen)
    print('reachable accesses to 0x80098708:', len(refs))
    for r in refs:
        print('   %#010x %s' % r)
