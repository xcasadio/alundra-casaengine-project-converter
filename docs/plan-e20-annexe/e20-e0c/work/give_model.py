"""give_model.py - model of GiveItem 0x80033F00 for the items that the loot tables can produce (0x24, 0x45-0x48, 0x51, 0x54, 0x55, 0x56) and 0x4F, 0x26."""
import models as M
from models import M32, s32, lcg
def div4(x):   # (x>=0? x : x+3) >> 2 (arithmetic)
    x = s32(x); return (x if x >= 0 else x + 3) >> 2
def clamp_hp(cur, mx): return mx if mx < cur else cur   # slt a1,v1 -> if (max < new) new = max (signed)
def give(item, st):
    """st: dict hp, hpmax, money, mp, mpmax, pending, inv{item:count}, invmax{item:max}, rng, effects(list journal), fx_full(bool), hero_pos=(x,y,z).
    returns retval; mutates st; appends to st['j'] events"""
    j = st['j']
    def effect(anim, sparkle_rng):
        j.append(('effect', 0, 0xE, anim, st['hx'], st['hy'], (st["hz"] + 0x100000) & M32))
        if st['fx_full']:
            return None
        return {}
    if item in (0x45, 0x46, 0x47, 0x48):
        n = {0x45: 1, 0x46: 5, 0x47: 10, 0x48: 30}[item]
        v = s32(st['money'] + n)
        st['money'] = 9999 if v >= 0x2710 else (0 if v < 0 else v)    # 0x8004DF80
    elif item == 0x4F:
        v = (st['pending'] + 1) & 0xFFFF
        st['pending'] = 0x32 if s32(v if v < 0x8000 else v - 0x10000) >= 0x33 else v
        st['bit400'] = True
    elif item == 0x54:
        st['hp'] = clamp_hp(st['hp'] + 2, st['hpmax'])
    elif item == 0x55:
        amt = div4(st['hpmax']); amt = amt if amt > 0 else 1
        st['hp'] = clamp_hp(st['hp'] + amt, st['hpmax'])
        fx = effect(0, False)
        if fx is not None: j.append(('fx5c', 0x20000))
    elif item == 0x56:
        mx = st['hpmax']; amt = (mx + ((mx & M32) >> 31)) >> 1
        amt = amt if amt >= 5 else 5
        st['hp'] = clamp_hp(st['hp'] + amt, st['hpmax'])
        for k in range(4):
            fx = effect(0, True)
            if fx is not None:
                s = st['rng']; v1 = lcg(s); a1 = lcg(v1)
                vx = ((v1 * 0x20001) >> 32) + 0xFFFF0000
                vy = ((a1 * 0x30001) >> 32) + 0xFFFE8000
                j.append(('fxvel', 0x20000, vx & M32, vy & M32)); st['rng'] = a1
    elif item in (0x51, 0x26):
        # 0x8004DF10(1): AddMp(1)
        v = s32(st['mp'] + 1)
        st['mp'] = st['mpmax'] if st['mpmax'] < v else (0 if v < 0 else v)
        for k in range(4):
            fx = effect(2, True)
            if fx is not None:
                s = st['rng']; v1 = lcg(s); a1 = lcg(v1)
                vx = ((v1 * 0x20001) >> 32) + 0xFFFF0000
                vy = ((a1 * 0x30001) >> 32) + 0xFFFE8000
                j.append(('fxvel', 0x20000, vx & M32, vy & M32)); st['rng'] = a1
    elif item == 0x24:
        before = st['inv'].get(item, 0)
        mx = st['invmax'][item]
        after = before if before == mx else before + 1
        st['inv'][item] = after
        if not (before < after):
            # inventory full: use at once: hp += max(5, hpmax/4) ; effect anim 0 with +0x5C
            amt = div4(st['hpmax']); amt = amt if amt >= 5 else 5
            st['hp'] = clamp_hp(st['hp'] + amt, st['hpmax'])
            fx = effect(0, False)
            if fx is not None: j.append(('fx5c', 0x20000))
    return 1 if M.itab_b4(item) == 0 else 0
