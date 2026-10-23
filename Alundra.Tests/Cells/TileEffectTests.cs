#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using CasaEngine.Framework.Assets.TileMap;
using Xunit;
using Alundra.Cells;
using Alundra.Rendering.Effects;
using Alundra.Rng;
using Alundra.Tests.Rng;
using Alundra.Tests.Support;

namespace Alundra.Tests.Cells;

/// <summary>
/// E20.e T1 (docs/plan-e20-heros.md section 1.27, phase A; (c1) to (c6)): <c>CheckAndTriggerTileEffect</c> (<c>0x8003A7B0</c>) as <see cref="TileEffect.Trigger"/> and the cut of one cell as <see cref="CellStore.CutCell"/>, against the 4 000 vectors that T0
/// made from the real instructions of the binary (<c>docs/plan-e20-annexe/e20-e/rev3/vectors/cate-vectors.json</c>) and the edges measured on the machine (<c>rev3/oracle/out/zedge.txt</c>). Nothing calls the port in production yet.
/// Every "does nothing" case is paired with the positive case that differs from it by the one thing under test. The tests draw from <see cref="GameRandom"/>: they live in the collection that serialises its static state.
/// </summary>
[Collection(RandomStaticStateCollection.Name)]
public sealed class TileEffectTests : IDisposable
{
    private const int Grass = CellStore.CuttableWalkabilityBit;
    private const int OnePixel = 1 << 16;
    private const int CellTop = (1 << 20) + 0x80000;   // zc of a cell of height byte 1.

    public TileEffectTests() => GameRandom.Reset();

    public void Dispose() => GameRandom.Reset();

    // ---- helpers ------------------------------------------------------------------------------------------------------------------------------------------------

    private static CutBench.Cell Bush(int height = 1, bool stack = true) => new(Grass, height, stack ? new[] { 12576, 12586 } : null);

    private static CutBench BenchWith(HashSet<(int, int)> bushes, int x, int y, int z, int width, int depth, int height, int bushHeight = 1, bool frame = true)
        => CutBench.Create((c, r) => bushes.Contains((c, r)) ? Bush(bushHeight) : default, x, y, z, width, depth, height, frame);

    /// <summary>A box of 4 x 4 pixels inside the cell (column, row), at the DLL's Z <paramref name="hitBoxZ"/>, <paramref name="boxHeight"/> high.</summary>
    private static CutBench InsideACell(int column, int row, int hitBoxZ, int boxHeight, int bushHeight = 1)
        => BenchWith(new HashSet<(int, int)> { (column, row) }, (column * 24 + 6) << 16, (row * 16 + 4) << 16, hitBoxZ, (4 << 16) - 1, (4 << 16) - 1, boxHeight, bushHeight);

    private static List<EffectSlot> RunTheStage(CutBench bench)
    {
        bench.Host.EffectStage!.RunFrame(1, new[] { false });
        return bench.Host.EffectStage.Reservoir.ActiveSlots.ToList();
    }

    private static void WithStage(CutBench bench) => bench.Host.EffectStage = new EffectStage();

    private static CutBench OfVector(CateVectors.Vector v) => CutBench.Create(
        (c, r) =>
        {
            var cell = v.Cells.FirstOrDefault(k => k.Column == c && k.Row == r);
            return cell == null ? default : new CutBench.Cell(cell.Flags & 0xFF, cell.Height, cell.Wall != 0xFFFF ? new[] { 1, 2 } : null);
        },
        v.BoxX, v.BoxY, unchecked(v.BoxZ - 1), v.BoxWidth, v.BoxDepth, v.BoxHeight, v.Frame, v.CutEffectId);   // the DLL's Z is the binary's minus one (ADR-0026).

