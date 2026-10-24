#nullable enable
using System.Collections.Generic;
using Xunit;
using Alundra.Entities;
using Alundra.Physics;
using Alundra.Tests.Support;

namespace Alundra.Tests.Physics;

/// <summary>
/// E20.g2c1 T1 (docs/plan-e20-heros.md section 1.24.5, phase A, (l3), D-G2C1-3): the reader of the four corner words of C4 on a synthetic field: 16 bits per corner (the walkability byte plus the ground property byte shifted by 8), at the CURRENT position, the corners being X - 10 and X + 10,
/// Y - 7 and Y + 7 (the geometry of <c>UpdateGroundSlope</c>), with NO qualification by height; C4 tests <c>(word &amp; 0x1001) == 0x1001</c> on each: a band <c>0x1001</c> and a wall <c>0x1041</c> have it, <c>0x1000</c>, <c>0x0001</c> and <c>0x1040</c> do not. The cells are 24 x 16 px: the entity stands on
/// the junction of four cells (x = 120, y = 80) so that each corner reads its own cell.
/// </summary>
public sealed class CornerTileWordsTests
{
    private const int Px = 1 << 16;

    /// <summary>The cells (column, row) -> (walkability, ground property); the four around (120, 80) are cells (4, 4), (5, 4), (4, 5), (5, 5).</summary>
    private static CutBench Bench(IReadOnlyDictionary<(int Column, int Row), (int Walkability, int GroundProperty)> cells, int height = 0)
    {
        var bench = CutBench.Create((column, row) => cells.TryGetValue((column, row), out var c) ? new CutBench.Cell(c.Walkability, height, null, c.GroundProperty) : default);
        var entity = bench.Entity;
        entity.PosX = 120 * Px;
        entity.PosY = 80 * Px;
        entity.PosZ = 0;
        entity.ModX = -10 * Px;
        entity.Width = (21 << 16) - 1;
        entity.ModY = -7 * Px;
        entity.Height = (15 << 16) - 1;
        return bench;
    }

    [Fact]
    public void TheFourCorners_ReadTheirOwnCell_InTheOrderTopLeftTopRightBottomLeftBottomRight()
    {
        var bench = Bench(new Dictionary<(int, int), (int, int)>
        {
            [(4, 4)] = (0x01, 0x10), // band
            [(5, 4)] = (0x41, 0x10), // wall on the band
            [(4, 5)] = (0x00, 0x10),
            [(5, 5)] = (0x01, 0x00),
        });

        var words = CornerTileWords.Read(bench.Entity);

        Assert.Equal(new uint[] { 0x1001, 0x1041, 0x1000, 0x0001 }, words);
    }

    [Theory]
    [InlineData(0x1001u, true)]
    [InlineData(0x1041u, true)]
    [InlineData(0x9001u, true)]
    [InlineData(0x1000u, false)]
    [InlineData(0x0001u, false)]
    [InlineData(0x1040u, false)]
    [InlineData(0x0000u, false)]
    public void TheMask_IsBothBits0x1001_OnAnyCorner(uint word, bool expected)
    {
        Assert.Equal(expected, CornerTileWords.AnyHasMask(new uint[] { 0, 0, word, 0 }));
        Assert.Equal(expected, CornerTileWords.AnyHasMask(new uint[] { word, 0, 0, 0 }));
        Assert.False(CornerTileWords.AnyHasMask(new uint[] { 0, 0, 0, 0 }));
    }

    [Fact]
    public void TheReaderDoesNotQualifyByHeight_AFlyingEntityReadsTheBandUnderIt_NextToAnEntityOnTheGround()
    {
        var cells = new Dictionary<(int, int), (int, int)> { [(4, 4)] = (0x01, 0x10), [(5, 4)] = (0x01, 0x10), [(4, 5)] = (0x01, 0x10), [(5, 5)] = (0x01, 0x10) };
        var bench = Bench(cells, height: 3);

        var onTheGround = CornerTileWords.Read(bench.Entity);
        bench.Entity.PosZ = 40 * Px;
        var flying = CornerTileWords.Read(bench.Entity);

        Assert.Equal(new uint[] { 0x1001, 0x1001, 0x1001, 0x1001 }, onTheGround);
        Assert.Equal(onTheGround, flying);
        Assert.True(CornerTileWords.AnyHasMask(flying));
    }

    [Fact]
    public void TheReaderFollowsTheCurrentPosition_AndTheBandOnlyUnderOneCornerIsEnough()
    {
        var bench = Bench(new Dictionary<(int, int), (int, int)> { [(5, 5)] = (0x01, 0x10) });

        Assert.Equal(new uint[] { 0, 0, 0, 0x1001 }, CornerTileWords.Read(bench.Entity));
        Assert.True(CornerTileWords.AnyHasMask(CornerTileWords.Read(bench.Entity)));

        // one cell to the right: the corners are now in the columns 5 and 6, the band is under the left corners.
        bench.Entity.PosX = 144 * Px;
        Assert.Equal(new uint[] { 0, 0, 0x1001, 0 }, CornerTileWords.Read(bench.Entity));

        // far from the band: no word has the mask.
        bench.Entity.PosX = 24 * Px;
        Assert.False(CornerTileWords.AnyHasMask(CornerTileWords.Read(bench.Entity)));
    }

    [Fact]
    public void WithoutACellsField_TheWordsAreZero()
    {
        var entity = new EntityScriptProxy { PosX = 120 * Px, PosY = 80 * Px, ModX = -10 * Px, Width = (21 << 16) - 1, ModY = -7 * Px, Height = (15 << 16) - 1 };

        Assert.Equal(new uint[] { 0, 0, 0, 0 }, CornerTileWords.Read(entity));
    }
}
