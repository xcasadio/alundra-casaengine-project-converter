import json, collections, struct
import mylib as m, mymips as mm, replay_mips as rp, gridlib
vecs=[json.loads(l) for l in open("../../sm3/vectors/sm3-vectors.jsonl",encoding="utf-8")]
steps=collections.Counter(); res=collections.Counter(); bad=0
for v in vecs:
    v0,r,mem=rp.run(v,gridlib.make_grid)
    res[(v['op'],r)]+=1
    if v0!=len(v['instr']): bad+=1
print("v0!=len(instr):",bad); print(sorted(res.items()))
# sensitivity: patch B5 handler bgez -> bltz (regimm rt 1->0) and the 9E 'andi 2' -> 'andi 4'
off=m.off(0x80041868)
w=struct.unpack_from("<I",m.DATA,off)[0]; print(hex(w))
d=bytearray(m.DATA); struct.pack_into("<I",d,off,(w & ~(0x1f<<16)) | (0<<16)); m.DATA=bytes(d)
mis=0
for v in vecs:
    if v['op']!='0xB5': continue
    v0,r,mem=rp.run(v,gridlib.make_grid)
    if r!=v['expect']['result']: mis+=1
print("B5 patched mismatches",mis)
