import random, sys, h2
from h2 import *
import exelib
seed = int(sys.argv[1]) if len(sys.argv) > 1 else 5
NV = int(sys.argv[2]) if len(sys.argv) > 2 else 8000
rng = random.Random(seed)
m = mk()
HERO = ENT0
E = ENT0 + 9*STRIDE
CHEST = ENT0 + 3*STRIDE
MAPINFO = 0x80140000
BUF = 0x80191268
B5 = [exelib.u8(ITEMTAB + 8*i + 5) for i in range(0x62)]
m.ww(0x800E4334, MAPINFO)
for i in range(0x62): m.ww(ITEMTAB + 8*i, 0xA0000000 + i)     # name tag
J = []
CFG = dict(dialog=[0], cd=0, give=0, di=0)

def nxt(lst):
    v = lst[CFG['di'] % len(lst)]; CFG['di'] += 1; return v

def tag(v):
    v &= M32
    if 0xA0000000 <= v < 0xA0001000: return ('name', v - 0xA0000000)
    if 0x90000000 <= v < 0x90001000: return ('str', v - 0x90000000)
    return ('raw', v)

def setr(mc, v): mc.r[2] = v & M32

def dialog(mc):
    J.append(('dialog',)); mc.r[2] = nxt(CFG['dialog'])
m.hooks[0x80045004] = dialog
m.hooks[0x800816D4] = lambda mc: setr(mc, 0x90000000 + (mc.r[4] & M32))

def strcpy(mc):
    J.append(('strcpy', tag(mc.r[5]))); mc.wb(mc.r[4], 1)

def strcat(mc):
    J.append(('strcat', tag(mc.r[5])))
m.hooks[0x80081FB8] = strcpy
m.hooks[0x80081EAC] = strcat
m.hooks[0x800450F0] = lambda mc: J.append(('open', mc.r[4] & M32, mc.r[5] & M32))
m.hooks[0x800490FC] = lambda mc: J.append(('sound', mc.r[4] & M32))

def give(mc):
    J.append(('give', mc.r[4] & M32, mc.r[5] & M32)); mc.r[2] = CFG['give']
m.hooks[0x80033F00] = give
m.hooks[0x80033DBC] = lambda mc: J.append(('apply', mc.r[4] & M32, mc.r[5] & M32))
m.hooks[0x80032B28] = lambda mc: J.append(('setflag', mc.r[4] & M32))
m.hooks[0x8003AD30] = lambda mc: (J.append(('block', mc.r[4] & M32)), setr(mc, 0))
m.hooks[0x8003ADAC] = lambda mc: (J.append(('unblock', mc.r[4] & M32)), setr(mc, 0))
m.hooks[0x80049B7C] = lambda mc: J.append(('bgm', mc.r[4] & M32))
m.hooks[0x8005A9E0] = lambda mc: J.append(('cdplay', mc.r[4] & M32))
m.hooks[0x8004507C] = lambda mc: J.append(('anim', mc.r[4] & M32))

def cdbusy(mc):
    J.append(('cdbusy',)); mc.r[2] = CFG['cd']
m.hooks[0x8005A7D4] = cdbusy
m.hooks[0x80049AF4] = lambda mc: J.append(('af4',))
m.hooks[0x8003A774] = lambda mc: J.append(('destroy_light', mc.r[4] & M32))

# ---------- the model, written from the disassembly -------------------------------
class Img:
    def __init__(s, raw): s.raw = bytearray(raw)
    def g(s, o): return int.from_bytes(s.raw[o:o+4], 'little')
    def gs(s, o): return s32(s.g(o))
    def s(s, o, v): s.raw[o:o+4] = (v & M32).to_bytes(4, 'little')

def cdwait(e, j, cdv):
    j.append(('cdbusy',))
    if cdv: return
    e.s(0x27C, e.g(0x27C) + 1); j.append(('anim', 3)); j.append(('af4',))

