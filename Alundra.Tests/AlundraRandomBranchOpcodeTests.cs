#nullable enable
using System;
using Alundra.Scripts;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E19.l1 L1-1 (docs/plan-e19-opcodes.md, section 1.2m.1): opcode 0x8C (<c>0x80040438</c>), one draw of the shared generator,
/// <c>Result</c> = 1 when <c>(seed &gt;&gt; 24) &gt;= v1</c>. The class is in the collection that serialises the generator's
/// static state and resets it before and after every test.
/// </summary>
[Collection(AlundraRandomStaticStateCollection.Name)]
public sealed class AlundraRandomBranchOpcodeTests : IDisposable
{
    public AlundraRandomBranchOpcodeTests() => AlundraRandom.Reset();

    public void Dispose() => AlundraRandom.Reset();

    private static int Run(int threshold, AlundraEventProgramRunner? runner = null)
    {
        var document = new EventProgramDocument
        {
            MapIndex = 1,
            EventCodesATable = new[] { 0, 0, 0, 0, 0, 0 },
            Codes = new[] { 0x8C, threshold, 0xFF },
        };
        runner ??= new AlundraEventProgramRunner(document, new AlundraGameState());
        var state = new EventProgramState { Codes = document.CodesAsBytes(), Result = 7 };

        runner.RunOneScriptCall(new AlundraEntityScriptProxy(), state);

        Assert.Equal(2, state.CodeIndex);
        return state.Result;
    }

    [Fact]
    public void IfRandomAtLeast_0x8C_TheFirstDrawIs53_SoThreshold128GivesZero_AndTheSeedAdvancesOnce()
    {
        var result = Run(128);

        Assert.Equal(0, result);
        Assert.Equal(0x35E36190u, (uint)AlundraRandom.RandomSeed);
    }

    [Fact]
    public void IfRandomAtLeast_0x8C_TheComparisonIsGreaterOrEqual()
    {
        Assert.Equal(1, Run(53));

        AlundraRandom.Reset();

        Assert.Equal(0, Run(54));
    }

    [Fact]
    public void IfRandomAtLeast_0x8C_TwoCallsDrawTwiceFromTheSameStream()
    {
        Assert.Equal(0, Run(128)); // r = 53

        Assert.Equal(1, Run(128)); // r = 200
    }
}
