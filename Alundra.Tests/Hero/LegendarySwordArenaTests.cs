#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Alundra.Session;
using Alundra.Hero;
using Alundra.Rng;
using Alundra.Rendering.Effects;
using Alundra.Transitions;
using Alundra.Tests.Ai.Projectiles;
using Alundra.Tests.Support;
using Alundra.Tests.Audio;

namespace Alundra.Tests.Hero;

/// <summary>
/// E20.b2 T1 (docs/plan-e20-heros.md section 1.4.1, D-B2-4): the Legendary Sword (item 8, rank 4 of the slot 1) on the REAL world of map 14 (<see cref="HitArena"/>: the production path, the real hit pass, a real Beannoid
/// whose hit handler D1 takes the life through the equipment records the hero's tick fills). The sword is the basic sword's swing (record 11 = record 0: ground 0x10, air 0x14, not chargeable) plus the equipment attack of
/// the sheet 38 (power 50, attribute 6): 51 life against a Beannoid whose response is 0x10, 2 for the basic sword; and a held Square counts no charge, makes no aura and no wave, whatever the sword's rank. The expected
/// values are the oracle's (<c>docs/plan-e20-annexe/e20-b2/K2-legend.json</c>: 100 to 49) and the D2 cases of the basic sword (100 to 98), never read from the code under test.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class LegendarySwordArenaTests : IDisposable
{
    private const uint Square = PadState.Square;
    private const uint Cross = PadState.Cross;
    private const int BasicSword = 1;
    private const int LegendarySword = 8;
    private const int SteelEdge = 2;
    private const int SwordSlot = 1;

    private readonly string _previousProjectPath = CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath;
    private readonly ulong _previousSeed = GameRandom.RandomSeed;

    public LegendarySwordArenaTests()
    {
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }

    public void Dispose()
    {
        NativeEffects.CreatedForTests = null;
        GameRandom.RandomSeed = _previousSeed;
        CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath = _previousProjectPath;
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }

    /// <summary>The Beannoid 40 pixels east of the hero, with 100 life (the natural 9 would die before showing the damage), the native handlers on so that its D1 takes the life.</summary>
    private static HitArena Arena(string name, int item)
    {
        var arena = new HitArena("b2-" + name, 212, 96, new List<int[]> { new[] { 212 + 40, 96 } });
        arena.Arc.Give(item);
        arena.Arc.Equip(SwordSlot);
        arena.Targets[0].Hp = 100;
        arena.EnableNativeHandlers();
        return arena;
    }

    /// <summary>The swing on the ground: the Square at tick 1; returns the life 30 ticks later and the animation of the tick of the contact (the one that sets the 25 ticks of delay).</summary>
    private static (int Hp, uint ContactAnimation, int Record) Ground(HitArena arena)
    {
        uint contactAnimation = 0xFFFF;
        arena.Begin();
        for (var tick = 1; tick <= 30; tick++)
        {
            arena.Frame(tick == 1 ? Square : 0, 1);
            if (contactAnimation == 0xFFFF && arena.Samples[^1].Targets[0].CooldownTicks == 25)
            {
                contactAnimation = arena.Samples[^1].Hero.AnimTarget;
            }
        }

        return (arena.Targets[0].Hp, contactAnimation, arena.Hero.CurrentWeaponRecord);
    }

    /// <summary>The swing in the air: the jump at tick 1, the Square at tick 17 (the hero comes down, 5 pixels up at the tick of the contact); same returns.</summary>
    private static (int Hp, uint ContactAnimation, int Record) Air(HitArena arena)
    {
        uint contactAnimation = 0xFFFF;
        arena.Begin();
        for (var tick = 1; tick <= 40; tick++)
        {
            arena.Frame(tick == 1 ? Cross : tick == 17 ? Square : 0u, 1);
            if (contactAnimation == 0xFFFF && arena.Samples[^1].Targets[0].CooldownTicks == 25)
            {
                contactAnimation = arena.Samples[^1].Hero.AnimTarget;
            }
        }

        return (arena.Targets[0].Hp, contactAnimation, arena.Hero.CurrentWeaponRecord);
    }

    [Fact]
    public void TheLegendarySword_OnTheGround_SwingsTheBasicSwordsSwing_AndTakesFiftyOneLifeFromARealBeannoid()
    {
        using var arena = Arena("ground-8", LegendarySword);

        var (hp, contactAnimation, record) = Ground(arena);

        Assert.Equal(11, record); // the record of the item 8 (the table of the weapons: item 8 is the record 11).
        Assert.Equal(0x10u, contactAnimation); // the ground swing of the record 0, byte for byte.
        Assert.Equal(100 - 51, hp); // (1 + 50) * 16 >> 4, once: the delay of 25 ticks blocks a second contact.
    }

    [Fact]
    public void TheBasicSword_OnTheGround_TakesTwoLifeFromARealBeannoid()
    {
        using var arena = Arena("ground-1", BasicSword);

        var (hp, contactAnimation, record) = Ground(arena);

        Assert.Equal(0, record);
        Assert.Equal(0x10u, contactAnimation);
        Assert.Equal(100 - 2, hp); // the attack (1, 1) of the swing 0x10 plus the equipment attack of the sword 1: the oracle's D2 case of the basic sword.
    }

    [Fact]
    public void TheLegendarySword_InTheAir_SwingsThe0x14_AndTakesFiftyOneLifeFromARealBeannoid()
    {
        using var arena = Arena("air-8", LegendarySword);

        var (hp, contactAnimation, record) = Air(arena);

        Assert.Equal(11, record);
        Assert.Equal(0x14u, contactAnimation); // the air swing of the record 11 = the record 0.
        Assert.Equal(100 - 51, hp);
    }

    [Fact]
    public void TheBasicSword_InTheAir_SwingsThe0x14_AndTakesTwoLifeFromARealBeannoid()
    {
        using var arena = Arena("air-1", BasicSword);

        var (hp, contactAnimation, record) = Air(arena);

        Assert.Equal(0, record);
        Assert.Equal(0x14u, contactAnimation);
        Assert.Equal(100 - 2, hp);
    }

    /// <summary>The Square held for 90 ticks then released, a Beannoid-free arena: the highest value of the charge counter, the auras made, the charge effects (hold sparks and burst) created, whether a wave was born.</summary>
    private static (int MaxCharge, int Auras, int ChargeEffects, bool Wave) Hold(int item, string name)
    {
        var chargeEffects = 0;
        NativeEffects.CreatedForTests = slot =>
        {
            if (slot.TargetEffect == NativeEffects.ChargeEffect && !slot.TargetMapTable)
            {
                chargeEffects++;
            }
        };

        using var run = new WaveRun("b2-" + name, item, 24);
        var maxCharge = 0;
        var lastGeneration = 0;
        var auras = 0;
        run.Go(1, useHook: false, lastTick: 140, afterTick: _ =>
        {
            var hero = run.Arena.Hero;
            maxCharge = Math.Max(maxCharge, hero.WeaponChargeTimer);
            if (hero.AuraSlot != null && hero.AuraGeneration != lastGeneration)
            {
                auras++;
            }

            lastGeneration = hero.AuraGeneration;
        });

        return (maxCharge, auras, chargeEffects, run.Born is not null);
    }

    [Fact]
    public void AHeldSquare_WithTheLegendarySword_CountsNoCharge_MakesNoAuraAndNoWave()
    {
        var (maxCharge, auras, chargeEffects, wave) = Hold(LegendarySword, "hold-8");

        Assert.Equal(0, maxCharge);
        Assert.Equal(0, auras);
        Assert.Equal(0, chargeEffects); // no hold spark and no burst of 16.
        Assert.False(wave);
    }

    /// <summary>The control of the test above: the same hold with the Steel Edge (a chargeable sword) counts to 60, makes the aura, the sparks and the wave, so the four observables can see what the Legendary Sword must not do.</summary>
    [Fact]
    public void AHeldSquare_WithTheSteelEdge_ChargesAndMakesTheAuraTheSparksAndTheWave_TheControlOfTheLegendaryHold()
    {
        var (maxCharge, auras, chargeEffects, wave) = Hold(SteelEdge, "hold-2");

        Assert.Equal(PlayerManager.FullChargeTimer, maxCharge);
        Assert.True(auras >= 1, "the aura of the charge");
        Assert.True(chargeEffects >= 16, "the burst of the release");
        Assert.True(wave, "the wave of the steel sword");
    }
}
