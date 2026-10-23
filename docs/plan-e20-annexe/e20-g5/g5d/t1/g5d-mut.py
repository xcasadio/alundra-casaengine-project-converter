import subprocess, sys, re
R = 'D:/development/repo/alundra-casaengine-project-converter/'
SK = 'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/e20e0a3b-exec/skipcopy.targets'
FILTER = 'FullyQualifiedName~ScriptedPushObjectsTests|FullyQualifiedName~PushObjectProgramsOpcodesTests'

def edit(path, old, new):
    p = R + path
    s = open(p, 'rb').read().decode('utf-8')
    old = old.replace('\n', '\r\n')
    new = new.replace('\n', '\r\n')
    assert s.count(old) == 1, (path, s.count(old))
    open(p, 'wb').write(s.replace(old, new).encode('utf-8'))

RUNNER = 'Alundra/Events/EventProgramRunner.cs'
PROXY = 'Alundra/Entities/EntityScriptProxy.cs'

MUT = {
 'M1-flag-of-F-not-set': (RUNNER,
  '''            case 0x05: // Flag on - Script_5_005
            {
                var flag = (uint)((v[2] << 8) | v[1]);
                var mask = (uint)(1 << (v[1] & 0x1f));
                _gameState.AddFlag(flag, mask);
                return 3;''',
  '''            case 0x05: // Flag on - Script_5_005
            {
                var flag = (uint)((v[2] << 8) | v[1]);
                var mask = (uint)(1 << (v[1] & 0x1f));
                if (entity.MapEventProgramId != ScriptHelper.ProgramFInteract)
                {
                    _gameState.AddFlag(flag, mask);
                }

                return 3;'''),
 'M2-opcode-0x3A-removed-from-dispatch': (RUNNER, '            case 0x3A: // Set cardinal direction', '            case 0x13A: // Set cardinal direction'),
 'M3-start-of-F-at-contact-removed': (PROXY, 'if (ReferenceEquals(host.ActiveCollisionEntity, this)', 'if (false && ReferenceEquals(host.ActiveCollisionEntity, this)'),
 'M4-four-force-words-of-hero-not-zeroed': (RUNNER,
  '''                    player.ForceStepY = 0;
                    player.ForceStepX = 0;
                    player.ForceY = 0;
                    player.ForceX = 0;
''', ''),
 'M5-0x87-never-touched': (RUNNER, '                state.Result = touched ? 1 : 0;\n                return 3;', '                state.Result = 0;\n                return 3;'),
}

for name in sys.argv[1:]:
    path, old, new = MUT[name]
    edit(path, old, new)
    try:
        r = subprocess.run(['dotnet', 'test', R + 'Alundra.Tests', '-c', 'Release', '-p:CustomAfterMicrosoftCommonTargets=' + SK, '--filter', FILTER, '--blame-hang-timeout', '60s'],
                           capture_output=True, text=True, encoding='utf-8', errors='replace')
        out = r.stdout + r.stderr
    finally:
        subprocess.run(['git', '-C', R, 'checkout', '--', path])
    failed = sorted(set(re.findall(r'(?:Échoué|Failed) (Alundra\.Tests\.Events\.\S+)', out)))
    summary = re.findall(r'(?:Réussi|Échoué)!.*', out)
    err = re.findall(r'error CS.*', out)
    print('==', name)
    print('  summary:', summary[-1] if summary else '(none)', '| compile errors:', len(err))
    for f in failed:
        print('  RED', f)
    d = subprocess.run(['git', '-C', R, 'diff', '--stat', '--', 'Alundra'], capture_output=True, text=True).stdout.strip()
    print('  git diff -- Alundra after revert:', repr(d))
    sys.stdout.flush()
