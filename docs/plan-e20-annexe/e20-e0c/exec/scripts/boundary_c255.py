"""gen_c255.py - E20.e0c T0 (f): the handler C255 (0x8007C174) of ALUN_CD.EXE (France) on the REAL code (MIPS interpreter) for the objects with +0x274 == 0 (placed) and 1 (dropped),
written as JSON vectors for the tests of the DLL (../vectors/c255.json). The chest (+0x274 == 2) is excluded (D-E0C-2).
Each vector is run twice: `full` (CdBusy as drawn) and `projected` (the fanfare as a stub, D-E0C-7: CdBusy always false; bgm0 + CD track 11 become one `fanfareStart`, 0x80049AF4 becomes
`fanfareEnd`). Externals stubbed with a journal: dialogue-open 0x80045004, string builders, message opener 0x800450F0, 0x8004507C (SetEtcAnimationMode, the close mask), sound 0x800490FC,
SetGameFlag 0x80032B28, GiveItem 0x80033F00, Block 0x8003AD30, Unblock 0x8003ADAC, 0x80049B7C (stop BGM), 0x8005A9E0 (CD track), CdBusy 0x8005A7D4, 0x80049AF4, light destroy 0x8003A774.
Run from a COPY of this folder."""
import json
import random
import sys

from lh import *
import models as M
from c255model import Img, HERO, BUF

random.seed(20261022)
E_ADDR = ENT0 + 9 * STRIDE
MAPINFO = 0x80140000
w = World()
m = w.m
for i in range(0x58):
    m.ww(ITEMTAB + 8 * i, 0xA0000000 + i)
m.ww(0x800E4334, MAPINFO)
m.ww(0x80029BCC, 1)
m.ww(0x8009A858, 0)

J = []
ST = dict(dialog=False, cdbusy=False, give=0)


def tagdec(v):
    v &= M32
    if 0x90000000 <= v < 0x90001000:
        return ['etc', v - 0x90000000]
    if 0xA0000000 <= v < 0xA0001000:
        return ['name', v - 0xA0000000]
    return ['raw', v]


def r2(mc, v):
    mc.r[2] = v


def install():
    h = m.hooks
    h[0x80045004] = lambda mc: (J.append(['dialogOpen?', int(ST['dialog'])]), r2(mc, 1 if ST['dialog'] else 0))
    h[0x800816D4] = lambda mc: r2(mc, 0x90000000 + (mc.r[4] & M32))
    def strcpy(mc):
        J.append(['strcpy'] + tagdec(mc.r[5])); mc.wb(mc.r[4], 1)
    def strcat(mc):
        J.append(['strcat'] + tagdec(mc.r[5]))
    h[0x80081FB8] = strcpy
    h[0x80081EAC] = strcat
    h[0x800450F0] = lambda mc: J.append(['open', mc.r[5] & M32])
    h[0x8004507C] = lambda mc: J.append(['closeMask', mc.r[4] & M32])
    h[0x800490FC] = lambda mc: J.append(['sound', mc.r[4] & M32])
    h[0x80032B28] = lambda mc: J.append(['setGameFlag', mc.r[4] & M32])
    h[0x80033F00] = lambda mc: (J.append(['give', mc.r[5] & M32, ST['give']]), r2(mc, ST['give']))
    h[0x80033DBC] = lambda mc: J.append(['applyItem', mc.r[5] & M32])
    h[0x8003AD30] = lambda mc: J.append(['block'])
    h[0x8003ADAC] = lambda mc: J.append(['unblock', 'self' if (mc.r[4] & M32) == E_ADDR else mc.r[4] & M32])
    h[0x80049B7C] = lambda mc: J.append(['bgmStop', mc.r[4] & M32])
    h[0x8005A9E0] = lambda mc: J.append(['cdTrack', mc.r[4] & M32])
    h[0x8005A7D4] = lambda mc: (J.append(['cdBusy?', int(ST['cdbusy'])]), r2(mc, 1 if ST['cdbusy'] else 0))
    h[0x80049AF4] = lambda mc: J.append(['fanfareStop'])
    h[0x8003A774] = lambda mc: J.append(['destroyLight'])


def put(addr, img):
    for i, b in enumerate(img):
        m.wb(addr + i, b)


def get(addr):
    return bytes(m.ram[addr & 0x1FFFFFFF:(addr & 0x1FFFFFFF) + STRIDE])


install()

KEYS = [0x04, 0x05, 0x14, 0x4E, 0x53, 0x40, 0x2B, 0x1F, 0x3E, 0x12, 0x08, 0x10]       # octet 5 == 0 (key objects) among the placed ones
NONKEYS = [0x24, 0x25, 0x26, 0x27, 0x29, 0x46, 0x47, 0x51, 0x52, 0x55, 0x45, 0x48, 0x54, 0x56, 0x3D, 0x21]    # octet 5 != 0
for it in KEYS:
    assert M.itab_b5(it) == 0, hex(it)
for it in NONKEYS:
    assert M.itab_b5(it) != 0, hex(it)

vectors = []


def box(rng, near):
    return rng.randrange(-300, 300) << 16 if near else rng.randrange(-2000, 2000) << 16


