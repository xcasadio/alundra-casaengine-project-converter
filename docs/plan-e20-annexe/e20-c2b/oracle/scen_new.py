"""scen_new.py - E20.c2b T0 (c): the scenarios the c2 oracle removed and c2b needs, computed by the REAL code of ALUN_CD.EXE (France) in the MIPS emulator of E20.c2.
NEW FILE: nothing of e20-c2/oracle is edited; the hurt-hero world (hworld), the scenario builder (hscen.mk) and the writers (hscen.write_run_text / run_json) are imported, read only.

  A3  the Charm of Nava (response 0x80): a hit that spares the life but still hurts; the control without the charm
  A5  the hurt in the middle of the hero's own actions: walking, in the middle / at the end of a sword swing, rising, at the apex of a jump, just before and at the landing, with a
      charged holy sword (the charge counter and the pending-shot pointer)
  C   the hurt with a carried object (the PutInHands core fields written by the harness: hero +0x2C, object +0x28, hero state 0x0C)

The specs hold inputs only (pad words, harness writes between two ticks, setup pokes).  The "teleport" of the hero into the contact position is the harness write of A6 (event
teleport_x, PosX := 232 px).  Run (Python 3 + capstone, from a COPY of the annex laid out as in the repository, python -B):
    python -B docs/plan-e20-annexe/e20-c2b/oracle/scen_new.py [outdir]          # default outdir: <this folder>/out
Outputs: out/A3.txt/.json, out/A5.txt/.json, out/C-carried.txt/.json (the same formats as e20-c2/oracle/out) and out/summary-new.txt.
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
C2 = os.path.normpath(os.path.join(HERE, '..', '..', 'e20-c2', 'oracle'))
sys.path.insert(0, C2)
sys.path.insert(0, os.path.join(C2, 'tools'))
import c1world as C
import hworld as H
import hscen as HS
import scen as S
import world as W

RIGHT, UP, SQ, CR = W.RIGHT, W.UP, W.SQUARE, W.CROSS
PX = 65536


def tl(res, key='anim_target', cap=14):
    out, prev = [], None
    for r in res['rows']:
        a = r['hero'][key] & 0xFFFF
        if a != prev:
            out.append('t%d:%X' % (r['tick'], a))
            prev = a
    return ' '.join(out[:cap])


def touched(res):
    return [r['tick'] for r in res['rows'] if r['hero']['touching_slot'] is not None]


def mk(name, desc, dx=20, n=60, **kw):
    return HS.mk(name, desc, dx, n=n, **kw)


# ---- A3 -------------------------------------------------------------------------------------------------------------------------------------------------------
def a3():
    return [
        mk('A3-charm', 'Charm of Nava worn (inv charm, response index 6 = 0x80), the Beannoid\'s CurrentAttack := (attribute 0x06, power 4) 20 px east of the resting hero, stats 10/10, 60 ticks',
           20, 60, inv='charm', target_init={0: H.set_attack(0x06, 4)}),
        mk('A3-nocharm', 'control: kit (no charm), the same synthetic attack (0x06, 4): an ordinary attribute, damage', 20, 60, target_init={0: H.set_attack(0x06, 4)}),
        mk('A3-charm-orc', 'charm worn, the real Orc of map 15 (attribute 0x02: response 0x0F, not 0x80), 60 ticks', 20, 60, inv='charm', kind='map15-orc', hp=12, hpmax=12),
    ]


# ---- A5 -------------------------------------------------------------------------------------------------------------------------------------------------------
def a5():
    L = []
    tel = lambda k: {k: [('teleport_x', 232)]}
    L.append(mk('A5-walk', 'walking: Right held from tick 1 from 60 px west of the Beannoid (A1 geometry), 100 ticks', 60, 100, after=RIGHT))
    L.append(mk('A5-walk-released', 'walking: Right held to tick 17 and released from 18 (the contact is reached at tick 17), 100 ticks', 60, 100, holds={t: RIGHT for t in range(1, 18)}))
    for k in (2, 8, 14, 21, 22):
        L.append(mk('A5-swing-t%d' % k, 'sword swing (Square at tick 1, anim 0x10 for ticks 1..21): the hero is put in contact (PosX := 232 px) before tick %d, 80 ticks' % k, 60, 80, holds={1: SQ}, events=tel(k)))
    L.append(mk('A5-rise-t2', 'jump (Cross at tick 1) rising: contact before tick 2, Beannoid on the ground, 80 ticks', 60, 80, holds={1: CR}, events=tel(2)))
    L.append(mk('A5-air-t10', 'jump: contact before tick 10 (the apex), Beannoid on the ground: no touch while the hero is high, 80 ticks', 60, 80, holds={1: CR}, events=tel(10)))
    L.append(mk('A5-apex-h8', 'jump: contact before tick 10 (apex ticks 10 and 11), the Beannoid raised by 8 (target height argument): touched in the air, 80 ticks', 60, 80, holds={1: CR}, events=tel(10),
                targets=[(S.TXPOS, S.TYPOS, 8)]))
    for k in (19, 20, 21, 22):
        L.append(mk('A5-land-t%d' % k, 'jump: contact before tick %d (the hero lands at tick 21), Beannoid on the ground, 80 ticks' % k, 60, 80, holds={1: CR}, events=tel(k)))
    L.append(mk('A5-charge', 'holy sword (inv holy), Square held from tick 1 (the charge counter and the aura build), contact before tick 70, 140 ticks', 60, 140, inv='holy', holds={t: SQ for t in range(1, 80)},
                events=tel(70)))
    # a harness write of the pending-shot pointer [0x80127110] (the value is the one the real swing writes at its tick 1) during the charge state: the resting idle state clears it at once
    # (so a poke in Idle is not a test), the charge state keeps it; the control (no contact) shows whether it survives to the contact tick
    def poke_w(m, slots):
        m.poke(H.WPTR, 0x80028C79)
    L.append(mk('A5-charge-wptr', 'holy sword, Square held from tick 1; before tick 50 the harness writes [0x80127110] := 0x80028C79; contact before tick 70, 140 ticks', 60, 140, inv='holy',
                holds={t: SQ for t in range(1, 80)}, events=tel(70), before={50: poke_w}))
    L.append(mk('A5-charge-wptr-nocontact', 'control: as A5-charge-wptr without the contact (does the written pointer survive in the charge state?), 140 ticks', 60, 140, inv='holy',
                holds={t: SQ for t in range(1, 80)}, before={50: poke_w}))
    L.append(mk('A5-charge-nocontact', 'control: as A5-charge without the contact (to read the charge counter and the pointer without a hurt), 140 ticks', 60, 140, inv='holy', holds={t: SQ for t in range(1, 80)}))
    return L


# ---- C: the carried object ------------------------------------------------------------------------------------------------------------------------------------
OBJ_FIELDS = (('status', 0x10), ('f24', 0x24), ('f28', 0x28), ('anim88', 0x88), ('dir8C', 0x8C), ('x114', 0x114), ('y118', 0x118), ('z11C', 0x11C), ('f30', 0x30), ('f34', 0x34), ('f38', 0x38))
CARRY_COUNTER = 0x80098F28


def carried_setup(m):
    """the PutInHands core (e20-g1a report 1.4, harness writes): hero +0x2C := the object, object +0x28 := the hero, hero +0x88 := 0x0C (carry, idle), the object offsets
       (+0x30/34/38) := object - hero; the object sits on the hero's head (z + 0x200000) as UpdatePlayerCarriedEntity(0) keeps it"""
    obj = m.peek(0x80134360)                  # hworld.setup_h poked the highest target slot there just before this hook
    o = C.ent(obj)
    hx, hy, hz = m.peek(H.HERO + 0x114), m.peek(H.HERO + 0x118), m.peek(H.HERO + 0x11C)
    m.poke(o + 0x114, hx)
    m.poke(o + 0x118, hy)
    m.poke(o + 0x11C, (hz + 0x200000) & 0xFFFFFFFF)
    m.poke(H.HERO + 0x2C, o)
    m.poke(o + 0x28, H.HERO)
    m.poke(H.HERO + 0x88, 0x0C)
    m.poke(o + 0x30, 0)
    m.poke(o + 0x34, 0)
    m.poke(o + 0x38, 0x200000)


