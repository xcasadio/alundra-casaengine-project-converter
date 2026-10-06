import sys, os
SP = r"C:\Users\casad\AppData\Local\Temp\claude\D--development-repo-alundra-casaengine-project-converter\b00d1a72-420d-4acc-ab34-0c25fbdad3a1\scratchpad"
sys.path.insert(0, os.path.join(SP, 'f3-disc', 'binary')); sys.path.insert(0, os.path.join(SP,'e19j-disc'))
import emu as E
from probe53 import ENT, POOL, SEED, CODE, CP, ST
STATS_PTR=0x80176318; STATS=0x80190100
def A(hp, hpmax):
    newmax = min(50, max(0, hpmax+1))
    amount = hpmax+1
    newhp = newmax if amount > newmax else (0 if amount < 0 else amount)
    return newhp, newmax
bad=0; n=0
for hpmax in range(0,51):
    for hp in range(0,hpmax+1):
        m=E.make_machine(); m.hooks[0x800490FC]=lambda mm: None
        m.ww(ENT+0x14,hp); m.ww(ENT+0x18,hpmax); m.ww(SEED,0xB017C93D)
        m.ww(STATS_PTR, STATS); m.wh(STATS+0,hp); m.wh(STATS+2,hpmax)
        m.wb(CODE,0x82); m.wb(CODE+1,0x53); m.ww(CP,CODE)
        m.call(0x8003FE7C,0,0,CP,ST)
        m.call(0x8004DC68, m.rw(ENT+0x18)); m.call(0x8004DD30, m.rw(ENT+0x14))
        got=(m.rhs(STATS+0), m.rhs(STATS+2))
        exp=A(hp,hpmax); n+=1
        if got!=exp: bad+=1; print("diff", hp,hpmax,got,exp)
print("cases",n,"diffs",bad)
