#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using CasaEngine.Core.Logging;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Assets.TileMap;
using Microsoft.Xna.Framework;
using Alundra.Maps;
using Alundra.Entities;

namespace Alundra.Physics;

/// <summary>One cell's wall tile stack (E7.a) - mirrors the converter's <c>WallTileStack</c>
/// (CellMetadataReader.cs) field-for-field: <see cref="Offset"/> is the original's raw
/// <c>WallTiles.Offset</c> pointer, <see cref="Tiles"/> the stacked tile ids. No separate "Count" field:
/// the original's <c>WallTiles.Count</c> is always <c>Tiles.Length</c> in this export (see
/// docs/formats/cells-companion.md), so there is nothing extra to carry.</summary>
public sealed class WallTileStack
{
    public int Offset { get; init; }
    public int[] Tiles { get; init; } = Array.Empty<int>();
}
