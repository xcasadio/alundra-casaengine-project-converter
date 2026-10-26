# E20.g2c2 T3: real mutants, applied by script, narrow suite (Release, neutralised copy), undone, `git diff` checked empty.
# usage: python -B mut.py <outfile> [name-prefix ...]
import subprocess, sys, re
R = 'D:/development/repo/alundra-casaengine-project-converter'
SK = 'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/e20e0a3b-exec/skipcopy.targets'
T = R + '/Alundra/Ai/Objects/TorchAi.cs'
B = R + '/Alundra/Ai/Objects/BrambleAi.cs'
L = R + '/Alundra/Ai/Objects/LiftableAi.cs'
N = R + '/Alundra/Ai/NativeSpriteRegistry.cs'
P = R + '/Alundra/Hero/PlayerManager.cs'
FILTER = '|'.join('FullyQualifiedName~' + c for c in ['TorchAiTests', 'BrambleAiTests', 'TorchDataGuardTests', 'TorchRegistrationTests', 'TorchWorldTests', 'NativeSpriteRegistryTests', 'LiftableAiRegistrationTests', 'LiftableAiTests', 'LiftableSharedReleaseTests', 'LiftPureTests'])
FL = 'flame.Flags = (flame.Flags & ~FlameClearBits) | FlameSetBits;'
muts = [
 ('m01 C70 | 0x34 -> | 0x30', L, 'TorchReleaseSetBits = 0x34', 'TorchReleaseSetBits = 0x30'),
 ('m02 C70 taken for C4 (the call)', T, 'LiftableAi.TickCarried(entity, LiftableAi.ReleaseFlagsPolicy.C70);', 'LiftableAi.TickCarried(entity, LiftableAi.ReleaseFlagsPolicy.C4);'),
 ('m03 C70 case of the switch returns the flags unchanged', L, 'ReleaseFlagsPolicy.C70 => (entity.Flags | TorchReleaseSetBits) & ~ReleaseClearBits,', 'ReleaseFlagsPolicy.C70 => entity.Flags,'),
 ('m04 erasure removed', T, 'flame.SpriteProgramIndexes[i] = 0;', 'flame.SpriteProgramIndexes[i] = flame.SpriteProgramIndexes[i];'),
 ('m05 erasure skips the low code (+0x70)', T, 'for (var i = 0; i < flame.SpriteProgramIndexes.Length; i++)', 'for (var i = 1; i < flame.SpriteProgramIndexes.Length; i++)'),
 ('m06 erasure skips the high code (+0x84)', T, 'for (var i = 0; i < flame.SpriteProgramIndexes.Length; i++)', 'for (var i = 0; i < flame.SpriteProgramIndexes.Length - 1; i++)'),
 ('m07 flame animation 3 -> 1', T, 'FlameAnimation = 3', 'FlameAnimation = 1'),
 ('m08 flame & ~0x10 forgotten', T, FL, 'flame.Flags = flame.Flags | FlameSetBits;'),
 ('m09 flame | 2 forgotten', T, FL, 'flame.Flags = flame.Flags & ~FlameClearBits;'),
 ('m10 +0xAC == 1 test removed', T, 'entity.TargetAnimationId == BurningAnimation && entity.ForceResetAnimationFlag == 1', 'entity.TargetAnimationId == BurningAnimation'),
 ('m11 +0x274 mark ignored', T, 'if (!HasNoFlameMark(entity))', 'if (true)'),
 ('m12 contact type 391 -> 392', T, 'WaterDropSpriteType = 391', 'WaterDropSpriteType = 392'),
 ('m13 D34 attribute 6 -> 5', B, 'FireAttribute = 6', 'FireAttribute = 5'),
 ('m14 D34 | 0x46 -> | 0x42', B, 'BurningSetBits = 0x46', 'BurningSetBits = 0x42'),
 ('m15 D34 & ~0x80 forgotten', B, 'entity.Flags = (entity.Flags | BurningSetBits) & ~BurningClearBits;', 'entity.Flags = (entity.Flags | BurningSetBits);'),
 ('m16 393 wired without its key (TorchAi.Register not called by the production registry)', N, '        TorchAi.Register(registry);\n', ''),
 ('m17 E20 writes Flags = 2 for the flame', T, FL, 'flame.Flags = FlameSetBits;'),
 ('m18 C70 guard (+0x28 == 0 -> return) removed', T, 'if (entity.PlatformEntity == null)\n        {\n            return;\n        }\n\n        LiftableAi.TickCarried(entity, LiftableAi.ReleaseFlagsPolicy.C70);', 'LiftableAi.TickCarried(entity, LiftableAi.ReleaseFlagsPolicy.C70);'),
 ('m19 torch not destroyed after the flame', T, 'entity.ScriptHost?.DestroyEntity(entity, -1);', ''),
 ('m20 burning torch & ~0x30 -> & ~0x20', T, 'BurningClearBits = 0x30', 'BurningClearBits = 0x20'),
 ('m21 burning torch | 0x40 forgotten', T, 'BurningSetBits = 0x40', 'BurningSetBits = 0'),
 ('m22 the mark written in byte 1 instead of byte 0', T, 'entity.Bytes[0] = 1;', 'entity.Bytes[1] = 1;'),
 ('m23 the bramble D34 key not registered', N, '        BrambleAi.Register(registry);\n', ''),
 ('m24 393 out of the wired set', P, ', Alundra.Ai.Objects.TorchAi.SpriteTypes));', ', System.Array.Empty<int>()));'),
 ('m25 D34 tests the whole attribute byte (no & 0xF)', B, '(attack.Attribute & 0xF) == FireAttribute', 'attack.Attribute == FireAttribute'),
 ('m26 table full: the flame null check removed (null deref)', T, 'if (flame != null)', 'if (true)'),
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
