"""c255model.py - Python model of the C255 handler 0x8007C174 (item pickup object) and of A255 0x80061BD4. Externals are an object X with journaling methods."""
import models as M
from models import M32, s32
HERO = 0x80127D30
BUF = 0x80191268
MAPINFO_PTR = 0x800E4334
CONST_29BCC = 1          # word at 0x80029BCC in the image
# entity image accessors (dict-like over bytearray)
class Img:
    def __init__(self, raw): self.raw = raw
    def g(self, o): return int.from_bytes(self.raw[o:o+4], 'little')
    def gs(self, o): return s32(self.g(o))
    def s(self, o, v): self.raw[o:o+4] = (v & M32).to_bytes(4, 'little')
    def g16(self, o): return int.from_bytes(self.raw[o:o+2], 'little')

def c255(e, hero, X, thr_u16, word_99858, const_29bcc=1):
    """e, hero: Img. X: externals. Returns nothing; mutates e (and journals through X)."""
    item = (e.g(0x68) - 0x1E) & M32
    st274 = e.g(0x274)
    if st274 == 2:
        d = e.g(0x278)
        if d != 0:
            e.s(0x278, d - 1); return
        sub = e.gs(0x27C)
        if sub == 1:
            return _wait_cd(e, X)
        if sub < 2:
            if sub == 0:
                e.s(0xB8, 0)
                if X.busy(): return
                if e.g(0x284) != 0:
                    X.strcpy(('str', 0x4C)); X.strcat(('name', item)); X.strcat(('str', 0x4D))
                    if e.g(0x288) != 0:
                        e.s(0x27C, e.g(0x27C) + 1)
                        X.open(1); X.anim(4); return
                    e.s(0x27C, e.g(0x27C) + 2)
                    s0 = M.itab_b5(item) if item < 0x58 else None
                    if s0 == 0: X.strcat(('str', 0x46))
                    X.sound(s0); X.open(1); return
                e.s(0x27C, e.g(0x27C) + 2)
                X.strcpy(('str', 0x4E)); X.strcat(('name', item)); X.strcat(('str', 0x4F)); X.open(1); return
            return
        if sub == 2:
            if X.busy(): return
            chest = e.g(0x280)
            if e.g(0x284) != 0:
                X.apply(item); X.setflag(X.chest40(chest))
            else:
                X.chest88zero(chest)
            X.unblock(('ent', chest)); X.destroy_light(); return
        return
    # ---- loot / placed item path
    if e.g(0x144) != 0 and e.g(0x284) != 0:
        v = s32((e.g(0x284) * 3 * 4) & M32) >> 4
        e.s(0x284, v)
        if not (s32((thr_u16 << 8)) < v):
            e.s(0x284, 0)
        e.s(0xB8, e.g(0x284))
    d = e.gs(0x278)
    if not (((d + 1) & M32) < 2):
        d = d - 1
        e.s(0x278, d)
        if d == 0:
            X.destroy_light(); return
        if d == 0x78:
            e.s(0x1CC, 0x78)
    sub = e.gs(0x27C)
    if sub == 1:
        return _pickup(e, hero, X, item, word_99858, const_29bcc)
    if sub < 2:
        if sub == 0:
            if e.gs(0xB8) > 0: return
            e.s(0x27C, 1)
        return
    if sub == 2:
        return _wait_cd(e, X)
    if sub == 3:
        if X.busy(): return
        X.unblock(('self',)); X.destroy_light(); return

def _wait_cd(e, X):
    if X.cdbusy(): return
    e.s(0x27C, e.g(0x27C) + 1)
    X.anim(3); X.stopsound()

def _pickup(e, hero, X, item, word_99858, const_29bcc):
    if X.busy(): return
    for ax, sz in ((0x1D8, 0x1F0), (0x1DC, 0x1F4), (0x1E0, 0x1F8)):
        a1 = e.gs(ax); v1 = hero.gs(ax); a0 = s32(a1 - v1)
        if a0 >= 0:
            if not (a0 < s32(hero.g(sz) + 1)): return
        else:
            if not (s32(v1 - a1) < s32(e.g(sz) + 1)): return
    r = X.give(item)
    if r == 0 and e.g(0x274) != 0:
        X.bufclear()
    else:
        X.strcpy(('name', item)); X.strcat(('str', 0x45))
    X.setflag(e.g(0x280))
    s0 = M.itab_b5(item) if item < 0x58 else None
    if s0 != 0:
        X.sound(s0)
        if X.bufnonempty(): X.open(0)
        e.s(0x27C, e.g(0x27C) + 2); return
    if const_29bcc != 0 or word_99858 != 0:
        e.s(0x27C, e.g(0x27C) + 1)
        X.block(); X.bgm0(); X.cd(0xB)
        if X.bufnonempty(): X.open(1)
        X.anim(4); return
    X.strcat(('str', 0x46))
    if X.bufnonempty(): X.open(0)
    e.s(0x27C, e.g(0x27C) + 2)
