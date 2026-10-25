import sys, struct
exec(open('bx.py').read().split("if __name__")[0])
for arg in sys.argv[1:]:
    tgt=int(arg,16)
    jal = 0x0C000000 | ((tgt & 0x0FFFFFFF) >> 2)
    j = 0x08000000 | ((tgt & 0x0FFFFFFF) >> 2)
    res=[]
    for i in range(0, len(data)-3, 4):
        w = struct.unpack_from('<I', data, i)[0]
        a = 0x80020000 + i - 0x800
        if w==jal: res.append(("jal",hex(a)))
        elif w==j: res.append(("j",hex(a)))
        elif w==tgt: res.append(("ptr",hex(a)))
    print(arg, res)
