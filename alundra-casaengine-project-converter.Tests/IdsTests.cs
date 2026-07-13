using AlundraCasaEngineProjectConverter;
using Xunit;

namespace AlundraCasaEngineProjectConverter.Tests;

public class IdsTests
{
    [Fact]
    public void For_SameKey_ReturnsSameGuid()
    {
        var a = Ids.For("tileset/map_10");
        var b = Ids.For("tileset/map_10");

        Assert.Equal(a, b);
    }

    [Fact]
    public void For_DifferentKeys_ReturnDifferentGuids()
    {
        var a = Ids.For("tileset/map_10");
        var b = Ids.For("tileset/map_11");

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void For_ReturnsRfc4122Version5Guid()
    {
        var bytes = Ids.For("tileset/map_10").ToByteArray();

        Assert.Equal(0x50, bytes[7] & 0xF0);
        Assert.Equal(0x80, bytes[8] & 0xC0);
    }
}
