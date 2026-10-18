"""cdis.py (critic h4d2) - own listing tool: capstone MIPS32 over ALUN_CD.EXE (France).  Usage: python -B cdis.py START [END] [--fn]
Lists with branch/jump labels.  --fn: stop at the first `jr ra` (+ delay slot) after the farthest forward branch target."""
import sys, struct, re
from capstone import Cs, CS_ARCH_MIPS, CS_MODE_MIPS32, CS_MODE_LITTLE_ENDIAN

EXE = r"D:\development\repo\Alundra Remake\Alundra (France)\Alundra (France)_extracted\ALUN_CD.EXE"
DATA = open(EXE, "rb").read()
BASE, OFF = 0x80020000, 0x800
md = Cs(CS_ARCH_MIPS, CS_MODE_MIPS32 + CS_MODE_LITTLE_ENDIAN)
md.detail = False


def rw(a):
    o = a - BASE + OFF
    return struct.unpack_from("<I", DATA, o)[0]


def ins_at(a):
    o = a - BASE + OFF
    r = list(md.disasm(DATA[o:o + 4], a))
    return r[0] if r else None


def listing(start, end=None, fn=False):
    a = start
    far = start
    out = []
    targets = set()
    # first pass: collect branch targets
    b = start
    stop = end if end else start + 0x2000
    tmp = []
    while b < stop:
        i = ins_at(b)
        if i is None:
            tmp.append((b, '.word', '0x%08x' % rw(b)))
            b += 4
            continue
        tmp.append((b, i.mnemonic, i.op_str))
        m = i.mnemonic
        if m in ('beq', 'bne', 'beqz', 'bnez', 'blez', 'bgtz', 'bltz', 'bgez', 'b', 'bal', 'bc1t', 'bc1f', 'beql', 'bnel'):
            mm = re.search(r'(0x[0-9a-fA-F]+)$', i.op_str)
            if mm:
                t = int(mm.group(1), 16)
                targets.add(t)
                if t > far:
                    far = t
        elif m in ('j',):
            mm = re.search(r'(0x[0-9a-fA-F]+)$', i.op_str)
            if mm:
                t = int(mm.group(1), 16)
                targets.add(t)
                if start <= t < start + 0x4000 and t > far:
                    far = t
        if fn and m == 'jr' and 'ra' in i.op_str and b >= far:
            # include delay slot
            j = ins_at(b + 4)
            tmp.append((b + 4, j.mnemonic, j.op_str))
            break
        b += 4
    for (addr, m, o) in tmp:
        lab = 'L%08x:' % addr if addr in targets else ''
        out.append('%08x %-10s %-8s %s' % (addr, lab, m, o))
    return out


if __name__ == '__main__':
    s = int(sys.argv[1], 16)
    e = None
    fn = '--fn' in sys.argv
    args = [x for x in sys.argv[2:] if not x.startswith('--')]
    if args:
        e = int(args[0], 16)
    print('\n'.join(listing(s, e, fn)))
