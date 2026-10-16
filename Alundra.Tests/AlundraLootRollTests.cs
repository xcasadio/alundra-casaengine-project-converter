#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using Alundra.Scripts;
using Xunit;
using static Alundra.Tests.AlundraLootOracle;

namespace Alundra.Tests;

/// <summary>
/// E20.e0b2 (docs/plan-e20-heros.md section 1.17.2, T1): the pure loot roll <see cref="AlundraLootRoll"/> against EVERY vector of the oracle of the real binary
/// (<c>docs/plan-e20-annexe/e20-e0b/oracle/out</c>, found above the test binaries): 2 626 + 256 choices of an item, 52 rolls on a hand-built entity, 183 rolls of the real <c>SpawnWarpEntity</c>, 12 rolls of the real record
/// spawner, 6 000 random rolls with flags, two banks, record and header codes. Nothing here computes an expected value from the DLL's own logic: the rows of the oracle are the expected values. The class is pure
/// (no shared stream, no world), so these tests need no static-state collection.
/// </summary>
public sealed class AlundraLootRollTests
{
    private const uint PersistentBank = 0x801EB344;
    private const uint TemporaryBank = 0x801EBA40;

    /// <summary>One step of the generator on 32 bits, the machine's own formula (<c>s * 0x7D2B89DD + 0xE06A02E7</c>).</summary>
    private sealed class Stream
    {
        internal uint State;

        internal Stream(uint seed) => State = seed;

        internal uint Next()
        {
            State = unchecked((State * 0x7D2B89DDu) + 0xE06A02E7u);
            return State;
        }
    }

    private static void SetBank(AlundraGameState state, uint bank, int word, uint value)
    {
        var words = bank switch
        {
            PersistentBank => state.GameFlags,
            TemporaryBank => state.TemporaryFlags,
            _ => throw new InvalidOperationException($"unknown bank 0x{bank:X8}"),
        };
        words[word] = value;
    }

    [Fact]
    public void TheTable_IsThe2048BytesOfTheExecutable_AndTheDumpOfTheOracle()
    {
        Assert.Equal(AlundraLootRoll.RowCount * AlundraLootRoll.RowSize, AlundraLootRoll.Table.Length);
        Assert.Equal("7c6b97ac50de72f436a6fbf08ed6bcd7", Convert.ToHexString(MD5.HashData(AlundraLootRoll.Table)).ToLowerInvariant());

        using var document = Read("loot_table.json");
        var rows = document.RootElement.GetProperty("table").EnumerateArray().ToList();
        Assert.Equal(AlundraLootRoll.RowCount, rows.Count);
        for (var r = 0; r < rows.Count; r++)
        {
            var row = rows[r].EnumerateArray().Select(e => (byte)e.GetInt32()).ToArray();
            Assert.Equal(row, AlundraLootRoll.Table.Slice(r * AlundraLootRoll.RowSize, AlundraLootRoll.RowSize).ToArray());
        }

        var nonZeroRows = Enumerable.Range(0, AlundraLootRoll.RowCount).Where(r => AlundraLootRoll.Table.Slice(r * AlundraLootRoll.RowSize, AlundraLootRoll.RowSize).ToArray().Any(b => b != 0)).ToArray();
        Assert.Equal(new[] { 0x01, 0x02, 0x03, 0x76, 0x77, 0x78, 0x79, 0x7A, 0x7B, 0x7C, 0x7D, 0x7E, 0x7F }, nonZeroRows);
    }

    [Theory]
    [InlineData("choose_vectors.json", 2626)]
    [InlineData("choose_high_vectors.json", 256)]
    public void ChooseRandomlyAnItem_EveryPathOfTheDecisionTree_IsTheMachines(string file, int count)
    {
        using var document = Read(file);
        var vectors = document.RootElement.EnumerateArray().ToList();
        Assert.Equal(count, vectors.Count);
        foreach (var v in vectors)
        {
            var code = I(v[0]);
            var stream = new Stream(U(v[1]));
            var item = AlundraLootRoll.Choose(code, stream.Next, out var draws);
            var label = $"code 0x{code:X} seed {U(v[1]):X8}";
            Assert.True(I(v[2]) == (int)item, $"{label}: item {item}, machine {I(v[2])}");
            Assert.True(U(v[3]) == stream.State, $"{label}: word {stream.State:X8}, machine {U(v[3]):X8}");
            Assert.True(I(v[4]) == draws, $"{label}: draws {draws}, machine {I(v[4])}");
        }
    }

