"""Critic: MY model of the DLL (mymodel.py, written from the C# text and rules R1..R5) against the preparation's FROZEN vectors,
in the 'prod' and 'ctx' montages, without any mutation.  Also checks the flag dll_hero_outside_list_same and the owner=hero tests."""
import sys, json, collections
sys.path.insert(0, 'work')
import mymodel as M

VEC = sys.argv[1]
vecs = []
for line in open(VEC, encoding='utf-8'):
    line = line.strip().rstrip(',')
    if line.startswith('{"anon"'):
        vecs.append(json.loads(line))


def s32(x):
    x &= 0xFFFFFFFF
    return x - 2**32 if x & 0x80000000 else x


def world(v):
    proxies = []
    for i, s in enumerate(v['slots']):
        ctx = None if i in v['anon'] else 'auto'
        proxies.append(M.P(i, s['status'], s['ref'], s['flags'], s32(s['anim']), [s32(t) for t in s['tile']], blocked=s['blocked'], ctx=ctx, is_player=(i == 0)))
    fp = M.P(99, 2, 77, 0, 0, [0, 0, 0], ctx='F')
    for i, s in enumerate(v['slots']):
        p = proxies[i]

        def idty(x):
            if x is None:
                return None
            if x == 'foreign':
                return 'FOREIGN'
            return proxies[x].ctx
        p.riding = idty(s['riding'])
        p.parent = idty(s['parent'])
        p.platform = idty(s['platform'])
        x = s['xcoll']
        p.xcoll = None if x is None else (fp if x == 'foreign' else proxies[x])
    return proxies


bad = []
flag_bad = []
n = 0
for v in vecs:
    px = world(v)
    ent = px[v['logic']]
    res = {}
    for mont in ('prod', 'ctx'):
        spawned = px if mont == 'prod' else px[1:]
        res[mont] = M.run(v['op'], ent, v['code'], spawned, px[0])
    n += 1
    if res['prod'] != v['binary']['result']:
        bad.append((v['id'], 'prod', res['prod'], v['binary']['result']))
    same = res['prod'] == res['ctx']
    if same != v['dll_hero_outside_list_same']:
        flag_bad.append((v['id'], same, v['dll_hero_outside_list_same']))
    if v['op'] in (0x3C, 0x3D) and not same:
        bad.append((v['id'], 'ctx-differs-for-zone', res))
print('vectors', n, 'prod mismatches/zone ctx diffs', bad, 'flag mismatches', flag_bad)
ctxdiff = [v['id'] for v in vecs if M.run(v['op'], world(v)[v['logic']], v['code'], world(v)[1:], world(v)[0]) != v['binary']['result']]
print('vectors whose result differs under ctx montage:', len(ctxdiff), ctxdiff)
