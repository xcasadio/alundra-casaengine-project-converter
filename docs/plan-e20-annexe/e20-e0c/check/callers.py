import sys, struct, exelib
D = exelib.DATA
def callers(t, kinds=(3,2)):
    out=[]
    for o in range(0x800, len(D)-3, 4):
        w = struct.unpack_from('<I', D, o)[0]
        if (w>>26) in kinds:
            tgt = ((w & 0x3FFFFFF)<<2) | 0x80000000
            if tgt == t:
                out.append((0x80020000 + o - 0x800, 'jal' if (w>>26)==3 else 'j'))
    return out
def words_ptr(t):
    out=[]
    for o in range(0x800, len(D)-3, 4):
        if struct.unpack_from('<I', D, o)[0]==t: out.append(0x80020000+o-0x800)
    return out
if __name__=='__main__':
    for a in sys.argv[1:]:
        t=int(a,16)
        print(hex(t), [(hex(x),k) for x,k in callers(t)], 'ptrs', [hex(x) for x in words_ptr(t)])
