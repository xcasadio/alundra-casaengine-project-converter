#nullable enable
using System;
using System.Collections.Generic;

namespace Alundra.Scripts;

/// <summary>Stub of T3 (E20.e0a1): the valid indices of the map page, per map, read from <c>Data/map-sprite-indices.json</c> by T4.</summary>
public sealed class AlundraMapSpriteIndices
{
    public AlundraMapSpriteIndices(string projectPath)
    {
    }

    internal AlundraMapSpriteIndices(IReadOnlyDictionary<int, IReadOnlyList<int>> indicesByMap)
    {
    }

    public bool IsLoaded => false;

    public bool IsValid(int mapIndex, int index) => false;

    public static AlundraMapSpriteIndices GetOrCreate(string projectPath) => throw new NotImplementedException();

    internal static void ResetForTests()
    {
    }
}
