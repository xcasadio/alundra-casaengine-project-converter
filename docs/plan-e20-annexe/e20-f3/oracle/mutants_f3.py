"""mutants_f3.py - E20.f3 T0: the one-word mutants of the oracle (teeth of the f3 facts).  Real patches of the emulated ALUN_CD.EXE (France), applied to RAM after the world is built,
never simulated.  Every mutant is compared with its control on a stated key and must be KILLED (the key changes) or PROVEN EQUIVALENT in writing (the reason is printed).

  W1  0x80039EA4  spawn Z without the "+1"            key: wave status/rawZ/ground_144/adj_13C and life        (wave_mut.py, re-run here)
  W2  0x8007A960  C61 call removed                     key: grass cells cut from tick 117 on                    (wave_mut.py)
  W3  0x8007F244  E10 DestroyEntity removed            key: the wave never leaves status 3                       (wave_mut.py)
  H1..H4          the four patches of hitfx_mut.py     key: pairs that differ from the decoded rule              (hitfx_mut.py)
  D1  0x80023648/0x8002364C  direction table {0,0x10,8,0x18} permuted to {0,8,0x10,0x18}      key: the arrow's dir_t, hero facing north
  D2  0x8002F7C8  release-delay comparison inverted    key: the arrow's spawn tick
  D3  0x8002F7E8  z byte of ptr[3]: shift 16 -> 17     key: the arrow's raw Z
  D4  0x8002F828  the weapon pointer is not cleared after the spawn      key: number of arrows born
  D5  0x8002F7B4  animation flag b1 ignored            key: arrow born / not born when the animation changes during the delay

usage (from the repository root):  python -B docs/plan-e20-annexe/e20-f3/oracle/mutants_f3.py        writes out/mutations.txt and out/mutations.json
"""
import os
import sys
import json

HERE = os.path.dirname(os.path.abspath(__file__))
TOOLS = os.path.join(HERE, 'tools')
sys.path.insert(0, TOOLS)
import pworld as P
import world as W
import hitfx as H
import hitfx_mut as HM
from wave_run import run_wave
from wave_grass import grass, cut_events
from wave_mut import patch, summarize

OUT = os.path.join(HERE, 'out')
PROJ = (3, 4, 7, 11, 12, 14, 15, 27, 28, 29)


def forced_anim_ticker(tick, anim):
    """harness stimulus: at tick `tick` the hero's TARGET animation (+0x88) is overwritten, as a hurt or fall animation would do; both control and mutant get it"""
    def f(m, pad, hold):
        if m.tick_no == tick:
            m.poke(W.ENT0 + 0x88, anim)
        W.tick(m, pad, hold)
    return f