    [Fact]
    public void TheRoll_OnAHandBuiltEntity_Is52VectorsOfTheMachine()
    {
        using var document = Read("roll_vectors.json");
        var vectors = document.RootElement.EnumerateArray().ToList();
        Assert.Equal(52, vectors.Count);
        foreach (var v in vectors)
        {
            var label = v.GetProperty("label").GetString()!;
            var state = new AlundraGameState();
            foreach (var bank in v.GetProperty("banks").EnumerateArray())
            {
                SetBank(state, U(bank[0]), I(bank[1]), U(bank[2]));
            }

            var hasRecord = I(v.GetProperty("rec")) != 0;
            var stream = new Stream(U(v.GetProperty("rng_in")));
            var result = AlundraLootRoll.Roll(
                hasRecord, hasRecord ? I(v.GetProperty("flag")) : 0, hasRecord ? I(v.GetProperty("rec_code")) : 0, I(v.GetProperty("header_contents")), state, stream.Next);
            Assert.True(I(v.GetProperty("item")) == (int)result.ItemId, $"{label}: item {result.ItemId}, machine {I(v.GetProperty("item"))}");
            Assert.True(I(v.GetProperty("flag_out")) == result.GameFlag, $"{label}: +0x40 {result.GameFlag}, machine {I(v.GetProperty("flag_out"))}");
            Assert.True(U(v.GetProperty("rng_out")) == stream.State, $"{label}: word {stream.State:X8}, machine {U(v.GetProperty("rng_out")):X8}");
            Assert.True(I(v.GetProperty("draws")) == result.Draws, $"{label}: draws {result.Draws}, machine {I(v.GetProperty("draws"))}");
        }
    }

    [Fact]
    public void TheRoll_OfTheRealSpawnWarpEntity_RecordLess_Is183VectorsOfTheMachine()
    {
        using var document = Read("spawn_vectors.json");
        var vectors = document.RootElement.EnumerateArray().ToList();
        Assert.Equal(183, vectors.Count);
        foreach (var v in vectors)
        {
            var label = $"page {I(v.GetProperty("page"))} index {I(v.GetProperty("index"))} seed {v.GetProperty("seed_name").GetString()}";
            var stream = new Stream(U(v.GetProperty("rng_in")));
            var result = AlundraLootRoll.Roll(false, 0, 0, I(v.GetProperty("header_contents")), new AlundraGameState(), stream.Next);
            Assert.True(I(v.GetProperty("item")) == (int)result.ItemId, $"{label}: item {result.ItemId}, machine {I(v.GetProperty("item"))}");
            Assert.True(I(v.GetProperty("flag40")) == result.GameFlag, $"{label}: +0x40 {result.GameFlag}, machine {I(v.GetProperty("flag40"))}");
            Assert.True(U(v.GetProperty("rng_out")) == stream.State, $"{label}: word {stream.State:X8}, machine {U(v.GetProperty("rng_out")):X8}");
            Assert.True(I(v.GetProperty("draws")) == result.Draws, $"{label}: draws {result.Draws}, machine {I(v.GetProperty("draws"))}");
        }
    }

