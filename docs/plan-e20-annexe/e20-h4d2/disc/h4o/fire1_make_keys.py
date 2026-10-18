"""fire1_make_keys.py - h4d2 discovery (read-only): writes the REMADE fire-1 world keys (slot index written, +0x48 = -1, per-frame generator step _pf, bench R-EL-4) as slim JSON, one file per scenario, plus a
manifest (MD5 of every file).  Two runs give the same manifest.  Run from a COPY (python -B).
usage: python -B fire1_make_keys.py OUTDIR
The keys are the BINARY mode (query bound exclusive, sort from 3 results, turn without the shortest way).  The `_qfix` keys are two scenarios on the binary with three instructions of the query patched (inclusive
bound, sort from 2 results); the `_allfix` keys are six scenarios with those three instructions AND the shortest-path turn hook (fire1_lib.install_turn_fix): INFERENCE, they say what the DLL gives under the
production defaults of Q-H4B-2, Q-H4B-3 and Q-H4D2-1 (the corrected modes)."""
import hashlib
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import fire1_lib as L

OUT = sys.argv[1]
os.makedirs(OUT, exist_ok=True)


def patch_query_corrected(m):
    """INFERENCE variant: 0x8003AF98 `slt v0,t3,v1` -> `slt v0,$zero,v1` (scan when count >= 1), 0x8003AFA8 `move t6,v1` -> `addiu t6,v1,1` (bound count inclusive), 0x8003B0B4 `slti v0,s0,3` -> `slti v0,s0,2` (sort from 2)"""
    assert m.peek(0x8003AF98) == 0x0163102A and m.peek(0x8003AFA8) == 0x00607021 and m.peek(0x8003B0B4) == 0x2A020003
    m.poke(0x8003AF98, 0x0003102A)
    m.poke(0x8003AFA8, 0x246E0001)
    m.poke(0x8003B0B4, 0x2A020002)


def patch_all_corrected(m):
    patch_query_corrected(m)
    L.install_turn_fix(m)


ENT_KEEP = ('status', 'id', 'type', 'flags', 'x', 'y', 'z', 'fx', 'fy', 'fz', 'anim_t', 'anim_c', 'dir_t', 'dir_c', 'row', 'animend', 'b274', 'b278_slot', 'b27c', 'c20', 'c1cc', 'c1d0', 'touch', 'slope',
            'adj13c', 'ground', 'hb', 'atk_attr', 'atk_pow')
TGT_KEEP = ('status', 'id', 'type', 'x', 'y', 'z', 'hp', 'c1cc', 'c1d0', 'touch')


def slim(res, name, meta):
    rows = []
    for r in res['rows']:
        ents = {}
        for i, e in r['ents'].items():
            if i == 0:
                continue
            keep = ENT_KEEP if e['type'] in (20, 23) else TGT_KEEP
            ents[str(i)] = {k: e[k] for k in keep}
        h = r['ents'].get(0, {})
        evs = []
        for n, a in r['journal']:
            if n == 'sound' and a == 0:
                continue                                  # snd(0) is the animation sound of sfx 0: noise (c1_make_keys.py drops it too)
            evs.append(L.jfmt(n, a))
        rows.append(dict(t=r['t'], hero=dict(anim_t=r['hero_anim_t'], anim_c=r['hero_anim_c'], x=h.get('x'), y=h.get('y'), z=h.get('z')),
                         item=dict(inuse=r['inuse'], phase=r['phase'], counter=r['counter'], mp=r['mp']), ptr_slot=r['ptr_slot'], ident=r['ident'], count=r['count'],
                         rng0=r['rng0'], rng1=r['rng1'], rng2=r['rng2'], draws=r['draws'], ents=ents, hits=r['hits'], resolved=r['resolved'], events=evs))
    path = os.path.join(OUT, name + '.json')
    json.dump(dict(scenario=name, meta=meta, target_slots=res['targets'], rows=rows), open(path, 'w'), sort_keys=True, separators=(',', ':'), default=str)
    return path


