# E20.g2c2 T3: real mutants, applied by script, narrow suite (Release, neutralised copy), undone, `git diff` checked empty.
# usage: python -B mut.py <outfile> [name-prefix ...]
import subprocess, sys, re
R = 'D:/development/repo/alundra-casaengine-project-converter'
SK = 'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/e20e0a3b-exec/skipcopy.targets'
PM = R + '/Alundra/Hero/PlayerManager.cs'
CF = R + '/Alundra/Physics/CellsCollisionField.cs'
EP = R + '/Alundra/Entities/EntityScriptProxy.cs'
WP = R + '/Alundra/Maps/WorldProxy.cs'
SM = R + '/Alundra/Physics/ScriptedMotion.cs'
TS = R + '/Alundra/SaveGames/TestSaves.cs'
FILTER = '|'.join('FullyQualifiedName~' + c for c in ['HeroDeepWaterWallTests', 'HeroDeepWaterWallWorldTests', 'DeepWaterWallFieldTests', 'HeroSwimArenaTests', 'TestSavesTests', 'PrefabArcSupportTests'])
REF = 'settings.WalkabilityMask = BootsLevelOf(state) < 2'
ELSE = ': settings.WalkabilityMask & ~CellsCollisionField.DeepWaterWallMaskBit;'
FIELD = '&& !((walkabilityMask & DeepWaterWallMaskBit) != 0 && (tileFlags & 0xE00) == 0x800);'
muts = [
 ('m01 boots level < 2 -> < 3', PM, REF, 'settings.WalkabilityMask = BootsLevelOf(state) < 3'),
 ('m02 boots level < 2 -> < 1', PM, REF, 'settings.WalkabilityMask = BootsLevelOf(state) < 1'),
 ('m03 (tileFlags & 0xE00) == 0x800 -> (tileFlags & 0x800) != 0', CF, FIELD, '&& !((walkabilityMask & DeepWaterWallMaskBit) != 0 && (tileFlags & 0x800) != 0);'),
 ('m04 a height condition added to the wall (cellHeight == 0)', CF, FIELD, '&& !((walkabilityMask & DeepWaterWallMaskBit) != 0 && (tileFlags & 0xE00) == 0x800 && cellHeight == 0);'),
 ('m05 the bit written only at the adoption (Tick does not refresh)', PM, '        RefreshHeroWalkabilityMask(player, player.ScriptHost?.GameState);\n        ScriptedMotion.TickPlayer(player, ticks);', '        ScriptedMotion.TickPlayer(player, ticks);'),
 ('m06 ResyncControllerFromFlags erases the bit', EP, ' | (Controller.Settings.WalkabilityMask & CellsCollisionField.DeepWaterWallMaskBit);', ';'),
 ('m07 the adoption does not give the bit', WP, '            PlayerManager.RefreshHeroWalkabilityMask(proxy, GameState);', '            // mutant'),
 ('m08 BlockedCorners on the Flags mask', SM, 'var mask = entity.Controller.Settings.WalkabilityMask;', 'var mask = CellsCollisionField.WalkabilityMaskFor(entity.Flags);'),
 ('m09 the function of the tick calls ResyncControllerFromFlags', PM, ELSE, ELSE + '\n        hero.ResyncControllerFromFlags();'),
 ('m10 the wall applied to every entity (WalkabilityMaskFor carries the bit)', CF, 'var mask = BaseWalkabilityMask;', 'var mask = BaseWalkabilityMask | DeepWaterWallMaskBit;'),
 ('m11 the bit never cleared (boots of level 2 keep the wall)', PM, ELSE, ': settings.WalkabilityMask;'),
 ('m12 the bit inside the 16 bits (0x10000 -> 0x100)', CF, 'DeepWaterWallMaskBit = 0x10000;', 'DeepWaterWallMaskBit = 0x100;'),
 ('m13 the equality on 0xE00 against 0xA00', CF, '(tileFlags & 0xE00) == 0x800);', '(tileFlags & 0xE00) == 0xA00);'),
 ('m14 the wall removed from the field', CF, FIELD, '&& !(false && (tileFlags & 0xE00) == 0x800);'),
 ('m15 mur-m14 given the boots of level 2', TS, 'new Preset("mur-m14", "test-mur-m14", 14, 40, 48, 1, [], [], [], [])', 'new Preset("mur-m14", "test-mur-m14", 14, 40, 48, 1, [], [], [], [(CounterIndex(0x1B), 1)])'),
]
out = sys.argv[1]
only = sys.argv[2:]
for name, path, old, new in muts:
    if only and not any(name.startswith(o + ' ') for o in only):
        continue
    raw = open(path, encoding='utf-8', newline='').read()
    crlf = '\r\n' in raw
    src = raw.replace('\r\n', '\n')
    if old not in src:
        res = 'PATTERN NOT FOUND'
    else:
        mutated = src.replace(old, new, 1)
        if crlf:
            mutated = mutated.replace('\n', '\r\n')
        open(path, 'w', encoding='utf-8', newline='').write(mutated)
        try:
            b = subprocess.run(['dotnet', 'build', 'Alundra.Tests/Alundra.Tests.csproj', '-c', 'Release', '--no-restore', '-p:CustomAfterMicrosoftCommonTargets=' + SK, '--nologo', '-v', 'q'], cwd=R, capture_output=True, text=True, encoding='utf-8', errors='replace', timeout=500)
            if re.search(r': error ', b.stdout):
                res = 'BUILD ERROR ' + ' | '.join(l for l in b.stdout.splitlines() if ': error ' in l)[:300]
            else:
                t = subprocess.run(['dotnet', 'test', 'Alundra.Tests/Alundra.Tests.csproj', '-c', 'Release', '--no-build', '-p:CustomAfterMicrosoftCommonTargets=' + SK, '--blame-hang-timeout', '60s', '--filter', FILTER, '--nologo'], cwd=R, capture_output=True, text=True, encoding='utf-8', errors='replace', timeout=580)
                failed = sorted(set(re.sub(r'^Alundra\.Tests\.', '', m.group(1)) for m in re.finditer(r'^\s+(?:Échoué|Failed)\s+(\S+)', t.stdout, re.M)))
                tot = re.search(r'(?:Échoué|Réussi|Failed|Passed)!.*', t.stdout)
                res = ('KILLED by %d: %s' % (len(failed), '; '.join(failed))) if failed else 'SURVIVED'
                res += '   [' + (tot.group(0) if tot else 'no summary') + ']'
        finally:
            open(path, 'w', encoding='utf-8', newline='').write(raw)
    d = subprocess.run(['git', '-C', R, 'diff', '--ignore-cr-at-eol', '--stat', '--', 'Alundra', 'Alundra.Tests'], capture_output=True, text=True).stdout.strip()
    line = '%s -> %s | git diff after undo: %s' % (name, res, 'EMPTY' if not d else d)
    print(line, flush=True)
    open(out, 'a', encoding='utf-8').write(line + '\n')
