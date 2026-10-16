import sys, struct
sys.dont_write_bytecode = True
EXE = r"D:/development/repo/Alundra Remake/Alundra (France)/Alundra (France)_extracted/ALUN_CD.EXE"
D = open(EXE,'rb').read()
BASE=0x80020000
def addr(off): return BASE + off - 0x800
targets = {'frame 8002E058':0x8002E058,'tick 8003B388':0x8003B388,'slotpass 8003C410':0x8003C410,'effpass 80038E84':0x80038E84,'hitpass 80039300':0x80039300,
           'anim step 8003BBDC':0x8003BBDC,'pos step 8003C284':0x8003C284,'create 8003BE74':0x8003BE74,'create2 8003BDD8':0x8003BDD8}
for name,t in targets.items():
    jal = 0x0C000000 | ((t>>2)&0x3FFFFFF)
    j = 0x08000000 | ((t>>2)&0x3FFFFFF)
    res=[]
    for off in range(0x800, len(D)-3, 4):
        w = struct.unpack_from('<I', D, off)[0]
        if w==jal: res.append(('jal',hex(addr(off))))
        elif w==j: res.append(('j',hex(addr(off))))
    # data refs (word == address) at any 4-aligned offset
    dat=[]
    for off in range(0x800, len(D)-3, 4):
        w = struct.unpack_from('<I', D, off)[0]
        if w==t: dat.append(hex(addr(off)))
    # lui/addiu or lui/ori pairs
    hi = (t+0x8000)>>16 & 0xFFFF; lo = t & 0xFFFF
    pairs=[]
    for off in range(0x800, len(D)-8, 4):
        w = struct.unpack_from('<I', D, off)[0]
        if (w>>26)==0x0F and (w&0xFFFF)==hi:
            rt=(w>>16)&31
            for k in range(1,6):
                if off+4*k+4>len(D): break
                w2 = struct.unpack_from('<I', D, off+4*k)[0]
                op=w2>>26
                if op in (9,13) and ((w2>>21)&31)==rt and (w2&0xFFFF)==lo:
                    pairs.append(hex(addr(off)))
    print(name, 'calls:', res, 'dataWords:', dat, 'luipairs:', pairs)
