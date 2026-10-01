#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Alundra.Scripts;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E19.c1 T3 (docs/plan-e19-opcodes.md §1.2e): opcode 0x0C ("random cardinal direction"), which draws from the process-wide
/// <see cref="AlundraRandom"/> stream. The class is in the collection that serialises the generator's static state and
/// resets it before and after every test.
/// </summary>
[Collection(AlundraRandomStaticStateCollection.Name)]
public sealed class AlundraRandomOpcodeTests : IDisposable
{
    public AlundraRandomOpcodeTests() => AlundraRandom.Reset();

    public void Dispose() => AlundraRandom.Reset();

    [Fact]
    public void SetRandomCardinalDirection_0x0C_FiveDrawsGiveTheBinarysDirections_AndTheSeedAdvancesOnce()
    {
        var document = new EventProgramDocument
        {
            MapIndex = 1,
            EventCodesATable = new[] { 0, 0, 0, 0, 0, 0 },
            Codes = new[] { 0x0C, 0x0C, 0x0C, 0x0C, 0x0C, 0xFF },
        };
        var runner = new AlundraEventProgramRunner(document, new AlundraGameState());
        var entity = new AlundraEntityScriptProxy();
        var directions = new List<uint>();
        uint? seedAfterTheFirstDraw = null;
        var sizes = new List<int>();
        runner.TraceSink = record =>
        {
            if (record.Opcode != 0x0C)
            {
                return;
            }

            directions.Add(entity.TargetDirection);
            sizes.Add(record.Size);
            seedAfterTheFirstDraw ??= (uint)AlundraRandom.RandomSeed;
        };
        var state = new EventProgramState { Codes = document.CodesAsBytes() };

        runner.RunOneScriptCall(entity, state);

        Assert.Equal(new uint[] { 0x00, 0x18, 0x18, 0x08, 0x00 }, directions.ToArray());
        Assert.Equal(0x35E36190u, seedAfterTheFirstDraw);
        Assert.All(sizes, size => Assert.Equal(1, size));
    }
}
