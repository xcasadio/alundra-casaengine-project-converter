"""run_all.py - run every survey script (python -B -I), then write out/manifest.txt = sha1 of every out/*.txt.  Two runs must give an identical manifest (the machine is deterministic).
Usage: python -B -I run_all.py [manifest_name]"""
import sys, os, subprocess, hashlib, glob
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, 'out')
SCRIPTS = ['g1_lift.py', 'g2_jump.py', 'g3_step.py', 'g4_throw.py', 'g4b_throw.py', 'g5_platform.py', 'g6_thrown_on_entity.py', 'g7_hit.py', 'g8_events.py', 'g9_lift_rules.py',
           'g10_upce_fuzz.py', 'g11_stack.py', 'g12_dirs.py', 'g13_onto_crate.py', 'g14_variants.py', 'g15_misc.py', 'g16_noaction.py', 'g17_ladder.py', 'g18_c4_stone.py']
KEEP = ('g', )          # outputs of the g* scripts only (the earlier smoke/dis files are not part of the manifest)

if __name__ == '__main__':
    name = sys.argv[1] if len(sys.argv) > 1 else 'manifest.txt'
    for s in SCRIPTS:
        r = subprocess.run([sys.executable, '-B', '-I', os.path.join(HERE, s)], capture_output=True, text=True, cwd=HERE)
        if r.returncode:
            print('FAILED', s, r.stderr[-600:])
            sys.exit(1)
        print('ok', s)
    lines = []
    for f in sorted(glob.glob(os.path.join(OUT, 'g*.txt'))):
        b = os.path.basename(f)
        if b.startswith('manifest'):
            continue
        lines.append('%s  %s' % (hashlib.sha1(open(f, 'rb').read()).hexdigest(), b))
    open(os.path.join(OUT, name), 'w').write('\n'.join(lines) + '\n')
    print(len(lines), 'files in', name)
