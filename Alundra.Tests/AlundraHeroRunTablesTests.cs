#nullable enable
using System;
using System.IO;
using Alundra.Scripts;
using Xunit;
using Alundra.Entities;

namespace Alundra.Tests;

/// <summary>
/// E20.d1 T1 (docs/plan-e20-heros.md section 1.13): the tables, the field and the helper of the run. The two tables of the run are compared word by word with the executable (<c>ALUN_CD.EXE</c>, France; file offset
/// <c>0x800 + address - 0x80020000</c>); the path is the oracle's (<c>docs/plan-e20-annexe/e20-d/oracle/base/tools/lib.py</c>) unless the variable <c>ALUNDRA_EXE</c> names another one, and the test fails naming it when the file is absent.
/// </summary>
public sealed class AlundraHeroRunTablesTests
{
    private const string DefaultExecutable = @"D:\development\repo\Alundra Remake\Alundra (France)\Alundra (France)_extracted\ALUN_CD.EXE";

    private static byte[] Executable()
    {
        var path = Environment.GetEnvironmentVariable("ALUNDRA_EXE") is { Length: > 0 } custom ? custom : DefaultExecutable;
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"the executable of the France version is missing: this test compares the tables of the run with it (set ALUNDRA_EXE to its path)", path);
        }

        return File.ReadAllBytes(path);
    }

    private static int[] Words(byte[] executable, uint address, int count)
    {
        var offset = 0x800 + (int)(address - 0x80020000);
        var words = new int[count];
        for (var i = 0; i < count; i++)
        {
            words[i] = BitConverter.ToInt32(executable, offset + 4 * i);
        }

        return words;
    }

    [Fact]
    public void T1a_TheDashTable_IsTheOneOfTheExecutable_WordByWord()
    {
        var expected = Words(Executable(), 0x80022CAC, 16);

        Assert.Equal(16, AnimationTables.DashDirectionByNibble.Length);
        Assert.Equal(expected, AnimationTables.DashDirectionByNibble);

        // And its meaning: a single cardinal bit gives the direction, up 0x10, right 0x18, down 0, left 8; everything else -1.
        Assert.Equal(0x10, AnimationTables.DashDirectionByNibble[1]);
        Assert.Equal(0x18, AnimationTables.DashDirectionByNibble[2]);
        Assert.Equal(0, AnimationTables.DashDirectionByNibble[4]);
        Assert.Equal(8, AnimationTables.DashDirectionByNibble[8]);
    }

    [Fact]
    public void T1b_TheConeTable_IsTheOneOfTheExecutable_WordByWord()
    {
        var expected = Words(Executable(), 0x80022CEC, 12);

        Assert.Equal(12, AnimationTables.SprintConeRows.Length);
        for (var i = 0; i < 12; i++)
        {
            Assert.Equal(expected[i], (int)AnimationTables.SprintConeRows[i]);
        }
    }

    [Fact]
    public void T1c_TheConeRows_AreIndexedByTheFacingOfTheDll_DownUpLeftRight()
    {
        // The facing of the DLL (0 down, 1 up, 2 left, 3 right) is the order of the rows: the middle word of a row is the facing's own direction.
        Assert.Equal(0u, AnimationTables.SprintConeRows[3 * 0 + 1]);
        Assert.Equal(0x10u, AnimationTables.SprintConeRows[3 * 1 + 1]);
        Assert.Equal(8u, AnimationTables.SprintConeRows[3 * 2 + 1]);
        Assert.Equal(0x18u, AnimationTables.SprintConeRows[3 * 3 + 1]);
    }

    [Fact]
    public void T1d_TheResumeCounter_IsAFieldOfTheHero_AtZero()
    {
        var hero = new AlundraEntityScriptProxy();
        Assert.Equal(0, hero.StopSprintCounter);
    }

    [Theory]
    [InlineData(3, 0x10u, 1)] // facing right, direction up: the table gives up.
    [InlineData(1, 0x18u, 3)] // facing up, direction right.
    [InlineData(3, 0x18u, 3)] // nothing to change.
    [InlineData(2, 0x00u, 0)] // facing left, direction down.
    [InlineData(0, 0x08u, 2)] // facing down, direction left.
    public void T1e_TheFacingOfTheTick_IsTheTableLookup_WithoutWritingTheField(int facing, uint direction, int expected)
    {
        var hero = new AlundraEntityScriptProxy { AnimationDirection = facing, TargetDirection = direction };

        Assert.Equal(expected, AlundraPlayerManager.TickFacing(hero));
        Assert.Equal(facing, hero.AnimationDirection);
    }

    [Fact]
    public void T1f_TheNamesOfTheRunStates_AreTheOnesOfTheBinary()
    {
        Assert.Equal(0x03u, AlundraPlayerManager.SprintAnimationId);
        Assert.Equal(0x04u, AlundraPlayerManager.TackleAnimationId);
        Assert.Equal(0x29u, AlundraPlayerManager.PrepareSprintAnimationId);
        Assert.Equal(0x2Au, AlundraPlayerManager.StopSprintAnimationId);
        Assert.Equal(0x3Eu, AlundraPlayerManager.SprintBounceAnimationId);
        Assert.Equal(0x1Cu, AlundraPlayerManager.HurtBounceAnimationId);
    }
}