def shot(init=None, holds=None, n=40, direction=24, item=5, ticker=None):
    """Hunter's bow (item 5) unless told otherwise.  Returns the births of projectile slots: (tick, slot, dir_t, rawZ, x)."""
    spec = dict(item=item, n=n, holds=holds if holds is not None else {1: W.SQUARE}, after=0, direction=direction, init=init or [])
    if ticker:
        spec['ticker'] = ticker
    res = P.run(spec)
    seen = {}
    for r in res['rows']:
        for s, d in r['slots'].items():
            if s != 0 and d['type68'] in PROJ and s not in seen:
                seen[s] = (r['tick'], s, d['dir_t'], d['pz'], d['px'] // 65536)
    return sorted(seen.values())


def cmp_row(name, addr_desc, key, control, mutant):
    killed = control != mutant
    return dict(name=name, patch=addr_desc, key=key, control=control, mutant=mutant, verdict='KILLED' if killed else 'SURVIVED')


def main():
    rows = []
    # ---- wave mutants ---------------------------------------------------------------------------------------------------
    ctl = run_wave('c', direction='east', n=170)
    life = lambda res: [(r['tick'], r['slots'][1]['status'], r['slots'][1]['pz'], r['slots'][1]['ground_144'], r['slots'][1]['adj_13C']) for r in res['rows'] if r['slots'].get(1)]
    res = run_wave('w1', direction='east', n=170, init=[patch((0x80039EA4, 0x24420000))])
    rows.append(cmp_row('W1', '0x80039EA4 addiu v0,v0,1 -> addiu v0,v0,0', 'wave life (first tick, last tick, last status)', summarize(ctl), summarize(res)))
    res = run_wave('w2', direction='east', n=170, cells=grass, init=[patch((0x8007A960, 0x00000000))])
    ctl_g = run_wave('cg', direction='east', n=170, cells=grass)
    rows.append(cmp_row('W2', '0x8007A960 jal CheckAndTriggerTileEffect -> nop', 'grass cells cut at t>=117', len([e for e in cut_events(ctl_g) if e[0] >= 117]), len([e for e in cut_events(res) if e[0] >= 117])))
    res = run_wave('w3', direction='east', n=170, init=[patch((0x8007F244, 0x00000000))])
    rows.append(cmp_row('W3', '0x8007F244 jal DestroyEntity -> nop', 'wave life (first tick, last tick, last status)', summarize(ctl), summarize(res)))
    # ---- hit-pass mutants -----------------------------------------------------------------------------------------------
    n0, bad0 = HM.run([])
    hm = (('H1', '0x800395E8 andi v0,v0,0xC0 -> andi v0,v0,0x80', [(0x800395E8, 0x30420080)]),
          ('H2', '0x800395F8 li v0,0xA -> li v0,0xB (effect 4 condition)', [(0x800395F8, 0x2402000B)]),
          ('H3', '0x80039644 li a1,5 -> li a1,4 (effect id)', [(0x80039644, 0x24050004)]),
          ('H4', '0x800395EC beq -> bne (gate)', [(0x800395EC, 0x1443001F)]))
    for name, desc, pw in hm:
        n, bad = HM.run(pw)
        rows.append(cmp_row(name, desc, 'pairs differing from the decoded rule (of %d)' % n, len(bad0), len(bad)))
    # ---- shot mutants ---------------------------------------------------------------------------------------------------
    c_north = shot(direction=16)
    m_dir = shot(init=[patch((0x80023648, 0x00000008), (0x8002364C, 0x00000010))], direction=16)
    rows.append(cmp_row('D1', '0x80023648 := 8, 0x8002364C := 0x10 (indices 1 and 2 swapped)', 'arrow births (tick, slot, dir_t, rawZ, x), hero facing north', c_north, m_dir))
    c_ground = shot()
    m_delay = shot(init=[patch((0x8002F7C8, 0x0043102A))])
    rows.append(cmp_row('D2', '0x8002F7C8 slt v0,v1,v0 -> slt v0,v0,v1', 'arrow births, ground, Square at t1', c_ground, m_delay))
    m_z = shot(init=[patch((0x8002F7E8, 0x00021440))])
    rows.append(cmp_row('D3', '0x8002F7E8 sll v0,v0,16 -> sll v0,v0,17', 'arrow births (rawZ)', c_ground, m_z))
    m_ptr = shot(init=[patch((0x8002F828, 0x00000000))])
    rows.append(cmp_row('D4', '0x8002F828 sw zero,0x7110(s0) -> nop', 'arrow births (count)', c_ground, m_ptr))
    # D5: the animation flag b1.  Find, by running, a pad history that changes the animation during the delay.
    d5 = None
    candidates = {
        'Square t1, Cross t3': {1: W.SQUARE, 3: W.CROSS},
        'Square t1, Cross t2': {1: W.SQUARE, 2: W.CROSS},
        'Square t1, Left held t2..t9': {**{1: W.SQUARE}, **{t: W.LEFT for t in range(2, 10)}},
        'Square t1, Square t3': {1: W.SQUARE, 3: W.SQUARE},
        'Square t1, Triangle t3': {1: W.SQUARE, 3: W.TRIANGLE},
        'Square t1, target animation forced to 0 at t3': ('forced', 3, 0),
    }
    tried = []
    for label, holds in candidates.items():
        if isinstance(holds, tuple):
            tk = forced_anim_ticker(holds[1], holds[2])
            c = shot(n=40, ticker=tk)
            mu = shot(n=40, ticker=tk, init=[patch((0x8002F7B4, 0x00000000))])
        else:
            c = shot(holds=holds, n=40)
            mu = shot(holds=holds, n=40, init=[patch((0x8002F7B4, 0x00000000))])
        tried.append((label, c, mu))
        if c != mu and d5 is None:
            d5 = (label, c, mu)
    if d5:
        rows.append(cmp_row('D5', '0x8002F7B4 beqz v0 -> nop (animation flag b1 ignored)', 'arrow births, pad history "%s"' % d5[0], d5[1], d5[2]))
    else:
        rows.append(dict(name='D5', patch='0x8002F7B4 beqz v0 -> nop (animation flag b1 ignored)', key='arrow births under %d pad histories' % len(tried), control=[t[1] for t in tried], mutant=[t[2] for t in tried], verdict='SURVIVED'))
    out = dict(rows=rows, d5_tried=[dict(history=a, control=b, mutant=c) for a, b, c in tried])
    os.makedirs(OUT, exist_ok=True)
    json.dump(out, open(os.path.join(OUT, 'mutations.json'), 'w'), indent=1, sort_keys=True)
    lines = ['# E20.f3 T0 mutants: %d one-word patches of the emulated binary; control vs mutant on the stated key' % len(rows)]
    for r in rows:
        lines.append('%-3s %-9s %s' % (r['name'], r['verdict'], r['patch']))
        lines.append('      key     : %s' % r['key'])
        lines.append('      control : %s' % (r['control'],))
        lines.append('      mutant  : %s' % (r['mutant'],))
    lines.append('D5 pad histories tried (control births | mutant births):')
    for a, b, c in tried:
        lines.append('   %-32s %s | %s' % (a, b, c))
    lines.append('killed %d of %d' % (sum(1 for r in rows if r['verdict'] == 'KILLED'), len(rows)))
    open(os.path.join(OUT, 'mutations.txt'), 'w').write('\n'.join(lines) + '\n')
    print('\n'.join(lines))
    return 0 if all(r['verdict'] == 'KILLED' for r in rows) else 2


if __name__ == '__main__':
    sys.exit(main())
