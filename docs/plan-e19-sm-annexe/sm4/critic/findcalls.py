import sys
sys.path.insert(0,'.')
import mydis as m
target=sys.argv[1].lower()
N=(len(m.D)-m.HDR)//4
for k in range(0,N,4096):
    for i in m.dis(m.BASE+4*k,min(4096,N-k)):
        if i.mnemonic in ('jal','j') and i.op_str.lower()==('0x'+target.replace('0x','')):
            print("%08x %s %s"%(i.address,i.mnemonic,i.op_str))
