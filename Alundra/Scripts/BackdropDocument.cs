#nullable enable
using System.Collections.Generic;

namespace Alundra.Scripts;

/// <summary>
/// A mode-1 ("Tiles") layer's parallax/auto-scroll parameters, mirrored field-for-field from
/// <c>AlundraCasaEngineProjectConverter.Readers.BackdropScrollarDocument</c> (see
/// <c>docs/formats/backdrops.md</c>): camera parallax is <c>cameraX * FactorXNum / FactorXDenom</c>
/// (and the Y equivalent), auto-scroll advances <c>ScrollXSpeed</c> per tick plus one extra pixel
/// every <c>|ScrollXPeriod|</c> ticks - see
/// <c>CasaEngine.Framework.Rendering.ScrollingLayers.ScrollingLayerService</c> (parallax and
/// auto-scroll advance, engine-side since E9.b) for the actual formulas.
/// </summary>
public sealed class BackdropScrollarData
{
    public int FactorXNum { get; set; }
    public int FactorXDenom { get; set; }
    public int FactorYNum { get; set; }
    public int FactorYDenom { get; set; }
    public int ScrollXSpeed { get; set; }
    public int ScrollXPeriod { get; set; }
    public int ScrollYSpeed { get; set; }
    public int ScrollYPeriod { get; set; }
}

/// <summary>
/// One cell of a Cellular-mode (mode 2) scroll layer - runtime-side mirror of
/// <c>AlundraCasaEngineProjectConverter.Readers.BackdropCellDocument</c>, field for field. See
/// <c>CasaEngine.Framework.Rendering.CellularLayers.CellularCellDefinition</c> (D-E9d) for what each
/// field drives.
/// </summary>
public sealed class BackdropCellData
{
    public int PalDex { get; set; }
    public int U0 { get; set; }
    public int V0 { get; set; }
    public int U1 { get; set; }
    public int V1 { get; set; }
    public int Type { get; set; }
    public int X0 { get; set; }
    public int Y0 { get; set; }
    public int CamXNum { get; set; }
    public int CamXDen { get; set; }
    public int CamYNum { get; set; }
    public int CamYDen { get; set; }
    public int DX { get; set; }
    public int PeriodX { get; set; }
    public int DY { get; set; }
    public int PeriodY { get; set; }
}

/// <summary>
/// A mode-2 ("Cellular") layer's shared wave-shimmer coefficients plus its cells - runtime-side mirror
/// of <c>AlundraCasaEngineProjectConverter.Readers.BackdropCellularDocument</c>, field for field. See
/// <c>CasaEngine.Framework.Rendering.CellularLayers.CellularLayerDefinition</c> (D-E9d) for what each
/// field drives; <see cref="AWaveY"/>/<see cref="AWavePhase"/>/<see cref="AWaveAmp"/>/<see cref="BWaveY"/>/
/// <see cref="BWavePhase"/>/<see cref="BWaveWeight"/> feed the <c>WaveX</c> cell type's formula, which
/// also consumes the map-level <see cref="BackdropDocument.WaveLut"/> table.
/// </summary>
public sealed class BackdropCellularData
{
    public int CountBase { get; set; }
    public int AWaveY { get; set; }
    public int AWavePhase { get; set; }
    public int AWaveAmp { get; set; }
    public int BWaveY { get; set; }
    public int BWavePhase { get; set; }
    public int BWaveWeight { get; set; }
    public int Divisions { get; set; }
    public List<BackdropCellData> Cells { get; set; } = new();
}

/// <summary>
/// One of a map's (up to) two scrolling background layers - runtime-side mirror of
/// <c>AlundraCasaEngineProjectConverter.Readers.BackdropLayerDocument</c>. <see cref="Cellular"/>
/// (mode 2 raw parameters, D-E9d) is now modeled field for field - <see cref="AlundraBackdropStage"/>
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
    /// <see cref="AlundraBackdropStage.ResolveFrameAssetIds"/> for the fallback this resolves to.
    /// </summary>
    public string[]? FrameTextureAssetIds { get; set; }

    public int Width { get; set; }
    public int Height { get; set; }
}

/// <summary>
/// One map's scrolling background - runtime-side mirror of
/// <c>AlundraCasaEngineProjectConverter.Readers.BackdropDocument</c>, read back from the converter's
/// own companion file (<c>Maps/{Zone}/{Name}-{id}/backdrop/{Name}-{id}.backdrop.json</c>), the same
/// convention as <see cref="EventProgramDocument"/>/<see cref="MapEventProgramLoader"/>. See
/// <see cref="BackdropLoader"/> for path resolution and <see cref="AlundraBackdropStage"/> (which
/// builds engine-side <c>ScrollingLayerDefinition</c>s/<c>CellularLayerDefinition</c>s from this and
/// pushes ticks to <c>CasaEngine.Framework.Rendering.ScrollingLayers.ScrollingLayerService</c> and
/// <c>CasaEngine.Framework.Rendering.CellularLayers.CellularLayerService</c>) for how this gets drawn.
///
/// <see cref="OverlayEnabled"/>/<see cref="OverlayColorR"/>/G/B mirror the converter's own tint
/// fields (see <c>docs/formats/backdrops.md</c>): a companion written before this feature existed
/// simply has these properties absent from its JSON, which <see cref="System.Text.Json.JsonSerializer"/>
/// leaves at their defaults (<c>false</c>/<c>0</c>) - an old companion therefore loads with the tint
/// disabled rather than failing.
/// </summary>
public sealed class BackdropDocument
{
    public int MapIndex { get; set; }
    public bool Enabled { get; set; }
    public int AnimNum { get; set; }
    public bool OverlayEnabled { get; set; }
    public byte OverlayColorR { get; set; }
    public byte OverlayColorG { get; set; }
    public byte OverlayColorB { get; set; }

    /// <summary>
    /// Per-tick sine-like displacement table (256 entries) consumed by every <c>WaveX</c> cellular
    /// cell (D-E9d) - mirror of <c>AlundraCasaEngineProjectConverter.Readers.BackdropDocument.WaveLut</c>.
    /// Absent from a companion with no Cellular layer, which <see cref="System.Text.Json.JsonSerializer"/>
    /// leaves as <see langword="null"/> here.
    /// </summary>
    public int[]? WaveLut { get; set; }

    /// <summary>
    /// Catalog ids of the whole-256x256-tile-sheet textures baked for a Cellular (mode 2) layer's
    /// cells (D-E9d), one per <c>PalDex</c> actually used by a cell, indexed by that <c>PalDex</c>
    /// (fixed length 8, matching the map's 8 palettes) - null entries are unused palettes. Shared by
    /// both layers of the map, since the tile sheet and palettes are per-map, not per-layer - mirror
    /// of <c>AlundraCasaEngineProjectConverter.Readers.BackdropDocument.CellularSheetTextureAssetIds</c>.
    /// Absent from a companion with no Cellular layer, which <see cref="System.Text.Json.JsonSerializer"/>
    /// leaves as <see langword="null"/> here.
    /// </summary>
    public string?[]? CellularSheetTextureAssetIds { get; set; }

    public List<BackdropLayerData> Layers { get; set; } = new();
}
