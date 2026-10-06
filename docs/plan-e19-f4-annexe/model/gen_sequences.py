"""E19.f4b: versioned generator of the random RAW sequences of the name box and the portrait (port of scenario_raw of the binary-validation script validate.py,
which used Python's Mersenne Twister and the binary rig; here a self-contained integer PRNG, SplitMix64, that a C# test can port in six lines, and no rig).

What it pins: the C# oracle of the test project (a port of f4_model.py, never DLL code) is fed the SAME inputs from the same seeds and must reproduce the digests
below; the DLL machines are then compared to that oracle frame by frame.  Chain of trust:
    ALUN_CD.EXE =(validate.py, 300 raw + dialogue cases, 0 diffs; binary-verify.md claim 28, v7_diff.py)= f4_model.py
    f4_model.py =(this digest file)= C# oracle =(T1-random, frame by frame)= DLL

Inputs per seed (raw mode), in this exact order of PRNG draws (the C# port must follow it):
    pool        4 entities: id = choice(IDS), flags = choice([0, P, P, P]) (P = 0x800000), x, y = rand_int(-100, 500), z = rand_int(-20, 60), h = choice([56, 56, 72])
    cam         2 x rand_int(0, 200)
    per frame   for each pool entity: if chance(500): x += rand_int(-6, 6); y += rand_int(-6, 6); z = clamp(z + rand_int(-2, 2), -30, 80)
                if chance(500): camx += rand_int(-5, 5); camy += rand_int(-5, 5)
                cn = chance(40); cp = chance(40)        (name close, portrait close)
                attempt = chance(50); if attempt: e = choice(pool)
Per frame: closes (name close; portrait close only if its state != 0 and a speaker was accepted), then the name pass and the portrait pass (the draws), then the
attempt (portrait open if the entity has the flag, remembering the speaker only when accepted; then name open with the entity id).
Digest line per frame: f|name_drawn|cfg_x|text_x|name_flags|name_slot|portrait_drawn|x|y|w|h|rgb|phase|portrait_state   (name_flags, name_slot, portrait_state AFTER the attempt).
The clip is not part of the digest (not exposed by the DLL, see notes.md).
"""
import os, sys, json, hashlib
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import f4_model as F
import f4_model_h as FH
import common as C

M64 = (1 << 64) - 1
IDS = [0x104, 0x10C, 0x1AB, 0x1AC, 0x0FF, 0x200, 0x1FD, 0x14B]
HEIGHTS = [56, 56, 72]
P = F.FLAG_HAS_PORTRAIT


