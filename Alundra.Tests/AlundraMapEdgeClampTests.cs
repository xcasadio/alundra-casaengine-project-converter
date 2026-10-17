#nullable enable
using System;
using Alundra.Scripts;
using Xunit;
using Alundra.Entities.Spawning;

namespace Alundra.Tests;

/// <summary>
/// E20.e0k3 (docs/plan-e20-heros.md section 1.18.3, T1, phase A): the pure clamp of the step to the grid of the map, <see cref="AlundraScriptedMotion.ClampStepToMapEdge"/>, the binary's forces pass (<c>0x800366FC</c>-<c>0x800367E0</c>).
/// Nothing in the tick calls it yet (phase B wires it). The bounds are derived HERE from the offsets of the box, never read back from the entity: <c>PosX + ModX &gt;= 0</c> and <c>PosX + ((offX + sizeX) &lt;&lt; 16) &lt;= 0x4E00000</c>
/// (the same on Y with <c>0x3C00000</c>). Each case without effect is set next to a positive case in the same assertion. The comparison is STRICT, as the binary's two <c>slt</c> (<c>0x80036774</c>, <c>0x80036788</c>, <c>0x800367AC</c>,
/// <c>0x800367C0</c>): a step that arrives exactly on a bound is neither bounded nor flagged (the oracle key <c>edge-exact</c>).
/// </summary>
public sealed class AlundraMapEdgeClampTests
{
    private const int HighX = 0x4E00000;
    private const int HighY = 0x3C00000;
    private const int Px = 65536;

    // Offsets of the box: the Beannoid (map 14, sprite 79), the hero, and an asymmetric box that tells the offsets from the sizes.
    public static readonly TheoryData<int, int, int, int> Boxes = new()
    {
        { -10, -7, 20, 14 },
        { -10, -7, 21, 15 },
        { -5, -9, 11, 18 },
    };

    private static AlundraEntityScriptProxy Box(int offX, int offY, int sizeX, int sizeY, int posX, int posY)
    {
        var entity = new AlundraEntityScriptProxy();
        AlundraEntitySpawnFactory.SetEntityDimensions(entity, offX, offY, 0, sizeX, sizeY, 8);
        entity.PosX = posX;
        entity.PosY = posY;
        return entity;
    }

    private static (int X, int Y, bool Adjusted) Clamp(AlundraEntityScriptProxy entity, int stepX, int stepY)
    {
        var result = AlundraScriptedMotion.ClampStepToMapEdge(entity, stepX, stepY);
        return (result.StepX, result.StepY, result.Adjusted);
    }

    [Fact]
    public void A_AStepThatHolds_IsLeftAlone_AndAStepThatGoesOver_IsBoundedAndFlagged()
    {
        var holds = Box(-10, -7, 20, 14, 600 * Px, 400 * Px);
        var over = Box(-10, -7, 20, 14, 1237 * Px, 400 * Px);
        var highX = HighX - ((-10 + 20) << 16);

        // The step that holds, then the same step further east: X bounded at the high bound, Y left alone (a diagonal step), the flag raised.
        Assert.Equal(
            ((3 * Px, -2 * Px, false), (highX - 1237 * Px, -2 * Px, true)),
            (Clamp(holds, 3 * Px, -2 * Px), Clamp(over, 3 * Px, -2 * Px)));
        // The function does not move the entity (it bounds a step, the caller moves).
        Assert.Equal((600 * Px, 400 * Px, 1237 * Px, 400 * Px), (holds.PosX, holds.PosY, over.PosX, over.PosY));
    }

    [Theory]
    [MemberData(nameof(Boxes))]
    public void B_TheFourEdges_AreTheOnesOfTheBoxOffsets_AndTheStepInwardIsLeftAlone(int offX, int offY, int sizeX, int sizeY)
    {
        var modX = offX << 16;
        var modY = offY << 16;
        var lowX = -modX;                                   // PosX + ModX >= 0
        var lowY = -modY;
        var highX = HighX - ((offX + sizeX) << 16);         // PosX + ((offX + sizeX) << 16) <= 0x4E00000
        var highY = HighY - ((offY + sizeY) << 16);
        var out3 = 3 * Px;

        // West: 1 px inside the low bound, 3 px west bounded at the bound (PosX + ModX == 0), the same 3 px east left alone.
        var west = Box(offX, offY, sizeX, sizeY, lowX + Px, 400 * Px);
        var westBounded = Clamp(west, -out3, 0);
        Assert.Equal(((out3, 0, false), (-Px, 0, true)), (Clamp(west, out3, 0), westBounded));
        Assert.Equal(0, west.PosX + westBounded.X + modX);

        // East.
        var east = Box(offX, offY, sizeX, sizeY, highX - Px, 400 * Px);
        var eastBounded = Clamp(east, out3, 0);
        Assert.Equal(((-out3, 0, false), (Px, 0, true)), (Clamp(east, -out3, 0), eastBounded));
        Assert.Equal(HighX, east.PosX + eastBounded.X + ((offX + sizeX) << 16));

        // North (Y low).
        var north = Box(offX, offY, sizeX, sizeY, 600 * Px, lowY + Px);
        var northBounded = Clamp(north, 0, -out3);
        Assert.Equal(((0, out3, false), (0, -Px, true)), (Clamp(north, 0, out3), northBounded));
        Assert.Equal(0, north.PosY + northBounded.Y + modY);

        // South (Y high).
        var south = Box(offX, offY, sizeX, sizeY, 600 * Px, highY - Px);
        var southBounded = Clamp(south, 0, out3);
        Assert.Equal(((0, -out3, false), (0, Px, true)), (Clamp(south, 0, -out3), southBounded));
        Assert.Equal(HighY, south.PosY + southBounded.Y + ((offY + sizeY) << 16));
    }

    [Theory]
    [MemberData(nameof(Boxes))]
    public void C_AStepThatArrivesExactlyOnABound_IsNeitherBoundedNorFlagged_AndOneRawUnitFurtherIs(int offX, int offY, int sizeX, int sizeY)
    {
        var lowX = -(offX << 16);
        var lowY = -(offY << 16);
        var highX = HighX - ((offX + sizeX) << 16);
        var highY = HighY - ((offY + sizeY) << 16);
        const int step = 196608;                            // 3 px, the step of the oracle key edge-exact
        var cases = new (string Name, AlundraEntityScriptProxy Entity, int Dx, int Dy)[]
        {
            ("east", Box(offX, offY, sizeX, sizeY, highX - step, 400 * Px), step, 0),
            ("west", Box(offX, offY, sizeX, sizeY, lowX + step, 400 * Px), -step, 0),
            ("south", Box(offX, offY, sizeX, sizeY, 600 * Px, highY - step), 0, step),
            ("north", Box(offX, offY, sizeX, sizeY, 600 * Px, lowY + step), 0, -step),
        };

        foreach (var (name, entity, dx, dy) in cases)
        {
            // Exactly on the bound: the step as asked, no flag. One raw unit further out: bounded back to the bound (the step as asked), flagged.
            var exact = Clamp(entity, dx, dy);
            var further = Clamp(entity, dx + Math.Sign(dx), dy + Math.Sign(dy));
            Assert.True(
                exact == (dx, dy, false) && further == (dx, dy, true),
                $"{name}: exactly on the bound {exact} (expected {(dx, dy, false)}), one raw unit further {further} (expected {(dx, dy, true)})");
        }
    }
}
