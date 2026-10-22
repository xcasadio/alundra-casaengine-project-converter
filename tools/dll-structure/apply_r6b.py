"""R6b: the line numbers cited by the 21 living documents.

    python -B tools/dll-structure/apply_r6b.py --survey     write r6b-citations.tsv (the survey: every range, before and after, the probes)
    python -B tools/dll-structure/apply_r6b.py --apply      edit the documents of the working tree (CRLF kept) from the survey

The decisions are the table DECISIONS below: one line per range of the documents at BASE_COMMIT, the range as it must read at the pinned trees
(decompilation `fc16872`, engine `92d8e847`, the parent itself for the DLL and the documents), the scope of the citation and the members it names.
`prove_r6b.py` checks the result independently.  The code cited never changes, only the citation.

Rules of the decisions (P-R6b-3)
- a range that names a member (`whole`) starts at the declaration of the member and ends at its closing brace or at its last statement; several
  members (`A+B`): from the declaration of the first to the end of the last;
- `part`: starts at the declaration, ends inside the member, on a line of code, where the sentence stops;
- `decl`: the single line is the declaration;
- `-`: a statement or a block described by the sentence: the start and the end are lines of code of that block;
- unchanged numbers stay (class `juste`); the others are `recalculée (décalage)` when both ends move by the same delta, otherwise
  `recalculée par contenu`.
"""
import argparse
import re
import subprocess
import sys

import r6b_lib as L

R = 'docs/intro-roadmap.md'
C = 'docs/formats/cells-companion.md'
G = 'docs/guidelines-runtime-alundra-casaengine.md'
B = 'docs/formats/backdrops.md'

