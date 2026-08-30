namespace AlundraCasaEngineProjectConverter.Readers;

/// <summary>
/// Reads <c>MapMusicIndex.csv</c> - the RAW port of the original's <c>g_defaultSoundOffsetList</c>
/// (<c>StaticVariables.cs:12620</c>), one row per <c>map_id</c> (0..482), <c>sound_index</c> holding
/// the exact value the original array carries for that map: <c>0</c>, <c>45</c>, <c>-1</c>, or a real
/// music index (docs/plan-e11c-musique.md, slice C1, facts 1.1/2.1, decision D-C-2).
///
/// Same "linked, not copied" precedent as <see cref="EntityNameCatalogReader"/>/<c>EntityNames.csv</c>:
/// the analyser owns this table (it is generated straight off the decompiled array, see
/// <c>alundra-datas-analyser/AlundraTools/AlundraTools/MapMusicIndex.csv</c>'s own header comment in
/// that project's <c>.csproj</c>), this converter only reads it and republishes it as
/// <c>Maps/music-index.json</c> (<see cref="Writers.WorldWriter"/>) for the runtime to consume.
///
/// Values are exported RAW on purpose (D-C-2): interpreting them (0 = do nothing, same-as-current =
/// do nothing, 45 = stop only, -1 = play index 1, otherwise play that index - fact 1.1) is the
/// CONSUMER's job (<c>Alundra.Scripts.AlundraMusicIndexTable</c>/<c>AlundraMusicPlayer</c>), not this
/// reader's - exporting already-interpreted values would lose information and freeze any future
/// re-reading of the semantics into the data itself.
/// </summary>
public static class MusicIndexCatalogReader
{
    public sealed record ReadResult(IReadOnlyDictionary<int, int> RawIndexByMapId, IReadOnlyList<string> Warnings);

    public static ReadResult Read(string csvPath)
    {
        var warnings = new List<string>();
        var byMapId = new Dictionary<int, int>();

        var lines = File.ReadAllLines(csvPath);
        for (var lineIndex = 1; lineIndex < lines.Length; lineIndex++) // row 0 is the header
        {
            var line = lines[lineIndex];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var columns = line.Split(';');
            if (columns.Length < 2
                || !int.TryParse(columns[0], out var mapId)
                || !int.TryParse(columns[1], out var rawIndex))
            {
                warnings.Add($"MapMusicIndex.csv: malformed row {lineIndex + 1} ('{line}'), skipped.");
                continue;
            }

            byMapId[mapId] = rawIndex;
        }

        return new ReadResult(byMapId, warnings);
    }
}
