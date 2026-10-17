#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Alundra.Scripts;
using CasaEngine.Framework.Rendering.EffectQuads;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Xunit;
using static Alundra.Tests.AlundraHeroChargeTests;
using Alundra.Session;
using Alundra.Hero;
using Alundra.Rng;
using Alundra.Rendering.Effects;

namespace Alundra.Tests;

/// <summary>
/// E20.f1b (docs/plan-e20-heros.md section 1.9, ADR-0057, ADR-0059): the aura of the charge, at one tick per frame on the montage of the charge (the steel sword, C2-steel), against the oracle
/// (<c>docs/plan-e20-annexe/e20-f1/oracle</c>: the binary's own <c>0x8003BE74</c> over a real pool): the creation at the counter 11, the renewal every 97 ticks of a held charge, the retry on a full reservoir, the release
/// below 11, and the stale handle of the original (D-E20-30). The frames of several ticks and the draw order are in <see cref="AlundraHeroAuraWorldTests"/>.
/// </summary>
public sealed class AlundraHeroAuraTests : IDisposable
{
    private const uint Square = AlundraPadState.Square;

    private readonly string _previousProjectPath = CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath;
    private readonly ulong _previousSeed = AlundraRandom.RandomSeed;

    public AlundraHeroAuraTests()
    {
        AlundraGameState.Instance.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests();
    }

    public void Dispose()
    {
        AlundraNativeEffects.CreatedForTests = null;
        AlundraRandom.RandomSeed = _previousSeed;
        CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath = _previousProjectPath;
        AlundraGameState.Instance.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests();
    }

    /// <summary>The auras alive in the reservoir: effect 3 of the global table anchored to the hero.</summary>
    private static List<AlundraEffectSlot> Auras(Montage m)
        => m.Stage.Reservoir.ActiveSlots.Where(s => s.TargetEffect == AlundraPlayerManager.AuraEffect && !s.TargetMapTable && ReferenceEquals(s.Anchor, m.Hero) && s.Mode == 1).ToList();

    /// <summary>Holds the Square for <paramref name="ticks"/> ticks of one tick per frame; <paramref name="beforeTick"/> is told the tick (1-based) before it runs. Returns the (tick, slot) of every creation of the aura.</summary>
    private static List<(int Tick, int Slot)> Hold(Montage m, int ticks, Action<int>? beforeTick = null, Action<int>? afterTick = null)
    {
        var creations = new List<(int, int)>();
        for (var tick = 1; tick <= ticks; tick++)
        {
            beforeTick?.Invoke(tick);
            m.Tick(Square);
            if (m.AuraCreatedThisTick is { } aura)
            {
                creations.Add((tick, aura.Index));
            }

            afterTick?.Invoke(tick);
        }

        return creations;
    }

    /// <summary>The oracle's A5 (C2-steel held 300 ticks, the lifetimes of the effects modelled): the aura is created at the ticks 33, 130 and 227, in the slots 0, 5 and 4 (the sparks and their lifetimes decide the free slots); once at the counter 11, then renewed every 97 ticks; one alive after each update.</summary>
    [Fact]
    public void AHeldCharge_RenewsTheAuraEvery97Ticks_AtTheTicks33_130_227_InTheSlotsOfTheOracle_OneAliveAfterEachUpdate()
    {
        using var seed = new SeedScope();
        var m = Start(2);
        var aliveAfter = new List<int>();

        var creations = Hold(m, 300, afterTick: tick => aliveAfter.Add(Auras(m).Count));

        Assert.Equal(new[] { (33, 0), (130, 5), (227, 4) }, creations);
        Assert.All(aliveAfter.Skip(32), count => Assert.Equal(1, count)); // from the tick 33 on, one aura after each update (the old one is freed by the pass of the tick that creates the new one).
        Assert.All(aliveAfter.Take(32), count => Assert.Equal(0, count));
        Assert.Equal(3, m.AuraCreations);
        Assert.Equal(0, m.AuraReleases);
    }

    /// <summary>The oracle's A1: the flag <c>+0x7D</c> of the aura set by hand before the tick 50 (the free pass modelled): a new aura at that tick, in the slot 1 (the old one still holds the slot 0 at the tick of the creation).</summary>
    [Fact]
    public void ADestroyFlagSetBeforeTheTick50_CreatesANewAuraAtThatTick_ScenarioA1()
    {
        using var seed = new SeedScope();
        var m = Start(2);

        var creations = Hold(m, 70, beforeTick: tick =>
        {
            if (tick == 50)
            {
                m.Hero.AuraSlot!.Destroying = true;
            }
        });

        Assert.Equal(new[] { (33, 0), (50, 1) }, creations);
        Assert.Single(Auras(m)); // the old one was freed by the pass of the tick 50.
    }

