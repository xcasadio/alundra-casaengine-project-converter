import sys, glob, os, re, json
sys.path.insert(0, "../binary")
from m import Map, SZ, MAPS
sys.stdout.reconfigure(encoding="utf-8")
def all_maps():
    out=[]
    for f in glob.glob(MAPS+"/*/*/events/*.events.json"):
        base=os.path.basename(os.path.dirname(os.path.dirname(f)))
        mm=re.search(r"-(\d+)$",base)
        if mm: out.append(int(mm.group(1)))
    return sorted(set(out))
tot=0; maps=0
for mid in all_maps():
    mp=Map(mid)
    C=mp.C
    boundary=sum(2*len(t) for t in mp.T.values())
    pc=boundary; n=0
    while pc<len(C):
        op=C[pc]
        if op==0x24: n+=1
        pc+=SZ.get(op) or 1
    if n: maps+=1; tot+=n
print(tot,maps,len(all_maps()))
