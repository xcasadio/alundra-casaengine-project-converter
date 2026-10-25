"""E20.f6b T5: real mutants of the DLL code of the balls, one at a time.

usage: python -B mutate_f6b_t5.py <out-dir> [name-prefix ...]

For each mutant: rewrite one source file from HEAD with one textual change, build Alundra.Tests (Release, neutralised copy),
run the narrow filter (FAST); if nothing is red, widen to the full targeted filter (FULL); then restore the file from HEAD
and check that `git diff` of the file is empty. One line per mutant: KILLED (stage, red count, first red tests) or SURVIVED.
"""
import subprocess, sys, re, io, os
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
REPO = 'D:/development/repo/alundra-casaengine-project-converter'
SK = 'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/e20e0a3b-exec/skipcopy.targets'
BALL = 'Alundra/Ai/Projectiles/BallAi.cs'
FX = 'Alundra/Rendering/Effects/NativeEffects.cs'
PM = 'Alundra/Hero/PlayerManager.cs'
ESP = 'Alundra/Entities/EntityScriptProxy.cs'
SM = 'Alundra/Physics/ScriptedMotion.cs'

FAST = '|'.join('FullyQualifiedName~' + n for n in ['BallAiTests', 'BallProductionTests', 'BallFlamesTests', 'NativeSpriteRegistryTests', 'WeaponSpawnSeamTests', 'MarkedEntityGroundSlopeTests'])
FULL = FAST + '|FullyQualifiedName~Alundra.Tests.Ai.Projectiles.|FullyQualifiedName~LiftableAiRegistrationTests'

FIRE_OR_ICE = 'fire ? FireBallEffect : IceBallEffect'
WATER = 'if (onWater)\n        {\n            host.DestroyEntity(entity, WaterEffect);\n        }\n\n        if (entity.ForceResetAnimationFlag'
PUFF_TAIL = "        if (onWater)\n        {\n            host.DestroyEntity(entity, WaterEffect);\n        }\n\n        if (entity.ForceResetAnimationFlag == 1)\n        {\n            host.DestroyEntity(entity, -1);\n            return;\n        }\n\n        if ((entity.FrameCounter & 7) != 0)\n        {\n            return;\n        }\n\n        world?.EffectStage?.Emit(reservoir => NativeEffects.CreateBallPuff(reservoir, fire, x, y, z));\n    }"
SET = '{ 3, 4, 7, 11, 12, 14, 15, 27, 28, 29 }'

# (name, file, old, new, occurrence (1-based))
MUTANTS = [
    ('T5-01 explosion: five effects -> four', FX, 'BallExplosionCount = 5', 'BallExplosionCount = 4', 1),
    ('T5-02 puff period 8 -> 4', BALL, '(entity.FrameCounter & 7) != 0', '(entity.FrameCounter & 3) != 0', 1),
    ('T5-03 slope 1 forgotten', BALL, 'entity.Slope_18c is 1 or 4', 'entity.Slope_18c is 4', 1),
    ('T5-04 slope 4 forgotten in branch A (later calls only)', BALL, WATER, 'if (entity.Slope_18c is 1)\n        {\n            host.DestroyEntity(entity, WaterEffect);\n        }\n\n        if (entity.ForceResetAnimationFlag', 1),
    ('T5-05 destruction with another effect (5)', BALL, 'WaterEffect = 6', 'WaterEffect = 5', 1),
    ('T5-06 puff before the destruction', BALL, PUFF_TAIL,
     "        if ((entity.FrameCounter & 7) == 0 && entity.ForceResetAnimationFlag != 1)\n        {\n            world?.EffectStage?.Emit(reservoir => NativeEffects.CreateBallPuff(reservoir, fire, x, y, z));\n        }\n\n        if (onWater)\n        {\n            host.DestroyEntity(entity, WaterEffect);\n        }\n\n        if (entity.ForceResetAnimationFlag == 1)\n        {\n            host.DestroyEntity(entity, -1);\n        }\n    }", 1),
    ('T5-07 fire Z offset applied to the ice (trail)', FX, 'z + (fire ? FireBallZOffset : 0));', 'z + FireBallZOffset);', 1),
    ('T5-08 fire Z offset applied to the ice (puff)', FX, 'z + (fire ? FireBallZOffset : 0));', 'z + FireBallZOffset);', 2),
    ('T5-09 trail speed not opposed (X)', FX, 'slot.ForceX = -forceX;', 'slot.ForceX = forceX;', 1),
    ('T5-10 trail speed not opposed (Z)', FX, 'slot.ForceZ = -forceZ;', 'slot.ForceZ = forceZ;', 1),
    ('T5-11 effect 17 for the ice (trail)', FX, FIRE_OR_ICE, 'FireBallEffect', 1),
    ('T5-12 effect 17 for the ice (explosion)', FX, FIRE_OR_ICE, 'FireBallEffect', 2),
    ('T5-13 draws made at the call, outside the application of the queue', BALL, 'world?.EffectStage?.Emit(reservoir => NativeEffects.CreateBallExplosion(reservoir, fire, x, y, z));',
     'if (world?.EffectStage is { } stage) { NativeEffects.CreateBallExplosion(stage.Reservoir, fire, x, y, z); }', 1),
    ('T5-14 key C60 forgotten', BALL, 'var tick = registry.Register(ScriptHelper.ProgramCTick, TickCode, SpriteTypes, (entity, context, state) => Trail(entity));',
     'System.IDisposable tick = new System.IO.MemoryStream();', 1),
    ('T5-15 key E9 forgotten', BALL, 'var deactivate = registry.Register(ScriptHelper.ProgramEDeactivate, DeactivateCode, SpriteTypes, (entity, context, state) => Explode(entity));',
     'System.IDisposable deactivate = new System.IO.MemoryStream();', 1),
    ('T5-16 type 10 wired in production', PM, SET, '{ 3, 4, 7, 10, 11, 12, 14, 15, 27, 28, 29 }', 1),
    ('T5-17 type 13 wired in production', PM, SET, '{ 3, 4, 7, 11, 12, 13, 14, 15, 27, 28, 29 }', 1),
    ('T5-18 set without 3 (the sword)', PM, SET, '{ 4, 7, 11, 12, 14, 15, 27, 28, 29 }', 1),
    ('T5-19 set without 15 (charged ice)', PM, SET, '{ 3, 4, 7, 11, 12, 14, 27, 28, 29 }', 1),
    ('T5-20 ball advances at the speed of the current animation (D-F5-5)', SM, '|| entity.HasLogicalZ ? entity.TargetAnimationId', '? entity.TargetAnimationId', 1),
    ('T5-21 slope fed to every entity', ESP, 'if (HasLogicalZ || Alundra.Ai.Objects.LiftableAi.FeedsGroundSlope(SpriteType))', 'if (true)', 1),
    ('T5-22 trail period 4 -> 8', BALL, '(entity.FrameCounter & 3) != 0', '(entity.FrameCounter & 7) != 0', 1),
    ('T5-23 trail Z without the +1 of the binary frame', BALL, 'var (x, y, z) = (entity.PosX, entity.PosY, unchecked(entity.PosZ + 1));\n        var (forceX', 'var (x, y, z) = (entity.PosX, entity.PosY, entity.PosZ);\n        var (forceX', 1),
    ('T5-24 later branch returns after the destruction on the water', BALL, WATER, 'if (onWater)\n        {\n            host.DestroyEntity(entity, WaterEffect);\n            return;\n        }\n\n        if (entity.ForceResetAnimationFlag', 1),
]


