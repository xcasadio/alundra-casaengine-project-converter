"""k2.py - E20.c1b task K2: the REAL ComputeNewHp 0x8003A374 (-> ResolveBalanceTarget 0x8004464C) on a Beannoid target (record 335 of map 14, level D = 0), for each K1 inventory.

States (the real tick passes produce them; ComputeNewHp is then called on copies of the machine, each restored byte for byte):
  HERO   the hero swings (Square at tick 1) at T, 40 px east; the first tick where the REAL hit pass leaves T.TouchingEntity = hero is the sampled state.  The hero's CurrentAttack
         and the latch 0x80146DF8 are whatever the real animation pass / hit pass left (for the weapons whose swing animation is not 0x10 this is the weapon's own animation).
  A10    the same state with the hero's CurrentAttack set to his Attacks[0x11] (animation 0x10: attr 0x81, power 1) and the latch set to 0x10 (the pair the task fixes); identical to HERO
         for every inventory whose real swing is animation 0x10.
  NH     a non-hero attacker: A (HitsA bit) touches V on tick 1 by the real hit pass while the hero stands idle (latch = idle animation 0); ComputeNewHp(V) is then called with A's attack
         as the hit pass left it (attr 0x01, no equipment bit), and with A's CurrentAttack replaced by synthetic attacks (stated in each line).
  HT     the hero as TARGET (his sheet 0x80127008 is the response source): A touches the hero (TouchingEntity written by the harness), ComputeNewHp(hero).
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import kworld as K
import kmodel as M
import world as W
import c1world as C

RESP_IDX = 3                     # sheet byte [3] = response of attribute nibble 1 (record + nibble + 2)
SC_REC = K.SCRATCH + 0x100       # copy of a balance record with one changed response
SC_EQ = K.SCRATCH + 0x200        # synthetic equipment record (3 attacks)


def attacks_of(m, p):
    if not p:
        return None
    cnt = m.peek(p + 0xE) & 0xFF
    return [(m.peek(p + 0xF + 2 * i) & 0xFF, m.peek(p + 0x10 + 2 * i) & 0xFF) for i in range(cnt)]


def hero_state(inv, max_ticks=16):
    m, meta = K.make_world(inv)
    pad = W.Pad()
    hit = None
    for t in range(1, max_ticks + 1):
        m.tick_no = t
        K.tick(m, pad, 0x80 if t == 1 else 0)
        if K.read_entity(m, K.SLOT['T'])['touching_slot'] == 0:
            hit = t
            break
    return m, hit


def nh_state(inv):
    m, meta = K.make_world(inv, a_flags_now=True)
    pad = W.Pad()
    m.tick_no = 1
    K.tick(m, pad, 0)
    return m


def experiment(m, target, hp, *, attacker=None, attack=None, sheet=None, latch=None, equip=None, resp_byte=None):
    """ComputeNewHp(target) on a copy.  attacker: slot written to target.+0x224 (default: leave); attack: address written to the attacker's +0x1C8; sheet: address written to target.+0x1C4;
       latch: value written to 0x80146DF8; equip: {k: pointer} written to 0x80146E00 + 8k.  Returns the measurement and the model's prediction."""
    sb = C.Sandbox(m)
    e = K.ent(target)
    m.poke(e + 0x14, hp)
    if attacker is not None:
        m.poke(e + 0x224, K.ent(attacker))
    att_slot = (m.peek(e + 0x224) - C.ENT0) // C.STRIDE
    if attack is not None:
        m.poke(K.ent(att_slot) + 0x1C8, attack)
    if sheet is not None:
        m.poke(e + 0x1C4, sheet)
    if latch is not None:
        m.poke(K.LATCH, latch)
    for k, p in (equip or {}).items():
        m.poke(K.EQUIP + 8 * k, p)
    # inputs the model is given (plain values, read before the call)
    atk = m.peek(K.ent(att_slot) + 0x1C8)
    attr, power = (m.peek(atk) & 0xFF, (m.peek(atk) >> 8) & 0xFF) if atk else (0, 0)
    shp = K.SHEET if target == 0 else m.peek(e + 0x1C4)
    nib = (attr & 0xF) if attr else 0
    resp = [m.peek(shp + n + 2) & 0xFF for n in range(16)]
    eq = [attacks_of(m, m.peek(K.EQUIP + 8 * k)) for k in range(3)]
    ptrs = [m.peek(K.EQUIP + 8 * k) for k in range(3)]
    lt = m.peeks(K.LATCH)
    ret = K.call(m, 0x8003A374, e)
    after = m.peeks(e + 0x14)
    uninit = sorted((hex(pc), hex(a), n) for (pc, a, n) in m.uninit_reads if (pc, a, n) not in sb.uninit)
    sb.restore()
    mhp, mdmg, how = M.damage_model(attr, power, lambda n: resp[n], hp, eq, lt)
    return dict(hp_before=hp, hp_after=after, damage=hp - after, ret=ret & M.M32, attr=attr, power=power, response=resp[nib] if attr else None, latch=lt,
                equip_ptrs=[('0x%08X' % p) if p else 'null' for p in ptrs], equip_attacks=eq, model_hp=mhp, model_how=how, equal=(mhp == after), uninit=uninit)


