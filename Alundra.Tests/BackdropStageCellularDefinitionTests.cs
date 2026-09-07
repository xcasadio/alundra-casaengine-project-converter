#nullable enable
using System;
using System.Collections.Generic;
using Alundra.Scripts;
using CasaEngine.Framework.Rendering.CellularLayers;
using CasaEngine.Framework.Rendering.Depth;
using Microsoft.Xna.Framework;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// D-E9d (docs/plan-e9d-mode-cellulaire.md) - covers <see cref="AlundraBackdropStage.BuildCellularDefinitions"/>,
/// the PURE translation of a loaded <see cref="BackdropDocument"/> into the engine mechanism's own
/// <c>CellularLayerDefinition[]</c> (<c>CasaEngine.Framework.Rendering.CellularLayers</c>). Nothing
/// here calls <see cref="AlundraBackdropStage.AttachCellularService"/>/<see cref="AlundraBackdropStage.PushFrame"/>
/// or touches production - see <see cref="BackdropPushProductionTests"/> for the production push pin.
///
/// The two real-companion tests read <c>alundra-project/</c> (the converter's own export) and FAIL,
/// rather than skip, when it is absent - same local convention as <see cref="BackdropStageDefinitionTests"/>.
/// </summary>
public class BackdropStageCellularDefinitionTests
{
    // -----------------------------------------------------------------------------------------
    // Real companions.
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// Map 420 (Nava, "Coast house") - a single Cellular layer, 120 <c>WaveX</c> cells, document-level
    /// AnimNum == layer AnimTimer == 1 (does NOT by itself distinguish the two - see the dedicated trap
    /// test below for that). Quoted straight from the companion:
    /// <c>Layers[0] = {"LayerId":0,"Mode":"Cellular","DepthOrder":1,"Ground":true,"BlendMode":2,"AnimTimer":1}</c>,
    /// <c>Cellular = {"CountBase":0,"AWaveY":4,"AWavePhase":4,"AWaveAmp":4,"BWaveY":5,"BWavePhase":5,"BWaveWeight":5,"Divisions":120}</c>,
    /// first cell <c>{"PalDex":0,"U0":0,"V0":0,"U1":159,"V1":3,"Type":4,"X0":0,"Y0":0,"CamXNum":0,"CamXDen":1,"CamYNum":0,"CamYDen":0,"DX":0,"PeriodX":0,"DY":0,"PeriodY":0}</c>,
    /// document-level <c>CellularSheetTextureAssetIds = ["ebe7af1a-ce33-5038-8f6a-179dbbd169d7", null, null, null, null, null, null, null]</c>.
    /// </summary>
    [Fact]
    public void BuildCellularDefinitions_RealMap420_TranslatesTheWaveXLayer_FieldForField()
    {
        var document = LoadRealCompanion("Coast house (Nava)-420");

        var layers = AlundraBackdropStage.BuildCellularDefinitions(document);

        var layer = Assert.Single(layers);
        Assert.Equal(0, layer.LayerId);
        Assert.Equal(1, layer.AnimTimer); // LAYER's own AnimTimer.
        Assert.Equal(1, layer.AnimNum); // DOCUMENT's own AnimNum.
        Assert.True(layer.Ground);
        // Ground=true, BlendMode 2 -> Additive, white (same policy the Tiles path resolves).
        Assert.Equal(SpriteBlendMode.Additive, layer.Blend);
        Assert.Equal(Color.White, layer.Tint);
        Assert.Equal(0, layer.SortingLayer);
        Assert.Equal(1, layer.OrderInLayer); // DepthOrder.

        Assert.Equal(4, layer.AWaveY);
        Assert.Equal(4, layer.AWavePhase);
        Assert.Equal(4, layer.AWaveAmp);
        Assert.Equal(5, layer.BWaveY);
        Assert.Equal(5, layer.BWavePhase);
        Assert.Equal(5, layer.BWaveWeight);

        Assert.Equal(120, layer.Cells.Length);
        var firstCell = layer.Cells[0];
        Assert.Equal(0, firstCell.PalDex);
        Assert.Equal(0, firstCell.U0);
        Assert.Equal(0, firstCell.V0);
        Assert.Equal(159, firstCell.U1);
        Assert.Equal(3, firstCell.V1);
        Assert.Equal(CellularCellType.WaveX, firstCell.Type);
        Assert.Equal(0, firstCell.X0);
        Assert.Equal(0, firstCell.Y0);
        Assert.Equal(0, firstCell.CamXNum);
        Assert.Equal(1, firstCell.CamXDen);
        Assert.Equal(0, firstCell.CamYNum);
        Assert.Equal(0, firstCell.CamYDen);
        Assert.Equal(0, firstCell.DX);
        Assert.Equal(0, firstCell.PeriodX);
        Assert.Equal(0, firstCell.DY);
        Assert.Equal(0, firstCell.PeriodY);

        Assert.Equal(
            new[] { Guid.Parse("ebe7af1a-ce33-5038-8f6a-179dbbd169d7"), Guid.Empty, Guid.Empty, Guid.Empty, Guid.Empty, Guid.Empty, Guid.Empty, Guid.Empty },
            layer.SheetTextureAssetIds);
    }

