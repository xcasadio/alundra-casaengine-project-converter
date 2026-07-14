namespace AlundraCasaEngineProjectConverter.Writers;

/// <summary>
/// Finds which Alundra map ids exist in data-extracted, from the Tiled export
/// (data/tiled/map_N.tmj). Shared by every phase's writer so "which maps exist" has one
/// definition.
/// </summary>
internal static class MapDiscovery
{
    public static IReadOnlyList<int> DiscoverMapIndices(string inputDirectory)
    {
        var tiledDirectory = Path.Combine(inputDirectory, "data", "tiled");
        if (!Directory.Exists(tiledDirectory))
        {
            return Array.Empty<int>();
        }

        var indices = new List<int>();
        foreach (var filePath in Directory.EnumerateFiles(tiledDirectory, "map_*.tmj"))
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            if (fileName.StartsWith("map_", StringComparison.Ordinal)
                && int.TryParse(fileName.AsSpan(4), out var mapIndex))
            {
                indices.Add(mapIndex);
            }
        }

        return indices;
    }
}
