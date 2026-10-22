"""run_c255.py - directed scenarios of C255 (0x8007C174) and A255 (0x80061BD4) run on the REAL code of ALUN_CD.EXE (France)
in the annex's MIPS interpreter (copied to this scratch folder). Read-only on the binary. Prints the journal of each tick.
Stubs (journal only): dialogue-open flag 0x80045004, string builders, message opener 0x800450F0, SetEtcAnimationMode 0x8004507C,
sound 0x800490FC, SetGameFlag 0x80032B28, GiveItem 0x80033F00, Block 0x8003AD30, Unblock 0x8003ADAC, bgm 0x80049B7C, CD 0x8005A9E0,
CdBusy 0x8005A7D4, 0x80049AF4, light destroy 0x8003A774."""
import sys
from lh import *
import models as M
from c255model import Img, HERO, BUF

E_ADDR = ENT0 + 9 * STRIDE
MAPINFO = 0x80140000

w = World()
m = w.m
for i in range(0x58):
    m.ww(ITEMTAB + 8 * i, 0xA0000000 + i)   # name pointer = tag (the real table is filled at load time)
m.ww(0x800E4334, MAPINFO)

J = []


def tagdec(v):
    v &= M32
    if 0x90000000 <= v < 0x90001000:
        return 'etc%#x' % (v - 0x90000000)
    if 0xA0000000 <= v < 0xA0001000:
        return 'name%#x' % (v - 0xA0000000)
    return 'raw%#x' % v


STATE = dict(dialog=0, cdbusy=0, give=0, buf=False)


def setr2(mc, v):
    mc.r[2] = v


def install():
    h = m.hooks
    h[0x80045004] = lambda mc: (J.append('dialogOpen?=%d' % STATE['dialog']), setr2(mc, STATE['dialog']))
    h[0x800816D4] = lambda mc: setr2(mc, 0x90000000 + (mc.r[4] & M32))
    def strcpy(mc):
        J.append('strcpy(%s)' % tagdec(mc.r[5])); mc.wb(mc.r[4], 1)
    def strcat(mc):
        J.append('strcat(%s)' % tagdec(mc.r[5]))
    h[0x80081FB8] = strcpy
    h[0x80081EAC] = strcat
    h[0x800450F0] = lambda mc: J.append('OPEN_MESSAGE(mode=%d)' % (mc.r[5] & M32))
    h[0x8004507C] = lambda mc: J.append('SetEtcAnimationMode(%d)' % (mc.r[4] & M32))
    h[0x800490FC] = lambda mc: J.append('sound(%d)' % (mc.r[4] & M32))
    h[0x80032B28] = lambda mc: J.append('SetGameFlag(%#x)' % (mc.r[4] & M32))
    h[0x80033F00] = lambda mc: (J.append('GiveItem(item=%#x) -> %d' % (mc.r[5] & M32, STATE['give'])), setr2(mc, STATE['give']))
    h[0x80033DBC] = lambda mc: J.append('ApplyItem(%#x)' % (mc.r[5] & M32))
    h[0x8003AD30] = lambda mc: J.append('Block(self)')
    h[0x8003ADAC] = lambda mc: J.append('Unblock(%s)' % ('self' if (mc.r[4] & M32) == E_ADDR else '%#x' % (mc.r[4] & M32)))
    h[0x80049B7C] = lambda mc: J.append('Bgm(%d)' % (mc.r[4] & M32))
    h[0x8005A9E0] = lambda mc: J.append('CD_PlayTrack(%d)' % (mc.r[4] & M32))
    h[0x8005A7D4] = lambda mc: (J.append('CdBusy?=%d' % STATE['cdbusy']), setr2(mc, STATE['cdbusy']))
    h[0x80049AF4] = lambda mc: J.append('StopFanfareFade()')
    h[0x8003A774] = lambda mc: J.append('DestroyLight(self)')


def put(addr, img):
    for i, b in enumerate(img):
        m.wb(addr + i, b)


def get(addr):
    return bytes(m.ram[addr & 0x1FFFFFFF:(addr & 0x1FFFFFFF) + STRIDE])


