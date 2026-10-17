import os, sys, random, struct
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), 'emu'))
import emu as E
M32=0xFFFFFFFF; ENT0=0x80127D30; ST=0x294
m=E.make_machine(); J=[]
def d2(mm): J.append(('destroy2',(mm.r[4]-ENT0)//ST, mm.r[5]&M32))
m.hooks[0x8003A59C]=d2
def wr(s,o,v): m.ww(ENT0+s*ST+o, v&M32)
def rd(s,o): return m.rw(ENT0+s*ST+o)
def snap(s): a=(ENT0+s*ST)&0x1FFFFFFF; return bytes(m.ram[a:a+ST])
rnd=random.Random(4242)
fails={}; cnt={}
def chk(n,ok,info=None):
    cnt[n]=cnt.get(n,0)+1
    if not ok: fails[n]=fails.get(n,0)+1
TABLE=[m.rw(0x80027400+4*i) for i in range(8)]
N=6000
for _ in range(N):
    # C23
    for s in (0,3,5):
        for o in range(0,ST,4): wr(s,o, rnd.getrandbits(32) if rnd.random()<0.4 else 0)
    carrier=rnd.choice([0,ENT0,ENT0])
    wr(3,0x28,carrier)
    t=rnd.choice([0,1,2,3,0xFFFFFFFF,4,5,6,7])
    wr(3,0x24,t); wr(0,0x2C, rnd.choice([0,ENT0+3*ST,ENT0+5*ST]))
    p3=snap(3); p0=snap(0)
    m.call(0x8006B848, ENT0+3*ST)
    q3=snap(3); q0=snap(0)
    e3=bytearray(p3); e0=bytearray(p0)
    if carrier!=0:
        ts=t if t<0x80000000 else t-(1<<32)
        if t==0:
            struct.pack_into('<I',e3,0x88,0)
        else:
            if ts==-1: anim=3
            else: anim=struct.unpack_from('<I', m.ram, (0x80027400+4*ts)&0x1FFFFFFF)[0] if 0<=ts<8 else None
            if anim is not None:
                struct.pack_into('<I',e3,0x88,anim)
                struct.pack_into('<I',e3,0x24,0)
                struct.pack_into('<I',e3,0x28,0)
                fl=struct.unpack_from('<I',p3,0x6C)[0]
                struct.pack_into('<I',e3,0x6C,((fl|0x30)&~0x80)&M32)
                # carrier.+0x2C = 0 (carrier is the hero image at ENT0 when carrier==ENT0)
                if carrier==ENT0: struct.pack_into('<I',e0,0x2C,0)
    if (carrier!=0 and t not in (0,) and not (t<0x80000000 and 0<=t<8) and t!=0xFFFFFFFF):
        chk('C23-table-oob-skip', True)  # out of table reads: not modelled
    else:
        chk('C23', bytes(e3)==q3 and bytes(e0)==q0)
    # E2
    for o in range(0,ST,4): wr(3,o, rnd.getrandbits(32) if rnd.random()<0.4 else 0)
    wr(3,0x88, rnd.choice([0,1,2,2,3])); wr(3,0xAC, rnd.choice([0,1,1,2])); wr(3,0x184, rnd.choice([0,4,5,0xFFFF,rnd.getrandbits(32)]))
    p3=snap(3); J.clear()
    m.call(0x8007ED30, ENT0+3*ST)
    q3=snap(3)
    a=struct.unpack_from('<I',p3,0x88)[0]; ae=struct.unpack_from('<I',p3,0xAC)[0]; v=struct.unpack_from('<I',p3,0x184)[0]
    e3=bytearray(p3)
    if (a==2 and ae==1) or (v&4):
        jm=[('destroy2',3,M32)]
    else:
        jm=[]
        struct.pack_into('<I',e3,0x88,2)
        fl=struct.unpack_from('<I',p3,0x6C)[0]; struct.pack_into('<I',e3,0x6C,((fl|0x40)&~0x30)&M32)
    chk('E2', bytes(e3)==q3 and J==jm)
    # destroy1
    for s in (0,3,7):
        for o in range(0,ST,4): wr(s,o, rnd.getrandbits(32) if rnd.random()<0.4 else 0)
    link=rnd.choice([0,ENT0+7*ST]); car=rnd.choice([0,ENT0,ENT0+7*ST])
    wr(3,0x1B8,link); wr(3,0x28,car); wr(0,0x2C,ENT0+3*ST); wr(7,0x68,rnd.randrange(1,500)); wr(7,0x2C,0x1234)
    p=[snap(s) for s in (0,3,7)]
    m.call(0x8003A774, ENT0+3*ST)
    q=[snap(s) for s in (0,3,7)]
    e=[bytearray(x) for x in p]
    struct.pack_into('<I',e[1],0x10,4); struct.pack_into('<I',e[1],0x228,M32)
    if link!=0:
        struct.pack_into('<I',e[2],0x68,0)   # link is slot 7 here
        struct.pack_into('<I',e[1],0x1B8,0)
    if car!=0:
        sl=(car-ENT0)//ST
        struct.pack_into('<I',e[{0:0,7:2}[sl]],0x2C,0)
    chk('Destroy1', [bytes(x) for x in e]==q)
print('counts',cnt); print('fails',fails)
