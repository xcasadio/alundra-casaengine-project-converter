"""critic h4d2: PROPOSED additional keys (relief) on the remade machine (slot index, +0x48=-1, per-frame step), run on MY OWN interpreter (emu.py = myemu.py).
usage: python -B make_relief_keys.py OUTDIR"""
import hashlib, json, os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import fire1_lib as L
OUT = sys.argv[1]; os.makedirs(OUT, exist_ok=True)
ENT_KEEP = ('status', 'id', 'type', 'flags', 'x', 'y', 'z', 'fx', 'fy', 'fz', 'anim_t', 'anim_c', 'dir_t', 'dir_c', 'row', 'animend', 'b274', 'b278_slot', 'b27c', 'c20', 'c1cc', 'c1d0', 'touch', 'slope', 'adj13c', 'ground', 'hb', 'atk_attr', 'atk_pow')
def slim(res, name, meta):
    rows = []
    for r in res['rows']:
        ents = {str(i): {k: e[k] for k in ENT_KEEP} for i, e in r['ents'].items() if i != 0 and e['type'] in (20, 23)}
        h = r['ents'].get(0, {})
        evs = [L.jfmt(n, a) for n, a in r['journal'] if not (n == 'sound' and a == 0)]
        rows.append(dict(t=r['t'], hero=dict(anim_t=r['hero_anim_t'], anim_c=r['hero_anim_c'], x=h.get('x'), y=h.get('y'), z=h.get('z')), item=dict(inuse=r['inuse'], phase=r['phase'], counter=r['counter'], mp=r['mp']),
                         rng0=r['rng0'], rng1=r['rng1'], rng2=r['rng2'], draws=r['draws'], ents=ents, hits=r['hits'], events=evs))
    path = os.path.join(OUT, name + '.json')
    json.dump(dict(scenario=name, meta=meta, rows=rows), open(path, 'w'), sort_keys=True, separators=(',', ':'), default=str)
    return path
def hero_z(z):
    def hook(m, t):
        if t == 1:
            m.poke(L.ent(0) + 0x11C, z)
    return hook
SC = [
 ('fire1_relief_plateau_hb1_cols6_12_pf', dict(ticks=200, cells=L.cells_cols(6, 12, (1, 0, 0, 0)))),
 ('fire1_relief_plateau_hb2_cols6_12_pf', dict(ticks=200, cells=L.cells_cols(6, 12, (2, 0, 0, 0)))),
 ('fire1_relief_hero_on_plateau_hb2_pf', dict(ticks=200, cells=(lambda c, r: (2, 0, 0, 0) if (c <= 5 and 2 <= r <= 6) else (0, 0, 0, 0)), hook=hero_z((32 << 16) + 1))),
]
man = []
for name, kw in SC:
    res = L.run(**kw)
    path = slim(res, name, dict(ticks=kw.get('ticks'), note='critic h4d2 proposed relief key', uninit_reads=len(res['uninit'])))
    h = hashlib.md5(open(path, 'rb').read()).hexdigest()
    man.append('%s  %s' % (h, os.path.basename(path)))
    print(name, h, 'uninit', len(res['uninit']), flush=True)
open(os.path.join(OUT, 'manifest.txt'), 'w').write('\n'.join(man) + '\n')
