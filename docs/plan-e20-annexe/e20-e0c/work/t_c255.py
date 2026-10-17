import random, sys
from lh import *
import models as M
from c255model import *
random.seed(int(sys.argv[1]) if len(sys.argv) > 1 else 3)
E_ADDR = ENT0 + 9 * STRIDE
CHEST = ENT0 + 3 * STRIDE
MAPINFO = 0x80140000

class Ext:
    def __init__(self, rv, chest_img, e_addr):
        self.rv = rv; self.j = []; self.chest = chest_img; self.e_addr = e_addr; self.nb = False; self.bi = 0
    def busy(self):
        v = self.rv['busy'][self.bi % len(self.rv['busy'])]; self.bi += 1; self.j.append(('busy',)); return v
    def cdbusy(self): self.j.append(('cdbusy',)); return self.rv['cd']
    def give(self, item): self.j.append(('give', item)); return self.rv['give']
    def strcpy(self, src): self.j.append(('strcpy', src)); self.nb = True
    def strcat(self, src): self.j.append(('strcat', src))
    def bufclear(self): self.nb = False
    def bufnonempty(self): return self.nb
    def open(self, mode): self.j.append(('open', mode))
    def anim(self, n): self.j.append(('anim', n))
    def sound(self, n): self.j.append(('sound', n))
    def setflag(self, f): self.j.append(('setflag', f & M32))
    def apply(self, item): self.j.append(('apply', item))
    def block(self): self.j.append(('block',))
    def unblock(self, who): self.j.append(('unblock', who))
    def bgm0(self): self.j.append(('bgm0',))
    def cd(self, n): self.j.append(('cd', n))
    def stopsound(self): self.j.append(('stopsound',))
    def destroy_light(self): self.j.append(('destroy_light',))
    def chest40(self, ptr): return self.chest.g(0x40)
    def chest88zero(self, ptr): self.chest.s(0x88, 0)

def rand_ent(rng):
    img = bytearray(STRIDE)
    I = Img(img)
    item = rng.choice(list(range(0, 0x58)) + [0x45, 0x46, 0x47, 0x54, 0x55, 0x24, 0x4F, 0x50, 0x51, 0x53, 0x56] * 3)
    I.s(0x68, item + 0x1E)
    I.s(0x274, rng.choice([0, 1, 1, 1, 1, 2, 2, 3]))
    I.s(0x278, rng.choice([M32, 0, 1, 2, 3, 0x78, 0x79, 0x7A, 600, 5, rng.getrandbits(32), 0xFFFFFFFE]))
    I.s(0x27C, rng.choice([0, 0, 1, 1, 2, 3, 4, M32, 5]))
    I.s(0x280, rng.choice([0, M32, CHEST, rng.getrandbits(32)]))
    I.s(0x284, rng.choice([0, 0xA0000, 0x78000, 0x5A000, 0x100, 0x800, 0x1000, 0x2000, rng.getrandbits(32), -0x50000 & M32]))
    I.s(0x288, rng.choice([0, 0, 1]))
    if I.g(0x274) == 2: I.s(0x280, CHEST)
    I.s(0xB8, rng.choice([0, 0xA0000, 0x10000, -0x10000 & M32, 1, M32]))
    I.s(0x144, rng.choice([0, 1, 1]))
    I.s(0x1CC, rng.choice([0, 5, 0x78]))
    for ax, sz in ((0x1D8, 0x1F0), (0x1DC, 0x1F4), (0x1E0, 0x1F8)):
        I.s(ax, rng.randrange(-40, 40) << 16 if rng.random() < 0.5 else rng.randrange(-300000, 300000))
        I.s(sz, rng.choice([0, 1, 0xFFFF, 0x1FFFF, 0xFFFFF, 0x20000, 0x3FFFF, rng.randrange(0, 0x100000)]))
    return img

def rand_hero(rng, e_img):
    img = bytearray(STRIDE); I = Img(img); E = Img(e_img)
    for ax, sz in ((0x1D8, 0x1F0), (0x1DC, 0x1F4), (0x1E0, 0x1F8)):
        near = rng.random() < 0.8
        base = E.gs(ax)
        I.s(ax, base + (rng.randrange(-0x40000, 0x40000) if near else rng.randrange(-0x2000000, 0x2000000)))
        I.s(sz, rng.choice([0, 1, 0xFFFF, 0x1FFFF, 0xFFFFF, 0x20000, 0x3FFFF, rng.randrange(0, 0x100000)]))
    return img


