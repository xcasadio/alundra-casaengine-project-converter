"""models.py - Python models of the decisive loot functions of ALUN_CD.EXE (France). Pure; no emulator."""
import exelib as e
M32 = 0xFFFFFFFF
RANDTAB = 0x80028E0C
ITEMTAB = 0x800C5F7C
def s32(x):
    x &= M32
    return x - (1 << 32) if x & 0x80000000 else x
def lcg(s): return (s * 0x7D2B89DD + 0xE06A02E7) & M32
def rtab(row, nib): return e.u8(RANDTAB + (row & 0x7F) * 16 + nib)
def itab_b4(item): return e.u8(ITEMTAB + item * 8 + 4)
def itab_b5(item): return e.u8(ITEMTAB + item * 8 + 5)

def choose(code, s):
    """ChooseRandomlyAnItem 0x80032968 -> (item, state, draws)"""
    draws = 0
    c = s32(code)
    while True:
        if c >= 0x100: return 0, s, draws
        if not (c & 0x80): return (c if c < 0x62 else 0), s, draws
        s = lcg(s); draws += 1
        c = rtab(c & 0x7F, s >> 28)

def veto(item, mpmax):
    """0x80032A00: 1 = no drop"""
    return 1 if item in (0x26, 0x51, 0x52) and s32(mpmax) == 0 else 0

def spawn_random_loot(code, X, Y, Z, s, mpmax, spawn_ok=True):
    """0x80032C7C. returns dict(ret, state, spawn=(args), fields, sounds)"""
    item, s, _ = choose(code, s)
    out = dict(ret=0, state=s, spawn=None, fields=None, sounds=[])
    if item == 0: return out
    if veto(item, mpmax): return out
    out['spawn'] = (0, 0, item + 0x1E, X & M32, Y & M32, Z & M32, 0)
    if not spawn_ok: return out
    out['fields'] = {0xB8: 0xA0000, 0x274: 1, 0x278: (600 if itab_b4(item) else M32), 0x27C: 0, 0x280: 0, 0x284: 0xA0000, 'flags6C_clear': 0x80}
    out['sounds'] = [0x54]
    out['ret'] = 1
    return out

def spawn_contents(ent, s, mpmax, spawn_ok=True):
    """0x80032B90. ent = dict with 0x3C,0x40,0x114,0x118,0x11C"""
    item = ent[0x3C]
    out = dict(ret=0, state=s, spawn=None, fields=None, sounds=[])
    if item == 0: return out
    if veto(item, mpmax): return out
    out['spawn'] = (0, 0, (item + 0x1E) & M32, ent[0x114] & M32, ent[0x118] & M32, ent[0x11C] & M32, 0)
    if not spawn_ok: return out
    out['fields'] = {0xB8: 0xA0000, 0x274: 1, 0x278: (600 if itab_b4(item) else M32), 0x27C: 0, 0x280: ent[0x40] & M32, 0x284: 0xA0000, 'flags6C_clear': 0x80}
    out['sounds'] = [0x54]
    out['ret'] = 1
    return out

def init_contents(rec, hdr_contents, flag_bank_g, flag_bank_t, s):
    """0x80032A40. rec = None or dict(f10=u16, c12=u8). returns (e+0x3C, e+0x40, state, draws)"""
    if rec is None:
        flag = 0
        code = hdr_contents & 0xFF
    else:
        v1 = rec['f10'] & 0xFFFF
        if (v1 & 0x7FFF) >= 0x800: v1 = 0
        flag = v1
        code = None
        if v1 != 0:
            bank = flag_bank_t if (v1 & 0x8000) else flag_bank_g
            word = bank[((v1 >> 3) & 0xFFC) // 4]
            if word & (1 << (v1 & 31)):
                code = 0
        if code is None:
            code = rec['c12'] & 0xFF
            if code == 0: code = hdr_contents & 0xFF
    item, s, draws = choose(code, s)
    return item, flag, s, draws
