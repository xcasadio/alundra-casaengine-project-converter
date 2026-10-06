using AlundraCasaEngineProjectConverter.Readers;

namespace AlundraCasaEngineProjectConverter.Writers;

public static class EffectWriter
{
    public static void ConvertEffects(
        string inputDirectory,
        string outputDirectory,
        IReadOnlyList<int>? mapFilter,
        IReadOnlyDictionary<int, MapLocation> mapLocations,
        ConversionReport report)
        => throw new NotImplementedException();
}
