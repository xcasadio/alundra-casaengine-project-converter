"""keys_h1.py - E20.h1 discovery: the WORLD KEYS of the five instant items on the R-EL-4 bench (docs/plan-e20-heros.md section 1.31, "Regles communes aux tranches d'element"),
run on the emulated binary (real MovePlayer, real TryUseItem 0x8002ED64, real start dispatcher 0x8003499C, real start and heal functions, real UseItem, real passes).

Bench = the h4 machine in its FAITHFUL form (the one of e20-h4d1/critic/c/h4o/fire2_key_c.py): the hero at (100, 100) px facing east (direction 24) on the flat synthetic field, the 64 entity
slots written with their index (+0 = i) and +0x48 = -1 as the map load does (0x8003B2BC; R-EL-1), seed 0x12345678 posed before the first tick, Circle pressed at tick 1, ONE tick per frame.
The item is given by the real AddOneItemIfUnlocked (0x8004E530) and equipped (stats + 0xC), the life is LOWERED first (hero entity +0x14), the magic set in the stats.
Differences with fire2_key_c.py: the effect stub 0x8003BDD8 hands out a DISTINCT effect object for every creation (so that all the fields each creation leaves can be read, not only the
last one's), and can refuse after K creations (a full effect pool: the real function then returns 0).
--pf adds the frame-end step of the generator after each tick (s <- s * 0x7D2B89DD + 0xE06A02E7) AFTER the tick's own record (the key is then "_pf": rng1 = after the draws, rng0 of the
next tick = pf-step(rng1)).
usage: python -B keys_h1.py OUTDIR      (writes OUTDIR/keys/*.json and OUTDIR/keys/manifest.txt; run it twice into two folders and compare the manifests)
"""
import hashlib
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, 'h4o'))
import spellworld as S      # noqa: E402
import world as W           # noqa: E402
from machine import M32, s32     # noqa: E402

HERO = 0x80127D30
STRIDE = 0x294
RNG = 0x80098708
MULT, ADD = 0x7D2B89DD, 0xE06A02E7
IN_USE, COUNTER, PHASE = 0x80127164, 0x8012715C, 0x80127160
EFF_BASE = 0x801E8000       # the harness's own effect objects: EFF_BASE + 0x100 * k (above the stub's single slot of machine.py)


def ent(i):
    return HERO + STRIDE * i


def lcg(s):
    return (s * MULT + ADD) & M32


def steps_between(a, b, limit=200):
    s = a
    for k in range(limit + 1):
        if s == b:
            return k
        s = lcg(s)
    return None


def install_effect_stub(m, free_slots):
    """0x8003BDD8: journal the call, hand out a distinct zeroed 0x100-byte object; after `free_slots` creations in the whole run, answer 0 (a full pool)"""
    state = {'k': 0, 'made': []}

    def hook(mm):
        sp = mm.r[29]
        args = [mm.r[4], mm.r[5], mm.r[6], mm.r[7]] + [mm.peek(sp + 0x10 + 4 * i) for i in range(2)]
        args = [x & M32 for x in args]
        n = len(state['made'])
        if free_slots is not None and n >= free_slots:
            mm.journal.append((mm.tick_no, 'effect_3BDD8', args + ['refused']))
            mm.r[2] = 0
            state['made'].append((None, args))
            return
        p = EFF_BASE + 0x100 * n
        mm.zero(p, 0x100)
        mm.poke(p + 0x3C, args[3])           # InitEffect 0x8003BB14 leaves the position in +0x3C/+0x40/+0x44 and the forces +0x54/+0x58/+0x5C at 0 (the template copy)
        mm.poke(p + 0x40, args[4])
        mm.poke(p + 0x44, args[5])
        mm.journal.append((mm.tick_no, 'effect_3BDD8', args))
        mm.r[2] = p
        state['made'].append((p, args))
    m.hooks[0x8003BDD8] = hook
    return state


