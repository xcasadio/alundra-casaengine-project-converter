import mydis as m
N=(len(m.D)-m.HDR)//4
hits=[]
for k in range(0,N,2048):
    a=m.BASE+4*k
    for i in m.dis(a,2048):
        if ('-0x7054' in i.op_str or '0x8fac' in i.op_str): hits.append(i)
for i in hits: print("%08x %s %s"%(i.address,i.mnemonic,i.op_str))