    [Fact]
    public void TheRoll_OfTheRealRecordSpawner_Is12VectorsOfTheMachine()
    {
        // The record template of the oracle (s6_record.py) is the Beannoid, sprite 79 of the map page, whose header Contents is 0xFC.
        const int beannoidHeaderContents = 0xFC;
        using var document = Read("record_vectors.json");
        var vectors = document.RootElement.EnumerateArray().ToList();
        Assert.Equal(12, vectors.Count);
        foreach (var v in vectors)
        {
            var label = v.GetProperty("label").GetString()!;
            var state = new AlundraGameState();
            foreach (var bank in v.GetProperty("banks").EnumerateArray())
            {
                SetBank(state, Convert.ToUInt32(bank[0].GetString(), 16), I(bank[1]), Convert.ToUInt32(bank[2].GetString(), 16));
            }

            var stream = new Stream(U(v.GetProperty("rng_in")));
            var result = AlundraLootRoll.Roll(true, I(v.GetProperty("rec_10")), I(v.GetProperty("rec_Contents")), beannoidHeaderContents, state, stream.Next);
            Assert.True(I(v.GetProperty("item")) == (int)result.ItemId, $"{label}: item {result.ItemId}, machine {I(v.GetProperty("item"))}");
            Assert.True(I(v.GetProperty("flag40")) == result.GameFlag, $"{label}: +0x40 {result.GameFlag}, machine {I(v.GetProperty("flag40"))}");
            Assert.True(U(v.GetProperty("rng_out")) == stream.State, $"{label}: word {stream.State:X8}, machine {U(v.GetProperty("rng_out")):X8}");
            Assert.True(I(v.GetProperty("draws")) == result.Draws, $"{label}: draws {result.Draws}, machine {I(v.GetProperty("draws"))}");
        }
    }

    [Fact]
    public void TheRoll_OnTheMachine_With6000RandomFlagsBanksAndCodes_HasNoMismatch()
    {
        // Fields of a vector: has_record, flag, record_code, header_code, rng_in, persistent words (byte offset, value), temporary words, item, flag_out, rng_out, draws.
        using var document = Read("roll_fuzz_vectors.json");
        var vectors = document.RootElement.GetProperty("vectors").EnumerateArray().ToList();
        Assert.Equal(6000, vectors.Count);
        var histogram = new int[3];
        foreach (var v in vectors)
        {
            var state = new AlundraGameState();
            foreach (var w in v[5].EnumerateArray())
            {
                SetBank(state, PersistentBank, I(w[0]) / 4, U(w[1]));
            }

            foreach (var w in v[6].EnumerateArray())
            {
                SetBank(state, TemporaryBank, I(w[0]) / 4, U(w[1]));
            }

            var stream = new Stream(U(v[4]));
            var result = AlundraLootRoll.Roll(I(v[0]) != 0, I(v[1]), I(v[2]), I(v[3]), state, stream.Next);
            var label = $"has_record {I(v[0])} flag 0x{I(v[1]):X} record 0x{I(v[2]):X} header 0x{I(v[3]):X} seed {U(v[4]):X8}";
            Assert.True(I(v[7]) == (int)result.ItemId, $"{label}: item {result.ItemId}, machine {I(v[7])}");
            Assert.True(I(v[8]) == result.GameFlag, $"{label}: +0x40 {result.GameFlag}, machine {I(v[8])}");
            Assert.True(U(v[9]) == stream.State, $"{label}: word {stream.State:X8}, machine {U(v[9]):X8}");
            Assert.True(I(v[10]) == result.Draws, $"{label}: draws {result.Draws}, machine {I(v[10])}");
            histogram[result.Draws]++;
        }

        // The oracle's own histogram (p1_roll.py): 0 draws 2105, 1 draw 3243, 2 draws 652.
        Assert.Equal(new[] { 2105, 3243, 652 }, histogram);
    }

    [Fact]
    public void ARollThatDrawsNothing_NeverCallsTheDraw()
    {
        // A code without bit 0x80 and a raised flag both give no draw: the delegate must not even be called (the shared stream does not move).
        var state = new AlundraGameState();
        state.GameFlags[0] |= 1u << 5;
        var calls = 0;
        uint Draw()
        {
            calls++;
            return 0;
        }

        Assert.Equal(0, AlundraLootRoll.Roll(false, 0, 0, 0x24, state, Draw).Draws);
        Assert.Equal(36u, AlundraLootRoll.Roll(false, 0, 0, 0x24, state, Draw).ItemId);
        var raised = AlundraLootRoll.Roll(true, 5, 0x82, 0xFC, state, Draw);
        Assert.Equal(new AlundraLootRoll.Result(0, 5, 0), raised);
        Assert.Equal(0, calls);
        var rolled = AlundraLootRoll.Roll(true, 0, 0x82, 0, state, Draw);
        Assert.Equal(1, rolled.Draws);
        Assert.Equal(1, calls);
    }
}
