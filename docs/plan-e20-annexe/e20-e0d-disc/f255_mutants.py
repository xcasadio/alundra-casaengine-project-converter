import subprocess, sys, re, os
HERE = os.path.dirname(os.path.abspath(__file__))
src = open(os.path.join(HERE, 'f255_oracle.py'), encoding='utf-8').read()
muts = [
 ("it[0xB8] = 0x8000", "it[0xB8] = 0x9000", "rise speed"),
 ("it[0x278] = 0x40", "it[0x278] = 0x41", "delay 64"),
 ("snap['chest'][0x88] = 2", "snap['chest'][0x88] = 1", "chest state 2"),
 ("('etc', 0x48), 0", "('etc', 0x4A), 0", "empty text"),
 ("('etc', 0x49), 0", "('etc', 0x4A), 0", "spawn failure text"),
 ("0x00440000 + 0x100000", "0x00440000 + 0x110000", "Z + 16px"),
 ("itemflags & ~0x180 & M32", "itemflags & ~0x100 & M32", "flag mask"),
 ("if g29 != 0 or g9a != 0:", "if g29 != 0 and g9a != 0:", "fanfare guard"),
 ("if m.rb(ITEMTAB + 8 * contents + 5) != 0:", "if m.rb(ITEMTAB + 8 * contents + 5) > 52:", "byte5 test"),
 ("if contents == 0 or chest88 != 0:", "if contents == 0:", "already-open test"),
 ("(dlg & 4)", "(dlg & 2)", "dialog bit"),
 ("it[0x20] = 0", "it[0x20] = 1", "unblock the item"),
 ("it[0x280] = CHEST", "it[0x280] = 0", "chest pointer"),
 ("j.append(('block', CHEST))", "pass", "block call"),
 ("return 1 if count < m.rb(ITEMTAB + 8 * item + 7) else 0\n    if item == 0x29", "return 1 if count <= m.rb(ITEMTAB + 8 * item + 7) else 0\n    if item == 0x29", "cantake <"),
]
killed = 0
for a, b, name in muts:
    if a not in src:
        print('MISSING', name); continue
    p = os.path.join(HERE, '_mut.py')
    open(p, 'w', encoding='utf-8').write(src.replace(a, b, 1))
    r = subprocess.run([sys.executable, '-I', p, '5', '4000'], capture_output=True, text=True, cwd=HERE)
    line = [l for l in r.stdout.splitlines() if l.startswith('F255 vectors')]
    diffs = int(line[0].split()[-1]) if line else -1
    ok = diffs > 0
    killed += ok
    print('%-22s diffs=%d %s' % (name, diffs, 'KILLED' if ok else 'SURVIVED'))
print('mutants killed', killed, '/', len(muts))
os.remove(os.path.join(HERE, '_mut.py'))
