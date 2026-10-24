"""scan_traces.py - walk-release-disc W3: among the oracle traces of the annex (JSON with hero rows) which ones contain a tick where the hero goes to Idle (target animation 0) from another
animation with a non-zero horizontal force on either side of the change ? Those are the traces whose replay is sensitive to the acceleration byte of the Idle animation set.
Read-only. Run: python -B -I scan_traces.py
"""
import json, glob, os, collections

ROOT = r'D:/development/repo/alundra-casaengine-project-converter/docs/plan-e20-annexe'
ANIM_KEYS = ('anim_target', 'anim_t', 'anim_tgt', 'target_anim')
FX_KEYS = ('force_x', 'fx')
FY_KEYS = ('force_y', 'fy')


def pick(d, keys):
    for k in keys:
        if k in d:
            return d[k]
    return None


def hero_of(row):
    h = row.get('hero') if isinstance(row, dict) else None
    return h if isinstance(h, dict) else None


def main():
    files = sorted(glob.glob(ROOT + '/**/*.json', recursive=True))
    fam = collections.OrderedDict()
    scanned = 0
    for f in files:
        try:
            d = json.load(open(f, encoding='utf-8-sig'))
        except Exception:
            continue
        if not isinstance(d, dict):
            continue
        rows = d.get('rows') or d.get('trace') or d.get('ticks')
        if not isinstance(rows, list) or not rows or hero_of(rows[0]) is None:
            continue
        h0 = hero_of(rows[0])
        if pick(h0, ANIM_KEYS) is None or pick(h0, FX_KEYS) is None:
            continue
        scanned += 1
        seq = []
        pre = d.get('pre')
        if isinstance(pre, dict) and hero_of(pre) is not None:
            seq.append((0, hero_of(pre)))
        for i, r in enumerate(rows):
            seq.append((r.get('tick', i + 1), hero_of(r)))
        hits = []
        for (t0, a), (t1, b) in zip(seq, seq[1:]):
            aa, ba = pick(a, ANIM_KEYS), pick(b, ANIM_KEYS)
            if ba == 0 and aa != 0 and any(v for v in (pick(a, FX_KEYS), pick(a, FY_KEYS), pick(b, FX_KEYS), pick(b, FY_KEYS))):
                hits.append((t1, aa, pick(a, FX_KEYS), pick(a, FY_KEYS), pick(b, FX_KEYS), pick(b, FY_KEYS)))
        key = os.path.relpath(os.path.dirname(f), ROOT)
        fam.setdefault(key, [0, 0, []])
        fam[key][0] += 1
        if hits:
            fam[key][1] += 1
            fam[key][2].append((os.path.basename(f), hits[0]))
    print('trace files scanned (hero rows with an animation and a force column):', scanned)
    for k, (n, nh, ex) in fam.items():
        print('%-45s scanned %3d  with a change to Idle under force: %3d  %s' % (k, n, nh, ex[:3]))


if __name__ == '__main__':
    main()
