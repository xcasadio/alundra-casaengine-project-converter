#nullable enable
using Xunit;
using Alundra.Ai.Objects;
using Alundra.Entities;

namespace Alundra.Tests.Ai.Objects;

/// <summary>
/// E20.g5a T1 (docs/plan-e20-heros.md section 1.24.6, phase A, (a3), D-G5-2): the counter <c>+0x274</c> is a signed 32-bit word held little-endian on <see cref="EntityScriptProxy.Bytes"/> 0 to 3, by helpers of the pillar
/// (<see cref="EntityScriptProxy"/> does not change).
/// </summary>
public sealed class PillarAiWordTests
{
    [Fact]
    public void A3_TheWordRoundTrips_ForZero_31_MinusOne_AndTheLimits()
    {
        foreach (var value in new[] { 0, 31, -1, int.MinValue, int.MaxValue, 0x01020304 })
        {
            var entity = new EntityScriptProxy();
            PillarAi.WriteCounter(entity, value);
            Assert.Equal(value, PillarAi.ReadCounter(entity));
        }
    }

    [Fact]
    public void A3_TheBytesAreLittleEndian_AndOnlyTheFirstFourAreTouched()
    {
        var entity = new EntityScriptProxy { Bytes = new byte[] { 0xAA, 0xAA, 0xAA, 0xAA, 0x55, 0x55 } };

        PillarAi.WriteCounter(entity, 31);
        Assert.Equal(new byte[] { 31, 0, 0, 0, 0x55, 0x55 }, entity.Bytes);

        PillarAi.WriteCounter(entity, -1);
        Assert.Equal(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x55, 0x55 }, entity.Bytes);

        PillarAi.WriteCounter(entity, 0x01020304);
        Assert.Equal(new byte[] { 4, 3, 2, 1, 0x55, 0x55 }, entity.Bytes);

        // reading: the high byte is the sign.
        var raw = new EntityScriptProxy { Bytes = new byte[] { 0x1F, 0, 0, 0xFF } };
        Assert.Equal(unchecked((int)0xFF00001F), PillarAi.ReadCounter(raw));
        Assert.Equal(31, PillarAi.ReadCounter(new EntityScriptProxy { Bytes = new byte[] { 0x1F, 0, 0, 0 } }));
    }
}
