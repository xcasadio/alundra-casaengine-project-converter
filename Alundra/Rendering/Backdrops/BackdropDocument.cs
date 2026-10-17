#nullable enable
using System.Collections.Generic;
using Alundra.Events;

namespace Alundra.Rendering.Backdrops;

/// <summary>
/// One map's scrolling background - runtime-side mirror of
/// <c>AlundraCasaEngineProjectConverter.Readers.BackdropDocument</c>, read back from the converter's
/// own companion file (<c>Maps/{Zone}/{Name}-{id}/backdrop/{Name}-{id}.backdrop.json</c>), the same
/// convention as <see cref="EventProgramDocument"/>/<see cref="MapEventProgramLoader"/>. See
/// <see cref="BackdropLoader"/> for path resolution and <see cref="BackdropStage"/> (which
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
    /// The raw <c>BGColorA</c> byte of the map, the blend mode of the overlay tint (E19.g G2d, O-E19-58): 1 average,
    /// 2 additive, 3 subtractive, 4 quarter. Absent from the JSON when 0 and from a companion written before the field
    /// existed, which <see cref="System.Text.Json.JsonSerializer"/> leaves at 0 - read as the average, today's drawing.
    /// </summary>
    public int OverlayBlendMode { get; set; }

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
