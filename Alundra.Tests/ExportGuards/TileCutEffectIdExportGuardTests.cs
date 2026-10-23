#nullable enable
using System.IO;
using CasaEngine.Framework.Assets.TileMap;
using Newtonsoft.Json.Linq;
using Xunit;
using Alundra.Cells;
using Alundra.Tests.Support;

namespace Alundra.Tests.ExportGuards;

/// <summary>
/// E20.e T2 guard on the REAL export (E20.eR, ADR-0079): the byte 9 of the header of a map is exported under the name <c>TileCutEffectId</c> (the effect of a cut of the grass), no more under the old name <c>BalanceLevel</c>.
/// Without this guard, <see cref="TileEffect.ResolveCutEffectIndex"/> would hide a wrong name behind its fall back on 2 (the byte is 2 on the 483 maps). The three maps the cut is measured on: 10, 14 and 16. A missing file FAILS, never skips.
/// </summary>
public sealed class TileCutEffectIdExportGuardTests
{
    [Theory]
    [InlineData("Overworld", "Overworld 2,1-10")]
    [InlineData("Overworld", "Overworld 3,1-14")]
    [InlineData("Overworld", "Overworld 3,3-16")]
    public void TheTileMapOfTheMap_CarriesTileCutEffectId_AndNoBalanceLevel(string folder, string name)
    {
        var path = Path.Combine(SaveGameDirectorTestSupport.FindProjectRoot(), "Maps", folder, name, "tilemap", name + ".tileMap");
        Assert.True(File.Exists(path), $"TileCutEffectIdExportGuardTests: the exported tile map is missing: '{path}'");
        var tileMapData = new TileMapData();
        tileMapData.Load(JObject.Parse(File.ReadAllText(path)));

        Assert.True(tileMapData.CustomProperties.TryGetValue(TileEffect.CutEffectPropertyName, out var value), $"'{name}' has no property '{TileEffect.CutEffectPropertyName}'");
        Assert.Equal("2", value);
        Assert.False(tileMapData.CustomProperties.ContainsKey("BalanceLevel"), $"'{name}' still carries the old name 'BalanceLevel'");
    }
}
