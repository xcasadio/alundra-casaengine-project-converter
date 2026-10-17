#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using CasaEngine.Framework.Rendering.EffectQuads;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Xunit;
using Alundra.Session;
using Alundra.Hero;
using Alundra.Rng;
using Alundra.Rendering.Effects;
using Alundra.Transitions;

namespace Alundra.Tests;

/// <summary>
/// E20.f1b (docs/plan-e20-heros.md section 1.9, ADR-0057, ADR-0059) on the REAL world of map 14 (<see cref="HitArena"/>, the production path, the steel sword): the depth key of the real hero as an effect anchor, the frames of
/// several ticks (the aura is created at once and read at the tick, the release is in the order of the ticks, the renewal at three ticks per frame), and the draw order of the aura against the hero's sprite with the engine's own
/// <see cref="DepthSortable2DComponent.BuildSortKey(Vector3)"/>.
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraHeroAuraWorldTests : IDisposable
{
    private const uint Square = AlundraPadState.Square;

    private readonly string _previousProjectPath = CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath;
    private readonly ulong _previousSeed = AlundraRandom.RandomSeed;

    public AlundraHeroAuraWorldTests()
    {
        GameState.Instance.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests();
    }

    public void Dispose()
    {
        AlundraNativeEffects.CreatedForTests = null;
        AlundraRandom.RandomSeed = _previousSeed;
        CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath = _previousProjectPath;
        GameState.Instance.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests();
    }

    private static HitArena Arena(string name, int x = 212, int y = 96)
    {
        var arena = new HitArena("f1b-" + name, x, y, new List<int[]>());
        arena.Arc.Give(2);
        arena.Arc.Equip(1);
        return arena;
    }

    private static List<AlundraEffectSlot> Auras(HitArena arena)
        => arena.Arc.Proxy.EffectStage!.Reservoir.ActiveSlots
            .Where(s => s.TargetEffect == AlundraPlayerManager.AuraEffect && !s.TargetMapTable && ReferenceEquals(s.Anchor, arena.Hero) && s.Mode == 1).ToList();

    /// <summary>
    /// The anchor of the REAL hero of the world, at the oracle's pixel (100, 100) facing east, steel sword, Square held from the tick 1: on every tick of the swing and of the charge (ticks 1 to 90, the oracle's sprites: IDSV 4 during the
    /// swing 0x3F, 5 once idle) the key the proxy exposes as an effect anchor is the sort driver's, 0x00680000 then 0x00690000, and the exposed Z is the binary's (the DLL's plus one raw unit).
    /// </summary>
    [Fact]
    public void TheHeroAnchor_ExposesTheKeyOfTheSortDriver_OnTheTicksOfTheSwingAndOfTheCharge()
    {
        var rows = ChargeOracleKeys();
        using var arena = Arena("key", 100, 100);
        IAlundraEffectAnchor anchor = arena.Hero;
        arena.Begin();

        for (var tick = 1; tick <= 90; tick++)
        {
            arena.Frame(Square, 1);
            var oracle = rows[tick - 1];
            Assert.Equal(oracle.Y, anchor.PosY);
            Assert.Equal(oracle.Z, anchor.PosZ); // 1: the binary's frame, the DLL's PosZ is 0.
            Assert.True(oracle.Key == anchor.DepthKey, $"tick {tick}: oracle {oracle.Key:X8}, anchor {anchor.DepthKey:X8}");
        }

        Assert.Equal(0x00690000u, anchor.DepthKey);
        Assert.Equal(2, anchor.Status); // Normal.
    }

    private static List<(int Y, int Z, uint Key)> ChargeOracleKeys()
    {
        var rows = new List<(int, int, uint)>();
        foreach (var line in System.IO.File.ReadAllLines(System.IO.Path.Combine(ChargeOracle.Folder(), "keyprobe.txt")))
        {
            var m = System.Text.RegularExpressions.Regex.Match(line, @"y=(-?\d+) z=(-?\d+) key=([0-9A-F]{8})");
            Assert.True(m.Success, line);
            rows.Add((int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), Convert.ToUInt32(m.Groups[3].Value, 16)));
        }

        return rows;
    }

    // -----------------------------------------------------------------------------------------
    // Frames of several ticks.
    // -----------------------------------------------------------------------------------------

    private sealed record Snapshot(int Index, int Mode, int Set, int Frame, int Counter, bool Destroying, int X, int Y, int Z, int Generation);

    private static List<Snapshot> State(HitArena arena)
        => arena.Arc.Proxy.EffectStage!.Reservoir.ActiveSlots
            .Select(s => new Snapshot(s.Index, s.Mode, s.CurrentImageSetIndex, s.FrameIndex, s.Counter, s.Destroying, s.X, s.Y, s.Z, 0)).ToList();

    private static List<Snapshot> RunGrouped(string name, int lastTick, params int[] frameSizes)
    {
        using var arena = Arena(name);
        AlundraRandom.RandomSeed = 0x12345678;
        arena.Begin();
        var tick = 0;
        var sizeIndex = 0;
        while (tick < lastTick)
        {
            var size = sizeIndex < frameSizes.Length ? frameSizes[sizeIndex++] : 1;
            arena.Frame(Square, size);
            tick += size;
        }

        Assert.Equal(lastTick, tick);
        Assert.Single(Auras(arena));
        return State(arena);
    }

    /// <summary>A frame of two ticks then a frame of three over the passage of the counter from 10 to 12 (the aura is created at the tick 33) leaves the same state as frames of one tick: one aura, and the same updates received by every slot.</summary>
    [Fact]
    public void AFrameOfTwoThenThreeTicks_OverTheCounterPassingFrom10To12_GivesTheSameStateAsFramesOfOneTick()
    {
        var singles = RunGrouped("n1", 40);
        var grouped = RunGrouped("n23", 40, Enumerable.Repeat(1, 31).Concat(new[] { 2, 3 }).ToArray()); // ticks 1-31, (32, 33), (34, 35, 36), then singles to 40.

        Assert.Equal(singles, grouped);
    }

    /// <summary>The same over a frame of three ticks that holds the creation (33) as its first, second and last tick: the aura is created once, with the same updates.</summary>
    [Theory]
    [InlineData(31)]
    [InlineData(32)]
    [InlineData(33)]
    public void AFrameOfThreeTicks_HoldingTheCreationOfTheAura_GivesTheSameStateAsFramesOfOneTick(int firstTickOfTheFrame)
    {
        var singles = RunGrouped("c1", 40);
        var grouped = RunGrouped("c3", 40, Enumerable.Repeat(1, firstTickOfTheFrame - 1).Concat(new[] { 3 }).ToArray());

        Assert.Equal(singles, grouped);
    }

    /// <summary>
    /// The renewal at one tick per frame: the aura of the first creation (tick 33) is renewed at the tick 130 (the animation lasts 96 ticks: the update 129 sets the flag, MovePlayer of 130 recreates). Returns the arena at the
    /// tick <paramref name="until"/> (frames of one tick), the aura being the one created at 33. The first renewal is the one where the aura holds the lowest free slot of the pool, which the spark born at 131 takes in a frame of three ticks.
    /// </summary>
    private HitArena ArenaAtTheTickBeforeTheRenewal(string name, int until, out List<int> creationTicks)
    {
        var arena = Arena(name);
        AlundraRandom.RandomSeed = 0x12345678;
        arena.Begin();
        var ticks = new List<int>();
        var lastGeneration = 0;
        for (var tick = 1; tick <= until; tick++)
        {
            arena.Frame(Square, 1);
            if (arena.Hero.AuraSlot != null && arena.Hero.AuraGeneration != lastGeneration)
            {
                ticks.Add(tick);
            }

            lastGeneration = arena.Hero.AuraGeneration;
        }

        creationTicks = ticks;
        return arena;
    }

    /// <summary>
    /// ADR-0059: a held charge in frames of three ticks, aligned so that c + 96 (129) is the first tick of a frame and a spark is born at c + 98 (131), c being the first creation (33, the oracle's C2-steel: renewal at 130, spark at 131): the frame frees the destroyed aura and then
    /// applies the spark on the freed slot. After the logic of the next frame a LIVE aura (effect 3, mode 1, anchored to the hero) is the hero's handle, and the handle carries its generation. Without the test of the generation
    /// in the condition of the creation the handle would keep pointing at the spark (a looping-less effect of 14 ticks) and no aura would exist.
    /// </summary>
    [Fact]
    public void TheRenewalInFramesOfThreeTicks_WhereASparkTakesTheFreedSlot_StillGivesALiveAura()
    {
        using var arena = ArenaAtTheTickBeforeTheRenewal("r3", 128, out var creations);
        Assert.Equal(new[] { 33 }, creations);

        arena.Frame(Square, 3); // the ticks 129, 130, 131
        var stage = arena.Arc.Proxy.EffectStage!;
        var handle = arena.Hero.AuraSlot;
        Assert.NotNull(handle);
        Assert.Equal(0, Auras(arena).Count); // the old aura was freed by the frame, and the spark of the tick 131 took its slot...
        Assert.Equal(AlundraNativeEffects.ChargeEffect, handle!.TargetEffect); // ... which the handle still points at: the stale handle of the frame.
        Assert.NotEqual(arena.Hero.AuraGeneration, handle.Generation);

        arena.Frame(Square, 3); // the ticks 132, 133, 134: the hero notices.

        var aura = arena.Hero.AuraSlot!;
        Assert.True(aura.Active);
        Assert.Equal((AlundraPlayerManager.AuraEffect, false, 1), (aura.TargetEffect, aura.TargetMapTable, aura.Mode));
        Assert.Same(arena.Hero, aura.Anchor);
        Assert.Equal(aura.Generation, arena.Hero.AuraGeneration);
        Assert.Single(Auras(arena));
        Assert.Same(aura, Auras(arena)[0]);
        Assert.True(stage.Reservoir.ActiveCount > 1); // the spark lives on.
    }

    /// <summary>
    /// The written deviation (1) of D-E20-28: a frame that holds c + 96 and c + 97 (here the ticks 129 and 130, a frame of two ticks) shows ONE frame without aura: the hero of the tick 130 does not see the flag that the update 129
    /// has not set yet, the frame frees the old aura, and the next frame creates the new one. Pinned: exactly one frame without aura in the whole run, the next one has its aura.
    /// </summary>
    [Fact]
    public void AFrameHoldingTheTicks96And97AfterACreation_ShowsOneFrameWithoutAura_TheNextOneHasIt()
    {
        using var arena = ArenaAtTheTickBeforeTheRenewal("gap", 128, out var creations);
        Assert.Equal(new[] { 33 }, creations);
        var framesWithoutAura = 0;

        arena.Frame(Square, 2); // 129, 130
        framesWithoutAura += Auras(arena).Count == 0 ? 1 : 0;
        Assert.Equal(0, Auras(arena).Count);

        arena.Frame(Square, 2); // 131, 132: the hero finds the slot inactive and recreates (ADR-0059).
        framesWithoutAura += Auras(arena).Count == 0 ? 1 : 0;
        Assert.Single(Auras(arena));

        for (var i = 0; i < 20; i++)
        {
            arena.Frame(Square, 2);
            framesWithoutAura += Auras(arena).Count == 0 ? 1 : 0;
        }

        Assert.Equal(1, framesWithoutAura);
    }

    // -----------------------------------------------------------------------------------------
    // B5: the draw order.
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// B5: every quad of the aura has a sort key strictly greater than the hero sprite's, in the same bucket (same pass, layer, order and elevation), computed with the engine's own
    /// <see cref="DepthSortable2DComponent.BuildSortKey(Vector3)"/> on the position of the hero's <see cref="RenderProjectionComponent"/>: the aura is drawn just above the hero (the binary: the anchor's key plus one), although
    /// its own position is 16 pixels higher (ADR-0049 alone would put it under the sprite).
    /// </summary>
    [Fact]
    public void TheAuraQuads_AreSortedStrictlyAfterTheHeroSprite_InTheSameBucket()
    {
        using var arena = Arena("draw", 100, 100);
        AlundraRandom.RandomSeed = 0x12345678;
        arena.Begin();
        for (var tick = 1; tick <= 50; tick++)
        {
            arena.Frame(Square, 1);
        }

        var hero = arena.Hero;
        var aura = hero.AuraSlot;
        Assert.NotNull(aura);
        var depthSortable = arena.Arc.HeroEntity.GetComponent<DepthSortable2DComponent>();
        var projection = arena.Arc.HeroEntity.GetComponent<RenderProjectionComponent>();
        Assert.NotNull(depthSortable);
        Assert.NotNull(projection);
        var heroKey = depthSortable!.BuildSortKey(projection!.WorldMatrixNoScale.Translation);

        var quads = new List<EffectQuad>();
        AlundraEffectQuadBuilder.Build(arena.Arc.Proxy.EffectStage!.Reservoir.Slots, false, 0, 0, quads);
        var auraQuads = quads.Where(q => q.SortKey.StableId == aura!.Index).ToList();

        Assert.NotEmpty(auraQuads);
        foreach (var quad in auraQuads)
        {
            Assert.Equal((heroKey.RenderPass, heroKey.SortingLayer, heroKey.OrderInLayer, heroKey.Elevation), (quad.SortKey.RenderPass, quad.SortKey.SortingLayer, quad.SortKey.OrderInLayer, quad.SortKey.Elevation));
            Assert.True(quad.SortKey.CompareTo(heroKey) > 0, $"aura {quad.SortKey} against hero {heroKey}");
        }
    }
}