class SplitMix64:
    def __init__(self, seed):
        self.s = seed & M64

    def next(self):
        self.s = (self.s + 0x9E3779B97F4A7C15) & M64
        z = self.s
        z = ((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9) & M64
        z = ((z ^ (z >> 27)) * 0x94D049BB133111EB) & M64
        return z ^ (z >> 31)

    def below(self, n):
        return self.next() % n

    def rand_int(self, lo, hi):
        return lo + self.below(hi - lo + 1)

    def chance(self, permille):
        return self.below(1000) < permille

    def choice(self, seq):
        return seq[self.below(len(seq))]


def etc_model():
    etc = dict(C.NAMES)
    etc[0x1AB] = b''            # present, empty: refused (the DLL: TryResolveText true + "")
    etc.pop(0x1AC, None)        # absent: refused
    return etc


def raw_sequence(seed, frames=400):
    rnd = SplitMix64(seed)
    pool = [dict(id=rnd.choice(IDS), flags=rnd.choice([0, P, P, P]), x=rnd.rand_int(-100, 500), y=rnd.rand_int(-100, 500), z=rnd.rand_int(-20, 60),
                 h=rnd.choice(HEIGHTS)) for _ in range(4)]
    cam = [rnd.rand_int(0, 200), rnd.rand_int(0, 200)]
    nb = F.NameBox(etc_model())
    pt = FH.PortraitH()
    speaker = None
    lines = []
    cov = dict(opens=0, name_open_ok=0, name_refused_busy=0, name_refused_range=0, name_refused_empty=0,
               portrait_open_ok=0, portrait_open_ignored=0, portrait_flagless=0, name_closes=0, name_close_while_sliding_in=0,
               name_close_while_closing=0, portrait_closes=0, portrait_close_while_in=0, portrait_close_while_out=0, portrait_retarget_ignored=0,
               frames_name=0, frames_portrait=0, frames_72=0, speaker_moved_between_open_and_close=0)
    open_pos = {}
    for f in range(frames):
        for e in pool:
            if rnd.chance(500):
                e['x'] += rnd.rand_int(-6, 6)
                e['y'] += rnd.rand_int(-6, 6)
                e['z'] = max(-30, min(80, e['z'] + rnd.rand_int(-2, 2)))
        if rnd.chance(500):
            cam[0] += rnd.rand_int(-5, 5)
            cam[1] += rnd.rand_int(-5, 5)
        cn = rnd.chance(40)
        cp = rnd.chance(40)
        attempt = rnd.chance(50)
        ent = rnd.choice(pool) if attempt else None
        if cn:
            cov['name_closes'] += 1
            if nb.flags & 1:
                cov['name_close_while_sliding_in'] += 1
            if nb.flags & 2:
                cov['name_close_while_closing'] += 1
            nb.close()
        if cp and pt.state != 0 and speaker is not None:
            cov['portrait_closes'] += 1
            if pt.state & 1:
                cov['portrait_close_while_in'] += 1
            if pt.state & 2:
                cov['portrait_close_while_out'] += 1
            if (speaker['x'], speaker['y'], speaker['z']) != open_pos.get(id(speaker)):
                cov['speaker_moved_between_open_and_close'] += 1
            pt.close(speaker, tuple(cam))
        nd = nb.pass_()
        pd = pt.pass_()
        if attempt:
            cov['opens'] += 1
            if ent['flags'] & F.FLAG_HAS_PORTRAIT:
                if pt.open(ent, tuple(cam), ent['h']):
                    speaker = ent
                    open_pos[id(ent)] = (ent['x'], ent['y'], ent['z'])
                    cov['portrait_open_ok'] += 1
                    cov['frames_72'] += ent['h'] == 72
                else:
                    cov['portrait_open_ignored'] += 1
                    if ent is not speaker:
                        cov['portrait_retarget_ignored'] += 1
            else:
                cov['portrait_flagless'] += 1
            nid = ent['id']
            s = nb.etc.get(nid)
            if nb.flags & 4:
                cov['name_refused_busy'] += 1
            elif not (0x100 <= nid < 0x200):
                cov['name_refused_range'] += 1
            elif not s:
                cov['name_refused_empty'] += 1
            else:
                cov['name_open_ok'] += 1
            nb.open(nid)
        cov['frames_name'] += nd is not None
        cov['frames_portrait'] += pd is not None
        nds = '-' if nd is None else '%d|%d' % (nd['frame'][0], nd['text'][0])
        pds = '-' if pd is None else '%d|%d|%d|%d|%d|%s' % (pd['x'], pd['y'], pd['w'], pd['h'], pd['rgb'], pd['phase'])
        lines.append('%d|%s|%s|%d|%d|%d' % (f, nds, pds, nb.flags, int(nb.slot), pt.state))
    return lines, cov


def digest(lines):
    return hashlib.sha256('\n'.join(lines).encode('ascii')).hexdigest()


def main():
    seeds = list(range(int(sys.argv[1]) if len(sys.argv) > 1 else 40))
    dump = int(sys.argv[2]) if len(sys.argv) > 2 else None
    if dump is not None:
        lines, cov = raw_sequence(dump)
        print('\n'.join(lines))
        print(cov, file=sys.stderr)
        return
    out = dict(generator='splitmix64-v1 / raw-v1', frames=400, seeds=len(seeds), prng_check=[str(SplitMix64(1).next()) for _ in range(1)],
               prng_first5_seed1=[], sequences={})
    r = SplitMix64(1)
    out['prng_first5_seed1'] = [str(r.next()) for _ in range(5)]
    tot = {}
    for seed in seeds:
        lines, cov = raw_sequence(seed)
        out['sequences'][str(seed)] = dict(sha256=digest(lines), coverage=cov)
        for k, v in cov.items():
            tot[k] = tot.get(k, 0) + v
    out['coverage_total'] = tot
    path = os.path.join(HERE, '..', 'sequences-raw-digests.json')
    json.dump(out, open(path, 'w'), indent=1)
    print(json.dumps(tot, indent=1))
    print('wrote', path)


if __name__ == '__main__':
    main()
