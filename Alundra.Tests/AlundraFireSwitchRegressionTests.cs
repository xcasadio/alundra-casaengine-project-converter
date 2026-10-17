#nullable enable
using System;
using System.Linq;
using Xunit;
using Xunit.Abstractions;
using Alundra.Session;
using Alundra.Entities;
using Alundra.Events;

namespace Alundra.Tests;

/// <summary>
/// E20.c1b K4/K5 (docs/plan-e20-heros.md section 1.6), the regression E20.c1a introduced and this slice closes: since c1a a sword hit sets the target's <c>TouchingEntity</c>, so the program D of the target runs; 181 of
/// those programs start with the opcode <c>0x87</c>, which the DLL used to skip, so they decided on a <c>Result</c> left by an unrelated program of the shared state (A, D, E and F share one, the binary's
/// <c>0x8013D9E8</c>). The real targets here are the fire switches of map 303 (<c>Olen, Cephas house</c>, records 4 to 9: sprite 51, balance entry 307, the sword's response 16, <c>MoreFlags</c> 129, class A): the program
/// D of the record 4 sits at code <c>700</c> and is <c>0x87 [128, 6]</c> (is the thing that touches me a fire attack), a jump when <c>Result</c> is 0, then a flag and <c>0x2B</c> (clear class A). A sword is the
/// attack type 1, never the fire (6): nothing must be set, whatever a previous program left in the shared <c>Result</c>.
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraFireSwitchRegressionTests
{
    private const uint Square = 0x80;

    private readonly ITestOutputHelper _output;

    public AlundraFireSwitchRegressionTests(ITestOutputHelper output) => _output = output;

    /// <summary>The record, the start of its program D, and where the hero stands (pixels, on the platform of the switch, 22 pixels west of it).</summary>
    public static TheoryData<int, int, int, int> Switches() => new()
    {
        { 4, 700, 950, 184 },
        { 6, 724, 950, 280 },
    };

    [Theory]
    [MemberData(nameof(Switches))]
    public void ASwordHitOnAFireSwitchOfMap303_DoesNotLightIt_WhateverTheSharedResultHolds(int record, int programStart, int heroX, int heroY)
    {
        // The hero arrives inside the spawn zone of the switches (their records only exist when the player is in tiles 35-51 by 5-24).
        var spec = new ArcSpec("E20c1b-fire303", "Olen, Cephas house", "Olen, Cephas house-303", Array.Empty<int>(), 0, 0, 0, 2000, RealController: true, Prefabs: true,
            Arrival: new ArcArrival((38 * 24 + 12) << 16, (11 * 16 + 8) << 16, 0, GameState.ResetAnimationId, 24));
        using var arc = new ArcRun(spec);
        arc.Hero.TargetAnimationId = 0; // the map-entry animation ends through its chain, which the headless montage never plays.
        for (var i = 0; i < 6; i++)
        {
            arc.OneFrame();
        }

        var fire = arc.EntityByRecord(record);
        Assert.NotNull(fire);
        Assert.Equal(0x100 + 51, fire!.SpriteType);
        Assert.Equal(129, (int)(fire.Flags & 0xFF));
        Assert.NotEqual(0u, fire.Flags & EntityFlags.ClassA);
        Assert.Equal(programStart, ProgramOf(fire));

        // The hero faces east, one swing away from the switch; the shared Result holds a value some unrelated program of A, D, E or F left.
        arc.PlaceHero(heroX, heroY, 32);
        arc.Hero.TargetDirection = 24;
        for (var i = 0; i < 4; i++)
        {
            arc.OneFrame();
        }

        arc.Runner.SharedProgramStateForTests.Result = 7;
        arc.Press(Square);
        for (var i = 0; i < 30; i++)
        {
            arc.OneFrame();
        }

        var ran = arc.Trace.Where(r => r.ProgramStart == programStart).ToList();
        _output.WriteLine($"program D @{programStart}: " + string.Join(", ", ran.Select(r => $"f{r.Frame} pc{r.Pc} op0x{r.Opcode:X2} {r.Kind}")));
        _output.WriteLine($"class A kept: {(fire.Flags & EntityFlags.ClassA) != 0}, shared Result {arc.Runner.SharedProgramStateForTests.Result}");
        Assert.NotEmpty(ran); // the sword touched the switch, so its program D ran.

        // Nothing is set: not the flag (the opcode 0x05 of the program), not the clearing of class A (0x2B).
        Assert.DoesNotContain(ran, r => r.Opcode == 0x05);
        Assert.DoesNotContain(ran, r => r.Opcode == 0x2B);
        Assert.NotEqual(0u, fire.Flags & EntityFlags.ClassA);

        // The opcode 0x87 itself ran (it is not skipped by its size) and answered no: "is the attacker a fire attack" is false for the sword.
        var test = Assert.Single(ran, r => r.Opcode == 0x87);
        Assert.Equal(EventTraceKind.Implemented, test.Kind);
        Assert.Equal(0, arc.Runner.SharedProgramStateForTests.Result);
    }

    private static int ProgramOf(EntityScriptProxy entity)
    {
        // EventCodesDTable[(index & 0x7F)]: the table of this map is [0,0,0,0,0,700,712,724,...], and the record's D index is 133 + k.
        var table = new[] { 0, 0, 0, 0, 0, 700, 712, 724, 736, 748, 760 };
        return table[entity.ProgramIndexes[3] & 0x7F];
    }
}
