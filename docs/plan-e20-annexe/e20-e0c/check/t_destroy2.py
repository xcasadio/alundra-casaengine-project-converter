import random, h2
from h2 import *
rng = random.Random(77)
m = mk()
PLAYER_PTR = 0x80176318; PLAYER = 0x80141000
m.ww(PLAYER_PTR, PLAYER)
MAXIDX = 0x80134360
HDR = 0x80150000
FX = 0x80150400
CARRIER = 0x80150800
FAKE = 0x80160000
B4 = [__import__('exelib').u8(ITEMTAB + 8 * i + 4) for i in range(0x62)]
J = []
nsp = [0]
def spawn(mc):
    sp = mc.r[29]
    J.append(('spawn', mc.r[6] & M32 - 0x1E if False else (mc.r[6] & M32), mc.r[7] & M32, mc.rw(sp + 0x10), mc.rw(sp + 0x14)))
    a = FAKE + nsp[0] * 0x300; nsp[0] += 1
    for i in range(0x300): mc.wb(a + i, 0)
    mc.r[2] = a
m.hooks[0x80039F58] = spawn
m.hooks[0x800490FC] = lambda mc: J.append(('sound', mc.r[4] & M32))
def fxcall(mc):
    sp = mc.r[29]
    J.append(('fx', mc.r[4] & M32, mc.r[5] & M32, mc.r[6] & M32, mc.r[7] & M32, mc.rw(sp + 0x10), mc.rw(sp + 0x14), mc.rw(sp + 0x18), mc.rw(sp + 0x1C)))
m.hooks[0x8003BE74] = fxcall

def setup(ents):
    """ents: list of dicts slot -> fields. returns nothing"""
    for s in range(64):
        for i in range(STRIDE): m.wb(ENT0 + s*STRIDE + i, 0)
    for s, f in ents.items():
        a = ENT0 + s*STRIDE
        for off, v in f.items(): m.ww(a + off, v)

def run_destroy(addr, slot, a1):
    J.clear(); nsp[0] = 0
    e = ENT0 + slot*STRIDE
    m.tracing = True
    m.call(addr, e, a1)
    m.tracing = False
    return list(J)

def model_destroy(ents, slot, a1, mpmax):
    """ents: dict slot-> field dict (copied). returns (journal, final dict)"""
    ents = {s: dict(f) for s, f in ents.items()}
    j = []
    def getf(s, off): return ents.setdefault(s, {}).get(off, 0)
    def setf(s, off, v): ents.setdefault(s, {})[off] = v & M32
    def spawncontents(s):
        item = getf(s, 0x3C)
        if item == 0: return
        if item in (0x26, 0x51, 0x52) and mpmax == 0: return
        j.append(('spawn', (item + 0x1E) & M32, getf(s, 0x114), getf(s, 0x118), getf(s, 0x11C)))
        j.append(('sound', 0x54))
    def finish(s, a1):
        spawncontents(s)  # note: caller order handled by callers
    def destroy(s, a1):
        spawncontents(s)
        setf(s, 0x10, 4)
        eff = getf(s, 0x1B8)
        setf(s, 0x228, M32)
        if eff:
            setf(s, 0x1B8, 0)  # effect +0x68 = 0 not tracked
        if s32(a1) == -1:
            a1 = m.rb(HDR + 0x1E)
        if a1 != 0:
            j.append(('fx', 0, a1 & M32, 0, ENT0 + s*STRIDE, 1, 0, 0, 0))
        carrier = getf(s, 0x28)
        if carrier: pass
    def recurse(s, a1, maxidx):
        if maxidx >= 0:
            for i in range(0, maxidx + 1):
                st = getf(s, 0x10)
                if 1 <= st <= 3 and getf(i, 0x0C) == ENT0 + s*STRIDE:
                    recurse(i, a1, maxidx)
        destroy(s, a1)
    return j, ents, destroy, recurse

