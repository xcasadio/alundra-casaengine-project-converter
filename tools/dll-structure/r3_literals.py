"""R3 survey of the string literals that carry a type name (docs/plan-restructuration-dll.md, section 7, T0 (b)). Python 3, standard library only.

  python -B tools/dll-structure/r3_literals.py gen                 write r3-literals.tsv from the tree (run ONCE, at the T0 state: it reads the r2 names)
  python -B tools/dll-structure/r3_literals.py check [--stage before|after] [--root DIR]
                                                                    the survey against a tree: every literal that carries an old name of the
                                                                    table is surveyed (before), or no literal keeps the old name of a type
                                                                    renamed in R3 unless it is a class (2) or (4) entry (after)
  python -B tools/dll-structure/r3_literals.py apply <Domain> [--root DIR] [--dry-run]
                                                                    rewrite the class (1), (1b) and (3) entries of the domain, byte for byte
                                                                    but for the names

Survey (r3-literals.tsv, one line per file, line and name; the columns are tab separated, `text` is the JSON form of the literal as it is in the
source). `file` is the `r2` path of the file (the stage the survey was taken at); `line` is the line where the literal starts (R3 changes no
line break). An entry stands for every occurrence of `name` (at identifier boundaries) in the string literals that start on that line.

Classes (section 7, T0 (b)):
  1   the name of the class that writes the literal (log prefix or message that names it); renamed with it, and the tests that read it with it
  1b  the name of another type of the table (renamed with the type named, at step (2) of the domain of that type); unchanged in R3 when that
      type is one of the 9 cited by the data (it changes in R4 with it)
  2   a data key, an asset name, the name of an exported datum: never changed
  3   the name of a source file renamed by R3 (a path or a file name): rewritten at step (2) of the domain that renames the file
  4   established meaning, kept (the name of a xunit collection, the prose of a trace harness, the name of a command-line tool): unchanged
  5   meaning not established: a stop
An entry whose type is not renamed in R3 (a cited type, or a type whose name does not change) has `new_name` equal to `name`.
Every literal of a string kind is read: regular, verbatim `@"..."`, raw `\"\"\"...\"\"\"` and the text pieces of interpolated strings (the holes are code).
"""
import argparse
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import apply_r2 as A  # noqa: E402

TSV = os.path.join(HERE, 'r3-literals.tsv')
COLS = ['file', 'line', 'name', 'new_name', 'class', 'domain', 'text', 'note']
APPLIED_CLASSES = ('1', '1b', '3')
ROOTS = ('Alundra', 'Alundra.Tests', 'tools/AlundraTestSaves')


class Info:
    """What the table says about names and files."""

    def __init__(self):
        self.tb = A.Table()
        rows = self.tb.rows
        # a literal that carries only the start of a type name, read by a test as a log prefix: old fragment -> (r3 fragment, domain)
        self.partial = {'AlundraEffect': ('Effect', 'Rendering')}
        self.all_old = sorted({r['old_name'] for r in rows} | set(self.partial), key=len, reverse=True)
        self.renamed = {}      # old name -> r3 name, for the types renamed in R3
        self.cited = set()
        self.domain_of_name = {}
        for r in rows:
            if r['data_cited']:
                self.cited.add(r['old_name'])
            if r['r2']['name'] != r['r3']['name']:
                self.renamed[r['old_name']] = r['r3']['name']
            d = A.Table.domain(r['r2']['file'])
            if d is None:
                d = A.Table.domain(r['final']['file'])
            self.domain_of_name[r['old_name']] = d
        self.r2_to_r3 = {}     # r2 path -> r3 path, for the files that change name
        self.r3_to_r2 = {}
        for m in self.tb.rows:   # every type row: the file of a split type is renamed like the file of a main type
            if m['r2']['file'] != m['r3']['file']:
                self.r2_to_r3[m['r2']['file']] = m['r3']['file']
                self.r3_to_r2[m['r3']['file']] = m['r2']['file']
        self.name_rx = re.compile(r'(?<![A-Za-z0-9_])(' + '|'.join(map(re.escape, self.all_old)) + r')(?![A-Za-z0-9_])')
        # file name (without extension) of an r2 file -> (r3 name without extension, domain of the move)
        self.file_rename = {}
        for o, n in self.r2_to_r3.items():
            self.file_rename[os.path.basename(o)[:-3]] = (os.path.basename(n)[:-3], A.Table.domain(n))

    def canon(self, path):
        """The r2 path of a file, given its current path."""
        return self.r3_to_r2.get(path, path)


def read_body(root, path):
    with open(os.path.join(root, path), 'rb') as f:
        t = f.read().decode('latin-1')
    return A.split_bom(t)


def tree_files(root):
    out = []
    for r in ROOTS:
        base = os.path.join(root, r)
        for dp, dn, fn in os.walk(base):
            dn[:] = [d for d in dn if d not in ('bin', 'obj', 'artifacts')]
            for f in fn:
                if f.endswith('.cs'):
                    out.append(os.path.relpath(os.path.join(dp, f), root).replace(os.sep, '/'))
    return sorted(out)


