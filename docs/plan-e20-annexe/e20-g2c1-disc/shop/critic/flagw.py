import sys, collections
sys.path.insert(0, 'D:/development/repo/alundra-casaengine-project-converter/docs/plan-e20-annexe/e20-g3/dll')
import decode_events as de
# persistent flag writes (id < 0x8000) in the shop B programs of each map
SHOP={163:range(2,5),258:range(2,6),298:range(2,6),398:range(1,8)}
for m,rng in SHOP.items():
    doc=de.load_map(m)
    for slot,i,s,e in de.all_programs(doc):
        if slot!='B' or i not in rng: continue
        w=[]
        for pc,op,v,bad in de.sweep(doc,s,e):
            if v is None: break
            if op in (0x05,0x06,0x32):
                fid=v[1]|v[2]<<8
                w.append(('on' if op==5 else 'off' if op==6 else 'tog', fid, hex(fid)))
        pers=[x for x in w if x[1]<0x8000]
        print(m,'B[%d]'%i,'persistent flag writes:',pers, ' local:',sorted({x[1] for x in w if x[1]>=0x8000}))
