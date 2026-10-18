"""model.py - D2: an independent Python model of the hit pass and of the damage routine, written from section 0.2.5 of docs/plan-e20-heros.md
(and the arithmetic read in out/dis_hits.txt), used ONLY to cross-check the real code on random sets.  It shares nothing with the emulator
except the memory image it dereferences sheet and attack pointers in.

A "spec" is a dict slot -> dict of the keys of hitworld.FIELDS (status, ctl20, flags, animflags, c1cc, c1d0, box, atk, sheet, mp, tsize, hb, asize, hp).
"""
M32 = 0xFFFFFFFF
LCG_M = 0x7D2B89DD
LCG_C = 0xE06A02E7


def s32(x):
    x &= M32
    return x - (1 << 32) if x & 0x80000000 else x


def lcg(x):
    return (x * LCG_M + LCG_C) & M32


def fan_mask(f):
    """class mask of the targets an attacker with flags f can hit (0x80039398-0x800393BC): HitsClassA -> ClassA, HitsClassB -> ClassB, HitsClassC -> ClassC"""
    return ((f >> 2) & 1) | ((f << 2) & 8) | (0x800 if f & 0x1000 else 0)


def axis_hit(hb, mp, tsize, asize):
    """overlap on one axis, raw 16.16: d = attacker box - target ModdedPos;  d >= 0 needs d < tsize + 1;  d < 0 needs -d < asize + 1  (signed 32-bit)"""
    d = s32(hb - mp)
    if d >= 0:
        return d < s32(tsize + 1)
    return s32(mp - hb) < s32(asize + 1)


def centre(hb, mp, tsize, asize):
    """centre of the intersection on one axis: ((max of lower bounds) + (min of upper bounds)) / 2, truncated toward zero (0x80039684-0x80039744)"""
    lo = mp if hb < mp else hb
    up_a = s32(hb + asize)
    up_t = s32(mp + tsize)
    hi = up_a if up_a < up_t else up_t
    t = s32(lo + hi)
    return -((-t) // 2) if t < 0 else t // 2


class Mem:
    def __init__(self, ram):
        self.ram = ram

    def u8(self, a):
        return self.ram[a & 0x1FFFFFFF]


def resolve(mem, atk, sheet, hp, equip, g_anim):
    """ResolveBalanceTarget 0x8004464C as section 0.2.5 describes it: returns the new HP"""
    if sheet == 0 or atk == 0:
        return hp
    attr = mem.u8(atk)
    if attr == 0:
        return hp
    r = mem.u8(sheet + (attr & 0xF) + 2)
    top = r & 0xC0
    if top == 0x40:
        return 0
    if top != 0:
        return hp
    power = mem.u8(atk + 1)
    if attr & 0x80:
        for k in range(3):
            sh = equip[k]
            if sh == 0:
                continue
            cnt = mem.u8(sh + 0xE)
            if cnt == 0:
                continue
            a = sh + 0xF + 2 * (g_anim + 1) if (g_anim + 1) < cnt else sh + 0xF
            power += mem.u8(a + 1)
    dmg = (power * r) >> 4
    if dmg == 0:
        dmg = 1
    return hp - dmg if dmg < hp else 0


def run(specs, ram, hero_anim, rng_seed, equip, hero_sheet_addr, hero_slot=0):
    """the tick part this oracle covers: list build, hit pass, then ComputeNewHp for every entity left with a TouchingEntity (ascending slot order).
    returns (state per slot, effect events, final rng, latched anim)"""
    mem = Mem(ram)
    slots = sorted(specs)
    active = [i for i in slots if specs[i]['status'] in (2, 3) and specs[i]['ctl20'] == 0]
    st = {i: dict(touching=None, c1d0=specs[i].get('c1d0', 0), hit=0, hp=specs[i].get('hp', 0)) for i in slots}
    ev = []
    rng = rng_seed
    for ai in active:
        A = specs[ai]
        if not A['box'] or not A['atk']:
            continue
        attr = mem.u8(A['atk'])
        if attr == 0:
            continue
        fan = fan_mask(A['flags'])
        if fan == 0:
            continue
        for ti in active:
            if ti == ai:
                continue
            T = specs[ti]
            if st[ti]['c1d0'] or T['c1cc'] or (T['animflags'] & 0x40):
                continue
            if not (T['flags'] & fan):
                continue
            if not all(axis_hit(A['hb'][k], T['mp'][k], T['tsize'][k], A['asize'][k]) for k in range(3)):
                continue
            nib = attr & 0xF
            r = mem.u8(T['sheet'] + nib + 2)
            if (r & 0xC0) != 0x80:
                if nib in (6, 10):
                    ev.append(('effect_3BE74', 4, ti))
                if nib in (7, 9):
                    ev.append(('effect_3BE74', 5, ti))
                st[ti]['touching'] = ai
            st[ti]['c1d0'] = 0x19
            st[ai]['hit'] += 1
            c = tuple(centre(A['hb'][k], T['mp'][k], T['tsize'][k], A['asize'][k]) for k in range(3))
            for _ in range(3):
                ev.append(('effect_3BDD8', 9, c))
                for _ in range(3):
                    rng = lcg(rng)
    dead = {}
    for i in slots:
        if st[i]['touching'] is None:
            continue
        T = specs[i]
        atk = specs[st[i]['touching']]['atk']
        sheet = hero_sheet_addr if i == hero_slot else T['sheet']
        new = resolve(mem, atk, sheet, st[i]['hp'], equip, hero_anim)
        st[i]['hp'] = new
        dead[i] = 1 if new == 0 else 0
    return st, ev, rng, dead
