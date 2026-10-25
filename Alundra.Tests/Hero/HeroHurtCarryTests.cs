#nullable enable
using System;
using Xunit;
using CasaEngine.Framework.Scene.Entities;
using Alundra.Entities;
using Alundra.Hero;
using Alundra.Session;
using Alundra.Transitions;
using Alundra.Tests.Audio;
using Alundra.Tests.Support;

namespace Alundra.Tests.Hero;

/// <summary>
/// E20.c2b T2 (docs/plan-e20-heros.md section 1.12.1, test b3, and the witness b8): what the hurt block does to what the hero holds, at 1, 2 and 3 ticks per frame on <see cref="CarryRig"/> (the real hero controller, the crate of the original,
/// the world's tick passes). The expected values are those of the oracle (<c>docs/plan-e20-annexe/e20-c2b/oracle/out/C-carried.txt</c>, <c>A5.txt</c>): on the tick of the hurt <c>UpdatePlayerCarriedEntity(2)</c> runs BEFORE the facing is written (the object takes the
/// OLD facing, the code of throw -1 and the head of the hero), the entry does not clear the link (<c>+0x2C</c>): what lets the object go and breaks it is the object's own program (C23, E14); the charge counter and the pending-shot pointer are zeroed.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class HeroHurtCarryTests : IDisposable
{
    private readonly string _previousProjectPath = CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath;

    public HeroHurtCarryTests()
    {
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }

    public void Dispose()
    {
        CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath = _previousProjectPath;
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }

    public static TheoryData<int> TicksPerFrame => new() { 1, 2, 3 };

    private static Entity NewToucher(EntityScriptProxy hero, int dxPixels, out EntityScriptProxy proxy)
    {
        var entity = new Entity { Name = "toucher", GameplayProxyClassName = typeof(EntityScriptProxy).FullName! };
        entity.Initialize();
        proxy = (EntityScriptProxy)entity.GameplayProxy;
        proxy.PosX = hero.PosX + dxPixels * CarryRig.Px;
        proxy.PosY = hero.PosY;
        return entity;
    }

    /// <summary>
    /// The hero holds the crate (the stand 0x0C) facing east; the hit pass of the tick before the returned one has touched him (the toucher stands 20 px east): the tick returned is the tick of the hurt. The touch is taken away after it (the counters pass).
    /// </summary>
    private static (CarryRig Rig, int HurtTick) HeldAndTouched(int ticksPerFrame, bool crateProgram)
    {
        var rig = CarryRig.Build(ticksPerFrame, heroX: 178f, heroY: 100f);
        rig.RunLiftableProgram = crateProgram;
        rig.AddCrate(178, 100, 32); // on the head of the hero.
        rig.Run(3);
        rig.LiftNow(fromTheHead: true);
        rig.Run(4);
        Assert.Equal(CarryRules.CarryStillState, rig.Hero.TargetAnimationId);
        Assert.Same(rig.Crate!.LogicContextEntity, rig.Hero.CarriedEntity);
        Assert.Equal(24u, rig.Hero.TargetDirection);

        var toucher = NewToucher(rig.Hero, 20, out _);
        var hurtTick = rig.Tick + 3;
        rig.AfterMovePlayer = tick =>
        {
            if (tick == hurtTick - 1)
            {
                rig.Hero.TouchingEntity = toucher;
            }
            else if (tick == hurtTick)
            {
                rig.Hero.TouchingEntity = null;
            }
        };
        rig.Run(2);
        Assert.Equal(CarryRules.CarryStillState, rig.Hero.TargetAnimationId); // not hurt yet.
        return (rig, hurtTick);
    }

    [Theory]
    [MemberData(nameof(TicksPerFrame))]
    public void B3_TheObjectInTheHands_TakesTheOldFacingAndTheCodeMinus1_AtTheHurt_TheEntryDoesNotClearTheLink(int ticksPerFrame)
    {
        var (rig, hurtTick) = HeldAndTouched(ticksPerFrame, crateProgram: false);
        var hero = rig.Hero;
        var crate = rig.Crate!;

        rig.Run(1);

        Assert.Equal(hurtTick, rig.Tick);
        Assert.Equal(PlayerManager.HurtAnimationId, hero.TargetAnimationId);
        Assert.Equal(8u, hero.TargetDirection); // away from the toucher, which stands east.
        Assert.Equal(-1, crate.Flags2); // the code of throw -1 (C-carried: object +0x24)...
        Assert.Equal(24u, crate.TargetDirection); // ...and the facing of the hero from BEFORE the tick (C-carried: object +0x8C = 24, the hero's new one is 8): mode 2 runs before the facing is written,
        Assert.Same(crate.LogicContextEntity, hero.CarriedEntity); // the entry does not clear +0x2C (C-carried: the link stays),
        Assert.Equal(0, hero.CarryLockCounter);
        Assert.Equal(PlayerManager.HurtInvulnerabilityTicks, hero.DamagedTickCounter);
    }

    [Theory]
    [MemberData(nameof(TicksPerFrame))]
    public void B3_WithTheProgramOfTheObject_TheObjectIsLetGoAtTheHurt_Animation3_TheLinksCleared_TheHeroHurt(int ticksPerFrame)
    {
        var (rig, hurtTick) = HeldAndTouched(ticksPerFrame, crateProgram: true);
        var hero = rig.Hero;
        var crate = rig.Crate!;

        rig.Run(1);

        // As deep water does (W8): mode 2 writes the code -1, the program C23 of the same tick lets the object go (animation 3, the code back to 0, the two links cleared, the flags 0x332374); the hero is hurt, not floating.
        Assert.Equal(hurtTick, rig.Tick);
        Assert.Equal(PlayerManager.HurtAnimationId, hero.TargetAnimationId);
        Assert.Null(hero.CarriedEntity);
        Assert.Equal(3u, crate.TargetAnimationId);
        Assert.Null(crate.PlatformEntity);
        Assert.Null(crate.RidingEntity);
        Assert.Equal(0x332374u, crate.Flags);
        Assert.Equal(0, crate.Flags2);
    }

    // -----------------------------------------------------------------------------------------
    // The charge and the pending shot are zeroed (A5-charge: the charge counter 48 at tick 70, 0 at the hurt tick 71 and after).
    // -----------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(TicksPerFrame))]
    public void B3_TheChargeCounterAndThePendingShotPointer_AreZeroedByTheHurtTick(int ticksPerFrame)
    {
        var rig = CarryRig.Build(ticksPerFrame, heroX: 178f, heroY: 100f);
        rig.Run(3);
        var hero = rig.Hero;
        var toucher = NewToucher(hero, 20, out _);
        var hurtTick = rig.Tick + 2;
        rig.AfterMovePlayer = tick =>
        {
            if (tick == hurtTick - 1)
            {
                hero.TouchingEntity = toucher;
                hero.WeaponChargeTimer = 48; // the oracle: the charge counter 48 at the tick of the touch,
                hero.WeaponRecordPointer = 0x80128258u; // and a pending shot (the harness write of A5-charge-wptr).
            }
            else if (tick == hurtTick)
            {
                hero.TouchingEntity = null;
            }
        };

        rig.Run(1);
        Assert.Equal(48, hero.WeaponChargeTimer); // the tick before the hurt does not clear them (nothing charges here: the harness posed the words after MovePlayer).
        Assert.NotEqual(0u, hero.WeaponRecordPointer);

        rig.Run(1);

        Assert.Equal(PlayerManager.HurtAnimationId, hero.TargetAnimationId);
        Assert.Equal(0, hero.WeaponChargeTimer);
        Assert.Equal(0u, hero.WeaponRecordPointer);
    }

    // -----------------------------------------------------------------------------------------
    // (b8) the witnesses: a montage without a hit pass never hurts the hero.
    // -----------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(TicksPerFrame))]
    public void B8_AMontageWithoutAHitPass_NeverHurtsTheHero_TheEntryAnswersMinus1(int ticksPerFrame)
    {
        // Nothing touches the hero (no hit pass, no hazard cell): thirty ticks of walk. The entry must answer -1 and write nothing, or every direct call of MovePlayer of the suite (32 of them) would end in the hurt state.
        var rig = CarryRig.Build(ticksPerFrame, hold: _ => PadState.Right, heroX: 120f, heroY: 100f);

        rig.Run(30);

        var hero = rig.Hero;
        Assert.NotEqual(PlayerManager.HurtAnimationId, hero.TargetAnimationId);
        Assert.Equal(1u, hero.TargetAnimationId); // walking east,
        Assert.Equal(24u, hero.TargetDirection);
        Assert.Equal(0, hero.DamagedTickCounter);
        Assert.True(hero.PosX > 120 * CarryRig.Px + 10 * CarryRig.Px);
    }
}
