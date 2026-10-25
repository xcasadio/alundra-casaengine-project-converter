"""F255 (0x8007FC88) - the treasure chest's interact handler - replayed on the REAL code of ALUN_CD.EXE (France) in the copied MIPS interpreter
(emu/emu.py, copied from the E20.e0c annex; read-only use of the binary). Python stubs only for: SpawnWarpEntity (0x80039F58), Block (0x8003AD30),
GetEtcText (0x800816D4), the message opener (0x800450F0), LoadBgm (0x80049B7C). CanTake (0x80033D34) and GetNumberOfItem (0x8004E428) and the
dialog test (0x80045004) run for real. A model written from the disassembly is compared on random vectors, with instruction coverage."""
import sys, random, struct
HERE = 'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/t2-chest'
sys.path.insert(0, 'C:/Users/casad/AppData/Roaming/Python/Python314/site-packages')
sys.path.insert(0, HERE + '/emu')
import emu

M32 = 0xFFFFFFFF
ENT0 = 0x80127D30
STRIDE = 0x294
ITEMTAB = 0x800C5F7C
STATS = 0x801A0000      # PlayerStats image (the pointer [0x80176318])
INV = 0x801A1000        # inventory array (the pointer [0x8017631C]), 4 bytes per item, count at +2 (halfword)
HERO = ENT0
CHEST = ENT0 + 3 * STRIDE
ITEM = ENT0 + 9 * STRIDE

def s32(x):
    x &= M32
    return x - (1 << 32) if x & 0x80000000 else x

class Cov(emu.Emu):
    def __init__(self):
        super().__init__()
        self.cov = set()
        self.tracing = False
    def step(self):
        if self.tracing:
            self.cov.add(self.pc)
            w = self.rw(self.pc)
            op = w >> 26
            if op in (1, 2, 3, 4, 5, 6, 7) or (op == 0 and (w & 63) in (8, 9)):
                self.cov.add(self.pc + 4)
        super().step()

def mk():
    m = Cov()
    mm = emu.make_machine()
    m.hooks.update(mm.hooks)
    return m

m = mk()
J = []
CFG = {'spawn': ITEM}

def tag(v):
    v &= M32
    if 0x90000000 <= v < 0x90001000:
        return ('etc', v - 0x90000000)
    return ('raw', v)

def h_spawn(mc):
    sp = mc.r[29]
    J.append(('spawn', mc.r[4] & M32, mc.r[5] & M32, mc.r[6] & M32, mc.r[7] & M32, mc.rw(sp + 0x10), mc.rw(sp + 0x14), mc.rw(sp + 0x18)))
    mc.r[2] = CFG['spawn']
m.hooks[0x80039F58] = h_spawn
m.hooks[0x8003AD30] = lambda mc: J.append(('block', mc.r[4] & M32))
m.hooks[0x800816D4] = lambda mc: setattr_r2(mc, 0x90000000 + (mc.r[4] & M32))
def setattr_r2(mc, v):
    mc.r[2] = v & M32
m.hooks[0x800450F0] = lambda mc: J.append(('open', tag(mc.r[4]), mc.r[5] & M32))
m.hooks[0x80049B7C] = lambda mc: J.append(('bgm', mc.r[4] & M32))

def setup(g_mask=0, g_menu=0, carried=0, dlg=0, contents=0, chest88=0, flag=0x555, g29=1, g9a=0, count=None, spawn=ITEM, itemflags=0x8180):
    for a in range(CHEST, CHEST + STRIDE):
        m.wb(a, 0)
    for a in range(ITEM, ITEM + STRIDE):
        m.wb(a, 0)
    m.ww(0x800DC4B8, g_mask)
    m.ww(0x800DC4C4, g_menu)
    m.ww(HERO + 0x2C, carried)
    m.ww(0x80152F08, dlg)
    m.ww(0x80029BCC, g29)
    m.ww(0x8009A858, g9a)
    m.ww(0x80176318, STATS)
    m.ww(0x8017631C, INV)
    m.ww(0x8017638C, 0x62)
    for i in range(0x62):
        m.wh(INV + 4 * i + 2, 0)
    if count is not None:
        for it, c in count.items():
            m.wh(INV + 4 * it + 2, c)
    m.ww(CHEST + 0x3C, contents)
    m.ww(CHEST + 0x40, flag)
    m.ww(CHEST + 0x88, chest88)
    m.ww(CHEST + 0x114, 0x00220000)
    m.ww(CHEST + 0x118, 0x00330000)
    m.ww(CHEST + 0x11C, 0x00440000)
    m.ww(ITEM + 0x6C, itemflags)
    m.ww(ITEM + 0x10, 0x12345678)
    CFG['spawn'] = spawn
    J.clear()

def snapshot():
    f = lambda base, offs: {o: m.rw(base + o) for o in offs}
    return {'chest': f(CHEST, (0x88, 0x3C, 0x40)), 'item': f(ITEM, (0x10, 0x20, 0x6C, 0xB8, 0x274, 0x278, 0x27C, 0x280, 0x284, 0x288))}

