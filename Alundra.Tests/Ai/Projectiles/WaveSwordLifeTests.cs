#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;
using Alundra.Maps;
using Alundra.Session;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Ai.Objects;
using Alundra.Ai.Projectiles;
using Alundra.Hero;
using Alundra.Rng;
using Alundra.Rendering.Effects;
using Alundra.Transitions;
using Alundra.Tests.Support;
using Alundra.Tests.Audio;

namespace Alundra.Tests.Ai.Projectiles;

/// <summary>
/// E20.f3c phase A (docs/plan-e20-heros.md section 1.19.3, T1; D-F3-5, D-F3-6): the life of the sword wave (the sprite types 27, 28, 29 of the global page) born by the TEST HOOK
/// (<see cref="PlayerManager.WeaponSpawnBirthHookForTests"/>, which calls <see cref="IEntityWorldContext.SpawnEntityBySpriteIndex"/> from the exact place of <c>MovePlayer</c> where the shot of the hero leaves, the tick of
/// the birth included) in the real world of map 14 (<see cref="HitArena"/>), against the oracle of <c>docs/plan-e20-annexe/e20-f3/oracle/out</c> read by key (<see cref="WaveOracle"/>; the arena releases at tick 91, the oracle at 96).
/// Nothing is born in production in this phase (the set of wired types stays empty). (v1) E10 destroys the wave at S+26; (v2) C61 calls the named stub at S+1 to S+25; (v3) the registry and the header; (v4) the whole life
/// of the wave, at 1, 2 and 3 ticks per frame, for the three swords.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class WaveSwordLifeTests : IDisposable
{
    private const int SteelItem = 2;
    private const int DemonItem = 3;
    private const int HolyItem = 4;
    private const uint East = 24;
    private const uint North = 16;
    private const uint West = 8;
    private const uint South = 0;

    private readonly string _previousProjectPath = CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath;
    private readonly ulong _previousSeed = GameRandom.RandomSeed;
    private readonly ITestOutputHelper _output;

    public WaveSwordLifeTests(ITestOutputHelper output)
    {
        _output = output;
        Reset();
    }

    public void Dispose() => Reset();

    private void Reset()
    {
        PlayerManager.WeaponSpawnBirthHookForTests = null;
        PlayerManager.WiredWeaponTypesForTests = null;
        WaveSwordAi.StubCallsForTests = null;
        NativeEffects.CreatedForTests = null;
        GameRandom.RandomSeed = _previousSeed;
        CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath = _previousProjectPath;
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (v1) E10: the wave is destroyed at S+26, its slot is free at S+27 and taken again by the next apparition, no break effect; with the native handlers on AND off (D-F3-5).
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void V1_E10_DestroysTheWaveAtSPlus26_TheSlotIsFreeAtSPlus27AndTakenAgain_NoBreakEffect_WhateverTheNativeSwitch(bool nativeHandlers)
    {
        using var run = new WaveRun("v1-" + nativeHandlers, SteelItem, East);
        run.Arena.Arc.Runner.NativeHandlersEnabled = nativeHandlers;
        var tick = 0;
        var creationTicks = new List<int>();
        NativeEffects.CreatedForTests = _ => creationTicks.Add(tick);
        run.Arena.BeforeTick = t => tick = t;
        var statusAt = new Dictionary<int, string>();
        var slotDuringTheLife = -2; // read while the wave is alive (at S): once it is destroyed its slot is given back (Slot = -1).
        var slotHeldAt = new Dictionary<int, bool>();
        var breakSlots = 0; // the active effect slots of the break effect 8 around the death: E10 has the body of E1 and the header of the wave has BreakEffect 0, so none (the mutant "destruction with the effect 8" creates one).

        run.Go(1, useHook: true, afterTick: t =>
        {
            if (t is >= 112 + 26 and <= 112 + 28)
            {
                breakSlots += run.Arena.Arc.Proxy.EffectStage!.Reservoir.ActiveSlots.Count(s => s.TargetEffect == 8);
            }

            if (t is >= 111 and <= 140)
            {
                statusAt[t] = run.Born is null ? "none" : run.Arena.Arc.Entities.Contains(run.Born) ? ((int)run.Born.Status).ToString() : "free";
            }

            if (t == 112 && run.Born is not null)
            {
                slotDuringTheLife = run.Born.Slot;
            }

            if (slotDuringTheLife >= 0 && t is >= 112 + 25 and <= 112 + 27)
            {
                slotHeldAt[t] = run.Arena.Arc.Proxy.SlotTable.IsHeld(slotDuringTheLife);
            }
        });

        var second = ((IEntityWorldContext)run.Arena.Arc.Proxy).SpawnEntityBySpriteIndex(run.Arena.Hero, false, 27, 0, 0, 0, 0x18);
        var burstTick = creationTicks.Count(t => t == 91);
        var createdAroundTheDeath = creationTicks.Count(t => t >= 112 + 25 && t <= 112 + 27);

        Assert.Equal(
            ("none", "2", "2", "4", "free", true, true, true, false, true, 0, 0),
            (statusAt[111], statusAt[112], statusAt[112 + 25], statusAt[112 + 26], statusAt[112 + 27], burstTick >= 16, slotDuringTheLife >= 0,
             slotHeldAt.GetValueOrDefault(112 + 26), slotHeldAt.GetValueOrDefault(112 + 27, true), second is not null && second.Slot == slotDuringTheLife, createdAroundTheDeath, breakSlots));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (v2) C61: the named stub is called once at each tick of S+1 to S+25 (the key wave_grass2), never at S nor after S+25; the same wave with the native handlers off does not call it.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void V2_C61_CallsTheNamedStubOnceAtEachTickOfSPlus1ToSPlus25_AndNeverWithTheNativeSwitchOff(int ticksPerFrame)
    {
        var expected = WaveOracle.GrassCallTicks();
        var callsByTick = new SortedDictionary<int, int>();
        using (var run = new WaveRun("v2-on-" + ticksPerFrame, SteelItem, East))
        {
            run.Arena.Arc.Runner.NativeHandlersEnabled = true; // HitArena turns them off (E14.a0, D-E14-5): the positive case needs them ON, only the paired negative case keeps them off.
            var journal = new List<EntityScriptProxy>();
            WaveSwordAi.StubCallsForTests = journal;
            var previous = 0;
            run.Go(ticksPerFrame, useHook: true, afterTick: t =>
            {
                if (journal.Count > previous)
                {
                    callsByTick[t] = journal.Count - previous;
                    previous = journal.Count;
                }
            });

            Assert.All(journal, e => Assert.Same(run.Born, e));
        }

        var offCalls = 0;
        using (var run = new WaveRun("v2-off-" + ticksPerFrame, SteelItem, East))
        {
            run.Arena.Arc.Runner.NativeHandlersEnabled = false;
            var journal = new List<EntityScriptProxy>();
            WaveSwordAi.StubCallsForTests = journal;
            run.Go(ticksPerFrame, useHook: true);
            offCalls = journal.Count;
            Assert.NotNull(run.Born); // the wave was there.
        }

        // The stub is told inside the events pass of the tick, before its inventory pass: the tick of a call is exact whatever the grouping.
        Assert.Equal(
            (string.Join(",", expected), true, 0),
            (string.Join(",", callsByTick.Keys), callsByTick.Values.All(n => n == 1), offCalls));
        Assert.Equal((WaveOracle.Birth + 1, WaveOracle.Birth + 25, 25), (expected.First(), expected.Last(), expected.Count)); // the key itself: S+1 to S+25.
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (v3) the registry: the keys of its classes (the 6 of the Beannoid and the Muruta, (C, 61), the 2 of the pickup objects, since E20.f4 the (C, 94) of the charged arrow and, since E20.f5, the (E, 17) of the normal arrows); the type 27 has (C, 61) and (E, 10 in line) and no other program; the invariant of D-F3-4 holds for {27, 28, 29}.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void V3_TheProductionRegistryHoldsTheKeysOfItsClasses_TheWaveTypesHaveC61AndE10InLineAndNoOtherProgram_TheInvariantHoldsForThem()
    {
        var runner = new EventProgramRunner(null, GameState.Instance);
        var registry = runner.NativeSprites;
        var catalog = new SpriteRecordCatalog(ProjectRootFinder.Find());

        var headers = WaveSwordAi.SpriteTypes.Select(type =>
        {
            Assert.True(catalog.TryGetPrefab(false, type, out var prefab), $"the prefab of the type {type}");
            Assert.True(catalog.TryGet(prefab, out var header), $"the header of the type {type}");
            return header;
        }).ToList();
        var types = WaveSwordAi.SpriteTypes;

        Assert.Equal(
            (3 + 3 + 1 + PickupAi.Keys.Count + ChargedArrowAi.Keys.Count + ArrowAi.Keys.Count + PillarAi.Keys.Count + LiftableAi.Keys.Count + LiftableAi.BreakKeys.Count, true, true, true, false, 0),
            (registry.Count,
             types.All(t => registry.TryGet(ScriptHelper.ProgramCTick, 61, t, out _)),
             headers.All(h => h.ProgramTick == 61 && h.ProgramDeactivate == 10),
             headers.All(h => h.ProgramLoad == 0 && h.ProgramTouch == 0 && h.ProgramInteract == 0),
             registry.TryGet(ScriptHelper.ProgramCTick, 61, 26, out _) || registry.TryGet(ScriptHelper.ProgramCTick, 61, 30, out _) || registry.TryGet(ScriptHelper.ProgramEDeactivate, 10, 27, out _), // (C, 61) for the wave only; E10 is in line, not in the registry.
             PlayerManager.UnhandledWeaponSpriteCodes(types, catalog, registry).Count));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (v4) the whole life of the wave, tick by tick, against the keys of the oracle, at 1, 2 and 3 ticks per frame: steel x 4 directions, demon east, holy east.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("steel", "east", 1)]
    [InlineData("steel", "east", 2)]
    [InlineData("steel", "east", 3)]
    [InlineData("steel", "north", 1)]
    [InlineData("steel", "north", 2)]
    [InlineData("steel", "north", 3)]
    [InlineData("steel", "west", 1)]
    [InlineData("steel", "west", 2)]
    [InlineData("steel", "west", 3)]
    [InlineData("steel", "south", 1)]
    [InlineData("steel", "south", 2)]
    [InlineData("steel", "south", 3)]
    [InlineData("demon", "east", 1)]
    [InlineData("demon", "east", 2)]
    [InlineData("demon", "east", 3)]
    [InlineData("holy", "east", 1)]
    [InlineData("holy", "east", 2)]
    [InlineData("holy", "east", 3)]
    public void V4_TheWholeLifeOfTheWave_FromSToSPlus27_EqualsTheKeysOfTheOracle_WhateverTheGrouping(string sword, string direction, int ticksPerFrame)
    {
        var item = sword switch { "steel" => SteelItem, "demon" => DemonItem, _ => HolyItem };
        var word = direction switch { "east" => East, "north" => North, "west" => West, _ => South };
        var oracle = WaveOracle.Rows(sword, direction);
        using var run = new WaveRun($"v4-{sword}-{direction}-{ticksPerFrame}", item, word);

        run.Go(ticksPerFrame, useHook: true);

        var expected = Enumerable.Range(WaveOracle.FirstTick, WaveOracle.LastTick - WaveOracle.FirstTick + 1).Select(t => $"{t - WaveOracle.Birth,4}: {WaveOracle.Describe(oracle[t])}").ToList();
        var actual = Enumerable.Range(WaveOracle.FirstTick, WaveOracle.LastTick - WaveOracle.FirstTick + 1).Select(t => $"{t - WaveOracle.Birth,4}: {run.Lines[t]}").ToList();
        _output.WriteLine(string.Join("\n", actual));

        Assert.Equal(string.Join("\n", expected), string.Join("\n", actual));
    }
}
