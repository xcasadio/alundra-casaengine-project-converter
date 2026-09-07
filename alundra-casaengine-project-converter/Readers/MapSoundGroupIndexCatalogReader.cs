namespace AlundraCasaEngineProjectConverter.Readers;

/// <summary>
/// Reads <c>MapSoundGroupIndex.csv</c> - the RAW port of the original's <c>VabIndexByMapId</c>
/// (<c>SoundBin.cs:2335</c>), one row per <c>map_id</c> (0..482), <c>vab_group_id</c> holding the exact
/// value the original array carries for that map (0..76, 74 distinct values). The original's own
/// consumer is <c>OpenMapVab(VabIndexByMapId[mapid])</c> (<c>SoundBin.cs:203</c>) -
/// docs/plan-e11b-opcodes-audio.md, slice B3, decision D-B-7.
///
/// Same "linked, not copied" precedent as <see cref="MusicIndexCatalogReader"/>/<c>MapMusicIndex.csv</c>:
/// the analyser owns this table (it is generated straight off the decompiled array, see
/// <c>alundra-datas-analyser/AlundraTools/AlundraTools/MapSoundGroupIndex.csv</c>'s own header comment
/// in that project's <c>.csproj</c>), this converter only reads it and republishes it as
/// <c>Maps/sound-group-index.json</c> (<see cref="Writers.WorldWriter"/>) for the runtime to consume.
///
/// Values are exported RAW on purpose, same reasoning as D-C-2 for the music index: this is the group
/// id <c>AlundraSoundBank.TryResolve</c> compares a sound-effect record's own <c>VabId</c> against to
/// decide whether the <c>RefSfxId</c> redirection chain fires - interpreting it here (rather than
/// letting the DLL consumer pass it straight through) would freeze that comparison's semantics into
/// the data.
/// </summary>
public static class MapSoundGroupIndexCatalogReader
{
    public sealed record ReadResult(IReadOnlyDictionary<int, int> GroupIdByMapId, IReadOnlyList<string> Warnings);

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
                || !int.TryParse(columns[1], out var groupId))
            {
                warnings.Add($"MapSoundGroupIndex.csv: malformed row {lineIndex + 1} ('{line}'), skipped.");
                continue;
            }

            byMapId[mapId] = groupId;
        }

        return new ReadResult(byMapId, warnings);
    }
}
