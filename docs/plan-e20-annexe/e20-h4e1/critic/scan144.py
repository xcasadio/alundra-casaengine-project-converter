import struct, mylib as L
# decode every word as an I-type candidate; report any with imm16 in 0x140..0x147 (and imm 0x144 in addiu/ori/andi forms)
OPS = {0x20:'lb',0x21:'lh',0x22:'lwl',0x23:'lw',0x24:'lbu',0x25:'lhu',0x26:'lwr',0x28:'sb',0x29:'sh',0x2a:'swl',0x2b:'sw',0x2e:'swr',0x09:'addiu',0x0d:'ori',0x0c:'andi',0x08:'addi'}
n = len(L.TEXT)//4
hits = []
for i in range(n):
    w = struct.unpack_from('<I', L.TEXT, 4*i)[0]
    op = w >> 26
    imm = w & 0xFFFF
    if op in OPS and 0x140 <= imm <= 0x147:
        hits.append((L.TBASE+4*i, OPS[op], imm, (w>>21)&31, (w>>16)&31))
stores = [h for h in hits if h[1] in ('sb','sh','sw','swl','swr')]
print('total I-type with imm 0x140..0x147:', len(hits))
print('STORES:')
for h in stores: print('  %08x %s rt=%d off=%#x base=r%d' % (h[0], h[1], h[4], h[2], h[3]))
print('address-forming (addiu/ori with 0x144):')
for h in hits:
    if h[1] in ('addiu','ori','addi') and h[2]==0x144: print('  %08x %s r%d, r%d, %#x' % (h[0], h[1], h[4], h[3], h[2]))
print('loads of 0x144:')
for h in hits:
    if h[1] in ('lw','lb','lbu','lh','lhu') and h[2]==0x144: print('  %08x %s' % (h[0], h[1]))