def run(item, hp, hpmax, mp, mpmax, count, ticks, presses=(1,), pf=True, free_slots=None, relower=None, seed=0x12345678, hpmax_poke=None, label=''):
    """one key: the rows of every tick.  relower = {tick: (hp, mp)}: values poked right before that tick (to use the item again)."""
    m, STATS, placed = S.make(item=item, mp=mp, mpmax=mpmax, seed=seed, indices=(1, 2), extra_items=[item] * (count - 1))
    P = ent(0)
    # R-EL-1: the map load writes the slot index and +0x48 = -1 over every slot, and the blank template copied over a destroyed slot has +0x48 = -1
    m.poke(0x80134368 + 0x48, 0xFFFFFFFF)
    for i in range(1, 64):
        m.poke(ent(i), i)
        m.poke(ent(i) + 0x48, 0xFFFFFFFF)
    if hpmax_poke is not None:
        m.poke(P + 0x18, hpmax_poke)
    m.poke(P + 0x14, hp)
    if hpmax_poke is not None or hp != 10:
        pass
    st = install_effect_stub(m, free_slots)
    pad = W.Pad()
    rows = []
    jn = 0
    made_before = 0
    inv_entry = m.peek(0x8017631C)            # inventory table pointer
    for t in range(1, ticks + 1):
        if relower and t in relower:
            lh, lm = relower[t]
            m.poke(P + 0x14, lh)
            m.poke16(STATS + 4, lm)
        rng0 = m.peek(RNG)
        m.tick_no = t
        W.tick(m, pad, W.CIRCLE if t in presses else 0, check_pad=False)
        rng1 = m.peek(RNG)
        rng2 = lcg(rng1) if pf else rng1
        if pf:
            m.poke(RNG, rng2)
        ev = []
        for j in m.journal[jn:]:
            if j[0] != t:
                continue
            if j[1] == 'sound':
                ev.append(['sound', j[2]])
            elif j[1] == 'effect_3BDD8':
                ev.append(['effect', [s32(x) for x in j[2][:6]] + ([j[2][6]] if len(j[2]) > 6 else [])])
        jn = len(m.journal)
        eff = []
        for p, args in st['made'][made_before:]:
            if p is None:
                eff.append({'obtained': False, 'args': [s32(x) for x in args[:6]]})
            else:
                eff.append({'obtained': True, 'args': [s32(x) for x in args], 'x3c': m.peeks(p + 0x3C), 'y40': m.peeks(p + 0x40), 'z44': m.peeks(p + 0x44), 'fx54': m.peeks(p + 0x54), 'fy58': m.peeks(p + 0x58), 'fz5c': m.peeks(p + 0x5C)})
        made_before = len(st['made'])
        stats_hp = m.peeks(STATS + 0) & 0xFFFF
        rows.append(dict(
            t=t, hero_hp=m.peeks(P + 0x14), hero_hpmax=m.peeks(P + 0x18), stats_hp=m.peek(STATS + 0) & 0xFFFF, stats_hpmax=m.peek(STATS + 2) & 0xFFFF,
            stats_mp=m.peek(STATS + 4) & 0xFFFF, stats_mpmax=m.peek(STATS + 6) & 0xFFFF,
            count=m.peek(m.peek(0x8017631C) + 4 * item) >> 16 & 0xFFFF, in_use=m.peeks(IN_USE), phase=m.peeks(PHASE), counter=m.peeks(COUNTER),
            hero_anim_t=m.peeks(P + 0x88), hero_anim_c=m.peeks(P + 0x90), pos=(m.peeks(P + 0x114), m.peeks(P + 0x118), m.peeks(P + 0x11C)),
            rng0=rng0, rng1=rng1, rng2=rng2, draws=steps_between(rng0, rng1), events=ev, effects=eff))
    unread = sorted((pc, a, n, c) for (pc, a, n), c in m.uninit_reads.items())
    return dict(label=label, item=item, args=dict(hp=hp, hpmax=hpmax_poke, mp=mp, mpmax=mpmax, count=count, ticks=ticks, presses=list(presses), pf=pf, free_slots=free_slots, relower=relower, seed=seed),
                uninitialised_reads=unread, rows=rows)


# ---- the keys ------------------------------------------------------------------------------------------------------------------------------------------------------------
HERB, LPOT, ELIX, ALE, SPOT = 0x24, 0x25, 0x26, 0x27, 0x29
KEYS = []


def key(name, **kw):
    KEYS.append((name, kw))


