"""sverify.py - E20.i discovery ("swim-oracle"): proves that the swim extension (sscen.py) does not change the D1 gate nor any existing run trace.

NEW FILE.  It imports sscen FIRST, so every import-time effect of the extension is in place (the extra inventories added to hworld.INV, rscen.OUT redirected), then
  1. runs one swim scenario (exercises the extension code and the layout / inventory machinery in this process);
  2. runs the D1 gate THROUGH the extended process: rgate.main() with its output redirected to ../out-verify/gate (S-A and S-B, 216 values each against the annex,
     and the two trace JSON files against the md5 of the unmodified D1 tools);
  3. regenerates every group of rscen (G1..G10, --cross: each run repeated through the second interpreter) into ../out-verify with the grouping of the reference run
     (G1 | G2 G3 | G4 | G5 G6 | G7 | G8 G9 | G10: the cov-*.json files depend on the grouping);
  4. compares the md5 of every regenerated file with the reference manifest (the files that rscen.py / rgate.py write: g*/, summary-G*, check/xcheck-G*, check/cov-G*, gate/*
     except gate/run.log).
Exit code 0 = every file identical.  usage: python -B sverify.py"""
import hashlib
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import sscen                      # noqa: E402  (import-time effects first, on purpose)
import rworld as R                # noqa: E402
import rscen as RS                # noqa: E402
import rgate                      # noqa: E402

VER = os.path.join(HERE, '..', 'out-verify')
REF = os.path.join(HERE, '..', '..', '..', '..', '..', '..', 'ref', 'out-ref', 'manifest.txt')   # scratch layout: <label>/ref/out-ref/manifest.txt
GROUPING = [['G1'], ['G2', 'G3'], ['G4'], ['G5', 'G6'], ['G7'], ['G8', 'G9'], ['G10']]


def md5(p):
    return hashlib.md5(open(p, 'rb').read()).hexdigest()


def main():
    os.makedirs(VER, exist_ok=True)
    lines = []
    # 1. one swim scenario in this process
    sscen.OUT = os.path.join(VER, 'swim-probe')
    RS.OUT = sscen.OUT
    res = sscen.run(dict(name='verify-swim-probe', desc='one swim run in the process that then runs the gate', hero_px=sscen.CENTER, n=20, pad=[(5, sscen.RIGHT)], layout=sscen.allwater(), inv='b1b', hero_anim=0x1D), '')
    lines.append('swim probe executed first: %d ticks, anim timeline %s' % (len(res['rows']), R.timeline_str(res)))
    # 2. the D1 gate through this process
    RS.OUT = VER
    rgate.GATE = os.path.join(VER, 'gate')
    rc = rgate.main()
    lines.append('rgate.main() exit code %d' % rc)
    # 3. every group of rscen, grouping of the reference run, with the second interpreter
    for names in GROUPING:
        R.COV.clear()
        RS.main(names + ['--cross'])
        lines.append('rscen groups %s regenerated' % ' '.join(names))
    # 4. md5 against the reference manifest
    want = {}
    for ln in open(REF, encoding='utf-8').read().splitlines():
        m = re.match(r'^([0-9a-f]{32})\s+(\S.*)$', ln)
        if not m:
            continue
        rel = m.group(2).lstrip('*')
        if re.match(r'^(g\d+/|summary-G|check/xcheck-G|check/cov-G|gate/)', rel) and rel != 'gate/run.log':
            want[rel] = m.group(1)
    bad, missing, ok = [], [], 0
    for rel, h in sorted(want.items()):
        p = os.path.join(VER, rel)
        if not os.path.exists(p):
            missing.append(rel)
        elif md5(p) != h:
            bad.append(rel)
        else:
            ok += 1
    extra = []
    for root, dirs, files in os.walk(VER):
        for f in files:
            rel = os.path.relpath(os.path.join(root, f), VER).replace(os.sep, '/')
            if rel.startswith('swim-probe/'):
                continue
            if re.match(r'^(g\d+/|summary-G|check/xcheck-G|check/cov-G|gate/)', rel) and rel not in want and rel != 'gate/run.log':
                extra.append(rel)
    lines.append('reference files compared: %d identical, %d different %s, %d missing %s, %d extra %s' % (ok, len(bad), bad[:5], len(missing), missing[:5], len(extra), extra[:5]))
    lines.append('SWIM EXTENSION %s' % ('DOES NOT CHANGE the gate nor any existing trace' if (rc == 0 and not bad and not missing and not extra) else 'CHANGES SOMETHING'))
    open(os.path.join(VER, 'verify.txt'), 'w').write('\n'.join(lines) + '\n')
    print('\n'.join(lines))
    return 0 if (rc == 0 and not bad and not missing and not extra) else 1


if __name__ == '__main__':
    sys.exit(main())
