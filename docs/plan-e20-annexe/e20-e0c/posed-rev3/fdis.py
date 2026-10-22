"""fdis.py - disassemble whole functions of ALUN_CD.EXE (read-only). Usage: python -B fdis.py ADDR [ADDR...]
Stops after the first `jr $ra` + delay slot that is followed by a new prologue (addiu $sp,$sp,-N) or by another jr $ra / nop padding,
capped at 600 instructions."""
import sys
import capstone
import exelib

md = capstone.Cs(capstone.CS_ARCH_MIPS, capstone.CS_MODE_MIPS32 + capstone.CS_MODE_LITTLE_ENDIAN)


def func(start, cap=600):
    code = exelib.DATA[exelib.off(start):exelib.off(start) + cap * 4]
    ins = list(md.disasm(code, start))
    out = []
    for i, x in enumerate(ins):
        out.append(x)
        if x.mnemonic == 'jr' and x.op_str == '$ra':
            # delay slot + look ahead
            if i + 1 < len(ins):
                out.append(ins[i + 1])
            nxt = ins[i + 2] if i + 2 < len(ins) else None
            if nxt is None or (nxt.mnemonic == 'addiu' and nxt.op_str.startswith('$sp, $sp, -')) or nxt.mnemonic == 'nop' or (nxt.mnemonic == 'jr'):
                break
            # a later return inside the same function is possible: continue only if some branch targets beyond nxt
            targets = []
            for y in out:
                if y.mnemonic in ('beq', 'bne', 'beqz', 'bnez', 'bgez', 'bltz', 'bgtz', 'blez', 'j'):
                    try:
                        targets.append(int(y.op_str.split(',')[-1].strip(), 16))
                    except Exception:
                        pass
            if not any(t >= nxt.address for t in targets):
                break
    return out


if __name__ == '__main__':
    for a in sys.argv[1:]:
        s = int(a, 16)
        print(f'=== function {s:#010x}')
        for x in func(s):
            print(f'{x.address:#010x}  {x.mnemonic:8s} {x.op_str}')
