"""e14check.py - model-vs-oracle check over recorded handler calls (scenario runs and vector sweeps).  The model is e14model.py (written from the annexes); the oracle is the emulated binary."""
import os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, 'tools'))
import e14model as M
import e14world as E
import kmodel as KM


def check_call(c):
    """-> list of problems (empty = the model predicts the call exactly)"""
    probs = []
    h = c['handler']
    before, after = c['ent_before'], c['ent_after']
    hp_after = E.decode(after)['hp']
    if h in ('D1', 'D9'):
        atk = c['attacker_atk']
        if atk is None or c['sheet'] is None:
            probs.append('no attacker attack recorded')
            return probs
        sheet = c['sheet']
        eq = [None if not q.get('ptr') else q['attacks'] for q in c['equip']]
        hp_b = E.decode(before)['hp']
        exp_hp, dmg, how = KM.damage_model(atk[0], atk[1], lambda n: sheet[n + 2], hp_b, eq, c['latch'])
        if exp_hp != hp_after:
            probs.append('ComputeNewHp: model hp %d (%s) real %d' % (exp_hp, how, hp_after))
    if h == 'E1':
        return probs
    img, rng_s, snd, nd = M.predict(h, before, c['hero_before'], c['hero_atk'], c['rng_in'], hp_after)
    # the HP word is ComputeNewHp's: take the real one into the predicted image for D handlers (checked above)
    if h in ('D1', 'D9'):
        b = bytearray(img)
        b[0x14:0x18] = after[0x14:0x18]
        img = bytes(b)
    if img != after:
        diff = [(o, int.from_bytes(img[o:o + 4], 'little'), int.from_bytes(after[o:o + 4], 'little')) for o in range(0, E.STRIDE, 4) if img[o:o + 4] != after[o:o + 4]]
        probs.append('image differs at ' + ', '.join('+0x%X model %08X real %08X' % d for d in diff[:6]))
    if rng_s != c['rng_out']:
        probs.append('RNG: model %08x (%d draws) real %08x' % (rng_s, nd, c['rng_out']))
    real_snd = sorted(a if not isinstance(a, list) else a[0] for k, a in c['journal'] if k == 'sound')
    if sorted(snd) != real_snd:
        probs.append('sounds: model %s real %s' % (sorted(snd), real_snd))
    return probs


def check_calls(calls, label=''):
    bad = []
    per = {}
    for c in calls:
        p = check_call(c)
        per.setdefault(c['handler'], [0, 0])
        per[c['handler']][0] += 1
        if p:
            per[c['handler']][1] += 1
            bad.append((c, p))
    return per, bad
