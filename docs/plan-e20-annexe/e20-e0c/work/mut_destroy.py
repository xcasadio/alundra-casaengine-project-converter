import os, shutil, subprocess, sys, re, tempfile
HERE = os.path.dirname(os.path.abspath(__file__))
SRC = open(os.path.join(HERE, 't_destroy.py')).read()
MUT = [
 ("if 1 <= ents[e][0x10] <= 3 and C[0x0C] == eaddr(e):", "if 1 <= C[0x10] <= 3 and C[0x0C] == eaddr(e):"),
 ("for i in range(0, count + 1):", "for i in range(0, count):"),
 ("for i in range(0, count + 1):", "for i in range(1, count + 1):"),
 ("if 1 <= ents[e][0x10] <= 3", "if 1 <= ents[e][0x10] <= 4"),
 ("if 1 <= ents[e][0x10] <= 3", "if 2 <= ents[e][0x10] <= 3"),
 ("if s32(a1) == -1: a1 = hdr[e]", "if s32(a1) == -1 and False: a1 = hdr[e]"),
 ("if a1 != 0: journal.append(('effect', 0, a1 & M32, 0, eaddr(e), 1, 0, 0, 0))", "if a1 > 0: journal.append(('effect', 0, a1 & M32, 0, eaddr(e), 1, 0, 0, 0))"),
 ("    model_destroy(ents, e, a1, mpmax, journal, hdr)\ndef model_rec", "    pass\ndef model_rec"),
 ("if item != 0 and not M.veto(item, mpmax):", "if item != 0:"),
 ("    E[0x10] = 4\n", "    E[0x10] = 3\n"),
 ("    model_destroy(ents, e, a1, mpmax, journal, hdr)\nbad = 0", "    model_destroy(ents, e, a1, mpmax, journal, hdr)\nbad = 0"),
]
killed = 0
for old, new in MUT:
    if old not in SRC: print('NOT FOUND', old); continue
    tmp = tempfile.mkdtemp(prefix='mutd_', dir=os.path.join(HERE, 'cache'))
    for f in os.listdir(HERE):
        p = os.path.join(HERE, f)
        if os.path.isfile(p) and f.endswith('.py'): shutil.copy(p, tmp)
    shutil.copytree(os.path.join(HERE, 'emu'), os.path.join(tmp, 'emu'))
    open(os.path.join(tmp, 't_destroy.py'), 'w').write(SRC.replace(old, new, 1))
    out = subprocess.run([sys.executable, '-B', 't_destroy.py', '5'], cwd=tmp, capture_output=True, text=True, timeout=900)
    mm = re.findall(r'mismatches (\d+)', out.stdout)
    dead = (not mm) or int(mm[0]) > 0
    print('KILLED  ' if dead else 'SURVIVOR', old[:70].replace('\n', ' '), '->', new[:50].replace('\n', ' '), mm, out.stderr[-100:].replace('\n', ' ') if not mm else '')
    killed += dead
    shutil.rmtree(tmp, ignore_errors=True)
print('mutants', len(MUT), 'killed', killed)
