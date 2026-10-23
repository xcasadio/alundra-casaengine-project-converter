"""export_vectors.py - E20.e rev3 T0 (a), (k): the 4 000 vectors of the binary's CheckAndTriggerTileEffect (0x8003A7B0) as JSON for the tests of the port.

Replays vec/emu_cate.py (byte copy of ../../work/emu_cate.py: the mini MIPS interpreter that executes the REAL instructions 0x8003A7B0-0x8003A9DC on a synthetic memory, the three
callees recorded) on the same seed (20261017) and the same generator of cases, checks each vector against the pseudo-code of report.md (0 mismatch required), and writes
../vectors/cate-vectors.json:

  per vector: the frame flag (+0x1D4 != 0), the box (+0x1FC.. +0x21C, raw 16.16 words), the byte 9 of the map header (the effect id), the CORNER cells (the up to four cells the
  clamped corners read; every other cell of the case is unreachable for this box) before and after (flags word, height byte, wall word), the calls in the order the binary makes
  them (effect(flag, id, anim, x, y, z) / loot(code, x, y, z) / sound(id)), and the two-step form of the plan (S-E3) computed on the same case, which must give the same calls and cells.

Also written: per vector a generator word and, per cut, the model of ChooseRandomlyAnItem (cutlib.choose, checked against the real code by loot_check.py): item, draws (1 or 2), word after.
python -B export_vectors.py  (about 25 s; exit 0 = 0 mismatch, two-step == interleaved on every vector)
"""
import json
import os
import random
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, 'vec'))
sys.path.insert(0, HERE)
import emu_cate as E          # noqa: E402
import cutlib as L            # noqa: E402

M32 = 0xFFFFFFFF
N = 4000
SEED = 20261017


def corner_cells(box):
    """the clamped (col, row) of the four corners, in the binary's order, with the unclamped (col, row) of the position; [] when there is no frame box"""
    if not box['frame']:
        return []
    X = E.s32(box['x'] & M32)
    xa = ((X >> 16) & 0xFFFF)
    xa = xa - 0x10000 if xa & 0x8000 else xa
    xb = E.s32((box['x'] + box['w']) & M32) >> 16
    ya = E.s32(box['y'] & M32) >> 20
    yb = E.s32((box['y'] + box['d']) & M32) >> 20
    ca, cb = xa // 24, xb // 24
    out = []
    for col, row in ((ca, ya), (cb, ya), (ca, yb), (cb, yb)):
        c = min(max(col, 0), 0x33) if col > 0 else 0
        r = min(max(row, 0), 0x3B) if row > 0 else 0
        out.append(dict(col=col, row=row, ccol=c, crow=r))
    return out


def two_step(box, cells, hdr9):
    """S-E3: pass 1 reads the corners one at a time on the CURRENT cells and cuts, filling at most four sites; pass 2 emits, for each site in order, the effect, the loot and the sound"""
    cells = {k: list(v) for k, v in cells.items()}
    sites = []
    for c in corner_cells(box):
        fl, ht, wo = cells.get((c['ccol'], c['crow']), (0, 0, 0xFFFF))
        if not (fl & 2):
            continue
        z0 = ht << 20
        zc = z0 + 0x80000
        z = E.s32(box['z'] & M32)
        if zc < z or z + E.s32(box['h'] & M32) < zc:
            continue
        cells[(c['ccol'], c['crow'])] = [fl & 0xFFFD, ht, 0xFFFF]
        sites.append((c['col'], c['row'], z0))
    log = []
    for col, row, z0 in sites:
        x = col * 0x180000 + 0xC0000
        y = row * 0x100000 + 0x80000
        log.append(('effect', 0, hdr9, 0, x, y, z0))
        log.append(('loot', 0xFF, x, y, z0))
        log.append(('sound', 0x1F))
    return log, cells


def main():
    rng = random.Random(SEED)
    vectors = []
    bad = 0
    twostep_bad = 0
    cuts = 0
    for i in range(N):
        box, cells, hdr9 = E.rand_case(rng)
        lb, cb = E.run_binary(box, cells, hdr9)
        lp, cp = E.pseudo(box, cells, hdr9)
        lt, ct = two_step(box, cells, hdr9)
        same = lb == lp and all((cb[k][0], cb[k][3]) == (v[0], v[2]) for k, v in cp.items())
        bad += 0 if same else 1
        t_same = [tuple(x) for x in lt] == [tuple(x) for x in lb] and all((cb[k][0], cb[k][3]) == (v[0], v[2]) for k, v in ct.items())
        twostep_bad += 0 if t_same else 1
        corners = corner_cells(box)
        used = []
        for c in corners:
            if (c['ccol'], c['crow']) not in used:
                used.append((c['ccol'], c['crow']))
        cell_rows = []
        for (c, r) in used:
            fl, ht, wo = cells.get((c, r), (0, 0, 0xFFFF))
            a = cb[(c, r)]
            cell_rows.append(dict(col=c, row=r, flags=fl, height=ht, wall=wo, flags_after=a[0], wall_after=a[3]))
        state = (SEED * 1000003 + i * 7919 + 12345) & M32
        calls = []
        s = state
        for t in lb:
            if t[0] == 'effect':
                calls.append(dict(call='effect', flag=t[1], id=t[2], anim=t[3], x=t[4], y=t[5], z=t[6]))
            elif t[0] == 'loot':
                item, draws, s2 = L.choose(t[1], s)
                calls.append(dict(call='loot', code=t[1], x=t[2], y=t[3], z=t[4], model_item=item, model_draws=draws, model_state_before=s, model_state_after=s2))
                s = s2
                cuts += 1
            else:
                calls.append(dict(call='sound', id=t[1]))
        vectors.append(dict(id=i, frame=box['frame'], box={k: box[k] & M32 for k in ('x', 'y', 'z', 'w', 'd', 'h')}, tile_cut_effect_id=hdr9, corners=corners, cells=cell_rows,
                            calls=calls, generator_word=state, cuts=sum(1 for t in lb if t[0] == 'loot')))
    out = dict(binary='ALUN_CD.EXE (France) sha1 a705f8dab665645af5dfaf8fb348c7926b5caa58', routine='0x8003A7B0', count=N, seed=SEED, mismatches_vs_pseudocode=bad,
               two_step_mismatches=twostep_bad, total_cuts=sum(v['cuts'] for v in vectors), vectors=vectors,
               note='box words are the raw 32-bit values of +0x1FC..+0x21C (width/depth/height are (n << 16) - 1); a cell is (flags word, height byte, wall word); the cell words not listed are 0, 0, 0xFFFF')
    os.makedirs(os.path.join(HERE, '..', 'vectors'), exist_ok=True)
    path = os.path.join(HERE, '..', 'vectors', 'cate-vectors.json')
    with open(path, 'w', newline='\n') as f:
        json.dump(out, f, sort_keys=True, separators=(',', ':'))
        f.write('\n')
    print('vectors=%d mismatches=%d two_step_mismatches=%d cuts=%d bytes=%d' % (N, bad, twostep_bad, out['total_cuts'], os.path.getsize(path)))
    return 1 if (bad or twostep_bad) else 0


if __name__ == '__main__':
    sys.exit(main())
