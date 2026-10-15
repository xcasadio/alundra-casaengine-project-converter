"""kmodel.py - E20.c1b: INDEPENDENT Python models, used only to cross-check the real code run by the emulated machine.

They are written from the E20.0 reports (docs/plan-e20-annexe/e20-0-rapports/D4-D5-equipement-0x8003634C.md, d2-oracle-coups/notes.md section 5, e20-c1/faits-dll-binaire.md sections 4-5)
and from the arithmetic read in the disassembly (out/dis_*.txt), never from the emulator.  They share with the emulator only DATA read from the disc: the item property tables of the EXE
(category table 0x800C5F7C, item property records 0x800B9FE8) and DATA/BALANCE.BIN.  Anything a model needs from the machine (entity fields, record pointers) is passed in as plain values.
"""
import os
import sys
import struct

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import lib
import world as W

BLOB_BASE = 0x80143DF8
CAT_BASE = 0x800C5F7C
PROP_BASE = 0x800B9FE8
M32 = 0xFFFFFFFF
_BLOB = None


def blob():
    global _BLOB
    if _BLOB is None:
        _BLOB = open(W.BALANCE_BIN, 'rb').read()
    return _BLOB


def exe_u8(addr):
    return lib.DATA[addr - lib.BASE + lib.OFF]


def exe_u16(addr):
    return exe_u8(addr) | (exe_u8(addr + 1) << 8)


def exe_s16(addr):
    v = exe_u16(addr)
    return v - 0x10000 if v & 0x8000 else v


def category(n):
    return exe_u8(CAT_BASE + 8 * n + 6) & 0x7F


def prop(n):
    """item property record 0x800B9FE8 + 10 n: slot, flags, rank, max count, icon (signed halfwords as `lh` reads them)"""
    a = PROP_BASE + 10 * n
    return dict(slot=exe_s16(a), flags=exe_u16(a + 2), rank=exe_s16(a + 4), maxc=exe_s16(a + 6), icon=exe_s16(a + 8))


def blob_u8(off):
    return blob()[off]


def item_record_offset(n, level_d):
    """0x800445C0(n): entry n + 0x1E of the offset table, then `rec += rec[1]` while rec[0] < D (the threshold [0x80143DF0])"""
    off = struct.unpack_from('<H', blob(), 2 * (n + 0x1E))[0]
    while blob()[off] < level_d:
        off += blob()[off + 1]
    return off


# ---- inventory and equipment records (D4) ------------------------------------------------------------------------------------
class Inventory:
    """what AddItem / SetPlayerWeaponId / SetCurrentItemId do to the save data, written from their decoded behaviour"""
    def __init__(self, owned, weapon_slot, item):
        self.count = {}
        for n in owned:
            c = self.count.get(n, 0)
            if c != prop(n)['maxc']:          # AddItem: left alone when the count equals the maximum
                self.count[n] = c + 1
        self.weapon_slot = weapon_slot
        self.item = item if item is not None else 0       # InitPlayerStats leaves ItemId 0

    def owned(self, n):
        return self.count.get(n, 0) > 0

    def item_from_slot(self, slot):
        """GetItemIdFromSlotId: scan items 0..0x7F; the first owned item of the slot is kept; if its flag bit 0 is clear it is returned at once; later matches replace it by rank"""
        best = None
        for n in range(0x80):
            p = prop(n)
            if p['slot'] == slot and self.owned(n):
                if best is None:
                    best = n
                    if p['flags'] & 1 == 0:
                        return n
                elif prop(best)['rank'] < p['rank']:
                    best = n
        return -1 if best is None else best

    def current_weapon(self):
        return -1 if self.weapon_slot == -1 else self.item_from_slot(self.weapon_slot)

    def advance(self):
        """the side effect of 0x8004E0F8 that the NEXT call sees: stats.ItemId := the best owned item of the slot of the current one (SetCurrentItemId range test 0 <= id < 0x63)"""
        if self.owned(self.item):
            best = self.item_from_slot(prop(self.item)['slot'])
            if 0 <= best < 0x63:
                self.item = best

    def current_item_old(self):
        """0x8004E0F8: returns the old ItemId when owned (and rewrites it with the best of its slot), -1 otherwise"""
        if not self.owned(self.item):
            return -1
        return self.item