    /// <summary>
    /// THE MAPPING TRAP, pinned with two DISTINCT real values: map 391 (The Klark, night/break) has
    /// document-level <c>AnimNum = 4</c> while its Cellular layer's own <c>AnimTimer = 1</c> - a swap of
    /// the two sources (reading AnimNum from the layer, or AnimTimer from the document) would flip these
    /// and go undetected by any test using equal values. Also carries a <c>FallRespawn</c> (Type 2)
    /// layer, quoted from the companion's first cell:
    /// <c>{"PalDex":0,"U0":2,"V0":0,"U1":2,"V1":63,"Type":2,"X0":145,"Y0":114,"CamXNum":2,"CamXDen":3,"CamYNum":2,"CamYDen":3,"DX":0,"PeriodX":0,"DY":8,"PeriodY":0}</c>.
    /// </summary>
    [Fact]
    public void BuildCellularDefinitions_RealMap391_AnimNumFromDocument_AnimTimerFromLayer_NotSwapped()
    {
        var document = LoadRealCompanion("Ship Klark (night, break, Event)-391");
        Assert.Equal(4, document.AnimNum); // sanity check on the source value itself.

        var layers = AlundraBackdropStage.BuildCellularDefinitions(document);

        var layer = Assert.Single(layers);
        Assert.Equal(1, layer.AnimTimer); // from the LAYER (layerInfos.AnimTimer), NOT document.AnimNum (4).
        Assert.Equal(4, layer.AnimNum); // from the DOCUMENT (Infos.AnimNum), NOT layer.AnimTimer (1).

        Assert.Equal(55, layer.Cells.Length);
        var firstCell = layer.Cells[0];
        Assert.Equal(CellularCellType.FallRespawn, firstCell.Type);
        Assert.Equal(2, firstCell.U0);
        Assert.Equal(0, firstCell.V0);
        Assert.Equal(2, firstCell.U1);
        Assert.Equal(63, firstCell.V1);
        Assert.Equal(145, firstCell.X0);
        Assert.Equal(114, firstCell.Y0);
        Assert.Equal(2, firstCell.CamXNum);
        Assert.Equal(3, firstCell.CamXDen);
        Assert.Equal(2, firstCell.CamYNum);
        Assert.Equal(3, firstCell.CamYDen);
        Assert.Equal(0, firstCell.DX);
        Assert.Equal(8, firstCell.DY);

        // Ground=true, BlendMode 1 -> AlphaBlend, (255,255,255,128) - same policy as the Tiles path.
        Assert.Equal(SpriteBlendMode.AlphaBlend, layer.Blend);
        Assert.Equal(new Color(255, 255, 255, 128), layer.Tint);
    }

