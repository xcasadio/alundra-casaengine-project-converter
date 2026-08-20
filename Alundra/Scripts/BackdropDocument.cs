#nullable enable
using System.Collections.Generic;

namespace Alundra.Scripts;

/// <summary>
/// A mode-1 ("Tiles") layer's parallax/auto-scroll parameters, mirrored field-for-field from
/// <c>AlundraCasaEngineProjectConverter.Readers.BackdropScrollarDocument</c> (see
/// <c>docs/formats/backdrops.md</c>): camera parallax is <c>cameraX * FactorXNum / FactorXDenom</c>
/// (and the Y equivalent), auto-scroll advances <c>ScrollXSpeed</c> per tick plus one extra pixel
/// every <c>|ScrollXPeriod|</c> ticks - see <see cref="BackdropOffsetMath"/> for the actual formulas.
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
/// One of a map's (up to) two scrolling background layers - runtime-side mirror of
/// <c>AlundraCasaEngineProjectConverter.Readers.BackdropLayerDocument</c>. Only the fields this
/// renderer actually consumes are modeled; <c>Cellular</c> (mode 2 raw parameters) is intentionally
/// omitted - <see cref="System.Text.Json.JsonSerializer"/> ignores JSON properties with no matching
/// member, so the companion file's "Cellular" object is simply skipped on load. Rendering mode 2
/// ("Cellular") layers is deferred (see <c>docs/formats/backdrops.md</c>).
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
    public string? TextureAssetId { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

/// <summary>
/// One map's scrolling background - runtime-side mirror of
/// <c>AlundraCasaEngineProjectConverter.Readers.BackdropDocument</c>, read back from the converter's
/// own companion file (<c>Maps/{Zone}/{Name}-{id}/backdrop/{Name}-{id}.backdrop.json</c>), the same
/// convention as <see cref="EventProgramDocument"/>/<see cref="MapEventProgramLoader"/>. See
/// <see cref="BackdropLoader"/> for path resolution and <see cref="BackdropRenderer"/> for how this
/// gets drawn.
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
    public List<BackdropLayerData> Layers { get; set; } = new();
}
