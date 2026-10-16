"""run_e0b2.py - E20.e0b2 T0: runs the whole oracle of the loot roll in order, checks it against the plan and writes out/summary_e0b2.txt and out/manifest_e0b2.txt (md5 of every output).

Run from anywhere:  python -B docs/plan-e20-annexe/e20-e0b/oracle/run_e0b2.py [--quick]   (--quick skips the 168-mutant battery p5_mutants.py, about 90 s)
Exit 0 = every check PASS.  Two runs give the same manifest.  Needs Python 3 with capstone, the disc EXE named in tools/lib.py, and the map JSONs of data-extracted/data (read only).
Nothing outside oracle/out/ is written.
"""
import hashlib
import json
import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
os.chdir(HERE)
OUT = os.path.join(HERE, 'out')
os.makedirs(OUT, exist_ok=True)
quick = '--quick' in sys.argv
STEPS = ['s1_table', 's2_choose', 's3_roll', 's5_spawn', 's6_record', 's8_veto', 'p1_roll', 'v8', 's10_mutants'] + ([] if quick else ['p5_mutants'])
lines = []
fail = 0


def check(label, ok):
    global fail
    lines.append('%-4s %s' % ('PASS' if ok else 'FAIL', label))
    if not ok:
        fail += 1


for s in STEPS:
    with open(os.path.join(OUT, s + '.stdout.txt'), 'wb') as f:
        rc = subprocess.run([sys.executable, '-B', s + '.py'], stdout=f, stderr=subprocess.STDOUT, cwd=HERE).returncode
    check('%s.py exits 0' % s, rc == 0)


def load(n):
    return json.load(open(os.path.join(OUT, n)))


def text(n):
    return open(os.path.join(OUT, n), encoding='utf-8', errors='replace').read()


check('choose_vectors.json: 2626 vectors (codes 0..255)', len(load('choose_vectors.json')) == 2626)
check('choose_high_vectors.json: 256 vectors (codes 0x100..0x1FF, all item 0 and no draw)', len(load('choose_high_vectors.json')) == 256 and all(v[2] == 0 and v[4] == 0 for v in load('choose_high_vectors.json')))
check('s2_choose: 0 mismatches over 2882 calls', 'mismatches between the model and the real function: 0 of 2882 calls' in text('s2_choose.stdout.txt'))
check('roll_vectors.json: 52 vectors, no write outside +0x3C/+0x40/the word (noise 0xA5A5A5A5)', len(load('roll_vectors.json')) == 52 and 'with writes outside the entity (+0x3C/+0x40) and the RNG word: 0' in text('s3_roll.stdout.txt'))
check('spawn_vectors.json: 183 vectors, 0 mismatches with the table model', len(load('spawn_vectors.json')) == 183 and 'mismatches with the table model: 0' in text('s5_spawn.stdout.txt'))
check('record_vectors.json: 12 vectors, all model_ok', len(load('record_vectors.json')) == 12 and all(v['model_ok'] for v in load('record_vectors.json')))
fz = load('roll_fuzz_vectors.json')['vectors']
check('roll_fuzz_vectors.json: 6000 vectors', len(fz) == 6000)
check('p1_roll: 6011 vectors (11 documented + 6000 fuzz), 0 failures', 'TOTAL vectors 6011, failures 0' in text('p1_roll.stdout.txt'))
check('roll_fuzz_vectors.json md5 087b48f326be28c6eb90763d933d4054 (the critic)', hashlib.md5(open(os.path.join(OUT, 'roll_fuzz_vectors.json'), 'rb').read()).hexdigest() == '087b48f326be28c6eb90763d933d4054')
tab = bytes(x for r in load('loot_table.json')['table'] for x in r)
check('loot table: 2048 bytes, md5 7c6b97ac50de72f436a6fbf08ed6bcd7, equal to the executable bytes at 0x960C', len(tab) == 2048 and hashlib.md5(tab).hexdigest() == '7c6b97ac50de72f436a6fbf08ed6bcd7')
v8 = load('v8.json')
check('v8.json: at least 12 (map, tile) pairs, maps 14, 390, 392, 115, 116 and 167 among them', len(v8['pairs']) >= 12 and {14, 390, 392, 115, 116, 167} <= {p['map'] for p in v8['pairs']})
check('v8.json: the critic pairs reproduce D5F4D64A (14 at (8, 6)) and BBE29AC9 (390 and 392 at (0, 0))', all(r['reproduced'] for r in v8['reproduction']) and [r['final_word'] for r in v8['reproduction']] == ['D5F4D64A', 'BBE29AC9', 'BBE29AC9'])
check('s10_mutants: 12 of 13 caught (the 13th is the argued equivalent)', 'mutants caught: 12 of 13' in text('s10_mutants.stdout.txt'))
if not quick:
    p5 = text('p5_mutants.stdout.txt')
    check('p5_mutants: 168 mutants, 120 caught by either vector set (48 survivors argued in the README)', p5.startswith('mutants 168; caught by the discovery vectors 120; by my vectors 120; by either 120'))
    check('p5_mutants: the sw $zero,0x3f mutant is caught (noise 0xA5A5A5A5)', '80032B00 imm-1' not in p5.split('SURVIVORS')[1])

with open(os.path.join(OUT, 'summary_e0b2%s.txt' % ('_quick' if quick else '')), 'w') as f:
    f.write('\n'.join(lines) + '\n')
manifest = ['choose_vectors.json', 'choose_high_vectors.json', 'roll_vectors.json', 'spawn_vectors.json', 'record_vectors.json', 'veto_vectors.json', 'roll_fuzz_vectors.json', 'loot_table.json', 'v8.json',
            'mutants.json', 's1_table.stdout.txt', 's2_choose.stdout.txt', 's3_roll.stdout.txt', 's5_spawn.stdout.txt', 's6_record.stdout.txt', 's8_veto.stdout.txt', 'p1_roll.stdout.txt', 'v8.stdout.txt',
            's10_mutants.stdout.txt'] + ([] if quick else ['p5_mutants.stdout.txt'])
with open(os.path.join(OUT, 'manifest_e0b2%s.txt' % ('_quick' if quick else '')), 'w') as f:
    for n in manifest:
        f.write('%s  %s\n' % (hashlib.md5(open(os.path.join(OUT, n), 'rb').read()).hexdigest(), n))
print('\n'.join(lines))
print('FAILURES: %d' % fail)
sys.exit(1 if fail else 0)