def fmt_exp(label, x):
    r = 'response 0x%02X' % x['response'] if x['response'] is not None else 'no attribute'
    return '   %-62s attack attr 0x%02X power %-3d %-14s latch %-3d hp %3d -> %3d (damage %3d) returns %d   model %-3d %s' % (
        label, x['attr'], x['power'], r, x['latch'], x['hp_before'], x['hp_after'], x['damage'], x['ret'], x['model_hp'], 'EQUAL' if x['equal'] else 'DIFFERENT (%s)' % x['model_how'])


def copy_record(m, target, resp_nibble1):
    """copy of the target's balance record (record 335, as 0x80044550 chose it) with the response of nibble 1 (record byte [3]) replaced"""
    src = m.peek(K.ent(target) + 0x1C4)
    for i in range(0x20):
        m.poke8(SC_REC + i, m.peek(src + i))
    m.poke8(SC_REC + RESP_IDX, resp_nibble1)
    return SC_REC


def equipment_line(m):
    out = []
    for k in range(3):
        p = m.peek(K.EQUIP + 8 * k)
        if not p:
            out.append('[%d] null' % k)
        else:
            out.append('[%d] item %d %s' % (k, m.peek(K.EQUIP + 8 * k + 4) - 0x1E, attacks_of(m, p)))
    return ', '.join(out)


def suite_hero(m, label, lines, results, extra_eq=True):
    """the K2 matrix on the current state of m (target T touched by the hero)"""
    T = K.SLOT['T']
    hero = 0
    nat = m.peek(K.ent(T) + 0x1C4)
    res0 = m.peek(nat + RESP_IDX) & 0xFF
    xs = []

    def add(lab, x):
        xs.append((lab, x))
        lines.append(fmt_exp(lab, x))
    for hp in (9, 100):
        add('natural record, response 0x%02X' % res0, experiment(m, T, hp))
    for r in (0x00, 0x40, 0x80, 0xC0):
        rec = copy_record(m, T, r)
        for hp in (9, 100):
            add('copy of the record with response[nibble 1] = 0x%02X' % r, experiment(m, T, hp, sheet=rec))
    # same hero attack but the 0x80 attribute bit cleared (no equipment term): the pair is synthetic (attr 0x01, power = the hero attack's power)
    hatk = m.peek(K.ent(hero) + 0x1C8)
    hp_pow = (m.peek(hatk) >> 8) & 0xFF
    m.poke_bytes(K.syn(0), bytes((0x01, hp_pow)))                       # re-written: (attr 0x01, power of the hero's attack)
    add('attack WITHOUT bit 0x80 (synthetic attr 0x01, same power)', experiment(m, T, 100, attack=K.syn(0)))
    m.poke_bytes(K.syn(0), bytes((0x01, 1)))
    add('attack WITHOUT bit 0x80 (synthetic attr 0x01, power 1)', experiment(m, T, 100, attack=K.syn(0)))
    add('attack WITHOUT bit 0x80 (synthetic attr 0x01, power 16)', experiment(m, T, 100, attack=K.syn(5)))
    add('attack nibble 4 (synthetic attr 0x84, power 2): response[4] = 0x40', experiment(m, T, 100, attack=K.syn(4)))
    results[label] = [(lab, x) for lab, x in xs]
    return xs


