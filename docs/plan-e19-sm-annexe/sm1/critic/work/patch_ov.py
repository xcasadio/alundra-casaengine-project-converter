s = open('work/mutants_eval3.py', encoding='utf-8').read()
marker = "rows = [l.rstrip(" + chr(39) + chr(92) + "n" + chr(39) + ").split("
i = s.index(marker)
j = s.index("\n", i)
add = """
byop = {0x60: [], 0x61: [], 0x3C: [], 0x3D: []}
for vid_, v_ in vecs.items():
    byop[v_['op']].append(vid_)
for name_, op_, mont_ in (('OV-01', 0x60, 'prod'), ('OV-02', 0x61, 'prod'), ('OV-03', 0x3C, 'prod'), ('OV-04', 0x3D, 'prod'), ('OV-05a', 0x3C, 'prod+ctx'), ('OV-05b', 0x3D, 'prod+ctx')):
    rows.append([name_, 'Oracle', 'replay', hex(op_), mont_, ' '.join(byop[op_]), '', '', ''])
"""
s = s[:j + 1] + add + s[j + 1:]
open('work/mutants_eval4.py', 'w', encoding='utf-8').write(s)
