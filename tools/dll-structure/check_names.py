"""Name checks for the Alundra DLL restructuring (docs/plan-restructuration-dll.md, rule (4) of section 2).

  python -B tools/dll-structure/check_names.py --table [--table-file <table.json>]
      The names planned by the table (every stage) against the engine, MGUI, NvgSharp, the packages and the BCL.
  python -B tools/dll-structure/check_names.py --tree --stage r2|r3|final [--root <folder>]
      The tree of the DLL (the working tree, or <folder>/Alundra) against the table at that stage, then the same collision checks on the
      names really declared.
  python -B tools/dll-structure/check_names.py --tests
      R5 (T22): the tree of Alundra.Tests (the working tree) against r5-files.tsv and r5-types.tsv: every .cs at its path of the table and
      no other, namespace = folder, every top-level type at its new name in its arrival file, no `Alundra*` / `IAlundra*` type or file name,
      no `Scripts/` folder, no `.cs` at the root or directly under `UI/`, then the collision checks on the names really declared.

All list EVERY finding and exit 1 if there is any (0 otherwise, 2 on a usage or reading error). Read-only on the repository.
Finding tags: [collision] [segment] [nom] [namespace] [fichier] [absent] [hors table]; warnings: [avertissement].
"""
import argparse
import collections
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import scan_types  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
DEFAULT_TABLE = os.path.join(HERE, 'table.json')
STAGES = ('r2', 'r3', 'final')


def collision_findings(types, ext):
    """types: iterable of (where, namespace, name). Returns the list of finding lines."""
    out = []
    segments = collections.defaultdict(set)
    for where, ns, name in types:
        for seg in (ns or '').split('.'):
            if seg:
                segments[seg].add(where)
    for where, ns, name in sorted(set(types)):
        for e in ext.get(name.lower(), []):
            if not e['nested']:
                out.append('[collision] type %s.%s (%s) has the same name as %s %s.%s' % (ns, name, where, e['label'], e['ns'], e['name']))
        if name in segments and (name != 'Alundra'):
            out.append('[segment] type %s.%s (%s) has the name of a namespace segment of the DLL (used in %s)' % (
                ns, name, where, ', '.join(sorted(segments[name])[:3])))
    for seg in sorted(segments):
        for e in ext.get(seg.lower(), []):
            if not e['nested']:
                out.append('[collision] namespace segment %s (%s) would mask the type %s %s.%s' % (
                    seg, ', '.join(sorted(segments[seg])[:3]), e['label'], e['ns'], e['name']))
    return out


def test_warnings(segments):
    out = []
    try:
        for d in scan_types.test_nested_names():
            if d['name'] in segments:
                out.append('[avertissement] %s:%d: the nested type %s has the name of a namespace segment (it only masks it inside its own class)' % (
                    d['path'], d['line'], d['name']))
    except scan_types.ScanError as e:
        out.append('[avertissement] tests not scanned: %s' % e)
    return out


def table_names(table):
    """(where, namespace, name) for every type and root type of every stage."""
    res = []
    for r in table['types']:
        for st in STAGES:
            res.append((st, r[st]['namespace'], r[st]['name']))
    for r in table['root_files']:
        for t in r['types']:
            res.append(('root', r['namespace'], t))
    return res


def check_table(table, ext):
    names = table_names(table)
    findings = collision_findings(names, ext)
    segs = {s for _, ns, _ in names for s in (ns or '').split('.') if s}
    warnings = test_warnings(segs)
    return findings, warnings, 'table: %d distinct (namespace, name) pairs' % len(set((a, b) for _, a, b in names))


def check_tree(table, stage, root, ext):
    decls, paths = scan_types.dll_decls(root=root)
    top = [d for d in decls if not d['nested']]
    findings = []
    expected = {}  # file -> list of (namespace, name, old description)
    for r in table['types']:
        expected.setdefault(r[stage]['file'], []).append((r[stage]['namespace'], r[stage]['name'], r['old_name']))
    for r in table['root_files']:
        expected.setdefault(r[stage], [])
        for t in r['types']:
            expected[r[stage]].append((r['namespace'], t, t))
    expected_lower = {p.lower() for p in expected}
    for f in sorted(expected):
        if f not in paths:
            findings.append('[absent] file %s (stage %s)' % (f, stage))
    for p in paths:
        if p.lower() not in expected_lower:
            findings.append('[hors table] file %s is not in the table at stage %s' % (p, stage))
    by_name = collections.defaultdict(list)
    for d in top:
        by_name[d['name']].append(d)
    seen = set()
    for f in sorted(expected):
        for ns, name, old in sorted(expected[f]):
            if (f, ns, name) in seen:
                continue
            seen.add((f, ns, name))
            here = [d for d in by_name.get(name, []) if d['path'] == f and d['ns'] == ns]
            if here:
                continue
            same = by_name.get(name, [])
            if not same:
                findings.append('[nom] %s (old name %s): no type named %s in the tree; expected in %s, namespace %s' % (name, old, name, f, ns))
            elif any(d['path'] == f for d in same):
                findings.append('[namespace] %s in %s is declared in namespace %s, expected %s' % (
                    name, f, ', '.join(sorted({d['ns'] for d in same if d['path'] == f})), ns))
            else:
                findings.append('[fichier] %s is declared in %s (namespace %s), expected in %s (namespace %s)' % (
                    name, ', '.join(sorted({d['path'] for d in same})), ', '.join(sorted({d['ns'] for d in same})), f, ns))
    names = [(d['path'], d['ns'], d['name']) for d in top]
    findings += collision_findings(names, ext)
    segs = {s for _, ns, _ in names for s in (ns or '').split('.') if s}
    return findings, test_warnings(segs), 'tree: %d files, %d top-level types' % (len(paths), len(top))


