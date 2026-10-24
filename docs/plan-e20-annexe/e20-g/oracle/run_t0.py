"""run_t0.py - T0 of E20.g (added): run the survey scripts g1..g18, the critic's c1, c2, c3, then c4_climb_lift and c5_status4 (each through run_json.py: text and JSON outputs in out/),
then write out/<manifest name> = sha1 of every out/*.txt and out/*.json (the machine is deterministic: two runs give an identical manifest).
Usage: python -B -I run_t0.py [manifest_name]  (rerun from a copy of this folder, never in place)"""
import sys, os, subprocess, hashlib, glob
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, 'out')
SCRIPTS = ['g1_lift.py', 'g2_jump.py', 'g3_step.py', 'g4_throw.py', 'g4b_throw.py', 'g5_platform.py', 'g6_thrown_on_entity.py', 'g7_hit.py', 'g8_events.py', 'g9_lift_rules.py',
           'g10_upce_fuzz.py', 'g11_stack.py', 'g12_dirs.py', 'g13_onto_crate.py', 'g14_variants.py', 'g15_misc.py', 'g16_noaction.py', 'g17_ladder.py', 'g18_c4_stone.py',
           'c1_equality.py', 'c2_platform.py', 'c3_contact.py', 'c4_climb_lift.py', 'c5_status4.py']
if __name__ == '__main__':
    name = sys.argv[1] if len(sys.argv) > 1 else 'manifest.txt'
    os.makedirs(OUT, exist_ok=True)
    for s in SCRIPTS:
        r = subprocess.run([sys.executable, '-B', '-I', os.path.join(HERE, 'run_json.py'), s], capture_output=True, text=True, cwd=HERE)
        if r.returncode:
            print('FAILED', s, r.stderr[-600:])
            sys.exit(1)
        print('ok', s)
    lines = []
    for f in sorted(glob.glob(os.path.join(OUT, '*.txt')) + glob.glob(os.path.join(OUT, '*.json'))):
        b = os.path.basename(f)
        if b.startswith('manifest'):
            continue
        lines.append('%s  %s' % (hashlib.sha1(open(f, 'rb').read()).hexdigest(), b))
    open(os.path.join(OUT, name), 'w', newline='\n').write('\n'.join(lines) + '\n')
    print(len(lines), 'files in', name)