# herb: + max(5, HpMax / 4)
key('herb_hp3of10', item=HERB, hp=3, hpmax=10, mp=4, mpmax=4, count=2, ticks=40)
key('herb_hp8of10_cap', item=HERB, hp=8, hpmax=10, mp=4, mpmax=4, count=2, ticks=40)
key('herb_hp10of10_refused', item=HERB, hp=10, hpmax=10, mp=4, mpmax=4, count=2, ticks=40)
key('herb_hp3of24', item=HERB, hp=3, hpmax=24, mp=4, mpmax=4, count=2, ticks=40, hpmax_poke=24)
key('herb_hp3of40', item=HERB, hp=3, hpmax=40, mp=4, mpmax=4, count=2, ticks=40, hpmax_poke=40)
key('herb_hp3of25_odd', item=HERB, hp=3, hpmax=25, mp=4, mpmax=4, count=2, ticks=40, hpmax_poke=25)
key('herb_last_one_then_none', item=HERB, hp=3, hpmax=10, mp=4, mpmax=4, count=1, ticks=60, presses=(1, 30), relower={29: (3, 4)})
key('herb_pool_full', item=HERB, hp=3, hpmax=10, mp=4, mpmax=4, count=2, ticks=40, free_slots=0)
# L potion: Hp = HpMax
key('lpotion_hp3of10', item=LPOT, hp=3, hpmax=10, mp=4, mpmax=4, count=2, ticks=40)
key('lpotion_hp10of10_refused', item=LPOT, hp=10, hpmax=10, mp=4, mpmax=4, count=2, ticks=40)
key('lpotion_hp1of24', item=LPOT, hp=1, hpmax=24, mp=4, mpmax=4, count=2, ticks=40, hpmax_poke=24)
key('lpotion_pool_3_free', item=LPOT, hp=3, hpmax=10, mp=4, mpmax=4, count=2, ticks=40, free_slots=3)
# S potion: + max(10, HpMax / 2), four sparks
key('spotion_hp3of10', item=SPOT, hp=3, hpmax=10, mp=4, mpmax=4, count=2, ticks=40)
key('spotion_hp3of30', item=SPOT, hp=3, hpmax=30, mp=4, mpmax=4, count=2, ticks=40, hpmax_poke=30)
key('spotion_hp3of25_odd', item=SPOT, hp=3, hpmax=25, mp=4, mpmax=4, count=2, ticks=40, hpmax_poke=25)
key('spotion_hp10of10_refused', item=SPOT, hp=10, hpmax=10, mp=4, mpmax=4, count=2, ticks=40)
key('spotion_pool_2_free', item=SPOT, hp=3, hpmax=10, mp=4, mpmax=4, count=2, ticks=40, free_slots=2)
key('spotion_pool_full', item=SPOT, hp=3, hpmax=10, mp=4, mpmax=4, count=2, ticks=40, free_slots=0)
key('spotion_two_uses', item=SPOT, hp=3, hpmax=30, mp=4, mpmax=4, count=2, ticks=70, hpmax_poke=30, presses=(1, 35), relower={34: (3, 4)})
# Magic elixir: Mp = MpMax
key('elixir_mp1of4', item=ELIX, hp=10, hpmax=10, mp=1, mpmax=4, count=2, ticks=40)
key('elixir_mp0of4', item=ELIX, hp=10, hpmax=10, mp=0, mpmax=4, count=2, ticks=40)
key('elixir_mp4of4_refused', item=ELIX, hp=10, hpmax=10, mp=4, mpmax=4, count=2, ticks=40)
key('elixir_mp1of3', item=ELIX, hp=10, hpmax=10, mp=1, mpmax=3, count=2, ticks=40)
key('elixir_hp3_mp4_refused', item=ELIX, hp=3, hpmax=10, mp=4, mpmax=4, count=2, ticks=40)
# Cure Ale: Hp = HpMax and Mp = MpMax, eight rings and eight sparks
key('cureale_hp3_mp1', item=ALE, hp=3, hpmax=10, mp=1, mpmax=4, count=2, ticks=40)
key('cureale_hp10_mp1', item=ALE, hp=10, hpmax=10, mp=1, mpmax=4, count=2, ticks=40)
key('cureale_hp3_mp4', item=ALE, hp=3, hpmax=10, mp=4, mpmax=4, count=2, ticks=40)
key('cureale_hp10_mp4_refused', item=ALE, hp=10, hpmax=10, mp=4, mpmax=4, count=2, ticks=40)
key('cureale_pool_5_free', item=ALE, hp=3, hpmax=10, mp=1, mpmax=4, count=2, ticks=40, free_slots=5)
# a Circle held from the first tick: one use only (the front, not the hold)
key('herb_circle_held_30', item=HERB, hp=3, hpmax=10, mp=4, mpmax=4, count=3, ticks=40, presses=tuple(range(1, 31)))


def main(outdir):
    kd = os.path.join(outdir, 'keys')
    os.makedirs(kd, exist_ok=True)
    manifest = []
    for name, kw in KEYS:
        args = dict(kw)
        args.pop('hp'), args.pop('hpmax')
        d = run(item=kw['item'], hp=kw['hp'], hpmax=kw['hpmax'], mp=kw['mp'], mpmax=kw['mpmax'], count=kw['count'], ticks=kw['ticks'], presses=kw.get('presses', (1,)),
                pf=True, free_slots=kw.get('free_slots'), relower=kw.get('relower'), hpmax_poke=kw.get('hpmax_poke'), label=name)
        d['pf'] = True
        txt = json.dumps(d, sort_keys=True, separators=(',', ':'))
        path = os.path.join(kd, 'key_%s_pf.json' % name)
        with open(path, 'w') as f:
            f.write(txt)
        manifest.append('%s  %d  %s' % (hashlib.sha1(txt.encode()).hexdigest(), len(txt), os.path.basename(path)))
        assert not d['uninitialised_reads'], (name, d['uninitialised_reads'][:3])
    with open(os.path.join(kd, 'manifest.txt'), 'w') as f:
        f.write('\n'.join(manifest) + '\n')
    print('%d keys written to %s' % (len(KEYS), kd))


if __name__ == '__main__':
    main(sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, 'out1'))