# doc | line | k | after | scope | members
DECISIONS = """
B|20|0|58-60|-|-
C|103|0|287-358|whole|CreateRendererOrderedTileLayers+AddRendererTile
C|117|0|298-319|-|-
C|119|0|338-358|whole|AddRendererTile
C|127|0|143|-|-
C|142|0|287|-|-
C|142|1|313-321|whole|GetTileDepthSlot
C|145|0|216-244|whole|CreateTileAnimDescriptors
C|220|0|275|-|-
C|220|1|324-347|-|-
G|142|0|159-162|whole|DeriveRenderPosition
G|229|0|552-621|whole|Update
R|39|0|1667-1718|whole|RunMapEvents
R|43|0|232-296|part|RunScript
R|59|0|1166-1176|whole|Script_54_036
R|60|0|1112-1129|whole|Script_51_033
R|61|0|2558-2579|whole|Script_139_08B
R|67|0|681-718|part|SpawnEntity
R|79|0|1223-1240|whole|Script_59_03B
R|97|0|1692-1698|-|-
R|104|0|268-273|-|-
R|166|0|1667-1718|whole|RunMapEvents
R|174|0|476-583|whole|InitializeMapEvents
R|180|0|806-921|whole|UpdateEntitiesEvents
R|183|0|239-264|-|-
R|186|0|234|-|-
R|188|0|304-377|-|-
R|188|1|345-363|-|-
R|189|0|380-389|-|-
R|196|0|1500-1587|whole|Update
R|197|0|1518|-|-
R|199|0|1638-1664|whole|UpdateWorld
R|201|0|367-394|part|UpdateEntities
R|205|0|808|-|-
R|207|0|17|decl|MovePlayer
R|208|0|225-229|-|-
R|212|0|169-219|-|-
R|213|0|179|-|-
R|213|1|429|decl|ClearTemporaryFlags
R|214|0|445|-|-
R|215|0|466|decl|LoadMapAndInitializeEntities
R|217|0|878|decl|WarpPlayer
R|220|0|621-645|whole|InitializeEntitySlots
R|223|0|681-758|whole|SpawnEntity
R|225|0|715-718|-|-
R|232|0|331-436|whole|InitializeGameState
R|237|0|648-672|whole|ResetEntityState
R|242|0|12814|-|-
R|243|0|331|-|-
R|245|0|58|-|-
R|245|1|61|-|-
R|248|0|38|-|-
R|250|0|680-691|whole|Script_16_010+Script_17_011
R|261|0|239-296|-|-
R|272|0|1202-1207|whole|Script_SetSaveMapIdToInternalMapIndex_038
R|273|0|2070-2075|whole|Script_103_067
R|274|0|680-684|whole|Script_16_010
R|275|0|1623-1654|whole|Script_85_055
R|276|0|2441-2445|whole|Script_133_085
R|276|1|2239-2322|whole|ChangeAreaTileProperties
R|277|0|1223-1240|whole|Script_59_03B
R|278|0|1476-1486|whole|Script_75_04B
R|279|0|743-747|whole|Script_27_01B
R|280|0|539-582|whole|Script_7_007
R|281|0|793-829|whole|Script_30_01E
R|282|0|599-603|whole|Script_10_00A
R|283|0|1455-1459|whole|Script_73_049
R|284|0|1713-1732|whole|Script_91_05B
R|285|0|729-733|whole|Script_25_019
R|286|0|832-841|whole|Script_31_01F
R|287|0|715-719|whole|Script_22_016
R|288|0|1694-1710|whole|Script_90_05A
R|289|0|2161-2165|whole|Script_112_070
R|290|0|687-691|whole|Script_17_011
R|325|0|429|decl|ClearTemporaryFlags
R|325|1|368-387|whole|InstallForMapEntry
R|326|0|445|-|-
R|327|0|454|-|-
R|328|0|621-672|whole|InitializeEntitySlots+ResetEntityState
R|329|0|476-583|whole|InitializeMapEvents
R|330|0|472|-|-
R|331|0|878-973|whole|WarpPlayer
R|332|0|214|-|-
R|333|0|215|-|-
R|334|0|216|-|-
R|335|0|218|-|-
R|336|0|1518|-|-
R|337|0|1667-1718|whole|RunMapEvents
R|338|0|808|-|-
R|338|1|17|decl|MovePlayer
R|339|0|243|decl|RunSpriteEvent
R|339|1|9|decl|SetSpawnFlagFromZPos
R|339|2|8|decl|FUN_8007ed10
R|340|0|379|-|-
R|341|0|381|-|-
R|342|0|383|-|-
R|343|0|384|-|-
R|344|0|10|decl|UpdateEntitiesPhysics
R|345|0|386|-|-
R|346|0|387|-|-
R|347|0|394|-|-
R|348|0|1663|-|-
R|349|0|1562-1577|-|-
R|350|0|1579|-|-
R|351|0|1581|-|-
R|368|0|24|-|-
R|368|1|50|-|-
R|368|2|155|-|-
R|368|3|203|-|-
R|368|4|233|-|-
R|373|0|1579-1597|whole|UpdateEntityPhysics
R|378|0|2239-2322|whole|ChangeAreaTileProperties
"""
DOCKEY = {'R': R, 'C': C, 'G': G, 'B': B}

# the citation that R6 removed from docs/intro-roadmap.md:227 and that P-R6b-4 restores (the text of the line is in the base blob)
RESTORED = dict(doc=R, line=227, k=0, cited='EntitySpawnFactory.cs', target='Alundra/Entities/Spawning/EntitySpawnFactory.cs',
                before='', after='59-94', anchor='Alundra/Entities/Spawning/EntitySpawnFactory.cs')

# the three citations that the history of the parent follows, not a pinned submodule
HAND_METHOD = {
    (R, 325, 1): 'DLL: pickaxe 59fac902 (2026-09-28), Alundra/Scripts/AlundraGameState.cs:275-294 (InstallForMapEntry); git diff --histogram to HEAD: +93, identical lines',
    (B, 20, 0): 'document: pickaxe dfaf38f4 (2026-09-06); a3fa8810 added one line above the paragraph of docs/editeur-couverture-dll.md',
    (G, 142, 0): 'engine: the formula render = (X, -(Y - Z), 0) is DeriveRenderPosition of TopDownElevationSimulationSpacePolicy at the pinned engine',
    (G, 229, 0): 'engine: World.Update(FrameTime) at the pinned engine (entities first, then GameplayProxy.Update)',
}

