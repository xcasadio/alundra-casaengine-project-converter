import struct, sys, exelib
D = exelib.DATA; base = exelib.T_ADDR
n = (len(D)-0x800)//4
want = [int(x,16) for x in sys.argv[1:]]
for i in range(n):
    w = struct.unpack_from("<I", D, 0x800+4*i)[0]
    op = w>>26
    if op in (0x23,0x2B,0x25,0x29,0x24,0x28):
        imm = w & 0xFFFF
        if imm in want:
            print(hex(base+4*i), {0x23:'lw',0x2B:'sw',0x25:'lhu',0x29:'sh',0x24:'lbu',0x28:'sb'}[op], 'rs',(w>>21)&31,'rt',(w>>16)&31, hex(imm))
