#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Xunit;
using Alundra.Ai.Props;
using Alundra.Ai.Items;
using Alundra.Ai;
using Alundra.Ai.Enemies;
using Alundra.Ai.Objects;
using Alundra.Ai.Projectiles;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Rendering.Effects;
using Alundra.Session;
using Alundra.Tests.Audio;
using Alundra.Tests.Support;
using Alundra.Transitions;

namespace Alundra.Tests.Ai.Projectiles;

/// <summary>
/// E20.f6b T2, phase B (docs/plan-e20-heros.md section 1.22.2, tests (m1) to (m3)): the three flames of the map 309 (the records 6, 7 and 8 of the global sprite 12, the charged fire ball) in the REAL world of the map 309 built by <see cref="ArcRun"/> (the arrival on the tile (38, 47), the real prefabs), with the registry
/// of PRODUCTION and no test hook: their program A (<c>1A 02</c> / <c>63 80 F0 00</c>) puts the animation 2 and clears the deactivation bits (<c>0x132154 -> 0x132104</c>), so E9 never runs for them, and C60 (registered by <see cref="NativeSpriteRegistry.CreateForProduction"/> since the phase B) emits the effect 17 every
/// four ticks at their place, 8 pixels above the binary's Z, with a nil speed and no draw. The expected values are those of the key <c>flames309_real_pf</c> of the oracle <c>docs/plan-e20-annexe/e20-f4f6/f6b/oracle/out/json</c> (the real code of <c>ALUN_CD.EXE</c> in the MIPS machine, with the real bytecode runner), read by key (<see cref="BallOracle"/>);
/// a tick of the oracle is a tick of the test, at one, two or three ticks per frame. They are RED while <c>CreateForProduction</c> does not register the keys of <see cref="BallAi"/>, and green once it does. Each case without effect is set next to a positive case in the same test.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class BallFlamesTests : IDisposable
{
    /// <summary>The ticks run (a multiple of one, two and three; the key holds 80).</summary>
    private const int LastTick = 78;
    private const string Key = "flames309_real_pf";
    private const int TrailEffect = 17;
    private const uint FlagsAfterProgramA = 0x132104;

    public BallFlamesTests() => Reset();

    public void Dispose() => Reset();

    private static void Reset()
    {
        EventProgramRunner.NativeSpriteTypeFilterForTests = null;
        NativeEffects.CreatedForTests = null;
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }

    /// <summary>The map 309 of the arc: the arrival on the tile (38, 47) from the maps 306 and 441 is posed directly (P-F6-2 of T0), the real controller and the real prefabs as in production.</summary>
    private static ArcSpec Spec(string name) => new(
        name, "Meia Dream", "Meia Dream (flashback)-309", Array.Empty<int>(), 38, 47, 0, 2000, RealController: true, Prefabs: true,
        Arrival: new ArcArrival((38 * 24 + 12) << 16, (47 * 16 + 8) << 16, 0, GameState.ResetAnimationId, 0));

    private readonly record struct FlameState(EntityStatus Status, int X, int Y, int Z, int ForceX, int ForceY, int ForceZ, uint Flags, int HitCounter, bool Touching, bool Present);

    /// <summary>The world of the map 309 run in frames of <paramref name="ticksPerFrame"/> logic ticks; records the effects the stage creates (the tick of the END of the frame that holds them) and the state of the flames after each frame.</summary>
    private sealed class FlameRun : IDisposable
    {
        internal readonly ArcRun Arc;
        internal readonly List<EntityScriptProxy> Flames;
        internal readonly List<(int Tick, int Id, int Direction, string X, string Y, string Z, string Vx, string Vy, string Vz)> Effects = new();
        internal readonly Dictionary<int, List<FlameState>> After = new();
        private int _frameEnd;

        internal FlameRun(string name, bool nativeHandlers = true)
        {
            Arc = new ArcRun(Spec(name));
            Arc.Runner.NativeHandlersEnabled = nativeHandlers;
            Flames = Arc.Entities.Where(e => e.SpriteType == 12).ToList();
            NativeEffects.CreatedForTests = slot =>
            {
                if (!slot.TargetMapTable)
                {
                    Effects.Add((_frameEnd, slot.TargetEffect, slot.TargetAnimation, BallRun.F3(slot.X), BallRun.F3(slot.Y), BallRun.F3(slot.Z), BallRun.F4(slot.ForceX), BallRun.F4(slot.ForceY), BallRun.F4(slot.ForceZ)));
                }
            };
        }

        internal void Go(int ticksPerFrame, int lastTick)
        {
            for (var tick = 1; tick <= lastTick; tick += ticksPerFrame)
            {
                _frameEnd = tick + ticksPerFrame - 1;
                Arc.OneFrame(ticksPerFrame * HitArena.OneTick);
                After[_frameEnd] = Flames.Select(f => new FlameState(f.Status, f.PosX, f.PosY, f.PosZ, f.ForceX, f.ForceY, f.ForceZ, f.Flags, f.HitCounter, f.TouchingEntity is not null, Arc.Entities.Contains(f))).ToList();
            }
        }

        public void Dispose()
        {
            NativeEffects.CreatedForTests = null;
            Arc.Dispose();
        }
    }

    /// <summary>The effects of the key, with the tick moved to the end of the frame that holds it; only the ticks the run reached.</summary>
    private static List<string> KeyEffects(BallOracle key, int ticksPerFrame, int lastTick)
        => key.Effects(TrailEffect).Where(e => e.Tick <= lastTick).Select(e => $"t{BallRun.FrameEnd(e.Tick, ticksPerFrame)} id={e.Id} dir={e.Direction} x={e.X} y={e.Y} z={e.Z} v=({e.Vx},{e.Vy},{e.Vz})").ToList();

    private static List<string> RunEffects(FlameRun run, int lastTick)
        => run.Effects.Where(e => e.Tick <= lastTick).Select(e => $"t{e.Tick} id={e.Id} dir={e.Direction} x={e.X} y={e.Y} z={e.Z} v=({e.Vx},{e.Vy},{e.Vz})").ToList();

    /// <summary>The slot (the key's name) of the flame at the pixel position of <paramref name="flame"/>.</summary>
    private static string SlotOf(BallOracle key, EntityScriptProxy flame)
        => key.Row(1).GetProperty("slots").EnumerateObject()
            .Single(s => s.Value.GetProperty("px").GetInt32() >> 16 == flame.PosX >> 16 && s.Value.GetProperty("py").GetInt32() >> 16 == flame.PosY >> 16).Name;

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (m1) the flames emit.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void M1_TheThreeFlamesOfTheMap309EmitTheEffect17EveryFourTicks_AtTheirPlace8PixelsAboveTheBinarysZ_WithANilSpeed_AsTheKeyDoes(int ticksPerFrame)
    {
        var key = BallOracle.Load(Key);
        var expected = KeyEffects(key, ticksPerFrame, LastTick);
        using var run = new FlameRun("f6b-m1-" + ticksPerFrame);

        Assert.Equal(3, run.Flames.Count);
        Assert.Equal(3 * 19, expected.Count); // the ticks 5, 9, ..., 77: nineteen emissions of three flames (the guard of the positive case).
        run.Go(ticksPerFrame, LastTick);

        Assert.Equal(expected, RunEffects(run, LastTick));
        Assert.All(run.Effects, e => Assert.Equal(("40.000", "0.0000", "0.0000", "0.0000"), (e.Z, e.Vx, e.Vy, e.Vz))); // 32 + 8 pixels, the P-F6-1 force being nil: no speed.
        Assert.Equal(
            new[] { 936, 948, 960 },
            run.Effects.Take(3).Select(e => (int)double.Parse(e.X, CultureInfo.InvariantCulture)).OrderBy(x => x));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void M1_TheFlamesAreWhereTheKeyHasThem_AfterTheProgramA_AtEveryFrame_StatusNormalFlags0x132104AndNoForce(int ticksPerFrame)
    {
        var key = BallOracle.Load(Key);
        using var run = new FlameRun("f6b-m1-state-" + ticksPerFrame);
        var slots = run.Flames.Select(f => SlotOf(key, f)).ToList();
        Assert.Equal(3, slots.Distinct().Count());
        run.Go(ticksPerFrame, LastTick);

        foreach (var (end, states) in run.After)
        {
            for (var i = 0; i < slots.Count; i++)
            {
                var row = key.Row(end).GetProperty("slots").GetProperty(slots[i]);
                var s = states[i];
                Assert.True(s.Present, $"flame {i} at tick {end}");
                Assert.Equal(
                    (row.GetProperty("status").GetInt32(), row.GetProperty("px").GetInt32(), row.GetProperty("py").GetInt32(), row.GetProperty("pz").GetInt32() - BallOracle.RawZUnit, row.GetProperty("fx").GetInt32(), row.GetProperty("fy").GetInt32(), row.GetProperty("fz").GetInt32(), (uint)row.GetProperty("flags6C").GetInt32()),
                    ((int)s.Status, s.X, s.Y, s.Z, s.ForceX, s.ForceY, s.ForceZ, s.Flags));
                Assert.Equal(FlagsAfterProgramA, s.Flags);
            }
        }

        // the positive case: the same world emits (C60 ran), so the zero forces above are those of flames that were alive.
        Assert.NotEmpty(run.Effects);
    }

    [Fact]
    public void M1_WithoutNativeHandlersTheSameWorldEmitsNothing_AndTheFlamesStillGetTheirProgramA_NextToTheWorldThatEmits()
    {
        using (var without = new FlameRun("f6b-m1-without", nativeHandlers: false))
        {
            without.Go(1, LastTick);

            Assert.Empty(without.Effects);
            Assert.All(without.After[LastTick], s => Assert.Equal(FlagsAfterProgramA, s.Flags)); // the program A is bytecode, not a native handler: it ran all the same.
        }

        Reset();
        using var with = new FlameRun("f6b-m1-with");
        with.Go(1, LastTick);

        Assert.Equal(3 * 19, with.Effects.Count); // the same world, handlers on: the emissions come from C60 and from nothing else.
        Assert.All(with.After[LastTick], s => Assert.Equal(FlagsAfterProgramA, s.Flags));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (m2) the flames do nothing but emit.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void M2_TheFlamesTouchNoEntityAndDoNothingButEmit_NoExplosionNoDestructionNoOtherEffect(int ticksPerFrame)
    {
        var key = BallOracle.Load(Key);
        using var run = new FlameRun("f6b-m2-" + ticksPerFrame);
        var slots = run.Flames.Select(f => SlotOf(key, f)).ToList();
        run.Go(ticksPerFrame, LastTick);

        Assert.NotEmpty(run.Effects); // the positive case: they do emit (C60 ran), so the zeros below are not those of a flame that never ran.
        Assert.All(run.Effects, e => Assert.Equal(TrailEffect, e.Id)); // E9 never runs for them: no explosion (five effects), no puff, no water effect.
        foreach (var (end, states) in run.After)
        {
            for (var i = 0; i < slots.Count; i++)
            {
                var row = key.Row(end).GetProperty("slots").GetProperty(slots[i]);
                Assert.Equal((0, 0), (row.GetProperty("hitc").GetInt32(), row.GetProperty("touch").GetInt32())); // the key: no hit, no touch.
                Assert.Equal((EntityStatus.Normal, 0, false, true), (states[i].Status, states[i].HitCounter, states[i].Touching, states[i].Present));
            }
        }
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (m3) the pin of the registry, by derivation.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static NativeSpriteRegistry Sum()
    {
        var sum = new NativeSpriteRegistry();
        BeannoideAi.Register(sum);
        MurutaAi.Register(sum);
        WaveSwordAi.Register(sum);
        PickupAi.Register(sum);
        ChestAi.Register(sum);
        ChargedArrowAi.Register(sum);
        ArrowAi.Register(sum);
        PillarAi.Register(sum);
        LiftableAi.Register(sum);
        LiftableAi.RegisterBreak(sum);
        LiftableAi.RegisterC4Production(sum);
        BallAi.Register(sum); // E20.f6b: (C, 60) and (E, 9), the two keys more.
        BombAi.Register(sum); // E20.h3a: (D, 30) and (E, 12)
        BombLidAi.Register(sum); // E20.h3a: (E, 19)
        TorchAi.Register(sum); // E20.g2c2: (C, 70) and (E, 20)
        BrambleAi.Register(sum); // E20.g2c2: (D, 34)
        WaterDropAi.Register(sum); // E20.g2c3: (A, 19) and (C, 71)
        return sum;
    }

    [Fact]
    public void M3_TheProductionRegistryHoldsC60AndE9_ForTheSixBallTypesAlone_AndItsCountIsTheSumOfTheCountsOfTheClassesPlusTwoKeys()
    {
        var production = NativeSpriteRegistry.CreateForProduction();
        var runner = new EventProgramRunner(null, GameState.Instance).NativeSprites;

        Assert.Equal(2, BallAi.Keys.Count);
        Assert.Equal(Sum().Count, production.Count);
        Assert.Equal(Sum().Count, runner.Count);
        Assert.Equal(6 + 1 + PickupAi.Keys.Count + ChestAi.Keys.Count + ChargedArrowAi.Keys.Count + ArrowAi.Keys.Count + PillarAi.Keys.Count + LiftableAi.Keys.Count + LiftableAi.BreakKeys.Count + LiftableAi.C4Keys.Count + BallAi.Keys.Count + BombAi.Keys.Count + BombLidAi.Keys.Count + TorchAi.Keys.Count + BrambleAi.Keys.Count + WaterDropAi.Keys.Count, production.Count);
        foreach (var registry in new[] { production, runner })
        {
            foreach (var (slot, code) in BallAi.Keys)
            {
                Assert.All(Enumerable.Range(0, 400), type => Assert.Equal(type is >= 10 and <= 15, registry.TryGet(slot, code, type, out _))); // the six types, and no other.
            }
        }
    }
}