def check_tests(ext):
    import r5_lib
    tbl = r5_lib.R5()
    findings = []
    out = scan_types.git(scan_types.REPO, 'ls-files', '--cached', '--others', '--exclude-standard', '--', 'Alundra.Tests').decode('utf-8').splitlines()
    paths = sorted(p for p in out if p.endswith('.cs') and not scan_types.EXCLUDED_DIR_RE.search(p) and (scan_types.REPO / p).is_file())
    expected = {r['new_path']: r for r in tbl.cs}
    for f in sorted(expected):
        if f not in paths:
            findings.append('[absent] file %s' % f)
    low = {p.lower() for p in expected}
    for p in paths:
        if p.lower() not in low:
            findings.append('[hors table] file %s is not in r5-files.tsv' % p)
        rel = p[len('Alundra.Tests/'):]
        folder, _, name = rel.rpartition('/')
        if not folder:
            findings.append('[fichier] %s is a .cs at the root of Alundra.Tests' % p)
        if folder == 'UI':
            findings.append('[fichier] %s is directly under UI/' % p)
        if folder.split('/')[0] == 'Scripts':
            findings.append('[fichier] %s is under a Scripts/ folder' % p)
        if name.startswith('Alundra') or name.startswith('IAlundra'):
            findings.append('[nom] file name %s keeps the prefix' % p)
    decls = []
    for p in paths:
        decls += scan_types.parse(p, (scan_types.REPO / p).read_bytes().decode('utf-8-sig', errors='replace'))
    top = [d for d in decls if not d['nested']]
    for d in decls:
        if d['name'].startswith('Alundra') or d['name'].startswith('IAlundra'):
            findings.append('[nom] %s:%d declares %s%s with the prefix' % (d['path'], d['line'], d['name'], ' (nested)' if d['nested'] else ''))
    for p in paths:
        r = expected.get(p)
        if r is None:
            continue
        folder = p[len('Alundra.Tests/'):].rsplit('/', 1)[0]
        if r['namespace'] != 'Alundra.Tests.' + folder.replace('/', '.'):
            findings.append('[namespace] the table: %s has namespace %s, not its folder' % (p, r['namespace']))
    got = collections.Counter((d['path'], d['ns'], d['name']) for d in top)
    want = collections.Counter((t['new_file'], t['new_namespace'], t['new_type']) for t in tbl.types)
    for k in sorted(set(got) | set(want)):
        if got[k] != want[k]:
            findings.append('[nom] %s: type %s.%s declared %d time(s), r5-types.tsv says %d' % (k[0], k[1], k[2], got[k], want[k]))
    names = [(d['path'], d['ns'], d['name']) for d in top]
    dll = [(d['path'], d['ns'], d['name']) for d in scan_types.dll_decls()[0] if not d['nested']]
    findings += collision_findings(names + dll, ext)
    segs = {s for _, ns, _ in names + dll for s in (ns or '').split('.') if s}
    return findings, test_warnings(segs), 'tests: %d files, %d top-level types' % (len(paths), len(top))


def main(argv=None):
    sys.stdout.reconfigure(encoding='utf-8')
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    g = ap.add_mutually_exclusive_group(required=True)
    g.add_argument('--table', action='store_true')
    g.add_argument('--tree', action='store_true')
    g.add_argument('--tests', action='store_true')
    ap.add_argument('--table-file', default=DEFAULT_TABLE)
    ap.add_argument('--stage', choices=STAGES)
    ap.add_argument('--root')
    a = ap.parse_args(argv)
    if a.tree and not a.stage:
        ap.error('--tree needs --stage')
    try:
        table = json.load(open(a.table_file, encoding='utf-8'))
        ext = scan_types.external_types()
        if a.tests:
            findings, warnings, head = check_tests(ext)
        else:
            findings, warnings, head = check_table(table, ext) if a.table else check_tree(table, a.stage, a.root, ext)
    except (scan_types.ScanError, OSError, ValueError) as e:
        print('ERROR: %s' % e)
        return 2
    print(head)
    for line in findings + warnings:
        print(line)
    print('%d finding(s), %d warning(s)' % (len(findings), len(warnings)))
    return 1 if findings else 0


if __name__ == '__main__':
    sys.exit(main())
