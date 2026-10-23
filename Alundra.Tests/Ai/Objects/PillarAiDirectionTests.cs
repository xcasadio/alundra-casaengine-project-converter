#nullable enable
using Xunit;
using Alundra.Ai.Objects;
using Alundra.Entities;

namespace Alundra.Tests.Ai.Objects;

/// <summary>
/// E20.g5a T1 (docs/plan-e20-heros.md section 1.24.6, phase A, (a2)): <c>GetPushablePillarPushDirection</c> (<c>0x8003AC9C</c>), the side the hero pushes from. The axis is picked by the offset of the two positions of the end of the previous tick
/// (<see cref="EntityScriptProxy.ModdedPosX"/>, <c>+0x1D8</c>) against the widths ((size &lt;&lt; 16) - 1, <see cref="EntityScriptProxy.Width"/>), the sense by the positions (<c>+0x114</c> and <c>+0x118</c>): 8 or 0x18 on the horizontal side,
/// otherwise <c>(hero.Y &lt; pillar.Y) &lt;&lt; 4</c>. A flush contact (the offset is the size, one more than the width) falls on the horizontal side.
/// </summary>
public sealed class PillarAiDirectionTests
{
    private const int PillarWidth = (20 << 16) - 1;
    private const int HeroWidth = (16 << 16) - 1;
    private const int Origin = 1000 << 16;

    private static EntityScriptProxy Pillar(int posX = Origin, int posY = Origin) => new() { ModdedPosX = Origin, Width = PillarWidth, PosX = posX, PosY = posY };

    private static EntityScriptProxy Hero(int moddedX, int posX, int posY) => new() { ModdedPosX = moddedX, Width = HeroWidth, PosX = posX, PosY = posY };

    [Fact]
    public void A2_TheFourSides_EastWestNorthSouth_AreTheValues_0x18_8_16_0()
    {
        var pillar = Pillar();

        // east of the pillar, flush (the offset is the size of the pillar): the horizontal side, the hero's X is greater.
        Assert.Equal(0x18, PillarAi.Direction(pillar, Hero(Origin + (20 << 16), Origin + (20 << 16), Origin)));

        // west, flush (the offset is minus the size of the hero): the horizontal side, the hero's X is smaller.
        Assert.Equal(8, PillarAi.Direction(pillar, Hero(Origin - (16 << 16), Origin - (16 << 16), Origin)));

        // the columns overlap: the vertical side; the hero above (a smaller Y) pushes from the north: 16; below: 0.
        Assert.Equal(16, PillarAi.Direction(pillar, Hero(Origin, Origin, Origin - (14 << 16))));
        Assert.Equal(0, PillarAi.Direction(pillar, Hero(Origin, Origin, Origin + (14 << 16))));
    }

    [Fact]
    public void A2_TheWidthBounds_AtTheWidthTheColumnsStillOverlap_OneMoreIsTheHorizontalSide_ForBothSigns()
    {
        var pillar = Pillar();

        // positive offset: the pillar's width. dx == width is still the vertical side (the hero below: 0), dx == width + 1 is the horizontal side (0x18).
        Assert.Equal(0, PillarAi.Direction(pillar, Hero(Origin + PillarWidth, Origin + 1, Origin + 1)));
        Assert.Equal(0x18, PillarAi.Direction(pillar, Hero(Origin + PillarWidth + 1, Origin + 1, Origin + 1)));

        // negative offset: the hero's width. -dx == width is still the vertical side (the hero above: 16), -dx == width + 1 is the horizontal side (8).
        Assert.Equal(16, PillarAi.Direction(pillar, Hero(Origin - HeroWidth, Origin - 1, Origin - 1)));
        Assert.Equal(8, PillarAi.Direction(pillar, Hero(Origin - HeroWidth - 1, Origin - 1, Origin - 1)));
    }

    [Fact]
    public void A2_TheAxisIsPickedOnTheModdedPositions_TheSenseOnThePositions_AndAnEqualPositionTakesTheSecondValue()
    {
        var pillar = Pillar();

        // the offset of ModdedPosX decides the side, whatever PosX says: the same PosX far east is vertical when the modded offset is 0 (0), horizontal when it is the size (0x18).
        Assert.Equal(0, PillarAi.Direction(pillar, Hero(Origin, Origin + (30 << 16), Origin + 1)));
        Assert.Equal(0x18, PillarAi.Direction(pillar, Hero(Origin + (20 << 16), Origin + (30 << 16), Origin + 1)));

        // the sense reads PosX: a modded offset to the west with a PosX to the east takes the east value, and an equal PosX is the east value too (strict "less than").
        Assert.Equal(0x18, PillarAi.Direction(pillar, Hero(Origin - (16 << 16), Origin + 1, Origin)));
        Assert.Equal(0x18, PillarAi.Direction(pillar, Hero(Origin - (16 << 16), Origin, Origin)));
        Assert.Equal(8, PillarAi.Direction(pillar, Hero(Origin - (16 << 16), Origin - 1, Origin)));

        // on the vertical side an equal Y is the south value (0), one less is the north value (16).
        Assert.Equal(0, PillarAi.Direction(pillar, Hero(Origin, Origin, Origin)));
        Assert.Equal(16, PillarAi.Direction(pillar, Hero(Origin, Origin, Origin - 1)));
    }

    [Fact]
    public void A2_TheWrapOfTheOffset_MinusTheMinimumStaysTheMinimum_SoTheColumnsOverlap_NextToTheOrdinaryHorizontalSide()
    {
        var pillar = Pillar();
        var wrapped = new EntityScriptProxy { ModdedPosX = int.MinValue + Origin, Width = HeroWidth, PosX = 0, PosY = Origin + 1 }; // dx == int.MinValue, -dx wraps to int.MinValue: width < int.MinValue is false.
        var ordinary = Hero(Origin - (16 << 16), 0, Origin + 1);

        Assert.Equal(0, PillarAi.Direction(pillar, wrapped));
        Assert.Equal(8, PillarAi.Direction(pillar, ordinary));
    }
}
