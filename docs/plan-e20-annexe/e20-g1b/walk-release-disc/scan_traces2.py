"""scan_traces2.py - walk-release-disc W3: same question as scan_traces.py for the two other oracle families the hero tests replay on the montages with the attack clock (Idle acceleration 0):
  * E20.b attack traces  docs/plan-e20-annexe/e20-b/oracle/out/**/*.trace.json (rows with flat keys anim_target, force_x, force_y)  -> HeroAttackTests
  * E20.f2 flail probes  docs/plan-e20-annexe/e20-f2/oracle/f-weapons/out/F-*.txt (rows 't N hold= anim_t= anim_c= fx= fy= ...')    -> FlailBounceTests
and the charge traces of E20.f (HeroChargeTests) if they follow one of the two formats. A hit = a tick where the target animation goes to 0 from another one while a horizontal force is
non-zero before or after the change.   Read-only.  Run: python -B -I scan_traces2.py
"""
import json, glob, os, re

ROOT = r'D:/development/repo/alundra-casaengine-project-converter/docs/plan-e20-annexe'


def scan_json(f):
    d = json.load(open(f, encoding='utf-8-sig'))
    rows = d.get('rows')
    if not isinstance(rows, list) or not rows or not isinstance(rows[0], dict) or 'anim_target' not in rows[0] or 'force_x' not in rows[0]:
        return None
    seq = [d['start']] if isinstance(d.get('start'), dict) and 'anim_target' in d['start'] else []
    seq += rows
    hits = []
    for a, b in zip(seq, seq[1:]):
        if b['anim_target'] == 0 and a['anim_target'] != 0 and any((a.get('force_x'), a.get('force_y'), b.get('force_x'), b.get('force_y'))):
            hits.append((b.get('tick'), a['anim_target'], a.get('force_x'), b.get('force_x')))
    return hits


ROW = re.compile(r'^t\s*(\d+) hold=([0-9A-F]+) anim_t=([0-9A-F]+) anim_c=([0-9A-F]+) fx=\s*(-?\d+) fy=\s*(-?\d+)')


def scan_txt(f):
    rows = []
    for line in open(f, encoding='utf-8', errors='replace'):
        m = ROW.match(line)
        if m:
            rows.append((int(m.group(1)), int(m.group(3), 16), int(m.group(5)), int(m.group(6))))
    if not rows:
        return None
    hits = []
    for a, b in zip(rows, rows[1:]):
        if b[1] == 0 and a[1] != 0 and (a[2] or a[3] or b[2] or b[3]):
            hits.append((b[0], a[1], a[2], b[2]))
    return hits


def main():
    nj = nh = 0
    ex = []
    for f in sorted(glob.glob(ROOT + '/e20-b/**/*.trace.json', recursive=True)):
        h = scan_json(f)
        if h is None:
            continue
        nj += 1
        if h:
            nh += 1
            ex.append((os.path.relpath(f, ROOT), h[:2]))
    print('E20.b attack traces scanned: %d ; with a change to Idle under force: %d' % (nj, nh))
    for e in ex[:10]:
        print('  ', e)
    nt = nth = 0
    ex = []
    for f in sorted(glob.glob(ROOT + '/e20-f2/**/*.txt', recursive=True)):
        h = scan_txt(f)
        if h is None:
            continue
        nt += 1
        if h:
            nth += 1
            ex.append((os.path.relpath(f, ROOT), h[:2]))
    print('E20.f2 flail probe files scanned: %d ; with a change to Idle under force: %d' % (nt, nth))
    for e in ex[:10]:
        print('  ', e)
    # any other .trace.json / probe file with the flat format
    other = 0
    for f in sorted(glob.glob(ROOT + '/**/*.trace.json', recursive=True)):
        if '/e20-b/' in f.replace('\\', '/'):
            continue
        h = scan_json(f)
        if h is not None:
            other += 1
            if h:
                print('  other family with a hit:', os.path.relpath(f, ROOT), h[:2])
    print('other flat-format .trace.json scanned:', other)


if __name__ == '__main__':
    main()