w = World(); m = w.m
# item name pointers: emulate the load-time fill with a tag per item
for i in range(0x58): m.ww(ITEMTAB + 8 * i, 0xA0000000 + i)
m.ww(MAPINFO_PTR, MAPINFO)
CUR = {}
def install(X):
    h = m.hooks
    def tagdec(v):
        v &= M32
        if 0x90000000 <= v < 0x90001000: return ('str', v - 0x90000000)
        if 0xA0000000 <= v < 0xA0001000: return ('name', v - 0xA0000000)
        return ('raw', v)
    h[0x80045004] = lambda mc: setattr_r2(mc, 1 if X.busy() else 0)
    h[0x800816D4] = lambda mc: setattr_r2(mc, 0x90000000 + (mc.r[4] & M32))
    def strcpy(mc): X.strcpy(tagdec(mc.r[5])); mc.wb(mc.r[4], 1)
    def strcat(mc): X.strcat(tagdec(mc.r[5]))
    h[0x80081FB8] = strcpy; h[0x80081EAC] = strcat
    h[0x800450F0] = lambda mc: X.open(mc.r[5] & M32)
    h[0x8004507C] = lambda mc: X.anim(mc.r[4] & M32)
    h[0x800490FC] = lambda mc: X.sound(mc.r[4] & M32)
    h[0x80032B28] = lambda mc: X.setflag(mc.r[4] & M32)
    h[0x80033F00] = lambda mc: setattr_r2(mc, X.give(mc.r[5] & M32))
    h[0x80033DBC] = lambda mc: X.apply(mc.r[5] & M32)
    h[0x8003AD30] = lambda mc: X.block()
    def unblock(mc):
        a = mc.r[4] & M32
        X.unblock(('self',) if a == E_ADDR else ('ent', a))
    h[0x8003ADAC] = unblock
    h[0x80049B7C] = lambda mc: X.bgm0()
    h[0x8005A9E0] = lambda mc: X.cd(mc.r[4] & M32)
    h[0x8005A7D4] = lambda mc: setattr_r2(mc, 1 if X.cdbusy() else 0)
    h[0x80049AF4] = lambda mc: X.stopsound()
    h[0x8003A774] = lambda mc: X.destroy_light()
def setattr_r2(mc, v): mc.r[2] = v
def put(addr, img):
    for i, b in enumerate(img): m.wb(addr + i, b)
def get(addr): return bytes(m.ram[addr & 0x1FFFFFFF:(addr & 0x1FFFFFFF) + STRIDE])

class RealExt(Ext):
    """same journal; the chest memory lives in RAM"""
    def chest40(self, ptr): return m.rw(CHEST + 0x40)
    def chest88zero(self, ptr): m.ww(CHEST + 0x88, 0)
    def bufnonempty(self): return m.rb(BUF) != 0
    # in the real run the handler itself reads the buffer byte; the hooks only write the marker