def carried():
    rec = {}

    def mkc(name, desc, **kw):
        sp = mk(name, desc, 20, 40, targets=[(S.TXPOS, S.TYPOS, 0), (S.TXPOS - 3, S.TYPOS - 3, 0)], setup=carried_setup, **kw)
        series = []
        rec[name] = series

        def snap(m, slots, series=series):
            o = C.ent(slots[1])
            series.append(dict(obj={k: m.peeks(o + off) for k, off in OBJ_FIELDS}, hero_2C=m.peek(H.HERO + 0x2C), counter=m.peek(CARRY_COUNTER)))
        sp['before'] = {t: snap for t in range(1, sp['n'] + 2)}
        return sp
    L = [mkc('C-carried', 'the hero carries a second target (object) on his head (harness writes of the PutInHands core, state 0x0C), touched by the Beannoid at tick 1 (20 px at rest), 40 ticks'),
         mkc('C-carried-walk', 'as C-carried with Right held from tick 1 from 60 px west (the hurt comes at tick 18 while walking with the object), 60 ticks', after=RIGHT)]
    L[1]['hero_px'] = S.hero_at(60)
    L[1]['n'] = 60
    L[1]['before'] = {t: L[1]['before'][1] for t in range(1, 62)}
    return L, rec


def facts_extra(res):
    h = res['rows']
    L = []
    L.append('touched at ticks %s; hurt entries at %s; HP %s; stats HP %s' % (HS.rngs(touched(res)), HS.rngs(HS.hurt_ticks(res)), [c[2] for c in HS.changes(res, 'hp')][:5],
                                                                          [c[2] for c in HS.changes(res, 'stats_hp', group='g')][:5]))
    L.append('hero state timeline (anim_target): %s' % tl(res))
    L.append('hero charge counter [0x80126FFC] / pending-shot pointer [0x80127110]: ticks with charge != 0: %s; wptr changes: %s' % (
        HS.rngs([r['tick'] for r in h if r['g']['charge']]), ', '.join('t%d 0x%X->0x%X' % c for c in HS.changes(res, 'wptr', group='g')[:6]) or 'none'))
    L.append('hero +0x2C (carried) at the end 0x%X; draw-list absence (blink) at %s' % (h[-1]['hero']['carried_2C'], HS.rngs(HS.hidden_ticks(res), cap=8)))
    return L


