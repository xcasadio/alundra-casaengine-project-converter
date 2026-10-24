import struct,sys
d=open(r"D:\development\repo\Alundra Remake\Alundra (France)\Alundra (France)_extracted\ALUN_CD.EXE","rb").read()
base=0x80020000; off=0x800
def scan(target):
    w=0x0C000000|((target&0x0FFFFFFF)>>2)
    res=[]
    for i in range(off,len(d)-3,4):
        if struct.unpack_from("<I",d,i)[0]==w:
            res.append(base+i-off)
    return res
for t in (0x80037730,0x80036F34,0x800370C4):
    print(hex(t),[hex(x) for x in scan(t)])