def run(args):
    return subprocess.run(args, cwd=REPO, capture_output=True, text=True, encoding='utf-8', errors='replace')


def test(flt):
    b = run(['dotnet', 'build', 'Alundra.Tests', '-c', 'Release', '-p:CustomAfterMicrosoftCommonTargets=' + SK])
    if ' error ' in b.stdout:
        return None, ['BUILD ERROR'] + [l.strip() for l in b.stdout.splitlines() if ' error ' in l][:2]
    t = run(['dotnet', 'test', 'Alundra.Tests', '-c', 'Release', '--no-build', '-p:CustomAfterMicrosoftCommonTargets=' + SK, '--filter', flt, '--blame-hang-timeout', '60s'])
    red = sorted(set(re.sub(r' \[[0-9 a-z]*\]$', '', l.strip()) for l in t.stdout.splitlines() if re.match(r'^\s+(Échoué|Failed) ', l)))
    ran = re.search(r'(Total|Réussite|Passed)\s*:', t.stdout) is not None
    if not red and not ran:
        print('   [no result]', t.stdout[-600:].replace(chr(10), ' | '), t.stderr[-300:], flush=True)
    return (len(red) if (red or ran) else -1), red


only = sys.argv[1:]
for name, path, old, new, occ in MUTANTS:
    if only and not any(name.startswith(o) for o in only):
        continue
    head = run(['git', 'show', 'HEAD:' + path]).stdout.replace('\r\n', '\n')
    full = os.path.join(REPO, path)
    raw = open(full, 'rb').read()
    crlf = b'\r\n' in raw
    parts = head.split(old)
    if len(parts) <= occ:
        print('NOT APPLICABLE', name, flush=True)
        continue
    mutated = old.join(parts[:occ]) + new + old.join(parts[occ:])
    if crlf:
        mutated = mutated.replace('\n', '\r\n')
    open(full, 'wb').write(mutated.encode('utf-8'))
    try:
        stage = 'FAST'
        n, red = test(FAST)
        if n == 0 or (n is not None and n < 0):
            stage = 'FULL'
            n, red = test(FULL)
        verdict = 'KILLED' if (n is not None and n > 0) else ('BUILD ERROR' if n is None else ('NO RESULT' if n < 0 else 'SURVIVED'))
        print('%-72s %s stage=%s red=%s' % (name, verdict, stage, n), flush=True)
        for r in red[:4]:
            print('      ', r[:170], flush=True)
    finally:
        open(full, 'wb').write(raw)
        d = run(['git', 'diff', '--quiet', '--', path])
        print('      git diff empty after: %s' % (d.returncode == 0), flush=True)
