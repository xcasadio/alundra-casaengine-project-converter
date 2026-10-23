#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using CasaEngine.Framework.Scene.Entities.Components;
using Xunit;
using Alundra.Audio;
using Alundra.Cells;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Hero;
using Alundra.Maps;
using Alundra.Rendering.Effects;
using Alundra.Rng;
using Alundra.Session;
using Alundra.Tests.Audio;
using Alundra.Tests.Support;
using Alundra.Transitions;

namespace Alundra.Tests.Cells;

/// <summary>
/// E20.e T2 (docs/plan-e20-heros.md section 1.27, w1 to w8): the cut of the grass in the REAL world of map 14 (<see cref="HitArena"/>, no target: the other entities are removed, the native handlers are ON), by the hero's sword
/// and by the sword wave, against the oracle <c>docs/plan-e20-annexe/e20-e/rev3/oracle/out/cut_m14.json</c> (the real code of ALUN_CD.EXE (France) on the real cells of the map, nothing in it comes from the DLL). Every expected
/// value below is read from that file: the tick, the cell, the position and the Z of the effect, the sounds 31 and 84, the generator word at the head of every tick, the cells after, the birth of the object.
/// <list type="bullet">
/// <item><description>The generator is set to the seed of the oracle at the head of the tick 1; the word at the head of every later tick must equal the oracle's (it carries the draws of the loot of every cut and of the hold sparks).</description></item>
/// <item><description>The effect is queued (ADR-0077, S-E1) and created by the stage at the end of the frame, so it is read at the tick of the cut (the tick of the last pad of the frame); the sound 31 is played at once, so the
/// sounds and the effects are compared as two lists, not interleaved. The sounds other than 31 and 84 (29, 42, 43, 428...) belong to the swing and the charge, not to the cut.</description></item>
/// </list>
/// Red before T3 (w1, w2, w5, w6, w7, w8): nothing calls the cut yet. w3 and w4 are the twins that must stay green (nothing is cut, the generator moves as the oracle's).
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class TileEffectCutWorldTests : IDisposable
{
    private const uint Square = PadState.Square;
    private const int SteelEdge = 2;
    private const int SwordSlot = 1;
    private const int CutSfx = 31;
    private const int LootSfx = 84;
    private const int Grass = CellStore.CuttableWalkabilityBit;

    /// <summary>
    /// The hero's counter <c>+0x1C</c> (<see cref="EntityScriptProxy.FrameCounter"/>) at the head of the tick 1 of the charged scenarios: it sets the phase of the hold sparks (one every 4 ticks of the counter, the sound 428 or 429 by its bit 2). The oracle's machine
    /// has its sparks at the ticks 85, 89 and 93 with the sounds 428, 429, 428, which is a counter of 96 at the tick 85 (the counter of the tick t is t + 11); the arena's hero, which has run its 10 settling ticks, would read 10 at the tick 1 and spark at the ticks 83, 87, 91 and 95. Setting the counter
    /// aligns the phase and nothing else (the sparks draw two words each, which the head words of the oracle include).</summary>
    private const int OracleHeroFrameCounterAtTick1 = 12;

    private readonly string _previousProjectPath = CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath;
    private readonly ulong _previousSeed = GameRandom.RandomSeed;

    public TileEffectCutWorldTests()
    {
        Reset();
    }

    public void Dispose()
    {
        Reset();
        GameRandom.RandomSeed = _previousSeed;
        CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath = _previousProjectPath;
    }

    private static void Reset()
    {
        NativeEffects.CreatedForTests = null;
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // The oracle.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private sealed record ExpectedCut(int Tick, string Caller, int Column, int Row, int Id, int X, int Y, int Z, bool HasStack);

    private sealed class Expected
    {
        public required string Name { get; init; }
        public required int Ticks { get; init; }
        public required int HeroX { get; init; }
        public required int HeroY { get; init; }
        public required uint HeroDirection { get; init; }
        public required uint Seed { get; init; }
        public required IReadOnlyList<uint> HeadWords { get; init; }   // rng_by_tick: the word at the head of the tick (index + 2)
        public required IReadOnlyList<ExpectedCut> Cuts { get; init; }
        public required IReadOnlyList<(int Tick, int Sfx)> Sounds { get; init; }   // only the sounds 31 and 84, in order
        public required IReadOnlyList<(int Tick, int Type, int X, int Y)> Born { get; init; }
        public required IReadOnlyList<(int Tick, int Z)> ObjectZ { get; init; }
        public required IReadOnlyList<(int Column, int Row, int Flags, bool HasStack)> CellsBefore { get; init; }
    }

    private static Expected Oracle(string name)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(AnnexFiles.CutAnnex("oracle/out/cut_m14.json")));
        var s = document.RootElement.GetProperty("scenarios").GetProperty(name);
        var stacks = s.GetProperty("cells_after").EnumerateArray()
            .ToDictionary(c => (c.GetProperty("col").GetInt32(), c.GetProperty("row").GetInt32()), c => c.GetProperty("before"));
        var cuts = s.GetProperty("cuts").EnumerateArray().Select(c =>
        {
            var key = (c.GetProperty("col").GetInt32(), c.GetProperty("row").GetInt32());
            var fx = c.GetProperty("effect");
            return new ExpectedCut(c.GetProperty("tick").GetInt32(), c.GetProperty("caller").GetString()!, key.Item1, key.Item2, fx.GetProperty("id").GetInt32(),
                fx.GetProperty("x").GetInt32(), fx.GetProperty("y").GetInt32(), fx.GetProperty("z").GetInt32(), stacks[key].GetProperty("has_stack").GetBoolean());
        }).ToList();
        var sounds = new List<(int, int)>();
        foreach (var step in s.GetProperty("sequence").EnumerateArray())
        {
            foreach (var ev in step.GetProperty("events").EnumerateArray().Select(e => e.GetString()!))
            {
                if (ev is "snd(31)" or "snd(84)")
                {
                    sounds.Add((step.GetProperty("tick").GetInt32(), int.Parse(ev[4..^1])));
                }
            }
        }

        var objectZ = s.GetProperty("object_first_ticks").EnumerateArray().SelectMany(o => o.GetProperty("ticks").EnumerateArray())
            .Select(t => (t.GetProperty("tick").GetInt32(), t.GetProperty("pz").GetInt32())).ToList();
        return new Expected
        {
            Name = name,
            Ticks = s.GetProperty("n").GetInt32(),
            HeroX = s.GetProperty("hero_px")[0].GetInt32(),
            HeroY = s.GetProperty("hero_px")[1].GetInt32(),
            HeroDirection = s.GetProperty("direction").GetString() switch { "west" => 8u, "east" => 24u, "north" => 16u, _ => 0u },
            Seed = s.GetProperty("seed").GetUInt32(),
            HeadWords = s.GetProperty("rng_by_tick").EnumerateArray().Select(e => e.GetUInt32()).ToList(),
            Cuts = cuts,
            Sounds = sounds,
            Born = s.GetProperty("born").EnumerateArray().Select(b => (b.GetProperty("tick").GetInt32(), b.GetProperty("type").GetInt32(), b.GetProperty("x").GetInt32(), b.GetProperty("y").GetInt32())).ToList(),
            ObjectZ = objectZ,
            CellsBefore = s.GetProperty("cells_after").EnumerateArray()
                .Select(c => (c.GetProperty("col").GetInt32(), c.GetProperty("row").GetInt32(), c.GetProperty("before").GetProperty("flags").GetInt32(), c.GetProperty("before").GetProperty("has_stack").GetBoolean())).ToList(),
        };
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // The run.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private sealed class Sounds : ISoundPlayer
    {
        public int Tick;
        public readonly List<(int Tick, int Sfx)> Played = new();

        public void PlaySfx(int sfxId) => Played.Add((Tick, sfxId));

        public void RemixVoice(int sfxId, int left, int right)
        {
        }

        public void FlushFrameSounds()
        {
        }

        public void StopAllSfx()
        {
        }
    }

    private sealed class Run : IDisposable
    {
        public required HitArena Arena { get; init; }
        public required Sounds Heard { get; init; }
        public required List<(int Tick, int Effect, bool MapTable, int X, int Y, int Z)> Effects { get; init; }
        public required Dictionary<int, uint> Heads { get; init; }   // tick -> low 32 bits of the generator at the head of the tick (from the tick 2)
        public required Dictionary<int, int> Rebuilds { get; init; }   // tick -> overlay reconstructions the frame of that tick did
        public required List<(int Tick, int X, int Y, int Z, int Type)> Objects { get; init; }
        public required int CuttableAtStart { get; init; }

        public CellStore Store => (CellStore)((IEntityWorldContext)Arena.Arc.Proxy).CellMutator!;

        public void Dispose() => Arena.Dispose();
    }

    private static int CuttableCells(CellStore store)
    {
        var count = 0;
        for (var row = 0; row < CutBench.Height; row++)
        {
            for (var column = 0; column < CutBench.Width; column++)
            {
                if ((store.GetWalkability(column, row) & Grass) != 0)
                {
                    count++;
                }
            }
        }

        return count;
    }

    /// <summary>
    /// The scenario <paramref name="e"/> on the real map 14: the hero at the pixel of the oracle (on the ground of the cells there, height 1 = 16 pixels except far from the grass), the other entities removed, the native handlers ON,
    /// the seed set at the head of the tick 1. <paramref name="hold"/> gives the pad of each tick; <paramref name="steel"/> gives the Steel Edge in the slot 1 (the wave).
    /// </summary>
    private static Run Play(Expected e, Func<int, uint> hold, bool steel = false, int? ticks = null, int heroPixelZ = 16)
    {
        var arena = new HitArena("e20e-" + e.Name, e.HeroX, e.HeroY, new List<int[]>(), e.HeroDirection, heroPixelZ);
        try
        {
            if (steel)
            {
                arena.Arc.Give(SteelEdge);
                arena.Arc.Equip(SwordSlot);
            }

            foreach (var other in arena.Arc.Entities.Where(x => !ReferenceEquals(x, arena.Hero)).ToList())
            {
                other.Status = EntityStatus.FlagToDestroy;
            }

            arena.Arc.Proxy.RecycleDestroyedEntities();
            ((EventProgramRunner)arena.Arc.Runner).NativeHandlersEnabled = true;

            var heard = new Sounds();
            typeof(WorldProxy).GetField("<SoundPlayer>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(arena.Arc.Proxy, heard);

            var tick = 0;
            var effects = new List<(int, int, bool, int, int, int)>();
            NativeEffects.CreatedForTests = slot =>
            {
                if (slot.TargetEffect == 2 || slot.TargetMapTable)
                {
                    effects.Add((tick, slot.TargetEffect, slot.TargetMapTable, slot.X, slot.Y, slot.Z));
                }
            };

            var heads = new Dictionary<int, uint>();
            var rebuilds = new Dictionary<int, int>();
            var objects = new List<(int, int, int, int, int)>();
            var sync = arena.Arc.Proxy.CellVisualSync!;
            var store = (CellStore)((IEntityWorldContext)arena.Arc.Proxy).CellMutator!;
            var cuttable = CuttableCells(store);
            var rebuildsAtHead = new Dictionary<int, int>();
            var wantedTypes = e.Born.Select(b => b.Type).ToHashSet();
            arena.BeforeTick = t =>
            {
                tick = t;
                heard.Tick = t;
                if (t == 1)
                {
                    GameRandom.RandomSeed = e.Seed;
                    if (steel)
                    {
                        arena.Hero.FrameCounter = OracleHeroFrameCounterAtTick1;
                    }
                }
                else
                {
                    heads[t] = unchecked((uint)GameRandom.RandomSeed);
                }

                rebuildsAtHead[t] = sync.ReconstructionCount;
            };
            arena.AfterTick = t =>
            {
                foreach (var born in arena.Arc.Entities.Where(x => wantedTypes.Contains(x.SpriteType) && !ReferenceEquals(x, arena.Hero)))
                {
                    objects.Add((t, born.PosX, born.PosY, born.PosZ, born.SpriteType));
                }
            };

            var n = ticks ?? e.Ticks;
            arena.Begin();
            for (var t = 1; t <= n; t++)
            {
                arena.Frame(hold(t), 1);
            }

            for (var t = 1; t < n; t++)
            {
                rebuilds[t] = rebuildsAtHead[t + 1] - rebuildsAtHead[t];
            }

            rebuilds[n] = sync.ReconstructionCount - rebuildsAtHead[n];
            return new Run { Arena = arena, Heard = heard, Effects = effects, Heads = heads, Rebuilds = rebuilds, Objects = objects, CuttableAtStart = cuttable };
        }
        catch
        {
            arena.Dispose();
            throw;
        }
    }

    private static uint Sword(int tick) => tick == 1 ? Square : 0;

    private static uint Charged(int tick) => tick <= 95 ? Square : 0;

    private static uint Nothing(int tick) => 0;

    private static string Fx((int Tick, int Effect, bool MapTable, int X, int Y, int Z) f) => $"t{f.Tick} id={f.Effect}{(f.MapTable ? "(map)" : "")} x={f.X} y={f.Y} z={f.Z}";

    private static string Fx(ExpectedCut c) => $"t{c.Tick} id={c.Id} x={c.X} y={c.Y} z={c.Z}";

    /// <summary>The cuts of the oracle, tick by tick, are the effects, the cells, the sounds 31 and 84 and the generator words of the run (the common body of w1, w2 and w7).</summary>
    private static void AssertTheCutsOfTheOracle(Expected e, Run run, int? upToTick = null)
    {
        var last = upToTick ?? e.Ticks;
        var cuts = e.Cuts.Where(c => c.Tick <= last).ToList();
        Assert.NotEmpty(cuts);

        // The generator: the word at the head of every tick from the tick 2 is the oracle's (one draw per cut or two for an object, the sparks of the charge, the step of each tick).
        for (var t = 2; t <= last; t++)
        {
            Assert.True(run.Heads.TryGetValue(t, out var head), $"no head word at the tick {t}");
            Assert.Equal(e.HeadWords[t - 2], head);
        }

        // The effect 2 of the global table at the centre of each cut cell (the unclamped column and row), at the Z of the cell, at the tick of the cut.
        Assert.Equal(cuts.Select(c => Fx(c)).ToList(), run.Effects.Where(f => f.Tick <= last).Select(f => Fx(f)).ToList());

        // The sounds 31 (and 84 before it, when the cut gives an object), in the order of the oracle, at the ticks of the oracle.
        Assert.Equal(e.Sounds.Where(s => s.Tick <= last).Select(s => $"t{s.Tick} snd{s.Sfx}").ToList(),
            run.Heard.Played.Where(p => p.Sfx is CutSfx or LootSfx && p.Tick <= last).Select(p => $"t{p.Tick} snd{p.Sfx}").ToList());

        // The cells: every cut cell has lost its bit 0x02 and its stack of tiles; nothing else is cut.
        var store = run.Store;
        foreach (var c in cuts)
        {
            Assert.Equal(0, store.GetWalkability(c.Column, c.Row) & Grass);
            Assert.Null(store.GetWallTileStack(c.Column, c.Row));
        }

        Assert.Equal(run.CuttableAtStart - cuts.Count, CuttableCells(store));

        // One reconstruction of the overlay per frame that cut a cell WITH a stack, none for a cell without one (Q-E4), the cuts of a frame coalesced.
        for (var t = 1; t <= last; t++)
        {
            var expected = cuts.Any(c => c.Tick == t && c.HasStack) ? 1 : 0;
            Assert.True(run.Rebuilds[t] == expected, $"tick {t}: {run.Rebuilds[t]} reconstruction(s), the oracle's cuts need {expected}");
        }
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // w1, w2, w7: the sword, the wave, the cell without a stack.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void W1_TheSwordOfTheHero_CutsTheBushesOfTheCornersOfItsBox_AtTheTicksCellsAndPositionsOfTheOracle()
    {
        var e = Oracle("sword_none");
        Assert.Equal(4, e.Cuts.Count);
        using var run = Play(e, Sword);

        AssertTheCutsOfTheOracle(e, run);
        Assert.Equal(new[] { 2, 6, 8, 8 }, e.Cuts.Select(c => c.Tick).ToArray());   // the grass cut by the swing, then by the second swing step: ticks 2, 6, 8 (two cells).
    }

    [Fact]
    public void W2_TheSwordWave_CutsTheBushesItCrosses_AfterTheHeroOwnCuts_AtTheTicksCellsAndPositionsOfTheOracle()
    {
        var e = Oracle("wave_none");
        Assert.Equal(6, e.Cuts.Count);
        Assert.Equal(new[] { "hero", "hero", "wave", "wave", "wave", "wave" }, e.Cuts.Select(c => c.Caller).ToArray());
        using var run = Play(e, Charged, steel: true);

        AssertTheCutsOfTheOracle(e, run);
        Assert.Equal(2, run.Effects.Count(f => f.Tick == 123));   // the wave crosses two cells at once, two columns on.
    }

    [Fact]
    public void W7_ACellWithoutAStackOfTiles_IsCutAsAnyOther_EffectLootAndSound_WithoutAReconstruction()
    {
        var e = Oracle("nostack_none");
        var withoutStack = e.Cuts.Where(c => !c.HasStack).ToList();
        Assert.Contains(withoutStack, c => c.Column == 27 && c.Row == 45);
        using var run = Play(e, Sword, heroPixelZ: 16);

        AssertTheCutsOfTheOracle(e, run);
        var noStackCut = withoutStack.First(c => c.Column == 27 && c.Row == 45);
        Assert.Equal(0, run.Rebuilds[noStackCut.Tick]);
        Assert.Contains(run.Effects, f => f.Tick == noStackCut.Tick && f.X == noStackCut.X && f.Y == noStackCut.Y);
        Assert.Contains(run.Heard.Played, p => p.Tick == noStackCut.Tick && p.Sfx == CutSfx);
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // w3, w4: nothing is cut (the twins that stay green).
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("nograss", 0)]       // the swing in the air far from any grass.
    [InlineData("beside", 16)]       // the swing next to the bushes of the cells 26 to 29, rows 40 to 44, facing away from them.
    public void W3_ASwingBesideTheBushes_CutsNothing_TheGeneratorMovesAsTheOracle(string scenario, int heroPixelZ)
    {
        var e = Oracle(scenario);
        Assert.Empty(e.Cuts);
        using var run = Play(e, Sword, heroPixelZ: heroPixelZ);

        AssertNothingWasCut(e, run);
    }

    [Fact]
    public void W4_AStateOutsideTheFifteen_CutsNothing_AtTheSamePlaceWhereTheSwingCuts()
    {
        var e = Oracle("idle");
        Assert.Empty(e.Cuts);
        Assert.Equal(4, Oracle("sword_none").Cuts.Count);   // the same place (732, 696) facing west: the swing cuts, the idle hero does not.
        using var run = Play(e, Nothing);

        AssertNothingWasCut(e, run);
        Assert.Equal(0u, run.Arena.Hero.TargetAnimationId);
    }

    private static void AssertNothingWasCut(Expected e, Run run)
    {
        Assert.Empty(run.Effects);
        Assert.DoesNotContain(run.Heard.Played, p => p.Sfx is CutSfx or LootSfx);
        for (var t = 2; t <= e.Ticks; t++)
        {
            Assert.Equal(e.HeadWords[t - 2], run.Heads[t]);
        }

        Assert.Equal(run.CuttableAtStart, CuttableCells(run.Store));
        Assert.All(run.Rebuilds.Values, n => Assert.Equal(0, n));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // w5: the grass grows back.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void W5_TheGrassGrowsBack_WhenTheMapIsEnteredAgain()
    {
        var e = Oracle("sword_none");
        using (var first = Play(e, Sword, ticks: 10))
        {
            foreach (var c in e.Cuts)
            {
                Assert.Equal(0, first.Store.GetWalkability(c.Column, c.Row) & Grass);   // cut in the first visit.
            }
        }

        using var second = Play(e, Nothing, ticks: 3);   // the map is loaded again from the export.
        foreach (var (column, row, flags, hasStack) in e.CellsBefore)
        {
            Assert.Equal(flags & Grass, second.Store.GetWalkability(column, row) & Grass);
            Assert.NotEqual(0, second.Store.GetWalkability(column, row) & Grass);
            Assert.Equal(hasStack, second.Store.GetWallTileStack(column, row) != null);
        }
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // w6: a cut that gives an object.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void W6_ACutByTheSword_ThatGivesAnObject_BirthsItAtTheCell_SoundsAreEightyFourThenThirtyOne_TheGeneratorIsTheOracles()
    {
        var e = Oracle("sword_obj");
        var born = Assert.Single(e.Born);
        using var run = Play(e, Sword);

        AssertTheCutsOfTheOracle(e, run);
        Assert.Equal(new[] { (2, LootSfx), (2, CutSfx) }, e.Sounds.ToArray());   // 84 then 31, in the same tick, as the binary.

        // The object: the type of the oracle, at the centre of the cut cell, born at the tick of the cut, at the Z the binary leaves after the program A of that same tick (the hero's slot, D-E0A-5), then rising as in the oracle.
        var seen = run.Objects.GroupBy(o => o.Tick).ToDictionary(g => g.Key, g => g.Single());
        Assert.Equal(born.Tick, seen.Keys.Min());
        var first = seen[born.Tick];
        Assert.Equal((born.Type, born.X, born.Y), (first.Type, first.X, first.Y));
        foreach (var (tick, z) in e.ObjectZ)
        {
            Assert.True(seen.ContainsKey(tick), $"no object at the tick {tick}");
            Assert.Equal(z - 1, seen[tick].Z);   // the DLL's Z is the binary's minus one (ADR-0026).
        }
    }

    [Fact]
    public void W6_ACutByTheWave_ThatGivesAnObject_BirthsItAtTheCell_SoundsAreEightyFourThenThirtyOne_TheGeneratorIsTheOracles()
    {
        var e = Oracle("wave_obj");
        var born = Assert.Single(e.Born);
        using var run = Play(e, Charged, steel: true);

        AssertTheCutsOfTheOracle(e, run);
        var waveTick = e.Cuts.First(c => c.Caller == "wave").Tick;
        Assert.Equal(waveTick, born.Tick);
        Assert.Equal(new[] { (waveTick, LootSfx), (waveTick, CutSfx), (waveTick, CutSfx) }, e.Sounds.Where(s => s.Tick == waveTick).ToArray());

        var seen = run.Objects.GroupBy(o => o.Tick).ToDictionary(g => g.Key, g => g.Single());
        Assert.Equal(born.Tick, seen.Keys.Min());
        var first = seen[born.Tick];
        Assert.Equal((born.Type, born.X, born.Y), (first.Type, first.X, first.Y));
        foreach (var (tick, z) in e.ObjectZ)
        {
            Assert.True(seen.ContainsKey(tick), $"no object at the tick {tick}");
            Assert.Equal(z - 1, seen[tick].Z);
        }
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // w8: the real property.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void W8_TheEffectOfTheCut_IsTheOneTheRealPropertyOfTheMapNames_TheGlobalEffectTwo()
    {
        var e = Oracle("sword_none");
        using var run = Play(e, Sword, ticks: 3);

        var tileMap = run.Arena.Arc.RealWorld!.Entities.First(x => x.Name == "tileMap").GetComponent<TileMapComponent>()!.TileMapData;
        Assert.Equal("2", tileMap.CustomProperties[TileEffect.CutEffectPropertyName]);   // the real exported property, not the fallback.
        Assert.False(tileMap.CustomProperties.ContainsKey("BalanceLevel"));
        Assert.Equal(2, ((IScriptHost)run.Arena.Arc.Proxy).TileCutEffectIndex);

        var effect = Assert.Single(run.Effects);
        Assert.Equal((2, 2, false), (effect.Tick, effect.Effect, effect.MapTable));
    }
}