    // -----------------------------------------------------------------------------------------
    // Synthetic documents - the mapping trap in isolation, filtering, sheet ids, blend policy.
    // -----------------------------------------------------------------------------------------

    /// <summary>The mapping trap, isolated with two synthetic values that would fail loudly if swapped
    /// (a future accidental swap of the sources cannot silently pass this test).</summary>
    [Fact]
    public void BuildCellularDefinitions_AnimNumAndAnimTimer_ReadFromDistinctSources_TwoDifferentValues()
    {
        var document = new BackdropDocument
        {
            AnimNum = 7, // document-level.
            Layers = new List<BackdropLayerData>
            {
                new()
                {
                    LayerId = 0,
                    Mode = "Cellular",
                    AnimTimer = 3, // layer-level - deliberately different from AnimNum.
                    Cellular = new BackdropCellularData(),
                },
            },
        };

        var layers = AlundraBackdropStage.BuildCellularDefinitions(document);

        var layer = Assert.Single(layers);
        Assert.Equal(7, layer.AnimNum); // document.AnimNum, never layer.AnimTimer.
        Assert.Equal(3, layer.AnimTimer); // layer.AnimTimer, never document.AnimNum.
    }

    [Fact]
    public void BuildCellularDefinitions_NonCellularOrNullCellular_IsFiltered()
    {
        var document = new BackdropDocument
        {
            Layers = new List<BackdropLayerData>
            {
                new() { LayerId = 0, Mode = "Tiles", Cellular = null },
                new() { LayerId = 1, Mode = "Disabled", Cellular = null },
                new() { LayerId = 1, Mode = "Cellular", Cellular = null }, // guard: null Cellular.
            },
        };

        var layers = AlundraBackdropStage.BuildCellularDefinitions(document);

        Assert.Empty(layers);
    }

    [Fact]
    public void BuildCellularDefinitions_MultipleCellularLayers_OneDefinitionEach_CellsMappedFieldForField()
    {
        var document = new BackdropDocument
        {
            AnimNum = 2,
            Layers = new List<BackdropLayerData>
            {
                new()
                {
                    LayerId = 0,
                    Mode = "Cellular",
                    Ground = true,
                    BlendMode = 3,
                    AnimTimer = 5,
                    DepthOrder = 1,
                    Cellular = new BackdropCellularData
                    {
                        CountBase = 10,
                        AWaveY = 1,
                        AWavePhase = 2,
                        AWaveAmp = 3,
                        BWaveY = 4,
                        BWavePhase = 5,
                        BWaveWeight = 6,
                        Divisions = 1,
                        Cells = new List<BackdropCellData>
                        {
                            new()
                            {
                                PalDex = 2, U0 = 10, V0 = 20, U1 = 30, V1 = 40, Type = 0,
                                X0 = 50, Y0 = 60, CamXNum = 1, CamXDen = 2, CamYNum = 3, CamYDen = 4,
                                DX = 5, PeriodX = 6, DY = 7, PeriodY = 8,
                            },
                        },
                    },
                },
                new()
                {
                    LayerId = 1,
                    Mode = "Cellular",
                    Ground = false,
                    AnimTimer = 0,
                    Cellular = new BackdropCellularData(),
                },
            },
        };

        var layers = AlundraBackdropStage.BuildCellularDefinitions(document);

        Assert.Equal(2, layers.Length);

        var layer0 = layers[0];
        Assert.Equal(0, layer0.LayerId);
        var cell = Assert.Single(layer0.Cells);
        Assert.Equal(2, cell.PalDex);
        Assert.Equal(10, cell.U0);
        Assert.Equal(20, cell.V0);
        Assert.Equal(30, cell.U1);
        Assert.Equal(40, cell.V1);
        Assert.Equal(CellularCellType.Normal, cell.Type);
        Assert.Equal(50, cell.X0);
        Assert.Equal(60, cell.Y0);
        Assert.Equal(1, cell.CamXNum);
        Assert.Equal(2, cell.CamXDen);
        Assert.Equal(3, cell.CamYNum);
        Assert.Equal(4, cell.CamYDen);
        Assert.Equal(5, cell.DX);
        Assert.Equal(6, cell.PeriodX);
        Assert.Equal(7, cell.DY);
        Assert.Equal(8, cell.PeriodY);

        var layer1 = layers[1];
        Assert.Equal(1, layer1.LayerId);
        Assert.Empty(layer1.Cells);
        Assert.False(layer1.Ground);
    }

