"""usage: run_mutants.py ID [ID ...]  -- applies each mutant of mutants.py by script, runs the targeted suite in Release (neutralised copy), undoes it, checks git diff is empty, writes t3/<ID>.txt"""
import subprocess, sys, re, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from mutants import M, PM
R = 'D:/development/repo/alundra-casaengine-project-converter'
SK = 'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/e20e0a3b-exec/skipcopy.targets'
OUT = R + '/docs/plan-e20-annexe/e20-h4d2/t3'
os.makedirs(OUT, exist_ok=True)
BASE = ['FireTome1', 'EarthTome1', 'TomeWords', 'NativeSpriteRegistryTests', 'LiftableAiRegistrationTests', 'ChargedArrowAiTests', 'WaveSwordLifeTests', 'BallFlamesTests']
WIDE = BASE + ['HeroItemUse', 'Tome']


def diff_clean():
    return subprocess.run(['git', '-C', R, 'diff', '--quiet', '--', 'Alundra', 'Alundra.Tests'], capture_output=True).returncode == 0


pass
for mid in sys.argv[1:]:
    fam, desc, edits = M[mid]
    saved = {}
    for path, old, new in edits:
        full = R + '/' + path
        if path not in saved:
            saved[path] = open(full, encoding='utf-8', newline='').read()
        s = open(full, encoding='utf-8', newline='').read()
        nl = '\r\n' if '\r\n' in s else '\n'
        o = old.replace('\n', nl)
        n = new.replace('\n', nl)
        assert s.count(o) == 1, (mid, path, s.count(o), old[:50])
        open(full, 'w', encoding='utf-8', newline='').write(s.replace(o, n))
    wide = any(p == PM for p, _, _ in edits)
    flt = '|'.join('FullyQualifiedName~' + x for x in (WIDE if wide else BASE))
    try:
        out = subprocess.run(['dotnet', 'test', R + '/Alundra.Tests/Alundra.Tests.csproj', '-c', 'Release', '-p:CustomAfterMicrosoftCommonTargets=' + SK, '--filter', flt, '--blame-hang-timeout', '60s'],
                             capture_output=True, text=True, encoding='utf-8', errors='replace').stdout
    finally:
        for path, s in saved.items():
            open(R + '/' + path, 'w', encoding='utf-8', newline='').write(s)
    clean = diff_clean()
    failed = sorted(set(re.findall(r'Échoué (Alundra\.Tests[^\[\n]*?)\s*\[', out)))
    builderr = sorted(set(re.findall(r'error CS\d+[^\n]*', out)))[:3]
    summ = [l.strip() for l in out.splitlines() if 'Échoué!' in l or 'Réussi!' in l]
    verdict = 'BUILD ERROR' if builderr else ('KILLED' if failed else 'SURVIVES')
    lines = ['%s [%s] %s' % (mid, fam, desc), 'verdict: %s (%d failing tests)' % (verdict, len(failed)), 'git diff empty after: %s' % clean, 'filter: %s' % ('wide' if wide else 'base')]
    lines += ['summary: ' + x for x in summ[:2]] + builderr + ['  ' + f.replace('Alundra.Tests.', '', 1) for f in failed]
    open(OUT + '/' + mid + '.txt', 'w', encoding='utf-8').write('\n'.join(lines) + '\n')
    print(lines[0]); print(lines[1], '|', lines[2]);
    if not clean:
        print('TREE NOT CLEAN, stopping'); sys.exit(2)