DECOY = (1100, 900)
SC = []
SC.append(('fire1_cast_pf', dict(ticks=200)))
SC.append(('fire1_cast_pf_seedB', dict(ticks=200, seed=0xA5A5A5A5)))
for T in (10, 33, 34, 35, 60, 100, 121, 122, 124, 140):
    SC.append(('fire1_hurt%d_pf' % T, dict(ticks=T + 75, hurt=T)))
SC.append(('fire1_tgt_one_pf', dict(ticks=200, targets=[(140, 100)])))
SC.append(('fire1_tgt_three_pf', dict(ticks=200, targets=[(140, 100), (100, 140), (180, 100)])))
SC.append(('fire1_tgt_two_wrong_order_pf', dict(ticks=200, targets=[(180, 100), (130, 100)])))
SC.append(('fire1_tgt_two_wrong_order_qfix_pf', dict(ticks=200, targets=[(180, 100), (130, 100)], patch=patch_query_corrected)))
SC.append(('fire1_tgt_topslot_pf', dict(ticks=200, targets=[DECOY] * 11 + [(140, 100)], remove={1: list(range(11))})))
SC.append(('fire1_tgt_topslot_qfix_pf', dict(ticks=200, targets=[DECOY] * 11 + [(140, 100)], remove={1: list(range(11))}, patch=patch_query_corrected)))
SC.append(('fire1_tgt_vanish75_pf', dict(ticks=200, targets=[(140, 100)], remove={75: [0]})))
SC.append(('fire1_col_hit_pf', dict(ticks=130, targets=[(108, 100)])))
SC.append(('fire1_wall_col6_pf', dict(ticks=200, cells=L.cells_cols(6, 6, (0, 0, 0x40, 0)))))
SC.append(('fire1_water_cols6_12_pf', dict(ticks=200, cells=L.cells_cols(6, 12, (0, 0, 0, 0x08)))))
for nm, kw in (('one', dict(ticks=200, targets=[(140, 100)])), ('three', dict(ticks=200, targets=[(140, 100), (100, 140), (180, 100)])), ('two_wrong_order', dict(ticks=200, targets=[(180, 100), (130, 100)])),
               ('topslot', dict(ticks=200, targets=[DECOY] * 11 + [(140, 100)], remove={1: list(range(11))})), ('vanish75', dict(ticks=200, targets=[(140, 100)], remove={75: [0]})), ('col_hit', dict(ticks=130, targets=[(108, 100)]))):
    SC.append(('fire1_%s_allfix_pf' % ('tgt_' + nm if nm not in ('col_hit',) else nm), dict(kw, patch=patch_all_corrected)))
SC.append(('fire1_table_full_pf', dict(ticks=80, targets=[DECOY] * 63)))
SC.append(('fire1_table_one_free_pf', dict(ticks=200, targets=[DECOY] * 62)))

man = []
for name, kw in SC:
    kw = dict(kw)
    res = L.run(**kw)
    meta = dict(ticks=kw.get('ticks'), seed=kw.get('seed', 0x12345678), hurt=kw.get('hurt'), targets=kw.get('targets', []), remove={str(k): v for k, v in (kw.get('remove') or {}).items()},
                cells=('custom' if kw.get('cells') else 'flat'), patch=({'patch_query_corrected': 'query corrected (INFERENCE)', 'patch_all_corrected': 'query corrected + shortest-path turn (INFERENCE)'}[kw['patch'].__name__] if kw.get('patch') else None), pf=True, faithful_slot_index=True, uninit_reads=len(res['uninit']))
    path = slim(res, name, meta)
    h = hashlib.md5(open(path, 'rb').read()).hexdigest()
    man.append('%s  %s' % (h, os.path.basename(path)))
    print(name, 'ok', h, 'uninit', len(res['uninit']))
open(os.path.join(OUT, 'manifest.txt'), 'w').write('\n'.join(man) + '\n')