    // ---- (c1) the cut of one cell ---------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void C1_CutCell_ClearsOnlyTheGrassBit_DestroysTheStack_AndKeepsTheHeightAndTheOtherBits()
    {
        var bench = CutBench.Create((c, r) => (c, r) == (7, 9) ? new CutBench.Cell(0x0A, 3, new[] { 1, 2 }) : default);
        bench.Store.CutCell(7, 9);

        var (walkability, height, hasStack) = bench.Read(7, 9);
        Assert.Equal(0x08, walkability);   // 0x0A lost the bit 0x02 only.
        Assert.Equal(3, height);
        Assert.False(hasStack);
    }

    [Fact]
    public void C1_ClearingTheBitWithoutTheCut_KeepsTheStack_TheContraryOfTheCut()
    {
        var bench = CutBench.Create((c, r) => (c, r) == (7, 9) ? new CutBench.Cell(0x0A, 3, new[] { 1, 2 }) : default);
        bench.Store.ClearCellBits(7, 9, Grass, 0);

        var (walkability, _, hasStack) = bench.Read(7, 9);
        Assert.Equal(0x08, walkability);
        Assert.True(hasStack);
    }

    [Fact]
    public void C1_CutCell_FiresTheEventOnce_WithTheIndexOfTheCell_AlsoForACellWithoutStack()
    {
        var bench = CutBench.Create((c, r) => (c, r) == (7, 9) ? new CutBench.Cell(Grass, 1, new[] { 1 }) : (c, r) == (8, 9) ? new CutBench.Cell(Grass, 1) : default);
        var fired = new List<int[]>();
        bench.Store.CellsMutated += indices => fired.Add(indices.ToArray());

        bench.Store.CutCell(7, 9);
        bench.Store.CutCell(8, 9);

        Assert.Equal(2, fired.Count);
        Assert.Equal(new[] { 9 * CutBench.Width + 7 }, fired[0]);
        Assert.Equal(new[] { 9 * CutBench.Width + 8 }, fired[1]);
        Assert.Equal(0, bench.Read(8, 9).Walkability);
    }

    [Fact]
    public void C1_TheCollisionFieldSeesTheCut_ThroughTheAliasedArrays_AndTheHeightStays()
    {
        var bench = CutBench.Create((c, r) => (c, r) == (7, 9) ? Bush(2) : default);
        Assert.True(bench.Field.TryReadBinaryCell(7, 9, out _, out var before));
        bench.Store.CutCell(7, 9);
        Assert.True(bench.Field.TryReadBinaryCell(7, 9, out var height, out var after));
        Assert.Equal(Grass, before & Grass);
        Assert.Equal(0, after & Grass);
        Assert.Equal(2, height);
    }

    // ---- (c2) the core against the vectors of the binary ------------------------------------------------------------------------------------------------------------

    [Fact]
    public void C2_TheOracleFile_IsTheOneOfTheT0Run_4000VectorsAnd460Cuts_NoMismatchWithItsPseudoCode_AndTheTwoStepsEqualTheInterleaving()
    {
        var vectors = CateVectors.Load();
        Assert.Equal(4000, vectors.Vectors.Count);
        Assert.Equal(460, vectors.TotalCuts);
        Assert.Equal(0, vectors.MismatchesOfTheOracle);
        Assert.Equal(460, vectors.Vectors.Sum(v => v.Cuts));
    }