def model(e_raw, hero_raw, chest_raw, mapgrav, dlg_seq, cdv, giveret, g29, g9a, bufnz):
    e = Img(e_raw); hero = Img(hero_raw); chest = Img(chest_raw)
    j = []; di = [0]; buf = [bufnz]
    def dlg():
        j.append(('dialog',)); v = dlg_seq[di[0] % len(dlg_seq)]; di[0] += 1; return v
    def strcpy_(t): j.append(('strcpy', t)); buf[0] = 1
    def strcat_(t): j.append(('strcat', t))
    item = e.gs(0x68) - 0x1E
    name = ('name', item)
    def S(n): return ('str', n)
    def ret(): return e, j, buf[0], chest
    k = e.gs(0x274)
    if k == 2:
        d = e.gs(0x278)
        if d != 0:
            e.s(0x278, d - 1); return ret()
        st = e.gs(0x27C)
        if st == 1:
            cdwait(e, j, cdv); return ret()
        if st < 2:
            if st != 0: return ret()
            e.s(0xB8, 0)
            if dlg(): return ret()
            if e.g(0x284) != 0:
                strcpy_(S(0x4c)); strcat_(name); strcat_(S(0x4d))
                if e.g(0x288) != 0:
                    e.s(0x27C, e.g(0x27C) + 1); j.append(('open', 'BUF', 1)); j.append(('anim', 4)); return ret()
                else:
                    e.s(0x27C, e.g(0x27C) + 2); s0 = B5[item]
                    if s0 == 0: strcat_(S(0x46))
                    j.append(('sound', s0)); j.append(('open', 'BUF', 1)); return ret()
            else:
                e.s(0x27C, e.g(0x27C) + 2)
                strcpy_(S(0x4e)); strcat_(name); strcat_(S(0x4f)); j.append(('open', 'BUF', 1)); return ret()
        if st == 2:
            if dlg(): return ret()
            if e.g(0x284) != 0:
                j.append(('apply', 'HERO', item)); j.append(('setflag', chest.g(0x40)))
            else:
                chest.s(0x88, 0)
            j.append(('unblock', 'CHESTPTR')); j.append(('destroy_light', 'E'))
            return ret()
        return ret()
    # dropped / placed
    if e.g(0x144) != 0 and e.g(0x284) != 0:
        v = s32((e.gs(0x284) * 12) & M32) >> 4
        e.s(0x284, v)
        if not (mapgrav << 8) < v: e.s(0x284, 0)
        e.s(0xB8, e.g(0x284))
    d = e.gs(0x278)
    if ((d + 1) & M32) >= 2:
        d = d - 1; e.s(0x278, d)
        if d == 0:
            j.append(('destroy_light', 'E')); return ret()
        if d == 0x78: e.s(0x1CC, 0x78)
    st = e.gs(0x27C)
    if st == 1:
        if dlg(): return ret()
        for ax, sz in ((0x1D8, 0x1F0), (0x1DC, 0x1F4), (0x1E0, 0x1F8)):
            a = s32((e.g(ax) - hero.g(ax)) & M32)
            if a >= 0:
                if not (a < s32((hero.g(sz) + 1) & M32)): return ret()
            else:
                dd = s32((hero.g(ax) - e.g(ax)) & M32)
                if not (dd < s32((e.g(sz) + 1) & M32)): return ret()
        j.append(('give', 'HERO', item))
        if giveret == 0 and e.g(0x274) != 0:
            buf[0] = 0
        else:
            strcpy_(name); strcat_(S(0x45))
        j.append(('setflag', e.g(0x280)))
        s0 = B5[item]
        if s0 == 0:
            if g29 != 0 or g9a != 0:
                e.s(0x27C, e.g(0x27C) + 1)
                j.append(('block', 'E')); j.append(('bgm', 0)); j.append(('cdplay', 0xB))
                if buf[0]: j.append(('open', 'BUF', 1))
                j.append(('anim', 4)); return ret()
            strcat_(S(0x46))
        else:
            j.append(('sound', s0))
        if buf[0]: j.append(('open', 'BUF', 0))
        e.s(0x27C, e.g(0x27C) + 2)
        return ret()
    if st < 2:
        if st == 0:
            if e.gs(0xB8) >= 0: return ret()
            e.s(0x27C, 1)
        return ret()
    if st == 2:
        cdwait(e, j, cdv); return ret()
    if st == 3:
        if dlg(): return ret()
        j.append(('unblock', 'E')); j.append(('destroy_light', 'E'))
    return ret()

def norm(j):
    out = []
    for t in j:
        t = list(t)
        for i, x in enumerate(t):
            if x == E: t[i] = 'E'
            elif x == HERO: t[i] = 'HERO'
            elif x == CHEST: t[i] = 'CHESTPTR'
            elif x == BUF: t[i] = 'BUF'
        out.append(tuple(t))
    return out