def make(vid, item, kind, state, rng):
    """kind 0 = placed, 1 = dropped; state = +0x27C start"""
    e = bytearray(STRIDE); E = Img(e)
    E.s(0x68, item + 0x1E)
    E.s(0x274, kind)
    if kind == 1:
        E.s(0x278, rng.choice([M32, M32, 600, 601, 122, 121, 120, 119, 5, 2, 1, 0]))
        E.s(0x280, rng.choice([0, 0, 0x55, 0x8003]))
        E.s(0x284, rng.choice([0xA0000, 0x78000, 0x5A000, 0x9029, 0x1000, 0, 0xC037]))
    else:
        E.s(0x278, rng.choice([M32, 0, 0, 5, 1]))                 # the placed one: +0x278 = +0x40 (flag), usually 0
        E.s(0x280, M32)
        E.s(0x284, 0)
    E.s(0x27C, state)
    E.s(0xB8, rng.choice([0, 0xA0000, 0x10000, 0x1000, E.g(0x284)]))
    E.s(0x144, rng.choice([0, 1, 1]))
    E.s(0x1CC, rng.choice([0, 0, 5]))
    hero = bytearray(STRIDE); H = Img(hero)
    overlap = rng.random() < 0.9
    for ax, sz in ((0x1D8, 0x1F0), (0x1DC, 0x1F4), (0x1E0, 0x1F8)):
        hs = rng.choice([0, 1, 5, 0xFFFF, 0x1FFFF, 0xFFFFF, 0x3FFFF])
        es = rng.choice([0, 1, 5, 0xFFFF, 0x1FFFF, 0xFFFFF, 0x3FFFF])
        hp = rng.randrange(-1000, 1000) << 8
        E.s(sz, es)
        H.s(sz, hs)
        H.s(ax, hp)
        if overlap:
            d = rng.choice([0, 0, 1, -1, s32(hs), s32(hs) + 1, s32(hs) + 2, -s32(es), -(s32(es) + 1), -(s32(es) + 2), rng.randrange(-min(es, 0x20000) if es else 0, (hs if hs < 0x20000 else 0x20000) + 1)])
        else:
            d = rng.choice([0x1000000, -0x1000000, 0x400000])
        E.s(ax, hp + d)
    thr = rng.choice([128, 128, 128, 3, 0, 1, 8, 0x10, 0xFFFF])
    K = rng.choice([1, 2, 3, 4, 6, 8, 12])
    dialog = [(rng.random() < 0.2) if t > 0 else (rng.random() < 0.12) for t in range(K)]
    cd = [rng.random() < 0.35 for _ in range(K)]
    give = rng.choice([0, 0, 0, 1]) if kind == 0 else 0
    ground = [rng.choice([None, None, 0, 1]) for _ in range(K)]
    return dict(id=vid, item=item, placed=(kind == 0), e=bytes(e), h=bytes(hero), thr=thr, K=K, dialog=dialog, cd=cd, give=give, ground=ground)


def run(v, projected):
    put(E_ADDR, v['e'])
    put(HERO, v['h'])
    m.ww(MAPINFO + 4, v['thr'])
    m.wh(MAPINFO + 4, v['thr'])
    m.wb(BUF, 0)
    ST['give'] = v['give']
    ticks = []
    for t in range(v['K']):
        ST['dialog'] = v['dialog'][t]
        ST['cdbusy'] = False if projected else v['cd'][t]
        J.clear()
        w.call(0x8007C174, E_ADDR)
        j = [list(x) for x in J]
        if projected:
            out = []
            for ev in j:
                if ev[0] in ('cdBusy?',):
                    continue
                if ev[0] == 'bgmStop':
                    out.append(['fanfareStart'])
                    continue
                if ev[0] == 'cdTrack':
                    continue
                if ev[0] == 'fanfareStop':
                    out.append(['fanfareEnd'])
                    continue
                out.append(ev)
            j = out
        img = get(E_ADDR)
        e = Img(bytearray(img))
        after = dict(w278=e.g(0x278), s27C=e.g(0x27C), w284=e.g(0x284), forceZ=e.g(0xB8), f1CC=e.g(0x1CC), onGround=e.g(0x144))
        ticks.append(dict(journal=j, after=after))
        destroyed = any(ev[0] == 'destroyLight' for ev in j)
        if v['ground'][t] is not None and not destroyed:
            m.ww(E_ADDR + 0x144, v['ground'][t])
        if destroyed:
            break
    return ticks


def bounce(speed, thr, ground=1):
    e = bytearray(STRIDE); E = Img(e)
    E.s(0x68, 0x45 + 0x1E); E.s(0x274, 1); E.s(0x278, M32); E.s(0x280, 0); E.s(0x284, speed)
    E.s(0x27C, 1); E.s(0xB8, 0); E.s(0x144, ground)
    for ax, sz in ((0x1D8, 0x1F0), (0x1DC, 0x1F4), (0x1E0, 0x1F8)):
        E.s(ax, 0x1000000); E.s(sz, 1)
    hero = bytearray(STRIDE)
    v = dict(e=bytes(e), h=bytes(hero), thr=thr, K=1, dialog=[False], cd=[False], give=0, ground=[None])
    t = run(v, True)[0]['after']
    return t['w284'], t['forceZ']

for thr in (128, 3):
    base = thr << 8
    # speed s -> v = (s*12)>>4 ; find the speeds around v == base
    for s in range(0, 1 << 20):
        if (s * 12) >> 4 == base:
            break
    for ds in (-2, -1, 0, 1, 2, 3):
        sp = s + ds
        v = (sp * 12) >> 4
        print('thr', thr, 'speed', hex(sp), 'v', hex(v), 'vs', hex(base), 'result w284/forceZ', [hex(x) for x in bounce(sp, thr)])