    [Fact]
    public void C2_TheCore_FollowsTheBinary_OnTheFourThousandVectors_CellsEffectsLootSoundsAndGenerator()
    {
        var cutsSeen = 0;
        foreach (var v in CateVectors.Load().Vectors)
        {
            var bench = OfVector(v);
            WithStage(bench);
            GameRandom.RandomSeed = v.GeneratorWord;

            TileEffect.Trigger(bench.Entity);
            var slots = RunTheStage(bench);

            var where = "vector " + v.Id;
            foreach (var k in v.Cells)
            {
                var (walkability, _, hasStack) = bench.Read(k.Column, k.Row);
                Assert.True(walkability == (k.FlagsAfter & 0xFF), where + ": walkability of the cell (" + k.Column + "," + k.Row + ")");
                Assert.True(hasStack == (k.WallAfter != 0xFFFF), where + ": stack of the cell (" + k.Column + "," + k.Row + ")");
            }

            var effects = v.Calls.Where(c => c.Kind == "effect").ToList();
            Assert.True(effects.Count == slots.Count, where + ": " + effects.Count + " effects expected, " + slots.Count + " created");
            for (var i = 0; i < effects.Count; i++)
            {
                Assert.True(!slots[i].TargetMapTable && slots[i].TargetEffect == effects[i].Id && slots[i].TargetAnimation == effects[i].Anim, where + ": effect " + i + " identity");
                Assert.True(slots[i].X == effects[i].X && slots[i].Y == effects[i].Y && slots[i].Z == effects[i].Z, where + ": effect " + i + " position");
            }

            var loots = v.Calls.Where(c => c.Kind == "loot").ToList();
            var expectedSounds = new List<int>();
            var expectedSpawns = new List<(int, int, int, int)>();
            foreach (var l in loots)
            {
                if (l.ModelItem != 0)
                {
                    expectedSounds.Add(84);
                    expectedSpawns.Add((l.ModelItem + 0x1E, l.X, l.Y, l.Z - 1));
                }

                expectedSounds.Add(31);
            }

            Assert.True(expectedSounds.SequenceEqual(bench.Host.Sounds.Requests), where + ": sounds [" + string.Join(",", bench.Host.Sounds.Requests) + "], expected [" + string.Join(",", expectedSounds) + "]");
            Assert.True(expectedSpawns.SequenceEqual(bench.Host.Spawns), where + ": the objects born");
            var expectedWord = loots.Count == 0 ? v.GeneratorWord : loots[^1].StateAfter;
            Assert.True(unchecked((uint)GameRandom.RandomSeed) == expectedWord, where + ": the generator word");
            cutsSeen += loots.Count;
        }

        Assert.Equal(460, cutsSeen);
    }

    // ---- (c2) the edges of the band of Z, measured on the machine (rev3/oracle/out/zedge.txt) ----------------------------------------------------------------------

    [Theory]
    [InlineData(-2, true)]    // HitBoxZ = zc - 2: the binary's bz = zc - 1, measured CUT.
    [InlineData(-1, true)]    // bz = zc: CUT.
    [InlineData(0, false)]    // HitBoxZ = zc: the binary's bz = zc + 1: skipped (the DLL's Z is the binary's minus one, ADR-0026).
    [InlineData(1, false)]
    public void C2_TheLowerEdgeOfTheBand_IsTheBinarys_InTheFrameOfTheDll(int hitBoxZMinusCellTop, bool cut)
    {
        var bench = InsideACell(10, 10, CellTop + hitBoxZMinusCellTop, 0x100000);
        TileEffect.Trigger(bench.Entity);
        Assert.Equal(cut, (bench.Read(10, 10).Walkability & Grass) == 0);
        Assert.Equal(cut ? 1 : 0, bench.Host.Sounds.Requests.Count(s => s == TileEffect.CutSfx));
    }

    [Theory]
    [InlineData(-1, false)]   // bz + height = zc - 1: skipped.
    [InlineData(0, true)]     // = zc: CUT.
    [InlineData(1, true)]
    public void C2_TheUpperEdgeOfTheBand_IsTheBinarys(int topMinusCellTop, bool cut)
    {
        const int below = 0x100000;
        // bz = HitBoxZ + 1 = zc - below; the top is bz + height = zc + topMinusCellTop.
        var bench = InsideACell(10, 10, CellTop - below - 1, below + topMinusCellTop);
        TileEffect.Trigger(bench.Entity);
        Assert.Equal(cut, (bench.Read(10, 10).Walkability & Grass) == 0);
    }

