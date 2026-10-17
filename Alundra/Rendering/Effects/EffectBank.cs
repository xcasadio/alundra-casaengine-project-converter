#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json;
using CasaEngine.Core.Logging;

namespace Alundra.Rendering.Effects;

/// <summary>
/// The effect data of a world (E19.g G3a, docs/plan-e19-opcodes.md section 1.2o.6): the records and the map table of the map companion, and the global tables
/// (a record's <c>0x80</c> flag selects the map table, otherwise the global one). Data only; the reservoir reads it.
/// </summary>
public sealed class AlundraEffectBank
{
    public static AlundraEffectBank Empty { get; } = new(null, null);

    private readonly AlundraEffectDocument? _map;
    private readonly AlundraEffectDocument? _global;

    public AlundraEffectBank(AlundraEffectDocument? map, AlundraEffectDocument? global)
    {
        _map = map;
        _global = global;
    }

    public AlundraEffectDocument? Map => _map;

    public AlundraEffectDocument? Global => _global;

    public IReadOnlyList<AlundraEffectRecord> Records => (IReadOnlyList<AlundraEffectRecord>?)_map?.Records ?? Array.Empty<AlundraEffectRecord>();

    /// <summary>The table <paramref name="effect"/> of the map table (<paramref name="mapTable"/>) or of the global one; false when there is none.</summary>
    public bool TryGetTable(bool mapTable, int effect, [NotNullWhen(true)] out AlundraEffectTable? table)
    {
        var tables = (mapTable ? _map : _global)?.Effects;
        if (tables != null && effect >= 0 && effect < tables.Count)
        {
            table = tables[effect];
            return true;
        }

        table = null;
        return false;
    }
}
