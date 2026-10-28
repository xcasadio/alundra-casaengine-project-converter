import subprocess, sys, re
R = 'D:/development/repo/alundra-casaengine-project-converter'
SK = 'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/e20e0a3b-exec/skipcopy.targets'
FM = 'Alundra/Ai/Items/FireTome2Ai.cs'
PM = 'Alundra/Hero/PlayerManager.cs'
SW = 'Alundra/Cameras/CameraSway.cs'
# each mutation: a list of (path, old, new) applied together
M = {
 'entries_stub': [
  (PM, 'item == EarthTome1Item || item == EarthTome2Item || item == FireTome2Item ? TomeStart(player, item)', 'item == EarthTome1Item || item == EarthTome2Item ? TomeStart(player, item)'),
  (PM, 'item == FireTome2Item ? FireTome2Ai.Driver(player) : ItemDriverStub(item)', 'ItemDriverStub(item)'),
  (PM, '        else if (item == FireTome2Item)\n        {\n            FireTome2Ai.Cancel(player);\n        }\n', ''),
 ],
 'start_stub': [(PM, 'item == EarthTome1Item || item == EarthTome2Item || item == FireTome2Item ? TomeStart(player, item)', 'item == EarthTome1Item || item == EarthTome2Item ? TomeStart(player, item)')],
 'driver_stub': [(PM, 'item == FireTome2Item ? FireTome2Ai.Driver(player) : ItemDriverStub(item)', 'ItemDriverStub(item)')],
 'cancel_stub': [(PM, '        else if (item == FireTome2Item)\n        {\n            FireTome2Ai.Cancel(player);\n        }\n', '')],
 'noreg': [('Alundra/Ai/NativeSpriteRegistry.cs', '        FireTome2Ai.Register(registry);\n', '')],
 'e28_neutral': [(FM, '    internal static void ColumnEnds(EntityScriptProxy entity)\n    {\n', '    internal static void ColumnEnds(EntityScriptProxy entity)\n    {\n        if (entity != null) { return; }\n')],
 'buffer_inert': [
  (SW, '    internal void CaptureToBuffer()\n    {\n', '    internal void CaptureToBuffer()\n    {\n        if (Buffer != null) { return; }\n'),
  (SW, '    internal void RestoreFromBuffer()\n    {\n', '    internal void RestoreFromBuffer()\n    {\n        if (Buffer != null) { return; }\n'),
 ],
}
name = sys.argv[1]
saved = {}
for path, old, new in M[name]:
    full = R + '/' + path
    if path not in saved:
        saved[path] = open(full, encoding='utf-8', newline='').read()
    s = open(full, encoding='utf-8', newline='').read()
    nl = '\r\n' if '\r\n' in s else '\n'
    o = old.replace('\n', nl); n = new.replace('\n', nl)
    assert s.count(o) == 1, (name, path, old[:40])
    open(full, 'w', encoding='utf-8', newline='').write(s.replace(o, n))
try:
    flt = 'FullyQualifiedName~FireTome2|FullyQualifiedName~CameraSwayBuffer|FullyQualifiedName~NativeSpriteRegistryTests|FullyQualifiedName~LiftableAiRegistrationTests|FullyQualifiedName~ChargedArrowAiTests|FullyQualifiedName~WaveSwordLifeTests|FullyQualifiedName~BallFlamesTests'
    out = subprocess.run(['dotnet', 'test', R + '/Alundra.Tests/Alundra.Tests.csproj', '-c', 'Release', '-p:CustomAfterMicrosoftCommonTargets=' + SK, '--filter', flt, '--blame-hang-timeout', '60s'], capture_output=True, text=True, encoding='utf-8', errors='replace').stdout
finally:
    for path, s in saved.items():
        open(R + '/' + path, 'w', encoding='utf-8', newline='').write(s)
failed = sorted(set(re.findall(r'Échoué (Alundra\.Tests\S+)', out)))
print(name, len(failed))
for f in failed:
    print('  ', f[13:].split('_')[0] if '_' in f else f[13:], '|', f[13:90])
print([l for l in out.splitlines() if 'Échoué!' in l or 'Réussi!' in l or ' error ' in l.lower()][:3])
