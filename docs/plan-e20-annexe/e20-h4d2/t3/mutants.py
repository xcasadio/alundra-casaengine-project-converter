"""E20.h4d2 T3 mutant table: id -> (family, description, [(path, old, new), ...]).
Every edit must match exactly once (checked by run_mutants.py). Applied by script, undone after the run."""
FM = 'Alundra/Ai/Items/FireTome1Ai.cs'
ET = 'Alundra/Ai/Items/EarthTome1Ai.cs'
PM = 'Alundra/Hero/PlayerManager.cs'
NE = 'Alundra/Rendering/Effects/NativeEffects.cs'

M = {}


def m(mid, fam, desc, *edits):
    assert mid not in M
    M[mid] = (fam, desc, list(edits))


# ---------------------------------------------------------------- pilot (disc/report.md section 5, T3, "pilote")
m('D01', 'pilot', 'counter mask & 7 -> & 15', (FM, 'BallPeriodMask = 7;', 'BallPeriodMask = 15;'))
m('D02', 'pilot', 'draw range 32 -> 16', (FM, 'DirectionBound = 32;', 'DirectionBound = 16;'))
m('D03', 'pilot', 'ball Z + 16 px -> + 8 px', (FM, 'BallBirthLift = 0x100000;', 'BallBirthLift = 0x80000;'))
m('D04', 'pilot', 'ForceZ of the ball 0x80000 -> 0x40000', (FM, 'BallBirthForceZ = 0x80000;', 'BallBirthForceZ = 0x40000;'))
m('D05', 'pilot', '+0x274 = 0x10 -> 0x0F', (FM, 'BallBirthWait = 0x10;', 'BallBirthWait = 0x0F;'))
m('D06', 'pilot', '+0x278 (target slot) not cleared at the birth', (FM, '            ball.DelayOrAngleOrEntityId = 0;\n            ball.SpellTarget = null;\n        }\n\n        return StillInUse;', '            ball.SpellTarget = null;\n        }\n\n        return StillInUse;'))
m('D07', 'pilot', 'death of the column -> animation 0x33 (hold) instead of 0x34', (FM, '            hero.TargetAnimationId = PlayerManager.TomeEndAnimationId;\n            hero.ItemUsePhase = 2;', '            hero.TargetAnimationId = PlayerManager.TomeHoldAnimationId;\n            hero.ItemUsePhase = 2;'))
m('D08', 'pilot', 'identity ignored (only the missing pointer counts)', (FM, 'column is null || column.Status == EntityStatus.Destroyed || column.Slot < 0;', 'column is null;'))
m('D09', 'pilot', 'phase 1 without the guard', (FM, '        if (PlayerManager.TomeSlopeGuard(hero) != 0)\n        {\n            return Finished;\n        }\n\n        if (ColumnChanged', '        if (ColumnChanged'))
m('D10', 'pilot', 'no chaining of phase 1 at the birth', (FM, '        hero.ItemUsePhase = 1;\n        return Throw(hero);', '        hero.ItemUsePhase = 1;\n        return StillInUse;'))
m('D11', 'pilot', 'null column answers 0 instead of 1', (FM, '        if (column is null)\n        {\n            return Finished;', '        if (column is null)\n        {\n            return StillInUse;'))
m('D12', 'pilot', 'phase 2 waits for 0x33 instead of 0x34', (FM, 'hero.TargetAnimationId == PlayerManager.TomeEndAnimationId ? StillInUse : Finished', 'hero.TargetAnimationId == PlayerManager.TomeHoldAnimationId ? StillInUse : Finished'))
m('D13', 'pilot', 'guard accepts 0x34 (TomeSlopeGuard)', (PM, 'DeepWaterSlope && player.TargetAnimationId is TomeCastAnimationId or TomeHoldAnimationId)', 'DeepWaterSlope && player.TargetAnimationId is TomeCastAnimationId or TomeHoldAnimationId or TomeEndAnimationId)'))
m('D14', 'pilot', 'guard without the slope (TomeSlopeGuard)', (PM, 'if (player.Slope_18c != DeepWaterSlope && player.TargetAnimationId is TomeCastAnimationId or TomeHoldAnimationId)', 'if (player.TargetAnimationId is TomeCastAnimationId or TomeHoldAnimationId)'))

