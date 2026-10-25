"""S8: replay of the E20.c2-0 oracle (copied to ./oracle) on simple hurt scenarios; prints a full per-tick chronology.
Run: python -I s8_replay.py <name> <nticks>     names: A1-pad0 (hero at rest 20 px west of a Beannoid, head-on, ground), A1 (hero walks east into it), A4-E ..., B1-... see hscen.scenarios()
Everything is computed by the emulated real code of ALUN_CD.EXE (France). Nothing is written to the repository."""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, r'C:\Users\casad\AppData\Roaming\Python\Python314\site-packages')   # capstone (user site is disabled by -I)
ORA = os.path.join(HERE, 'oracle')
sys.path.insert(0, ORA)
sys.path.insert(0, os.path.join(ORA, 'tools'))
import c1world as C
import hworld as H
import hscen as HS

name = sys.argv[1] if len(sys.argv) > 1 else 'A1-pad0'
rng = sys.argv[2] if len(sys.argv) > 2 else '0-40'
ranges = [tuple(int(x) for x in part.split('-')) if '-' in part else (int(part), int(part)) for part in rng.split(',')]
nmax = max(b for a, b in ranges)
sp = HS.find(name)
sp = dict(sp)
res = H.run(sp, 'emu', capture=True)
print("# scenario", name, "-", sp['desc'])
print("# columns: tick | anim_target(+0x88) anim_cur(+0x90) dir_target(+0x8C) | pos_x(px) force_x(raw 16.16) on_ground | touching | +0x1CC +0x1D0 | Hp HpMax | stats Hp | +0xAC | visible(draw list) | journal")
rows = [res['pre']] + res['rows']
for i, r in enumerate(rows):
    if i > nmax:
        break
    if not any(a <= i <= b for a, b in ranges):
        continue
    h = r['hero']
    g = r['g']
    j = ' | '.join(r.get('journal', [])) or '-'
    print("%3d | %3X %3X %2d | %9s %8d %d | %s | %3d %2d | %2d/%2d | %2d | %d | %d | %s" % (
        i, h['anim_target'], h['anim_cur'], h['dir_target'], C.px(h['pos_x']), h['force_x'], h['on_ground'],
        '-' if h['touching_slot'] is None else 'slot%d' % h['touching_slot'], h['c1cc'], h['c1d0'], h['hp'], h['hpmax_18'], g['stats_hp'],
        h['anim_end_AC'], h['visible'], j))
