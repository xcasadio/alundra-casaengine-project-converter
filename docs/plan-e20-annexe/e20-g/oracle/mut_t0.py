"""mut_t0.py - T0 of E20.g (added): the 19 named one-word mutants of the plan (section 1.24.0), each applied to the REAL code in the oracle machine; a mutant is KILLED when the outputs of a
scenario differ from the baseline out/ (or the scenario crashes).  Stage 1 runs the quick scenarios, stage 2 (only for the survivors of stage 1) all 23.
Usage: python -B -I mut_t0.py   (needs out/ of run_t0.py next to it; writes mut/<n>/ and mut_result.txt)"""
import sys, os, subprocess, filecmp, glob
from concurrent.futures import ThreadPoolExecutor
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import run_t0

# (id, address, new word, what the plan names, the original word)
MUTANTS = [
    ('M01', 0x8002EDD4, 0x30420400, 'genre test: andi 0x600 -> 0x400', 0x30420600),
    ('M02', 0x8002EDE0, 0x14620009, 'genre test: beq (genre == 1) -> bne', 0x10620009),
    ('M03', 0x8002EDFC, 0x2404003A, 'GetNumberOfItem(0x3B) -> 0x3A', 0x2404003B),
    ('M04', 0x8002EE00, 0x14400024, 'branch of GetNumberOfItem: beqz -> bnez', 0x10400024),
    ('M05', 0x8002EF20, 0x30420040, 'Square mask 0x80 -> 0x40', 0x30420080),
    ('M06', 0x8002EF70, 0x2C420003, 'status test of the loop: sltiu 2 -> 3', 0x2C420002),
    ('M07', 0x8002EF94, 0x10540005, '+0x12C == hero: bne -> beq', 0x14540005),
    ('M08', 0x8002F98C, 0x3C060002, 'step 0x10000 of mode 0 (Z axis) -> 0x20000', 0x3C060001),
    ('M09', 0x8002FA68, 0x3C060001, 'step 0x20000 of mode 1 (X axis) -> 0x10000', 0x3C060002),
    ('M10', 0x8002F9E4, 0x34C6FFFE, 'threshold 0x1FFFF -> 0x1FFFE', 0x34C6FFFF),
    ('M11', 0x8002F9CC, 0x28620004, 'counter 5 -> 4', 0x28620005),
    ('M12', 0x8002F94C, 0x0083102A, 'slt of the equality of mode 0: operands swapped', 0x0064102A),
    ('M13', 0x8002F984, 0x3C050021, 'target 0x200000 -> 0x210000', 0x3C050020),
    ('M14', 0x8007ED68, 0x2403FFEF, 'E2 mask ~0x30 -> ~0x10', 0x2403FFCF),
    ('M15', 0x8006B8A8, 0x2403FFBF, 'C23 mask ~0x80 -> ~0x40', 0x2403FF7F),
    ('M16', 0x8006B8B0, 0x34420020, 'C23 |0x30 -> |0x20', 0x34420030),
    ('M17', 0x8006B878, 0x24427404, 'table 0x80027400 -> 0x80027404', 0x24427400),
    ('M18', 0x80032370, 0x30421000, 'guard 0x2000 -> 0x1000', 0x30422000),
    ('M19', 0x80032458, 0x30428000, 'guard 0x4000 -> 0x8000', 0x30424000),
    ('M20', 0x8003237C, 0x8E02008C, '+0x98 read as +0x8C (throw table index)', 0x8E020098),
]
STAGE1 = ['c1_equality.py', 'c3_contact.py', 'g1_lift.py', 'g9_lift_rules.py', 'g4_throw.py', 'g5_platform.py', 'c4_climb_lift.py', 'g17_ladder.py', 'g12_dirs.py', 'g2_jump.py', 'c5_status4.py']


def txts(script):
    """names of the text outputs a scenario writes in the baseline: found by the script's own stem list, so we just compare every baseline file the run rewrote"""
    return None


def run_one(args):
    mid, script, addr, word = args
    out = os.path.join(HERE, 'mut', mid, os.path.splitext(script)[0])
    os.makedirs(out, exist_ok=True)
    r = subprocess.run([sys.executable, '-B', '-I', os.path.join(HERE, 'run_mut.py'), out, script, '%x:%x' % (addr, word)], capture_output=True, text=True, cwd=HERE)
    if r.returncode:
        return mid, script, 'CRASH ' + r.stderr.strip().splitlines()[-1][:120] if r.stderr.strip() else 'CRASH'
    diff = []
    for f in sorted(glob.glob(os.path.join(out, '*.txt'))):
        b = os.path.join(HERE, 'out', os.path.basename(f))
        if not os.path.exists(b) or not filecmp.cmp(f, b, shallow=False):
            diff.append(os.path.basename(f))
    return mid, script, ('DIFF ' + ','.join(diff[:3])) if diff else 'same'


def sanity():
    """the unmutated word at each address must be the word of the binary (the mutants are one-word edits of the real code)"""
    import exelib
    for mid, addr, w, what, orig in MUTANTS:
        assert exelib.u32(addr) == orig, (mid, hex(addr), hex(exelib.u32(addr)), hex(orig))
        assert w != orig


if __name__ == '__main__':
    sanity()
    res = {}
    with ThreadPoolExecutor(6) as ex:
        jobs = [(mid, s, a, w) for mid, a, w, _, _ in MUTANTS for s in STAGE1]
        for mid, s, r in ex.map(run_one, jobs):
            res.setdefault(mid, {})[s] = r
        surv = [m for m in MUTANTS if not any(v != 'same' for v in res[m[0]].values())]
        print('stage 1 survivors:', [m[0] for m in surv], flush=True)
        rest = [s for s in run_t0.SCRIPTS if s not in STAGE1]
        jobs = [(m[0], s, m[1], m[2]) for m in surv for s in rest]
        for mid, s, r in ex.map(run_one, jobs):
            res[mid][s] = r
    lines = []
    for mid, a, w, what, orig in MUTANTS:
        kills = [(s, v) for s, v in res[mid].items() if v != 'same']
        lines.append('%s %08x %08x->%08x %-52s %s' % (mid, a, orig, w, what, ('KILLED by %d scenario(s), first %s: %s' % (len(kills), kills[0][0], kills[0][1])) if kills else 'SURVIVES (all %d scenarios identical)' % len(res[mid])))
    open(os.path.join(HERE, 'mut_result.txt'), 'w').write('\n'.join(lines) + '\n')
    print('\n'.join(lines))
