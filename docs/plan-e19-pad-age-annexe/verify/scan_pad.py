"""Independent scan: every memory access whose effective address lies in the pad block 0x80126F18..0x80126F40,
tracking lui/addiu/ori constants through registers (linear sweep, callee-saved kept across jal)."""
import sys, re
sys.path.insert(0, r"C:\Users\casad\AppData\Local\Temp\claude\D--development-repo-alundra-casaengine-project-converter\b00d1a72-420d-4acc-ab34-0c25fbdad3a1\scratchpad\e19j-disc")
import lib

REG = {n: i for i, n in enumerate(['zero','at','v0','v1','a0','a1','a2','a3','t0','t1','t2','t3','t4','t5','t6','t7','s0','s1','s2','s3','s4','s5','s6','s7','t8','t9','k0','k1','gp','sp','fp','ra'])}
REG['s8'] = 30
CALLEE = set(range(16, 24)) | {28, 29, 30}
LO, HI = 0x80126F18, 0x80126F48

def r(n):
    return REG.get(n.strip().lstrip('$'), -1)

ins = lib.all_ins()
const = {}
hits = []
# also: simple addr -> function start map (jal targets are function starts; use prologue 'addiu sp, sp, -N' heuristic)
funcstarts = []
for i in ins:
    if i.mnemonic == 'addiu' and i.op_str.startswith('$sp, $sp, -'):
        funcstarts.append(i.address)

def func_of(a):
    lo, hi = 0, len(funcstarts) - 1
    best = None
    while lo <= hi:
        m = (lo + hi) // 2
        if funcstarts[m] <= a:
            best = funcstarts[m]; lo = m + 1
        else:
            hi = m - 1
    return best

def ev(s):
    return int(s, 0)

for idx, i in enumerate(ins):
    m, o = i.mnemonic, i.op_str
    p = [x.strip() for x in o.split(',')] if o else []
    if m == 'lui' and len(p) == 2:
        const[r(p[0])] = (ev(p[1]) << 16) & 0xFFFFFFFF
        continue
    if m in ('addiu', 'addi') and len(p) == 3:
        d, s = r(p[0]), r(p[1])
        if s in const:
            const[d] = (const[s] + ev(p[2])) & 0xFFFFFFFF
        else:
            const.pop(d, None)
        continue
    if m == 'ori' and len(p) == 3:
        d, s = r(p[0]), r(p[1])
        if s in const:
            const[d] = const[s] | ev(p[2])
        else:
            const.pop(d, None)
        continue
    mm = re.match(r'^(lb|lbu|lh|lhu|lw|sb|sh|sw)$', m)
    if mm and len(p) == 2:
        mo = re.match(r'^(-?0x[0-9a-f]+|-?\d+)\((\$\w+)\)$', p[1])
        if mo:
            off = ev(mo.group(1)); base = r(mo.group(2))
            if base in const:
                ea = (const[base] + off) & 0xFFFFFFFF
                if LO <= ea < HI:
                    hits.append((i.address, m, p[0], ea, func_of(i.address)))
        if m.startswith('l'):
            const.pop(r(p[0]), None)
        continue
    if m == 'jal':
        for k in list(const):
            if k not in CALLEE:
                const.pop(k, None)
        continue
    # generic: destination register of other ops invalidates
    if m in ('jr', 'j', 'jalr', 'nop', 'beq', 'bne', 'beqz', 'bnez', 'bgez', 'bltz', 'blez', 'bgtz', 'bgezal', 'bltzal', 'break', 'syscall', 'mtc2', 'ctc2', 'swc2', 'lwc2'):
        continue
    if p:
        d = p[0]
        if d.startswith('$'):
            const.pop(r(d), None)

for h in hits:
    print("%08x %-4s %-4s ea=%08x func=%s" % (h[0], h[1], h[2], h[3], ('%08x' % h[4]) if h[4] else '?'))
print(len(hits), "hits")