def scenario(title, item, st274, f278, f27c, f280, f284, onground, hero_overlap, give, cdbusy_seq, dialog_seq, ticks, grav=128, f40=0):
    print('=== ' + title)
    e = bytearray(STRIDE); E = Img(e)
    E.s(0x68, item + 0x1E)
    E.s(0x274, st274); E.s(0x278, f278); E.s(0x27C, f27c); E.s(0x280, f280); E.s(0x284, f284); E.s(0x144, onground)
    E.s(0x40, f40)
    hh = bytearray(STRIDE); H = Img(hh)
    for ax, sz in ((0x1D8, 0x1F0), (0x1DC, 0x1F4), (0x1E0, 0x1F8)):
        E.s(ax, 0); E.s(sz, 0x10000)
        H.s(ax, 0 if hero_overlap else 0x100000); H.s(sz, 0x10000)
    put(E_ADDR, e); put(HERO, hh)
    m.ww(MAPINFO + 4, grav); m.wh(MAPINFO + 4, grav)
    m.ww(0x8009A858, 0); m.ww(0x80029BCC, 1)
    m.wb(BUF, 0)
    STATE['give'] = give
    ci = 0; di = 0
    for t in range(ticks):
        STATE['cdbusy'] = cdbusy_seq[min(t, len(cdbusy_seq) - 1)]
        STATE['dialog'] = dialog_seq[min(t, len(dialog_seq) - 1)]
        J.clear()
        w.call(0x8007C174, E_ADDR)
        f = Img(bytearray(get(E_ADDR)))
        print('tick %d: %s | +0x27C=%d +0x278=%d +0x1CC=%d' % (t, ', '.join(J) if J else '-', f.gs(0x27C), f.gs(0x278), f.gs(0x1CC)))
        if any(x.startswith('DestroyLight') for x in J):
            break


install()
# 1. dropped heart (0x54, sound 52, 600-rule, GiveItem returns 0): no message, no state 2
scenario('dropped medium heart 0x55 overlapping the hero, state 1', 0x55, 1, 400, 1, 0, 0, 0, True, 0, [0], [0], 4)
# 2. posed Medium heart (+0x274 = 0), A255 done (+0x280=-1, +0x278=0 flag, +0x284=0), overlapping
scenario('posed medium heart 0x55 (flag 0), overlapping', 0x55, 0, 0, 1, M32, 0, 1, True, 0, [0], [0], 5)
# 3. posed key object (Sword 0x04, sound 0 -> fanfare), GiveItem returns 1 (octet4 == 0), the fanfare busy for 3 ticks, the dialog open for 4 ticks after the pickup
scenario('posed key object 0x04, fanfare busy 3 ticks, dialogue open from tick 2 to 6', 0x04, 0, 0, 1, M32, 0, 1, True, 1, [0, 0, 1, 1, 1, 0, 0, 0, 0], [0, 0, 1, 1, 1, 1, 1, 0, 0, 0], 12)
# 4. posed 5 Gildas (0x46, sound 53, b4=1 so GiveItem rets 0)
scenario('posed 5 gildas 0x46', 0x46, 0, 0, 1, M32, 0, 1, True, 0, [0], [0], 5)
# 5. posed herb, GiveItem returns 0 (b4 = 1)
scenario('posed herb 0x24', 0x24, 0, 0, 1, M32, 0, 1, True, 0, [0], [0], 5)
# 6. a dialogue open at the pickup tick: nothing happens until it closes
scenario('posed heart, dialogue open for 3 ticks first', 0x55, 0, 0, 1, M32, 0, 1, True, 0, [0], [1, 1, 1, 0], 6)
# 7. state 0 -> 1 for a posed object with ForceZ 0
scenario('posed object at state 0 (ForceZ 0)', 0x55, 0, 0, 0, M32, 0, 1, True, 0, [0], [0], 3)
# 8. posed object with a game flag in +0x278 (A255 copies +0x40 into +0x278): the countdown!
scenario('posed object whose flag +0x40 = 5 (so +0x278 = 5): the countdown destroys it', 0x55, 0, 5, 1, M32, 0, 1, False, 0, [0], [0], 8)
