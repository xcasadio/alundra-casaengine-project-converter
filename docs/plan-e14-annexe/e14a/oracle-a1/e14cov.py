"""e14cov.py - instruction coverage of the live handlers and of the helpers they call, from the executed-PC sets of the runs (Machine.cov)."""
import os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, 'tools'))
import lib

FUNCS = [
    ('A0 (Muruta load)', 0x8006174C), ('A1 (Beannoide load)', 0x80061758),
    ('C1 (Beannoide tick)', 0x80065ED4), ('D1 (Beannoide touch)', 0x8007D9A4),
    ('C10 (Muruta tick)', 0x80068154), ('D9 (Muruta touch)', 0x8007DE04),
    ('E1 (destroy)', 0x8007ED10), ('DestroyEntity', 0x8003A59C),
    ('loot spawn-at-death 0x80032B90', 0x80032B90),
    ('relative position 0x8007FE20', 0x8007FE20), ('relative position (2nd entry) 0x8007FE8C', 0x8007FE8C),
    ('StartWalk 0x8007FEF8', 0x8007FEF8), ('HandleAnimationDirection 0x80080144', 0x80080144),
    ('TryAttackPlayer2 0x8008048C', 0x8008048C), ('TryAttackPlayerFront 0x800805C8', 0x800805C8),
    ('GetDirectionToTarget 0x8002E814', 0x8002E814), ('ComputeNewHp 0x8003A374', 0x8003A374),
]


def func_range(entry):
    """[entry, end): scan forward; end after the first `jr $ra` (and its delay slot) at or after the largest branch target seen so far"""
    ins = lib.dis(entry, 700)
    maxt = entry
    for k, i in enumerate(ins):
        mn, op = i.mnemonic, i.op_str
        if mn in ('beq', 'bne', 'beqz', 'bnez', 'blez', 'bgtz', 'bltz', 'bgez', 'b', 'bal', 'bltzal', 'bgezal', 'j'):
            try:
                t = int(op.split(',')[-1].strip(), 16)
            except ValueError:
                t = 0
            if mn == 'j':
                if t >= entry:
                    maxt = max(maxt, t)
            elif t > i.address:
                maxt = max(maxt, t)
        if mn == 'jr' and op.strip() == '$ra' and i.address >= maxt - 4:
            return entry, i.address + 8
    return entry, entry + 4 * len(ins)


def coverage(cov):
    out = []
    for name, entry in FUNCS:
        a, b = func_range(entry)
        words = list(range(a, b, 4))
        hit = [w for w in words if w in cov]
        miss = [w for w in words if w not in cov]
        # group the misses into ranges
        rng = []
        for w in miss:
            if rng and w == rng[-1][1] + 4:
                rng[-1][1] = w
            else:
                rng.append([w, w])
        out.append((name, a, b, len(hit), len(words), rng))
    return out


def text(cov):
    L = []
    for name, a, b, h, n, rng in coverage(cov):
        L.append('%-46s 0x%08X-0x%08X  %4d / %4d instructions executed (%5.1f%%)' % (name, a, b, h, n, 100.0 * h / n))
        if rng and h < n:
            L.append('      not executed: ' + ', '.join('0x%08X' % r[0] if r[0] == r[1] else '0x%08X-0x%08X' % (r[0], r[1]) for r in rng[:14]) + (' ...' if len(rng) > 14 else ''))
    return L
