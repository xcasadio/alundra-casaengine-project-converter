#nullable enable
using System;
using System.Collections.Generic;
using System.Text.Json;
using CasaEngine.Core.Logging;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Framework.Scene.Entities.Components;
using Alundra.Maps.Loop;

namespace Alundra.Cells;

/// <summary>
/// One map's "AlundraWallPlacements" custom property (see
/// <c>AlundraCasaEngineProjectConverter.Writers.CellMetadataWriter.WriteWallPlacements</c> and
/// <c>docs/formats/cells-companion.md</c>): every baked wall tile's exact flat-layer location
/// (<see cref="Plane"/>/<see cref="X"/>/<see cref="Y"/>, matching <c>TileMapComponent.RemoveTile</c>'s
/// own (layer, x, y) triple) plus its PSX depth slot (0..5, see <see cref="DepthSlot"/>). Columnar for
/// the same reason the converter's own <c>WallPlacementDocument</c> is: this project does not reference
/// the converter, so the shape is duplicated here rather than shared - field names/order are read back
/// with the same <c>JsonNamingPolicy.SnakeCaseLower</c> the converter serialized them with, so no
/// per-field attribute is needed on either side.
/// </summary>
public sealed class WallPlacementRecords
{
    public int MapIndex { get; init; }
    public int Count { get; init; }
    public int[] CellX { get; init; } = Array.Empty<int>();
    public int[] CellY { get; init; } = Array.Empty<int>();
    public int[] StackIndex { get; init; } = Array.Empty<int>();
    public int[] Plane { get; init; } = Array.Empty<int>();
    public int[] X { get; init; } = Array.Empty<int>();
    public int[] Y { get; init; } = Array.Empty<int>();
    public int[] Gid { get; init; } = Array.Empty<int>();
    public int[] DepthSlot { get; init; } = Array.Empty<int>();
}
