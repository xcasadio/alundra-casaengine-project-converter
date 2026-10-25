"""Which chest flags (persistent bank, the _10 field of the type-30 chests) are read or written by event programs of the maps,
and where the opcode 0x9F (If chest opened) is used. Read-only. Linear sweep of the program bytes, as the discovery's scripts do
(false positives possible on data bytes after an undecodable opcode: the sweep stops there)."""
import csv, io, sys, collections, glob, re
sys.path.insert(0, 'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/t2-chest')
import evlib
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
rows = [r for r in csv.DictReader(open('t7_chests.tsv', encoding='utf-8'), delimiter='\t') if r['page'] == 'G' and r['en'] == '1' and r['map'] != '0']
chestflag = collections.defaultdict(list)
for r in rows:
    if int(r['flag']) and r['flagbank'] == 'P':
        chestflag[int(r['flag'])].append((int(r['map']), int(r['idx']), int(r['contents'])))
FLAGOPS = {0x05: 'FlagOn', 0x06: 'FlagOff', 0x30: 'IfFlagOn', 0x31: 'IfFlagOff', 0x35: 'WaitFlagClear', 0x36: 'WaitFlagSet'}
hits = collections.defaultdict(list)
f9f = []
maps = sorted(int(re.search(r'map_(\d+)\.json', f).group(1)) for f in glob.glob(evlib.P + 'data-extracted/data/map_*.json') if re.search(r'map_(\d+)\.json$', f))
for mid in maps:
    d = evlib.load(mid)
    si = d.get('SpriteInfo')
    if not si or not isinstance(si.get('EventCodes'), dict):
        continue
    codes = si['EventCodes']['Codes']
    starts = evlib.all_starts(mid)
    for i, s in enumerate(starts):
        end = starts[i + 1] if i + 1 < len(starts) else len(codes)
        pc = s
        while pc < end:
            op = codes[pc]
            sz = evlib.SIZES.get(op, (0, ''))[0]
            if sz <= 0:
                break
            if op in FLAGOPS and pc + 2 < len(codes):
                fl = codes[pc + 1] | (codes[pc + 2] << 8)
                if fl in chestflag:
                    hits[fl].append((mid, pc, FLAGOPS[op]))
            if op == 0x9F:
                f9f.append((mid, pc, codes[pc + 1]))
            pc += sz
print('chest flags (persistent) :', len(chestflag))
print('chest flags touched by an event program of ANY map:', len(hits))
for fl in sorted(hits):
    ch = chestflag[fl]
    print('flag %4d chest(s) %s  touched by %s' % (fl, ch, sorted(set((m, o) for m, p, o in hits[fl]))))
print()
print('opcode 0x9F (If chest opened) sites:', len(f9f))
bymap = collections.defaultdict(list)
for m, p, a in f9f:
    bymap[m].append((p, a))
for m in sorted(bymap):
    print('  map', m, bymap[m])
