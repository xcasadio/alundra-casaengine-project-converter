import subprocess, sys, re
R = 'D:/development/repo/alundra-casaengine-project-converter'
SK = 'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/e20e0a3b-exec/skipcopy.targets'
M = {
 'noreg': ('Alundra/Ai/NativeSpriteRegistry.cs', '        EarthTome1Ai.Register(registry);\n', ''),
 'driverstub': ('Alundra/Hero/PlayerManager.cs', 'item == EarthTome1Item ? EarthTome1Ai.Driver(player) : ItemDriverStub(item)', 'ItemDriverStub(item)'),
 'e3neutral': ('Alundra/Ai/Items/EarthTome1Ai.cs', '    internal static void ImpactDeactivate(EntityScriptProxy entity)\n    {\n', '    internal static void ImpactDeactivate(EntityScriptProxy entity)\n    {\n        if (entity != null) { return; }\n'),
 'startstub': ('Alundra/Hero/PlayerManager.cs', 'item == EarthTome1Item ? TomeStart(player, item) : ItemStartStub(item)', 'ItemStartStub(item)'),
 'cancelstub': ('Alundra/Hero/PlayerManager.cs', '        else if (item == EarthTome1Item)\n        {\n            EarthTome1Ai.Cancel(player);\n        }\n', ''),
}
name = sys.argv[1]
path, old, new = M[name]
full = R + '/' + path
s = open(full, encoding='utf-8', newline='').read()
nl = '\r\n' if '\r\n' in s else '\n'
old = old.replace('\n', nl); new = new.replace('\n', nl)
assert s.count(old) == 1, name
open(full, 'w', encoding='utf-8', newline='').write(s.replace(old, new))
try:
    flt = 'FullyQualifiedName~EarthTome1|FullyQualifiedName~NativeSpriteRegistryTests|FullyQualifiedName~LiftableAiRegistrationTests|FullyQualifiedName~ChargedArrowAiTests|FullyQualifiedName~WaveSwordLifeTests|FullyQualifiedName~BallFlamesTests'
    out = subprocess.run(['dotnet', 'test', R + '/Alundra.Tests/Alundra.Tests.csproj', '-c', 'Release', '-p:CustomAfterMicrosoftCommonTargets=' + SK, '--filter', flt, '--blame-hang-timeout', '60s'], capture_output=True, text=True, encoding='utf-8', errors='replace').stdout
finally:
    open(full, 'w', encoding='utf-8', newline='').write(s)
failed = sorted(set(re.findall(r'Échoué (Alundra\.Tests\S+)', out)))
print(name, len(failed))
for f in failed:
    print('  ', f)
print([l for l in out.splitlines() if 'Échoué!' in l or 'Réussi!' in l or 'error' in l.lower()][:3])
