"""run_e0b1.py - E20.e0b1 oracle: the generator advance of the REAL frame function 0x8002BAEC of ALUN_CD.EXE (France), run on the MIPS machine of the E14/E20 oracles.

Run from the repository root:   python -B docs/plan-e20-annexe/e20-e0b/oracle/run_e0b1.py      (about 3 s; exit code 0 = every check of out/summary.txt is PASS)
It runs s7_frame.py (the discovery probe, byte copy: 24 calls of the frame function, with the pad sampler, the sound frame, DisplayInventory, MovePlayer, the effects pass and the hit pass
stubbed; the last three make ONE real draw each so that the order of the draws against the advance is visible), then derives from its journal:
  out/frame_trace.json     the 24 calls of the probe (md5 4943e0d29796bdee75e0a876f82b6b48 in the discovery)
  out/frame_vectors.json   the 10 rows of the slice, each with its predicted values (derived here from the formula, never read from the machine) and PASS / FAIL
  out/advance_words.json   the six words of the advance (0 -> E06A02E7 ...), computed by an independent model AND compared with the constants of the plan
  out/summary.txt          one line per check
  out/manifest.txt         md5 of every other file but mutations.txt, which mutants_e0b1.py writes (two runs are identical)
Nothing outside out/ is written.  The machine only reads ALUN_CD.EXE (path in tools/lib.py) and data-extracted/data (read only)."""
import os
import sys
import json
import hashlib
import subprocess

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, 'out')
M32 = 0xFFFFFFFF
K = 0x7D2B89DD
C = 0xE06A02E7
DISCOVERY_MD5 = '4943e0d29796bdee75e0a876f82b6b48'

# the six words of the plan ("Faits porteurs": 0 -> E06A02E7, FFFFFFFF -> 633E790A, 80000000 -> 606A02E7, B017C93D -> 35E36190, 12345678 -> B806E07F, four steps -> AFBE9EAC)
PLAN_WORDS = [(0x00000000, 1, 0xE06A02E7), (0xFFFFFFFF, 1, 0x633E790A), (0x80000000, 1, 0x606A02E7), (0xB017C93D, 1, 0x35E36190), (0x12345678, 1, 0xB806E07F), (0x12345678, 4, 0xAFBE9EAC)]

# the ten rows of the slice, picked from the 24 calls of the probe by (label, a0, gate, input word)
TEN = [
    ('gate sweep', 0, 0x00, 0x12345678),
    ('gate sweep', 0, 0x48, 0x12345678),
    ('gate sweep', 1, 0x00, 0x12345678),
    ('gate sweep', 1, 0x48, 0x12345678),
    ('trigger, DisplayInventory returns 1 (opened)', 0, 0x00, 0x12345678),
    ('trigger, DisplayInventory returns 0 (refused: the loop exits)', 0, 0x00, 0x12345678),
    ('post-load call a0=1, trigger, returns 0: request cleared at 0x8002BD3C', 1, 0x00, 0x12345678),
    ('gate 0x08: trigger refused (gate word must be 0)', 0, 0x08, 0x12345678),
    ('advance only, seed sweep', 0, 0x48, 0x00000000),
    ('advance only, seed sweep', 0, 0x48, 0xB017C93D),
]


def lcg(s):
    return (s * K + C) & M32


def step_n(s, n):
    for _ in range(n):
        s = lcg(s)
    return s


