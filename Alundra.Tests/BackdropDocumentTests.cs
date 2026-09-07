using System.Text.Json;
using Alundra.Scripts;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// Plan E9.b (docs/plan-e9b-backdrops-moteur.md, §3 "S2") - <see cref="BackdropDocument"/>'s
/// overlay-tint deserialization, moved here from the now-retired <c>BackdropRendererTests</c>
/// (that renderer's own drawing of the tint is gone; the JSON contract it read is unchanged and
/// still consumed by <see cref="AlundraBackdropStage.BuildDefinitions"/>).
/// </summary>
public class BackdropDocumentTests
{
    [Fact]
    public void BackdropDocument_DeserializesOverlayTintFields()
    {
        const string json = """
            {
              "MapIndex": 18,
              "Enabled": true,
              "OverlayEnabled": true,
              "OverlayColorR": 84,
              "OverlayColorG": 75,
              "OverlayColorB": 52,
              "Layers": []
            }
            """;

        var document = JsonSerializer.Deserialize<BackdropDocument>(json);

        Assert.NotNull(document);
        Assert.True(document!.OverlayEnabled);
        Assert.Equal(84, document.OverlayColorR);
        Assert.Equal(75, document.OverlayColorG);
        Assert.Equal(52, document.OverlayColorB);
    }

    [Fact]
    public void BackdropDocument_WithAbsentOverlayFields_DefaultsToTintDisabled()
    {
        // An old companion written before this feature existed - no Overlay* properties at all.
        const string json = """{ "MapIndex": 4, "Enabled": true, "Layers": [] }""";

        var document = JsonSerializer.Deserialize<BackdropDocument>(json);

        Assert.NotNull(document);
        Assert.False(document!.OverlayEnabled);
        Assert.Equal(0, document.OverlayColorR);
        Assert.Equal(0, document.OverlayColorG);
        Assert.Equal(0, document.OverlayColorB);
    }

    /// <summary>
    /// D-E9d: before this slice, a companion's "Cellular" object and the document-level
    /// "CellularSheetTextureAssetIds"/"WaveLut" fields were silently dropped (no matching member) -
    /// this pins that they now populate the model, field for field.
    /// </summary>
    [Fact]
    public void BackdropDocument_DeserializesCellularData()
    {
        const string json = """
            {
              "MapIndex": 420,
              "Enabled": true,
              "AnimNum": 1,
              "WaveLut": [0, 1, 2],
              "CellularSheetTextureAssetIds": ["ebe7af1a-ce33-5038-8f6a-179dbbd169d7", null, null, null, null, null, null, null],
              "Layers": [
                {
                  "LayerId": 0,
                  "Mode": "Cellular",
                  "DepthOrder": 1,
                  "Ground": true,
                  "BlendMode": 2,
                  "AnimTimer": 1,
                  "Cellular": {
                    "CountBase": 0,
                    "AWaveY": 4,
                    "AWavePhase": 4,
                    "AWaveAmp": 4,
                    "BWaveY": 5,
                    "BWavePhase": 5,
                    "BWaveWeight": 5,
                    "Divisions": 120,
                    "Cells": [
                      {
                        "PalDex": 0, "U0": 0, "V0": 0, "U1": 159, "V1": 3, "Type": 4,
                        "X0": 0, "Y0": 0, "CamXNum": 0, "CamXDen": 1, "CamYNum": 0, "CamYDen": 0,
                        "DX": 0, "PeriodX": 0, "DY": 0, "PeriodY": 0
                      }
                    ]
                  }
                }
              ]
            }
            """;

        var document = JsonSerializer.Deserialize<BackdropDocument>(json);

        Assert.NotNull(document);
        Assert.Equal(new[] { 0, 1, 2 }, document!.WaveLut);
        Assert.Equal(
            new[] { "ebe7af1a-ce33-5038-8f6a-179dbbd169d7", null, null, null, null, null, null, null },
            document.CellularSheetTextureAssetIds);

        var layer = Assert.Single(document.Layers);
        Assert.Equal("Cellular", layer.Mode);
        Assert.NotNull(layer.Cellular);
        var cellular = layer.Cellular!;
        Assert.Equal(0, cellular.CountBase);
        Assert.Equal(4, cellular.AWaveY);
        Assert.Equal(4, cellular.AWavePhase);
        Assert.Equal(4, cellular.AWaveAmp);
        Assert.Equal(5, cellular.BWaveY);
        Assert.Equal(5, cellular.BWavePhase);
        Assert.Equal(5, cellular.BWaveWeight);
        Assert.Equal(120, cellular.Divisions);

        var cell = Assert.Single(cellular.Cells);
        Assert.Equal(0, cell.PalDex);
        Assert.Equal(0, cell.U0);
        Assert.Equal(0, cell.V0);
        Assert.Equal(159, cell.U1);
        Assert.Equal(3, cell.V1);
        Assert.Equal(4, cell.Type);
        Assert.Equal(0, cell.X0);
        Assert.Equal(0, cell.Y0);
        Assert.Equal(0, cell.CamXNum);
        Assert.Equal(1, cell.CamXDen);
        Assert.Equal(0, cell.CamYNum);
        Assert.Equal(0, cell.CamYDen);
        Assert.Equal(0, cell.DX);
        Assert.Equal(0, cell.PeriodX);
        Assert.Equal(0, cell.DY);
        Assert.Equal(0, cell.PeriodY);
    }

    /// <summary>A companion written before D-E9d has no "Cellular"/"WaveLut"/"CellularSheetTextureAssetIds"
    /// properties at all - they must load as null, never fail.</summary>
    [Fact]
    public void BackdropDocument_WithAbsentCellularFields_LoadsAsNull()
    {
        const string json = """{ "MapIndex": 4, "Enabled": true, "Layers": [ { "LayerId": 0, "Mode": "Disabled" } ] }""";

        var document = JsonSerializer.Deserialize<BackdropDocument>(json);

        Assert.NotNull(document);
        Assert.Null(document!.WaveLut);
        Assert.Null(document.CellularSheetTextureAssetIds);
        Assert.Null(document.Layers[0].Cellular);
    }
}
