"""check_h1_bodies.py - E20.e0c T0 (g): the heal bodies as GiveItem runs them on the real code (give-item.json, made by gen_give.py) against MY-OWN model of E20.h1 (model_h1.py, docs/plan-e20-annexe/e20-h1/disc/oracle/),
member by member. A body that differs from the h1 model is a STOP (plan section 1.26, Arrets). usage: python -B check_h1_bodies.py <give-item.json> <dir of model_h1.py>"""
import json
import sys

vec = json.load(open(sys.argv[1]))
sys.path.insert(0, sys.argv[2])
import model_h1 as H

M = H.M
FX = 0x80144000
BODY = {0x24: H.heal_herb, 0x25: H.heal_lpotion, 0x27: H.heal_cureale, 0x29: H.heal_spotion, 0x52: H.heal_elixir, 0x56: H.heal_water1}
INV_FIRST = {0x24, 0x25, 0x27, 0x29}
MAX = vec['inventoryMaxima']
PLAYER = 0x80141000
checked = bad = 0
for v in vec['vectors']:
    s, o = v['state'], v['out']
    item = s['item']
    if item not in BODY:
        continue
    if item in INV_FIRST and s['count'] != MAX[hex(item)]:
        continue                      # the object entered the inventory: no body runs
    hero = H.HERO
    hx, hy, hz = s['hero']
    # the effect creations of the real run, in order: the returned addresses (0 = refused)
    reads = [(hero + 0x14, 4, s['hp'] & M), (hero + 0x18, 4, s['hpmax']), (hero + 0x114, 4, hx), (hero + 0x118, 4, hy), (hero + 0x11C, 4, hz),
             (H.STATS_PTR, 4, PLAYER), (PLAYER + 4, 2, s['mp']), (PLAYER + 6, 2, s['mpmax']), (H.RNG, 4, s['seed'])]
    for i in range(0, 64, 2):
        pass
    stubs = {}
    addrs = []
    n = 0
    free = s['free']
    created = 0
    for e in o['effects']:
        ok = e['created']
        if ok:
            a = FX + created * 0x100
            created += 1
            addrs.append(a)
            reads.append((a + 0x3C, 4, e['x']))
            reads.append((a + 0x40, 4, e['y']))
            reads.append((a + 0x44, 4, e['z']))
        else:
            addrs.append(0)
    stubs[0x8003BDD8] = addrs
    # the tables of the rings are read from the binary by the model through c.rh: list them
    import exelib as E
    for base in (H.TAB_X, H.TAB_Y):
        for i in range(32):
            reads.append((base + 2 * i, 2, (E.s16(base + 2 * i) & 0xFFFF)))
    H.LENIENT = False
    c = H.Ctx(reads, stubs)
    BODY[item](c, hero, item)
    H.LENIENT = True
    got_hp = H.s32(c.rw(hero + 0x14))
    got_mp = H.s16(c.rh(PLAYER + 4))
    got_rng = c.rw(H.RNG)
    exp_fx = []
    for e in o['effects']:
        exp_fx.append(e)
    ok = (got_hp == o['hp'] and got_mp == o['mp'] and got_rng == o['seed'])
    # effect fields
    for k, a in enumerate(a for a in addrs if a):
        e = [x for x in o['effects'] if x['created']][k]
        got = (c.rw(a + 0x3C), c.rw(a + 0x40), c.rw(a + 0x44), c.rw(a + 0x54), c.rw(a + 0x58), c.rw(a + 0x5C))
        want = (e['x3c'], e['y40'], e['z44'], e['fx'], e['fy'], e['fz'])
        if got != want:
            ok = False
    # creation args sequence
    want_calls = [[x for x in (0, 14, e['anim'], e['x'], e['y'], e['z'])] for e in o['effects']]
    got_calls = [call[1] for call in c.calls if call[0] == '0x8003bdd8']
    if [list(x) for x in got_calls] != want_calls:
        ok = False
    checked += 1
    if not ok:
        bad += 1
        if bad < 5:
            print('MISMATCH', hex(item), s, 'model hp/mp/rng', got_hp, got_mp, hex(got_rng), 'real', o['hp'], o['mp'], hex(o['seed']))
print('h1 bodies checked', checked, 'mismatches', bad)
sys.exit(1 if bad else 0)