# ---------------------------------------------------------------- cancellation 0x80034D2C ("annulation")
m('X01', 'cancel', 'types 20 / 23 swapped in the cancellation', (FM, '            if (entity.SpriteType == ColumnType)\n            {\n                var x', '            if (entity.SpriteType == BallType)\n            {\n                var x'), (FM, '            else if (entity.SpriteType == BallType)\n            {\n                entity.Status = EntityStatus.Deactivated;', '            else if (entity.SpriteType == ColumnType)\n            {\n                entity.Status = EntityStatus.Deactivated;'))
m('X02', 'cancel', 'effect 19 -> 18', (NE, 'FireColumnBlastEffect = 19;', 'FireColumnBlastEffect = 18;'))
m('X03', 'cancel', 'effect variant 2 -> 1', (NE, 'reservoir.Create(false, FireColumnBlastEffect, 2, x, y, z);', 'reservoir.Create(false, FireColumnBlastEffect, 1, x, y, z);'))
m('X04', 'cancel', 'balls -> FlagToDestroy (status 4) instead of Deactivated (3)', (FM, 'entity.Status = EntityStatus.Deactivated;', 'entity.Status = EntityStatus.FlagToDestroy;'))
m('X05', 'cancel', '+0x20 (blocker) not cleared', (FM, '                entity.BlockedByEntity = null;\n', ''))
m('X06', 'cancel', 'loop exclusive of the last slot', (FM, 'foreach (var entity in world.SpawnedEntities)', 'foreach (var entity in System.Linq.Enumerable.SkipLast(world.SpawnedEntities, 1))'))
m('X07', 'cancel', 'status 3 balls also cancelled', (FM, '            if (entity.Status != EntityStatus.Normal)\n            {\n                continue;', '            if (entity.Status != EntityStatus.Normal && entity.Status != EntityStatus.Deactivated)\n            {\n                continue;'))
m('X08', 'cancel', 'balls cancelled by position instead of type 23 (any type but 20)', (FM, 'else if (entity.SpriteType == BallType)', 'else'))
m('X09', 'cancel', 'effect Z without the binary frame (+ 1)', (FM, 'var z = entity.PosZ + 1;', 'var z = entity.PosZ;'))
m('X10', 'cancel', 'column destroyed BEFORE the effect (declared equivalent: the effect takes coordinates)', (FM, '                hero.ScriptHost.EffectStage?.Emit(reservoir => NativeEffects.CreateFireColumnBlast(reservoir, x, y, z));\n                world.DestroyEntity(entity);', '                world.DestroyEntity(entity);\n                hero.ScriptHost.EffectStage?.Emit(reservoir => NativeEffects.CreateFireColumnBlast(reservoir, x, y, z));'))
m('X11', 'cancel', 'column destroyed with the break effect (two-argument destruction -1)', (FM, 'world.DestroyEntity(entity);', 'hero.ScriptHost.DestroyEntity(entity, -1);'))

# ---------------------------------------------------------------- E3 (0x8007EDA0)
m('E01', 'e3', 'without | 0x40', (ET, '(entity.Flags | 0x40u) & ~0x30u', '(entity.Flags) & ~0x30u'))
m('E02', 'e3', '~0x10 instead of ~0x30', (ET, '(entity.Flags | 0x40u) & ~0x30u', '(entity.Flags | 0x40u) & ~0x10u'))
m('E03', 'e3', 'destroy without the end of the animation', (ET, 'entity.TargetAnimationId == ImpactAnimation && entity.ForceResetAnimationFlag == 1', 'entity.TargetAnimationId == ImpactAnimation'))
m('E04', 'e3', 'destroy when the end of the animation alone', (ET, 'entity.TargetAnimationId == ImpactAnimation && entity.ForceResetAnimationFlag == 1', 'entity.ForceResetAnimationFlag == 1'))
m('E05', 'e3', 'animation set to 2 instead of 1', (ET, 'ImpactAnimation = 1;', 'ImpactAnimation = 2;'))
m('E06', 'e3', '(E, 3) encore {16}', (ET, 'SpriteTypes = { 16, 23 };', 'SpriteTypes = { 16 };'))
m('E07', 'e3', 'E3 widened to 24', (ET, 'SpriteTypes = { 16, 23 };', 'SpriteTypes = { 16, 23, 24 };'))

