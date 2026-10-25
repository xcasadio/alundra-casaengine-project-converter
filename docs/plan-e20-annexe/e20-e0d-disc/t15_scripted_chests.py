import csv, io, sys
sys.path.insert(0, 'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/t2-chest')
import evlib
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
rows = [r for r in csv.DictReader(open('t7_chests.tsv', encoding='utf-8'), delimiter='\t') if r['page'] == 'G' and r['en'] == '1' and r['map'] != '0' and (int(r['F']) & 0x7f)]
for r in rows:
    mid = int(r['map'])
    seen = []
    for slot in 'ACF':
        idx = int(r[slot])
        if not (idx & 0x7f):
            continue
        st = evlib.program_start(mid, slot, idx)
        ops = evlib.decode(mid, st, 200)
        interesting = [(pc, op, ops_) for pc, op, name, ops_ in ops if op in (0x82, 0x8B, 0xB9, 0xA6, 0x0D, 0x50, 0x1A, 0xBD, 0x05, 0x06)]
        seen.append((slot, idx & 0x7f, [(hex(op), o) for pc, op, o in interesting]))
    print('map %s idx %s' % (mid, r['idx']))
    for s in seen:
        print('    slot', s[0], 'prog', s[1], s[2])