SEP = re.compile(r'^\d+(?:-\d+)?$')
TSV_COLS = ['doc', 'line', 'k', 'col', 'cited', 'target', 'before', 'after', 'W', 'method', 'class', 'scope', 'members', 'probe_start', 'probe_end']
TSV = L.ROOT / 'tools' / 'dll-structure' / 'r6b-citations.tsv'
IGNORE_REVS = ['542e62c9', 'a25b815d', 'e73815b7', '2964d9ae', '613c6c9e']


def decisions():
    out = {}
    for row in DECISIONS.strip().split('\n'):
        d, ln, k, after, scope, members = row.split('|')
        out[(DOCKEY[d], int(ln), int(k))] = (after, scope, members)
    return out


_blame = {}


def blame(doc):
    if doc not in _blame:
        args = ['blame', '--porcelain', '-w'] + [x for r in IGNORE_REVS for x in ('--ignore-rev', L.git('rev-parse', r).strip())] + [L.BASE_COMMIT, '--', doc]
        out = L.git(*args) or ''
        res = {}
        for m in re.finditer(r'^([0-9a-f]{40}) \d+ (\d+)', out, re.M):
            res[int(m.group(2))] = m.group(1)[:8]
        _blame[doc] = res
    return _blame[doc]


def sub_of(target):
    return next((s for s in L.SUBMODULES if target.startswith(s + '/')), None)


def method_of(c, w):
    sub = sub_of(c['target'])
    if not sub:
        return ''
    now = L.pointer(sub)
    then = L.pointer(sub, w)
    inner = c['target'][len(sub) + 1:]
    a = L.git('show', f'{then}:{inner}', cwd=L.ROOT / sub, raw=True)
    b = L.git('show', f'{now}:{inner}', cwd=L.ROOT / sub, raw=True)
    state = 'file unchanged since W' if a == b else 'file changed since W'
    return f'{sub}@{then[:8]}->{now[:8]}: {state}; range checked against the content at the pointer'


def klass(lo, hi, nlo, nhi):
    if (lo, hi) == (nlo, nhi):
        return 'juste'
    if nlo - lo == nhi - hi:
        return 'recalculée (décalage)'
    return 'recalculée par contenu'


def survey():
    dec = decisions()
    rows = []
    errors = []
    seen = set()
    for c in L.extract_all():
        key = (c['doc'], c['line'], c['k'])
        seen.add(key)
        if key not in dec:
            errors.append(f'no decision for {key} {c["before"]}')
            continue
        after, scope, members = dec[key]
        if not SEP.match(c['before']) or not SEP.match(after):
            errors.append(f'unexpected form {key} {c["before"]!r} -> {after!r}')
            continue
        nlo, nhi = (int(x) for x in after.split('-')) if '-' in after else (int(after),) * 2
        lines = L.target_lines(c['target'])
        if lines is None:
            errors.append(f'target not readable {key} {c["target"]}')
            continue
        if nhi > len(lines):
            errors.append(f'beyond the end {key} {after} / {len(lines)}')
            continue
        w = blame(c['doc']).get(c['line'], '')
        method = HAND_METHOD.get(key) or method_of(c, w)
        rows.append(dict(doc=c['doc'], line=c['line'], k=c['k'], col=c['col'], cited=c['cited'], target=c['target'], before=c['before'],
                         after=after, W=w, method=method, **{'class': klass(c['lo'], c['hi'], nlo, nhi)}, scope=scope, members=members,
                         probe_start=L.probe(lines, nlo), probe_end=L.probe(lines, nhi)))
    for key in dec:
        if key not in seen:
            errors.append(f'decision without citation {key}')
    # the restored citation
    r = RESTORED
    text = L.text_at(L.BASE_COMMIT, r['doc']).split('\n')[r['line'] - 1]
    i = text.find(r['anchor'] + '`')
    if i < 0 or text.count(r['anchor'] + '`') != 1:
        errors.append('anchor of the restored citation not found exactly once')
    else:
        lines = L.target_lines(r['target'])
        lo, hi = (int(x) for x in r['after'].split('-'))
        rows.append(dict(doc=r['doc'], line=r['line'], k=r['k'], col=i + len(r['anchor']), cited=r['cited'], target=r['target'], before='',
                         after=r['after'], W='3a880a63',
                         method='P-R6b-4: written at 3a880a63 against Alundra/Scripts/AlundraWorldProxy.cs:288-323, removed by R6 (542e62c9); the 36 lines are identical here (the numbers of P-R6b-4, kept to the letter: they start in the XML doc of ShouldSpawnRecord and stop inside it)',
                         **{'class': 'rétablie'}, scope='tracked', members='-', probe_start=L.probe(lines, lo), probe_end=L.probe(lines, hi)))
    rows.sort(key=lambda x: (L.DOCS.index(x['doc']), x['line'], x['col']))
    return rows, errors


