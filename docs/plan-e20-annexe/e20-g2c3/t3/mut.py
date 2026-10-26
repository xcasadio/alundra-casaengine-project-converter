# E20.g2c3 T3: real mutants, applied by script, narrow suite (Release, neutralised copy), undone, `git diff` checked empty.
# usage: python -B mut.py <outfile> [name-prefix ...]
import subprocess, sys, re
R = 'D:/development/repo/alundra-casaengine-project-converter'
SK = 'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/e20e0a3b-exec/skipcopy.targets'
W = R + '/Alundra/Ai/Objects/WaterDropAi.cs'
N = R + '/Alundra/Ai/NativeSpriteRegistry.cs'
FILTER = '|'.join('FullyQualifiedName~' + c for c in ['WaterDropAi', 'WaterDropWorldTests', 'WaterDropMap92WorldTests', 'NativeSpriteRegistryTests', 'LiftableAiRegistrationTests', 'ChargedArrowAiTests', 'WaveSwordLifeTests', 'BallFlamesTests'])
muts = [
 ('m01 wait 40 -> 39', W, 'WaitTicks = 40', 'WaitTicks = 39'),
 ('m02 wait 40 -> 41', W, 'WaitTicks = 40', 'WaitTicks = 41'),
 ('m03 state 2 ignores CollidedWithEntityZ', W, 'drop.CollidedWithEntityZ != 0 || drop.IsOnGround != 0', 'drop.IsOnGround != 0'),
 ('m04 state 2 ignores IsOnGround', W, 'drop.CollidedWithEntityZ != 0 || drop.IsOnGround != 0', 'drop.CollidedWithEntityZ != 0'),
 ('m05 state 3 keeps Collidable', W, 'new DropStep(4, counter, null, flags & ~Collidable, false, targetIsTorch)', 'new DropStep(4, counter, null, flags, false, targetIsTorch)'),
 ('m06 state 3 advance only with a torch', W, 'return new DropStep(4, counter, null, flags & ~Collidable, false, targetIsTorch);', 'return targetIsTorch ? new DropStep(4, counter, null, flags & ~Collidable, false, true) : new DropStep(3, counter, null, flags, false, false);'),
 ('m07 torch type changed', W, 'target.SpriteType == TorchAi.SpriteType', 'target.SpriteType == TorchAi.WaterDropSpriteType'),
 ('m08 hero support taken without reading his carried object', W, 'hero != null && ReferenceEquals(support, hero.LogicContextEntity) ? hero.CarriedEntity : support', 'support'),
 ('m09 null carried object test removed', W, 'return target?.GameplayProxy as EntityScriptProxy;', 'return target!.GameplayProxy as EntityScriptProxy;'),
 ('m10 extinction without Bytes[0] = 1', W, 'target.Bytes[0] = NoFlameMark;', ''),
 ('m11 extinction without status 3', W, 'target!.Status = EntityStatus.Deactivated;', ''),
 ('m12 rise 160 -> 128', W, 'RiseRaw = 160 << 16', 'RiseRaw = 128 << 16'),
 ('m13 rise 160 -> 161', W, 'RiseRaw = 160 << 16', 'RiseRaw = 161 << 16'),
 ('m14 ForceZ not reset', W, 'drop.ForceZ = 0;', ''),
 ('m15 Gravity not removed at the restoration', W, '(flags & ~Gravity) | Collidable', 'flags | Collidable'),
 ('m16 Collidable not restored', W, '(flags & ~Gravity) | Collidable', '(flags & ~Gravity)'),
 ('m17 PushLogicalPositionToRoot forgotten', W, 'drop.PushLogicalPositionToRoot();', ''),
 ('m18 ProgramIndexes[2] not restored', W, 'drop.ProgramIndexes[2] = drop.Bytes[1];', ''),
 ('m19 restoration from Bytes[0]', W, 'drop.ProgramIndexes[2] = drop.Bytes[1];', 'drop.ProgramIndexes[2] = drop.Bytes[0];'),
 ('m20 A19 absent (Load does nothing)', W, 'drop.Bytes[1] = (byte)drop.ProgramIndexes[2];', 'drop.Bytes[1] = drop.Bytes[1];'),
 ('m21 state >= 5 treated as state 0', W, 'return new DropStep(state, counter, null, flags, false, false);', 'return new DropStep(1, WaitTicks, null, flags, false, false);'),
 ('m22 fall animation 1 -> 2', W, 'FallAnimation = 1', 'FallAnimation = 2'),
 ('m23 splash animation 2 -> 1', W, 'SplashAnimation = 2', 'SplashAnimation = 1'),
 ('m24 counter decremented by 2', W, 'counter - 1', 'counter - 2'),
 ('m25 rest animation 0 -> 1', W, 'RestAnimation = 0', 'RestAnimation = 1'),
 ('m26 A19 key 19 -> 18', W, 'LoadCode = 19', 'LoadCode = 18'),
 ('m27 not registered in production', N, 'WaterDropAi.Register(registry);', ''),
 ('m28 state 4 restores at once (animation end ignored)', W, 'drop.ForceResetAnimationFlag != 0,', 'true,'),
 ('m29 ResyncControllerFromFlags only on restore', W, 'if (step.Flags != drop.Flags || step.Restore)', 'if (step.Restore)'),
 ('m30 mark and status written on any target (type test removed)', W, 'target != null && target.SpriteType == TorchAi.SpriteType', 'target != null'),
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