def string_spans(body):
    """[(line, start, end)] of the string literals (not the characters), in order."""
    src = A.Src(body)
    out = []
    for a, b, k in src.spans:
        if k == 's' and not body.startswith("'", a):
            out.append((body.count('\n', 0, a) + 1, a, b))
    return src, out


def survey(root, info):
    """[(canonical file, line, name, enclosing top-level type, literal text)] for every literal naming a name of the table."""
    out = []
    for p in tree_files(root):
        bom, body = read_body(root, p)
        src, spans = string_spans(body)
        ext = None
        for line, a, b in spans:
            t = body[a:b]
            names = sorted(set(info.name_rx.findall(t)))
            if not names:
                continue
            if ext is None:
                ext = A.top_level_extents(src)
            enc = [n for n, (x, y) in ext.items() if x <= a < y]
            for n in names:
                out.append((info.canon(p), line, n, enc[0] if enc else '', t))
    return out


def classify(info, rec, dll_prefix_owners):
    """Auto classification (class, domain, note) of one (file, line, name, enclosing, text) record, or None when to be decided by hand."""
    f, line, name, enc, text = rec
    new = info.renamed.get(name, name)
    in_dll = f.startswith('Alundra/')
    is_path = re.search(r'(?<![A-Za-z0-9_])' + re.escape(name) + r'\.cs\b', text) is not None
    if is_path:
        fr = info.file_rename.get(name)
        if fr is None:
            return ('3', '', 'a file name, but the file is not renamed by R3') if name not in info.renamed else None
        return ('3', fr[1], '')
    if name not in info.renamed:
        if name in info.cited:
            return ('1' if name == enc else '1b', '', 'type cited by the data: unchanged in R3, changes in R4 with the type')
        return ('1' if name == enc else '1b', '', 'type not renamed')
    dom = info.domain_of_name[name]
    if in_dll:
        if name == enc:
            return ('1', dom, '')
        return ('1b', dom, 'names a type other than the one that writes it (%s)' % enc)
    # tests and tools
    if f.startswith('tools/'):
        return ('4', '', 'name of the command-line tool (its folder keeps the name), kept')
    if enc.endswith('Collection') and re.fullmatch(r'"%s (singleton|static state)"' % re.escape(name), text):
        return ('4', '', 'name of a xunit collection, kept')
    if enc in ('HeroTraceHarnessTests', 'HeadlessIntroSimulation'):
        return ('4', '', 'prose of a trace harness, kept')
    if text == '"%s"' % name and name in dll_prefix_owners:
        return ('1', dom, 'log prefix written by %s, read by this test' % name)
    return None


def build_rows(info, root):
    recs = survey(root, info)
    owners = {r[2] for r in recs if r[0].startswith('Alundra/') and r[2] == r[3]}
    rows, todo = [], []
    for rec in recs:
        c = classify(info, rec, owners)
        if c is None:
            todo.append(rec)
            continue
        f, line, name, enc, text = rec
        new = info.renamed.get(name, name)
        if c[0] == '3':
            fr = info.file_rename.get(name)
            if fr:
                new = fr[0]
        rows.append([f, str(line), name, new, c[0], c[1], json.dumps(text), c[2]])
    return rows, todo


def write_tsv(rows, path=TSV):
    rows = sorted(rows, key=lambda r: (r[0], int(r[1]), r[2]))
    with open(path, 'w', encoding='utf-8', newline='\n') as f:
        f.write('\t'.join(COLS) + '\n')
        for r in rows:
            f.write('\t'.join(r) + '\n')


def load_tsv(path=TSV):
    out = []
    with open(path, encoding='utf-8') as f:
        head = f.readline().rstrip('\n').split('\t')
        if head != COLS:
            raise A.R2Error('%s: unexpected columns %s' % (path, head))
        for ln in f:
            cells = ln.rstrip('\n').split('\t')
            if len(cells) != len(COLS):
                raise A.R2Error('%s: a line with %d columns' % (path, len(cells)))
            d = dict(zip(COLS, cells))
            d['line'] = int(d['line'])
            d['text'] = json.loads(d['text'])
            out.append(d)
    return out


def entries_by_key(entries):
    return {(e['file'], e['line'], e['name']): e for e in entries}


