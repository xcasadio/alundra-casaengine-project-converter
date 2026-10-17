"""R4 of the DLL restructuring (docs/plan-restructuration-dll.md, section 8, T0 (b)): the 9 types that the data cite go to their final place
and name. Python 3, standard library only, always run with `python -B`. R2 and R3 tools are reused (apply_r2.py: lexer, table, `using` lines).

  python -B tools/dll-structure/apply_r4.py move                    T1: `git mv` of the 10 files (r3 path -> final path of table.json), not one byte changes
  python -B tools/dll-structure/apply_r4.py fix [--allow-dirty]     T2 step (2): (a) the `namespace` line of the 10 files; (b) for ALL .cs files of the DLL,
                                                                    of Alundra.Tests and of tools/AlundraTestSaves: the `using` lines and the
                                                                    `Alundra.Scripts.<T>` qualifications (code and cref) of the types that moved
  python -B tools/dll-structure/apply_r4.py literals [--allow-dirty]
                                                                    T2 step (3): the class (1) and (1b) entries of r4-literals.tsv, byte for byte but for the names
  python -B tools/dll-structure/apply_r4.py literals-gen            write r4-literals.tsv from the tree (run ONCE, at the T0 state: it reads the r3 names)
  python -B tools/dll-structure/apply_r4.py literals-check [--stage before|after] [--root DIR]
                                                                    the survey against a tree: every literal that names a cited type is surveyed (before), or
                                                                    no literal keeps the old name of a cited type unless the entry is class 4 (after)
  Options: --dry-run (move, fix, literals), --root DIR (literals-check, literals-gen).

The order of T2 is: (1) the rename tool (`--rename Old=New`, nine pairs) while everything is still in `Alundra.Scripts`; (2) `fix`; (3) `literals`;
(4) the converter and its tests; (5) the six `design.json` (by hand, proven by prove_r4.py).

Types moved: the 10 types declared in the 10 files of `Alundra/Scripts/` (the 9 cited types, two parts of EntityScriptProxy in two files, and
`MapEvent`, declared in the file of the world proxy). After R4 no type of the DLL is in `Alundra.Scripts`.

Rules of `fix` (section 8, T0 (b)):
  - the `using` lines are computed from the final namespace of the moved types: a file gets `using <ns>;` for every namespace of a moved type it names
    (code without comments or strings, `cref` included), except its own namespace and the namespaces that enclose it; added after the existing `using`
    lines in alphabetical order; `using Alundra.Scripts;` is removed (no type is left in that namespace);
  - a `Alundra.Scripts.<T>` qualification (code and `cref`) is rewritten to the final namespace of the type;
  - the files keep their line endings and their encoding byte for byte (read and written as latin-1), but for the lines changed;
  - no `global using`; the script refuses to run when the tree is not clean outside the paths of the slice (and inside them, unless `--allow-dirty`).

Survey r4-literals.tsv (same columns as r3-literals.tsv; `file` is the r3 path of the file; `line` is the line at the T0 state; `text` is the JSON form
of the literal as it is in the source). An entry stands for every occurrence of `name` (identifier boundaries) in the string literals that start on that
line. `apply` finds a literal by its text, not by its line (the `using` lines of step (2) move the lines): the (file, text, name) of two entries with
different treatments is refused at `literals-gen`. Classes: 1 the name of the class that writes the literal; 1b the name of another type; 4 established
meaning, kept (the prose of a trace harness written in a versioned trace); 5 meaning not established: a stop.
"""
import argparse
import collections
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import apply_r2 as A  # noqa: E402
import r3_literals as L  # noqa: E402

REPO = A.REPO
TSV = os.path.join(HERE, 'r4-literals.tsv')
APPLIED = ('1', '1b')
KEPT_PROSE_FILE = 'Alundra.Tests/HeroTraceHarnessTests.cs'   # class 4: prose of a harness, written in the four versioned hero traces


