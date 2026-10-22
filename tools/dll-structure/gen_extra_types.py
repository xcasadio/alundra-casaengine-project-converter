"""Generates rules-extra-types.tsv: the public top-level types of the NuGet packages that Alundra.Tests compiles against and that
`scan_types.PACKAGES` / `PACKAGES_WITHOUT_XML` do not cover (xunit.assert, System.Text.Json, YarnSpinner.Compiler, ...), read from the TypeDef
table of their compile-time assemblies (R7, rule (4) for the tests).

  python -B tools/dll-structure/gen_extra_types.py <work folder outside the repository>

Needs `Alundra.Tests/obj/project.assets.json` (a restored test project), `pwsh`, and the NuGet cache. Rewrites rules-extra-types.tsv (one row:
`<package> <version>` TAB namespace TAB name, generic arity stripped, sorted). Run it again when a package of the test project changes.
"""
import json
import os
import re
import subprocess
import sys

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import scan_types  # noqa: E402

sys.stdout.reconfigure(encoding='utf-8')


def main():
    if len(sys.argv) != 2:
        print(__doc__)
        return 2
    work = sys.argv[1]
    os.makedirs(work, exist_ok=True)
    assets = os.path.join(str(scan_types.REPO), 'Alundra.Tests', 'obj', 'project.assets.json')
    if not os.path.isfile(assets):
        print('ERROR: %s not found (restore Alundra.Tests first)' % assets)
        return 2
    a = json.load(open(assets, encoding='utf-8'))
    target = sorted(a['targets'])[0]
    covered = {v.split('/')[0] for v in scan_types.PACKAGES.values()} | {v.split('/')[0] for v in scan_types.PACKAGES_WITHOUT_XML.values()}
    rows = set()
    n_pk = 0
    for lib, v in sorted(a['targets'][target].items()):
        if v.get('type') != 'package':
            continue
        pid, ver = lib.split('/')
        if pid.lower() in covered:
            continue
        comp = [c for c in v.get('compile', {}) if c.endswith('.dll')]
        if not comp:
            continue
        n_pk += 1
        for c in comp:
            dll = os.path.join(str(scan_types.NUGET), pid.lower(), ver, c.replace('/', os.sep))
            out = os.path.join(work, 'dump-%s.tsv' % pid)
            r = subprocess.run(['pwsh', '-NoProfile', '-File', os.path.join(HERE, 'dump_public_types.ps1'), '-Dir', os.path.dirname(dll), '-Out', out],
                               capture_output=True)
            if r.returncode != 0 or not os.path.isfile(out):
                print('ERROR: dump of %s failed: %s' % (dll, r.stderr.decode('utf-8', 'replace')))
                return 2
            for line in open(out, encoding='utf-8'):
                asm, ns, name = line.rstrip('\n').split('\t')
                if asm == os.path.basename(dll):
                    rows.add('%s %s\t%s\t%s' % (pid, ver, ns, re.sub(r'`\d+', '', name)))
    with open(os.path.join(HERE, 'rules-extra-types.tsv'), 'w', encoding='utf-8', newline='\n') as f:
        for r in sorted(rows):
            f.write(r + '\n')
    print('%d packages, %d rows written to rules-extra-types.tsv' % (n_pk, len(rows)))
    return 0


if __name__ == '__main__':
    sys.exit(main())
