import collections, json, sys
SM4 = 'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/sm-prep/sm4'
mine = [l.rstrip('\n').split('\t') for l in open('my-skipped-sites.tsv')][1:]
sm4ops = {'0x15', '0x66', '0x72', '0x7A', '0x88', '0x96', '0x97', '0xB3'}
m4 = {(int(r[0]), r[1], int(r[2]), int(r[3]), r[4].upper()) for r in mine if r[4] in sm4ops}
cov = [l.rstrip('\n').split('\t') for l in open(SM4 + '/corpus/sm4-sites-coverage.tsv')][1:]
theirs = {(int(r[1]), r[2], int(r[3]), int(r[4]), '0x' + r[0].upper()) for r in cov}
print('mine', len(m4), 'theirs', len(theirs), 'only mine', sorted(m4 - theirs), 'only theirs', sorted(theirs - m4))
# operands check: decode the instruction bytes at each site with my own loader
sys.path.insert(0, '.')
import census_critic as c
idx = json.load(open(c.PROJ + '/Maps/world-index.json', encoding='utf-8'))
cache = {}
shapes = collections.Counter()
for (mid, slot, prog, pc, op) in sorted(m4):
    if mid not in cache:
        cache[mid] = c.load_map(mid, idx)
    codes = cache[mid][1]['Codes']
    size = c.SIZES[int(op, 16)]
    bs = ' '.join('%02X' % b for b in codes[pc:pc + size])
    shapes[bs] += 1
for k, v in sorted(shapes.items()):
    print(v, k)
# compare with their operand column
bad = 0
for r in cov:
    mid, slot, prog, pc = int(r[1]), r[2], int(r[3]), int(r[4])
    if mid not in cache:
        cache[mid] = c.load_map(mid, idx)
    codes = cache[mid][1]['Codes']
    size = c.SIZES[int(r[0], 16)]
    ops = ' '.join('%02X' % b for b in codes[pc + 1:pc + size])
    if ops.replace(' ', '') != r[5].replace(' ', ''):
        bad += 1
        print('operand mismatch', r, ops)
print('operand mismatches vs their column:', bad)
