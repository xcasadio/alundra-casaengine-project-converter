import struct, exelib
D = exelib.DATA; base = exelib.T_ADDR
n = (len(D)-0x800)//4
hits = {}
names = {0x23:'lw',0x2B:'sw',0x21:'lh',0x25:'lhu',0x20:'lb',0x24:'lbu',0x29:'sh',0x28:'sb'}
for i in range(n):
    w = struct.unpack_from("<I", D, 0x800+4*i)[0]
    op = w>>26
    if op in (0x23,0x2B):
        imm = w & 0xFFFF
        rs = (w>>21)&31; rt=(w>>16)&31
        if imm in (0x28,0x2C) and rs != 29:
            hits.setdefault((op,imm),[]).append((base+4*i, rs, rt))
for k,v in sorted(hits.items()):
    print(names[k[0]], hex(k[1]), len(v))
    print('  ', ' '.join(f"{a:08x}(r{rs},r{rt})" for a,rs,rt in v))