def model_records(inv, level_d):
    """-> list of 3 (blob offset or None, item word) following 0x800307E8: [0] highest-numbered owned item of category 2; [1] current weapon if 1..0x61 and category 1; [2] 0x8004E0F8's item if 1..0x61 and category 3"""
    recs = [None, None, None]
    for n in range(0x61, -1, -1):
        if category(n) == 2 and inv.owned(n):
            recs[0] = (item_record_offset(n, level_d), n + 0x1E)
            break
    w = inv.current_weapon()
    if 1 <= w <= 0x61 and category(w) == 1:
        recs[1] = (item_record_offset(w, level_d), w + 0x1E)
    it = inv.current_item_old()
    if 1 <= it <= 0x61 and category(it) == 3:
        recs[2] = (item_record_offset(it, level_d), it + 0x1E)
    return recs


def combine_response(b, A, B):
    """one response byte of the hero sheet from the base byte b (record [0] or the hero record) and the bytes A (record [1]) and B (record [2]); a missing record counts as 0x10.
       D4 rule (0x80030B64-0x80030C0C): any byte whose top two bits are exactly 0x80 -> 0x80; b flagged and A flagged and B flagged -> 0x40 (stored as is);
       otherwise the value starts at 0x10 when b is flagged (else b); A is added minus 0x10 only when A has no flag bits, B likewise; clamp to 0..0x1F."""
    fb, fa, fc = b & 0xC0, A & 0xC0, B & 0xC0
    if fb == 0x80 or fa == 0x80 or fc == 0x80:
        return 0x80
    if fb != 0:
        v = 0x10
        if fa != 0 and fc != 0:
            return 0x40
    else:
        v = b
    if fa == 0:
        v = v - 0x10 + A
    if fc == 0:
        v = v - 0x10 + B
    if v >= 0x20:
        return 0x1F
    if v < 0:
        return 0
    return v


def model_sheet(recs, base_bytes):
    """the 255-byte hero sheet 0x80127008: a copy of base_bytes[:255] (record [0] from the blob, or the hero's own +0x1C4 record when [0] is null), responses 1..11 rewritten"""
    s = bytearray(base_bytes[:255])
    ra = blob_slice(recs[1]) if recs[1] else None
    rb = blob_slice(recs[2]) if recs[2] else None
    for i in range(1, 12):
        A = ra[i + 2] if ra else 0x10
        B = rb[i + 2] if rb else 0x10
        s[i + 2] = combine_response(s[i + 2], A, B)
    return bytes(s)


def blob_slice(rec, n=0x100):
    off = rec[0]
    return blob()[off:off + n]


def model_boots(inv):
    """0x800307E8 head: items 0x1C, 0x1B, 0x1A owned -> 3, 2, 1 else 0"""
    if inv.owned(0x1C):
        return 3
    if inv.owned(0x1B):
        return 2
    if inv.owned(0x1A):
        return 1
    return 0


# ---- damage (D2 section 5) ------------------------------------------------------------------------------------------------
def s32(x):
    x &= M32
    return x - (1 << 32) if x & 0x80000000 else x


def damage_model(attr, power, response_of_nibble, hp, equip_attacks, latch):
    """ResolveBalanceTarget 0x8004464C.  attr, power: the two bytes of the attack; response_of_nibble(n) -> the sheet byte [n + 2];
       equip_attacks: for each of the 3 equipment records, None (null pointer) or the list of (attribute, power) of its Attacks; latch: 0x80146DF8 (signed).
       returns (new hp, damage or None, how)"""
    if attr == 0:
        return hp, None, 'attribute byte 0: unchanged'
    r = response_of_nibble(attr & 0xF)
    top = r & 0xC0
    if top == 0x40:
        return 0, None, 'response top bits 0x40: kill'
    if top != 0:
        return hp, None, 'response top bits 0x%02X: unchanged' % top
    p = power
    if attr & 0x80:
        for atk in equip_attacks:
            if atk is None or len(atk) == 0:
                continue
            idx = latch + 1 if (latch + 1) < len(atk) else 0
            p += atk[idx][1]
    d = (s32(p) * r) >> 4
    how = 'dmg %d' % d
    if d == 0:
        d = 1
        how = 'dmg 0 -> minimum 1'
    if d < hp:
        return hp - d, d, how
    return 0, d, how + ' >= hp: 0'


