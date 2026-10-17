"""Replays goldens_g1a.json against the REAL code in my own harness (fresh memory) - checks the golden files are real-code derived."""
import os, sys, json
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), 'emu'))
import emu as E
M32=0xFFFFFFFF; ENT0=0x80127D30; ST=0x294
G=json.load(open(r"C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/binfacts2/g1a/lst/goldens_g1a.json"))
ADDR={'StepTowards':0x8002F854,'TryLift':0x8002EDBC,'PlayerTryAction':0x8002EEAC,'UpdatePlayerCarriedEntity':0x8002F884,'C23':0x8006B848,'E2':0x8007ED30,'DestroyEntity1':0x8003A774,'MoveEntityCarriedBranch':0x80037E34}
GOFFS=[0x10,0x14,0x20,0x24,0x28,0x2C,0x30,0x34,0x38,0x68,0x6C,0x88,0x8C,0x98,0xAC,0xB0,0x114,0x118,0x11C,0x12C,0x130,0x134,0x138,0x13C,0x140,0x144,0x168,0x180,0x184,0x1B8,0x1C4,0x1D8,0x1DC,0x1E0,0x1E4,0x1E8,0x1EC,0x228]
m=E.make_machine()
J=[]
HAS={'v':0}
def getnum(mm): J.append(['item',mm.r[4]&M32]); mm.r[2]=HAS['v']
def d2(mm): J.append(['destroy',(mm.r[4]-ENT0)//ST, (mm.r[5]&M32) if (mm.r[5]&M32)<0x80000000 else (mm.r[5]&M32)-(1<<32)])
m.hooks[0x8004E428]=getnum; m.hooks[0x8003A59C]=d2
def zero(mm): mm.r[2]=0
m.hooks[0x800375E0]=zero; m.hooks[0x80037730]=zero
bad={}; tot={}
for name,lst in G.items():
    for g in lst:
        tot[name]=tot.get(name,0)+1
        # reset
        for s in range(0,12):
            for o in range(0,ST,4): m.ww(ENT0+s*ST+o,0)
        pre=g['pre']
        for sl,d in pre['entities'].items():
            for o,v in d.items(): m.ww(ENT0+int(sl)*ST+int(o,16), v)
        m.ww(0x80098F28, pre['ctr']); m.ww(0x80134360, pre['nslot']); m.wh(0x80126F2A, pre['padedge']); HAS['v']=pre['has3b']
        m.wb(0x80141000+0xE, pre['sheet_attack'])
        J.clear()
        r=m.call(ADDR[name], *g['args'])
        # compare
        ok = (r&M32)==g['ret']
        if ok:
            for sl in range(0,10):
                for o in GOFFS:
                    cur=m.rw(ENT0+sl*ST+o)
                    exp=g['post_changed']['entities'].get(str(sl),{}).get('0x%X'%o)
                    if exp is None: exp=pre['entities'].get(str(sl),{}).get('0x%X'%o,0)
                    if cur!=exp: ok=False
            if m.rw(0x80098F28)!=g['post_changed']['ctr']: ok=False
            if J!=g['journal']: ok=False
        if not ok: bad[name]=bad.get(name,0)+1
print('total',tot); print('mismatch',bad)
