import subprocess, sys, re
R = 'D:/development/repo/alundra-casaengine-project-converter'
SK = 'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/e20e0a3b-exec/skipcopy.targets'
FM = 'Alundra/Ai/Items/FireTome1Ai.cs'
PM = 'Alundra/Hero/PlayerManager.cs'
ET = 'Alundra/Ai/Items/EarthTome1Ai.cs'
# each mutation: a list of (path, old, new) applied together
M = {
 'entries_stub': [
  (PM, 'item == EarthTome2Item || item == FireTome1Item || item == FireTome2Item ? TomeStart(player, item)', 'item == EarthTome2Item || item == FireTome2Item ? TomeStart(player, item)'),
  (PM, 'item == FireTome1Item ? FireTome1Ai.Drive(player) : item == FireTome2Item', 'item == FireTome2Item'),
  (PM, '        else if (item == FireTome1Item)\n        {\n            FireTome1Ai.Cancel(player);\n        }\n', ''),
 ],
 'cancel_stub': [(PM, '        else if (item == FireTome1Item)\n        {\n            FireTome1Ai.Cancel(player);\n        }\n', '')],
 'noreg': [('Alundra/Ai/NativeSpriteRegistry.cs', '        FireTome1Ai.Register(registry);\n', '')],
 'cancel_neutral': [(FM, '    internal static void Cancel(EntityScriptProxy hero)\n    {\n', '    internal static void Cancel(EntityScriptProxy hero)\n    {\n        if (hero != null) { return; }\n')],
 'drive_neutral': [(FM, '    internal static int Drive(EntityScriptProxy hero)\n    {\n', '    internal static int Drive(EntityScriptProxy hero)\n    {\n        if (hero != null) { return 0; }\n')],
 'balltick_neutral': [(FM, 'int sortThreshold = TargetQuery.SortThreshold)\n    {\n', 'int sortThreshold = TargetQuery.SortThreshold)\n    {\n        if (ball != null) { return; }\n')],
 'types16': [(ET, 'SpriteTypes = { 16, 23 };', 'SpriteTypes = { 16 };')],
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
    flt = 'FullyQualifiedName~FireTome1|FullyQualifiedName~EarthTome1|FullyQualifiedName~TomeWords|FullyQualifiedName~NativeSpriteRegistryTests|FullyQualifiedName~LiftableAiRegistrationTests|FullyQualifiedName~ChargedArrowAiTests|FullyQualifiedName~WaveSwordLifeTests|FullyQualifiedName~BallFlamesTests'
    out = subprocess.run(['dotnet', 'test', R + '/Alundra.Tests/Alundra.Tests.csproj', '-c', 'Release', '-p:CustomAfterMicrosoftCommonTargets=' + SK, '--filter', flt, '--blame-hang-timeout', '60s'], capture_output=True, text=True, encoding='utf-8', errors='replace').stdout
finally:
    for path, s in saved.items():
        open(R + '/' + path, 'w', encoding='utf-8', newline='').write(s)
failed = sorted(set(re.findall(r'Échoué (Alundra\.Tests[^\[\n]*?)\s*\[', out)))
print(name, len(failed))
for f in failed:
    print('  ', f[13:])
print([l for l in out.splitlines() if 'Échoué!' in l or 'Réussi!' in l or ' error ' in l.lower()][:3])