class R4:
    """What the table says about the 9 cited types and the 10 files."""

    def __init__(self):
        self.tb = A.Table()
        rows = self.tb.rows
        self.rename = {}        # r3 name -> final name, for the 9 cited types
        for r in rows:
            if r['data_cited']:
                if r['r2']['name'] != r['r3']['name']:
                    raise A.R2Error('%s: renamed in R3 although cited by the data' % r['old_name'])
                self.rename[r['r3']['name']] = r['final']['name']
        if len(self.rename) != 9:
            raise A.R2Error('%d cited types in the table, expected 9' % len(self.rename))
        self.moves = {}         # r3 file -> final file, the 10 files
        self.file_ns = {}       # final file -> final namespace
        self.moved_ns = {}      # final name -> final namespace, for every type declared in the 10 files
        self.old_ns = {}        # r3 name -> r3 namespace
        for r in rows:
            if r['r3']['file'] != r['final']['file']:
                if self.moves.setdefault(r['r3']['file'], r['final']['file']) != r['final']['file']:
                    raise A.R2Error('%s: two final files' % r['r3']['file'])
                if self.file_ns.setdefault(r['final']['file'], r['final']['namespace']) != r['final']['namespace']:
                    raise A.R2Error('%s: two final namespaces' % r['final']['file'])
                if self.moved_ns.setdefault(r['final']['name'], r['final']['namespace']) != r['final']['namespace']:
                    raise A.R2Error('%s: two final namespaces' % r['final']['name'])
                if r['r3']['namespace'] != A.SCRIPTS_NS:
                    raise A.R2Error('%s: not in Alundra.Scripts at r3' % r['final']['name'])
            elif r['r3']['namespace'] != r['final']['namespace']:
                raise A.R2Error('%s: namespace changes in R4 without a file move' % r['final']['name'])
        if len(self.moves) != 10:
            raise A.R2Error('%d files move in R4, expected 10' % len(self.moves))
        self.final_to_r3 = {v: k for k, v in self.moves.items()}
        for r in rows:   # a moved name must not be declared elsewhere (the using lines would be ambiguous)
            if r['final']['name'] in self.moved_ns and r['final']['file'] not in self.file_ns:
                raise A.R2Error('%s is declared in %s too' % (r['final']['name'], r['final']['file']))
        # old names (r3) and final names of every moved type, for the qualification rewrite
        self.name_ns = dict(self.moved_ns)
        for old in self.rename:
            self.name_ns[old] = self.moved_ns[self.rename[old]]
        self.old_names = set(self.rename)                   # the 9 r3 names
        self.all_old = self.old_names                       # names a literal may carry

    def canon(self, path):
        """The r3 path of a file, given its current path."""
        return self.final_to_r3.get(path, path)

    def current(self, root, canon):
        for p in dict.fromkeys([self.moves.get(canon, canon), canon]):
            if os.path.isfile(os.path.join(root, p)):
                return p
        return None


# ---------------------------------------------------------------------------------------------------- the survey
def name_rx(names):
    return re.compile(r'(?<![A-Za-z0-9_])(' + '|'.join(map(re.escape, sorted(names, key=len, reverse=True))) + r')(?![A-Za-z0-9_])')


def survey(root, r4):
    """[(canonical file, line, name, enclosing top-level type, literal text)] for every literal naming a cited r3 name; plus the files that
    carry `Alundra.Scripts` in a literal (reported by the caller)."""
    rx = name_rx(r4.all_old)
    out, scripts = [], []
    for p in L.tree_files(root):
        bom, body = L.read_body(root, p)
        src, spans = L.string_spans(body)
        ext = None
        for line, a, b in spans:
            t = body[a:b]
            if 'Alundra.Scripts' in t:
                scripts.append((r4.canon(p), line, t))
            names = sorted(set(rx.findall(t)))
            if not names:
                continue
            if ext is None:
                ext = A.top_level_extents(src)
            enc = [n for n, (x, y) in ext.items() if x <= a < y]
            for n in names:
                out.append((r4.canon(p), line, n, enc[0] if enc else '', t))
    return out, scripts


def classify(r4, rec):
    f, line, name, enc, text = rec
    if f == KEPT_PROSE_FILE and enc == 'HeroTraceHarnessTests':
        return ('4', 'prose of a harness, written in the four versioned hero traces (docs/hero-trace-389-*.txt): kept')
    if f.startswith('Alundra/'):
        return ('1' if name == enc else '1b', '' if name == enc else 'names a type other than the one that writes it (%s)' % enc)
    if f.startswith('Alundra.Tests/'):
        return ('1b', 'comment-like text of a test harness, not written in a versioned trace; follows the type')
    return None


def build_rows(root, r4):
    recs, scripts = survey(root, r4)
    rows, todo = [], []
    for rec in recs:
        c = classify(r4, rec)
        if c is None:
            todo.append(rec)
            continue
        f, line, name, enc, text = rec
        rows.append([f, str(line), name, r4.rename[name] if c[0] in APPLIED else name, c[0], '', json.dumps(text), c[1]])
    return rows, todo, scripts


