#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Alundra.Tests.Items;

/// <summary>One (map, tile) pair of V8 (<c>v8.json</c>): the real load loop of the binary for a map, the hero standing on <see cref="TileX"/>, <see cref="TileY"/> (the tile the DLL hero occupies DURING the load).</summary>
internal sealed record LoadPair(
    int Map, string Zone, string World, int TileX, int TileY, int RecordsCalled, IReadOnlyList<LoadedEntity> Entities, int Draws, uint FinalWord)
{
    internal bool Rolls => Draws > 0;
}
