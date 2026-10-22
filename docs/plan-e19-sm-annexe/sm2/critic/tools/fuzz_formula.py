import sys, random
sys.path.insert(0,'tools')
from myemu import *
random.seed(777)
special=[0,1,2,0xFFFF,0x10000,0x7FFF0000,0x80000000,0x80000001,0x7FFFFFFF,0xFFFFFFFF,0xFFFF0000,0x8000,0x01000000,0x00FFFFFF,0xFFFFFFFE]
def rnd():
    r=random.random()
    if r<0.35: return random.choice(special)
    if r<0.55: return (random.choice(special)+random.randint(-3,3))&M32
    return random.getrandbits(32)
def cf(memo,pos,lo,hi):
    d=s32(memo-pos)
    if d<0: d=s32(-d)
    return 0 if d<s32(((lo|(hi<<8))<<16)) else 1
n=0; bad=0
for it in range(120000):
    op=random.choice((0x6B,0x6C,0x6D)); k=op-0x6B
    memo=rnd(); pos=rnd()
    lo=random.choice((0,1,0x78,0xFF,random.randint(0,255))); hi=random.choice((0,1,0x7F,0x80,0xFF,random.randint(0,255)))
    # make d near thr sometimes
    if random.random()<0.3:
        thr=((lo|(hi<<8))<<16)&M32
        pos=(memo-thr+random.randint(-2,2))&M32
        if random.random()<0.5: pos=(memo+thr+random.randint(-2,2))&M32
    S_words={0xC:0,0x10:0,0x14:0,0x2C:7}
    S_words[0xC+4*k]=memo
    pp=[0,0,0]; pp[k]=pos
    v0,mem,S,E,O=call_handler(op,tuple(pp),(0,0,0),S_words,[op,lo,hi])
    got=mem.lw(S+0x2C)
    if got!=cf(memo,pos,lo,hi): bad+=1; print('BAD',hex(op),hex(memo),hex(pos),lo,hi,got)
    n+=1
print('runs',n,'bad',bad)