    [Fact]
    public void C2_TheRealHerosBox_CutsTheCellsOfItsOwnHeightByteOnly_TheBandIsOneCellOfHeightWide()
    {
        // the hero rests at HitBoxZ = ground in the DLL's frame (the binary's bz = ground + 1, measured) with a box 0xEFFFF high.
        foreach (var (cellHeight, cut) in new[] { (0, false), (1, true), (2, false) })
        {
            var bench = InsideACell(10, 10, 1 << 20, 0xEFFFF, cellHeight);
            TileEffect.Trigger(bench.Entity);
            Assert.Equal(cut, (bench.Read(10, 10).Walkability & Grass) == 0);
        }
    }

    // ---- (c2) the corners: order, rows, positions --------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void C2_TheFourCorners_AreVisitedInTheOrder_AB_rowA_then_AB_rowB_AndTheEffectsFollowThatOrder()
    {
        // box x 250 px (column 10), 20 px wide: columns 10 and 11; y 88 px (row 5), 16 px deep: rows 5 and 6.
        var bushes = new HashSet<(int, int)> { (10, 5), (11, 5), (10, 6), (11, 6) };
        var bench = BenchWith(bushes, 250 << 16, 88 << 16, 1 << 20, (20 << 16) - 1, (16 << 16) - 1, 0xEFFFF);
        WithStage(bench);
        TileEffect.Trigger(bench.Entity);
        var slots = RunTheStage(bench);

        var expected = new[] { (10, 5), (11, 5), (10, 6), (11, 6) };
        Assert.Equal(expected.Select(e => (e.Item1 * 0x180000 + 0xC0000, e.Item2 * 0x100000 + 0x80000, 1 << 20)).ToArray(), slots.Select(s => (s.X, s.Y, s.Z)).ToArray());
        Assert.Equal(4, bench.Host.Sounds.Requests.Count(s => s == TileEffect.CutSfx));
    }

    [Fact]
    public void C2_TheSouthRows_AreTheBinarysSingleShift_NotADecompiledDoubleDivision()
    {
        // y 84 px, 24 px deep: the rows are 84 / 16 = 5 and 108 / 16 = 6 (the decompilation divides the south one twice and tests the row 0 instead).
        var bushes = new HashSet<(int, int)> { (10, 5), (10, 6), (10, 0) };
        var bench = BenchWith(bushes, (10 * 24 + 6) << 16, 84 << 16, 1 << 20, (4 << 16) - 1, (24 << 16) - 1, 0xEFFFF);
        TileEffect.Trigger(bench.Entity);
        Assert.Equal(0, bench.Read(10, 5).Walkability & Grass);
        Assert.Equal(0, bench.Read(10, 6).Walkability & Grass);
        Assert.Equal(Grass, bench.Read(10, 0).Walkability & Grass);
    }

    [Fact]
    public void C2_AboveTheMap_TheRowIsMinusOne_TheCellIsTheRowZeroAndTheEffectIsAtMinusHalfARow_D9()
    {
        var bench = BenchWith(new HashSet<(int, int)> { (10, 0) }, (10 * 24 + 6) << 16, -8 * OnePixel, 1 << 20, (4 << 16) - 1, (4 << 16) - 1, 0xEFFFF);
        WithStage(bench);
        TileEffect.Trigger(bench.Entity);
        var slot = Assert.Single(RunTheStage(bench));
        Assert.Equal(0, bench.Read(10, 0).Walkability & Grass);
        Assert.Equal(-0x80000, slot.Y);   // the binary: row -1 * 0x100000 + 0x80000. The decompilation's truncating division gives +0x80000.
        Assert.Equal((10 * 0x180000) + 0xC0000, slot.X);
    }

    [Fact]
    public void C2_BelowTheLastRow_TheCellIsClamped_ButTheEffectKeepsTheUnclampedRow()
    {
        var bench = BenchWith(new HashSet<(int, int)> { (10, 59) }, (10 * 24 + 6) << 16, 960 << 16, 1 << 20, (4 << 16) - 1, (4 << 16) - 1, 0xEFFFF);
        WithStage(bench);
        TileEffect.Trigger(bench.Entity);
        var slot = Assert.Single(RunTheStage(bench));
        Assert.Equal(0, bench.Read(10, 59).Walkability & Grass);
        Assert.Equal((60 * 0x100000) + 0x80000, slot.Y);
    }

