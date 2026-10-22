"""For each program holding a 96 80 / 97 / 88 / 72 / B3 site: every reachable 0x42 / 0x43 / 0x40 of the program (walker of census_critic)."""
import json, sys
sys.path.insert(0, '.')
import census_critic as c

idx = json.load(open(c.PROJ + '/Maps/world-index.json', encoding='utf-8'))
rows = [l.rstrip('\n').split('\t') for l in open('my-skipped-sites.tsv')][1:]
sm4 = {'0x15', '0x66', '0x72', '0x7A', '0x88', '0x96', '0x97', '0xB3'}
cache = {}
done = set()
for r in rows:
    if r[4] not in sm4:
        continue
    mid, slot, prog = int(r[0]), r[1], int(r[2])
    if (mid, slot, prog) in done:
        continue
    done.add((mid, slot, prog))
    if mid not in cache:
        cache[mid] = c.load_map(mid, idx)
    ev = cache[mid][1]
    codes = ev['Codes']
    entry = c.tables(ev)[c.SLOTS.index(slot)][prog]
    extra = set()
    seen = c.walk_program(codes, entry, extra)
    ret = sorted(p for p, o in seen.items() if o in (0x42, 0x43))
    n40 = sorted(p for p, o in seen.items() if o == 0x40)
    info = []
    if ret:
        info.append('retarget ' + ','.join('%d:%02X' % (p, seen[p]) for p in ret))
    if n40:
        info.append('0x40@' + ','.join(map(str, n40)))
    if info and not (mid in range(452, 471)):
        print(mid, slot, prog, ' | '.join(info))
print('programs scanned', len(done))
