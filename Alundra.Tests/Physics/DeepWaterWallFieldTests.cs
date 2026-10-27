#nullable enable
using Microsoft.Xna.Framework;
using Xunit;
using Alundra.Physics;
using Alundra.Tests.Support;

namespace Alundra.Tests.Physics;

/// <summary>
/// E20.i2a T1 (c1, docs/plan-e20-heros.md section 1.14.2, D-I2A-1): the pseudo-bit of the walkability mask that makes <see cref="CellsCollisionField"/> block a corner standing on deep water, as the hero's
/// corner test of the binary does without the swim boots (<c>0x80037504</c>, <c>0x80037558-0x80037560</c>: <c>(half-word of the cell &amp; 0xE00) == 0x800</c>, with no condition on the height). The bit is above the 16 bits
/// of <c>walkability | groundProperty &lt;&lt; 8</c>, so no real bit of the mask collides with it; the test is an EQUALITY on <c>0xE00</c>, never a bit test (the ladder, <c>0xC00</c>, has the bit <c>0x800</c> too).
/// </summary>
public class DeepWaterWallFieldTests
{
    /// <summary>The pseudo-bit (T0 (d)); the constant of production is asserted equal to it at the code commit.</summary>
    private const uint Bit = 0x10000;

    private const int GroundDeepWater = 0x18; // 0x1800 & 0xE00 == 0x800
    private const int GroundDeepWaterBare = 0x08; // 0x0800 & 0xE00 == 0x800
    private const int GroundLadder = 0x0C; // 0x0C00 & 0xE00 == 0xC00: has the bit 0x800, is not the deep water
    private const int GroundShallow = 0x10; // 0x1000 & 0xE00 == 0
    private const int GroundOther = 0x1A; // 0x1A00 & 0xE00 == 0xA00

    private static CellsCollisionField Field(int groundProperty, int height = 0)
        => FlatCells.Create(4, 4, cell: (c, r) => (0, height), groundProperty: (c, r) => groundProperty);

    private static bool Walkable(CellsCollisionField field, uint mask)
    {
        Assert.True(field.TrySampleGround(new Vector3(36f, 24f, 0f), float.MaxValue, mask, out var sample));
        return sample.IsWalkable;
    }

    [Theory]
    [InlineData(GroundDeepWater)]
    [InlineData(GroundDeepWaterBare)]
    public void TheBit_BlocksTheCellsOfTheDeepWater_AndWithoutTheBitTheyAreWalkable(int groundProperty)
    {
        var field = Field(groundProperty);

        Assert.True(Walkable(field, CellsCollisionField.BaseWalkabilityMask));
        Assert.False(Walkable(field, CellsCollisionField.BaseWalkabilityMask | Bit));
    }

    [Theory]
    [InlineData(GroundLadder)]
    [InlineData(GroundShallow)]
    [InlineData(GroundOther)]
    [InlineData(0)]
    public void TheBit_BlocksOnlyTheEqualityOnE00_NotTheLadderNorAnyOtherCell(int groundProperty)
    {
        var field = Field(groundProperty);

        Assert.True(Walkable(field, CellsCollisionField.BaseWalkabilityMask | Bit));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void TheBit_HasNoConditionOnTheHeight(int height)
    {
        Assert.False(Walkable(Field(GroundDeepWater, height), CellsCollisionField.BaseWalkabilityMask | Bit));
        Assert.True(Walkable(Field(GroundDeepWater, height), CellsCollisionField.BaseWalkabilityMask));
    }

    [Fact]
    public void TheBit_DoesNotChangeTheOtherBitsOfTheMask()
    {
        // A wall cell (walkability 0x40) stays blocked with or without the bit; a free cell stays free.
        var wall = FlatCells.Create(4, 4, cell: (c, r) => (0x40, 0));
        var free = FlatCells.Create(4, 4);

        Assert.False(Walkable(wall, CellsCollisionField.BaseWalkabilityMask));
        Assert.False(Walkable(wall, CellsCollisionField.BaseWalkabilityMask | Bit));
        Assert.True(Walkable(free, CellsCollisionField.BaseWalkabilityMask | Bit));
    }
}
