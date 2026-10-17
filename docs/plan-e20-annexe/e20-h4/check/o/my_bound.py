import spellworld as S
from machine import s32, M32
HERO=0x80127D30
INUSE,COUNTER,PHASE=0x80127164,0x8012715C,0x80127160
m,STATS,placed=S.make(item=None, indices=(1,))
m.hooks[0x800490FC]=lambda mm: None
def run(item,phase,counter,extra=None):
    m.poke(HERO+0x88,0x33); m.poke(HERO+0x18C,0)
    m.poke(INUSE,item); m.poke(PHASE,phase); m.poke(COUNTER,counter)
    m.poke(0x80134360,0)
    if extra: extra()
    r=s32(m.call(0x8003634C))
    return r, s32(m.peek(HERO+0x88)), s32(m.peek(PHASE)), s32(m.peek(INUSE)), s32(m.peek(COUNTER))
print('earth1 phase1 c=99 ', run(0x2B,1,99))
print('earth1 phase1 c=100', run(0x2B,1,100))
print('earth2 phase1 c=279', run(0x2C,1,279))
print('earth2 phase1 c=280', run(0x2C,1,0x118))
def w(v): m.poke(0x80127118, v)
print('water1 phase1 [118]=58 -> after inc 59', run(0x2D,1,5,lambda: w(58)))
print('water1 phase1 [118]=59 -> 60', run(0x2D,1,5,lambda: w(59)))
