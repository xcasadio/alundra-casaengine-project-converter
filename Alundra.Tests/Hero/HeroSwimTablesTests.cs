#nullable enable
using System;
using System.IO;
using Xunit;
using Alundra.Hero;

namespace Alundra.Tests;

/// <summary>
/// E20.i T1 (docs/plan-e20-heros.md section 1.14): the swim table and the names of the swim. The table <c>0x80022E7C</c> (80 words, the address of a handler per state) is compared entry by entry with the executable
/// (<c>ALUN_CD.EXE</c>, France; file offset <c>0x800 + address - 0x80020000</c>; the path of <see cref="AlundraHeroRunTablesTests"/>, or the variable <c>ALUNDRA_EXE</c>).
/// </summary>
public sealed class AlundraHeroSwimTablesTests
{
    private const string DefaultExecutable = @"D:\development\repo\Alundra Remake\Alundra (France)\Alundra (France)_extracted\ALUN_CD.EXE";

    private static byte[] Executable()
    {
        var path = Environment.GetEnvironmentVariable("ALUNDRA_EXE") is { Length: > 0 } custom ? custom : DefaultExecutable;
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("the executable of the France version is missing: this test compares the swim table with it (set ALUNDRA_EXE to its path)", path);
        }

        return File.ReadAllBytes(path);
    }

    private static uint Word(byte[] executable, uint address) => BitConverter.ToUInt32(executable, 0x800 + (int)(address - 0x80020000));

    private static PlayerManager.SwimHandler HandlerAt(uint address) => address switch
    {
        0x80032674 => PlayerManager.SwimHandler.Float,
        0x80032658 => PlayerManager.SwimHandler.FlipThenFloat,
        0x80032680 => PlayerManager.SwimHandler.ItemOnly,
        0x80032690 => PlayerManager.SwimHandler.Steer,
        0x800326FC => PlayerManager.SwimHandler.HurtEnd,
        0x80032758 => PlayerManager.SwimHandler.Skip,
        _ => throw new InvalidOperationException($"the swim table of the executable holds the address 0x{address:X8}, which is not one of the six handlers"),
    };

    [Fact]
    public void T1a_TheSwimTable_IsTheOneOfTheExecutable_EntryByEntry()
    {
        var executable = Executable();

        Assert.Equal(0x50, PlayerManager.SwimTable.Length);
        for (uint id = 0; id < 0x50; id++)
        {
            var address = Word(executable, 0x80022E7C + 4 * id);
            Assert.True(
                HandlerAt(address) == PlayerManager.SwimHandlerOf(id),
                $"state 0x{id:X2}: the executable sends it to 0x{address:X8} ({HandlerAt(address)}), the table of the DLL says {PlayerManager.SwimHandlerOf(id)}");
        }
    }

    [Fact]
    public void T1b_AStateFrom0x50On_IsSkipped()
    {
        // 0x8003261C: a state >= 0x50 leaves the table and goes to 0x80032758. Spot checks on the way: the cancel group, not the float group.
        foreach (var id in new uint[] { 0x50, 0x51, 0x7F, 0xFF, 0x1000, uint.MaxValue })
        {
            Assert.Equal(PlayerManager.SwimHandler.Skip, PlayerManager.SwimHandlerOf(id));
        }
    }

    [Fact]
    public void T1c_TheSixGroups_HaveTheMembersTheExecutableGives()
    {
        static uint[] Members(PlayerManager.SwimHandler handler)
        {
            var list = new System.Collections.Generic.List<uint>();
            for (uint id = 0; id < 0x50; id++)
            {
                if (PlayerManager.SwimHandlerOf(id) == handler)
                {
                    list.Add(id);
                }
            }

            return list.ToArray();
        }

        Assert.Equal(new uint[] { 0x0F, 0x1D }, Members(PlayerManager.SwimHandler.Steer));
        Assert.Equal(new uint[] { 0x1C, 0x31, 0x3E }, Members(PlayerManager.SwimHandler.FlipThenFloat));
        Assert.Equal(new uint[] { 0x28, 0x3A }, Members(PlayerManager.SwimHandler.ItemOnly));
        Assert.Equal(new uint[] { 0x3B }, Members(PlayerManager.SwimHandler.HurtEnd));
        Assert.Equal(new uint[] { 0x0D, 0x1A, 0x1B, 0x1E, 0x27, 0x37, 0x38, 0x3D, 0x43, 0x45, 0x47, 0x48, 0x4A, 0x4C, 0x4D }, Members(PlayerManager.SwimHandler.Skip));
        Assert.Equal(0x50 - 2 - 3 - 2 - 1 - 15, Members(PlayerManager.SwimHandler.Float).Length);
    }

    [Fact]
    public void T1d_TheNamesOfTheFiveSwimStates_AndTheMaskOfTheDive()
    {
        Assert.Equal(0x0Fu, PlayerManager.SwimAnimationId);
        Assert.Equal(0x1Du, PlayerManager.FloatAnimationId);
        Assert.Equal(0x28u, PlayerManager.DiveAnimationId);
        Assert.Equal(0x3Au, PlayerManager.SwimHurtAnimationId);
        Assert.Equal(0x3Bu, PlayerManager.SwimHurtEndAnimationId);

        // pressed & 0xD0 (0x800326D0): the Triangle, the Cross and the Square; the Circle (0x20) is not in it.
        Assert.Equal(0xD0u, PlayerManager.SwimDiveButtons);
        Assert.Equal(0u, PlayerManager.SwimDiveButtons & PadState.Circle);
    }
}