def write_tsv(rows):
    with open(TSV, 'w', encoding='utf-8', newline='') as f:
        f.write('\t'.join(TSV_COLS) + '\n')
        for r in rows:
            f.write('\t'.join(str(r[c]).replace('\t', ' ') for c in TSV_COLS) + '\n')


def read_tsv():
    import csv
    with open(TSV, encoding='utf-8', newline='') as f:
        return list(csv.DictReader(f, delimiter='\t', quoting=csv.QUOTE_NONE))


def expected_line(base_line, rows_of_line):
    """The line of the document after the edits of the survey (rows sorted by column)."""
    out = base_line
    for r in sorted(rows_of_line, key=lambda x: -int(x['col'])):
        col = int(r['col'])
        if r['before'] == '':
            out = out[:col] + ':' + r['after'] + out[col:]
        else:
            if out[col:col + len(r['before'])] != r['before']:
                raise ValueError(f'{r["doc"]}:{r["line"]} column {col}: {out[col:col + len(r["before"])]!r} is not {r["before"]!r}')
            out = out[:col] + r['after'] + out[col + len(r['before']):]
    return out


def apply_docs(rows):
    by_doc = {}
    for r in rows:
        by_doc.setdefault(r['doc'], {}).setdefault(int(r['line']), []).append(r)
    for doc, lines_rows in by_doc.items():
        base = L.text_at(L.BASE_COMMIT, doc)
        path = L.ROOT / doc
        raw = path.read_bytes()
        cur = raw.decode('utf-8').replace('\r\n', '\n')
        if cur != base:
            raise SystemExit(f'{doc}: the working tree is not the document of {L.BASE_COMMIT} (or was already edited); stop')
        lines = base.split('\n')
        for ln, rs in lines_rows.items():
            lines[ln - 1] = expected_line(lines[ln - 1], rs)
        new = '\n'.join(lines)
        eol = b'\r\n' if b'\r\n' in raw else b'\n'
        path.write_bytes(new.encode('utf-8').replace(b'\n', eol))
        print(f'{doc}: {sum(len(v) for v in lines_rows.values())} ranges edited')


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--survey', action='store_true')
    ap.add_argument('--apply', action='store_true')
    a = ap.parse_args()
    if a.survey:
        rows, errors = survey()
        if errors:
            print('\n'.join(errors))
            sys.exit(1)
        write_tsv(rows)
        from collections import Counter
        print(len(rows), 'ranges,', dict(Counter(r['class'] for r in rows)))
    if a.apply:
        apply_docs(read_tsv())


if __name__ == '__main__':
    main()