    /// <summary>The oracle's A2: the flag before the ticks 60 and 70: creations at 33, 60 and 70, the slot 0 reused at the tick 70.</summary>
    [Fact]
    public void ADestroyFlagSetBeforeTheTicks60And70_CreatesAuras_AndReusesTheSlot0_ScenarioA2()
    {
        using var seed = new SeedScope();
        var m = Start(2);

        var creations = Hold(m, 80, beforeTick: tick =>
        {
            if (tick is 60 or 70)
            {
                m.Hero.AuraSlot!.Destroying = true;
            }
        });

        Assert.Equal(new[] { (33, 0), (60, 1), (70, 0) }, creations);
        Assert.Single(Auras(m));
    }

    /// <summary>The oracle's P1: the reservoir is full from before the tick 30 to the tick 59: the creation of the aura fails at each tick from 33 and is retried at each tick (the handle stays null), then it succeeds as soon as there is room.</summary>
    [Theory]
    [InlineData(60)]
    [InlineData(45)]
    public void AFullReservoir_RetriesTheAuraAtEveryTick_UntilThereIsRoom_ScenarioP1(int freedBeforeTick)
    {
        using var seed = new SeedScope();
        var m = Start(2);
        var fillers = new List<AlundraEffectSlot>();

        var creations = Hold(m, 70, beforeTick: tick =>
        {
            if (tick == 30)
            {
                while (m.Stage.Reservoir.ActiveCount < AlundraEffectReservoir.SlotCount)
                {
                    fillers.Add(m.Stage.Reservoir.Create(false, 3, 1, 0, 0, 0)!);
                }
            }
            else if (tick == freedBeforeTick)
            {
                m.Stage.Reservoir.Free(fillers[0]);
            }

            if (tick is >= 33 and < 60 && tick < freedBeforeTick)
            {
                Assert.Null(m.Hero.AuraSlot); // the null result is the handle.
            }
        });

        Assert.Equal(new[] { (freedBeforeTick, 0) }, creations);
        Assert.Equal(1, m.AuraCreations);
    }

    /// <summary>The release below 11 (the oracle, C2-steel: the handle is cleared at the tick 92), and the slot is freed by the pass of that tick.</summary>
    [Fact]
    public void TheAura_IsReleasedBelowTheCounter11_TheHandleAtOnce_TheSlotAtTheTickOfTheRelease()
    {
        var rows = ChargeOracle.Load("C2-steel");
        using var seed = new SeedScope();
        var m = Start(2);
        AlundraEffectSlot? aura = null;

        foreach (var row in rows)
        {
            m.Tick(row.Hold, row.RngBefore);
            if (row.Tick == 91)
            {
                aura = m.Hero.AuraSlot;
                Assert.NotNull(aura);
                Assert.True(aura!.Active);
            }
            else if (row.Tick == 92)
            {
                Assert.Null(m.Hero.AuraSlot);
                Assert.False(aura!.Active); // the stage applied the release at the tick 92.
                Assert.Empty(Auras(m));
            }
        }
    }

    /// <summary>
    /// D-E20-30: the aura expired during a lock (its slot was freed by the pass of the effects while MovePlayer did not run), and another effect took the slot. The original frees whatever occupies the slot at the next release (a dangling
    /// pointer); the port frees the slot only if it still holds the aura.
    /// </summary>
    [Fact]
    public void AStaleHandle_AfterALock_DoesNotFreeTheEffectThatTookTheSlot()
    {
        using var seed = new SeedScope();
        var m = Start(2);
        Hold(m, 40);
        m.Tick(0); // the release edge of the Square: a released Square still counts, the counter is 41 and the aura stays.
        var aura = m.Hero.AuraSlot!;
        var generation = m.Hero.AuraGeneration;
        Assert.Equal(generation, aura.Generation);

        m.Stage.Reservoir.Free(aura); // the aura expired while the hero was locked (the lock clears the counter; the pass of the effects still runs).
        var other = m.Stage.Reservoir.Create(false, 3, 1, 0, 0, 0)!; // a looping effect takes the slot.
        Assert.Same(aura, other);
        Assert.NotEqual(generation, other.Generation);
        m.Hero.WeaponChargeTimer = 0;

        m.Tick(0); // the first normal tick: no Square, the counter is under 11, the aura is released.

        Assert.Null(m.Hero.AuraSlot);
        Assert.True(other.Active); // the other effect lives on.
    }

    /// <summary>The same stale handle while the charge goes on: the slot is reused, the hero notices that it no longer holds the aura and creates a new one (ADR-0059), without freeing the other effect.</summary>
    [Fact]
    public void AHandleWhoseSlotWasTaken_IsRecreatedWhileTheChargeGoesOn_AndTheOtherEffectLives()
    {
        using var seed = new SeedScope();
        var m = Start(2);
        Hold(m, 40);
        var aura = m.Hero.AuraSlot!;
        m.Stage.Reservoir.Free(aura);
        var other = m.Stage.Reservoir.Create(false, 3, 1, 0, 0, 0)!;
        Assert.Same(aura, other);

        m.Tick(Square);

        Assert.NotNull(m.AuraCreatedThisTick);
        Assert.NotSame(other, m.Hero.AuraSlot);
        Assert.Equal(m.Hero.AuraSlot!.Generation, m.Hero.AuraGeneration);
        Assert.True(other.Active);
    }
}