def run():
    m.tracing = True
    m.call(0x8007FC88, CHEST)
    m.tracing = False
    return snapshot(), list(J)

def b5(item):
    return struct_u8(ITEMTAB + 8 * item + 5)
def struct_u8(a):
    return m.rb(a)

# the table of the objects is part of the EXE image in RAM: byte5 and byte7 are real data
def cantake_model(item, count):
    if item < 0x28:
        if item < 0x24:
            return 0 if item == 0 else 1
        return 1 if count < m.rb(ITEMTAB + 8 * item + 7) else 0
    if item == 0x29:
        return 1 if count < m.rb(ITEMTAB + 8 * item + 7) else 0
    return 1

def model(g_mask, g_menu, carried, dlg, contents, chest88, spawn, g29, g9a, count, itemflags):
    j = []
    snap = {'chest': {0x88: chest88, 0x3C: contents, 0x40: 0x555}, 'item': {0x10: 0x12345678, 0x20: 0, 0x6C: itemflags, 0xB8: 0, 0x274: 0, 0x278: 0, 0x27C: 0, 0x280: 0, 0x284: 0, 0x288: 0}}
    if g_mask != 0 or g_menu != 0 or carried != 0 or (dlg & 4):
        return snap, j
    if contents == 0 or chest88 != 0:
        j.append(('open', ('etc', 0x48), 0))
        return snap, j
    snap['chest'][0x88] = 2
    j.append(('spawn', CHEST, 0, contents + 0x1E, 0x00220000, 0x00330000, 0x00440000 + 0x100000, 0))
    if spawn == 0:
        j.append(('open', ('etc', 0x49), 0))
        return snap, j
    j.append(('block', CHEST))
    it = snap['item']
    it[0xB8] = 0x8000
    it[0x278] = 0x40
    it[0x20] = 0
    it[0x274] = 2
    it[0x27C] = 0
    it[0x280] = CHEST
    it[0x6C] = itemflags & ~0x180 & M32
    v = cantake_model(contents, count)
    it[0x284] = v
    if v == 0:
        it[0x288] = 0
        return snap, j
    if m.rb(ITEMTAB + 8 * contents + 5) != 0:
        it[0x288] = 0
        return snap, j
    if g29 != 0 or g9a != 0:
        it[0x288] = 1
        j.append(('bgm', 0))
        return snap, j
    it[0x288] = 0
    return snap, j

if __name__ == '__main__':
    seed = int(sys.argv[1]) if len(sys.argv) > 1 else 11
    NV = int(sys.argv[2]) if len(sys.argv) > 2 else 12000
    rng = random.Random(seed)
    items = [0, 1, 2, 3, 4, 6, 0x0E, 0x10, 0x13, 0x1A, 0x20, 0x24, 0x25, 0x26, 0x27, 0x28, 0x29, 0x2B, 0x3C, 0x3D, 0x45, 0x47, 0x48, 0x49, 0x4F, 0x50, 0x51, 0x52, 0x53, 0x54, 0x61]
    bad = 0
    outcomes = {}
    for it in range(NV):
        p = dict(
            g_mask=rng.choice([0, 0, 0, 0, 8, 0x10, 0x48]), g_menu=rng.choice([0, 0, 0, 1]), carried=rng.choice([0, 0, 0, 0, ITEM]), dlg=rng.choice([0, 0, 0, 4, 1, 5]),
            contents=rng.choice(items), chest88=rng.choice([0, 0, 0, 1, 2]), spawn=rng.choice([ITEM, ITEM, ITEM, 0]),
            g29=rng.choice([1, 1, 1, 0]), g9a=rng.choice([0, 1]), itemflags=rng.choice([0x8180, 0, M32, 0x181, rng.getrandbits(32)]))
        cnt = {}
        for k in (0x24, 0x25, 0x26, 0x27, 0x29):
            cnt[k] = rng.choice([0, 1, 8, 9, 10])
        p['count'] = cnt
        setup(**p)
        real, jr = run()
        mdl, jm = model(p['g_mask'], p['g_menu'], p['carried'], p['dlg'], p['contents'], p['chest88'], p['spawn'], p['g29'], p['g9a'], cnt.get(p['contents'], 0), p['itemflags'])
        # when the spawn stub returns 0 the item entity is untouched by F255 (the model keeps its initial image)
        key = (('guard' if (p['g_mask'] or p['g_menu'] or p['carried'] or (p['dlg'] & 4)) else 'run'), len(jr), tuple(t[0] for t in jr))
        outcomes[key] = outcomes.get(key, 0) + 1
        if real != mdl or jr != jm:
            bad += 1
            if bad < 6:
                print('DIFF', p, '\n real', real, jr, '\n model', mdl, jm)
    print('F255 vectors', NV, 'diffs', bad)
    ws = list(range(0x8007FC88, 0x8007FE20, 4))
    miss = [a for a in ws if a not in m.cov]
    print('coverage', len(ws) - len(miss), '/', len(ws), [hex(a) for a in miss])
    for k, v in sorted(outcomes.items(), key=lambda kv: -kv[1]):
        print('  outcome', k, v)