def rand_ent():
    I = Img(bytearray(STRIDE)); img = I.raw
    item = rng.choice(list(range(0, 0x58)) + [0x45, 0x46, 0x47, 0x54, 0x55, 0x24, 0x4F, 0x50, 0x51, 0x53, 0x56, 0x25, 0x26] * 3)
    I.s(0x68, item + 0x1E)
    I.s(0x274, rng.choice([0, 1, 1, 1, 1, 2, 2, 3]))
    I.s(0x278, rng.choice([M32, 0, 1, 2, 3, 0x78, 0x79, 0x7A, 600, 5, rng.getrandbits(32), 0xFFFFFFFE]))
    I.s(0x27C, rng.choice([0, 0, 1, 1, 1, 2, 3, 4, M32, 5]))
    I.s(0x280, rng.choice([0, M32, CHEST, rng.getrandbits(32)]))
    if I.g(0x274) == 2: I.s(0x280, CHEST)
    I.s(0x284, rng.choice([0, 0xA0000, 0x78000, 0x5A000, 0x100, 0x800, 0x1000, 0x2000, rng.getrandbits(32), (-0x50000) & M32]))
    I.s(0x288, rng.choice([0, 0, 1]))
    I.s(0xB8, rng.choice([0, 0xA0000, 0x10000, (-0x10000) & M32, 1, M32]))
    I.s(0x144, rng.choice([0, 1, 1]))
    I.s(0x1CC, rng.choice([0, 5, 0x78]))
    for ax, sz in ((0x1D8, 0x1F0), (0x1DC, 0x1F4), (0x1E0, 0x1F8)):
        I.s(ax, rng.randrange(-40, 40) << 16 if rng.random() < .5 else rng.randrange(-300000, 300000))
        I.s(sz, rng.choice([0, 1, 0xFFFF, 0x1FFFF, 0xFFFFF, 0x20000, 0x3FFFF, rng.randrange(0, 0x100000)]))
    return img

def rand_hero(e_raw):
    I = Img(bytearray(STRIDE)); img = I.raw; Eim = Img(e_raw)
    for ax, sz in ((0x1D8, 0x1F0), (0x1DC, 0x1F4), (0x1E0, 0x1F8)):
        near = rng.random() < .8
        base = Eim.gs(ax)
        I.s(ax, (base + (rng.randrange(-0x40000, 0x40000) if near else rng.randrange(-0x2000000, 0x2000000))) & M32)
        I.s(sz, rng.choice([0, 1, 0xFFFF, 0x1FFFF, 0xFFFFF, 0x20000, 0x3FFFF, rng.randrange(0, 0x100000)]))
    return img

def put(addr, raw):
    for i, b in enumerate(raw): m.wb(addr + i, b)

def get(addr): return bytes(m.ram[addr & 0x1FFFFFFF:(addr & 0x1FFFFFFF) + STRIDE])

bad = 0
for it in range(NV):
    er = rand_ent(); hr = rand_hero(er)
    CI = Img(bytearray(STRIDE)); CI.s(0x40, rng.getrandbits(32)); CI.s(0x88, rng.getrandbits(32)); chest = CI.raw
    for tick in range(rng.choice([1, 1, 2, 3, 6])):
        grav = rng.choice([3, 128, 128, 0, 255, 0x8000, 0xFFFF])
        dlg_seq = [rng.choice([0, 0, 0, 1])] if rng.random() < .7 else [rng.choice([0, 1]) for _ in range(3)]
        cdv = rng.choice([0, 1]); giveret = rng.choice([0, 1]); g29 = rng.choice([1, 1, 1, 0]); g9a = rng.choice([0, 0, 1])
        bufnz = rng.choice([0, 1])
        J.clear(); CFG.update(dialog=dlg_seq, cd=cdv, give=giveret, di=0)
        put(E, er); put(HERO, hr); put(CHEST, bytes(chest))
        m.wh(MAPINFO + 4, grav); m.ww(0x80029BCC, g29); m.ww(0x8009A858, g9a); m.wb(BUF, bufnz)
        m.tracing = True; m.call(0x8007C174, E); m.tracing = False
        real = (get(E), norm(J), m.rb(BUF) != 0, get(CHEST))
        mod = model(er, hr, bytes(chest), grav, dlg_seq, cdv, giveret, g29, g9a, bufnz)
        mod = (bytes(mod[0].raw), norm(mod[1]), bool(mod[2]), bytes(mod[3].raw))
        if real[0] != mod[0] or real[1] != mod[1] or real[2] != mod[2] or real[3] != mod[3]:
            bad += 1
            if bad <= 5:
                print('DIFF seed', seed, 'it', it, 'tick', tick, 'k', Img(er).g(0x274), 'st', Img(er).gs(0x27C))
                print(' real J', real[1]); print(' mod  J', mod[1])
                diffs = [(hex(o), real[0][o], mod[0][o]) for o in range(STRIDE) if real[0][o] != mod[0][o]][:6]
                print(' ent diffs', diffs, 'buf', real[2], mod[2])
        er = real[0]; chest = bytearray(real[3])
        if rng.random() < .3: hr = rand_hero(er)
print('seed', seed, 'vectors', NV, 'diffs', bad)
ws = list(range(0x8007C174, 0x8007C768, 4)); hit = [a for a in ws if a in m.cov]
print('cov', len(hit), len(ws), [hex(a) for a in ws if a not in m.cov])