def ambiguity(rows):
    """(file, text, name) with two different treatments: the apply, which finds a literal by its text, would be ambiguous."""
    seen, bad = {}, []
    for r in rows:
        k = (r[0], r[6], r[2])
        v = (r[3], r[4])
        if seen.setdefault(k, v) != v:
            bad.append('%s:%s %s: the same literal text has two treatments' % (r[0], r[1], r[2]))
    return bad


def check(r4, root, stage):
    entries = L.load_tsv(TSV)
    by = L.entries_by_key(entries)
    problems = []
    for e in entries:
        if e['class'] not in ('1', '1b', '4'):
            problems.append('%s:%d %s: class %s is not decided' % (e['file'], e['line'], e['name'], e['class']))
        elif e['class'] in APPLIED and e['new_name'] != r4.rename.get(e['name']):
            problems.append('%s:%d %s: new name %s, the table says %s' % (e['file'], e['line'], e['name'], e['new_name'], r4.rename.get(e['name'])))
        elif e['class'] == '4' and e['new_name'] != e['name']:
            problems.append('%s:%d %s: a kept entry changes the name' % (e['file'], e['line'], e['name']))
    recs, scripts = survey(root, r4)
    for f, line, t in scripts:
        problems.append('%s:%d: a literal carries `Alundra.Scripts` (%s)' % (f, line, t[:80]))
    if stage == 'before':
        seen = set()
        for f, line, name, enc, text in recs:
            e = by.get((f, line, name))
            if e is None:
                problems.append('%s:%d: the literal names %s and is not in the survey' % (f, line, name))
                continue
            seen.add((f, line, name))
            if e['text'] != text:
                problems.append('%s:%d: the literal differs from the survey (%s)' % (f, line, name))
        for k, e in by.items():
            if k not in seen:
                problems.append('%s:%d: surveyed literal (%s) not found' % (k[0], k[1], k[2]))
    else:
        for f, line, name, enc, text in recs:
            kept = [e for e in entries if e['file'] == f and e['name'] == name and e['text'] == text and e['class'] == '4']
            if not kept:
                problems.append('%s:%d: the literal still names %s (renamed %s in R4)' % (f, line, name, r4.rename[name]))
        rx = name_rx(r4.all_old)
        have = collections.Counter()
        for p in L.tree_files(root):
            bom, body = L.read_body(root, p)
            for line, a, b in L.string_spans(body)[1]:
                have[(r4.canon(p), body[a:b])] += 1
        need = collections.Counter()
        for e in entries:
            if e['class'] in APPLIED:
                need[(e['file'], rx.sub(lambda m: r4.rename[m.group(1)], e['text']))] += 1
        for k, n in need.items():
            if have[k] < n:
                problems.append('%s: %d literal(s) expected after the rename, %d found: %s' % (k[0], n, have[k], k[1][:70]))
    return problems


def apply_literals(r4, root, dry):
    entries = [e for e in L.load_tsv(TSV) if e['class'] in APPLIED]
    rx = name_rx(r4.all_old)
    byfile = collections.defaultdict(list)
    for e in entries:
        byfile[e['file']].append(e)
    n_changed = n_lits = 0
    for f, es in sorted(byfile.items()):
        path = r4.current(root, f)
        if path is None:
            raise A.R2Error('%s: no such file (nor its final path)' % f)
        bom, body = L.read_body(root, path)
        spans = L.string_spans(body)[1]
        want = collections.Counter()
        for e in es:
            want[e['text']] += 1
        # one entry per (line, name): an entry stands for every literal of that text that carries the name
        need = collections.Counter((e['text'], e['name']) for e in es)
        edits = []
        got = collections.Counter()
        for line, a, b in spans:
            t = body[a:b]
            names = set(rx.findall(t))
            for n in names:
                if (t, n) in need:
                    got[(t, n)] += 1
                    edits.append((a, b, t))
        for k, n in need.items():
            if got[k] != n:
                raise A.R2Error('%s: %d literal(s) expected with the text %s, %d found' % (f, n, k[0][:60], got[k]))
        out, pos = [], 0
        for a, b, t in sorted(set(edits)):
            out.append(body[pos:a])
            out.append(rx.sub(lambda m: r4.rename[m.group(1)], t))
            pos = b
            n_lits += 1
        out.append(body[pos:])
        if not dry:
            with open(os.path.join(root, path), 'wb') as fh:
                fh.write((bom + ''.join(out)).encode('latin-1'))
        n_changed += 1
    print('literals: %d literal(s) rewritten in %d file(s)%s' % (n_lits, n_changed, ' (dry run)' if dry else ''))
    return 0


