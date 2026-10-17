#nullable enable
using System.Collections.Generic;
using Alundra.Events;

namespace Alundra.Rendering.Backdrops;

/// <summary>
/// One of a map's (up to) two scrolling background layers - runtime-side mirror of
/// <c>AlundraCasaEngineProjectConverter.Readers.BackdropLayerDocument</c>. <see cref="Cellular"/>
/// (mode 2 raw parameters, D-E9d) is now modeled field for field - <see cref="BackdropStage"/>
/// translates it into an engine-side <c>CellularLayerDefinition</c>.
/// </summary>
public sealed class BackdropLayerData
{
    public int LayerId { get; set; }
    public string Mode { get; set; } = "Disabled";
    public int DepthOrder { get; set; }
    public bool Ground { get; set; }
    public int BlendMode { get; set; }
    public int AnimTimer { get; set; }
    public BackdropScrollarData? Scrollar { get; set; }
    public BackdropCellularData? Cellular { get; set; }
    public string? TextureAssetId { get; set; }

    /// <summary>
    /// One texture id per V-animation frame (docs/plan-e9-backdrops-residus.md D-E9-2/D-E9-3/D-E9-5) -
    /// mirror of <c>AlundraCasaEngineProjectConverter.Readers.BackdropLayerDocument.FrameTextureAssetIds</c>.
    /// <c>[0]</c> is always equal to <see cref="TextureAssetId"/>. Absent from a companion's JSON for
    /// every non-animated layer (the converter omits it via <c>JsonIgnoreCondition.WhenWritingNull</c>),
    /// which <see cref="System.Text.Json.JsonSerializer"/> leaves as <see langword="null"/> here - see
    /// <see cref="BackdropStage.ResolveFrameAssetIds"/> for the fallback this resolves to.
    /// </summary>
    public string[]? FrameTextureAssetIds { get; set; }

    public int Width { get; set; }
    public int Height { get; set; }
}