# ---- opcodes (e20-c1/faits-dll-binaire.md section 5, disassembly of 0x8003FFD4, 0x80040048, 0x800407C0, 0x8003FA10) -------------------------------
NIBBLE_TABLE = [1, 2, 3, 4, 5, 5, 6, 7]


def nibble_for(type_idx, exe_bytes):
    """the byte at 0x80098FA4 + typeIdx: the table is 8 bytes, and the bytes that follow it (the handler table 0x80098FAC) are read for indices 8..11"""
    return exe_bytes[type_idx]


def model_search(entities, owner_self, hero, search, record_ok):
    """0x8003C954 for the codes the tests use.  entities: dict slot -> dict(status, rec48, addr); returns the list of matched slots in ascending order.
       search < 0x80: status 1..3 and +0x48 == search; 0x80: the entity the handler got in a0 (owner_self slot); 0x81: slot 0; 0x82: every status 1..3 entity from slot 0"""
    out = []
    if search < 0x80:
        for s in sorted(entities):
            e = entities[s]
            if s >= 1 and 1 <= e['status'] <= 3 and e['rec48'] == search:
                out.append(s)
    elif search == 0x80:
        out.append(owner_self)
    elif search == 0x81:
        out.append(0)
    elif search == 0x82:
        for s in sorted(entities):
            if 1 <= entities[s]['status'] <= 3:
                out.append(s)
    return out


def model_0x87(entities, matched, nibble):
    """Result 1 iff some matched entity has a non-null TouchingEntity whose frame-box (+0x1D4) and attack (+0x1C8) pointers are non-null, whose attack attribute byte is non-zero and (attr & 0xF) == nibble"""
    for s in matched:
        t = entities[s]['touching_slot']
        if t is None:
            continue
        a = entities[t]
        if a['box'] and a['atk'] and a['attr'] and (a['attr'] & 0xF) == nibble:
            return 1
    return 0


def model_0x95(entities, count_slot, rect, mask, nibble):
    """0x800407C0.  rect: dict(ox, oy, oz, sx, sy) raw 16.16 (sz = sx); entities: slot -> dict(status, ctl20, box, atk, attr, flags, hitbox(3), asize(3)); scans slots 0..count_slot inclusive.
       X: d = hitbox.x - ox; d >= 0 -> d < sx; else ox - hitbox.x < asize.x + 1.  Y: with sy (the program's size is (n << 20) - 1 and the test adds 1), asize.y + 1.  Z: shares sx, asize.z + 1."""
    if mask == 0:
        return 0
    for s in range(0, count_slot + 1):
        e = entities.get(s)
        if e is None:
            continue
        if not (2 <= e['status'] <= 3):
            continue
        if e['ctl20'] != 0 or not e['box'] or not e['atk'] or e['attr'] == 0:
            continue
        if (e['flags'] & mask) == 0 or (e['attr'] & 0xF) != nibble:
            continue
        ok = True
        for k, (o, size_ok, asz) in enumerate(((rect['ox'], rect['sx'], e['asize'][0]), (rect['oy'], rect['sy'], e['asize'][1]), (rect['oz'], rect['sx'], e['asize'][2]))):
            d = s32(e['hitbox'][k] - o)
            if d >= 0:
                if not d < size_ok:
                    ok = False
                    break
            else:
                if not s32(o - e['hitbox'][k]) < s32(asz + 1):
                    ok = False
                    break
        if ok:
            return 1
    return 0