# ---------------------------------------------------------------- C63 (0x8007AC60)
m('C01', 'c63', 'delay not decremented', (FM, '            TomeWords.Write274(ball, unchecked(wait - 1));\n', ''))
m('C02', 'c63', 'delay decremented by 2', (FM, 'unchecked(wait - 1)', 'unchecked(wait - 2)'))
m('C03', 'c63', 'ForceZ -3 px -> -2 px', (FM, 'FallForceZ = unchecked((int)0xFFFD0000);', 'FallForceZ = unchecked((int)0xFFFE0000);'))
m('C04', 'c63', 'ForceZ written before the delay test', (FM, '        var wait = TomeWords.Read274(ball);\n        if (wait != 0)\n        {', '        var wait = TomeWords.Read274(ball);\n        ball.ForceZ = FallForceZ;\n        if (wait != 0)\n        {'))
m('C05', 'c63', 'no-target draw 9 -> 8', (FM, 'NoTargetWaitBound = 9;', 'NoTargetWaitBound = 8;'))
m('C06', 'c63', 'no-target wait stored without the draw', (FM, 'TomeWords.Write274(ball, EnemyHelpers.Below(NoTargetWaitBound));', 'TomeWords.Write274(ball, 4);'))
m('C07', 'c63', 'type & 0xF -> & 0x7', (FM, '(ball.CurrentAttack?.Attribute ?? 0) & 0xF;', '(ball.CurrentAttack?.Attribute ?? 0) & 0x7;'))
m('C08', 'c63', 'mask ClassA 1 -> 5', (FM, 'EntityFlags.ClassA, type', '5u, type'))
m('C09', 'c63', 'identity not stored', (FM, '            ball.DelayOrAngleOrEntityId = target.Slot;\n', ''))
m('C10', 'c63', 'dead target not forgotten (the status is not tested)', (FM, 'ball.SpellTarget is not { Status: EntityStatus.Normal } kept', 'ball.SpellTarget is not { } kept'))
m('C11', 'c63', 'target forgotten only when its status is 3 or more... kept when 2 or 3', (FM, 'ball.SpellTarget is not { Status: EntityStatus.Normal } kept', 'ball.SpellTarget is not { Status: EntityStatus.Normal or EntityStatus.Deactivated } kept'))
m('C12', 'c63', '+0x274 = 2 -> 3 after a turn', (FM, 'TurnWait = 2;', 'TurnWait = 3;'))
m('C13', 'c63', 'turn step 4 -> 3', (FM, 'TurnStep = 4;', 'TurnStep = 3;'))
m('C14', 'c63', 'turn threshold < 4 -> < 3', (FM, 'next = current - wanted < TurnStep ? wanted : current - TurnStep;', 'next = current - wanted < 3 ? wanted : current - TurnStep;'), (FM, 'next = wanted - current < TurnStep ? wanted : current + TurnStep;', 'next = wanted - current < 3 ? wanted : current + TurnStep;'))
m('C15', 'c63', 'turn threshold <= 4 (declared equivalent: the equal case is the step itself)', (FM, 'next = current - wanted < TurnStep ? wanted : current - TurnStep;', 'next = current - wanted <= TurnStep ? wanted : current - TurnStep;'), (FM, 'next = wanted - current < TurnStep ? wanted : current + TurnStep;', 'next = wanted - current <= TurnStep ? wanted : current + TurnStep;'))
m('C16', 'c63', 'turn up step 4 -> 5', (FM, 'next = wanted - current < TurnStep ? wanted : current + TurnStep;', 'next = wanted - current < TurnStep ? wanted : current + 5;'))
m('C17', 'c63', 'first result: last instead of first', (FM, 'target = found[0];', 'target = found[found.Count - 1];'))
m('C18', 'c63', '+0x274 = 2 also when the direction is unchanged', (FM, '        if (wanted == current)\n        {\n            return;', '        if (wanted == current)\n        {\n            TomeWords.Write274(ball, TurnWait);\n            return;'))
m('C19', 'c63', 'turn deferred: no turn in the acquisition tick', (FM, '            ball.SpellTarget = target;\n        }\n        else', '            ball.SpellTarget = target;\n            return;\n        }\n        else'))