def run_inventory(inv):
    name = inv['name']
    lines = []
    results = {}
    ok = True
    # ---- HERO (real) and A10 ----------------------------------------------------------------------------------------------------
    m, hit = hero_state(inv)
    H = K.read_entity(m, 0)
    T = K.read_entity(m, K.SLOT['T'])
    lines.append('== K2 %s (%s)' % (name, inv['desc']))
    if hit is None:
        lines.append('   HERO: the swing never touches T within 16 ticks')
        return lines, results, False
    lines.append('   HERO state (tick %d, real hit pass): hero animation 0x%02X, CurrentAttack 0x%08X = attr 0x%02X power %d, latch 0x80146DF8 = %d, T.TouchingEntity = slot %d, hero HitCounter %d; equipment %s' % (
        hit, H['anim'], H['atk'], H['attr'], H['power'], K.read_aux(m)['latch'], T['touching_slot'], H['hit_counter'], equipment_line(m)))
    results['meta'] = dict(hit_tick=hit, hero_anim=H['anim'], atk_attr=H['attr'], atk_power=H['power'], latch=K.read_aux(m)['latch'], equipment=equipment_line(m))
    xs = suite_hero(m, 'HERO', lines, results)
    ok &= all(x['equal'] for _, x in xs)
    # ---- A10 -----------------------------------------------------------------------------------------------------------------------
    hs = m.peek(K.ent(0) + 0x1C4)
    a10 = hs + 0xF + 2 * 0x11
    a10_attr, a10_pow = m.peek(a10) & 0xFF, (m.peek(a10) >> 8) & 0xFF
    same = (H['atk'] == a10 and K.read_aux(m)['latch'] == 0x10)
    if same:
        lines.append('   A10 state: the real state above IS the A10 state (hero CurrentAttack 0x%08X = his Attacks[0x11], attr 0x%02X power %d; latch 16)' % (a10, a10_attr, a10_pow))
        results['A10'] = 'same as HERO'
    else:
        sb = C.Sandbox(m)
        m.poke(K.ent(0) + 0x1C8, a10)
        m.poke(K.LATCH, 0x10)
        lines.append('   A10 state (forced: the real swing of this weapon is animation 0x%02X): hero CurrentAttack := 0x%08X (his Attacks[0x11]: attr 0x%02X power %d), latch := 16; T.TouchingEntity = hero as the real hit left it; equipment %s' % (
            H['anim'], a10, a10_attr, a10_pow, equipment_line(m)))
        xs = suite_hero(m, 'A10', lines, results)
        ok &= all(x['equal'] for _, x in xs)
        sb.restore()
    # ---- NH ---------------------------------------------------------------------------------------------------------------------------
    n = nh_state(inv)
    A = K.read_entity(n, K.SLOT['A'])
    V = K.read_entity(n, K.SLOT['V'])
    Hn = K.read_entity(n, 0)
    lines.append('   NH state (tick 1, real hit pass, hero idle): V.TouchingEntity = slot %s (A), A attack 0x%08X attr 0x%02X power %d, A.HitCounter %d, hero animation %d, latch %d; equipment %s' % (
        V['touching_slot'], A['atk'], A['attr'], A['power'], A['hit_counter'], Hn['anim'], K.read_aux(n)['latch'], equipment_line(n)))
    ok &= (V['touching_slot'] == K.SLOT['A'])
    Vs = K.SLOT['V']
    xs = []

    def add(lab, x):
        xs.append((lab, x))
        lines.append(fmt_exp(lab, x))
    add("A's own attack (attr 0x01: no equipment bit)", experiment(n, Vs, 100))
    add('A attack := synthetic attr 0x81 power 1 (equipment bit), latch as is', experiment(n, Vs, 100, attack=K.syn(1)))
    add('A attack := synthetic attr 0x81 power 1, latch := 0x10 (hero swinging)', experiment(n, Vs, 100, attack=K.syn(1), latch=0x10))
    add('A attack := synthetic attr 0x81 power 16, latch := 0x10', experiment(n, Vs, 100, attack=K.syn(6), latch=0x10))
    add('A attack := synthetic attr 0x81 power 1, equipment records ALL nulled', experiment(n, Vs, 100, attack=K.syn(1), equip={0: 0, 1: 0, 2: 0}))
    if name == 'kit':
        # a synthetic equipment record with 3 attacks (1,2) (1,3) (1,5) in slot [2]: the index rule g + 1 < count ? g + 1 : 0
        rec = bytes([255, 21, 0] + [16] * 11 + [3, 1, 2, 1, 3, 1, 5])
        for i, b in enumerate(rec):
            n.poke8(SC_EQ + i, b)
        for g in (0x10, 0, 1, 2, -1):
            add('synthetic 3-attack record in [2], A attr 0x81 power 1, latch := %d' % g, experiment(n, Vs, 100, attack=K.syn(1), latch=g, equip={2: SC_EQ}))
    results['NH'] = [(lab, x) for lab, x in xs]
    ok &= all(x['equal'] for _, x in xs)
    # ---- HT: the hero as the target ------------------------------------------------------------------------------------------------
    xs = []
    lines.append('   HT (hero as target, sheet 0x80127008 = [3] 0x%02X, A touches the hero; HP 200)' % (n.peek(K.SHEET + RESP_IDX) & 0xFF))
    add("A's own attack (attr 0x01 power 1)", experiment(n, 0, 200, attacker=K.SLOT['A']))
    add('A attack := synthetic attr 0x01 power 16', experiment(n, 0, 200, attacker=K.SLOT['A'], attack=K.syn(5)))
    add('A attack := synthetic attr 0x81 power 16 (equipment term, latch as is)', experiment(n, 0, 200, attacker=K.SLOT['A'], attack=K.syn(6)))
    results['HT'] = [(lab, x) for lab, x in xs]
    ok &= all(x['equal'] for _, x in xs)
    unw = [x['uninit'] for lst in results.values() if isinstance(lst, list) for _, x in lst if x['uninit']]
    if unw:
        ok = False
        lines.append('   UNWRITTEN-BYTE READS: %s' % unw)
    lines.append('   => %s' % ('OK' if ok else 'FAIL'))
    return lines, results, ok


def run(names=None):
    lines = []
    results = {}
    ok = True
    for name, inv in K.INV.items():
        if names and name not in names:
            continue
        l, r, o = run_inventory(inv)
        lines += l + ['']
        results[name] = r
        ok &= o
    return lines, results, ok


if __name__ == '__main__':
    ls, res, ok = run(sys.argv[1:] or None)
    print('\n'.join(ls))
    print('K2', 'PASS' if ok else 'FAIL')
