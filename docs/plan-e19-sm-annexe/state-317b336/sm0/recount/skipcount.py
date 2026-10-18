"""Read-only recount at HEAD: skipped-by-size reached sites, from the HEAD Dispatch cases and the census of HEAD data."""
import collections
import csv
import re

REPO = 'D:/development/repo/alundra-casaengine-project-converter'
S = 'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/sm-state/sm0/recount'

runner = open(REPO + '/Alundra/Events/EventProgramRunner.cs', encoding='utf-8').read().split('\n')
start = next(i for i, l in enumerate(runner) if 'private int Dispatch(' in l)
end = next(i for i in range(start, len(runner)) if runner[i].startswith('    }'))
print('Dispatch lines', start + 1, end + 1)
cases = set()
for l in runner[start:end + 1]:
    m = re.match(r'^            case 0x([0-9A-Fa-f]{2}):', l)
    if m:
        cases.add(int(m.group(1), 16))
print('top-level cases', len(cases))

src = open(REPO + '/Alundra/Events/EventOpcodeSizeTable.cs', encoding='utf-8').read()
sizes = {int(m.group(1), 16): int(m.group(2)) for m in re.finditer(r'\{\s*0x([0-9A-Fa-f]{2}),\s*new\((\d+),', src)}
print('table entries', len(sizes))

ported = cases | {0x00, 0xFF}
skipped_by_size = sorted(op for op, sz in sizes.items() if sz > 0 and op not in ported)
size0 = sorted(op for op, sz in sizes.items() if sz == 0)
notable = sorted(op for op in range(256) if op not in sizes)
print('skipped by size', len(skipped_by_size), ' '.join('%02X' % o for o in skipped_by_size))
print('size 0', len(size0), ' '.join('%02X' % o for o in size0))
print('not in table', len(notable))

rows = list(csv.DictReader(open(S + '/census-sites.tsv', encoding='utf-8'), delimiter='\t'))
print('census rows', len(rows))
sk = [r for r in rows if int(r['opcode'], 16) in skipped_by_size]
print('reached skipped sites', len(sk), 'maps', len({r['map'] for r in sk}))
print(sorted(collections.Counter(r['opcode'] for r in sk).items(), key=lambda x: -x[1]))
z = [r for r in rows if int(r['opcode'], 16) in size0 or int(r['opcode'], 16) in notable]
print('reached size-0 or unknown-size sites', len(z))

old = list(csv.DictReader(open(REPO + '/docs/plan-e19-sm-annexe/skipped-sites.tsv', encoding='utf-8'), delimiter='\t'))
oldkeys = {(r['map'], r['slot'], r['program'], r['pc'], r['opcode'].upper()) for r in old}
newkeys = {(r['map'], r['slot'], r['program'], r['pc'], r['opcode'].upper()) for r in sk}
print('annex 510 == recount at HEAD:', oldkeys == newkeys, len(oldkeys), len(newkeys), 'only-annex', len(oldkeys - newkeys), 'only-head', len(newkeys - oldkeys))

# Chain maps / central maps
chain = [389, 390, 476, 478, 392, 391, 416, 163, 162, 165, 164, 172, 169, 170, 171, 173, 174, 175, 10, 179, 176, 177, 178, 180, 181, 182, 135, 183, 184, 185]
combat = [14, 15, 44, 115, 116, 117, 362]
print('skipped on chain maps', sum(1 for r in sk if int(r['map']) in chain), ' on combat maps', collections.Counter(r['opcode'] for r in sk if int(r['map']) in combat))
# Result consumers
tsv = {(r['map'], r['slot'], r['program'], r['pc']): r for r in old}
print('annex rows with Result consumer next:', sum(1 for r in old if r['next_is_Result_consumer'] == 'Y'))
print('annex distinct (map,slot,pc) vs key (map,slot,program,pc):', len({(r['map'], r['slot'], r['pc']) for r in old}), len(tsv))
# two programs at one pc?
cnt = collections.Counter((r['map'], r['slot'], r['pc']) for r in old)
print('pcs shared by several programs:', sum(1 for v in cnt.values() if v > 1))
print('maps by slot count', collections.Counter(r['slot'] for r in old))