def main():
    os.makedirs(OUT, exist_ok=True)
    # 1. the probe, byte copy of the discovery script, run from this folder (it reads tools/ and espawn.py next to it)
    p = subprocess.run([sys.executable, '-B', 's7_frame.py'], cwd=HERE, capture_output=True, text=True)
    open(os.path.join(OUT, 's7_frame.stdout.txt'), 'w', newline='\n').write(p.stdout)
    if p.returncode != 0:
        sys.stderr.write(p.stderr)
        return 2
    raw = open(os.path.join(OUT, 'frame_trace.json'), 'rb').read()
    trace_md5 = hashlib.md5(raw).hexdigest()
    rows = json.loads(raw)
    lines = []
    ok_all = True

    def check(name, ok, detail=''):
        nonlocal ok_all
        ok_all = ok_all and ok
        lines.append('%s %s%s' % ('PASS' if ok else 'FAIL', name, (' - ' + detail) if detail else ''))

    check('frame_trace.json md5 equals the discovery', trace_md5 == DISCOVERY_MD5, trace_md5)
    check('the probe made 24 calls', len(rows) == 24, str(len(rows)))

    # 2. the six words of the advance, independent model against the constants of the plan
    words = []
    for seed, n, want in PLAN_WORDS:
        got = step_n(seed, n)
        words.append({'in': '%08X' % seed, 'steps': n, 'plan': '%08X' % want, 'model': '%08X' % got})
        check('advance word %08X x%d = %08X' % (seed, n, want), got == want, '%08X' % got)
    json.dump(words, open(os.path.join(OUT, 'advance_words.json'), 'w', newline='\n'), indent=0)

    # 3. the ten rows
    vec = []
    by_key = {(r['label'], r['a0'], r['gate'], int(r['rng_in'], 16)): r for r in rows}
    for i, key in enumerate(TEN):
        label, a0, gate, seed = key
        r = by_key[key]
        gated = (gate & 0x48) != 0
        # predicted: the gate 0x48 shuts every pass but the list pass (no marker draw), an open gate runs the three stubbed passes (one marker draw each), then ONE advance, the last operation
        markers = 0 if gated else 3
        pred_steps = markers + 1
        pred_out = step_n(seed, pred_steps)
        trigger = r['trigger']
        # request word: only a Start trigger refused by DisplayInventory (returns 0) with an open gate and a0 = 0 leaves it at 1 (a0 = 1 clears it at 0x8002BD3C)
        pred_req = 1 if (trigger and r['inventory_returns'] == 0 and not gated and a0 == 0) else 0
        advs = [t for t in r['trace'] if t[0] == 'ADVANCE']
        adv_ok = len(advs) == 1 and r['trace'][-1][0] == 'ADVANCE' and int(advs[0][3], 16) == lcg(int(advs[0][2], 16))
        ok = (r['steps'] == pred_steps and r['steps_ok'] and int(r['rng_out'], 16) == pred_out and r['request_word'] == pred_req and adv_ok and r['coverage']['advance_0x8002BD34'])
        vec.append({'row': i + 1, 'label': label, 'a0': a0, 'gate': '%02X' % gate, 'trigger': trigger, 'inventory_returns': r['inventory_returns'], 'rng_in': r['rng_in'], 'rng_out': r['rng_out'],
                    'predicted_out': '%08X' % pred_out, 'steps': r['steps'], 'predicted_steps': pred_steps, 'advance_events': len(advs), 'advance_is_last': r['trace'][-1][0] == 'ADVANCE',
                    'request_word': r['request_word'], 'predicted_request': pred_req, 'result': 'PASS' if ok else 'FAIL'})
        check('row %d: %s a0=%d gate=%02X in %s' % (i + 1, label, a0, gate, r['rng_in']), ok,
              'steps %d (predicted %d), advance events %d, out %s' % (r['steps'], pred_steps, len(advs), r['rng_out']))
    json.dump(vec, open(os.path.join(OUT, 'frame_vectors.json'), 'w', newline='\n'), indent=0)
    check('ten rows', len(vec) == 10, str(len(vec)))

    # 4. a0 has no effect on the number of steps nor on the word (same gate, same input word): rows 1/3 and 2/4
    for a, b in ((0, 2), (1, 3)):
        check('a0 without effect on rows %d and %d' % (a + 1, b + 1), vec[a]['rng_out'] == vec[b]['rng_out'] and vec[a]['steps'] == vec[b]['steps'])
    # 5. every one of the 24 calls advances exactly once (one ADVANCE event per call of the frame function)
    check('every call of the probe has exactly one advance event, the last operation', all(sum(1 for t in r['trace'] if t[0] == 'ADVANCE') == 1 and r['trace'][-1][0] == 'ADVANCE' for r in rows))

    open(os.path.join(OUT, 'summary.txt'), 'w', newline='\n').write('\n'.join(lines) + '\n')
    names = sorted(f for f in os.listdir(OUT) if f not in ('manifest.txt', 'mutations.txt'))
    with open(os.path.join(OUT, 'manifest.txt'), 'w', newline='\n') as f:
        for n in names:
            f.write('%s  %s\n' % (hashlib.md5(open(os.path.join(OUT, n), 'rb').read()).hexdigest(), n))
    print('\n'.join(lines))
    return 0 if ok_all else 1


if __name__ == '__main__':
    sys.exit(main())