    /// <summary>Document-level <c>CellularSheetTextureAssetIds</c> reaches every cellular layer's own
    /// definition (shared by both layers, since the sheet and palettes are per map).</summary>
    [Fact]
    public void BuildCellularDefinitions_DocumentLevelSheetIds_ReachEveryLayersDefinition()
    {
        var id0 = Guid.NewGuid();
        var id3 = Guid.NewGuid();
        var document = new BackdropDocument
        {
            CellularSheetTextureAssetIds = new[] { id0.ToString(), null, null, id3.ToString(), null, null, null, null },
            Layers = new List<BackdropLayerData>
            {
                new() { LayerId = 0, Mode = "Cellular", Cellular = new BackdropCellularData() },
                new() { LayerId = 1, Mode = "Cellular", Cellular = new BackdropCellularData() },
            },
        };

        var layers = AlundraBackdropStage.BuildCellularDefinitions(document);

        Assert.Equal(2, layers.Length);
        var expected = new[] { id0, Guid.Empty, Guid.Empty, id3, Guid.Empty, Guid.Empty, Guid.Empty, Guid.Empty };
        Assert.Equal(expected, layers[0].SheetTextureAssetIds);
        Assert.Equal(expected, layers[1].SheetTextureAssetIds);
    }

    [Fact]
    public void BuildCellularDefinitions_NullDocumentLevelSheetIds_ResolvesToEmptyArray()
    {
        var document = new BackdropDocument
        {
            CellularSheetTextureAssetIds = null,
            Layers = new List<BackdropLayerData>
            {
                new() { LayerId = 0, Mode = "Cellular", Cellular = new BackdropCellularData() },
            },
        };

        var layers = AlundraBackdropStage.BuildCellularDefinitions(document);

        Assert.Empty(Assert.Single(layers).SheetTextureAssetIds);
    }

    /// <summary>The Ground/blend policy for a Cellular layer matches EXACTLY what the Tiles path
    /// resolves for the same (Ground, BlendMode) inputs - both call the SAME
    /// <see cref="AlundraBackdropStage.ResolveGroundLayerBlend"/>, no second policy.</summary>
    [Theory]
    [InlineData(true, 1)]
    [InlineData(true, 2)]
    [InlineData(true, 3)]
    [InlineData(true, 4)]
    [InlineData(false, 0)]
    [InlineData(false, 1)]
    public void BuildCellularDefinitions_GroundBlendPolicy_MatchesTheTilesPathForTheSameInputs(bool ground, int blendMode)
    {
        var document = new BackdropDocument
        {
            Layers = new List<BackdropLayerData>
            {
                new()
                {
                    LayerId = 0,
                    Mode = "Cellular",
                    Ground = ground,
                    BlendMode = blendMode,
                    Cellular = new BackdropCellularData(),
                },
            },
        };

        var layers = AlundraBackdropStage.BuildCellularDefinitions(document);
        var layer = Assert.Single(layers);

        var (expectedBlend, expectedTint) = AlundraBackdropStage.ResolveGroundLayerBlend(ground, blendMode);
        Assert.Equal(expectedBlend, layer.Blend);
        Assert.Equal(expectedTint, layer.Tint);
    }

    // -----------------------------------------------------------------------------------------
    // Helpers.
    // -----------------------------------------------------------------------------------------

    private static BackdropDocument LoadRealCompanion(string worldName)
    {
        var projectRoot = AlundraWorldProxyGlobalFreezeTests.FindProjectRoot();
        var document = BackdropLoader.Load(projectRoot, worldName);
        Assert.NotNull(document);
        return document!;
    }
}
