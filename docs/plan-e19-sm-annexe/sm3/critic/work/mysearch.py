import mylib as m
a=0x8003C954
ins=m.dis(a,700)
# find function end: first jr ra after a, followed by a later prologue (addiu sp,-)
out=[]
for i in ins:
    out.append("%08X  %-8s %s"%(i.address,i.mnemonic,i.op_str))
open('../listings/my_search_raw.txt','w').write("\n".join(out))
# list jr ra and prologues
for i in ins:
    if i.mnemonic=='jr' : print('jr',hex(i.address),i.op_str)
    if i.mnemonic=='addiu' and i.op_str.startswith('$sp, $sp, -'): print('prologue',hex(i.address),i.op_str)
