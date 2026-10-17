"""Proof that check_names.py detects what it must (docs/plan-restructuration-dll.md, section 5, T1).

  python -B tools/dll-structure/selftest.py <work folder outside the repository>

Builds from table.json synthetic trees (one file per path of stage r2, with the namespace and minimal declarations of its types) in
<work folder>/case-*, runs `check_names.py --tree --stage r2 --root <case>` on them (and `--table` on a modified copy of the table) and
checks the exit code and the finding reported:
  (a) conforming tree         -> 0, no finding
  (b) a moved file keeps `namespace Alundra.Scripts;`        -> 1, [namespace]
  (c) a type carries its r3 name at stage r2                 -> 1, [nom]
  (d) an extra .cs file                                      -> 1, [hors table]
  (e) a missing file                                         -> 1, [absent]
  (f) --table, a final name changed to Vector2               -> 1, [collision]
Writes only inside the work folder. Exit 0 when the six cases behave as expected, 1 otherwise.
"""
import json
import os
import shutil
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, '..', '..'))
CHECK = os.path.join(HERE, 'check_names.py')
TABLE = os.path.join(HERE, 'table.json')
STAGE = 'r2'


def decl(kind, access, partial, name):
    p = 'partial ' if partial else ''
    if kind == 'delegate':
        return '%s delegate void %s();' % (access, name)
    if kind in ('record', 'record class', 'record struct'):
        return '%s %s%s %s();' % (access, p, kind, name)
    if kind == 'enum':
        return '%s enum %s { A }' % (access, name)
    return '%s %s%s %s { }' % (access, p, kind, name)


def build_tree(table, root, mutate=None):
    """Writes the conforming stage-r2 tree under root; returns {path: content}."""
    files = {}
    for r in table['types']:
        files.setdefault(r[STAGE]['file'], []).append(r)
    content = {}
    for path, rs in files.items():
        nss = {r[STAGE]['namespace'] for r in rs}
        assert len(nss) == 1, path
        body = ['namespace %s;' % nss.pop(), '']
        for r in rs:
            body.append(decl(r['kind'], r['access'], r['partial'], r[STAGE]['name']))
        content[path] = '\n'.join(body) + '\n'
    for r in table['root_files']:
        if r['types']:
            content[r[STAGE]] = 'namespace %s;\n\npublic class %s { }\n' % (r['namespace'], r['types'][0])
        else:
            content[r[STAGE]] = '// assembly attributes only\n'
    return content, files


def write_tree(root, content):
    for path, text in content.items():
        full = os.path.join(root, path)
        os.makedirs(os.path.dirname(full), exist_ok=True)
        with open(full, 'w', encoding='utf-8', newline='\n') as f:
            f.write(text)


def run(args):
    p = subprocess.run([sys.executable, '-B', CHECK] + args, capture_output=True, text=True, encoding='utf-8')
    return p.returncode, p.stdout + p.stderr


def main():
    sys.stdout.reconfigure(encoding='utf-8')
    if len(sys.argv) != 2:
        print(__doc__)
        return 2
    work = os.path.abspath(sys.argv[1])
    try:
        inside = os.path.commonpath([work, REPO]) == REPO
    except ValueError:  # another drive
        inside = False
    if inside:
        print('ERROR: the work folder must be outside the repository: ' + work)
        return 2
    os.makedirs(work, exist_ok=True)
    table = json.load(open(TABLE, encoding='utf-8'))
    content, files = build_tree(table, None)
    # single-type files, not data-cited, whose type is renamed at r3 (for cases b, c, e)
    single = sorted(p for p, rs in files.items() if len(rs) == 1 and rs[0]['kind'] in ('class', 'struct', 'interface') and not rs[0]['data_cited'] and rs[0]['r3']['name'] != rs[0][STAGE]['name']
                    and rs[0][STAGE]['namespace'] != 'Alundra.Scripts')
    victim = single[0]
    vrow = files[victim][0]
    results = []

    def case(label, mutate, args_extra, want_tag, want_count):
        d = os.path.join(work, 'case-' + label)
        if os.path.isdir(d):
            shutil.rmtree(d)
        c = dict(content)
        mutate(c)
        write_tree(d, c)
        code, out = run(['--tree', '--stage', STAGE, '--root', d])
        findings = [l for l in out.splitlines() if l.startswith('[') and not l.startswith('[avertissement]')]
        ok = (code == (0 if want_tag is None else 1)) and (want_tag is None and not findings or want_tag is not None and any(l.startswith(want_tag) for l in findings))
        if want_count is not None:
            ok = ok and len(findings) == want_count
        results.append((label, ok, code, findings[:3], len(findings)))

    def b(c):
        c[victim] = c[victim].replace('namespace %s;' % vrow[STAGE]['namespace'], 'namespace Alundra.Scripts;')

    def cc(c):
        c[victim] = c[victim].replace(' %s ' % vrow[STAGE]['name'], ' %s ' % vrow['r3']['name'])

    def d(c):
        c['Alundra/Stray/Orphan.cs'] = 'namespace Alundra.Stray;\n\npublic class Orphan { }\n'

    def e(c):
        del c[victim]

    case('a-conforming', lambda c: None, [], None, 0)
    case('b-namespace', b, [], '[namespace]', 1)
    case('c-name', cc, [], '[nom]', 1)
    case('d-extra-file', d, [], '[hors table]', 1)
    case('e-missing-file', e, [], '[absent]', 2)

    # (f) --table on a copy of the table where one final name is Vector2
    t2 = json.loads(json.dumps(table))
    target = t2['types'][0]
    target['final']['name'] = 'Vector2'
    fpath = os.path.join(work, 'table-vector2.json')
    json.dump(t2, open(fpath, 'w', encoding='utf-8'), indent=1)
    code, out = run(['--table', '--table-file', fpath])
    findings = [l for l in out.splitlines() if l.startswith('[') and not l.startswith('[avertissement]')]
    results.append(('f-table-collision', code == 1 and any(l.startswith('[collision]') for l in findings), code, findings[:3], len(findings)))
    # the unmodified table must be green
    code0, out0 = run(['--table'])
    results.append(('g-table-clean', code0 == 0, code0, [], 0))

    allok = True
    for label, ok, code, f, n in results:
        allok &= ok
        print('%-20s %s exit=%d findings=%d %s' % (label, 'OK  ' if ok else 'FAIL', code, n, ' | '.join(f)))
    print('selftest', 'PASSED' if allok else 'FAILED', '(victim file: %s)' % victim)
    return 0 if allok else 1


if __name__ == '__main__':
    sys.exit(main())
