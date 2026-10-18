"""h4_start_mut.py - sensitivity of h4_start.py: each mutation of the MODEL must be caught by the vectors (exit 0 = all caught)."""
import subprocess, sys, os, shutil
src = open('h4_start.py', encoding='utf-8').read()
MUT = [
    ('cost 1 -> 2', 'newmp = mp - 1 ', 'newmp = mp - 2 '),
    ('staff exemption removed', "if st['weapon'] != 7:", "if True:"),
    ('MP == 0 check removed', "if mp == 0:\n                sounds.append(3)\n                return done(1)", "if False:\n                sounds.append(3)\n                return done(1)"),
    ('item bound 0x62 -> 0x63', 'if item >= 0x62:', 'if item >= 0x63:'),
    ('in-use mismatch rule removed', "if inuse != 0 and inuse != item:", "if False:"),
    ('carried check removed', "if st['carried'] == 0 and", "if True and"),
    ('target anim bound 2 -> 3', "(tanim & M32) < 2", "(tanim & M32) < 3"),
    ('in-use check removed in tome start', "and inuse == 0:\n        if st['weapon']", ":\n        if st['weapon']"),
    ('voice byte offset', "st['voice_bytes'][item - 0x2B]", "st['voice_bytes'][item - 0x2C]"),
    ('locked mask: != 0 -> & 0x34', "if st['locked'] != 0:", "if st['locked'] & 0x34:"),
    ('edge bit 0x20 -> 0x10', "(st['edge'] & 0x20) == 0", "(st['edge'] & 0x10) == 0"),
    ('anim after cast 0x32 -> 0x33', "tanim = 0x32", "tanim = 0x33"),
    ('counter/phase not reset', "        counter = 0\n        phase = 0", "        pass"),
]
caught = 0
os.makedirs('mutdir', exist_ok=True)
for f in os.listdir('.'):
    if f.endswith('.py') and not f.startswith('h4_start_mut') and f != 'h4_start.py':
        shutil.copy(f, 'mutdir/' + f)
for name, a, b in MUT:
    if a not in src:
        print('PATTERN NOT FOUND', name); continue
    open('mutdir/h4_start.py', 'w', encoding='utf-8').write(src.replace(a, b, 1))
    r = subprocess.run([sys.executable, '-B', 'h4_start.py', '1500'], cwd='mutdir', capture_output=True, text=True)
    ok = r.returncode != 0
    caught += ok
    line = [l for l in r.stdout.splitlines() if l.startswith('vectors')]
    print(('CAUGHT   ' if ok else 'MISSED   ') + name, '|', line[0] if line else r.stderr[-200:])
print('%d/%d mutations caught' % (caught, len(MUT)))
shutil.rmtree('mutdir', ignore_errors=True)
sys.exit(0 if caught == len(MUT) else 1)