# ---------------------------------------------------------------- the more ("Plus")
m('P01', 'plus', '(C, 63) also registered for 17', (FM, 'SpriteTypes = { 23 };', 'SpriteTypes = { 23, 17 };'))
SHORT = ('        int next;\n        if (wanted < current)\n        {\n            next = current - wanted < TurnStep ? wanted : current - TurnStep;\n        }\n        else\n        {\n            next = wanted - current < TurnStep ? wanted : current + TurnStep;\n        }\n')
m('P02', 'plus', 'turn by the shortest way, wrapped (the refused correction)', (FM, SHORT, '        int next;\n        var diff = (wanted - current) & 31;\n        if (diff < TurnStep || diff > 32 - TurnStep)\n        {\n            next = wanted;\n        }\n        else if (diff < 16)\n        {\n            next = (current + TurnStep) & 31;\n        }\n        else\n        {\n            next = (current - TurnStep) & 31;\n        }\n'))
m('P03', 'plus', 'wrap in the d < cur branch: ((d - cur) & 31) < 4', (FM, 'next = current - wanted < TurnStep ? wanted : current - TurnStep;', 'next = ((wanted - current) & 31) < TurnStep ? wanted : current - TurnStep;'))
m('P04', 'plus', 'wrap in the d > cur branch: ((cur - d) & 31) < 4', (FM, 'next = wanted - current < TurnStep ? wanted : current + TurnStep;', 'next = ((current - wanted) & 31) < TurnStep ? wanted : current + TurnStep;'))
m('P05', 'plus', 'count fixed to 63 in the binary-mode entry (the count given is ignored)', (FM, '        var wait = TomeWords.Read274(ball);\n        if (wait != 0)', '        count = 63;\n        var wait = TomeWords.Read274(ball);\n        if (wait != 0)'))
m('P06', 'plus', 'the query is called despite a target', (FM, 'if (ball.DelayOrAngleOrEntityId == 0)\n        {\n            var type', 'if (ball.DelayOrAngleOrEntityId == 0 || ball.SpellTarget is not null)\n        {\n            var type'))
m('P07', 'plus', 'target remembered by its slot alone (looked up in the table)', (FM, 'ball.SpellTarget is not { Status: EntityStatus.Normal } kept', 'System.Linq.Enumerable.FirstOrDefault(entities, e => e.Slot == ball.DelayOrAngleOrEntityId) is not { Status: EntityStatus.Normal } kept'))
m('P08', 'plus', 'ItemSpawnedEntity tested by Slot < 0 alone', (FM, 'column is null || column.Status == EntityStatus.Destroyed || column.Slot < 0;', 'column is null || column.Slot < 0;'))
m('P09', 'plus', 'ItemSpawnedEntity tested by the Destroyed status alone', (FM, 'column is null || column.Status == EntityStatus.Destroyed || column.Slot < 0;', 'column is null || column.Status == EntityStatus.Destroyed;'))
m('P10', 'plus', 'death of the column read one tick too early (FlagToDestroy counts)', (FM, 'column is null || column.Status == EntityStatus.Destroyed || column.Slot < 0;', 'column is null || column.Status == EntityStatus.Destroyed || column.Status == EntityStatus.FlagToDestroy || column.Slot < 0;'))
m('P11', 'plus', 'birth of the ball before the guard', (FM, '    private static int Throw(EntityScriptProxy hero)\n    {\n        if (PlayerManager.TomeSlopeGuard(hero) != 0)\n        {\n            return Finished;\n        }\n', '    private static int Throw(EntityScriptProxy hero)\n    {\n        if ((hero.ItemUseCounter & BallPeriodMask) == 0)\n        {\n            (hero.ScriptHost as IEntityWorldContext)?.SpawnEntityBySpriteIndex(hero, false, BallType, hero.PosX, hero.PosY, hero.PosZ + BallBirthLift, EnemyHelpers.Below(DirectionBound));\n        }\n\n        if (PlayerManager.TomeSlopeGuard(hero) != 0)\n        {\n            return Finished;\n        }\n'))
m('P12', 'plus', 'C63 run at the tick of the birth', (FM, '            ball.SpellTarget = null;\n        }\n\n        return StillInUse;', '            ball.SpellTarget = null;\n            BallTick(ball, (IEntityWorldContext)hero.ScriptHost!);\n        }\n\n        return StillInUse;'))

