"""Critic-own: which memory reads hit an UNDEFINED address (neither written by the world nor in the exe image), over the 837 vectors?
An undefined read that can influence the Result would make the vector's expected value depend on garbage."""
import json
import collections
import replay_mips as rp
import gridlib

P = 'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/sm-prep/sm3/vectors/sm3-vectors.jsonl'
BUF = 0x8013D8D4
regions = collections.Counter()
examples = {}
for line in open(P, encoding='utf-8'):
    v = json.loads(line)
    v0, res, mem = rp.run(v, gridlib.make_grid)
    for a in mem.unmapped:
        if rp.SP - 0x1000 <= a < rp.SP:
            key = 'stack'
        elif BUF <= a < BUF + 4 * 80:
            key = 'search-buffer'
        elif rp.ENT0 <= a < rp.ENT0 + 70 * rp.STRIDE:
            key = 'entity-table slot %d off 0x%X' % ((a - rp.ENT0) // rp.STRIDE, (a - rp.ENT0) % rp.STRIDE)
        else:
            key = 'other 0x%08X' % a
        regions[(v['op'], key.split(' off')[0] if key.startswith('entity') else key)] += 1
        examples.setdefault((v['op'], key), v['id'])
for k, n in sorted(regions.items()):
    print(k, n)
print('--- examples of non stack/buffer')
for (op, key), vid in examples.items():
    if key not in ('stack', 'search-buffer'):
        print(op, key, vid)
