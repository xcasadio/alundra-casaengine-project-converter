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
using Alundra.Maps;

namespace Alundra.Entities;

/// <summary>
/// Seam over <see cref="SpriteRecordCatalog"/> so spawn-time code (<see cref="WorldProxy"/>) and
/// its tests can swap the real file-backed catalog for an in-memory fake, the same way
/// <c>WorldProxy.CreateEntityFromRecord</c>'s <c>prefabLoader</c> parameter is swapped in tests.
/// </summary>
public interface ISpriteRecordCatalog
{
    /// <summary>
    /// Looks up the raw sprite-record header of the bank prefab <paramref name="prefabAssetId"/> links
    /// to. Returns false when the catalog has no entry for it - including when the whole catalog
    /// failed to load (see <see cref="SpriteRecordCatalog"/>'s class doc): callers treat that the same
    /// way as "this particular prefab has no header", a degraded-but-safe spawn with no header-derived
    /// initialization.
    /// </summary>
    bool TryGet(Guid prefabAssetId, out SpriteRecordHeader header);

    /// <summary>
    /// E20.e0a1 (ADR-0063): the prefab of the sprite at <paramref name="index"/> of the table of the global page (<paramref name="isMapPage"/> false) or of the map page - the
    /// pair <c>SpawnWarpEntity</c> (<c>0x80039F58</c>) receives. The key is the <c>BankKey</c> of the export (<c>alundra_N</c> on the global page, <c>N</c> on the map page; one prefab per number
    /// whichever map holds it). False when the catalog has no such key, including an export that predates <c>BankKey</c> or a catalog that failed to load. A default interface member, so
    /// every implementer written before keeps compiling and answers "no prefab".
    /// </summary>
    bool TryGetPrefab(bool isMapPage, int index, out Guid prefabAssetId)
    {
        prefabAssetId = Guid.Empty;
        return false;
    }
}