    [Fact]
    public void C2_BeforeTheTable_TheColumnIsZero_TheBoundOfTheDll_TheBinaryReadsMemoryItDoesNotDefine()
    {
        var bench = BenchWith(new HashSet<(int, int)> { (0, 10) }, -5 * OnePixel, (10 * 16 + 4) << 16, 1 << 20, (4 << 16) - 1, (4 << 16) - 1, 0xEFFFF);
        WithStage(bench);
        TileEffect.Trigger(bench.Entity);
        var slot = Assert.Single(RunTheStage(bench));
        Assert.Equal(0, bench.Read(0, 10).Walkability & Grass);
        Assert.Equal(0xC0000, slot.X);
    }

    [Fact]
    public void C2_PastTheTable_TheCellIsTheLastColumn_ButTheEffectKeepsTheUnclampedColumn_Q_E7()
    {
        // 1250 px is past the table of 1248 (the binary reads memory it does not define there): the DLL keeps the formula for the position and clamps the cell, like the rows below the map.
        var bench = BenchWith(new HashSet<(int, int)> { (51, 10) }, 1250 << 16, (10 * 16 + 4) << 16, 1 << 20, (4 << 16) - 1, (4 << 16) - 1, 0xEFFFF);
        WithStage(bench);
        TileEffect.Trigger(bench.Entity);
        var slot = Assert.Single(RunTheStage(bench));
        Assert.Equal(0, bench.Read(51, 10).Walkability & Grass);
        Assert.Equal((52 * 0x180000) + 0xC0000, slot.X);
    }

    // ---- (c3) a box narrower than a cell cuts once ------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void C3_ABoxNarrowerThanACell_CutsTheCellOnce_OneEffect_OneRoll_OneSound()
    {
        var bench = InsideACell(10, 10, 1 << 20, 0xEFFFF);
        WithStage(bench);
        GameRandom.RandomSeed = 0x1002;
        TileEffect.Trigger(bench.Entity);

        Assert.Single(RunTheStage(bench));
        Assert.Single(bench.Host.Sounds.Requests, TileEffect.CutSfx);
        // one roll of one word (the seed 0x1002 gives no object: 0x935EE6A1 >> 28 = 9, the row 0x7F is 0 there): the word moved by exactly one step.
        Assert.Equal(0x7D4935EE6A1ul, GameRandom.RandomSeed);
    }

    [Fact]
    public void C3_TheContrary_ABoxOverFourBushes_CutsFour()
    {
        var bushes = new HashSet<(int, int)> { (10, 5), (11, 5), (10, 6), (11, 6) };
        var bench = BenchWith(bushes, 250 << 16, 88 << 16, 1 << 20, (20 << 16) - 1, (16 << 16) - 1, 0xEFFFF);
        TileEffect.Trigger(bench.Entity);
        Assert.Equal(4, bench.Host.Sounds.Requests.Count(s => s == TileEffect.CutSfx));
    }

    // ---- (c4) nothing without the frame box, the context, the mutator or the field ----------------------------------------------------------------------------------

    private static (int Sounds, int Effects, ulong Word, int Walkability) Outcome(CutBench bench, Action prepare)
    {
        WithStage(bench);
        GameRandom.RandomSeed = 0x1002;
        prepare();
        TileEffect.Trigger(bench.Entity);
        return (bench.Host.Sounds.Requests.Count, RunTheStage(bench).Count, GameRandom.RandomSeed, bench.Read(10, 10).Walkability & Grass);
    }

