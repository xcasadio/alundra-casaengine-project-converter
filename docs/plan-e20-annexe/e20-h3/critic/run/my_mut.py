"""my_mut.py - sensitivity of MY differential test: mutate my models' source (one edit at a time) and count the mismatches against the real code."""
import re, sys, random, importlib, types
import my_diff, my_models
src = open('my_models.py').read()
lines = src.split('\n')
EDITS = [
    (r'== (\d+)', lambda m: '== %d' % (int(m.group(1)) + 1)),
    (r'!= (\d+)', lambda m: '!= %d' % (int(m.group(1)) + 1)),
    (r'< 60', lambda m: '< 64'), (r'< 60', lambda m: '< 56'), (r'& 7\)', lambda m: '& 3)'), (r'>= 3', lambda m: '> 3'), (r'>= 3', lambda m: '>= 2'),
    (r'0xF0', lambda m: '0xEF'), (r'0x30001', lambda m: '0x30002'), (r'0x20001', lambda m: '0x20003'), (r'0x80000', lambda m: '0x40000'),
    (r'0x1001', lambda m: '0x1000'), (r'0xFFFE8000', lambda m: '0xFFFE8001'), (r'0xFFFF0000', lambda m: '0xFFFF0001'),
    (r'\(4, 6, 10\)', lambda m: '(4, 6)'), (r'\(4, 6, 10\)', lambda m: '(4, 10)'), (r'\(4, 6, 10\)', lambda m: '(6, 10)'),
    (r'~0x30', lambda m: '~0x10'), (r'\| 0x40', lambda m: '| 0x20'), (r'\| 0x10\)', lambda m: '| 0x20)'), (r'0x199', lambda m: '0x19A'),
    (r'else 0xB', lambda m: 'else 0xC'), (r'5 if', lambda m: '6 if'), (r'\+ 0x10\) & 0x1F', lambda m: '+ 0x08) & 0x1F'), (r'<= 0\b', lambda m: '< 0'),
    (r'sound\(J, 0x18\)', lambda m: 'sound(J, 0x19)'), (r'sound\(J, 3\)', lambda m: 'sound(J, 2)'), (r'8 if kind == 1 else 12', lambda m: '8 if kind == 1 else 9'),
    (r'M32\)\)', lambda m: '0))'),
]
cnt = 0
surv = []
rnd = random.Random(5)
for li, line in enumerate(lines):
    if line.strip().startswith('#') or line.strip().startswith('"""') or not line.strip():
        continue
    for pat, fn in EDITS:
        mm = re.search(pat, line)
        if not mm:
            continue
        new = line[:mm.start()] + fn(mm) + line[mm.end():]
        if new == line:
            continue
        lines2 = list(lines); lines2[li] = new
        mod = types.ModuleType('my_models')
        try:
            exec(compile('\n'.join(lines2), 'mut', 'exec'), mod.__dict__)
        except Exception as ex:
            continue
        my_diff.MM = mod
        # find which function this line is in
        fn_name = None
        for k in range(li, -1, -1):
            if lines[k].startswith('def '):
                fn_name = lines[k].split('(')[0][4:]; break
        names = {'start': ['start_beans', 'start_bomb'], 'cancel': ['cancel_beans', 'cancel_bomb'], 'dispatch': ['dispatch'], 'driver': ['drv_beans', 'drv_bomb'],
                 'C62': ['C62'], 'D30': ['D30'], 'E12': ['E12'], 'C73': ['C73'], 'E21': ['E21'], 'effect': ['C62'], 'lcg': ['C62']}.get(fn_name)
        if not names:
            continue
        import io, contextlib
        buf = io.StringIO()
        with contextlib.redirect_stdout(buf):
            bad = my_diff.main(500, 77, names)
        cnt += 1
        status = 'killed' if bad else 'SURVIVED'
        if not bad:
            surv.append((li + 1, line.strip(), new.strip()))
        print('%-8s line %3d  %s   ->  %s' % (status, li + 1, line.strip()[:60], new.strip()[:60]))
print('mutants', cnt, 'survivors', len(surv))
for s in surv:
    print('SURV', s)
