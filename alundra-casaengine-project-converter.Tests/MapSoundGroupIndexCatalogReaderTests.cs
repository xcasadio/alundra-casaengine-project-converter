using AlundraCasaEngineProjectConverter.Readers;
using Xunit;

namespace AlundraCasaEngineProjectConverter.Tests;

/// <summary>
/// B3 of docs/plan-e11b-opcodes-audio.md, D-B-7: drives <see cref="MapSoundGroupIndexCatalogReader.Read"/>
/// directly, off a small synthetic CSV (not the real linked <c>MapSoundGroupIndex.csv</c> - the
/// <c>WorldWriterTests</c> end-to-end test already covers that one, all 483 real rows).
/// </summary>
public class MapSoundGroupIndexCatalogReaderTests
{
    [Fact]
    public void Read_ParsesEveryRow_SkippingTheHeader()
    {
        var path = Path.Combine(Path.GetTempPath(), "MapSoundGroupIndexCatalogReaderTests_" + Guid.NewGuid() + ".csv");
        File.WriteAllLines(path, new[]
        {
            "map_id;vab_group_id",
            "0;0",
            "1;46",
            "389;56",
        });

        try
        {
            var result = MapSoundGroupIndexCatalogReader.Read(path);

            Assert.Empty(result.Warnings);
            Assert.Equal(3, result.GroupIdByMapId.Count);
            Assert.Equal(0, result.GroupIdByMapId[0]);
            Assert.Equal(46, result.GroupIdByMapId[1]);
            Assert.Equal(56, result.GroupIdByMapId[389]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_MalformedRow_WarnsAndSkipsIt_ButKeepsTheOthers()
    {
        var path = Path.Combine(Path.GetTempPath(), "MapSoundGroupIndexCatalogReaderTests_" + Guid.NewGuid() + ".csv");
        File.WriteAllLines(path, new[]
        {
            "map_id;vab_group_id",
            "0;0",
            "not-a-number;7",
            "2;12",
        });

        try
        {
            var result = MapSoundGroupIndexCatalogReader.Read(path);

            Assert.Single(result.Warnings);
            Assert.Equal(2, result.GroupIdByMapId.Count);
            Assert.False(result.GroupIdByMapId.ContainsKey(1));
            Assert.Equal(12, result.GroupIdByMapId[2]);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