    [Fact]
    public void C4_WithoutAFrameBox_NothingHappens_AndWithOne_TheCellIsCut()
    {
        var with = Outcome(InsideACell(10, 10, 1 << 20, 0xEFFFF), () => { });
        Assert.Equal((1, 1, 0), (with.Sounds, with.Effects, with.Walkability));

        var bench = InsideACell(10, 10, 1 << 20, 0xEFFFF);
        var none = Outcome(bench, () => bench.Entity.HasFrameBox = false);
        Assert.Equal((0, 0, 0x1002ul, Grass), none);
    }

    [Fact]
    public void C4_WithoutTheMutator_NothingHappens_NoEffectNoLootNoSound_TheBitStays()
    {
        var bench = InsideACell(10, 10, 1 << 20, 0xEFFFF);
        var none = Outcome(bench, () => bench.Host.CellMutator = null);
        Assert.Equal((0, 0, 0x1002ul, Grass), none);
    }

    [Fact]
    public void C4_WithoutTheCollisionField_NothingHappens()
    {
        var bench = InsideACell(10, 10, 1 << 20, 0xEFFFF);
        var none = Outcome(bench, () => bench.Entity.JoinsBirthTick = false);   // no Owner.World and no birth-tick join: the entity reads no field.
        Assert.Equal((0, 0, 0x1002ul, Grass), none);
    }

    [Fact]
    public void C4_ForAHostThatIsNotAWorldContext_NothingHappens()
    {
        var bench = InsideACell(10, 10, 1 << 20, 0xEFFFF);
        var none = Outcome(bench, () => bench.Entity.ScriptHost = new ContactHost());
        Assert.Equal((0, 0, 0x1002ul, Grass), none);
    }

    // ---- (c5) the order: the effect in the queue, the loot and the sound at once -------------------------------------------------------------------------------------

    private static CateVectors.Vector FirstCutWithAnObject() => CateVectors.Load().Vectors.First(v => v.Cuts == 1 && v.Calls.Any(c => c.Kind == "loot" && c.ModelItem != 0));

    private static int Steps(uint from, uint to)
    {
        var s = from;
        for (var k = 0; k <= 2; k++)
        {
            if (s == to)
            {
                return k;
            }

            s = unchecked((uint)(((ulong)s * 0x7D2B89DD) + 0xE06A02E7));
        }

        return -1;
    }

    [Fact]
    public void C5_TheEffectIsInTheQueueUntilTheStageRuns_TheLootAndTheSoundHavePassed_TheObjectSoundPrecedesTheCutSound()
    {
        var v = FirstCutWithAnObject();
        var bench = OfVector(v);
        WithStage(bench);
        GameRandom.RandomSeed = v.GeneratorWord;

        TileEffect.Trigger(bench.Entity);

        Assert.Equal(0, bench.Host.EffectStage!.Reservoir.ActiveCount);                 // not yet: the queue.
        Assert.Equal(new[] { 84, 31 }, bench.Host.Sounds.Requests.ToArray());          // the loot (the object) first, then the cut.
        Assert.Single(bench.Host.Spawns);
        var loot = v.Calls.Single(c => c.Kind == "loot");
        Assert.Equal(loot.StateAfter, unchecked((uint)GameRandom.RandomSeed));
        Assert.Equal(loot.ModelDraws, Steps(v.GeneratorWord, loot.StateAfter));        // 1 or 2 words of the real stream.

        var slot = Assert.Single(RunTheStage(bench));
        Assert.False(slot.TargetMapTable);
        Assert.Equal(v.CutEffectId, slot.TargetEffect);
    }

    [Fact]
    public void C5_AFullTableOfEffects_StopsNeitherTheLootNorTheSound()
    {
        var v = FirstCutWithAnObject();
        var bench = OfVector(v);
        WithStage(bench);
        for (var i = 0; i < EffectReservoir.SlotCount; i++)
        {
            Assert.NotNull(bench.Host.EffectStage!.Reservoir.Create(false, 14, 0, 0, 0, 0));
        }

        GameRandom.RandomSeed = v.GeneratorWord;
        TileEffect.Trigger(bench.Entity);
        bench.Host.EffectStage!.RunFrame(1, new[] { false });

        Assert.Equal(new[] { 84, 31 }, bench.Host.Sounds.Requests.ToArray());
        Assert.Single(bench.Host.Spawns);
        Assert.DoesNotContain(bench.Host.EffectStage.Reservoir.ActiveSlots, s => s.TargetEffect == v.CutEffectId);
    }

