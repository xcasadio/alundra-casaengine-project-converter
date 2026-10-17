import sys, os
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), 'emu'))
import emu as E
M32=0xFFFFFFFF; ENT0=0x80127D30; ST=0x294
m=E.make_machine()
def ea(s): return ENT0+s*ST
def wr(s,o,v): m.ww(ea(s)+o, v&M32)
def rd(s,o): return m.rw(ea(s)+o)
def s32(x):
    x&=M32; return x-(1<<32) if x&0x80000000 else x
# equality case m == e
res=[]
for (dx,dy,mag) in [(0x150000,0,0x150000),(0,-0x150000,0x150000),(0x100000,0x80000,0x100000)]:
    for sgn in (+1,-1):
        for s in range(0,9): 
            for o in range(0,ST,4): wr(s,o,0)
        wr(0,0x2C,ea(3)); wr(3,0x1C4,0x80141000); m.wb(0x80141000+0xE,1)
        # e = |dz-0x200000| == m  -> dz = 0x200000 + sgn*mag
        dz = 0x200000 + sgn*mag
        wr(3,0x114,dx); wr(3,0x118,dy); wr(3,0x11C,dz)
        m.call(0x8002F884, 0)
        res.append(((hex(dx&M32),hex(dy&M32),hex(dz&M32)), tuple(hex(rd(0,o)) for o in (0x30,0x34,0x38))))
for r in res: print(r)
