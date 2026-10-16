"""run_all.py - runs every scenario module (each on the two interpreters emu.py and mips.py) and writes out/manifest.txt = md5 of every deterministic output file.
usage: python -B -I run_all.py   (about 3 minutes)"""
import os, sys, subprocess, hashlib, glob
HERE = os.path.dirname(os.path.abspath(__file__))
MODS = ['k0_arrows.py', 'k0_arrows2.py', 'k0_crate.py', 'k0_walker.py', 'k0_carry.py', 'k0_drift.py', 'k0_muruta.py', 'k0_header.py', 'k0_masks.py', 'k0_xyfuzz.py', 'k0_magnet.py', 'k0_edge.py']
args = {'k0_xyfuzz.py': ['20000']}
for f in glob.glob(os.path.join(HERE, 'out', '*')):
    b = os.path.basename(f)
    if b.startswith(('arrows', 'crate', 'walker', 'carry', 'drift', 'xyfuzz', 'muruta', 'header', 'masks', 'magnet', 'edge')):
        os.remove(f)
for mod in MODS:
    r = subprocess.run([sys.executable, '-B', '-I', os.path.join(HERE, mod)] + args.get(mod, []), capture_output=True, text=True, cwd=HERE)
    open(os.path.join(HERE, 'out', 'log-' + mod.replace('.py', '.txt')), 'w').write(r.stdout + r.stderr)
    print(mod, 'exit', r.returncode)
    if r.returncode:
        print(r.stderr[-2000:])
        sys.exit(1)
lines = []
for f in sorted(glob.glob(os.path.join(HERE, 'out', '*'))):
    b = os.path.basename(f)
    if b.startswith(('arrows', 'crate', 'walker', 'carry', 'drift', 'xyfuzz', 'muruta', 'header', 'masks', 'magnet', 'edge')):
        lines.append('%s  %s' % (hashlib.md5(open(f, 'rb').read()).hexdigest(), b))
open(os.path.join(HERE, 'out', 'manifest.txt'), 'w').write('\n'.join(lines) + '\n')
print(len(lines), 'files in the manifest')
