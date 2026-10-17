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
public sealed class EffectBank
{
    public static EffectBank Empty { get; } = new(null, null);

    private readonly EffectDocument? _map;
    private readonly EffectDocument? _global;

    public EffectBank(EffectDocument? map, EffectDocument? global)
    {
        _map = map;
        _global = global;
    }

    public EffectDocument? Map => _map;

    public EffectDocument? Global => _global;

    public IReadOnlyList<EffectRecord> Records => (IReadOnlyList<EffectRecord>?)_map?.Records ?? Array.Empty<EffectRecord>();

    /// <summary>The table <paramref name="effect"/> of the map table (<paramref name="mapTable"/>) or of the global one; false when there is none.</summary>
    public bool TryGetTable(bool mapTable, int effect, [NotNullWhen(true)] out EffectTable? table)
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
