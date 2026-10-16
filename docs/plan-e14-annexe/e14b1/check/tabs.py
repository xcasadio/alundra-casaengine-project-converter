import exelib as X
for s,c in [(0,0),(2,15),(2,16),(2,17),(2,18),(2,19),(3,5),(3,6),(3,11),(3,12),(4,1),(4,0)]:
    base=X.slot_table_base(s)
    print("ABCDEF"[s],c,hex(base+4*c),hex(X.handler(s,c)))
# jump tables
for a,n in [(0x80027334,8),(0x80027354,8),(0x80027374,8),(0x80027394,8)]:
    print(hex(a),[hex(X.u32(a+4*i)) for i in range(n)])
