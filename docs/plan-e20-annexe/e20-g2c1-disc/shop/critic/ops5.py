import sys
sys.path.insert(0, 'D:/development/repo/alundra-casaengine-project-converter/docs/plan-e20-annexe/e20-g3/dll')
import decode_events as de
OPS={0x82:'give',0x83:'count>=',0x84:'use',0x97:'spend',0x98:'addmoney',0x99:'tryspend',0x9A:'enough'}
for m in (134,163,258,298,398):
    doc=de.load_map(m); seen=set()
    for slot,i,s,e in de.all_programs(doc):
        for pc,op,v,bad in de.sweep(doc,s,e):
            if v is None: break
            if op in OPS: print(m,'%s[%d]@%d'%(slot,i,pc),OPS[op],list(v[1:]))