# ---------------------------------------------------------------------------------------------------- move and fix
def do_move(r4, dry):
    for o, n in sorted(r4.moves.items()):
        if not os.path.isfile(os.path.join(REPO, o)):
            raise A.R2Error('source file missing: %s' % o)
        if os.path.exists(os.path.join(REPO, n)):
            raise A.R2Error('target already exists: %s' % n)
    for o, n in sorted(r4.moves.items()):
        print('git mv %s -> %s' % (o, n))
        if not dry:
            os.makedirs(os.path.dirname(os.path.join(REPO, n)), exist_ok=True)
            A.git('mv', o, n)
    print('%d file(s) moved' % len(r4.moves))
    return 0


def extension_namespaces(r4, files, cur):
    """{method name: final namespace} of the extension methods declared in the 10 files."""
    out = {}
    for p in files:
        if p not in r4.file_ns:
            continue
        body = A.split_bom(cur(p))[1]
        if 'this ' not in body:
            continue
        src = A.Src(body)
        ext = A.top_level_extents(src)
        for m in A.EXT_RE.finditer(src.masked):
            if [n for n, (a, b) in ext.items() if a <= m.start() < b]:
                out[m.group(1)] = r4.file_ns[p]
    return out


def do_fix(r4, dry):
    files = A.source_files()
    fileset = set(files)
    for o, n in r4.moves.items():
        if o in fileset or n not in fileset:
            raise A.R2Error('T1 is not complete: %s should be at %s' % (o, n))
    written = collections.OrderedDict()

    def cur(path):
        return written[path] if path in written else A.read(path)

    for n, ns in sorted(r4.file_ns.items()):
        t = cur(n)
        t2 = A.set_namespace(t, ns)
        if t2 != t:
            written[n] = t2
    ext_ns = extension_namespaces(r4, files, cur)
    for p in files:
        t = cur(p)
        t2 = A.fix_usings(p, t, None, None, r4.name_ns, ext_ns, False)
        if t2 != t:
            written[p] = t2
    changed = 0
    for p, t in written.items():
        if not dry:
            if A.read(p) == t:
                continue
            A.write(p, t)
        changed += 1
    print('fix: %d file(s) written%s' % (changed, ' (dry run)' if dry else ''))
    return 0


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('cmd', choices=('move', 'fix', 'literals', 'literals-gen', 'literals-check'))
    ap.add_argument('--stage', choices=('before', 'after'), default='before')
    ap.add_argument('--root', default=REPO)
    ap.add_argument('--dry-run', action='store_true')
    ap.add_argument('--allow-dirty', action='store_true')
    a = ap.parse_args()
    sys.stdout.reconfigure(encoding='utf-8')
    try:
        r4 = R4()
        if a.cmd == 'literals-gen':
            rows, todo, scripts = build_rows(a.root, r4)
            for rec in todo:
                print('TO DECIDE: %s:%d [%s] %s | %s' % (rec[0], rec[1], rec[3], rec[2], rec[4][:140]))
                rows.append([rec[0], str(rec[1]), rec[2], rec[2], '5', '', json.dumps(rec[4]), 'to decide'])
            for f, line, t in scripts:
                print('ALUNDRA.SCRIPTS IN A LITERAL: %s:%d %s' % (f, line, t[:100]))
            for msg in ambiguity(rows):
                print('AMBIGUOUS: ' + msg)
            L.write_tsv(rows, TSV)
            by = collections.Counter(r[4] for r in rows)
            print('%d entries written (%s)' % (len(rows), ', '.join('class %s: %d' % kv for kv in sorted(by.items()))))
            return 0 if not todo and not scripts and not ambiguity(rows) else 1
        if a.cmd == 'literals-check':
            pr = check(r4, a.root, a.stage)
            for p in pr:
                print(p)
            print('apply_r4 literals-check (%s): %s' % (a.stage, 'OK' if not pr else '%d problem(s)' % len(pr)))
            return 0 if not pr else 1
        if a.root != REPO:
            ap.error('--root only for literals-gen and literals-check')
        A.check_tree_clean(a.allow_dirty)
        if a.cmd == 'move':
            return do_move(r4, a.dry_run)
        if a.cmd == 'fix':
            return do_fix(r4, a.dry_run)
        return apply_literals(r4, REPO, a.dry_run)
    except A.R2Error as e:
        print('ERROR: %s' % e)
        return 2


if __name__ == '__main__':
    sys.exit(main())
