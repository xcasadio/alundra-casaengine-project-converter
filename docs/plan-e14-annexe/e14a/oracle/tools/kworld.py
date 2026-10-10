"""kworld.py - E20.c1b oracle: inventories (built with the REAL new-game code), the machine setup of c1world with a chosen inventory, and the readers shared by K1, K2, K3.

Real code of ALUN_CD.EXE (France) run by the emulated machine:  InitPlayerStats 0x8004DAC0, AddItem 0x8004E530, SetPlayerWeaponId 0x8004E484 (-> 0x8004E030),
SetCurrentItemId 0x8004E4D8 (the "item setter": `sh a1, 0xc(stats)` at 0x8004E51C, range test against [0x8017638C]), UpdateItemEffectState 0x800307E8,
ComputeNewHp 0x8003A374, ResolveBalanceTarget 0x8004464C, the opcode handlers of the table 0x80098FAC, the search 0x8003C954, and (for the hit) the tick passes of c1world.

Nothing in this file knows an expected value.  Stubs are those of machine.py (sound, effects, spawn) - none is reached by K1/K3 except the sparks of the real hit pass in K2/K3 setups.
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import c1world as C
import world as W
import machine
from machine import M32, s32

STATS_PTR = 0x80176318          # [0x80176318] = g_playerStats (0x801EB82C in the synthetic world)
INV_PTR = 0x8017631C            # [0x8017631C] = inventory table: 4 bytes per item, +2 = owned count (lh)
ITEM_COUNT = 0x8017638C         # [0x8017638C] = number of items (0x63)
CATEGORY_TABLE = 0x800C5F7C     # 8 bytes per item, byte 6 & 0x7F = category (1 weapon, 2 armour, 3 accessory), bit 0x80 = in the new-game kit
ITEM_PROPS = 0x800B9FE8         # 10 bytes per item: slot, flags, rank, max count, icon
BLOB = 0x80143DF8               # balance blob base
EQUIP = 0x80146E00              # 3 records, stride 8: +0 balance record pointer, +4 item id + 0x1E (0 = none)
LATCH = 0x80146DF8
HERO = W.ENT0
SHEET = 0x80127008              # the 255-byte hero working sheet
BOOTS = 0x80127000
REGEN = 0x80126FE8              # 3 words

KIT_ITEMS = (1, 17, 25)         # property byte 6 bit 0x80 (asserted against the real loop below)

# name -> (owned items added after InitPlayerStats in this order, SetPlayerWeaponId argument, SetCurrentItemId argument or None, description)
INVENTORIES = [
    ('kit',      (1, 17, 25),            1, None, 'new-game kit: Basic sword (1), cloth jacket (17), normal boots (25), weapon slot 1'),
    ('steel',    (1, 17, 25, 2),         1, None, 'Steel Edge (item 2) owned and equipped (slot 1, rank 1 beats the Basic sword), kit otherwise'),
    ('holy',     (1, 17, 25, 4),         1, None, 'Holy Sword Elnard (item 4) owned and equipped (slot 1, rank 3), kit otherwise'),
    ('ring',     (1, 17, 25, 0x33),      1, 0x33, 'ring 0x33 owned and set as the equipped item (SetCurrentItemId 0x33), kit otherwise'),
    ('leather',  (1, 17, 25, 0x12),      1, None, 'leather armour 0x12 owned (the cloth jacket 17 stays owned; the highest owned armour wins), kit otherwise'),
    ('noweapon', (17, 25),               1, None, 'no weapon owned: Basic sword absent, weapon slot 1 still selected'),
    # extras (not asked): they exercise every branch of the response combination and the all-records-present case used by the dependency map
    ('all',      (1, 17, 25, 4, 0x12, 0x33), 1, 0x33, 'EXTRA: Holy Sword + leather armour 0x12 + ring 0x33 equipped (all three records non-null)'),
    ('noarmour', (1, 25),                1, None, 'EXTRA: no armour owned (record [0] null: the sheet comes from the hero +0x1C4 record)'),
    ('bracelet', (1, 17, 25, 0x35),      1, 0x35, 'EXTRA: Super bracelet 0x35 equipped (responses 15)'),
    ('charm',    (1, 17, 25, 0x36),      1, 0x36, 'EXTRA: Charm of Nava 0x36 equipped (response index 6 = 0x80)'),
    ('tonic',    (1, 17, 25, 0x38),      1, 0x38, 'EXTRA: Tonic 0x38 equipped (regeneration record, MaxHp field 200)'),
    ('rings2',   (1, 17, 25, 0x33, 0x34), 1, 0x33, 'EXTRA: rings 0x33 and 0x34 (same slot 20, ranks 0 and 1) owned, 0x33 set: the first call uses 0x33, then ItemId becomes 0x34'),
]
INV = {n: dict(name=n, owned=o, weapon=w, item=i, desc=d) for n, o, w, i, d in INVENTORIES}
REQUIRED = ('kit', 'steel', 'holy', 'ring', 'leather', 'noweapon')


def build_inventory(m, inv):
    """real new-game code: InitPlayerStats, AddItem for each owned item, SetPlayerWeaponId, SetCurrentItemId (through the interpreter under test).  Returns the list of owned items in order."""
    call(m, 0x8004DAC0)
    for i in inv['owned']:
        call(m, 0x8004E530, i)
    call(m, 0x8004E484, inv['weapon'])
    if inv['item'] is not None:
        call(m, 0x8004E4D8, inv['item'])
    return list(inv['owned'])


def kit_by_real_loop(m):
    """the loop of 0x80031868-0x80031890: every item i < 0x62 whose property byte 6 has bit 0x80"""
    return [i for i in range(0x62) if (m.peek(CATEGORY_TABLE + 8 * i + 4) >> 16) & 0x80]


PATCHES = []                # (address, old word, new word) applied to every machine built by setup_inv (empty except in the mutation tests)
COVS = []                   # the executed-PC set of every machine built (coverage report)


def setup_inv(inv, **kw):
    """c1world.setup with the chosen inventory in place of the new-game kit (the hero, the balance blob at level D = 0, the targets and the pad are c1world's).
       kw: hero_px, targets, direction, seed, ... as c1world.setup (targets default: none)."""
    kw.setdefault('targets', ())
    orig = W.init_stats
    W.init_stats = lambda m: build_inventory(m, inv)
    try:
        m, meta = C.setup(**kw)
    finally:
        W.init_stats = orig
    meta['inventory'] = dict(name=inv['name'], owned=list(inv['owned']), weapon_slot=inv['weapon'], item=inv['item'])
    for addr, old, new in PATCHES:                  # mutation tests: one-word patches of the emulated binary (RAM only), the old word is checked
        cur = m.peek(addr)
        if cur != old:
            raise RuntimeError('word at %08X is %08X, expected %08X' % (addr, cur, old))
        m.poke(addr, new)
    COVS.append(m.cov)
    return m, meta


def hero_stats(m):
    p = m.peek(STATS_PTR)
    return dict(weapon_id=s16(m.peek(p + 0xA)), item_id=s16(m.peek(p + 0xC)))


def s16(v):
    v &= 0xFFFF
    return v - 0x10000 if v & 0x8000 else v


def owned_count(m, item):
    return s16(m.peek(m.peek(INV_PTR) + 4 * item + 2))


def read_records(m):
    """the three equipment records: pointer, offset in the blob, item id word (item + 0x1E, 0 = none), level, size, MaxHp, AttackCount, Attacks, responses"""
    out = []
    for k in range(3):
        p = m.peek(EQUIP + 8 * k)
        idw = m.peek(EQUIP + 8 * k + 4)
        r = dict(k=k, ptr=p, item_word=idw)
        if p:
            raw = [m.peek(p + j) & 0xFF for j in range(0x20)]
            cnt = raw[0xE]
            r.update(blob_offset=p - BLOB, level=raw[0], size=raw[1], max_hp=raw[2], responses=raw[3:14], attack_count=cnt,
                     attacks=[(raw[0xF + 2 * i], raw[0x10 + 2 * i]) for i in range(cnt)])
        out.append(r)
    return out


def read_sheet(m):
    return bytes(m.peek(SHEET + i) & 0xFF for i in range(255))


def read_aux(m):
    return dict(boots=m.peeks(BOOTS), regen=[m.peek(REGEN + 4 * i) for i in range(3)], latch=m.peeks(LATCH))


def hexs(bs):
    return ' '.join('%02X' % b for b in bs)


def snapshot_all(m):
    """whole-RAM snapshot (bytes) and shadow; restore_all puts both back (the machine's journals are not touched by the K1 calls)"""
    return bytes(m.ram), bytes(m.init)


def restore_all(m, snap):
    m.ram[:] = snap[0]
    m.init[:] = snap[1]


# ---- the two interpreters ------------------------------------------------------------------------------------------------------
ENGINE = ['emu']            # 'emu' = emu.py (the oracle's machine), 'mips' = mips.py through interp2.py (a separately written interpreter)


def use_engine(name):
    assert name in ('emu', 'mips')
    ENGINE[0] = name


COUNT = {'emu': 0, 'mips': 0}       # calls made through each interpreter (reported by run_c1b.py)


def call(m, addr, *args):
    """call a function of the binary with a0.. = args, stack pointer 0x801FFF00 (both interpreters), return v0"""
    COUNT[ENGINE[0]] += 1
    if ENGINE[0] == 'emu':
        return m.call(addr, *args)
    import interp2
    return interp2.call2(m, addr, *args)


def tick(m, pad, hold):
    if ENGINE[0] == 'emu':
        W.tick(m, pad, hold)
    else:
        import interp2
        interp2.tick2(m, pad, hold)


# ---- the K2 / K3 world ---------------------------------------------------------------------------------------------------------
TXPOS, TYPOS = 20, 11
TX, TY = C.target_px(TXPOS, TYPOS)               # 252, 96 px
ORDER = ('T', 'T2', 'A', 'V', 'U')               # map entity records 0..4 -> slots 1..5 (the real spawner takes the first free slot)
RECS = dict(T=(20, 11, 0), T2=(21, 12, 0), A=(28, 11, 0), V=(29, 11, 0), U=(20, 20, 0))
SLOT = dict(H=0, T=1, T2=2, A=3, V=4, U=5)
SCRATCH = 0x801EA000                             # free RAM: synthetic attacks, copies of balance records, a synthetic equipment record
SYN_ATTACKS = SCRATCH                            # 2 bytes (attribute, power) each, stride 2


def ent(slot):
    return C.ent(slot)


def make_world(inv, hero_dx=40, a_flags_now=False, a_attack=None):
    """hero at (TX - hero_dx, TY) facing east; targets T (record 0, at the hero's dx), T2 (record 1, +12 px east, +8 px south), A (record 2, 96 px further east) and V (record 3,
       12 px east of A), U (record 4, 72 px south of T); a sixth map record (index 5) exists but is never spawned.  All are Beannoids (record 335 of map 14) spawned by the real spawner.
       a_flags_now: give A the HitsA bit (+0x6C |= 4) at once, else the caller does it before the tick it wants A to hit V.  a_attack: address of a (attribute, power) pair that replaces A's CurrentAttack"""
    m, meta = setup_inv(inv, hero_px=(TX - hero_dx, TY), targets=[RECS[k] for k in ORDER])
    sp = C.map14()
    rec5 = C.entity_record_bytes(sp.entities[0], 5, 5, 0)
    m.poke_bytes(C.ENT_REC + 20 * 5, rec5)
    m.poke(C.MAPBLK + 0x14, 6)
    for a, p in enumerate(((0x01, 1), (0x81, 1), (0x04, 2), (0x03, 2), (0x84, 2), (0x01, 16), (0x81, 16))):
        m.poke_bytes(SYN_ATTACKS + 2 * a, bytes(p))
    if a_attack is not None:
        m.poke(ent(SLOT['A']) + 0x1C8, a_attack)
    if a_flags_now:
        arm_a(m)
    m.uninit_reads.clear()
    return m, meta


def arm_a(m):
    a = ent(SLOT['A']) + 0x6C
    m.poke(a, m.peek(a) | 4)


def syn(i):
    """address of the i-th synthetic attack pair: 0 (0x01,1) 1 (0x81,1) 2 (0x04,2) 3 (0x03,2) 4 (0x84,2) 5 (0x01,16) 6 (0x81,16)"""
    return SYN_ATTACKS + 2 * i


E_NAMES = {0: 'hero', 1: 'T', 2: 'T2', 3: 'A', 4: 'V', 5: 'U'}


def read_entity(m, slot):
    e = ent(slot)
    atk = m.peek(e + 0x1C8)
    tch = m.peek(e + 0x224)
    return dict(slot=slot, name=E_NAMES.get(slot, str(slot)), status=m.peek(e + 0x10), hp=m.peeks(e + 0x14), ctl20=m.peeks(e + 0x20), rec48=m.peeks(e + 0x48), flags=m.peek(e + 0x6C),
                anim=m.peeks(e + 0x88), animflags=m.peek(e + 0xB4), sheet=m.peek(e + 0x1C4), atk=atk, box=m.peek(e + 0x1D4),
                attr=(m.peek(atk) & 0xFF) if atk else None, power=((m.peek(atk) >> 8) & 0xFF) if atk else None,
                c1cc=m.peeks(e + 0x1CC), c1d0=m.peeks(e + 0x1D0), hit_counter=m.peeks(e + 0x220), touching=tch,
                touching_slot=((tch - C.ENT0) // C.STRIDE) if tch else None,
                hitbox=[m.peeks(e + 0x1FC + 4 * k) for k in range(3)], asize=[m.peeks(e + 0x214 + 4 * k) for k in range(3)],
                mp=[m.peeks(e + 0x1D8 + 4 * k) for k in range(3)], tsize=[m.peeks(e + 0x1F0 + 4 * k) for k in range(3)],
                pos=[m.peeks(e + 0x114 + 4 * k) for k in range(3)], boff=[m.peeks(e + 0x208 + 4 * k) for k in range(3)])


def read_entities(m, n=6):
    return {s: read_entity(m, s) for s in range(n)}
