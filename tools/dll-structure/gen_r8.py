"""R8 (T0): generates the survey `r8-comments.tsv` of the old names that stand in the comments, at the state of departure of the tranche.

    python -B tools/dll-structure/gen_r8.py [--rev REV] [--check]

Reads the `.cs` of `Alundra/` and `Alundra.Tests/` (and the two files that are not C#) at REV (default 55fac9af, the state of departure) and writes one row per candidate
(file, line, rank, kind, old, new, class, reason).  Refuses to write when a candidate falls outside the rules and the written cases of P-R8-3
(class `stop`).  `--check` compares what it would write with the file instead of writing it.
Replayed at the state of departure (the default REV), it gives the same bytes; at a later commit the comments are rewritten and it would not.
"""
import argparse
import collections
import sys

import r8_lib as L


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--rev', default='55fac9af')
    ap.add_argument('--check', action='store_true')
    a = ap.parse_args()
    paths = L.list_cs_git(a.rev)
    blobs = L.blobs_at(a.rev, paths + list(L.OTHER_FILES))
    rows, stops = [], []
    for path in paths + list(L.OTHER_FILES):
        bom, body = L.decode(blobs[path])
        cands = L.other_candidates(path, body) if path in L.OTHER_FILES else L.cs_candidates(path, body)
        for c in cands:
            if c.cls == 'stop':
                stops.append('%s:%d: %s %r' % (path, c.line, c.reason, c.old))
            rows.append([path, str(c.line), str(c.rank), c.kind, c.old, c.new, c.cls, c.reason])
    if stops:
        print('STOP: %d candidate(s) that the rules and P-R8-3 do not decide:' % len(stops))
        for s in stops:
            print('  ' + s)
        return 2
    text = '\t'.join(L.COLS) + '\n' + ''.join('\t'.join(r) + '\n' for r in rows)
    data = text.encode('utf-8')
    if a.check:
        same = open(L.SURVEY, 'rb').read().replace(b'\r\n', b'\n') == data
        print('survey identical' if same else 'survey DIFFERS')
        return 0 if same else 1
    with open(L.SURVEY, 'wb') as f:
        f.write(data)
    by = collections.Counter((r[6], r[3]) for r in rows)
    print('rows', len(rows), 'files', len({r[0] for r in rows}))
    for k, v in sorted(by.items()):
        print('  %-5s %-15s %d' % (k[0], k[1], v))
    return 0


if __name__ == '__main__':
    sys.exit(main())
