import csv, collections
TSV = 'D:/development/repo/alundra-casaengine-project-converter/docs/plan-e19-sm-annexe/skipped-sites.tsv'
rows = list(csv.DictReader(open(TSV, encoding='utf-8'), delimiter='\t'))
SM = {'sm1': {0x3C,0x3D,0x60,0x61}, 'sm2': {0x6A,0x6B,0x6C,0x6D}, 'sm3': {0xA9,0xAA,0xB2,0xB4,0xB5,0xB6,0xB7,0x9E},
      'sm4': {0x72,0x88,0x96,0x97,0xB3,0x15,0x66,0x7A}, 'sm5': {0x56}, 'sm7': {0xB9,0xBA}, 'sm8': {0xBB,0xC2}}
owner_of = {}
for k, ops in SM.items():
    for o in ops: owner_of[o] = k
def stats(rs):
    keys = {(r['map'], r['pc']) for r in rs}
    return dict(lines=len(rs), instr=len({(r['map'], r['pc']) for r in rs}), maps=len({r['map'] for r in rs}), opcodes=len({r['opcode'] for r in rs}))
print('all:', stats(rows))
per = collections.Counter(owner_of[int(r['opcode'],16)] for r in rows)
print('per owner:', dict(sorted(per.items())))
rest = [r for r in rows if owner_of[int(r['opcode'],16)] != 'sm2']
print('after sm2 removal:', stats(rest))
sm2 = [r for r in rows if owner_of[int(r['opcode'],16)] == 'sm2']
print('sm2 only:', stats(sm2), ' duplicates (map,pc) inside sm2:', len(sm2) - len({(r['map'], r['pc']) for r in sm2}))
# (map, slot, program, pc) uniqueness
print('distinct (map,slot,program,pc) in sm2:', len({(r['map'], r['slot'], r['program'], r['pc']) for r in sm2}))
gone = {r['map'] for r in rows} - {r['map'] for r in rest}
print('maps that leave the list entirely with sm2:', sorted(map(int, gone)))
# per map in sm2: remaining sites of other owners
rem = collections.defaultdict(list)
for r in rest:
    if r['map'] in {x['map'] for x in sm2}:
        rem[r['map']].append(r['opcode'])
print('sm2 maps that keep other-owner lines:', {k: dict(collections.Counter(v)) for k, v in rem.items()})