def main():
    outdir = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, 'out')
    os.makedirs(outdir, exist_ok=True)
    summary = []
    groups = [('A3', a3(), 'the Charm of Nava (response 0x80)'), ('A5', a5(), 'the hurt in the middle of the hero\'s own actions')]
    cl, rec = carried()
    groups.append(('C-carried', cl, 'the hurt with a carried object'))
    for name, specs, desc in groups:
        results = [H.run(sp) for sp in specs]
        L = ['# %s: %s (E20.c2b T0 (c), new scenarios; the real code of ALUN_CD.EXE (France) in the MIPS emulator; sound, effects, spawn and the targets\' programs are the journalled stubs of c1world)' % (name, desc),
             '# per-tick values are the state AFTER the passes of that tick; tick 0 = start state; no expected value here: see the plan and the tests', '']
        for res in results:
            HS.write_run_text(res, L)
            for line in facts_extra(res):
                L.append('   + ' + line)
            if name == 'C-carried':
                series = rec[res['spec']['name']]
                L.append('   + carried object (state BEFORE tick t, t = 1.. ; the fields of the object slot, the hero +0x2C and the counter [0x80098F28]):')
                prev = None
                for t, s in enumerate(series, 1):
                    cur = (tuple(s['obj'].items()), s['hero_2C'], s['counter'])
                    if cur != prev:
                        L.append('     before t%d: hero+0x2C=0x%X counter=%d obj %s' % (t, s['hero_2C'], s['counter'], ' '.join('%s=%d' % kv if kv[0] != 'f24' else 'f24=%d' % kv[1] for kv in s['obj'].items())))
                    prev = cur
            L.append('')
        with open(os.path.join(outdir, name + '.txt'), 'w', encoding='utf-8', newline='\n') as f:
            f.write('\n'.join(L) + '\n')
        js = dict(name=name, desc=desc, runs=[HS.run_json(r) for r in results])
        if name == 'C-carried':
            js['carried_series'] = {k: v for k, v in rec.items()}
        with open(os.path.join(outdir, name + '.json'), 'w', encoding='utf-8', newline='\n') as f:
            f.write(json.dumps(js, sort_keys=True, separators=(',', ':'), default=str) + '\n')
        for res in results:
            h = res['rows']
            ht = HS.hurt_ticks(res)
            summary.append('%-22s touched %-14s hurt %-12s HP %-14s timeline %s' % (res['spec']['name'], HS.rngs(touched(res), cap=3), HS.rngs(ht, cap=3), [c[2] for c in HS.changes(res, 'hp')][:3], tl(res, cap=8)))
    with open(os.path.join(outdir, 'summary-new.txt'), 'w', encoding='utf-8', newline='\n') as f:
        f.write('\n'.join(summary) + '\n')
    print('\n'.join(summary))


if __name__ == '__main__':
    main()