    [Fact]
    public void C5_WithoutAStage_TheEffectIsSkipped_TheLootAndTheSoundFollow()
    {
        var v = FirstCutWithAnObject();
        var bench = OfVector(v);
        GameRandom.RandomSeed = v.GeneratorWord;
        TileEffect.Trigger(bench.Entity);

        Assert.Equal(new[] { 84, 31 }, bench.Host.Sounds.Requests.ToArray());
        Assert.Equal(0, bench.Read(v.Cells[0].Column, v.Cells[0].Row).Walkability & Grass);
    }

    [Fact]
    public void C5_TheEffectDrawsNoNumber_ApplyingTheQueueMovesNoWordOfTheGenerator()
    {
        var v = CateVectors.Load().Vectors.First(x => x.Cuts == 1 && x.Calls.Single(c => c.Kind == "loot").ModelItem == 0);
        var bench = OfVector(v);
        WithStage(bench);
        GameRandom.RandomSeed = v.GeneratorWord;
        TileEffect.Trigger(bench.Entity);
        var afterTrigger = GameRandom.RandomSeed;
        RunTheStage(bench);

        Assert.Equal(afterTrigger, GameRandom.RandomSeed);
        Assert.Equal(new[] { 31 }, bench.Host.Sounds.Requests.ToArray());
    }

    // ---- (c6) the effect index: the property of the map, 2 when it says nothing --------------------------------------------------------------------------------------

    private static TileMapData MapWith(string? value)
    {
        var data = new TileMapData();
        if (value != null)
        {
            data.CustomProperties[TileEffect.CutEffectPropertyName] = value;
        }

        return data;
    }

    [Fact]
    public void C6_ResolveCutEffectIndex_ReadsTheProperty_AndFallsBackOnTwo_WhenMissingNotNumericOrNoMap()
    {
        Assert.Equal(5, TileEffect.ResolveCutEffectIndex(MapWith("5")));
        Assert.Equal(2, TileEffect.ResolveCutEffectIndex(MapWith("2")));
        Assert.Equal(2, TileEffect.ResolveCutEffectIndex(MapWith(null)));
        Assert.Equal(2, TileEffect.ResolveCutEffectIndex(MapWith("two")));
        Assert.Equal(2, TileEffect.ResolveCutEffectIndex(null));
        Assert.Equal("TileCutEffectId", TileEffect.CutEffectPropertyName);
    }

    [Fact]
    public void C6_TheOldNameOfTheProperty_IsNotRead_TheFallbackWouldHideAWrongName()
    {
        var data = new TileMapData();
        data.CustomProperties["BalanceLevel"] = "7";
        Assert.Equal(2, TileEffect.ResolveCutEffectIndex(data));
        data.CustomProperties[TileEffect.CutEffectPropertyName] = "7";
        Assert.Equal(7, TileEffect.ResolveCutEffectIndex(data));
    }

    [Fact]
    public void C6_TheIdOfTheEmittedEffect_FollowsTheIndexOfTheHost()
    {
        var bench = CutBench.Create((c, r) => (c, r) == (10, 10) ? Bush() : default, (10 * 24 + 6) << 16, (10 * 16 + 4) << 16, 1 << 20, (4 << 16) - 1, (4 << 16) - 1, 0xEFFFF, true, cutEffect: 9);
        WithStage(bench);
        TileEffect.Trigger(bench.Entity);
        Assert.Equal(9, Assert.Single(RunTheStage(bench)).TargetEffect);
    }
}
