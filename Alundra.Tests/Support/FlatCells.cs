#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CasaEngine.Engine.Geometry;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Application.Components.Physics;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Physics;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;
using Alundra.Session;
using Alundra.Entities;
using Alundra.Entities.Spawning;
using Alundra.Events;
using Alundra.Hero;
using Alundra.Physics;
using Alundra.Audio;

namespace Alundra.Tests.Support;

/// <summary>A synthetic flat field of cells (walkability, ground property and height 0) for the hero montages.</summary>
internal static class FlatCells
{
    /// <summary>
    /// A field of <paramref name="width"/> x <paramref name="height"/> cells, every one of walkability 0 and height 0 - or, with <paramref name="cell"/>, the
    /// walkability (<c>0x08</c> water, <c>0x10</c> the x160 bit, <c>0x20</c> ice) and the height (in cells of 16 px) that function gives for the cell (x, y).
    /// </summary>
    public static CellsCollisionField Create(int width = 40, int height = 40, Func<int, int, (int Walkability, int Height)>? cell = null, Func<int, int, int>? groundProperty = null, Func<int, int, int>? slope = null)
    {
        var count = width * height;
        string Zeros()
        {
            var sb = new StringBuilder("[");
            for (var i = 0; i < count; i++)
            {
                sb.Append(i == 0 ? "0" : ",0");
            }

            return sb.Append(']').ToString();
        }

        string Column(Func<(int Walkability, int Height), int> pick)
        {
            var sb = new StringBuilder("[");
            for (var i = 0; i < count; i++)
            {
                sb.Append(i == 0 ? "" : ",").Append(pick(cell!(i % width, i / width)));
            }

            return sb.Append(']').ToString();
        }

        string GroundPropertyColumn()
        {
            var sb = new StringBuilder("[");
            for (var i = 0; i < count; i++)
            {
                sb.Append(i == 0 ? "" : ",").Append(groundProperty!(i % width, i / width));
            }

            return sb.Append(']').ToString();
        }

        string SlopeColumn()
        {
            var sb = new StringBuilder("[");
            for (var i = 0; i < count; i++)
            {
                sb.Append(i == 0 ? "" : ",").Append(slope!(i % width, i / width));
            }

            return sb.Append(']').ToString();
        }

        var walkabilityJson = cell == null ? Zeros() : Column(c => c.Walkability);
        var heightJson = cell == null ? Zeros() : Column(c => c.Height);

        var tileMapData = new TileMapData { MapSize = new CasaEngine.Core.Math.Size(width, height) };
        tileMapData.CustomProperties["AlundraCells"] =
            "{\"map_index\":1,\"cell_count\":" + count + ",\"walkability\":" + walkabilityJson + ",\"ground_property\":" + (groundProperty == null ? Zeros() : GroundPropertyColumn())
            + ",\"slope\":" + (slope == null ? Zeros() : SlopeColumn()) + ",\"height\":" + heightJson + ",\"tile_id\":" + Zeros() + ",\"wall_tiles_offset\":" + Zeros()
            + ",\"wall_tiles\":{}}";
        Assert.True(CellsCollisionField.TryCreate(tileMapData, "flat_cells", out var field));
        return field!;
    }
}
