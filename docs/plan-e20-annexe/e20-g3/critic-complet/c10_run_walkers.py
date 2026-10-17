#!/usr/bin/env python3
"""critic-complet c10: runs the DLL discovery's walkers.py (copied unchanged in ./dllcopy) over every map with liftables, stdout in utf-8,
then keeps the record walkers whose summed walk radius + 24 px reaches a liftable footprint (the static bound of walkers.py; INFERENCE)."""
import io, json, os, re, runpy, sys
HERE = os.path.dirname(os.path.abspath(__file__))
os.chdir(os.path.join(HERE, 'dllcopy'))
sys.path.insert(0, '.')
R = json.load(open('all_records.json', encoding='utf-8'))
maps = sorted({r['map'] for r in R if r['kind']})
buf = io.StringIO()
old = sys.stdout
sys.stdout = buf
sys.argv = ['walkers.py'] + [str(m) for m in maps]
runpy.run_path('walkers.py', run_name='__main__')
sys.stdout = old
sys.stdout.reconfigure(encoding='utf-8')
txt = buf.getvalue()
open(os.path.join(HERE, 'c10_walkers_all.out'), 'w', encoding='utf-8').write(txt)
rows = []
mp = None
for line in txt.splitlines():
    m = re.match(r'== map (\d+)', line)
    if m:
        mp = int(m.group(1)); continue
    m = re.search(r'rec\s*(\d+) (.*?) at \((\d+),(\d+),(\d+)\) (C\[\d+\]) walks=(.*) sumR=(\d+) nearestLiftableGap=(\d+)', line)
    if m:
        sumr, gap = int(m.group(8)), int(m.group(9))
        if gap < sumr + 24:
            rows.append((mp, int(m.group(1)), m.group(2).strip(), (int(m.group(3)), int(m.group(4)), int(m.group(5))), m.group(6), sumr, gap))
print('maps scanned: %d ; record walkers within reach of a liftable (sumR + 24 > gap): %d' % (len(maps), len(rows)))
for r in rows:
    print('  map %d rec%d %-26s at %s %s sumR=%d gap=%d' % r)
