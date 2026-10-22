#nullable enable
using System;
using Xunit;
using Alundra.Session;
using Alundra.Events;
using Alundra.Rng;
using Alundra.Entities;

namespace Alundra.Tests.Rng;

/// <summary>
/// E19.l1 L1-1 (docs/plan-e19-opcodes.md, section 1.2m.1): opcode 0x8C (<c>0x80040438</c>), one draw of the shared generator,
/// <c>Result</c> = 1 when <c>(seed &gt;&gt; 24) &gt;= v1</c>. The class is in the collection that serialises the generator's
/// static state and resets it before and after every test.
/// </summary>
[Collection(RandomStaticStateCollection.Name)]
public sealed class RandomBranchOpcodeTests : IDisposable
{
    public RandomBranchOpcodeTests() => GameRandom.Reset();

    public void Dispose() => GameRandom.Reset();

    private static int Run(int threshold, EventProgramRunner? runner = null)
    {
        var document = new EventProgramDocument
        {
            MapIndex = 1,
            EventCodesATable = new[] { 0, 0, 0, 0, 0, 0 },
            Codes = new[] { 0x8C, threshold, 0xFF },
        };
        runner ??= new EventProgramRunner(document, new GameState());
        var state = new EventProgramState { Codes = document.CodesAsBytes(), Result = 7 };

        runner.RunOneScriptCall(new EntityScriptProxy(), state);

        Assert.Equal(2, state.CodeIndex);
        return state.Result;
    }

    [Fact]
    public void IfRandomAtLeast_0x8C_TheFirstDrawIs53_SoThreshold128GivesZero_AndTheSeedAdvancesOnce()
    {
        var result = Run(128);

        Assert.Equal(0, result);
        Assert.Equal(0x35E36190u, (uint)GameRandom.RandomSeed);
    }

    [Fact]
    public void IfRandomAtLeast_0x8C_TheComparisonIsGreaterOrEqual()
    {
        Assert.Equal(1, Run(53));

        GameRandom.Reset();

        Assert.Equal(0, Run(54));
    }

    [Fact]
    public void IfRandomAtLeast_0x8C_TwoCallsDrawTwiceFromTheSameStream()
    {
        Assert.Equal(0, Run(128)); // r = 53

        Assert.Equal(1, Run(128)); // r = 200
    }
}
