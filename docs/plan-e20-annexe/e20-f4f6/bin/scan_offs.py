import sys, re
sys.path.insert(0, 'src')
import lib
ins = lib.all_ins()
offs = [int(x, 16) for x in sys.argv[1:]]
pat = re.compile(r'^\$(\w+), (-?0x[0-9a-f]+|-?\d+)\(\$(\w+)\)$')
res = {}
for i in ins:
    if i.mnemonic in ('sw','sh','sb','lw','lh','lhu','lb','lbu'):
        m = pat.match(i.op_str)
        if m:
            d = int(m.group(2), 0)
            if d in offs:
                res.setdefault(d, []).append((i.address, i.mnemonic, i.op_str))
for d in offs:
    print('== offset 0x%X: %d sites' % (d, len(res.get(d, []))))
    for a, mn, op in res.get(d, []):
        print('  %08X %s %s' % (a, mn, op))
