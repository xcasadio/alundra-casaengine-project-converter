#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Alundra.Tests.Entities;

/// <summary>One vector of the oracle: the call (page, index, X and Y in pixels, Z as the binary's raw 16.16 argument, direction word, parent kind, world) and what the real binary left.</summary>
internal sealed record SpawnVector(
    string Id, int Page, int Index, int? Map, string Parent, int X, int Y, int Z, int Direction, string World, int Ret, bool Rolls, IReadOnlyList<int> Sounds, SpawnProjection? P, uint RngOut)
{
    internal bool IsMapPage => Page == 1;
}
