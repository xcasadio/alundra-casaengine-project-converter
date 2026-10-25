import subprocess, sys, shutil, re, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
REPO = 'D:/development/repo/alundra-casaengine-project-converter'
S = 'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad'
SK = S + '/e20e0a3b-exec/skipcopy.targets'
BALL = REPO + '/Alundra/Ai/Projectiles/BallAi.cs'
FX = REPO + '/Alundra/Rendering/Effects/NativeEffects.cs'
real = {BALL: S + '/f6b-t1/BallAi.real.cs', FX: S + '/f6b-t1/NativeEffects.real.cs'}

# (name, file, old, new, filter)
MUTANTS = [
    ('M01 explosion: five effects -> four', FX, 'BallExplosionCount = 5', 'BallExplosionCount = 4'),
    ('M02 puff period 8 -> 4', BALL, '(entity.FrameCounter & 7) != 0', '(entity.FrameCounter & 3) != 0'),
    ('M03 slope 1 forgotten (branch B and A)', BALL, 'entity.Slope_18c is 1 or 4', 'entity.Slope_18c is 4'),
    ('M04 slope 4 forgotten', BALL, 'entity.Slope_18c is 1 or 4', 'entity.Slope_18c is 1'),
    ('M05 destruction with another effect', BALL, 'WaterEffect = 6', 'WaterEffect = 5'),
    ('M06 type 13 is fire', BALL, '(uint)(entity.SpriteType - 11) < 2u', '(uint)(entity.SpriteType - 11) < 3u'),
    ('M07 trail speed not opposed (Z)', FX, 'slot.ForceZ = -forceZ;', 'slot.ForceZ = forceZ;'),
    ('M08 trail speed not opposed (X)', FX, 'slot.ForceX = -forceX;', 'slot.ForceX = forceX;'),
    ('M09 ice trail takes the fire offset', FX, 'z + (fire ? FireBallZOffset : 0));\n            if (slot != null)\n            {\n                slot.ForceX = -forceX;', 'z + FireBallZOffset);\n            if (slot != null)\n            {\n                slot.ForceX = -forceX;'),
    ('M10 puff offset on the ice', FX, 'var slot = reservoir.Create(false, fire ? FireBallEffect : IceBallEffect, 0, x, y, z + (fire ? FireBallZOffset : 0));\n            if (slot != null)\n            {\n                var r1', 'var slot = reservoir.Create(false, fire ? FireBallEffect : IceBallEffect, 0, x, y, z + FireBallZOffset);\n            if (slot != null)\n            {\n                var r1'),
    ('M11 explosion scale vx 0x60001 -> 0x50001', FX, 'slot.ForceX = (int)(((ulong)r1 * 0x60001) >> 32) - 0x30000;', 'slot.ForceX = (int)(((ulong)r1 * 0x50001) >> 32) - 0x30000;'),
    ('M24 explosion scale vy 0x40001 -> 0x30001', FX, 'slot.ForceY = (int)(((ulong)r2 * 0x40001) >> 32) - 0x20000;', 'slot.ForceY = (int)(((ulong)r2 * 0x30001) >> 32) - 0x20000;'),
    ('M25 puff scale vx 0x30001 -> 0x20001', FX, 'slot.ForceX = (int)(((ulong)r1 * 0x30001) >> 32) - 0x18000;', 'slot.ForceX = (int)(((ulong)r1 * 0x20001) >> 32) - 0x18000;'),
    ('M26 puff scale vy 0x20001 -> 0x30001', FX, 'slot.ForceY = (int)(((ulong)r2 * 0x20001) >> 32) - 0x10000;', 'slot.ForceY = (int)(((ulong)r2 * 0x30001) >> 32) - 0x10000;'),
    ('M12 puff vz 2 -> 1', FX, 'slot.ForceZ = 0x20000;', 'slot.ForceZ = 0x10000;'),
    ('M13 flag mask ~0x30 -> ~0x31', BALL, '& ~0x30u', '& ~0x31u'),
    ('M14 animation set to 2 instead of 1', BALL, 'ExplodedAnimation = 1;', 'ExplodedAnimation = 2;'),
    ('M15 end of the animation test never true', BALL, 'entity.ForceResetAnimationFlag == 1', 'entity.ForceResetAnimationFlag == 7'),
    ('M16 explosion Z carries the fire offset', BALL, 'world?.EffectStage?.Emit(reservoir => NativeEffects.CreateBallExplosion(reservoir, fire, x, y, z));', 'world?.EffectStage?.Emit(reservoir => NativeEffects.CreateBallExplosion(reservoir, fire, x, y, fire ? z + 0x80000 : z));'),
    ('M17 draws made at the call (no queue)', BALL, 'world?.EffectStage?.Emit(reservoir => NativeEffects.CreateBallExplosion(reservoir, fire, x, y, z));', 'if (world?.EffectStage is { } stage) { NativeEffects.CreateBallExplosion(stage.Reservoir, fire, x, y, z); }'),
    ('M18 trail period 4 -> 8', BALL, '(entity.FrameCounter & 3) != 0', '(entity.FrameCounter & 7) != 0'),
    ('M19 trail Z not binary frame (no +1)', BALL, 'var (x, y, z) = (entity.PosX, entity.PosY, unchecked(entity.PosZ + 1));\n        var (forceX', 'var (x, y, z) = (entity.PosX, entity.PosY, entity.PosZ);\n        var (forceX'),
    ('M20 later branch: no destruction on the water', BALL, 'if (onWater)\n        {\n            host.DestroyEntity(entity, WaterEffect);\n        }\n\n        if (entity.ForceResetAnimationFlag', 'if (entity.ForceResetAnimationFlag'),
    ('M21 key C60 forgotten', BALL, 'var tick = registry.Register(ScriptHelper.ProgramCTick, TickCode, SpriteTypes, (entity, context, state) => Trail(entity));', 'var tick = registry.Register(ScriptHelper.ProgramCTick, TickCode, new int[] { 11, 12, 14 }, (entity, context, state) => Trail(entity));'),
    ('M22 key E9 for 3 types only', BALL, 'registry.Register(ScriptHelper.ProgramEDeactivate, DeactivateCode, SpriteTypes,', 'registry.Register(ScriptHelper.ProgramEDeactivate, DeactivateCode, new int[] { 10, 11, 12 },'),
    ('M23 later branch returns after the destruction on the water', BALL, 'if (onWater)\n        {\n            host.DestroyEntity(entity, WaterEffect);\n        }\n\n        if (entity.ForceResetAnimationFlag', 'if (onWater)\n        {\n            host.DestroyEntity(entity, WaterEffect);\n            return;\n        }\n\n        if (entity.ForceResetAnimationFlag'),
    ('M27 the puff comes before the destruction', BALL,
     "        if (onWater)\n        {\n            host.DestroyEntity(entity, WaterEffect);\n        }\n\n        if (entity.ForceResetAnimationFlag == 1)\n        {\n            host.DestroyEntity(entity, -1);\n            return;\n        }\n\n        if ((entity.FrameCounter & 7) != 0)\n        {\n            return;\n        }\n\n        world?.EffectStage?.Emit(reservoir => NativeEffects.CreateBallPuff(reservoir, fire, x, y, z));\n    }",
     "        if ((entity.FrameCounter & 7) == 0 && entity.ForceResetAnimationFlag != 1)\n        {\n            world?.EffectStage?.Emit(reservoir => NativeEffects.CreateBallPuff(reservoir, fire, x, y, z));\n        }\n\n        if (onWater)\n        {\n            host.DestroyEntity(entity, WaterEffect);\n        }\n\n        if (entity.ForceResetAnimationFlag == 1)\n        {\n            host.DestroyEntity(entity, -1);\n        }\n    }"),
]
FILTER = sys.argv[1]
only = sys.argv[2:] if len(sys.argv) > 2 else None
survivors = []
for name, path, old, new in MUTANTS:
    if only and not any(name.startswith(o) for o in only):
        continue
    text = open(real[path], encoding='utf-8').read()
    text = text.replace('\r\n', '\n')
    if old not in text:
        print('NOT APPLICABLE', name)
        survivors.append(name + ' (not applicable)')
        continue
    mutated = text.replace(old, new, 1)
    open(path, 'w', encoding='utf-8', newline='\n').write(mutated)
    try:
        b = subprocess.run(['dotnet', 'build', 'Alundra.Tests', '-c', 'Release', '-p:CustomAfterMicrosoftCommonTargets=' + SK], cwd=REPO, capture_output=True, text=True, encoding='utf-8', errors='replace')
        if ' error ' in b.stdout:
            print('BUILD ERROR', name)
            survivors.append(name + ' (build error)')
            continue
        t = subprocess.run(['dotnet', 'test', 'Alundra.Tests', '-c', 'Release', '--no-build', '--filter', FILTER, '--blame-hang-timeout', '60s'], cwd=REPO, capture_output=True, text=True, encoding='utf-8', errors='replace')
        m = re.search(r'!\s+-\s+\S+\s*:\s+(\d+),\s+\S+\s*:\s+(\d+)', t.stdout)
        failed = int(m.group(1)) if m else -1
        print('%-70s %s failed=%d' % (name, 'KILLED' if failed > 0 else 'SURVIVED', failed))
        if failed <= 0:
            survivors.append(name)
    finally:
        shutil.copyfile(real[path], path)
print('survivors:', survivors)
