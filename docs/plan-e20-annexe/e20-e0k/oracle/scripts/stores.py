"""stores.py - linear scan of ALUN_CD.EXE text: stores (sw/sh/sb) and loads whose effective entity offset (immediate + the bias of the base register when it was set by
`addiu rX, rY, bias` earlier in the same straight-line run) equals one of the fields of interest.  Heuristic (no control flow): a bias is forgotten at any branch target/jump/call return.
usage: python -B -I stores.py 0x13C 0x28 ..."""
import sys, os, re
HERE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, HERE)
import exe

want = [int(a, 0) for a in sys.argv[1:]]
lines = open(os.path.join(HERE, 'out', 'dis_all.txt')).read().split('\n')
pat = re.compile(r'^([0-9A-F]{8}): ([0-9A-F]{8})  (\w+)\s*(.*)$')
bias = {}
res = {w: [] for w in want}
def reg(s): return s.strip()
for ln in lines:
    m = pat.match(ln)
    if not m:
        continue
    addr = int(m.group(1), 16); mn = m.group(3); ops = m.group(4).split(';')[0].strip()
    parts = [p.strip() for p in ops.split(',')] if ops else []
    if mn in ('jr', 'jal', 'jalr', 'j', 'beq', 'bne', 'beqz', 'bnez', 'bgez', 'bltz', 'blez', 'bgtz', 'bgezal', 'bltzal'):
        # keep biases across a branch: the delay slot executes; after a jal the callee clobbers t/a regs but not s regs: keep s regs only
        if mn in ('jal', 'jalr', 'jr'):
            bias = {r: b for r, b in bias.items() if r.startswith('$s') or r in ('$gp', '$sp', '$fp')}
        continue
    if mn in ('addiu', 'addi') and len(parts) == 3:
        rd, rs, imm = parts
        try:
            iv = int(imm, 0)
        except ValueError:
            continue
        if rs in bias and rd != rs:
            bias[rd] = bias[rs] + iv
        elif rs == rd:
            bias[rd] = bias.get(rs, 0) + iv if rs in bias else iv if abs(iv) >= 0x100 and False else bias.get(rs, 0) + iv if rs in bias else None
            if bias[rd] is None:
                del bias[rd]
        else:
            if 0x100 <= iv < 0x400:
                bias[rd] = iv
            elif rd in bias:
                del bias[rd]
        continue
    if mn in ('sw', 'sh', 'sb', 'lw', 'lh', 'lhu', 'lb', 'lbu') and len(parts) == 2 and '(' in parts[1]:
        disp, base = parts[1].split('(')
        base = base.rstrip(')')
        try:
            d = int(disp, 0) if disp else 0
        except ValueError:
            continue
        for off in want:
            eff = [d]
            if base in bias:
                eff.append(d + bias[base])
            if off in eff and 0 <= off < 0x294:
                kind = 'W' if mn.startswith('s') else 'R'
                res[off].append((addr, kind, mn, parts[0], parts[1], 'biased' if (base in bias and d + bias[base] == off and d != off) else 'direct'))
        if mn.startswith('l'):
            rt = parts[0]
            bias.pop(rt, None)
        continue
    # any other write to a register kills its bias
    if parts and parts[0].startswith('$') and mn not in ('sw', 'sh', 'sb', 'mult', 'multu', 'div', 'divu', 'mtc2', 'ctc2'):
        bias.pop(parts[0], None)
for off in want:
    print('== offset 0x%X: %d sites' % (off, len(res[off])))
    for a, k, mn, rt, mem, how in res[off]:
        if k == 'W' or '-v' in os.environ.get('K0V', '-v'):
            print('  %08X %s %s %s, %s [%s]' % (a, k, mn, rt, mem, how))
