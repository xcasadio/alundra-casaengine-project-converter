import sys, csv
sys.path.insert(0,'tools')
from myemu import *
P="C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/sm-prep/sm2/vectors/vectors-sm2.tsv"
rows=list(csv.DictReader(open(P),delimiter='\t'))
print(len(rows))
def h(x): return int(x,16)
bad=0; nread_unexpected=0
red_today={}  # compute "red today": state after skipping by size  == Result unchanged, params unchanged
for r in rows:
    op=int(r['op'],16)
    instr=bytes(int(b,16) for b in r['instr'].split())
    S_words={0xC:h(r['memo_p2']),0x10:h(r['memo_p3']),0x14:h(r['memo_p4']),0x2C:h(r['result_in']),
             0x0:0xA0A0A0A0,0x8:0xB1B1B1B1,0x18:0xC2C2C2C2,0x30:0xD3D3D3D3,0x34:0xE4E4E4E4}
    v0,mem,S,E,O=call_handler(op,(h(r['pos_x']),h(r['pos_y']),h(r['pos_z'])),(0x0DEC0114,0x0DEC0118,0x0DEC011C),S_words,instr)
    out={'res':mem.lw(S+0x2C),'p2':mem.lw(S+0xC),'p3':mem.lw(S+0x10),'p4':mem.lw(S+0x14)}
    exp={'res':h(r['result_out']),'p2':h(r['memo_p2_out']),'p3':h(r['memo_p3_out']),'p4':h(r['memo_p4_out'])}
    if out!=exp or v0!=int(r['v0']):
        bad+=1
        if bad<10: print("MISMATCH",r['id'],out,exp,v0,r['v0'])
    # independent closed form
    pos=(h(r['pos_x']),h(r['pos_y']),h(r['pos_z'])); memo=(h(r['memo_p2']),h(r['memo_p3']),h(r['memo_p4']))
    if op==0x6A:
        cf={'res':h(r['result_in']),'p2':pos[0],'p3':pos[1],'p4':pos[2]}
    else:
        k=op-0x6B
        d=s32(memo[k]-pos[k])
        if d<0: d=s32(-d)
        thr=s32(((instr[1]|(instr[2]<<8))<<16))
        cf={'res':0 if d<thr else 1,'p2':memo[0],'p3':memo[1],'p4':memo[2]}
    if cf!=exp:
        bad+=1; print("CF MISMATCH",r['id'],cf,exp)
    # red today = (Result_out != Result_in) or memo changed
    changed = (exp['res']!=h(r['result_in'])) or (exp['p2'],exp['p3'],exp['p4'])!=memo
    red_today[r['op']]=red_today.get(r['op'],[0,0]); red_today[r['op']][0]+=1; red_today[r['op']][1]+= 1 if changed else 0
print("bad",bad); print(red_today)
