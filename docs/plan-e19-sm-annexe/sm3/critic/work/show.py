import sys, re
import mycorpus as c
names={}
for mt in re.finditer(r'\{\s*0x([0-9A-Fa-f]{2}),\s*new\((\d+),\s*"([^"]*)"\)\s*\}', c.src):
    names[int(mt.group(1),16)]=mt.group(3)
def show(mid, slot, prog, lo=None, hi=None):
    b,d=c.load(mid); codes=d['Codes']
    e=d['EventCodes%sTable'%slot][prog]
    print("map",mid,b,slot,prog,"entry",e)
    for pc,op,ops in c.decode_from(codes,e):
        if lo is not None and pc<lo: continue
        if hi is not None and pc>hi: break
        print("  %5d %02X %-34s %s"%(pc,op,names.get(op,'?'),' '.join('%02X'%x for x in ops)))
if __name__=='__main__':
    show(int(sys.argv[1]),sys.argv[2],int(sys.argv[3]),int(sys.argv[4]) if len(sys.argv)>4 else None,int(sys.argv[5]) if len(sys.argv)>5 else None)
