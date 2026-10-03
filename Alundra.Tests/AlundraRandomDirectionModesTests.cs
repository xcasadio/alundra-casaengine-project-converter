#nullable enable
using System;
using System.IO;
using Alundra.Scripts;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E19.m0 (docs/plan-e19-opcodes.md, section 1.2s.1): modes 4 and 5 of <c>ResolveDirectionFromParam</c> (binary <c>0x8003CFC8</c>) draw
/// from the shared generator. Mode 4 = <c>CardinalDirectionTable[new &gt;&gt; 30]</c>, mode 5 = <c>new &gt;&gt; 27</c>, the low five
/// bits of the parameter are ignored. The class is in the collection that serialises the generator's static state; every test pins
/// the seed itself and compares the state as <c>(uint)RandomSeed</c> (the DLL state is an unmasked ulong).
/// </summary>
[Collection(AlundraRandomStaticStateCollection.Name)]
public sealed class AlundraRandomDirectionModesTests : IDisposable
{
    private const uint Mode4 = 0x80;
    private const uint Mode5 = 0xA0;

    public AlundraRandomDirectionModesTests() => AlundraRandom.Reset();

    public void Dispose() => AlundraRandom.Reset();

    private static AlundraEventProgramRunner NewRunner()
    {
        var document = new EventProgramDocument
        {
            MapIndex = 1,
            EventCodesATable = new[] { 0, 0, 0, 0, 0, 0 },
            Codes = new[] { 0xFF },
        };
        return new AlundraEventProgramRunner(document, new AlundraGameState());
    }

    private static uint[] Draw(ulong seed, params uint[] encodedDirs)
    {
        AlundraRandom.RandomSeed = seed;
        var runner = NewRunner();
        var entity = new AlundraEntityScriptProxy();
        var values = new uint[encodedDirs.Length];
        for (var i = 0; i < encodedDirs.Length; i++)
        {
            values[i] = runner.ResolveDirectionFromParam(entity, encodedDirs[i]);
        }

        return values;
    }

    [Theory]
    [InlineData(0xB017C93DUL, new uint[] { 0, 24, 24 })]
    [InlineData(0x00000000UL, new uint[] { 24, 8, 16 })]
    [InlineData(0x12345678UL, new uint[] { 8, 0, 16 })]
    public void Mode4_ThreeConsecutiveCalls_CardinalTableIndexedByTheTopTwoBitsOfTheNewSeed(ulong seed, uint[] expected)
    {
        Assert.Equal(expected, Draw(seed, Mode4, Mode4, Mode4));
    }

    [Theory]
    [InlineData(0xB017C93DUL, new uint[] { 6, 25, 28 })]
    [InlineData(0x00000000UL, new uint[] { 28, 19, 15 })]
    [InlineData(0x12345678UL, new uint[] { 23, 4, 11 })]
    public void Mode5_ThreeConsecutiveCalls_TheTopFiveBitsOfTheNewSeed(ulong seed, uint[] expected)
    {
        Assert.Equal(expected, Draw(seed, Mode5, Mode5, Mode5));
    }

    [Fact]
    public void Modes4And5_AlternatingOnOneStream_DrawOncePerCall()
    {
        Assert.Equal(new uint[] { 0, 25, 24 }, Draw(0xB017C93DUL, Mode4, Mode5, Mode4));
        Assert.Equal(0xE4013D62u, (uint)AlundraRandom.RandomSeed);
    }

    [Fact]
    public void Mode4_AdvancesTheSeedOncePerCall()
    {
        Draw(0xB017C93DUL, Mode4);
        Assert.Equal(0x35E36190u, (uint)AlundraRandom.RandomSeed);
    }

    [Fact]
    public void Mode4_TheLowFiveBitsAreIgnored_0x9FBehavesLike0x80()
    {
        Assert.Equal(new uint[] { 0, 24, 24 }, Draw(0xB017C93DUL, 0x9F, 0x9F, 0x9F));
    }

    [Fact]
    public void Mode5_TheLowFiveBitsAreIgnored_0xBFBehavesLike0xA0()
    {
        Assert.Equal(new uint[] { 6, 25, 28 }, Draw(0xB017C93DUL, 0xBF, 0xBF, 0xBF));
    }

    private static string? FindProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "alundra-project");
            if (Directory.Exists(Path.Combine(candidate, "Maps")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null; // self-skip: alundra-project/ not present in this checkout
    }

    [Fact]
    public void RealMap167_OneFullRunnerCallFrom0x5AAt144_DrawsOnceAndReachesTheWaitAt152()
    {
        var projectRoot = FindProjectRoot();
        if (projectRoot == null)
        {
            return; // self-skip
        }

        var document = MapEventProgramLoader.Load(projectRoot, "Inoa (inner)-167");
        if (document == null)
        {
            return; // self-skip: export absent
        }

        var codes = document.CodesAsBytes();
        Assert.Equal(new byte[] { 0x5A, 0x80, 0x80 }, codes[144..147]);

        AlundraRandom.RandomSeed = 0xB017C93D;
        var runner = new AlundraEventProgramRunner(document, new AlundraGameState());
        var entity = new AlundraEntityScriptProxy();
        var state = new EventProgramState { Codes = codes, CodeIndex = 144 };

        runner.RunOneScriptCall(entity, state);

        Assert.Equal(152, state.CodeIndex);
        Assert.Equal(0u, entity.TargetDirection);
        Assert.Equal(8u, entity.TargetAnimationId);
        Assert.Equal(0x35E36190u, (uint)AlundraRandom.RandomSeed);
    }
}