def check(info, root, stage):
    entries = load_tsv()
    by = entries_by_key(entries)
    problems = []
    for e in entries:
        if e['class'] not in ('1', '1b', '2', '3', '4'):
            problems.append('%s:%d %s: class %s is not decided' % (e['file'], e['line'], e['name'], e['class']))
    recs = survey(root, info) if stage == 'before' else None
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
        # after R3: a literal keeps no old name of a type renamed in R3 unless the survey keeps it (class 2 or 4)
        for f, line, name, enc, text in survey(root, info):
            e = by.get((f, line, name))
            if (name in info.renamed or name in info.partial) and (e is None or e['class'] not in ('2', '4')):
                problems.append('%s:%d: the literal still names %s (renamed to %s in R3)' % (f, line, name, info.renamed[name] if name in info.renamed else info.partial[name][0]))
        for e in entries:
            if e['class'] in APPLIED_CLASSES and e['new_name'] != e['name']:
                p = e['file']
                p = info.r2_to_r3.get(p, p)
                if not os.path.isfile(os.path.join(root, p)):
                    problems.append('%s: file of the entry missing' % p)
                    continue
                bom, body = read_body(root, p)
                src, spans = string_spans(body)
                lits = [body[a:b] for l, a, b in spans if l == e['line']]
                rx = re.compile(r'(?<![A-Za-z0-9_])' + re.escape(e['new_name']) + r'(?![A-Za-z0-9_])')
                if not any(rx.search(t) for t in lits):
                    problems.append('%s:%d: the entry (%s -> %s) is not applied' % (p, e['line'], e['name'], e['new_name']))
    return problems


def apply_domain(info, root, domain, dry):
    entries = [e for e in load_tsv() if e['class'] in APPLIED_CLASSES and e['domain'] == domain and e['new_name'] != e['name']]
    byfile = {}
    for e in entries:
        byfile.setdefault(e['file'], []).append(e)
    n_changed = n_entries = 0
    for f, es in sorted(byfile.items()):
        # the file is at its r3 path (the domain's move is done) or still at its r2 path; the two names of Events swap, so the right one is the
        # candidate where a literal starting on the line of an entry names the type
        found = None
        for path in dict.fromkeys([info.r2_to_r3.get(f, f), f]):
            if not os.path.isfile(os.path.join(root, path)):
                continue
            bom, body = read_body(root, path)
            src, spans = string_spans(body)
            if all(any(l == e['line'] and re.search(r'(?<![A-Za-z0-9_])' + re.escape(e['name']) + r'(?![A-Za-z0-9_])', body[a:b]) for l, a, b in spans) for e in es):
                found = (path, bom, body, spans)
                break
        if found is None:
            raise A.R2Error('%s: no file (%s or its r3 name) holds the literals of the survey' % (f, f))
        path, bom, body, spans = found
        edits = []
        for e in es:
            rx = re.compile(r'(?<![A-Za-z0-9_])' + re.escape(e['name']) + r'(?![A-Za-z0-9_])')
            hit = 0
            for line, a, b in spans:
                if line != e['line']:
                    continue
                for m in rx.finditer(body, a, b):
                    edits.append((m.start(), m.end(), e['new_name']))
                    hit += 1
            if hit == 0:
                raise A.R2Error('%s:%d: no occurrence of %s in a literal that starts on that line' % (path, e['line'], e['name']))
            n_entries += 1
        out, pos = [], 0
        for a, b, new in sorted(edits):
            out.append(body[pos:a])
            out.append(new)
            pos = b
        out.append(body[pos:])
        if not dry:
            with open(os.path.join(root, path), 'wb') as fh:
                fh.write((bom + ''.join(out)).encode('latin-1'))
        n_changed += 1
    print('%s: %d entr%s applied in %d file(s)%s' % (domain, n_entries, 'y' if n_entries == 1 else 'ies', n_changed, ' (dry run)' if dry else ''))
    return 0


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('cmd', choices=('gen', 'check', 'apply'))
    ap.add_argument('domain', nargs='?')
    ap.add_argument('--stage', choices=('before', 'after'), default='before')
    ap.add_argument('--root', default=A.REPO)
    ap.add_argument('--dry-run', action='store_true')
    a = ap.parse_args()
    sys.stdout.reconfigure(encoding='utf-8')
    info = Info()
    try:
        if a.cmd == 'gen':
            rows, todo = build_rows(info, a.root)
            for rec in todo:
                print('TO DECIDE: %s:%d [%s] %s | %s' % (rec[0], rec[1], rec[3], rec[2], rec[4][:140]))
                rows.append([rec[0], str(rec[1]), rec[2], info.renamed.get(rec[2], rec[2]), '5', '', json.dumps(rec[4]), 'to decide'])
            write_tsv(rows)
            print('%d entries written (%d to decide)' % (len(rows), len(todo)))
            return 0
        if a.cmd == 'check':
            pr = check(info, a.root, a.stage)
            for p in pr:
                print(p)
            print('r3_literals check (%s): %s' % (a.stage, 'OK' if not pr else '%d problem(s)' % len(pr)))
            return 0 if not pr else 1
        if not a.domain:
            ap.error('apply needs a domain')
        return apply_domain(info, a.root, a.domain, a.dry_run)
    except A.R2Error as e:
        print('ERROR: %s' % e)
        return 2


if __name__ == '__main__':
    sys.exit(main())