bad = 0; n = 0; nticks = 0; jcount = None; RECORD = []
import collections
jcount = collections.Counter()
rng = random.Random(random.randrange(1 << 30))
NV = int(sys.argv[2]) if len(sys.argv) > 2 else 6000
installed = {}
for it in range(NV):
    e_img = rand_ent(rng); h_img = rand_hero(rng, e_img)
    ch_img = bytearray(STRIDE); CH = Img(ch_img); CH.s(0x40, rng.getrandbits(32)); CH.s(0x88, rng.choice([0, 1, 2]))
    thr = rng.choice([0, 1, 2, 3, 8, 0x10, 0x1000, 0xFFFF]);
    if rng.random() < 0.35:
        # targeted boundary vector: pickup overlap and bounce threshold on the exact edges
        E = Img(e_img); H = Img(h_img)
        E.s(0x274, 1); E.s(0x27C, 1); E.s(0x278, M32 if rng.random() < 0.7 else 600)
        for ax, sz in ((0x1D8, 0x1F0), (0x1DC, 0x1F4), (0x1E0, 0x1F8)):
            hs = rng.choice([0, 1, 5, 0xFFFFFFFF, 0xFFFF]); es = rng.choice([0, 1, 5, 0xFFFFFFFF, 0xFFFF])
            H.s(sz, hs); E.s(sz, es)
            hp = rng.randrange(-1000, 1000)
            d = rng.choice([-1, 0, 1, s32(hs), s32(hs) + 1, s32(hs) + 2, -s32(es), -(s32(es) + 1), -(s32(es) + 2), -s32(es) - 3, 0])
            H.s(ax, hp); E.s(ax, hp + d)
        if rng.random() < 0.6:
            E.s(0x144, 1)
            tt = rng.choice([0, 1, 2, 3, 8, 0x10])
            x = ((tt << 8) * 16) // 12 + rng.randrange(-2, 3)
            E.s(0x284, x & M32); E.s(0x1CC, 0)
            thr = tt
    w99858 = rng.choice([0, 1]); c29 = 1 if rng.random() < 0.8 else 0
    rv = dict(busy=[rng.choice([0, 0, 0, 0, 1]) for _ in range(40)], cd=rng.choice([0, 0, 1]), give=rng.choice([0, 1]))
    K = rng.choice([1, 3, 8, 14])
    tick_in = [rng.choice([None, 0, 1]) for _ in range(K)]
    # ---- model
    em = Img(bytearray(e_img)); hm = Img(bytearray(h_img)); chm = Img(bytearray(ch_img))
    Xm = Ext(rv, chm, E_ADDR)
    jm = []; destroyed_m = False
    for t in range(K):
        Xm.j = []
        c255(em, hm, Xm, thr, w99858, c29)
        jm.append(list(Xm.j))
        if tick_in[t] is not None: em.s(0x144, tick_in[t])
        if ('destroy_light',) in Xm.j: break
    # ---- real
    put(E_ADDR, e_img); put(HERO, h_img); put(CHEST, ch_img)
    m.ww(MAPINFO + 4, thr); m.wh(MAPINFO + 4, thr)
    m.ww(0x8009A858, w99858); m.ww(0x80029BCC, c29)
    m.wb(BUF, 0)
    Xr = RealExt(rv, None, E_ADDR); install(Xr)
    jr = []
    for t in range(K):
        Xr.j = []
        w.call(0x8007C174, E_ADDR)
        jr.append(list(Xr.j))
        if tick_in[t] is not None: m.ww(E_ADDR + 0x144, tick_in[t])
        if ('destroy_light',) in Xr.j: break
    n += 1; nticks += len(jr)
    RECORD.append(dict(e=bytes(e_img), h=bytes(h_img), ch=bytes(ch_img), thr=thr, w=w99858, c29=c29, rv=rv, K=K, tick_in=tick_in, jr=jr, fe=get(E_ADDR), chr=get(CHEST)))
    fe_m = bytes(em.raw); fe_r = get(E_ADDR)
    for tt in jm:
        for ev in tt: jcount[ev[0]] += 1
    if jm != jr or fe_m != fe_r or bytes(chm.raw) != get(CHEST):
        bad += 1
        if bad < 6:
            print('MISMATCH', it, 'K', K, 'c29', c29, 'w99858', w99858, 'ram', hex(m.rw(0x80029BCC)), hex(m.rw(0x8009A858)))
            for t, (a, b) in enumerate(zip(jm, jr)):
                if a != b: print('  tick', t, 'model', a, 'real', b); break
            diffs = [(o, hex(int.from_bytes(fe_m[o:o+4], "little")), hex(int.from_bytes(fe_r[o:o+4], "little"))) for o in range(0, STRIDE, 4) if fe_m[o:o+4] != fe_r[o:o+4]]
            print('  field diffs', diffs[:8])
print('C255 vectors', n, 'ticks', nticks, 'mismatches', bad)
fn = set(range(0x8007C174, 0x8007C768, 4))
print(' coverage', len(m.cov & fn), '/', len(fn), 'missing', [hex(a) for a in sorted(fn - m.cov)])
print(' journal event counts', dict(jcount))

import pickle
pickle.dump(RECORD, open('cache/c255_vectors.pkl','wb'))
print('recorded', len(RECORD))
