import re
from mipsdis import dis
RANGES = {
 'A0': (0x8006174C, 0x80061760),
 'C60': (0x8007A8A0, 0x8007A958),
 'C61': (0x8007A958, 0x8007A978),
 'C94': (0x8007C0D8, 0x8007C1E0),
 'E1': (0x8007ED10, 0x8007ED50),
 'E9': (0x8007EF50, 0x8007F240),
 'E10': (0x8007F23C, 0x8007F27C),
 'E17': (0x8007F420, 0x8007F660),
}
for n,(a,b) in RANGES.items():
    hits=[]
    cnt=0
    for i in dis(a,b):
        cnt+=1
        m = re.search(r'(-?0x[0-9a-f]+)\(\$', i.op_str)
        if m and i.mnemonic in ('lw','lh','lhu','lb','lbu','sw','sh','sb'):
            o = int(m.group(1),16)
            if 0x278 <= o <= 0x28F:
                hits.append('%08X %s %s' % (i.address, i.mnemonic, i.op_str))
    print(n, 'instrs', cnt, 'accesses +0x278..0x28F:', hits)