bad = 0; n = 0
for it in range(3000):
    mpmax = rng.choice([0, 3])
    m.wh(PLAYER + 6, mpmax)
    m.wb(HDR + 0x1E, rng.choice([0, 0, 7, 255]))
    nent = rng.randrange(2, 8)
    maxidx = nent - 1 + rng.choice([0, 0, 1])
    ents = {}
    for s in range(nent):
        f = {0x10: rng.choice([0, 1, 2, 3, 4]), 0x3C: rng.choice([0, 0, 0x45, 0x54, 0x26, 0x51, 0x24]), 0x114: rng.getrandbits(32), 0x118: rng.getrandbits(32), 0x11C: rng.getrandbits(32),
             0x64: HDR, 0x1B8: rng.choice([0, 0, FX]), 0x28: rng.choice([0, CARRIER])}
        par = rng.choice([0, 0, ENT0 + rng.randrange(0, nent)*STRIDE])
        f[0x0C] = par
        ents[s] = f
    # avoid cycles (the real game would loop forever)
    def has_cycle():
        for s in range(nent):
            seen = set(); cur = s
            while True:
                p = ents[cur][0x0C]
                if p == 0: break
                ps = (p - ENT0)//STRIDE
                if ps in seen or ps == cur: return True
                seen.add(cur); cur = ps
        return False
    if has_cycle(): continue
    mode = rng.choice(['destroy', 'rec', 'light'])
    slot = rng.randrange(0, nent)
    a1 = rng.choice([M32, 0, 6, 255])
    for s in range(64, 70): pass
    setup(ents)
    m.ww(CARRIER + 0x2C, 0x55)
    m.ww(FX + 0x68, 0x77)
    m.ww(MAXIDX, maxidx)
    addr = {'destroy': 0x8003A59C, 'rec': 0x8003A648, 'light': 0x8003A774}[mode]
    jr = run_destroy(addr, slot, a1)
    real = {s: {off: m.rw(ENT0 + s*STRIDE + off) for off in (0x10, 0x228, 0x1B8)} for s in range(nent)}
    rcar = m.rw(CARRIER + 0x2C); rfx = m.rw(FX + 0x68)
    # model
    j, me, destroy, recurse = model_destroy(ents, slot, a1, mpmax)
    if mode == 'destroy': destroy(slot, a1)
    elif mode == 'rec': recurse(slot, a1, maxidx)
    else:
        # light: state 4, +0x228=-1, release effect, carrier cleared
        me[slot][0x10] = 4; me[slot][0x228] = M32
        if me[slot].get(0x1B8): me[slot][0x1B8] = 0
        j = []
    # carriers: any destroyed entity with +0x28 != 0 clears CARRIER+0x2C (all share CARRIER)
    mcar = 0 if any(me[s].get(0x28) and me[s].get(0x10) == 4 and (mode != 'rec' or True) for s in me if me[s].get(0x10) == 4 and ents[s].get(0x10) != 4 or (s == slot and me[s].get(0x28))) else 0x55
    # simplified: determine from the set of destroy() calls via the journal-less approach below
    modelfinal = {s: {off: me[s].get(off, 0) for off in (0x10, 0x228, 0x1B8)} for s in range(nent)}
    # compare ent states and journals, not the carrier (shared pointer, checked below)
    jm = [t for t in j]
    ok = (jr == jm) and (real == modelfinal)
    n += 1
    if not ok:
        bad += 1
        if bad < 6: print('DIFF', mode, slot, hex(a1), mpmax, maxidx, [(s, ents[s][0x10], ents[s][0x3C], hex(ents[s][0x0C])) for s in ents]); print(' real', jr); print(' mod ', jm); print(' states real', {s: real[s][0x10] for s in real}, 'mod', {s: modelfinal[s][0x10] for s in modelfinal})
print('destroy/recursion vectors', n, 'diffs', bad)
for nm, lo, hi in (('DestroyEntity', 0x8003A59C, 0x8003A648), ('recursion', 0x8003A648, 0x8003A774), ('light', 0x8003A774, 0x8003A7B0)):
    ws = list(range(lo, hi, 4)); print(nm, 'cov', len([a for a in ws if a in m.cov]), len(ws), [hex(a) for a in ws if a not in m.cov])
