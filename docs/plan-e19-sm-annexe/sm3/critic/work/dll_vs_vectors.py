import json, collections
import models as md, gridlib
vecs=[json.loads(l) for l in open("../../sm3/vectors/sm3-vectors.jsonl",encoding="utf-8")]
bad=[]; stale_ok=0; freeslot=0
for v in vecs:
    ins=[int(x,16) for x in v['instr']]
    w=dict(n_last=v['n_last'],slots=v['slots'],ref=v['ref'])
    if ins[0]==0x9E:
        g=gridlib.make_grid(v); r=md.nine_e(ins,g)
    else:
        r=md.handlers(md.dll_search,w,v['ref'],ins)
    if r!=v['expect']['result']: bad.append(v['id'])
    if v['expect']['result']==v['result_before']: stale_ok+=1
    if any(s['status']==0 for s in v['slots']): freeslot+=1
print("DLL-shape model vs expected: mismatches",len(bad),bad[:10]); print("vectors whose expected == result_before:",stale_ok,"; vectors with a free slot:",freeslot)