# ---------------------------------------------------------------- the critic's 18 (critic/results/extra_mutants_vs_vectors.txt), the ones with a C# analogue
m('K01', 'critic', 'counter read AFTER the increment (c+1) & 7', (FM, '(hero.ItemUseCounter & BallPeriodMask) != 0', '((hero.ItemUseCounter + 1) & BallPeriodMask) != 0'))
m('K02', 'critic', 'draw consumed only if the ball is born', (FM, '        var direction = EnemyHelpers.Below(DirectionBound);\n        var ball = (hero.ScriptHost as IEntityWorldContext)?.SpawnEntityBySpriteIndex(hero, false, BallType, hero.PosX, hero.PosY, hero.PosZ + BallBirthLift, direction);\n        if (ball is not null)\n        {\n', '        var ball = (hero.ScriptHost as IEntityWorldContext)?.SpawnEntityBySpriteIndex(hero, false, BallType, hero.PosX, hero.PosY, hero.PosZ + BallBirthLift, 0);\n        if (ball is not null)\n        {\n            ball.TargetDirection = (uint)EnemyHelpers.Below(DirectionBound);\n'))
m('K03', 'critic', 'column pointer stored only when not null', (FM, '        hero.ItemSpawnedEntity = column;\n        if (column is null)', '        if (column is not null) { hero.ItemSpawnedEntity = column; }\n        if (column is null)'))
m('K04', 'critic', 'phase advanced before the identity is stored (the identity of the previous cast is seen)', (FM, '        hero.ItemSpawnedEntity = column;\n        if (column is null)\n        {\n            return Finished;\n        }\n\n        hero.ItemUsePhase = 1;\n        return Throw(hero);', '        if (column is null)\n        {\n            hero.ItemSpawnedEntity = column;\n            return Finished;\n        }\n\n        hero.ItemUsePhase = 1;\n        var answer = Throw(hero);\n        hero.ItemSpawnedEntity = column;\n        return answer;'))
m('K05', 'critic', 'guard result ignored in phase 1 (cancel done, the spell goes on)', (FM, '        if (PlayerManager.TomeSlopeGuard(hero) != 0)\n        {\n            return Finished;\n        }\n\n        if (ColumnChanged', '        PlayerManager.TomeSlopeGuard(hero);\n\n        if (ColumnChanged'))
m('K06', 'critic', 'guard result of phase 0 ignored', (FM, '        var guard = PlayerManager.TomeSlopeGuard(hero);\n        if (guard != 0)\n        {\n            return guard;\n        }\n', '        PlayerManager.TomeSlopeGuard(hero);\n'))
m('K07', 'critic', 'column of phase 0 born at hero Z + 16 px', (FM, 'ColumnType, hero.PosX, hero.PosY, hero.PosZ, 0);', 'ColumnType, hero.PosX, hero.PosY, hero.PosZ + BallBirthLift, 0);'))
m('K08', 'critic', 'column born with the direction of the hero', (FM, 'ColumnType, hero.PosX, hero.PosY, hero.PosZ, 0);', 'ColumnType, hero.PosX, hero.PosY, hero.PosZ, (int)hero.TargetDirection);'))
