#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using CasaEngine.Core.Logging;
using CasaEngine.Engine.Environment;
using Alundra.Session;

namespace Alundra.Entities;

/// <summary>
/// In-memory <see cref="ISpriteRecordCatalog"/> for tests: no file I/O, entries added directly.
/// </summary>
public sealed class FakeSpriteRecordCatalog : ISpriteRecordCatalog
{
    private readonly Dictionary<Guid, SpriteRecordHeader> _headersByPrefabId = new();

    private readonly Dictionary<string, Guid> _prefabIdsByBankKey = new(StringComparer.Ordinal);

    public FakeSpriteRecordCatalog Add(Guid prefabAssetId, SpriteRecordHeader header)
    {
        _headersByPrefabId[prefabAssetId] = header;
        return this;
    }

    /// <summary>E20.e0a1: gives the sprite at <paramref name="index"/> of a page the prefab <paramref name="prefabAssetId"/> (the <c>BankKey</c> of the real export).</summary>
    public FakeSpriteRecordCatalog AddBankKey(bool isMapPage, int index, Guid prefabAssetId)
    {
        _prefabIdsByBankKey[SpriteRecordCatalog.BankKeyOf(isMapPage, index)] = prefabAssetId;
        return this;
    }

    public bool TryGet(Guid prefabAssetId, out SpriteRecordHeader header)
        => _headersByPrefabId.TryGetValue(prefabAssetId, out header);

    public bool TryGetPrefab(bool isMapPage, int index, out Guid prefabAssetId)
        => _prefabIdsByBankKey.TryGetValue(SpriteRecordCatalog.BankKeyOf(isMapPage, index), out prefabAssetId);
}
