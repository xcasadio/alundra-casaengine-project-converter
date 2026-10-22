#nullable enable
using Xunit;
using Alundra.Ai.Objects;
using Alundra.Entities;

namespace Alundra.Tests.Ai.Objects;

/// <summary>
/// E20.e0c T1 (docs/plan-e20-heros.md section 1.26, (l4), D-E0C-13, D-E20-55): the 32-bit words of the pickup object that the proxy keeps in pieces - <c>+0x274</c> on the four bytes of <see cref="EntityScriptProxy.Bytes"/>,
/// <c>+0x280</c> and <c>+0x284</c> on four halfwords of <see cref="EntityScriptProxy.AIValues"/> - read and written little-endian, as the binary does. The accessors are the ones of the pickup object alone: the proxy has none
/// (<c>Alundra/Entities/EntityScriptProxy.cs</c> is not changed by this tranche).
/// </summary>
public sealed class PickupAiTests
{
    [Fact]
    public void Word274_IsReadAndWrittenLittleEndian_OnTheFourBytes()
    {
        var entity = new EntityScriptProxy();
        PickupAi.WriteWord274(entity, 0x04030201);

        Assert.Equal(new byte[] { 1, 2, 3, 4 }, entity.Bytes);
        Assert.Equal(0x04030201U, PickupAi.ReadWord274(entity));

        entity.Bytes[0] = 0xFF;
        entity.Bytes[3] = 0x80;
        Assert.Equal(0x800302FFU, PickupAi.ReadWord274(entity));
    }

    [Fact]
    public void Word280_IsLowThenHighHalfword_OfTheFirstTwoValues_AndWord284OfTheNextTwo()
    {
        var entity = new EntityScriptProxy();
        PickupAi.WriteWord280(entity, 0x1234ABCD);
        PickupAi.WriteWord284(entity, 0x89EF4567);

        Assert.Equal(unchecked((short)0xABCD), entity.AIValues[0]);
        Assert.Equal(unchecked((short)0x1234), entity.AIValues[1]);
        Assert.Equal(unchecked((short)0x4567), entity.AIValues[2]);
        Assert.Equal(unchecked((short)0x89EF), entity.AIValues[3]);
        Assert.Equal(0x1234ABCDU, PickupAi.ReadWord280(entity));
        Assert.Equal(0x89EF4567U, PickupAi.ReadWord284(entity));
    }

    [Theory]
    [InlineData(0xFFFFFFFFU)]
    [InlineData(0x00000000U)]
    [InlineData(0x1234ABCDU)]
    [InlineData(0xABCD1234U)]
    [InlineData(0x80000000U)]
    [InlineData(0x0000FFFFU)]
    public void EachWord_RoundTrips_AMinusOneAndAValueWhoseTwoHalvesDiffer(uint value)
    {
        var entity = new EntityScriptProxy();

        PickupAi.WriteWord274(entity, value);
        PickupAi.WriteWord280(entity, value);
        PickupAi.WriteWord284(entity, value);

        Assert.Equal(value, PickupAi.ReadWord274(entity));
        Assert.Equal(value, PickupAi.ReadWord280(entity));
        Assert.Equal(value, PickupAi.ReadWord284(entity));
    }

    [Fact]
    public void TheThreeWords_DoNotAliasEachOther_NorTheOtherValuesOfTheProxy()
    {
        var entity = new EntityScriptProxy();
        entity.AIValues[4] = 77;
        entity.AIValues[9] = -3;
        PickupAi.WriteWord274(entity, 0xFFFFFFFF);

        Assert.Equal(0U, PickupAi.ReadWord280(entity));
        Assert.Equal(0U, PickupAi.ReadWord284(entity));

        PickupAi.WriteWord280(entity, 0xFFFFFFFF);
        Assert.Equal(0U, PickupAi.ReadWord284(entity));
        PickupAi.WriteWord284(entity, 0x12345678);
        Assert.Equal(0xFFFFFFFFU, PickupAi.ReadWord280(entity));
        Assert.Equal(0xFFFFFFFFU, PickupAi.ReadWord274(entity));
        Assert.Equal((short)77, entity.AIValues[4]);
        Assert.Equal((short)-3, entity.AIValues[9]);
    }
}
